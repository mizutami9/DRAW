using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    public enum OnlineBackendMode
    {
        Fake,
        DirectTcp,
        Eos
    }

    public interface IOnlineBackend
    {
        event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        OnlineConnectionState State { get; }
        OnlineLobbyInfo CurrentLobby { get; }
        string LocalPlayerId { get; }
        event Action<OnlinePlayerState> PlayerStateReceived;
        event Action<OnlineBodyData> BodyDataReceived;
        void Initialize();
        void Login();
        void Tick();
        void StartRandomMatch();
        void CreateRoom(string roomName, int maxPlayers, bool isPrivate);
        void JoinRoom(string roomId);
        void LeaveLobby();
        void SetReady(bool ready);
        void StartGame(string stageId);
        void SendBodyData(OnlineBodyData bodyData);
        void SendInput(OnlineInputData inputData);
        void SendPlayerState(OnlinePlayerState playerState);
    }

    public sealed class OnlineManager : MonoBehaviour
    {
        [SerializeField] private OnlineBackendMode backendMode = OnlineBackendMode.Eos;
        [SerializeField] private bool autoLogin = true;
        [SerializeField] private int directTcpPort = 7777;

        private IOnlineBackend backend;

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public OnlineConnectionState State => backend != null ? backend.State : OnlineConnectionState.Offline;
        public OnlineLobbyInfo CurrentLobby => backend != null ? backend.CurrentLobby : null;
        public string LocalPlayerId => backend != null ? backend.LocalPlayerId : string.Empty;

        private void Awake()
        {
            switch (backendMode)
            {
                case OnlineBackendMode.Eos:
                    backend = new EosOnlineBackend();
                    break;
                case OnlineBackendMode.DirectTcp:
                    backend = new DirectTcpOnlineBackend(directTcpPort);
                    break;
                default:
                    backend = new FakeOnlineBackend();
                    break;
            }
            backend.StateChanged += OnBackendStateChanged;
            backend.PlayerStateReceived += OnBackendPlayerStateReceived;
            backend.BodyDataReceived += OnBackendBodyDataReceived;
            backend.Initialize();
        }

        private void Start()
        {
            if (autoLogin)
            {
                Login();
            }
        }

        private void Update()
        {
            backend?.Tick();
        }

        public void Login()
        {
            backend?.Login();
        }

        public void StartRandomMatch()
        {
            backend?.StartRandomMatch();
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            backend?.CreateRoom(roomName, maxPlayers, isPrivate);
        }

        public void JoinRoom(string roomId)
        {
            backend?.JoinRoom(roomId);
        }

        public void LeaveLobby()
        {
            backend?.LeaveLobby();
        }

        public void ToggleReady()
        {
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null || lobby.Players == null || lobby.Players.Length == 0)
            {
                backend?.SetReady(true);
                return;
            }

            bool currentReady = false;
            string localPlayerId = backend != null ? backend.LocalPlayerId : string.Empty;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                if (lobby.Players[i].PlayerId == localPlayerId)
                {
                    currentReady = lobby.Players[i].IsReady;
                    break;
                }
            }

            backend?.SetReady(!currentReady);
        }

        public void StartGame(string stageId)
        {
            backend?.StartGame(stageId);
        }

        public void SendBodyData(OnlineBodyData bodyData)
        {
            backend?.SendBodyData(bodyData);
        }

        public void SendInput(OnlineInputData inputData)
        {
            backend?.SendInput(inputData);
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
            backend?.SendPlayerState(playerState);
        }

        private void OnBackendStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            StateChanged?.Invoke(state, lobby, message);
        }

        private void OnBackendPlayerStateReceived(OnlinePlayerState playerState)
        {
            PlayerStateReceived?.Invoke(playerState);
        }

        private void OnBackendBodyDataReceived(OnlineBodyData bodyData)
        {
            BodyDataReceived?.Invoke(bodyData);
        }
    }

    internal sealed class FakeOnlineBackend : IOnlineBackend
    {
        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => "local";

        public void Initialize()
        {
            SetState(OnlineConnectionState.Offline, null, "Fake backend initialized.");
        }

        public void Login()
        {
            SetState(OnlineConnectionState.LoggingIn, null, "Logging in...");
            SetState(OnlineConnectionState.Online, null, "Online as local test player.");
        }

        public void Tick()
        {
        }

        public void StartRandomMatch()
        {
            CurrentLobby = CreateLobby("RANDOM", "Random Match", OnlineLobbyMode.Random, 4);
            CurrentLobby.Players = new[]
            {
                CreatePlayer("local", "You", true, false),
                CreatePlayer("fake-2", "Player2", false, false)
            };
            SetState(OnlineConnectionState.Matching, CurrentLobby, "Fake random match ready.");
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            CurrentLobby = CreateLobby("ABC123", string.IsNullOrEmpty(roomName) ? "Draw Together" : roomName, OnlineLobbyMode.Room, maxPlayers);
            CurrentLobby.Players = new[] { CreatePlayer("local", "You", true, false) };
            SetState(OnlineConnectionState.InLobby, CurrentLobby, isPrivate ? "Private room created." : "Public room created.");
        }

        public void JoinRoom(string roomId)
        {
            CurrentLobby = CreateLobby(string.IsNullOrEmpty(roomId) ? "ABC123" : roomId, "Friend Room", OnlineLobbyMode.Room, 4);
            CurrentLobby.Players = new[]
            {
                CreatePlayer("host", "Host", true, true),
                CreatePlayer("local", "You", false, false)
            };
            SetState(OnlineConnectionState.InLobby, CurrentLobby, "Joined fake room.");
        }

        public void LeaveLobby()
        {
            CurrentLobby = null;
            SetState(OnlineConnectionState.Online, null, "Left lobby.");
        }

        public void SetReady(bool ready)
        {
            if (CurrentLobby == null || CurrentLobby.Players == null || CurrentLobby.Players.Length == 0)
            {
                return;
            }

            CurrentLobby.Players[0].IsReady = ready;
            SetState(State, CurrentLobby, ready ? "Ready." : "Not ready.");
        }

        public void StartGame(string stageId)
        {
            if (CurrentLobby != null)
            {
                CurrentLobby.StageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            }

            SetState(OnlineConnectionState.Playing, CurrentLobby, "Fake stage start.");
        }

        public void SendBodyData(OnlineBodyData bodyData)
        {
        }

        public void SendInput(OnlineInputData inputData)
        {
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
        }

        private void SetState(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            State = state;
            StateChanged?.Invoke(state, lobby, message);
        }

        private static OnlineLobbyInfo CreateLobby(string lobbyId, string roomName, OnlineLobbyMode mode, int maxPlayers)
        {
            return new OnlineLobbyInfo
            {
                LobbyId = lobbyId,
                RoomName = roomName,
                Mode = mode,
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4),
                StageId = "1-1"
            };
        }

        private static OnlinePlayerInfo CreatePlayer(string playerId, string displayName, bool isHost, bool isReady)
        {
            return new OnlinePlayerInfo
            {
                PlayerId = playerId,
                DisplayName = displayName,
                IsHost = isHost,
                IsReady = isReady
            };
        }
    }
}
