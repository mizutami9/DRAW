#if !EOS_DISABLE
using System;
using System.Collections.Generic;
using System.Text;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using PlayEveryWare.EpicOnlineServices;
using UnityEngine;

namespace DrawBody.Prototype
{
    internal sealed class EosOnlineBackend : IOnlineBackend
    {
        private const string SocketName = "drawbody";
        private const string MessageReady = "ready";
        private const string MessageStart = "start";
        private const string MessageState = "state";
        private const string MessageBody = "body";
        private const string MessageBodyChunk = "body_chunk";
        private const string MessageCarry = "carry";
        private const string MessageStageSelect = "stage_select";
        private const string MessageSessionSync = "session_sync";
        private const string MessageGimmick = "gimmick";
        private const string RoomCodeAttributeKey = "roomCode";
        private const string RoomCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int RoomCodeMaxCreateAttempts = 5;
        private const int BodyChunkRawBytes = 700;
        private const byte ReliableChannel = 0;
        private const byte RealtimeStateChannel = 1;
        private const byte RealtimeGimmickChannel = 2;
        private const byte BodyChannel = 3;
        private const byte ReliableGimmickChannel = 4;
        private const float LobbyRefreshInterval = 1.5f;
        private const float SessionSyncInterval = 0.75f;
        private const int SessionModeLobby = 0;
        private const int SessionModeStageSelect = 1;
        private const int SessionModePlaying = 2;

        private readonly List<ProductUserId> peers = new List<ProductUserId>();
        private readonly Queue<Action> mainThreadActions = new Queue<Action>();
        private readonly Dictionary<string, bool> readyByPlayerId = new Dictionary<string, bool>();
        private readonly Dictionary<string, float> lastPeerPacketAt = new Dictionary<string, float>();
        private readonly Dictionary<string, BodyChunkAssembly> bodyChunkAssemblies = new Dictionary<string, BodyChunkAssembly>();
        private int nextBodyTransferId;

        private sealed class BodyChunkAssembly
        {
            public byte[][] Chunks;
            public int Received;
        }

        [Serializable]
        private sealed class SessionSyncPayload
        {
            public int Mode;
            public string StageId;
            public int StageRevision;
            public int RetryRevision;
        }
        private LobbyInterface lobbyInterface;
        private P2PInterface p2pInterface;
        private ConnectInterface connectInterface;
        private ProductUserId localUserId;
        private ProductUserId hostPeer;
        private string lobbyId;
        private bool isHost;
        private bool triedCreateDeviceId;
        private bool shuttingDown;
        private ulong lobbyMemberStatusNotificationId;
        private ulong lobbyUpdateNotificationId;
        private ulong p2pConnectionRequestNotificationId;
        private SocketId socketId;
        private float nextLobbyRefreshAt;
        private float nextSessionSyncAt;
        private int sessionMode;
        private int stageRevision;
        private int retryRevision;
        private int lastAppliedSessionMode = -1;
        private int lastAppliedStageRevision = -1;
        private int lastAppliedRetryRevision = -1;

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => localUserId != null ? localUserId.ToString() : "eos-local";

        public void Initialize()
        {
            shuttingDown = false;
            socketId = new SocketId { SocketName = SocketName };
            EnsureEosManager();
            SetState(OnlineConnectionState.Offline, null, LocalizationManager.T("online_eos_initialized"));
        }

        public void Login()
        {
            if (shuttingDown)
            {
                return;
            }

            EnsureEosManager();
            SetState(OnlineConnectionState.LoggingIn, null, LocalizationManager.T("online_eos_login"));

            try
            {
                triedCreateDeviceId = false;
                connectInterface = EOSManager.Instance.GetEOSConnectInterface();
                if (connectInterface == null)
                {
                    SetState(OnlineConnectionState.Error, null, LocalizationManager.T("online_eos_connect_not_ready"));
                    return;
                }

                LoginWithDeviceId();
            }
            catch (Exception ex)
            {
                SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_login_failed", ex.Message));
            }
        }

        public void Tick()
        {
            if (shuttingDown)
            {
                return;
            }

            lock (mainThreadActions)
            {
                while (mainThreadActions.Count > 0)
                {
                    mainThreadActions.Dequeue()?.Invoke();
                }
            }

            PumpP2P();
            TickSessionRecovery();
        }

        public void Shutdown()
        {
            shuttingDown = true;
            RemoveNotifications();
            CloseP2PConnections();

            if (lobbyInterface != null && localUserId != null && !string.IsNullOrEmpty(lobbyId))
            {
                try
                {
                    LeaveLobbyOptions options = new LeaveLobbyOptions { LocalUserId = localUserId, LobbyId = lobbyId };
                    lobbyInterface.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo data) => { });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("EOS lobby cleanup failed: " + ex.Message);
                }
            }

            lock (mainThreadActions)
            {
                mainThreadActions.Clear();
            }

