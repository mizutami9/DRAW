using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class LocalMultiplayerDebugMode
    {
        private static readonly bool noTimeLimit = DetectLocalRegressionLaunch();
        private static readonly int simulatedLatencyMs = ReadIntArgument("-pico-net-latency-ms", 0, 1000);
        private static readonly float simulatedLossPercent = ReadIntArgument("-pico-net-loss-percent", 0, 50);
        private static readonly bool networkMonitor = HasArgument("-pico-net-monitor");

        public static bool NoTimeLimit => noTimeLimit;
        public static bool Enabled => noTimeLimit || simulatedLatencyMs > 0 || simulatedLossPercent > 0f;
        public static float SimulatedLatencySeconds => simulatedLatencyMs / 1000f;
        public static float SimulatedLoss01 => simulatedLossPercent / 100f;
        public static bool NetworkMonitor => networkMonitor;

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

        private static int ReadIntArgument(string key, int minimum, int maximum)
        {
            string[] args = Environment.GetCommandLineArgs();
            string prefix = key + "=";
            if (args == null) return minimum;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(arg.Substring(prefix.Length), out int value))
                    return Mathf.Clamp(value, minimum, maximum);
            }
            return minimum;
        }

        private static bool HasArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return true;
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
        private const string DiagnosticObjectId = "__network_diagnostics";
        private const string DiagnosticPingRequestKind = "network_diagnostic_ping_request";
        private const string DiagnosticPingReplyKind = "network_diagnostic_ping_reply";
        public const string LocalGamePlayerRejectedKind = "local_game_player_rejected";
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
        private readonly List<ScheduledInboundPacket> scheduledInboundPackets =
            new List<ScheduledInboundPacket>();
        private readonly Dictionary<int, float> pendingDiagnosticPings = new Dictionary<int, float>();
        private readonly Dictionary<string, float> rejectedGamePlayerUntil =
            new Dictionary<string, float>();
        private readonly Dictionary<string, OnlinePlayerInfo> rememberedVirtualPlayers =
            new Dictionary<string, OnlinePlayerInfo>();
        private readonly Dictionary<string, float> rememberedVirtualPlayerSeenAt =
            new Dictionary<string, float>();
        private int nextDiagnosticPingToken;
        private float nextDiagnosticPingAt;
        private int inboundPacketCount;
        private int simulatedDropCount;
        private int networkCorrectionCount;

        [Serializable]
        private sealed class NetworkDiagnosticPing
        {
            public int Token;
            public string TargetPlayerId;
        }

        private sealed class ScheduledInboundPacket
        {
            public float DeliverAt;
            public Action Deliver;
        }

        private sealed class LocalRegressionOptions
        {
            public bool IsHost;
            public bool FullFlow;
            public bool AiTest;
            public bool UseEos;
            public string CoordinationPath;
            public int Port = 7777;
            public int ExpectedPlayers = 2;
            public int StartPlayers = 2;
            public string StageId = "1-1";
            public string PlayerName;
        }

        public event Action<OnlineConnectionState, OnlineLobbyInfo, string> StateChanged;
        public event Action<OnlinePlayerState> PlayerStateReceived;
        public event Action<OnlineBodyData> BodyDataReceived;
        public event Action<OnlineCarryData> CarryDataReceived;
        public event Action<OnlineGimmickData> GimmickDataReceived;
        private static int nextLocalBodyRevision;
        private string localBodyHash = string.Empty;
        private string localBodySpecies = string.Empty;
        private readonly Dictionary<string, int> localBodyRevisionByPlayer = new Dictionary<string, int>();
        private readonly Dictionary<string, string> localBodyHashByPlayer = new Dictionary<string, string>();
        private readonly Dictionary<string, string> localBodySpeciesByPlayer = new Dictionary<string, string>();

        /// <summary>
        /// Latest body revision published by this client. Ready-room messages use
        /// this value to prevent a peer from validating an older remote body when
        /// redraw completion and body packets arrive on different channels.
        /// </summary>
        public int LocalBodyRevision => nextLocalBodyRevision;
        public string LocalBodyHash => localBodyHash;
        public string LocalBodySpecies => localBodySpecies;
        public int GetLocalBodyRevision(string playerId) => !string.IsNullOrEmpty(playerId)
            && localBodyRevisionByPlayer.TryGetValue(playerId, out int value) ? value : LocalBodyRevision;
        public string GetLocalBodyHash(string playerId) => !string.IsNullOrEmpty(playerId)
            && localBodyHashByPlayer.TryGetValue(playerId, out string value) ? value : LocalBodyHash;
        public string GetLocalBodySpecies(string playerId) => !string.IsNullOrEmpty(playerId)
            && localBodySpeciesByPlayer.TryGetValue(playerId, out string value) ? value : LocalBodySpecies;
        public OnlineConnectionState State => backend != null ? backend.State : OnlineConnectionState.Offline;
        public OnlineLobbyInfo CurrentLobby => backend != null ? backend.CurrentLobby : null;
        public string LocalPlayerId => backend != null ? backend.LocalPlayerId : string.Empty;
        public OnlineBackendMode EffectiveBackendMode => effectiveBackendMode;
        public bool IsLocalRegressionActive => localRegression != null;
        public float LastNetworkRoundTripMs { get; private set; } = -1f;
        public int InboundPacketCount => inboundPacketCount;
        public int SimulatedDropCount => simulatedDropCount;
        public int NetworkCorrectionCount => networkCorrectionCount;

        public void RecordNetworkCorrection()
        {
            networkCorrectionCount++;
        }

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
            return GetConfirmedInkExcludingPlayer(LocalPlayerId);
        }

        public float GetConfirmedInkExcludingPlayer(string excludedGamePlayerId)
        {
            float total = 0f;
            OnlinePlayerInfo[] players = CurrentLobby != null ? CurrentLobby.Players : null;
            if (players == null)
            {
                return 0f;
            }

            for (int i = 0; i < players.Length; i++)
            {
                OnlinePlayerInfo player = players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId)
                    || player.PlayerId == excludedGamePlayerId)
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

        public float GetConfirmedInkForPlayer(string playerId)
        {
            return !string.IsNullOrEmpty(playerId)
                && confirmedInkByPlayer.TryGetValue(playerId, out float ink)
                    ? ink
                    : 0f;
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

        /// <summary>
        /// Uses the live authoritative roster rather than a stage's start-time
        /// participant cache. This keeps elimination/finish rules valid after a
        /// participant leaves or joins an in-progress room.
        /// </summary>
        public bool IsCurrentLobbyPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return false;
            OnlinePlayerInfo[] players = CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].PlayerId == playerId) return true;
            return false;
        }

        public bool AreAllCurrentLobbyPlayersIn(ICollection<string> playerIds)
        {
            if (playerIds == null) return false;
            OnlinePlayerInfo[] players = CurrentLobby?.Players;
            if (players == null || players.Length == 0) return false;
            int activePlayers = 0;
            for (int i = 0; i < players.Length; i++)
            {
                string playerId = players[i]?.PlayerId;
                if (string.IsNullOrEmpty(playerId)) continue;
                activePlayers++;
                if (!playerIds.Contains(playerId)) return false;
            }
            return activePlayers > 0;
        }

        private void Awake()
        {
            // The host owns the simulation. Window focus must not freeze the
            // network and physics for every participant.
            Application.runInBackground = true;
            localRegression = ParseLocalRegressionOptions(Environment.GetCommandLineArgs());
            if (localRegression != null)
            {
                if (localRegression.UseEos)
                {
                    backendMode = OnlineBackendMode.Eos;
                }
                else
                {
                backendMode = OnlineBackendMode.DirectTcp;
                directTcpPort = localRegression.Port;
                }
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
            if (LocalMultiplayerDebugMode.SimulatedLatencySeconds > 0f
                || LocalMultiplayerDebugMode.SimulatedLoss01 > 0f)
            {
                Debug.Log($"[PICO NET SIM] latency={LocalMultiplayerDebugMode.SimulatedLatencySeconds * 1000f:0}ms "
                    + $"loss={LocalMultiplayerDebugMode.SimulatedLoss01 * 100f:0}%");
            }
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
            float timeoutSeconds =
                localRegression.UseEos ? 120f :
                45f;
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

                if (localRegression.UseEos)
                {
                    string roomCode = CurrentLobby?.RoomCode;
                    if (string.IsNullOrEmpty(roomCode))
                    {
                        Debug.LogError("[PICO AI TEST] EOS room was created without a room code.");
                        yield break;
                    }
                    PublishAiTestRoomCode(localRegression.CoordinationPath, roomCode);
                }

                if (localRegression.AiTest) yield return new WaitForSecondsRealtime(2f);
                ToggleReady();
                while (Time.unscaledTime < deadline)
                {
                    OnlinePlayerInfo[] players = CurrentLobby?.Players;
                    if (CountPlayers(players) >= localRegression.StartPlayers && AreAllPlayersReady(players))
                    {
                        if (localRegression.FullFlow)
                        {
                            Debug.Log($"[PICO REGRESSION] Full flow lobby is ready with {players.Length} players. Select the first stage on the host.");
                            yield break;
                        }

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
                string joinTarget = localRegression.UseEos
                    ? ReadAiTestRoomCode(localRegression.CoordinationPath)
                    : $"127.0.0.1:{localRegression.Port}";
                if (string.IsNullOrEmpty(joinTarget))
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }
                JoinRoom(joinTarget);
                float attemptDeadline = Time.unscaledTime + 1.5f;
                while (State != OnlineConnectionState.InLobby
                    && State != OnlineConnectionState.Playing
                    && State != OnlineConnectionState.Error
                    && Time.unscaledTime < attemptDeadline)
                    yield return null;
                // A participant joining an already-running Direct room receives
                // the lobby snapshot with a positive StageRevision and moves
                // straight to Playing. Treat that as a successful join too;
                // retrying JoinRoom would close the healthy stage connection.
                if (State == OnlineConnectionState.InLobby
                    || State == OnlineConnectionState.Playing)
                {
                    if (localRegression.AiTest) yield return new WaitForSecondsRealtime(2f);
                    // READY belongs to the pre-game lobby. A late participant is
                    // already in Playing and must not mutate lobby readiness after
                    // StageManager has loaded the running stage.
                    if (State == OnlineConnectionState.InLobby)
                    {
                        ToggleReady();
                        Debug.Log("[PICO REGRESSION] Joined local room and marked READY.");
                    }
                    else
                    {
                        Debug.Log("[PICO REGRESSION] Joined the running stage without changing READY.");
                    }
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
                FullFlow = HasCommandLineFlag(args, "-pico-regression-full-flow"),
                AiTest = HasCommandLineFlag(args, "-pico-ai-test"),
                StageId = GetCommandLineValue(args, "-pico-regression-stage") ?? "1-1",
                PlayerName = GetCommandLineValue(args, "-pico-regression-name")
            };
            result.UseEos = string.Equals(
                GetCommandLineValue(args, "-pico-regression-backend"),
                "eos", StringComparison.OrdinalIgnoreCase);
            result.CoordinationPath = GetCommandLineValue(args, "-pico-ai-coordination");
            if (int.TryParse(GetCommandLineValue(args, "-pico-regression-port"), out int port))
                result.Port = Mathf.Clamp(port, 1024, 65535);
            if (int.TryParse(GetCommandLineValue(args, "-pico-regression-players"), out int players))
            {
                result.ExpectedPlayers = Mathf.Clamp(players, 2, 4);
                result.StartPlayers = result.ExpectedPlayers;
            }
            if (int.TryParse(GetCommandLineValue(args, "-pico-regression-start-players"), out int startPlayers))
                result.StartPlayers = Mathf.Clamp(startPlayers, 2, result.ExpectedPlayers);
            return result;
        }

        private static void PublishAiTestRoomCode(string path, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, roomCode.Trim());
            Debug.Log($"[PICO AI TEST] Published EOS room code to {fullPath}.");
        }

        private static string ReadAiTestRoomCode(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static bool HasCommandLineFlag(string[] args, string key)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
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
                FlushScheduledInboundPackets();
                UpdateNetworkDiagnostics();
            }
        }

        private bool TryScheduleInbound(Action deliver, bool allowSimulatedLoss)
        {
            if (deliver == null) return true;
            if (allowSimulatedLoss && LocalMultiplayerDebugMode.SimulatedLoss01 > 0f
                && UnityEngine.Random.value < LocalMultiplayerDebugMode.SimulatedLoss01)
            {
                simulatedDropCount++;
                return true;
            }
            float delay = LocalMultiplayerDebugMode.SimulatedLatencySeconds;
            if (delay <= 0f) return false;
            scheduledInboundPackets.Add(new ScheduledInboundPacket
            {
                DeliverAt = Time.unscaledTime + delay,
                Deliver = deliver
            });
            return true;
        }

        private void FlushScheduledInboundPackets()
        {
            for (int i = 0; i < scheduledInboundPackets.Count;)
            {
                ScheduledInboundPacket packet = scheduledInboundPackets[i];
                if (packet != null && Time.unscaledTime < packet.DeliverAt)
                {
                    i++;
                    continue;
                }
                scheduledInboundPackets.RemoveAt(i);
                packet.Deliver?.Invoke();
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

        public void SetLocalReady(bool ready)
        {
            if (shuttingDown || CurrentLobby == null) return;
            backend?.SetReady(ready);
        }

        public void StartGame(string stageId)
        {
            if (shuttingDown || !CanUseOnline())
            {
                return;
            }

            backend?.StartGame(DemoAccessPolicy.NormalizeStageId(stageId));
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

            string connectionId = string.IsNullOrEmpty(LocalPlayerId) ? "local" : LocalPlayerId;
            if (string.IsNullOrEmpty(bodyData.PlayerId) || bodyData.PlayerId == "local")
                bodyData.PlayerId = connectionId;
            bodyData.ConnectionId = connectionId;
            // DrawManager can be recreated or reinitialized while OnlineManager and
            // the lobby stay alive. Keep the revision monotonic for the whole
            // connection so a redraw is never mistaken for an older body.
            int previousRevision = localBodyRevisionByPlayer.TryGetValue(bodyData.PlayerId, out int rememberedRevision)
                ? rememberedRevision
                : 0;
            if (bodyData.Revision <= previousRevision)
            {
                bodyData.Revision = previousRevision + 1;
            }
            localBodyRevisionByPlayer[bodyData.PlayerId] = bodyData.Revision;
            nextLocalBodyRevision = Mathf.Max(nextLocalBodyRevision, bodyData.Revision);
            bodyData.ContentFingerprint = ContentIntegrityVerifier.Fingerprint;
            bodyData.BodyHash = ContentIntegrityVerifier.HashString(bodyData.Json ?? string.Empty);
            localBodyHash = bodyData.BodyHash;
            localBodyHashByPlayer[bodyData.PlayerId] = bodyData.BodyHash;
            SerializableBodyDrawing serializedBody = !string.IsNullOrEmpty(bodyData.Json)
                ? JsonUtility.FromJson<SerializableBodyDrawing>(bodyData.Json)
                : null;
            localBodySpecies = serializedBody?.Species ?? string.Empty;
            localBodySpeciesByPlayer[bodyData.PlayerId] = localBodySpecies;
            if (LocalMultiplayerDebugMode.Enabled)
            {
                Debug.Log($"[PICO BODY] SEND id={bodyData.PlayerId} rev={bodyData.Revision} bytes={bodyData.Json?.Length ?? 0}");
            }
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

            string connectionId = string.IsNullOrEmpty(LocalPlayerId) ? "local" : LocalPlayerId;
            if (string.IsNullOrEmpty(playerState.PlayerId) || playerState.PlayerId == "local")
                playerState.PlayerId = connectionId;
            playerState.ConnectionId = connectionId;
            if (!EnsureGamePlayerInLobby(playerState)) return;
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
            if (string.IsNullOrEmpty(carryData.CarrierPlayerId) || carryData.CarrierPlayerId == "local")
                carryData.CarrierPlayerId = LocalPlayerId;
            // Apply carry prediction on the sender immediately as well. Some
            // backends echo messages while the local TCP host does not, so relying
            // on a round trip makes throw responsiveness depend on who is hosting.
            CarryDataReceived?.Invoke(carryData);
            backend?.SendCarryData(carryData);
        }

        public void SendGimmickData(OnlineGimmickData gimmickData)
        {
            if (shuttingDown || gimmickData == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(gimmickData.PlayerId))
            {
                string claimedActor = ExtractJsonStringField(
                    gimmickData.Json,
                    "PlayerId", "SourcePlayerId", "CollectorId", "RequesterId",
                    "ShooterPlayerId", "OwnerPlayerId");
                if (OnlinePlayerIdentity.IsOwnedByConnection(claimedActor, LocalPlayerId))
                    gimmickData.PlayerId = claimedActor;
            }
            StampStageSession(gimmickData);
            backend?.SendGimmickData(gimmickData);
        }

        private static string ExtractJsonStringField(string json, params string[] fieldNames)
        {
            if (string.IsNullOrEmpty(json) || fieldNames == null) return null;
            for (int i = 0; i < fieldNames.Length; i++)
            {
                string marker = "\"" + fieldNames[i] + "\":\"";
                int start = json.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0) continue;
                start += marker.Length;
                int end = json.IndexOf('"', start);
                if (end > start) return json.Substring(start, end - start);
            }
            return null;
        }

        public void AnnounceLocalGamePlayerLeft(string gamePlayerId)
        {
            if (!OnlinePlayerIdentity.IsOwnedByConnection(gamePlayerId, LocalPlayerId)
                || gamePlayerId == LocalPlayerId) return;
            SendGimmickData(new OnlineGimmickData
            {
                PlayerId = LocalPlayerId,
                Kind = "local_game_player_left",
                ObjectId = gamePlayerId
            });
            RemoveVirtualGamePlayer(gamePlayerId);
        }

        private bool RemoveVirtualGamePlayer(string gamePlayerId)
        {
            rememberedVirtualPlayers.Remove(gamePlayerId);
            rememberedVirtualPlayerSeenAt.Remove(gamePlayerId);
            OnlineLobbyInfo lobby = CurrentLobby;
            if (lobby?.Players == null || string.IsNullOrEmpty(gamePlayerId)) return false;
            List<OnlinePlayerInfo> remaining = new List<OnlinePlayerInfo>();
            bool removed = false;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo candidate = lobby.Players[i];
                if (candidate != null && candidate.PlayerId == gamePlayerId
                    && candidate.LocalPlayerIndex > 0)
                {
                    removed = true;
                    continue;
                }
                if (candidate != null) remaining.Add(candidate);
            }
            if (!removed) return false;
            lobby.Players = remaining.ToArray();
            confirmedInkByPlayer.Remove(gamePlayerId);
            NormalizeGamePlayerSlots(lobby);
            StateChanged?.Invoke(State, lobby, LocalizationManager.T("online_lobby_members_updated"));
            return true;
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
            MergeRememberedVirtualPlayers(lobby);
            SyncVirtualPlayerMetadata(lobby);
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
            if (lobby == null)
            {
                knownHostPlayerId = null;
                rememberedVirtualPlayers.Clear();
                rememberedVirtualPlayerSeenAt.Clear();
            }
            if (lobby != null && IsHostPlayer(LocalPlayerId))
                EnforceAuthoritativeGamePlayerLimit(lobby);
            PruneConfirmedInk(lobby);
            StateChanged?.Invoke(state, lobby, message);
        }

        private static void SyncVirtualPlayerMetadata(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null) return;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo virtualPlayer = lobby.Players[i];
                if (virtualPlayer == null || virtualPlayer.LocalPlayerIndex <= 0
                    || string.IsNullOrEmpty(virtualPlayer.ConnectionId)) continue;
                for (int j = 0; j < lobby.Players.Length; j++)
                {
                    OnlinePlayerInfo connectionPlayer = lobby.Players[j];
                    if (connectionPlayer == null
                        || connectionPlayer.PlayerId != virtualPlayer.ConnectionId) continue;
                    virtualPlayer.IsReady = connectionPlayer.IsReady;
                    virtualPlayer.IsHost = false;
                    break;
                }
            }
            NormalizeGamePlayerSlots(lobby);
        }

        private void MergeRememberedVirtualPlayers(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null || rememberedVirtualPlayers.Count == 0) return;
            HashSet<string> physicalConnections = new HashSet<string>();
            HashSet<string> listedIds = new HashSet<string>();
            List<OnlinePlayerInfo> merged = new List<OnlinePlayerInfo>();
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo candidate = lobby.Players[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.PlayerId)) continue;
                merged.Add(candidate);
                listedIds.Add(candidate.PlayerId);
                if (candidate.LocalPlayerIndex <= 0) physicalConnections.Add(candidate.PlayerId);
            }

            List<string> expired = null;
            foreach (KeyValuePair<string, OnlinePlayerInfo> pair in rememberedVirtualPlayers)
            {
                OnlinePlayerInfo remembered = pair.Value;
                bool connectionPresent = remembered != null
                    && physicalConnections.Contains(remembered.ConnectionId);
                bool recent = rememberedVirtualPlayerSeenAt.TryGetValue(pair.Key, out float seenAt)
                    && Time.unscaledTime - seenAt <= 15f;
                if (!connectionPresent || !recent)
                {
                    expired ??= new List<string>();
                    expired.Add(pair.Key);
                    continue;
                }
                if (!listedIds.Contains(pair.Key))
                {
                    merged.Add(remembered);
                    listedIds.Add(pair.Key);
                }
            }
            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    rememberedVirtualPlayers.Remove(expired[i]);
                    rememberedVirtualPlayerSeenAt.Remove(expired[i]);
                }
            }
            lobby.Players = merged.ToArray();
        }

        private void OnBackendPlayerStateReceived(OnlinePlayerState playerState)
        {
            inboundPacketCount++;
            if (TryScheduleInbound(() => DeliverBackendPlayerState(playerState), true)) return;
            DeliverBackendPlayerState(playerState);
        }

        private void DeliverBackendPlayerState(OnlinePlayerState playerState)
        {
            if (playerState == null || !AcceptRemoteContent(playerState.ContentFingerprint)
                || !IsSanePlayerState(playerState) || !IsCurrentStageSession(
                    playerState.StageId, playerState.StageRevision, playerState.RetryRevision, false))
            {
                return;
            }
            if (!EnsureGamePlayerInLobby(playerState)) return;
            PlayerStateReceived?.Invoke(playerState);
        }

        private bool EnsureGamePlayerInLobby(OnlinePlayerState state)
        {
            OnlineLobbyInfo lobby = CurrentLobby;
            if (state == null || lobby == null || string.IsNullOrEmpty(state.PlayerId)) return state != null;
            string connectionId = string.IsNullOrEmpty(state.ConnectionId)
                ? state.PlayerId
                : state.ConnectionId;
            if (state.LocalPlayerIndex < 0
                || state.LocalPlayerIndex >= StageManager.MaxLocalPlayersPerConnection
                || OnlinePlayerIdentity.Compose(connectionId, state.LocalPlayerIndex) != state.PlayerId)
                return false;
            if (IsHostPlayer(LocalPlayerId)) EnforceAuthoritativeGamePlayerLimit(lobby);
            if (rejectedGamePlayerUntil.TryGetValue(state.PlayerId, out float rejectedUntil))
            {
                if (Time.unscaledTime < rejectedUntil) return false;
                rejectedGamePlayerUntil.Remove(state.PlayerId);
            }
            OnlinePlayerInfo[] existing = lobby.Players ?? System.Array.Empty<OnlinePlayerInfo>();
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null || existing[i].PlayerId != state.PlayerId) continue;
                NormalizeGamePlayerSlots(lobby);
                state.PlayerSlot = existing[i].PlayerSlot;
                bool metadataChanged = existing[i].InputDevice != state.InputDevice;
                existing[i].InputDevice = state.InputDevice;
                if (!string.IsNullOrEmpty(state.DisplayName)
                    && existing[i].DisplayName != state.DisplayName)
                {
                    existing[i].DisplayName = state.DisplayName;
                    metadataChanged = true;
                }
                if (state.LocalPlayerIndex > 0)
                {
                    rememberedVirtualPlayers[state.PlayerId] = existing[i];
                    rememberedVirtualPlayerSeenAt[state.PlayerId] = Time.unscaledTime;
                }
                SyncVirtualPlayerMetadata(lobby);
                if (metadataChanged)
                {
                    StateChanged?.Invoke(State, lobby,
                        LocalizationManager.T("online_lobby_members_updated"));
                }
                return true;
            }
            int playerLimit = Mathf.Clamp(lobby.MaxPlayers, 1, 4);
            if (existing.Length >= playerLimit)
            {
                if (IsHostPlayer(LocalPlayerId)) RejectGamePlayer(state.PlayerId);
                return false;
            }

            OnlinePlayerInfo connectionPlayer = null;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null && existing[i].PlayerId == connectionId)
                {
                    connectionPlayer = existing[i];
                    break;
                }
            }
            OnlinePlayerInfo virtualPlayer = new OnlinePlayerInfo
            {
                PlayerId = state.PlayerId,
                ConnectionId = connectionId,
                LocalPlayerIndex = state.LocalPlayerIndex,
                InputDevice = state.InputDevice,
                DisplayName = state.DisplayName,
                IsHost = connectionPlayer != null && connectionPlayer.IsHost && state.LocalPlayerIndex == 0,
                IsReady = connectionPlayer == null || connectionPlayer.IsReady,
                PlayerSlot = state.PlayerSlot
            };
            OnlinePlayerInfo[] expanded = new OnlinePlayerInfo[existing.Length + 1];
            System.Array.Copy(existing, expanded, existing.Length);
            expanded[existing.Length] = virtualPlayer;
            lobby.Players = expanded;
            if (state.LocalPlayerIndex > 0)
            {
                rememberedVirtualPlayers[state.PlayerId] = virtualPlayer;
                rememberedVirtualPlayerSeenAt[state.PlayerId] = Time.unscaledTime;
            }
            NormalizeGamePlayerSlots(lobby);
            state.PlayerSlot = virtualPlayer.PlayerSlot;
            StateChanged?.Invoke(State, lobby, LocalizationManager.T("online_lobby_members_updated"));
            return true;
        }

        private void EnforceAuthoritativeGamePlayerLimit(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null) return;
            int limit = Mathf.Clamp(lobby.MaxPlayers, 1, 4);
            while (lobby.Players.Length > limit)
            {
                OnlinePlayerInfo reject = null;
                // Physical connections remain usable; an extra local seat is
                // the reversible part of a simultaneous join race.
                for (int i = lobby.Players.Length - 1; i >= 0; i--)
                {
                    OnlinePlayerInfo candidate = lobby.Players[i];
                    if (candidate != null && candidate.LocalPlayerIndex > 0)
                    {
                        reject = candidate;
                        break;
                    }
                }
                if (reject == null || string.IsNullOrEmpty(reject.PlayerId)) break;
                RejectGamePlayer(reject.PlayerId);
            }
        }

        private void RejectGamePlayer(string gamePlayerId)
        {
            if (string.IsNullOrEmpty(gamePlayerId)) return;
            bool firstRejection = !rejectedGamePlayerUntil.TryGetValue(gamePlayerId, out float until)
                || Time.unscaledTime >= until;
            rejectedGamePlayerUntil[gamePlayerId] = Time.unscaledTime + 2.5f;
            RemoveVirtualGamePlayer(gamePlayerId);
            if (!firstRejection || !IsHostPlayer(LocalPlayerId)) return;

            OnlineGimmickData rejection = new OnlineGimmickData
            {
                PlayerId = LocalPlayerId,
                Kind = LocalGamePlayerRejectedKind,
                ObjectId = gamePlayerId
            };
            // The backend normally does not echo the host's own packet. Apply
            // locally first so an over-cap local P2 is removed as well.
            GimmickDataReceived?.Invoke(rejection);
            SendGimmickData(rejection);
        }

        private static void NormalizeGamePlayerSlots(OnlineLobbyInfo lobby)
        {
            if (lobby?.Players == null) return;
            string hostConnectionId = null;
            for (int i = 0; i < lobby.Players.Length; i++)
                if (lobby.Players[i] != null && lobby.Players[i].IsHost)
                {
                    hostConnectionId = lobby.Players[i].PlayerId;
                    break;
                }
            List<OnlinePlayerInfo> ordered = new List<OnlinePlayerInfo>();
            for (int i = 0; i < lobby.Players.Length; i++)
                if (lobby.Players[i] != null && !string.IsNullOrEmpty(lobby.Players[i].PlayerId))
                    ordered.Add(lobby.Players[i]);
            ordered.Sort((left, right) =>
            {
                string leftConnection = string.IsNullOrEmpty(left.ConnectionId)
                    ? left.PlayerId
                    : left.ConnectionId;
                string rightConnection = string.IsNullOrEmpty(right.ConnectionId)
                    ? right.PlayerId
                    : right.ConnectionId;
                bool leftHostGroup = leftConnection == hostConnectionId;
                bool rightHostGroup = rightConnection == hostConnectionId;
                if (leftHostGroup != rightHostGroup) return leftHostGroup ? -1 : 1;
                int connectionOrder = string.CompareOrdinal(leftConnection, rightConnection);
                if (connectionOrder != 0) return connectionOrder;
                int localOrder = left.LocalPlayerIndex.CompareTo(right.LocalPlayerIndex);
                return localOrder != 0 ? localOrder : string.CompareOrdinal(left.PlayerId, right.PlayerId);
            });
            for (int i = 0; i < ordered.Count; i++) ordered[i].PlayerSlot = Mathf.Clamp(i, 0, 3);
        }

        private void OnBackendBodyDataReceived(OnlineBodyData bodyData)
        {
            inboundPacketCount++;
            bool contentAccepted = bodyData != null && AcceptRemoteContent(bodyData.ContentFingerprint);
            bool sane = bodyData != null && IsSaneBodyData(bodyData);
            bool validOwner = bodyData != null
                && !string.IsNullOrEmpty(bodyData.ConnectionId)
                && bodyData.LocalPlayerIndex >= 0
                && bodyData.LocalPlayerIndex < StageManager.MaxLocalPlayersPerConnection
                && OnlinePlayerIdentity.Compose(bodyData.ConnectionId, bodyData.LocalPlayerIndex)
                    == bodyData.PlayerId;
            if (LocalMultiplayerDebugMode.Enabled)
            {
                Debug.Log($"[PICO BODY] RECEIVE id={bodyData?.PlayerId ?? "<null>"} rev={bodyData?.Revision ?? 0} bytes={bodyData?.Json?.Length ?? 0} content={contentAccepted} sane={sane}");
            }
            if (bodyData == null || !contentAccepted || !sane || !validOwner
                || rejectedGamePlayerUntil.TryGetValue(bodyData.PlayerId, out float rejectedUntil)
                    && Time.unscaledTime < rejectedUntil)
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
            inboundPacketCount++;
            if (carryData == null || !AcceptRemoteContent(carryData.ContentFingerprint)
                || !IsSaneVector(carryData.ReleaseVelocity, 1000f)
                || !IsSaneVector(carryData.ReleasePosition, 10000f)
                || !IsSaneVector(carryData.LocalOffset, 100f)
                || !IsCurrentStageSession(
                    carryData.StageId, carryData.StageRevision, carryData.RetryRevision, false))
            {
                return;
            }
            CarryDataReceived?.Invoke(carryData);
        }

        private void OnBackendGimmickDataReceived(OnlineGimmickData gimmickData)
        {
            inboundPacketCount++;
            bool realtime = gimmickData != null && !string.IsNullOrEmpty(gimmickData.Kind)
                && (gimmickData.Kind == "transform"
                    || gimmickData.Kind.EndsWith("_state", StringComparison.Ordinal)
                    || gimmickData.Kind.EndsWith("_snapshot", StringComparison.Ordinal)
                    || gimmickData.Kind.EndsWith("_input", StringComparison.Ordinal)
                    || gimmickData.Kind.EndsWith("_contact", StringComparison.Ordinal));
            if (TryScheduleInbound(() => DeliverBackendGimmickData(gimmickData), realtime)) return;
            DeliverBackendGimmickData(gimmickData);
        }

        private void DeliverBackendGimmickData(OnlineGimmickData gimmickData)
        {
            bool isRetry = gimmickData != null && gimmickData.Kind == "stage_retry";
            if (gimmickData == null || !AcceptRemoteContent(gimmickData.ContentFingerprint)
                || (gimmickData.Json != null && gimmickData.Json.Length > 1024 * 1024)
                || !IsCurrentStageSession(
                    gimmickData.StageId, gimmickData.StageRevision, gimmickData.RetryRevision, isRetry))
            {
                return;
            }
            if (gimmickData.Kind == "local_game_player_left"
                && OnlinePlayerIdentity.IsOwnedByConnection(gimmickData.ObjectId, gimmickData.PlayerId))
            {
                RemoveVirtualGamePlayer(gimmickData.ObjectId);
                return;
            }
            if (gimmickData.Kind == LocalGamePlayerRejectedKind
                && IsHostPlayer(gimmickData.PlayerId)
                && !string.IsNullOrEmpty(gimmickData.ObjectId))
            {
                rejectedGamePlayerUntil[gimmickData.ObjectId] = Time.unscaledTime + 2.5f;
                RemoveVirtualGamePlayer(gimmickData.ObjectId);
                GimmickDataReceived?.Invoke(gimmickData);
                return;
            }
            if (HandleNetworkDiagnostic(gimmickData)) return;
            GimmickDataReceived?.Invoke(gimmickData);
        }

        private void UpdateNetworkDiagnostics()
        {
            if (!LocalMultiplayerDebugMode.NetworkMonitor || CurrentLobby == null
                || string.IsNullOrEmpty(LocalPlayerId) || IsHostPlayer(LocalPlayerId)
                || Time.unscaledTime < nextDiagnosticPingAt) return;
            nextDiagnosticPingAt = Time.unscaledTime + 2f;
            int token = ++nextDiagnosticPingToken;
            pendingDiagnosticPings[token] = Time.unscaledTime;
            if (pendingDiagnosticPings.Count > 16)
            {
                var stale = new List<int>();
                foreach (KeyValuePair<int, float> pair in pendingDiagnosticPings)
                    if (Time.unscaledTime - pair.Value > 10f) stale.Add(pair.Key);
                for (int i = 0; i < stale.Count; i++) pendingDiagnosticPings.Remove(stale[i]);
            }
            SendGimmickData(new OnlineGimmickData
            {
                ObjectId = DiagnosticObjectId,
                Kind = DiagnosticPingRequestKind,
                Json = JsonUtility.ToJson(new NetworkDiagnosticPing { Token = token })
            });
        }

        private bool HandleNetworkDiagnostic(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != DiagnosticObjectId) return false;
            NetworkDiagnosticPing ping = JsonUtility.FromJson<NetworkDiagnosticPing>(data.Json);
            if (ping == null || ping.Token <= 0) return true;
            if (data.Kind == DiagnosticPingRequestKind && IsHostPlayer(LocalPlayerId))
            {
                ping.TargetPlayerId = data.PlayerId;
                SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = DiagnosticObjectId,
                    Kind = DiagnosticPingReplyKind,
                    Json = JsonUtility.ToJson(ping)
                });
                return true;
            }
            if (data.Kind == DiagnosticPingReplyKind && IsHostPlayer(data.PlayerId)
                && ping.TargetPlayerId == LocalPlayerId
                && pendingDiagnosticPings.TryGetValue(ping.Token, out float sentAt))
            {
                pendingDiagnosticPings.Remove(ping.Token);
                LastNetworkRoundTripMs = Mathf.Max(0f, (Time.unscaledTime - sentAt) * 1000f);
            }
            return true;
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!LocalMultiplayerDebugMode.NetworkMonitor) return;
            GUI.Box(new Rect(8f, 8f, 290f, 70f), string.Empty);
            string role = IsHostPlayer(LocalPlayerId) ? "HOST" : "CLIENT";
            string rtt = LastNetworkRoundTripMs >= 0f ? $"{LastNetworkRoundTripMs:0}ms" : "--";
            GUI.Label(new Rect(18f, 14f, 270f, 20f), $"NET {role}  RTT {rtt}");
            GUI.Label(new Rect(18f, 34f, 270f, 20f),
                $"RX {InboundPacketCount}  DROP {SimulatedDropCount}  CORR {NetworkCorrectionCount}");
            GUI.Label(new Rect(18f, 54f, 270f, 20f),
                $"SIM {LocalMultiplayerDebugMode.SimulatedLatencySeconds * 1000f:0}ms / {LocalMultiplayerDebugMode.SimulatedLoss01 * 100f:0}%");
