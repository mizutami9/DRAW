using System;
using System.Collections.Generic;
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
        event Action<OnlineCarryData> CarryDataReceived;
        event Action<OnlineGimmickData> GimmickDataReceived;
        void Initialize();
        void Login();
        void Tick();
        void Shutdown();
        void StartRandomMatch();
        void CreateRoom(string roomName, int maxPlayers, bool isPrivate);
        void JoinRoom(string roomId);
        void LeaveLobby();
        void SetReady(bool ready);
        void OpenStageSelect();
        void CloseStageSelect();
        void StartGame(string stageId);
        void SendBodyData(OnlineBodyData bodyData);
        void SendInput(OnlineInputData inputData);
        void SendPlayerState(OnlinePlayerState playerState);
        void SendCarryData(OnlineCarryData carryData);
        void SendGimmickData(OnlineGimmickData gimmickData);
    }

    public sealed class OnlineManager : MonoBehaviour
    {
        [SerializeField] private OnlineBackendMode backendMode = OnlineBackendMode.Eos;
        [SerializeField] private bool autoLogin = true;
        [SerializeField] private bool allowEosInEditor = false;
        [SerializeField] private int directTcpPort = 7777;

        private IOnlineBackend backend;
        private OnlineBackendMode effectiveBackendMode;
        private bool shuttingDown;
        private readonly Dictionary<string, float> confirmedInkByPlayer = new Dictionary<string, float>();
        private string knownHostPlayerId;

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        public OnlineConnectionState State => backend != null ? backend.State : OnlineConnectionState.Offline;
        public OnlineLobbyInfo CurrentLobby => backend != null ? backend.CurrentLobby : null;
        public string LocalPlayerId => backend != null ? backend.LocalPlayerId : string.Empty;
        public OnlineBackendMode EffectiveBackendMode => effectiveBackendMode;

        public int GetInkBudgetPlayerCount()
        {
            OnlinePlayerInfo[] players = CurrentLobby != null ? CurrentLobby.Players : null;
            if (players == null)
            {
                return 1;
            }

            int count = 0;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId))
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
        }

        public float GetConfirmedInkExcludingLocal()
        {
            string localPlayerId = LocalPlayerId;
            float total = 0f;
            OnlinePlayerInfo[] players = CurrentLobby != null ? CurrentLobby.Players : null;
            if (players == null)
            {
                return 0f;
            }

            for (int i = 0; i < players.Length; i++)
            {
                OnlinePlayerInfo player = players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId) || player.PlayerId == localPlayerId)
                {
                    continue;
                }

                if (confirmedInkByPlayer.TryGetValue(player.PlayerId, out float ink))
                {
                    total += ink;
                }
            }

            return total;
        }

        public bool IsHostPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            OnlinePlayerInfo[] players = CurrentLobby?.Players;
            if (players != null)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    OnlinePlayerInfo player = players[i];
                    if (player == null || !player.IsHost || string.IsNullOrEmpty(player.PlayerId)) continue;
                    knownHostPlayerId = player.PlayerId;
                    break;
                }
            }
            return playerId == knownHostPlayerId;
        }

        private void Awake()
        {
            effectiveBackendMode = GetEffectiveBackendMode();
            switch (effectiveBackendMode)
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
            backend.CarryDataReceived += OnBackendCarryDataReceived;
            backend.GimmickDataReceived += OnBackendGimmickDataReceived;
            backend.Initialize();
        }

        private void Start()
        {
            if (shuttingDown)
            {
                return;
            }

            if (autoLogin)
            {
                Login();
            }
        }

        private void Update()
        {
            if (!shuttingDown)
            {
                backend?.Tick();
            }
        }

        private void OnDestroy()
        {
            ShutdownBackend();
        }

        private void OnApplicationQuit()
        {
            ShutdownBackend();
        }

        private OnlineBackendMode GetEffectiveBackendMode()
        {
#if UNITY_EDITOR
            if (backendMode == OnlineBackendMode.Eos && !allowEosInEditor)
            {
                Debug.Log("OnlineManager: EOS backend is disabled in the Unity Editor for stable repeated Play Mode. Enable Allow Eos In Editor on OnlineManager when you need to test EOS in-editor. Builds still use the selected backend.");
                return OnlineBackendMode.Fake;
            }
#endif
            return backendMode;
        }

        private void ShutdownBackend()
        {
            if (shuttingDown)
            {
                return;
            }

            shuttingDown = true;
            if (backend == null)
            {
                return;
            }

            backend.StateChanged -= OnBackendStateChanged;
            backend.PlayerStateReceived -= OnBackendPlayerStateReceived;
            backend.BodyDataReceived -= OnBackendBodyDataReceived;
            backend.CarryDataReceived -= OnBackendCarryDataReceived;
            backend.GimmickDataReceived -= OnBackendGimmickDataReceived;
            backend.Shutdown();
            backend = null;
        }

        public void Login()
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.Login();
        }

        public void StartRandomMatch()
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.StartRandomMatch();
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.CreateRoom(roomName, maxPlayers, isPrivate);
        }

        public void JoinRoom(string roomId)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.JoinRoom(roomId);
        }

        public void LeaveLobby()
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.LeaveLobby();
        }

        public void ToggleReady()
        {
            if (shuttingDown)
            {
                return;
            }

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
            if (shuttingDown)
            {
                return;
            }

            backend?.StartGame(stageId);
        }

        public void OpenStageSelect()
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.OpenStageSelect();
        }

        public void CloseStageSelect()
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.CloseStageSelect();
        }

        public void SendBodyData(OnlineBodyData bodyData)
        {
            if (shuttingDown || bodyData == null)
            {
                return;
            }

            bodyData.PlayerId = string.IsNullOrEmpty(LocalPlayerId) ? "local" : LocalPlayerId;
            CacheConfirmedInk(bodyData);
            backend?.SendBodyData(bodyData);
        }

        public void SendInput(OnlineInputData inputData)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.SendInput(inputData);
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.SendPlayerState(playerState);
        }

        public void SendCarryData(OnlineCarryData carryData)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.SendCarryData(carryData);
        }

        public void SendGimmickData(OnlineGimmickData gimmickData)
        {
            if (shuttingDown)
            {
                return;
            }

            backend?.SendGimmickData(gimmickData);
        }

        private void OnBackendStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            if (lobby?.Players != null)
            {
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    if (lobby.Players[i] != null && lobby.Players[i].IsHost
                        && !string.IsNullOrEmpty(lobby.Players[i].PlayerId))
                    {
                        knownHostPlayerId = lobby.Players[i].PlayerId;
                        break;
                    }
                }
            }
            if (lobby == null) knownHostPlayerId = null;
            PruneConfirmedInk(lobby);
            StateChanged?.Invoke(state, lobby, message);
        }

        private void OnBackendPlayerStateReceived(OnlinePlayerState playerState)
        {
            PlayerStateReceived?.Invoke(playerState);
        }

        private void OnBackendBodyDataReceived(OnlineBodyData bodyData)
        {
            CacheConfirmedInk(bodyData);
            BodyDataReceived?.Invoke(bodyData);
        }

        private void CacheConfirmedInk(OnlineBodyData bodyData)
        {
            if (bodyData == null || string.IsNullOrEmpty(bodyData.PlayerId) || string.IsNullOrEmpty(bodyData.Json))
            {
                return;
            }

            SerializableBodyDrawing body = JsonUtility.FromJson<SerializableBodyDrawing>(bodyData.Json);
            float total = 0f;
            if (body?.Parts != null)
            {
                for (int i = 0; i < body.Parts.Length; i++)
                {
                    SerializableBodyPartDrawing part = body.Parts[i];
                    if (part != null && !float.IsNaN(part.Ink) && !float.IsInfinity(part.Ink))
                    {
                        total += Mathf.Max(0f, part.Ink);
                    }
                }
            }

            confirmedInkByPlayer[bodyData.PlayerId] = total;
        }

        private void PruneConfirmedInk(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null)
            {
                confirmedInkByPlayer.Clear();
                return;
            }

            HashSet<string> activePlayerIds = new HashSet<string>();
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                if (lobby.Players[i] != null && !string.IsNullOrEmpty(lobby.Players[i].PlayerId))
                {
                    activePlayerIds.Add(lobby.Players[i].PlayerId);
                }
            }

            List<string> stalePlayerIds = new List<string>();
            foreach (string playerId in confirmedInkByPlayer.Keys)
            {
                if (!activePlayerIds.Contains(playerId))
                {
                    stalePlayerIds.Add(playerId);
                }
            }

            for (int i = 0; i < stalePlayerIds.Count; i++)
            {
                confirmedInkByPlayer.Remove(stalePlayerIds[i]);
            }
        }

        private void OnBackendCarryDataReceived(OnlineCarryData carryData)
        {
            CarryDataReceived?.Invoke(carryData);
        }

        private void OnBackendGimmickDataReceived(OnlineGimmickData gimmickData)
        {
            GimmickDataReceived?.Invoke(gimmickData);
        }
    }

    internal sealed class FakeOnlineBackend : IOnlineBackend
    {
        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        public OnlineConnectionState State { get; private set; }
        public OnlineLobbyInfo CurrentLobby { get; private set; }
        public string LocalPlayerId => "local";

        public void Initialize()
        {
            SetState(OnlineConnectionState.Offline, null, LocalizationManager.T("online_fake_initialized"));
        }

        public void Login()
        {
            SetState(OnlineConnectionState.LoggingIn, null, LocalizationManager.T("online_logging_in"));
            SetState(OnlineConnectionState.Online, null, LocalizationManager.T("online_local_test_player"));
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
            CurrentLobby = null;
            State = OnlineConnectionState.Offline;
        }

        public void StartRandomMatch()
        {
            CurrentLobby = CreateLobby("RANDOM", LocalizationManager.T("multi_random_match"), OnlineLobbyMode.Random, 4);
            CurrentLobby.Players = new[]
            {
                CreatePlayer("local", LocalizationManager.T("online_player_you"), true, false)
            };
            SetState(OnlineConnectionState.Matching, CurrentLobby, LocalizationManager.T("online_fake_random_ready"));
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            CurrentLobby = CreateLobby("TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(), string.IsNullOrEmpty(roomName) ? LocalizationManager.T("multi_default_room_name") : roomName, OnlineLobbyMode.Room, maxPlayers);
            CurrentLobby.Players = new[] { CreatePlayer("local", LocalizationManager.T("online_player_you"), true, false) };
            SetState(OnlineConnectionState.InLobby, CurrentLobby, isPrivate ? LocalizationManager.T("online_private_room_created") : LocalizationManager.T("online_public_room_created"));
        }

        public void JoinRoom(string roomId)
        {
            CurrentLobby = CreateLobby(string.IsNullOrEmpty(roomId) ? "ABC123" : roomId, LocalizationManager.T("multi_friend_room_name"), OnlineLobbyMode.Room, 4);
            CurrentLobby.Players = new[]
            {
                CreatePlayer("host", LocalizationManager.T("online_player_host"), true, true),
                CreatePlayer("local", LocalizationManager.T("online_player_you"), false, false)
            };
            SetState(OnlineConnectionState.InLobby, CurrentLobby, LocalizationManager.T("online_joined_fake_room"));
        }

        public void LeaveLobby()
        {
            CurrentLobby = null;
            SetState(OnlineConnectionState.Online, null, LocalizationManager.T("online_left_lobby"));
        }

        public void SetReady(bool ready)
        {
            if (CurrentLobby == null || CurrentLobby.Players == null || CurrentLobby.Players.Length == 0)
            {
                return;
            }

            CurrentLobby.Players[0].IsReady = ready;
            SetState(State, CurrentLobby, ready ? LocalizationManager.T("online_ready_on") : LocalizationManager.T("online_ready_off"));
        }

        public void StartGame(string stageId)
        {
            if (CurrentLobby != null)
            {
                CurrentLobby.StageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
                CurrentLobby.StageRevision++;
            }

            SetState(OnlineConnectionState.Playing, CurrentLobby, LocalizationManager.T("online_fake_stage_start"));
        }

        public void OpenStageSelect()
        {
            SetState(State, CurrentLobby, LocalizationManager.T("online_stage_select_opened"));
        }

        public void CloseStageSelect()
        {
            SetState(State, CurrentLobby, LocalizationManager.T("online_stage_select_closed"));
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

        public void SendCarryData(OnlineCarryData carryData)
        {
        }

        public void SendGimmickData(OnlineGimmickData gimmickData)
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
