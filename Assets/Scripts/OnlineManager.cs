using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class LocalMultiplayerDebugMode
    {
        private static readonly bool noTimeLimit = DetectLocalRegressionLaunch();

        public static bool NoTimeLimit => noTimeLimit;

        private static bool DetectLocalRegressionLaunch()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(
                    arg, "-pico-debug-no-time-limit", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

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
        private LocalRegressionOptions localRegression;
        private bool integrityErrorReported;

        private sealed class LocalRegressionOptions
        {
            public bool IsHost;
            public int Port = 7777;
            public int ExpectedPlayers = 2;
            public string StageId = "1-1";
            public string PlayerName;
        }

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        public OnlineConnectionState State => backend != null ? backend.State : OnlineConnectionState.Offline;
        public OnlineLobbyInfo CurrentLobby => backend != null ? backend.CurrentLobby : null;
        public string LocalPlayerId => backend != null ? backend.LocalPlayerId : string.Empty;
        public OnlineBackendMode EffectiveBackendMode => effectiveBackendMode;
        public bool IsLocalRegressionActive => localRegression != null;

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
            // The host owns the simulation. Window focus must not freeze the
            // network and physics for every participant.
            Application.runInBackground = true;
            localRegression = ParseLocalRegressionOptions(Environment.GetCommandLineArgs());
            if (localRegression != null)
            {
                backendMode = OnlineBackendMode.DirectTcp;
                directTcpPort = localRegression.Port;
                autoLogin = true;
                if (!string.IsNullOrWhiteSpace(localRegression.PlayerName))
                    PlayerNameSettings.TrySet(localRegression.PlayerName);
            }
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
            if (localRegression != null)
            {
                StartCoroutine(RunLocalRegression());
            }
        }

        private IEnumerator RunLocalRegression()
        {
            const float timeoutSeconds = 45f;
            float deadline = Time.unscaledTime + timeoutSeconds;
            while (State != OnlineConnectionState.Online && Time.unscaledTime < deadline)
                yield return null;

            if (State != OnlineConnectionState.Online)
            {
                Debug.LogError("[PICO REGRESSION] Online login timed out.");
                yield break;
            }

            if (localRegression.IsHost)
            {
                CreateRoom("LOCAL REGRESSION", localRegression.ExpectedPlayers, true);
                while (State != OnlineConnectionState.InLobby && Time.unscaledTime < deadline)
                    yield return null;
                if (State != OnlineConnectionState.InLobby)
                {
                    Debug.LogError("[PICO REGRESSION] Failed to create the local room.");
                    yield break;
                }

                ToggleReady();
                while (Time.unscaledTime < deadline)
                {
                    OnlinePlayerInfo[] players = CurrentLobby?.Players;
                    if (CountPlayers(players) >= localRegression.ExpectedPlayers && AreAllPlayersReady(players))
                    {
                        Debug.Log($"[PICO REGRESSION] Starting {localRegression.StageId} with {players.Length} players.");
                        StartGame(localRegression.StageId);
                        // Normal host flow selects the stage locally before/while
                        // broadcasting the start. Regression mode starts directly
                        // from the title, so the backend notification only moves
                        // clients unless we explicitly move the host as well.
                        StageManager stageManager = FindFirstObjectByType<StageManager>();
                        if (stageManager != null)
                        {
                            stageManager.SelectStage(localRegression.StageId);
                        }
                        else
                        {
                            Debug.LogError("[PICO REGRESSION] StageManager was not found for the host transition.");
                        }
                        yield break;
                    }
                    yield return null;
                }
                Debug.LogError("[PICO REGRESSION] Timed out waiting for players/READY.");
                yield break;
            }

            // The host process may still be opening its listener. Retry locally
            // instead of requiring a carefully timed manual launch order.
            while (Time.unscaledTime < deadline)
            {
                JoinRoom($"127.0.0.1:{localRegression.Port}");
                float attemptDeadline = Time.unscaledTime + 1.5f;
                while (State != OnlineConnectionState.InLobby
                    && State != OnlineConnectionState.Error
                    && Time.unscaledTime < attemptDeadline)
                    yield return null;
                if (State == OnlineConnectionState.InLobby)
                {
                    ToggleReady();
                    Debug.Log("[PICO REGRESSION] Joined local room and marked READY.");
                    yield break;
                }
                Login();
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Debug.LogError("[PICO REGRESSION] Timed out joining the local room.");
        }

        private static int CountPlayers(OnlinePlayerInfo[] players)
        {
            if (players == null) return 0;
            int count = 0;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId)) count++;
            return count;
        }

        private static bool AreAllPlayersReady(OnlinePlayerInfo[] players)
        {
            if (players == null || players.Length == 0) return false;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || string.IsNullOrEmpty(players[i].PlayerId) || !players[i].IsReady)
                    return false;
            }
            return true;
        }

        private static LocalRegressionOptions ParseLocalRegressionOptions(string[] args)
        {
            string role = GetCommandLineValue(args, "-pico-regression-role");
            if (!string.Equals(role, "host", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(role, "client", StringComparison.OrdinalIgnoreCase))
                return null;

            LocalRegressionOptions result = new LocalRegressionOptions
            {
                IsHost = string.Equals(role, "host", StringComparison.OrdinalIgnoreCase),
                StageId = GetCommandLineValue(args, "-pico-regression-stage") ?? "1-1",
                PlayerName = GetCommandLineValue(args, "-pico-regression-name")
            };
            if (int.TryParse(GetCommandLineValue(args, "-pico-regression-port"), out int port))
                result.Port = Mathf.Clamp(port, 1024, 65535);
            if (int.TryParse(GetCommandLineValue(args, "-pico-regression-players"), out int players))
                result.ExpectedPlayers = Mathf.Clamp(players, 2, 4);
            return result;
        }

        private static string GetCommandLineValue(string[] args, string key)
        {
            if (args == null) return null;
            string prefix = key + "=";
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length).Trim('"');
            }
            return null;
        }

        private void Update()
        {
            if (!shuttingDown)
            {
                backend?.Tick();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Application.runInBackground = true;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Application.runInBackground = true;
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
            if (shuttingDown || !CanUseOnline())
            {
                return;
            }

            backend?.Login();
        }

        public void StartRandomMatch()
        {
            if (shuttingDown || !CanUseOnline())
            {
                return;
            }

            backend?.StartRandomMatch();
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            if (shuttingDown || !CanUseOnline())
            {
                return;
            }

            backend?.CreateRoom(roomName, maxPlayers, isPrivate);
        }

        public void JoinRoom(string roomId)
        {
            if (shuttingDown || !CanUseOnline())
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
            if (shuttingDown || !CanUseOnline())
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
            bodyData.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            CacheConfirmedInk(bodyData);
            backend?.SendBodyData(bodyData);
        }

        public void SendInput(OnlineInputData inputData)
        {
            if (shuttingDown || inputData == null)
            {
                return;
            }

            StampStageSession(inputData);
            backend?.SendInput(inputData);
        }

        public void SendPlayerState(OnlinePlayerState playerState)
        {
            if (shuttingDown || playerState == null)
            {
                return;
            }

            StampStageSession(playerState);
            backend?.SendPlayerState(playerState);
        }

        public void SendCarryData(OnlineCarryData carryData)
        {
            if (shuttingDown || carryData == null)
            {
                return;
            }

            StampStageSession(carryData);
            backend?.SendCarryData(carryData);
        }

        public void SendGimmickData(OnlineGimmickData gimmickData)
        {
            if (shuttingDown || gimmickData == null)
            {
                return;
            }

            StampStageSession(gimmickData);
            backend?.SendGimmickData(gimmickData);
        }

        private void StampStageSession(OnlineInputData data)
        {
            data.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null) return;
            data.StageId = lobby.StageId;
            data.StageRevision = lobby.StageRevision;
            data.RetryRevision = lobby.RetryRevision;
        }

        private void StampStageSession(OnlinePlayerState data)
        {
            data.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null) return;
            data.StageId = lobby.StageId;
            data.StageRevision = lobby.StageRevision;
            data.RetryRevision = lobby.RetryRevision;
        }

        private void StampStageSession(OnlineCarryData data)
        {
            data.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null) return;
            data.StageId = lobby.StageId;
            data.StageRevision = lobby.StageRevision;
            data.RetryRevision = lobby.RetryRevision;
        }

        private void StampStageSession(OnlineGimmickData data)
        {
            data.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null) return;
            data.StageId = lobby.StageId;
            data.StageRevision = lobby.StageRevision;
            data.RetryRevision = lobby.RetryRevision;
        }

        private bool IsCurrentStageSession(string stageId, int stageRevision, int retryRevision, bool allowRetryAdvance)
        {
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby == null || State != OnlineConnectionState.Playing)
            {
                return true;
            }

            // Revision zero is used before the first online stage starts. Once a
            // revision exists, unscoped/legacy packets must not leak into it.
            if (stageRevision <= 0)
            {
                return lobby.StageRevision <= 0;
            }

            if (!string.Equals(stageId, lobby.StageId, StringComparison.Ordinal)
                || stageRevision != lobby.StageRevision)
            {
                return false;
            }

            if (retryRevision == lobby.RetryRevision)
            {
                return true;
            }

            // Backends increment RetryRevision while relaying stage_retry. The
            // request itself therefore legitimately carries the previous value.
            return allowRetryAdvance && retryRevision + 1 == lobby.RetryRevision;
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
            if (playerState == null || !AcceptRemoteContent(playerState.ContentFingerprint)
                || !IsSanePlayerState(playerState) || !IsCurrentStageSession(
                    playerState.StageId, playerState.StageRevision, playerState.RetryRevision, false))
            {
                return;
            }
            PlayerStateReceived?.Invoke(playerState);
        }

        private void OnBackendBodyDataReceived(OnlineBodyData bodyData)
        {
            if (bodyData == null || !AcceptRemoteContent(bodyData.ContentFingerprint) || !IsSaneBodyData(bodyData))
            {
                return;
            }
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
            if (carryData == null || !AcceptRemoteContent(carryData.ContentFingerprint)
                || !IsSaneVector(carryData.ReleaseVelocity, 1000f) || !IsSaneVector(carryData.LocalOffset, 100f)
                || !IsCurrentStageSession(
                    carryData.StageId, carryData.StageRevision, carryData.RetryRevision, false))
            {
                return;
            }
            CarryDataReceived?.Invoke(carryData);
        }

        private void OnBackendGimmickDataReceived(OnlineGimmickData gimmickData)
        {
            bool isRetry = gimmickData != null && gimmickData.Kind == "stage_retry";
            if (gimmickData == null || !AcceptRemoteContent(gimmickData.ContentFingerprint)
                || (gimmickData.Json != null && gimmickData.Json.Length > 1024 * 1024)
                || !IsCurrentStageSession(
                    gimmickData.StageId, gimmickData.StageRevision, gimmickData.RetryRevision, isRetry))
            {
                return;
            }
            GimmickDataReceived?.Invoke(gimmickData);
        }

        private bool CanUseOnline()
        {
            if (ContentIntegrityVerifier.IsTrusted) return true;
            ReportIntegrityError();
            return false;
        }

        private bool AcceptRemoteContent(string remoteFingerprint)
        {
            if (!ContentIntegrityVerifier.IsTrusted)
            {
                ReportIntegrityError();
                return false;
            }

            if (string.Equals(remoteFingerprint, ContentIntegrityVerifier.Fingerprint, StringComparison.Ordinal))
                return true;

            ReportIntegrityError();
            return false;
        }

        private void ReportIntegrityError()
        {
            if (integrityErrorReported) return;
            integrityErrorReported = true;
            string message = LocalizationManager.T("online_content_mismatch");
            Debug.LogError(message + " " + ContentIntegrityVerifier.FailureReason);
            StateChanged?.Invoke(OnlineConnectionState.Error, CurrentLobby, message);
        }

        private static bool IsSanePlayerState(OnlinePlayerState state)
        {
            return IsSaneVector(state.Position, 10000f)
                && IsSaneVector(state.Velocity, 1000f)
                && IsFinite(state.Rotation) && Mathf.Abs(state.Rotation) <= 100000f
                && IsSaneVector(state.AimDirection, 10f)
                && IsSaneVector(state.CarryOffset, 100f)
                && IsFinite(state.SpeedBoostMultiplier) && state.SpeedBoostMultiplier >= 0f && state.SpeedBoostMultiplier <= 100f
                && IsFinite(state.SpeedBoostRemaining) && state.SpeedBoostRemaining >= 0f && state.SpeedBoostRemaining <= 3600f
                && IsFinite(state.RespawnRemaining) && state.RespawnRemaining >= 0f && state.RespawnRemaining <= 3600f
                && IsFinite(state.RespawnGraceRemaining) && state.RespawnGraceRemaining >= 0f && state.RespawnGraceRemaining <= 3600f;
        }

        private static bool IsSaneBodyData(OnlineBodyData data)
        {
            if (string.IsNullOrEmpty(data.PlayerId) || string.IsNullOrEmpty(data.Json)
                || data.PlayerId.Length > 256 || data.Json.Length > 4 * 1024 * 1024)
                return false;
            try
            {
                SerializableBodyDrawing body = JsonUtility.FromJson<SerializableBodyDrawing>(data.Json);
                if (body == null || body.Parts == null || body.Parts.Length > 64) return false;
                float totalInk = 0f;
                for (int i = 0; i < body.Parts.Length; i++)
                {
                    SerializableBodyPartDrawing part = body.Parts[i];
                    if (part == null || !IsFinite(part.Ink) || part.Ink < 0f || part.Ink > 500f
                        || part.Points == null || part.Points.Length > 20000)
                        return false;
                    totalInk += part.Ink;
                    if (totalInk > 500.01f) return false;
                    for (int pointIndex = 0; pointIndex < part.Points.Length; pointIndex++)
                        if (!IsSaneVector(part.Points[pointIndex], 10000f)) return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsSaneVector(Vector2 value, float maximumAbsoluteValue)
        {
            return IsFinite(value.x) && IsFinite(value.y)
                && Mathf.Abs(value.x) <= maximumAbsoluteValue && Mathf.Abs(value.y) <= maximumAbsoluteValue;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class FakeOnlineBackend : IOnlineBackend
    {
        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived { add { } remove { } }
        public event Action<OnlineBodyData> BodyDataReceived { add { } remove { } }
        public event Action<OnlineCarryData> CarryDataReceived { add { } remove { } }
        public event Action<OnlineGimmickData> GimmickDataReceived { add { } remove { } }
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
                CreatePlayer("local", PlayerNameSettings.CurrentName, true, false)
            };
            SetState(OnlineConnectionState.Matching, CurrentLobby, LocalizationManager.T("online_fake_random_ready"));
        }

        public void CreateRoom(string roomName, int maxPlayers, bool isPrivate)
        {
            CurrentLobby = CreateLobby("TEST-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(), string.IsNullOrEmpty(roomName) ? LocalizationManager.T("multi_default_room_name") : roomName, OnlineLobbyMode.Room, maxPlayers);
            CurrentLobby.Players = new[] { CreatePlayer("local", PlayerNameSettings.CurrentName, true, false) };
            SetState(OnlineConnectionState.InLobby, CurrentLobby, isPrivate ? LocalizationManager.T("online_private_room_created") : LocalizationManager.T("online_public_room_created"));
        }

        public void JoinRoom(string roomId)
        {
            CurrentLobby = CreateLobby(string.IsNullOrEmpty(roomId) ? "ABC123" : roomId, LocalizationManager.T("multi_friend_room_name"), OnlineLobbyMode.Room, 4);
            CurrentLobby.Players = new[]
            {
                CreatePlayer("host", LocalizationManager.T("online_player_host"), true, true),
                CreatePlayer("local", PlayerNameSettings.CurrentName, false, false)
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
