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
        private const string MessageCarry = "carry";
        private const string MessageStageSelect = "stage_select";
        private const string MessageGimmick = "gimmick";
        private const string RoomCodeAttributeKey = "roomCode";
        private const string RoomCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int RoomCodeMaxCreateAttempts = 5;

        private readonly List<ProductUserId> peers = new List<ProductUserId>();
        private readonly Queue<Action> mainThreadActions = new Queue<Action>();
        private LobbyInterface lobbyInterface;
        private P2PInterface p2pInterface;
        private ConnectInterface connectInterface;
        private ProductUserId localUserId;
        private string lobbyId;
        private bool isHost;
        private bool triedCreateDeviceId;
        private bool shuttingDown;
        private ulong lobbyMemberStatusNotificationId;
        private ulong p2pConnectionRequestNotificationId;
        private SocketId socketId;

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
            lobbyId = null;
            CurrentLobby = null;
            localUserId = null;
            lobbyInterface = null;
            p2pInterface = null;
            connectInterface = null;
            isHost = false;
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
            peers.Clear();
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
            peers.Clear();
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
            lobbyId = null;
            CurrentLobby = null;
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
            Broadcast(MessageStart, CurrentLobby.StageId);
            SetState(OnlineConnectionState.Playing, CurrentLobby, LocalizationManager.Format("online_starting_stage", CurrentLobby.StageId));
        }

        public void OpenStageSelect()
        {
            if (!isHost || CurrentLobby == null)
            {
                return;
            }

            Broadcast(MessageStageSelect, "open");
            SetState(State, CurrentLobby, LocalizationManager.T("online_stage_select_opened"));
        }

        public void CloseStageSelect()
        {
            if (!isHost || CurrentLobby == null)
            {
                return;
            }

            Broadcast(MessageStageSelect, "close");
            SetState(State, CurrentLobby, LocalizationManager.T("online_stage_select_closed"));
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
                Broadcast(MessageBody, payload);
            }
            else
            {
                SendToHost(MessageBody, payload);
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
            if (isHost)
            {
                Broadcast(MessageState, JsonUtility.ToJson(playerState));
            }
            else
            {
                SendToHost(MessageState, JsonUtility.ToJson(playerState));
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
            string payload = JsonUtility.ToJson(gimmickData);
            if (isHost)
            {
                Broadcast(MessageGimmick, payload);
            }
            else
            {
                SendToHost(MessageGimmick, payload);
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
            peers.Clear();

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
                if (!local)
                {
                    peers.Add(member);
                    AcceptPeer(member);
                }

                players.Add(CreatePlayer(member.ToString(), local ? LocalizationManager.T("online_player_you") : LocalizationManager.Format("online_player_number", i + 1), host, local && IsLocalReady()));
            }

            if (CurrentLobby == null)
            {
                CurrentLobby = CreateLobbyInfo(lobbyId, isHost ? LocalizationManager.T("multi_default_room_name") : LocalizationManager.T("multi_friend_room_name"), 4);
            }

            CurrentLobby.Players = players.ToArray();
            SetState(OnlineConnectionState.InLobby, CurrentLobby, message);
            details.Release();
        }

        private void PumpP2P()
        {
            if (p2pInterface == null || localUserId == null)
            {
                return;
            }

            for (int i = 0; i < 12; i++)
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
                    SetPlayerReady(parts[0], parts[1] == "1");
                    SetState(State, CurrentLobby, LocalizationManager.T("online_ready_changed"));
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
            else if (type == MessageState)
            {
                OnlinePlayerState state = JsonUtility.FromJson<OnlinePlayerState>(payload);
                PlayerStateReceived?.Invoke(state);
                if (isHost)
                {
                    Broadcast(type, payload, peer);
                }
            }
            else if (type == MessageBody)
            {
                OnlineBodyData bodyData = JsonUtility.FromJson<OnlineBodyData>(payload);
                BodyDataReceived?.Invoke(bodyData);
                if (isHost)
                {
                    Broadcast(type, payload, peer);
                }
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
                    Broadcast(type, payload, peer);
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

        private void SendToHost(string type, string payload)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                Send(peers[i], type, payload);
            }
        }

        private void Send(ProductUserId remote, string type, string payload)
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
                Channel = 0,
                Data = new ArraySegment<byte>(bytes),
                AllowDelayedDelivery = true,
                Reliability = PacketReliability.ReliableOrdered,
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
