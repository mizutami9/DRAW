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

        private readonly List<ProductUserId> peers = new List<ProductUserId>();
        private readonly Queue<Action> mainThreadActions = new Queue<Action>();
        private LobbyInterface lobbyInterface;
        private P2PInterface p2pInterface;
        private ConnectInterface connectInterface;
        private ProductUserId localUserId;
        private string lobbyId;
        private bool isHost;
        private bool triedCreateDeviceId;
        private SocketId socketId;

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => localUserId != null ? localUserId.ToString() : "eos-local";

        public void Initialize()
        {
            socketId = new SocketId { SocketName = SocketName };
            EnsureEosManager();
            SetState(OnlineConnectionState.Offline, null, "EOS backend initialized. Configure EOS Plugin before login.");
        }

        public void Login()
        {
            EnsureEosManager();
            SetState(OnlineConnectionState.LoggingIn, null, "EOS login...");

            try
            {
                triedCreateDeviceId = false;
                connectInterface = EOSManager.Instance.GetEOSConnectInterface();
                if (connectInterface == null)
                {
                    SetState(OnlineConnectionState.Error, null, "EOS Connect interface is not ready.");
                    return;
                }

                LoginWithDeviceId();
            }
            catch (Exception ex)
            {
                SetState(OnlineConnectionState.Error, null, "EOS login failed: " + ex.Message);
            }
        }

        public void Tick()
        {
            lock (mainThreadActions)
            {
                while (mainThreadActions.Count > 0)
                {
                    mainThreadActions.Dequeue()?.Invoke();
                }
            }

            PumpP2P();
        }

        public void StartRandomMatch()
        {
            CreateRoom("Random Match", 4, false);
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            if (!RequireLoggedIn())
            {
                return;
            }

            isHost = true;
            peers.Clear();
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                LocalUserId = localUserId,
                MaxLobbyMembers = (uint)Mathf.Clamp(maxPlayers, 2, 4),
                PermissionLevel = isPrivate ? LobbyPermissionLevel.Inviteonly : LobbyPermissionLevel.Publicadvertised,
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
                        SetState(OnlineConnectionState.Error, null, "EOS create lobby failed: " + resultCode);
                        return;
                    }

                    lobbyId = createdLobbyId;
                    CurrentLobby = CreateLobbyInfo(lobbyId, string.IsNullOrEmpty(roomName) ? "Draw Together" : roomName, 4);
                    CurrentLobby.Players = new[] { CreatePlayer(LocalPlayerId, "You", true, false) };
                    RefreshLobbyMembers("EOS room created. Share this Lobby ID.");
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
                SetState(OnlineConnectionState.Error, null, "Enter an EOS Lobby ID.");
                return;
            }

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
                        SetState(OnlineConnectionState.Error, null, "EOS join lobby failed: " + resultCode);
                        return;
                    }

                    lobbyId = joinedLobbyId;
                    CurrentLobby = CreateLobbyInfo(lobbyId, "Friend Room", 4);
                    RefreshLobbyMembers("Joined EOS room.");
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
            SetState(OnlineConnectionState.Online, null, "Left EOS lobby.");
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
                SetState(State, CurrentLobby, ready ? "Ready." : "Not ready.");
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
                SetState(State, CurrentLobby, "Only the host can start.");
                return;
            }

            if (CurrentLobby == null)
            {
                return;
            }

            CurrentLobby.StageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            Broadcast(MessageStart, CurrentLobby.StageId);
            SetState(OnlineConnectionState.Playing, CurrentLobby, "Starting stage " + CurrentLobby.StageId + ".");
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
                        SetState(OnlineConnectionState.Error, null, "EOS Device ID creation failed: " + resultCode);
                        return;
                    }

                    LoginWithDeviceId();
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
                        SetState(OnlineConnectionState.LoggingIn, null, "Creating EOS Device ID...");
                        CreateDeviceId();
                        return;
                    }

                    SetState(OnlineConnectionState.Error, null, "EOS Device ID login failed: " + resultCode);
                    return;
                }

                localUserId = loggedInUserId;
                lobbyInterface = EOSManager.Instance.GetEOSLobbyInterface();
                p2pInterface = EOSManager.Instance.GetEOSP2PInterface();
                RegisterNotifications();
                SetState(OnlineConnectionState.Online, null, "EOS online as " + LocalPlayerId + ".");
            });
        }

        private void RegisterNotifications()
        {
            if (lobbyInterface != null)
            {
                AddNotifyLobbyMemberStatusReceivedOptions memberOptions = new AddNotifyLobbyMemberStatusReceivedOptions();
                lobbyInterface.AddNotifyLobbyMemberStatusReceived(ref memberOptions, null, (ref LobbyMemberStatusReceivedCallbackInfo data) =>
                {
                    Enqueue(() => RefreshLobbyMembers("Lobby members updated."));
                });
            }

            if (p2pInterface != null)
            {
                AddNotifyPeerConnectionRequestOptions requestOptions = new AddNotifyPeerConnectionRequestOptions
                {
                    LocalUserId = localUserId,
                    SocketId = socketId
                };
                p2pInterface.AddNotifyPeerConnectionRequest(ref requestOptions, null, (ref OnIncomingConnectionRequestInfo data) =>
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

                players.Add(CreatePlayer(member.ToString(), local ? "You" : "Player " + (i + 1), host, local && IsLocalReady()));
            }

            if (CurrentLobby == null)
            {
                CurrentLobby = CreateLobbyInfo(lobbyId, isHost ? "Draw Together" : "Friend Room", 4);
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
                    SetState(State, CurrentLobby, "Ready changed.");
                }
            }
            else if (type == MessageStart)
            {
                if (CurrentLobby == null)
                {
                    CurrentLobby = CreateLobbyInfo(lobbyId, "Friend Room", 4);
                }

                CurrentLobby.StageId = string.IsNullOrEmpty(payload) ? "1-1" : payload;
                SetState(OnlineConnectionState.Playing, CurrentLobby, "Starting stage " + CurrentLobby.StageId + ".");
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
                SetState(OnlineConnectionState.Error, CurrentLobby, "EOS is not logged in. Configure EOS Plugin and login first.");
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
            return new OnlineLobbyInfo
            {
                LobbyId = id,
                RoomName = name,
                MaxPlayers = maxPlayers,
                StageId = "1-1",
                Mode = OnlineLobbyMode.Room,
                Players = Array.Empty<OnlinePlayerInfo>()
            };
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
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => "eos-disabled";
        public void Initialize() => SetState(OnlineConnectionState.Error, null, "EOS is disabled.");
        public void Login() => SetState(OnlineConnectionState.Error, null, "EOS is disabled.");
        public void Tick() { }
        public void StartRandomMatch() { }
        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate) { }
        public void JoinRoom(string roomId) { }
        public void LeaveLobby() { }
        public void SetReady(bool ready) { }
        public void StartGame(string stageId) { }
        public void SendBodyData(OnlineBodyData bodyData) { }
        public void SendInput(OnlineInputData inputData) { }
        public void SendPlayerState(OnlinePlayerState playerState) { }
        private void SetState(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            State = state;
            StateChanged?.Invoke(state, lobby, message);
        }
    }
}
#endif