            peers.Clear();
            readyByPlayerId.Clear();
            lastPeerPacketAt.Clear();
            bodyChunkAssemblies.Clear();
            lobbyId = null;
            CurrentLobby = null;
            localUserId = null;
            hostPeer = null;
            lobbyInterface = null;
            p2pInterface = null;
            connectInterface = null;
            isHost = false;
            ResetSessionTracking();
            triedCreateDeviceId = false;
            State = OnlineConnectionState.Offline;
        }

        public void StartRandomMatch()
        {
            CreateRoom(LocalizationManager.T("multi_random_match"), 4, false);
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            if (!RequireLoggedIn())
            {
                return;
            }

            isHost = true;
            hostPeer = null;
            peers.Clear();
            readyByPlayerId.Clear();
            lastPeerPacketAt.Clear();
            ResetSessionTracking();
            TryCreateRoomWithUniqueCode(roomName, maxPlayers, isPrivate, RoomCodeMaxCreateAttempts);
        }

        private void TryCreateRoomWithUniqueCode(string roomName, int maxPlayers, bool isPrivate, int attemptsRemaining)
        {
            string roomCode = GenerateRoomCode();

            FindLobbyIdByRoomCode(roomCode, (searchResult, existingLobbyId) =>
            {
                if (searchResult == Result.Success && !string.IsNullOrEmpty(existingLobbyId) && attemptsRemaining > 1)
                {
                    TryCreateRoomWithUniqueCode(roomName, maxPlayers, isPrivate, attemptsRemaining - 1);
                    return;
                }

                if (searchResult == Result.Success && !string.IsNullOrEmpty(existingLobbyId))
                {
                    SetState(OnlineConnectionState.Error, null, LocalizationManager.T("online_eos_room_code_collision"));
                    return;
                }

                if (searchResult != Result.Success)
                {
                    SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_room_code_search_failed", searchResult));
                    return;
                }

                CreateLobbyOptions options = new CreateLobbyOptions
                {
                    LocalUserId = localUserId,
                    MaxLobbyMembers = (uint)Mathf.Clamp(maxPlayers, 2, 4),
                    PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                    PresenceEnabled = true,
                    AllowInvites = true,
                    BucketId = "drawbody-room",
                    DisableHostMigration = true,
                    EnableJoinById = true
                };

                lobbyInterface.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo data) =>
                {
                    Result resultCode = data.ResultCode;
                    string createdLobbyId = data.LobbyId;
                    Enqueue(() =>
                    {
                        if (resultCode != Result.Success)
                        {
                            SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_create_lobby_failed", resultCode));
                            return;
                        }

                        lobbyId = createdLobbyId;
                        CurrentLobby = CreateLobbyInfo(lobbyId, string.IsNullOrEmpty(roomName) ? LocalizationManager.T("multi_default_room_name") : roomName, Mathf.Clamp(maxPlayers, 2, 4), roomCode);
                        CurrentLobby.Players = new[] { CreatePlayer(LocalPlayerId, LocalizationManager.T("online_player_you"), true, false) };
                        AddRoomCodeAttribute(lobbyId, roomCode, () => RefreshLobbyMembers(LocalizationManager.T("online_eos_room_created")));
                    });
                });
            });
        }

        public void JoinRoom(string roomId)
        {
            if (!RequireLoggedIn())
            {
                return;
            }

            string id = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            if (string.IsNullOrEmpty(id))
            {
                SetState(OnlineConnectionState.Error, null, LocalizationManager.T("online_eos_enter_room_code"));
                return;
            }

            id = id.ToUpperInvariant();
            if (IsRoomCode(id))
            {
                JoinRoomByCode(id);
                return;
            }

            JoinLobbyById(id, string.Empty);
        }

        private void JoinLobbyById(string id, string roomCode)
        {
            isHost = false;
            hostPeer = null;
            peers.Clear();
            readyByPlayerId.Clear();
            lastPeerPacketAt.Clear();
            ResetSessionTracking();
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                LobbyId = id,
                LocalUserId = localUserId,
                PresenceEnabled = true
            };

            lobbyInterface.JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo data) =>
            {
                Result resultCode = data.ResultCode;
                string joinedLobbyId = data.LobbyId;
                Enqueue(() =>
                {
                    if (resultCode != Result.Success)
                    {
                        SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_join_lobby_failed", resultCode));
                        return;
                    }

                    lobbyId = joinedLobbyId;
                    CurrentLobby = CreateLobbyInfo(lobbyId, LocalizationManager.T("multi_friend_room_name"), 4, roomCode);
                    RefreshLobbyMembers(LocalizationManager.T("online_eos_joined_room"));
                    SendToHost(MessageReady, "0");
                });
            });
        }

        public void LeaveLobby()
        {
            if (lobbyInterface != null && localUserId != null && !string.IsNullOrEmpty(lobbyId))
            {
                LeaveLobbyOptions options = new LeaveLobbyOptions { LocalUserId = localUserId, LobbyId = lobbyId };
                lobbyInterface.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo data) => { });
            }

            peers.Clear();
            readyByPlayerId.Clear();
            lastPeerPacketAt.Clear();
            bodyChunkAssemblies.Clear();
            lobbyId = null;
            hostPeer = null;
            CurrentLobby = null;
            ResetSessionTracking();
            SetState(OnlineConnectionState.Online, null, LocalizationManager.T("online_eos_left_lobby"));
        }

        public void SetReady(bool ready)
        {
            if (CurrentLobby == null)
            {
                return;
            }

            SetLocalReady(ready);
            if (isHost)
            {
                Broadcast(MessageReady, LocalPlayerId + "|" + (ready ? "1" : "0"));
                SetState(State, CurrentLobby, ready ? LocalizationManager.T("online_ready_on") : LocalizationManager.T("online_ready_off"));
            }
            else
            {
                SendToHost(MessageReady, LocalPlayerId + "|" + (ready ? "1" : "0"));
            }
        }

        public void StartGame(string stageId)
        {
            if (!isHost)
            {
                SetState(State, CurrentLobby, LocalizationManager.T("multi_host_only_start"));
                return;
            }

            if (CurrentLobby == null)
            {
                return;
            }

            CurrentLobby.StageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            sessionMode = SessionModePlaying;
            stageRevision++;
            CurrentLobby.StageRevision = stageRevision;
            CurrentLobby.RetryRevision = retryRevision;
            BroadcastSessionSync();
            SetState(OnlineConnectionState.Playing, CurrentLobby, LocalizationManager.Format("online_starting_stage", CurrentLobby.StageId));
        }

        public void OpenStageSelect()
        {
            if (!isHost || CurrentLobby == null)
            {
                return;
            }

            sessionMode = SessionModeStageSelect;
            BroadcastSessionSync();
            SetState(State, CurrentLobby, LocalizationManager.T("online_stage_select_opened"));
        }

        public void CloseStageSelect()
        {
            if (!isHost || CurrentLobby == null)
            {
                return;
            }

            sessionMode = SessionModeLobby;
            BroadcastSessionSync();
            SetState(OnlineConnectionState.InLobby, CurrentLobby, LocalizationManager.T("online_stage_select_closed"));
        }

        public void SendBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            bodyData.PlayerId = LocalPlayerId;
            string payload = JsonUtility.ToJson(bodyData);
            if (isHost)
            {
                BroadcastBody(payload);
            }
            else
            {
                SendBodyToHost(payload);
            }
        }

        public void SendInput(OnlineInputData inputData)
        {
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
            if (playerState == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            playerState.PlayerId = LocalPlayerId;
            string payload = JsonUtility.ToJson(playerState);
            if (isHost)
            {
                BroadcastRealtimeState(payload);
            }
            else
            {
                SendRealtimeStateToHost(payload);
            }
        }

        public void SendCarryData(OnlineCarryData carryData)
        {
            if (carryData == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            carryData.CarrierPlayerId = LocalPlayerId;
            string payload = JsonUtility.ToJson(carryData);
            if (isHost)
            {
                Broadcast(MessageCarry, payload);
            }
            else
            {
                SendToHost(MessageCarry, payload);
            }
        }

        public void SendGimmickData(OnlineGimmickData gimmickData)
        {
            if (gimmickData == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            gimmickData.PlayerId = LocalPlayerId;
            if (isHost && gimmickData.Kind == "stage_retry")
            {
                retryRevision++;
                if (CurrentLobby != null) CurrentLobby.RetryRevision = retryRevision;
                sessionMode = SessionModePlaying;
                BroadcastSessionSync();
                return;
            }
            string payload = JsonUtility.ToJson(gimmickData);
            bool realtimeTransform = gimmickData.Kind == "transform";
            if (isHost && realtimeTransform)
            {
                BroadcastRealtimeGimmick(payload);
            }
            else if (!isHost && realtimeTransform)
            {
                SendRealtimeGimmickToHost(payload);
            }
            else if (isHost)
            {
                BroadcastReliableGimmick(payload);
            }
            else
            {
                SendReliableGimmickToHost(payload);
            }
        }

        private void LoginWithDeviceId()
        {
            Credentials credentials = new Credentials
            {
                Type = ExternalCredentialType.DeviceidAccessToken
            };
            UserLoginInfo userLoginInfo = new UserLoginInfo
            {
                DisplayName = SystemInfo.deviceName
            };
            LoginOptions options = new LoginOptions
            {
                Credentials = credentials,
                UserLoginInfo = userLoginInfo
            };

            connectInterface.Login(ref options, null, OnConnectLogin);
        }

        private void CreateDeviceId()
        {
            CreateDeviceIdOptions options = new CreateDeviceIdOptions
            {
                DeviceModel = "Windows PC"
            };
            connectInterface.CreateDeviceId(ref options, null, (ref CreateDeviceIdCallbackInfo data) =>
            {
                Result resultCode = data.ResultCode;
                Enqueue(() =>
                {
                    if (resultCode != Result.Success && resultCode != Result.DuplicateNotAllowed)
                    {
                        SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_device_create_failed", resultCode));
                        return;
                    }

                    LoginWithDeviceId();
                });
            });
        }

        private void AddRoomCodeAttribute(string targetLobbyId, string roomCode, Action onComplete)
        {
            UpdateLobbyModificationOptions modificationOptions = new UpdateLobbyModificationOptions
            {
                LocalUserId = localUserId,
                LobbyId = targetLobbyId
            };

            Result modificationResult = lobbyInterface.UpdateLobbyModification(ref modificationOptions, out LobbyModification modification);
            if (modificationResult != Result.Success || modification == null)
            {
                SetState(OnlineConnectionState.Error, CurrentLobby, LocalizationManager.Format("online_eos_room_code_failed", modificationResult));
                return;
            }

            AttributeData data = new AttributeData
            {
                Key = RoomCodeAttributeKey,
                Value = roomCode
            };
            LobbyModificationAddAttributeOptions attributeOptions = new LobbyModificationAddAttributeOptions
            {
                Attribute = data,
                Visibility = LobbyAttributeVisibility.Public
            };

            Result attributeResult = modification.AddAttribute(ref attributeOptions);
            if (attributeResult != Result.Success)
            {
                modification.Release();
                SetState(OnlineConnectionState.Error, CurrentLobby, LocalizationManager.Format("online_eos_room_code_failed", attributeResult));
                return;
            }

            UpdateLobbyOptions updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = modification };
            lobbyInterface.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo data) =>
            {
                Result resultCode = data.ResultCode;
                Enqueue(() =>
                {
                    modification.Release();
                    if (resultCode != Result.Success)
                    {
                        SetState(OnlineConnectionState.Error, CurrentLobby, LocalizationManager.Format("online_eos_room_code_failed", resultCode));
                        return;
                    }

                    onComplete?.Invoke();
                });
            });
        }

        private void JoinRoomByCode(string roomCode)
        {
            FindLobbyIdByRoomCode(roomCode, (resultCode, foundLobbyId) =>
            {
                if (resultCode != Result.Success)
                {
                    SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_room_code_search_failed", resultCode));
                    return;
                }

                if (string.IsNullOrEmpty(foundLobbyId))
                {
                    SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_room_code_not_found", roomCode));
                    return;
                }

                JoinLobbyById(foundLobbyId, roomCode);
            });
        }

        private void FindLobbyIdByRoomCode(string roomCode, Action<Result, string> completed)
        {
            CreateLobbySearchOptions searchOptions = new CreateLobbySearchOptions { MaxResults = 10 };
            Result createSearchResult = lobbyInterface.CreateLobbySearch(ref searchOptions, out LobbySearch search);
            if (createSearchResult != Result.Success || search == null)
            {
                completed?.Invoke(createSearchResult, string.Empty);
                return;
            }

            AttributeData parameter = new AttributeData
            {
                Key = RoomCodeAttributeKey,
                Value = roomCode
            };
            LobbySearchSetParameterOptions parameterOptions = new LobbySearchSetParameterOptions
            {
                Parameter = parameter,
                ComparisonOp = ComparisonOp.Equal
            };

            Result parameterResult = search.SetParameter(ref parameterOptions);
            if (parameterResult != Result.Success)
            {
                search.Release();
                completed?.Invoke(parameterResult, string.Empty);
                return;
            }

            LobbySearchFindOptions findOptions = new LobbySearchFindOptions { LocalUserId = localUserId };
            search.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo data) =>
            {
                Result resultCode = data.ResultCode;
                Enqueue(() =>
                {
                    if (resultCode != Result.Success)
                    {
                        search.Release();
                        completed?.Invoke(resultCode, string.Empty);
                        return;
                    }

                    LobbySearchGetSearchResultCountOptions countOptions = new LobbySearchGetSearchResultCountOptions();
                    uint count = search.GetSearchResultCount(ref countOptions);
                    if (count == 0)
                    {
                        search.Release();
                        completed?.Invoke(Result.Success, string.Empty);
                        return;
                    }

                    LobbySearchCopySearchResultByIndexOptions copyOptions = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
                    Result copyResult = search.CopySearchResultByIndex(ref copyOptions, out LobbyDetails details);
                    if (copyResult != Result.Success || details == null)
                    {
                        search.Release();
                        completed?.Invoke(copyResult, string.Empty);
                        return;
                    }

                    LobbyDetailsCopyInfoOptions infoOptions = new LobbyDetailsCopyInfoOptions();
                    Result infoResult = details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info);
                    details.Release();
                    search.Release();

                    if (infoResult != Result.Success || info == null || string.IsNullOrEmpty(info.Value.LobbyId))
                    {
                        completed?.Invoke(infoResult, string.Empty);
                        return;
                    }

                    completed?.Invoke(Result.Success, info.Value.LobbyId);
                });
            });
        }

        private void OnConnectLogin(ref LoginCallbackInfo data)
        {
            Result resultCode = data.ResultCode;
            ProductUserId loggedInUserId = data.LocalUserId;
            Enqueue(() =>
            {
                if (resultCode != Result.Success)
                {
                    if (!triedCreateDeviceId)
                    {
                        triedCreateDeviceId = true;
                        SetState(OnlineConnectionState.LoggingIn, null, LocalizationManager.T("online_eos_creating_device_id"));
                        CreateDeviceId();
                        return;
                    }

                    SetState(OnlineConnectionState.Error, null, LocalizationManager.Format("online_eos_device_login_failed", resultCode));
                    return;
                }

                localUserId = loggedInUserId;
                lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
                p2pInterface = EOSManager.Instance.GetEOSP2PInterface();
                RegisterNotifications();
                SetState(OnlineConnectionState.Online, null, LocalizationManager.Format("online_eos_online_as", LocalPlayerId));
            });
        }

        private void RegisterNotifications()
        {
            if (lobbyInterface != null)
            {
                AddNotifyLobbyMemberStatusReceivedOptions memberOptions = new AddNotifyLobbyMemberStatusReceivedOptions();
                lobbyMemberStatusNotificationId = lobbyInterface.AddNotifyLobbyMemberStatusReceived(ref memberOptions, null, (ref LobbyMemberStatusReceivedCallbackInfo data) =>
                {
                    Enqueue(() => RefreshLobbyMembers(LocalizationManager.T("online_lobby_members_updated")));
                });

                AddNotifyLobbyUpdateReceivedOptions updateOptions = new AddNotifyLobbyUpdateReceivedOptions();
                lobbyUpdateNotificationId = lobbyInterface.AddNotifyLobbyUpdateReceived(ref updateOptions, null, (ref LobbyUpdateReceivedCallbackInfo data) =>
                {
                    Enqueue(() => RefreshLobbyMembers(LocalizationManager.T("online_lobby_members_updated")));
                });
            }

            if (p2pInterface != null)
            {
                AddNotifyPeerConnectionRequestOptions requestOptions = new AddNotifyPeerConnectionRequestOptions
                {
                    LocalUserId = localUserId,
                    SocketId = socketId
                };
                p2pConnectionRequestNotificationId = p2pInterface.AddNotifyPeerConnectionRequest(ref requestOptions, null, (ref OnIncomingConnectionRequestInfo data) =>
                {
                    ProductUserId remoteUserId = data.RemoteUserId;
                    Enqueue(() =>
                    {
                        AcceptConnectionOptions accept = new AcceptConnectionOptions
                        {
                            LocalUserId = localUserId,
                            RemoteUserId = remoteUserId,
                            SocketId = socketId
                        };
                        p2pInterface.AcceptConnection(ref accept);
                    });
                });
            }
        }

        private void RemoveNotifications()
        {
            if (lobbyInterface != null && lobbyMemberStatusNotificationId != 0)
            {
                try
                {
                    lobbyInterface.RemoveNotifyLobbyMemberStatusReceived(lobbyMemberStatusNotificationId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("EOS lobby notification cleanup failed: " + ex.Message);
                }

                lobbyMemberStatusNotificationId = 0;
            }

            if (lobbyInterface != null && lobbyUpdateNotificationId != 0)
            {
                try
                {
                    lobbyInterface.RemoveNotifyLobbyUpdateReceived(lobbyUpdateNotificationId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("EOS lobby update notification cleanup failed: " + ex.Message);
                }

                lobbyUpdateNotificationId = 0;
            }

            if (p2pInterface != null && p2pConnectionRequestNotificationId != 0)
            {
                try
                {
                    p2pInterface.RemoveNotifyPeerConnectionRequest(p2pConnectionRequestNotificationId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("EOS P2P notification cleanup failed: " + ex.Message);
                }

                p2pConnectionRequestNotificationId = 0;
            }
        }

        private void CloseP2PConnections()
        {
            if (p2pInterface == null || localUserId == null)
            {
                return;
            }

            try
            {
                CloseConnectionsOptions options = new CloseConnectionsOptions
                {
                    LocalUserId = localUserId,
                    SocketId = socketId
                };
                p2pInterface.CloseConnections(ref options);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("EOS P2P cleanup failed: " + ex.Message);
            }
        }

        private void RefreshLobbyMembers(string message)
        {
            if (lobbyInterface == null || localUserId == null || string.IsNullOrEmpty(lobbyId))
            {
                return;
            }

            CopyLobbyDetailsHandleOptions options = new CopyLobbyDetailsHandleOptions
            {
                LobbyId = lobbyId,
                LocalUserId = localUserId
            };

            Result result = lobbyInterface.CopyLobbyDetailsHandle(ref options, out LobbyDetails details);
            if (result != Result.Success || details == null)
            {
                SetState(State, CurrentLobby, message);
                return;
            }

            LobbyDetailsGetMemberCountOptions countOptions = new LobbyDetailsGetMemberCountOptions();
            uint count = details.GetMemberCount(ref countOptions);
            LobbyDetailsGetLobbyOwnerOptions ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
            ProductUserId owner = details.GetLobbyOwner(ref ownerOptions);
            string ownerId = owner != null ? owner.ToString() : string.Empty;
            List<OnlinePlayerInfo> players = new List<OnlinePlayerInfo>();
            List<ProductUserId> previousPeers = new List<ProductUserId>(peers);
            peers.Clear();
            hostPeer = null;

            for (uint i = 0; i < count; i++)
            {
                LobbyDetailsGetMemberByIndexOptions memberOptions = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                ProductUserId member = details.GetMemberByIndex(ref memberOptions);
                if (member == null)
                {
                    continue;
                }

                bool local = member.ToString() == LocalPlayerId;
                bool host = !string.IsNullOrEmpty(ownerId) ? member.ToString() == ownerId : i == 0;
                if (host && !local)
                {
                    hostPeer = member;
                }
                if (!local)
                {
                    peers.Add(member);
                    AcceptPeer(member);
                }

                string memberId = member.ToString();
                bool ready = readyByPlayerId.TryGetValue(memberId, out bool rememberedReady)
                    && rememberedReady;
                players.Add(CreatePlayer(memberId, local ? LocalizationManager.T("online_player_you") : LocalizationManager.Format("online_player_number", i + 1), host, ready));
            }

            if (isHost && CurrentLobby?.Players != null)
            {
                for (int i = 0; i < previousPeers.Count; i++)
                {
                    ProductUserId previousPeer = previousPeers[i];
                    string previousId = previousPeer != null ? previousPeer.ToString() : string.Empty;
                    if (string.IsNullOrEmpty(previousId)
                        || !lastPeerPacketAt.TryGetValue(previousId, out float heardAt)
                        || Time.unscaledTime - heardAt > 4f)
                    {
                        continue;
                    }

                    bool alreadyListed = false;
                    for (int p = 0; p < players.Count; p++)
                    {
                        if (players[p] != null && players[p].PlayerId == previousId)
                        {
                            alreadyListed = true;
                            break;
                        }
                    }
                    if (alreadyListed)
                    {
                        continue;
                    }

                    peers.Add(previousPeer);
                    for (int p = 0; p < CurrentLobby.Players.Length; p++)
                    {
                        OnlinePlayerInfo previousPlayer = CurrentLobby.Players[p];
                        if (previousPlayer != null && previousPlayer.PlayerId == previousId)
                        {
                            players.Add(previousPlayer);
                            break;
                        }
                    }
                }
            }

            if (CurrentLobby == null)
            {
                CurrentLobby = CreateLobbyInfo(lobbyId, isHost ? LocalizationManager.T("multi_default_room_name") : LocalizationManager.T("multi_friend_room_name"), 4);
            }

            CurrentLobby.Players = players.ToArray();
            OnlineConnectionState refreshedState = sessionMode == SessionModePlaying
                ? OnlineConnectionState.Playing
                : State == OnlineConnectionState.Matching
                    ? OnlineConnectionState.Matching
                    : OnlineConnectionState.InLobby;
            SetState(refreshedState, CurrentLobby, message);
            if (isHost)
            {
                for (int i = 0; i < CurrentLobby.Players.Length; i++)
                {
                    OnlinePlayerInfo player = CurrentLobby.Players[i];
                    Broadcast(MessageReady, player.PlayerId + "|" + (player.IsReady ? "1" : "0"));
                }
                BroadcastSessionSync();
            }
            else if (hostPeer != null)
            {
                SendToHost(MessageReady, LocalPlayerId + "|" + (IsLocalReady() ? "1" : "0"));
            }
            details.Release();
        }

        private void TickSessionRecovery()
        {
            if (string.IsNullOrEmpty(lobbyId) || CurrentLobby == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now >= nextLobbyRefreshAt)
            {
                nextLobbyRefreshAt = now + LobbyRefreshInterval;
                RefreshLobbyMembers(LocalizationManager.T("online_lobby_members_updated"));
            }

            if (now < nextSessionSyncAt)
            {
                return;
            }

            nextSessionSyncAt = now + SessionSyncInterval;
            if (isHost)
            {
                BroadcastSessionSync();
                if (CurrentLobby.Players != null)
                {
                    for (int i = 0; i < CurrentLobby.Players.Length; i++)
                    {
                        OnlinePlayerInfo player = CurrentLobby.Players[i];
                        if (player != null)
                        {
                            Broadcast(MessageReady, player.PlayerId + "|" + (player.IsReady ? "1" : "0"));
                        }
                    }
                }
            }
            else if (hostPeer != null)
            {
                SendToHost(MessageReady, LocalPlayerId + "|" + (IsLocalReady() ? "1" : "0"));
            }
        }

        private void BroadcastSessionSync()
        {
            if (!isHost || CurrentLobby == null)
            {
                return;
            }

            SessionSyncPayload payload = new SessionSyncPayload
            {
                Mode = sessionMode,
                StageId = string.IsNullOrEmpty(CurrentLobby.StageId) ? "1-1" : CurrentLobby.StageId,
                StageRevision = stageRevision,
                RetryRevision = retryRevision
            };
            Broadcast(MessageSessionSync, JsonUtility.ToJson(payload));
        }

        private void ApplySessionSync(ProductUserId peer, string json)
        {
            SessionSyncPayload payload = JsonUtility.FromJson<SessionSyncPayload>(json);
            if (payload == null)
            {
                return;
            }

            if (CurrentLobby == null)
            {
                CurrentLobby = CreateLobbyInfo(lobbyId, LocalizationManager.T("multi_friend_room_name"), 4);
            }
            CurrentLobby.StageId = string.IsNullOrEmpty(payload.StageId) ? "1-1" : payload.StageId;
            CurrentLobby.StageRevision = payload.StageRevision;
            CurrentLobby.RetryRevision = payload.RetryRevision;

            bool modeChanged = payload.Mode != lastAppliedSessionMode;
            bool stageChanged = payload.StageRevision > lastAppliedStageRevision;
            bool retryChanged = lastAppliedRetryRevision >= 0 && payload.RetryRevision > lastAppliedRetryRevision;
            lastAppliedSessionMode = payload.Mode;
            lastAppliedStageRevision = Mathf.Max(lastAppliedStageRevision, payload.StageRevision);
            lastAppliedRetryRevision = Mathf.Max(lastAppliedRetryRevision, payload.RetryRevision);
            sessionMode = payload.Mode;
            stageRevision = Mathf.Max(stageRevision, payload.StageRevision);
            retryRevision = Mathf.Max(retryRevision, payload.RetryRevision);

            if (payload.Mode == SessionModePlaying && (modeChanged || stageChanged || State != OnlineConnectionState.Playing))
            {
                SetState(OnlineConnectionState.Playing, CurrentLobby, LocalizationManager.Format("online_starting_stage", CurrentLobby.StageId));
            }
            else if (payload.Mode == SessionModeStageSelect && modeChanged)
            {
                SetState(OnlineConnectionState.InLobby, CurrentLobby, LocalizationManager.T("online_stage_select_opened"));
            }
            else if (payload.Mode == SessionModeLobby && modeChanged)
            {
                SetState(OnlineConnectionState.InLobby, CurrentLobby, LocalizationManager.T("online_stage_select_closed"));
            }

            if (retryChanged)
            {
                GimmickDataReceived?.Invoke(new OnlineGimmickData
                {
                    PlayerId = peer != null ? peer.ToString() : GetHostPlayerId(),
                    ObjectId = CurrentLobby.StageId,
                    Kind = "stage_retry",
                    Json = "{}"
                });
            }
        }

        private string GetHostPlayerId()
        {
            if (CurrentLobby?.Players != null)
            {
                for (int i = 0; i < CurrentLobby.Players.Length; i++)
                {
                    if (CurrentLobby.Players[i] != null && CurrentLobby.Players[i].IsHost)
                    {
                        return CurrentLobby.Players[i].PlayerId;
                    }
                }
            }
            return hostPeer != null ? hostPeer.ToString() : "eos-host";
        }

        private void ResetSessionTracking()
        {
            nextLobbyRefreshAt = 0f;
            nextSessionSyncAt = 0f;
            sessionMode = SessionModeLobby;
            stageRevision = 0;
            retryRevision = 0;
            lastAppliedSessionMode = -1;
            lastAppliedStageRevision = -1;
            lastAppliedRetryRevision = -1;
        }

        private void PumpP2P()
        {
            if (p2pInterface == null || localUserId == null)
            {
                return;
            }

            // Carried objects add transform traffic. Drain a generous number of
            // packets so movement snapshots never accumulate behind that traffic.
            for (int i = 0; i < 64; i++)
            {
                GetNextReceivedPacketSizeOptions sizeOptions = new GetNextReceivedPacketSizeOptions { LocalUserId = localUserId };
                Result sizeResult = p2pInterface.GetNextReceivedPacketSize(ref sizeOptions, out uint size);
                if (sizeResult != Result.Success || size == 0)
                {
                    return;
                }

                byte[] buffer = new byte[size];
                ProductUserId peer = null;
                SocketId receivedSocket = SocketId.Empty;
                ReceivePacketOptions receiveOptions = new ReceivePacketOptions
                {
                    LocalUserId = localUserId,
                    MaxDataSizeBytes = size
                };

                Result receiveResult = p2pInterface.ReceivePacket(ref receiveOptions, ref peer, ref receivedSocket, out byte channel, new ArraySegment<byte>(buffer), out uint written);
                if (receiveResult != Result.Success)
                {
                    return;
                }

                string line = Encoding.UTF8.GetString(buffer, 0, (int)written);
                HandleMessage(peer, line);
            }
        }

        private void HandleMessage(ProductUserId peer, string line)
        {
            if (peer != null)
            {
                lastPeerPacketAt[peer.ToString()] = Time.unscaledTime;
            }
            int split = line.IndexOf('\t');
            if (split <= 0)
            {
                return;
            }

            string type = line.Substring(0, split);
            string payload = line.Substring(split + 1);
            if (type == MessageReady)
            {
                string[] parts = payload.Split('|');
                if (parts.Length == 2)
                {
                    if (isHost && peer != null)
                    {
                        EnsureSessionPeer(peer);
                    }
                    string readyPlayerId = isHost && peer != null ? peer.ToString() : parts[0];
                    bool ready = parts[1] == "1";
                    SetPlayerReady(readyPlayerId, ready);
                    SetState(State, CurrentLobby, LocalizationManager.T("online_ready_changed"));
                    if (isHost)
                    {
                        Broadcast(MessageReady, readyPlayerId + "|" + (ready ? "1" : "0"), peer);
                    }
                }
            }
            else if (type == MessageStart)
            {
                if (CurrentLobby == null)
                {
                    CurrentLobby = CreateLobbyInfo(lobbyId, LocalizationManager.T("multi_friend_room_name"), 4);
                }

                CurrentLobby.StageId = string.IsNullOrEmpty(payload) ? "1-1" : payload;
                SetState(OnlineConnectionState.Playing, CurrentLobby, LocalizationManager.Format("online_starting_stage", CurrentLobby.StageId));
            }
            else if (type == MessageStageSelect)
            {
                string message = payload == "close"
                    ? LocalizationManager.T("online_stage_select_closed")
                    : LocalizationManager.T("online_stage_select_opened");
                SetState(OnlineConnectionState.InLobby, CurrentLobby, message);
            }
            else if (type == MessageSessionSync)
            {
                ApplySessionSync(peer, payload);
            }
            else if (type == MessageState)
            {
                OnlinePlayerState state = JsonUtility.FromJson<OnlinePlayerState>(payload);
                PlayerStateReceived?.Invoke(state);
                if (isHost)
                {
                    BroadcastRealtimeState(payload, peer);
                }
            }
            else if (type == MessageBody)
            {
                HandleBodyPayload(peer, payload);
            }
            else if (type == MessageBodyChunk)
            {
                HandleBodyChunk(peer, payload);
            }
            else if (type == MessageCarry)
            {
                OnlineCarryData carryData = JsonUtility.FromJson<OnlineCarryData>(payload);
                CarryDataReceived?.Invoke(carryData);
                if (isHost)
                {
                    Broadcast(type, payload, peer);
                }
            }
            else if (type == MessageGimmick)
            {
                OnlineGimmickData gimmickData = JsonUtility.FromJson<OnlineGimmickData>(payload);
                GimmickDataReceived?.Invoke(gimmickData);
                if (isHost)
                {
                    if (gimmickData != null && gimmickData.Kind == "transform")
                    {
                        BroadcastRealtimeGimmick(payload, peer);
                    }
                    else
                    {
                        BroadcastReliableGimmick(payload, peer);
                    }
                }
            }
        }

        private void Broadcast(string type, string payload, ProductUserId except = null)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                if (except != null && peers[i].ToString() == except.ToString())
                {
                    continue;
                }

                Send(peers[i], type, payload);
            }
        }

        private void EnsureSessionPeer(ProductUserId peer)
        {
            if (peer == null || peer.ToString() == LocalPlayerId)
            {
                return;
            }

            lastPeerPacketAt[peer.ToString()] = Time.unscaledTime;

            bool listedPeer = false;
            for (int i = 0; i < peers.Count; i++)
            {
                if (peers[i] != null && peers[i].ToString() == peer.ToString())
                {
                    listedPeer = true;
                    break;
                }
            }
            if (!listedPeer)
            {
                peers.Add(peer);
                AcceptPeer(peer);
            }

            if (CurrentLobby == null)
            {
                return;
            }

            List<OnlinePlayerInfo> players = new List<OnlinePlayerInfo>(CurrentLobby.Players ?? Array.Empty<OnlinePlayerInfo>());
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].PlayerId == peer.ToString())
                {
                    return;
                }
            }

            if (players.Count >= Mathf.Max(2, CurrentLobby.MaxPlayers))
            {
                return;
            }

            bool ready = readyByPlayerId.TryGetValue(peer.ToString(), out bool rememberedReady) && rememberedReady;
            players.Add(CreatePlayer(
                peer.ToString(),
                LocalizationManager.Format("online_player_number", players.Count + 1),
                false,
                ready));
            CurrentLobby.Players = players.ToArray();
        }

        private void BroadcastRealtimeState(string payload, ProductUserId except = null)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                if (except != null && peers[i].ToString() == except.ToString()) continue;
                SendRealtimeState(peers[i], payload);
            }
        }

        private void SendRealtimeStateToHost(string payload)
        {
            if (hostPeer == null) return;
            SendRealtimeState(hostPeer, payload);
        }

        private void SendRealtimeState(ProductUserId remote, string payload)
        {
            Send(remote, MessageState, payload, RealtimeStateChannel, PacketReliability.UnreliableUnordered);
        }

        private void BroadcastRealtimeGimmick(string payload, ProductUserId except = null)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                if (except != null && peers[i].ToString() == except.ToString()) continue;
                Send(peers[i], MessageGimmick, payload, RealtimeGimmickChannel, PacketReliability.UnreliableUnordered);
            }
        }

        private void SendRealtimeGimmickToHost(string payload)
        {
            if (hostPeer == null) return;
            Send(hostPeer, MessageGimmick, payload, RealtimeGimmickChannel, PacketReliability.UnreliableUnordered);
        }

        private void BroadcastReliableGimmick(string payload, ProductUserId except = null)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                if (except != null && peers[i].ToString() == except.ToString()) continue;
                Send(peers[i], MessageGimmick, payload, ReliableGimmickChannel, PacketReliability.ReliableOrdered);
            }
        }

        private void SendReliableGimmickToHost(string payload)
        {
            if (hostPeer == null) return;
            Send(hostPeer, MessageGimmick, payload, ReliableGimmickChannel, PacketReliability.ReliableOrdered);
        }

        private void BroadcastBody(string payload, ProductUserId except = null)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                if (except != null && peers[i].ToString() == except.ToString()) continue;
                SendBodyPayload(peers[i], payload);
            }
        }

        private void SendBodyToHost(string payload)
        {
            if (hostPeer == null) return;
            SendBodyPayload(hostPeer, payload);
        }

        private void SendBodyPayload(ProductUserId remote, string payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
            if (bytes.Length <= BodyChunkRawBytes)
            {
                Send(remote, MessageBody, payload, BodyChannel, PacketReliability.ReliableOrdered);
                return;
            }

            string transferId = LocalPlayerId + "-" + (++nextBodyTransferId).ToString("X8");
            int chunkCount = Mathf.CeilToInt(bytes.Length / (float)BodyChunkRawBytes);
            for (int i = 0; i < chunkCount; i++)
            {
                int offset = i * BodyChunkRawBytes;
                int length = Mathf.Min(BodyChunkRawBytes, bytes.Length - offset);
                string encoded = Convert.ToBase64String(bytes, offset, length);
                Send(
                    remote,
                    MessageBodyChunk,
                    transferId + "|" + i + "|" + chunkCount + "|" + encoded,
                    BodyChannel,
                    PacketReliability.ReliableOrdered);
            }
        }

        private void HandleBodyChunk(ProductUserId peer, string payload)
        {
            string[] parts = payload.Split(new[] { '|' }, 4);
            if (parts.Length != 4
                || !int.TryParse(parts[1], out int index)
                || !int.TryParse(parts[2], out int count)
                || count <= 0 || count > 2048 || index < 0 || index >= count)
            {
                return;
            }
            byte[] chunk;
            try { chunk = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { return; }

            string peerId = peer != null ? peer.ToString() : "unknown";
            string key = peerId + "|" + parts[0];
            if (!bodyChunkAssemblies.TryGetValue(key, out BodyChunkAssembly assembly)
                || assembly.Chunks.Length != count)
            {
                assembly = new BodyChunkAssembly { Chunks = new byte[count][] };
                bodyChunkAssemblies[key] = assembly;
            }
            if (assembly.Chunks[index] == null)
            {
                assembly.Chunks[index] = chunk;
                assembly.Received++;
            }
            if (assembly.Received != count) return;

            int totalLength = 0;
            for (int i = 0; i < count; i++) totalLength += assembly.Chunks[i].Length;
            byte[] combined = new byte[totalLength];
            int writeOffset = 0;
            for (int i = 0; i < count; i++)
            {
                Buffer.BlockCopy(assembly.Chunks[i], 0, combined, writeOffset, assembly.Chunks[i].Length);
                writeOffset += assembly.Chunks[i].Length;
            }
            bodyChunkAssemblies.Remove(key);
            HandleBodyPayload(peer, Encoding.UTF8.GetString(combined));
        }

        private void HandleBodyPayload(ProductUserId peer, string payload)
        {
            OnlineBodyData bodyData = JsonUtility.FromJson<OnlineBodyData>(payload);
            if (bodyData == null || string.IsNullOrEmpty(bodyData.PlayerId)) return;
            BodyDataReceived?.Invoke(bodyData);
            if (isHost) BroadcastBody(payload, peer);
        }

        private void SendToHost(string type, string payload)
        {
            if (hostPeer == null) return;
            Send(hostPeer, type, payload);
        }

        private void Send(ProductUserId remote, string type, string payload)
        {
            Send(remote, type, payload, ReliableChannel, PacketReliability.ReliableOrdered);
        }

        private void Send(ProductUserId remote, string type, string payload, byte channel, PacketReliability reliability)
        {
            if (p2pInterface == null || localUserId == null || remote == null)
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(type + "\t" + (payload ?? string.Empty));
            SendPacketOptions options = new SendPacketOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = remote,
                SocketId = socketId,
                Channel = channel,
                Data = new ArraySegment<byte>(bytes),
                // Realtime snapshots are superseded by the next packet and must
                // not be queued during a connection interruption. Reliable
                // controls/body data still wait for delivery as before.
                AllowDelayedDelivery = reliability != PacketReliability.UnreliableUnordered,
                Reliability = reliability,
                DisableAutoAcceptConnection = false
            };
            p2pInterface.SendPacket(ref options);
        }

        private void AcceptPeer(ProductUserId peer)
        {
            if (p2pInterface == null || peer == null)
            {
                return;
            }

            AcceptConnectionOptions options = new AcceptConnectionOptions
            {
                LocalUserId = localUserId,
                RemoteUserId = peer,
                SocketId = socketId
            };
            p2pInterface.AcceptConnection(ref options);
        }

        private bool RequireLoggedIn()
        {
            if (localUserId == null || lobbyInterface == null || p2pInterface == null)
            {
                SetState(OnlineConnectionState.Error, CurrentLobby, LocalizationManager.T("online_eos_not_logged_in"));
                return false;
            }

            return true;
        }

        private bool IsLocalReady()
        {
            if (CurrentLobby?.Players == null)
            {
                return false;
            }

            for (int i = 0; i < CurrentLobby.Players.Length; i++)
            {
                if (CurrentLobby.Players[i].PlayerId == LocalPlayerId)
                {
                    return CurrentLobby.Players[i].IsReady;
                }
            }

            return false;
        }

        private void SetLocalReady(bool ready)
        {
            SetPlayerReady(LocalPlayerId, ready);
        }

        private void SetPlayerReady(string playerId, bool ready)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }
            readyByPlayerId[playerId] = ready;
            if (CurrentLobby?.Players == null)
            {
                return;
            }

            for (int i = 0; i < CurrentLobby.Players.Length; i++)
            {
                if (CurrentLobby.Players[i].PlayerId == playerId)
                {
                    CurrentLobby.Players[i].IsReady = ready;
                    return;
                }
            }
        }

        private void SetState(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            State = state;
            StateChanged?.Invoke(state, lobby, message);
        }

        private void Enqueue(Action action)
        {
            if (shuttingDown)
            {
                return;
            }

            lock (mainThreadActions)
            {
                mainThreadActions.Enqueue(action);
            }
        }

        private static void EnsureEosManager()
        {
            if (UnityEngine.Object.FindObjectOfType<EOSManager>() == null)
            {
                new GameObject("EOSManager").AddComponent<EOSManager>();
            }
        }

        private static OnlineLobbyInfo CreateLobbyInfo(string id, string name, int maxPlayers)
        {
            return CreateLobbyInfo(id, name, maxPlayers, string.Empty);
        }

        private static OnlineLobbyInfo CreateLobbyInfo(string id, string name, int maxPlayers, string roomCode)
        {
            return new OnlineLobbyInfo
            {
                LobbyId = id,
                RoomCode = roomCode,
                RoomName = name,
                MaxPlayers = maxPlayers,
                StageId = "1-1",
                Mode = OnlineLobbyMode.Room,
                Players = Array.Empty<OnlinePlayerInfo>()
            };
        }

        private static string GenerateRoomCode()
        {
            StringBuilder builder = new StringBuilder(6);
            for (int i = 0; i < 6; i++)
            {
                builder.Append(RoomCodeAlphabet[UnityEngine.Random.Range(0, RoomCodeAlphabet.Length)]);
            }

            return builder.ToString();
        }

        private static bool IsRoomCode(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 6)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 'A' || c > 'Z')
                {
                    return false;
                }
            }

            return true;
        }

        private static OnlinePlayerInfo CreatePlayer(string playerId, string displayName, bool host, bool ready)
        {
            return new OnlinePlayerInfo
            {
                PlayerId = playerId,
                DisplayName = displayName,
                IsHost = host,
                IsReady = ready
            };
        }
    }
}
#else
using System;

namespace DrawBody.Prototype
{
    internal sealed class EosOnlineBackend : IOnlineBackend
    {
        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => "eos-disabled";
        public void Initialize() => SetState(OnlineConnectionState.Error, null, LocalizationManager.T("online_eos_disabled"));
        public void Login() => SetState(OnlineConnectionState.Error, null, LocalizationManager.T("online_eos_disabled"));
        public void Tick() { }
        public void Shutdown() { CurrentLobby = null; State = OnlineConnectionState.Offline; }
        public void StartRandomMatch() { }
        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate) { }
        public void JoinRoom(string roomId) { }
        public void LeaveLobby() { }
        public void SetReady(bool ready) { }
        public void OpenStageSelect() { }
        public void CloseStageSelect() { }
        public void StartGame(string stageId) { }
        public void SendBodyData(OnlineBodyData bodyData) { }
        public void SendInput(OnlineInputData inputData) { }
        public void SendPlayerState(OnlinePlayerState playerState) { }
        public void SendCarryData(OnlineCarryData carryData) { }
        public void SendGimmickData(OnlineGimmickData gimmickData) { }
        private void SetState(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            State = state;
            StateChanged?.Invoke(state, lobby, message);
        }
    }
}
#endif
