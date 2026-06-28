using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DrawBody.Prototype
{
    internal sealed class DirectTcpOnlineBackend : IOnlineBackend
    {
        private const string MessageHello = "hello";
        private const string MessageLobby = "lobby";
        private const string MessageReady = "ready";
        private const string MessageStart = "start";
        private const string MessageState = "state";
        private const string MessageBody = "body";

        private readonly int port;
        private readonly string localPlayerId;
        private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        private readonly List<ClientPeer> peers = new List<ClientPeer>();
        private TcpListener listener;
        private TcpClient client;
        private Thread acceptThread;
        private bool isHost;
        private bool running;

        public DirectTcpOnlineBackend(int port)
        {
            this.port = Mathf.Clamp(port, 1024, 65535);
            localPlayerId = "p-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => localPlayerId;

        public void Initialize()
        {
            SetState(OnlineConnectionState.Offline, null, "Direct TCP backend initialized.");
        }

        public void Login()
        {
            SetState(OnlineConnectionState.Online, null, "Online ready. Host or join a room.");
        }

        public void Tick()
        {
            while (mainThreadActions.TryDequeue(out Action action))
            {
                action?.Invoke();
            }
        }

        public void StartRandomMatch()
        {
            CreateRoom("Random Match", 4, false);
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            CloseSockets();
            isHost = true;
            running = true;

            CurrentLobby = new OnlineLobbyInfo
            {
                LobbyId = GetRoomAddress(),
                RoomName = string.IsNullOrEmpty(roomName) ? "Draw Together" : roomName,
                StageId = "1-1",
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4),
                Mode = OnlineLobbyMode.Room,
                Players = new[] { CreatePlayer(localPlayerId, "You", true, false) }
            };

            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                acceptThread = new Thread(AcceptLoop) { IsBackground = true };
                acceptThread.Start();
                SetState(OnlineConnectionState.InLobby, CurrentLobby, "Room created. Share the ID with your friend.");
            }
            catch (Exception ex)
            {
                CloseSockets();
                SetState(OnlineConnectionState.Error, CurrentLobby, "Failed to host: " + ex.Message);
            }
        }

        public void JoinRoom(string roomId)
        {
            CloseSockets();
            isHost = false;
            running = true;

            string address = NormalizeAddress(roomId);
            string[] parts = address.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int remotePort))
            {
                SetState(OnlineConnectionState.Error, null, "Room ID must be host-ip:port.");
                return;
            }

            try
            {
                client = new TcpClient();
                client.Connect(parts[0], remotePort);
                StartReadLoop(client);
                Send(client, MessageHello, JsonUtility.ToJson(CreatePlayer(localPlayerId, "You", false, false)));

                CurrentLobby = new OnlineLobbyInfo
                {
                    LobbyId = address,
                    RoomName = "Friend Room",
                    StageId = "1-1",
                    MaxPlayers = 4,
                    Mode = OnlineLobbyMode.Room,
                    Players = new[] { CreatePlayer(localPlayerId, "You", false, false) }
                };
                SetState(OnlineConnectionState.InLobby, CurrentLobby, "Joining room...");
            }
            catch (Exception ex)
            {
                CloseSockets();
                SetState(OnlineConnectionState.Error, null, "Failed to join: " + ex.Message);
            }
        }

        public void LeaveLobby()
        {
            CloseSockets();
            CurrentLobby = null;
            SetState(OnlineConnectionState.Online, null, "Left lobby.");
        }

        public void SetReady(bool ready)
        {
            if (CurrentLobby == null)
            {
                return;
            }

            if (isHost)
            {
                SetPlayerReady(localPlayerId, ready);
                BroadcastLobby("Ready changed.");
            }
            else
            {
                Send(client, MessageReady, JsonUtility.ToJson(new ReadyPayload { PlayerId = localPlayerId, Ready = ready }));
            }
        }

        public void StartGame(string stageId)
        {
            if (CurrentLobby == null)
            {
                return;
            }

            if (!isHost)
            {
                SetState(State, CurrentLobby, "Only the host can start.");
                return;
            }

            CurrentLobby.StageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            Broadcast(MessageStart, CurrentLobby.StageId);
            SetState(OnlineConnectionState.Playing, CurrentLobby, "Starting stage " + CurrentLobby.StageId + ".");
        }

        public void SendBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || CurrentLobby == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            bodyData.PlayerId = localPlayerId;
            string payload = JsonUtility.ToJson(bodyData);
            if (isHost)
            {
                Broadcast(MessageBody, payload);
            }
            else
            {
                Send(client, MessageBody, payload);
            }
        }

        public void SendInput(OnlineInputData inputData)
        {
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
            if (playerState == null || CurrentLobby == null || State == OnlineConnectionState.Offline)
            {
                return;
            }

            playerState.PlayerId = localPlayerId;
            string payload = JsonUtility.ToJson(playerState);
            if (isHost)
            {
                Broadcast(MessageState, payload);
            }
            else
            {
                Send(client, MessageState, payload);
            }
        }

        private void AcceptLoop()
        {
            while (running && listener != null)
            {
                try
                {
                    TcpClient accepted = listener.AcceptTcpClient();
                    lock (peers)
                    {
                        peers.Add(new ClientPeer { Client = accepted });
                    }

                    StartReadLoop(accepted);
                }
                catch
                {
                    if (running)
                    {
                        Enqueue(() => SetState(OnlineConnectionState.Error, CurrentLobby, "Accept failed."));
                    }
                }
            }
        }

        private void StartReadLoop(TcpClient tcpClient)
        {
            Thread thread = new Thread(() => ReadLoop(tcpClient)) { IsBackground = true };
            thread.Start();
        }

        private void ReadLoop(TcpClient tcpClient)
        {
            try
            {
                using (StreamReader reader = new StreamReader(tcpClient.GetStream(), Encoding.UTF8))
                {
                    while (running && tcpClient.Connected)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }

                        HandleLine(tcpClient, line);
                    }
                }
            }
            catch
            {
            }

            if (isHost)
            {
                Enqueue(() => RemovePeer(tcpClient));
            }
        }

        private void HandleLine(TcpClient tcpClient, string line)
        {
            int split = line.IndexOf('\t');
            if (split <= 0)
            {
                return;
            }

            string type = line.Substring(0, split);
            string payload = line.Substring(split + 1);
            Enqueue(() => HandleMessage(tcpClient, type, payload));
        }

        private void HandleMessage(TcpClient tcpClient, string type, string payload)
        {
            if (isHost)
            {
                HandleHostMessage(tcpClient, type, payload);
            }
            else
            {
                HandleClientMessage(type, payload);
            }
        }

        private void HandleHostMessage(TcpClient tcpClient, string type, string payload)
        {
            if (type == MessageHello)
            {
                OnlinePlayerInfo player = JsonUtility.FromJson<OnlinePlayerInfo>(payload);
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    return;
                }

                player.IsHost = false;
                player.IsReady = false;
                MarkPeerPlayer(tcpClient, player.PlayerId);
                AddOrReplacePlayer(player);
                BroadcastLobby(player.DisplayName + " joined.");
            }
            else if (type == MessageReady)
            {
                ReadyPayload ready = JsonUtility.FromJson<ReadyPayload>(payload);
                SetPlayerReady(ready.PlayerId, ready.Ready);
                BroadcastLobby("Ready changed.");
            }
            else if (type == MessageState)
            {
                OnlinePlayerState state = JsonUtility.FromJson<OnlinePlayerState>(payload);
                PlayerStateReceived?.Invoke(state);
                Broadcast(MessageState, payload);
            }
            else if (type == MessageBody)
            {
                OnlineBodyData bodyData = JsonUtility.FromJson<OnlineBodyData>(payload);
                BodyDataReceived?.Invoke(bodyData);
                Broadcast(MessageBody, payload);
            }
        }

        private void HandleClientMessage(string type, string payload)
        {
            if (type == MessageLobby)
            {
                CurrentLobby = JsonUtility.FromJson<OnlineLobbyInfo>(payload);
                SetState(OnlineConnectionState.InLobby, CurrentLobby, "Lobby updated.");
            }
            else if (type == MessageStart)
            {
                if (CurrentLobby == null)
                {
                    CurrentLobby = new OnlineLobbyInfo();
                }

                CurrentLobby.StageId = string.IsNullOrEmpty(payload) ? "1-1" : payload;
                SetState(OnlineConnectionState.Playing, CurrentLobby, "Starting stage " + CurrentLobby.StageId + ".");
            }
            else if (type == MessageState)
            {
                OnlinePlayerState state = JsonUtility.FromJson<OnlinePlayerState>(payload);
                PlayerStateReceived?.Invoke(state);
            }
            else if (type == MessageBody)
            {
                OnlineBodyData bodyData = JsonUtility.FromJson<OnlineBodyData>(payload);
                BodyDataReceived?.Invoke(bodyData);
            }
        }

        private void BroadcastLobby(string message)
        {
            Broadcast(MessageLobby, JsonUtility.ToJson(CurrentLobby));
            SetState(OnlineConnectionState.InLobby, CurrentLobby, message);
        }

        private void Broadcast(string type, string payload)
        {
            lock (peers)
            {
                for (int i = peers.Count - 1; i >= 0; i--)
                {
                    if (!Send(peers[i].Client, type, payload))
                    {
                        peers.RemoveAt(i);
                    }
                }
            }
        }

        private static bool Send(TcpClient tcpClient, string type, string payload)
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(type + "\t" + (payload ?? string.Empty) + "\n");
                tcpClient.GetStream().Write(bytes, 0, bytes.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AddOrReplacePlayer(OnlinePlayerInfo player)
        {
            List<OnlinePlayerInfo> players = new List<OnlinePlayerInfo>(CurrentLobby.Players ?? Array.Empty<OnlinePlayerInfo>());
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId == player.PlayerId)
                {
                    players[i] = player;
                    CurrentLobby.Players = players.ToArray();
                    return;
                }
            }

            if (players.Count < CurrentLobby.MaxPlayers)
            {
                players.Add(player);
                CurrentLobby.Players = players.ToArray();
            }
        }

        private void SetPlayerReady(string playerId, bool ready)
        {
            if (CurrentLobby == null || CurrentLobby.Players == null)
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

        private void MarkPeerPlayer(TcpClient tcpClient, string playerId)
        {
            lock (peers)
            {
                for (int i = 0; i < peers.Count; i++)
                {
                    if (peers[i].Client == tcpClient)
                    {
                        peers[i].PlayerId = playerId;
                        return;
                    }
                }
            }
        }

        private void RemovePeer(TcpClient tcpClient)
        {
            string playerId = null;
            lock (peers)
            {
                for (int i = peers.Count - 1; i >= 0; i--)
                {
                    if (peers[i].Client == tcpClient)
                    {
                        playerId = peers[i].PlayerId;
                        peers.RemoveAt(i);
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(playerId) && CurrentLobby != null && CurrentLobby.Players != null)
            {
                List<OnlinePlayerInfo> players = new List<OnlinePlayerInfo>(CurrentLobby.Players);
                players.RemoveAll(p => p.PlayerId == playerId);
                CurrentLobby.Players = players.ToArray();
                BroadcastLobby("Player left.");
            }
        }

        private void CloseSockets()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            try { client?.Close(); } catch { }
            listener = null;
            client = null;

            lock (peers)
            {
                for (int i = 0; i < peers.Count; i++)
                {
                    try { peers[i].Client?.Close(); } catch { }
                }

                peers.Clear();
            }
        }

        private void SetState(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            State = state;
            StateChanged?.Invoke(state, lobby, message);
        }

        private void Enqueue(Action action)
        {
            mainThreadActions.Enqueue(action);
        }

        private string GetRoomAddress()
        {
            return GetLocalIPv4() + ":" + port;
        }

        private static string GetLocalIPv4()
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                for (int i = 0; i < host.AddressList.Length; i++)
                {
                    IPAddress address = host.AddressList[i];
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch
            {
            }

            return "127.0.0.1";
        }

        private static string NormalizeAddress(string roomId)
        {
            string address = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
            if (string.IsNullOrEmpty(address) || address == "ABC123")
            {
                address = "127.0.0.1:7777";
            }

            if (!address.Contains(":"))
            {
                address += ":7777";
            }

            return address;
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

        [Serializable]
        private sealed class ReadyPayload
        {
            public string PlayerId;
            public bool Ready;
        }

        private sealed class ClientPeer
        {
            public TcpClient Client;
            public string PlayerId;
        }
    }
}