#endif
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
                    if (part == null || !IsFinite(part.Ink) || part.Ink < 0f
                        || part.Ink > DrawManager.IndividualInkLimit + 0.01f
                        || part.Points == null || part.Points.Length > 20000
                        || part.StrokeWidths != null && part.StrokeWidths.Length > part.Points.Length)
                        return false;
                    if (part.StrokeWidths != null)
                    {
                        for (int widthIndex = 0; widthIndex < part.StrokeWidths.Length; widthIndex++)
                        {
                            float width = part.StrokeWidths[widthIndex];
                            if (!IsFinite(width) || width < 1f || width > 30f) return false;
                        }
                    }
                    totalInk += part.Ink;
                    if (totalInk > DrawManager.IndividualInkLimit + 0.01f) return false;
                    for (int pointIndex = 0; pointIndex < part.Points.Length; pointIndex++)
                    {
                        Vector2 point = part.Points[pointIndex];
                        if (DrawManager.IsBreakPoint(point))
                        {
                            // DrawManager stores a deliberate (NaN, NaN) marker
                            // between separate strokes of the same body part.
                            // It is data structure syntax, not a gameplay position.
                            if (!float.IsNaN(point.x) || !float.IsNaN(point.y)) return false;
                            continue;
                        }
                        if (!IsSaneVector(point, 10000f)) return false;
                    }
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
