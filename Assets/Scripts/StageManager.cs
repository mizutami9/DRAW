using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed partial class StageManager : MonoBehaviour
    {
        public const int MaxLocalPlayersPerConnection = 2;
        private const string GimmickKindClearRequest = "stage_clear_request";
        private const string GimmickKindClear = "stage_clear";
        private const string GimmickKindGoalState = "player_goal_state";
        private const string GimmickKindRetry = "stage_retry";
        private const string GimmickKindSessionEnded = "session_ended";
        private const string GimmickKindCollectRequest = "collect_request";
        private const string GimmickKindCollectState = "collect_state";
        private const string GimmickKindCollectSnapshot = "collect_snapshot";
        private const string GimmickKindChallengeFailed = "challenge_failed";
        private const string GimmickKindSpeciesSwapRequest = "species_swap_request";
        private const string GimmickKindSpeciesSwapResponse = "species_swap_response";
        private const string GimmickKindSpeciesSwapApply = "species_swap_apply";
        private const string GimmickKindChallengeSessionRequest = "challenge_session_request";
        private const string GimmickKindChallengeSessionState = "challenge_session_state";
        private const string GimmickKindPlayerAbilityEffect = "player_ability_effect";
        private const string GimmickKindSlimeBoxBreakRequest = "slime_box_break_request";
        private const string GimmickKindSlimeBoxBreakState = "slime_box_break_state";
        private const float LowestStageObjectFallMargin = 8f;
        private const float ChallengeStartCountdownDuration = 4f;
        private const float ChallengeTimeUpReturnDelay = 5f;
        private const float RespawnCollapseDuration = 0.28f;
        private const float RespawnPauseDuration = 0.34f;
        private const float RespawnAppearDuration = 0.30f;
        private const float RespawnGraceDuration = 0.85f;

        [SerializeField] private PlayerController2D player;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private StageLoader stageLoader;
        [SerializeField] private RuntimeStageEditor stageEditor;
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float fallResetY = -6f;
        [SerializeField] private LayerMask groundLayer = 1 << 6;
        [SerializeField] private float groundSeparation = 0.06f;

        private PlayerController2D primaryPlayer;
        private PlayerController2D secondaryPlayer;
        private readonly List<PlayerController2D> localPlayers =
            new List<PlayerController2D>(MaxLocalPlayersPerConnection);
        private Vector3 stablePlayerLocalScale = Vector3.one;
        private bool drawing;
        private bool cleared;
        private bool stageStarted;
        private bool stageEditing;
        private bool testingEditedStage;
        private bool stageSelectEditMode;
        private bool stageSelectReturnToMultiLobby;
        private bool stageSelectRemoteWaiting;
        private string onlineSessionLobbyId;
        private bool onlineStageLoadRequired = true;
        private bool titleMode;
        private bool titleTextInputActive;
        private bool optionOverlayActive;
        private bool gameplayMenuOverlayActive;
        private bool onlineStateSubscribed;
        private bool onlineSessionEnding;
        private string endingOnlineLobbyId;
        private string currentStageId = "1-0";
        private string remotePlayerId;
        private string onlineCarrierPlayerId;
        private readonly Dictionary<string, PlayerController2D> onlineRemotePlayers =
            new Dictionary<string, PlayerController2D>();
        private bool onlineCarryHeld;
        private PlayerController2D onlineCarriedLocalPlayer;
        private Rigidbody2D onlineCarryBody;
        private RigidbodyType2D onlineCarryPreviousBodyType;
        private float onlineCarryPreviousGravityScale;
        private bool onlineCarryPreviousFreezeRotation;
        private bool onlineCarryIsCatGrab;
        private float onlineCarryBeganAt;
        private float onlineCarryLastConfirmedAt;
        private string lastOnlineThrowCarrierId;
        private int lastOnlineThrowEventSequence;
        private float staleCarrySnapshotGuardUntil;
        private Vector2 onlineCarryLocalOffset;
        private readonly List<Collider2D> onlineCarryColliders = new List<Collider2D>();
        private readonly List<bool> onlineCarryColliderEnabledStates = new List<bool>();
        private readonly List<RaycastHit2D> onlineCarrySweepHits = new List<RaycastHit2D>(32);
        private bool onlineCarryHasBounds;
        private Vector2 onlineCarryBoundsCenterOffset;
        private Vector2 onlineCarryBoundsExtents;
        private readonly Dictionary<string, string> knownOnlineLobbyPlayers =
            new Dictionary<string, string>();
        private readonly Dictionary<PlayerController2D, DrawManager.DrawingState> drawingStates =
            new Dictionary<PlayerController2D, DrawManager.DrawingState>();
        private readonly Dictionary<PlayerController2D, IndependentRedrawState> independentRedrawStates =
            new Dictionary<PlayerController2D, IndependentRedrawState>();
        private readonly Dictionary<PlayerController2D, RespawnAnimationState> respawnAnimations =
            new Dictionary<PlayerController2D, RespawnAnimationState>();
        private bool fullStageRetryScheduled;
        private int lastAppliedOnlineRetryRevision = -1;
        private readonly Dictionary<PlayerController2D, float> respawnGraceUntil =
            new Dictionary<PlayerController2D, float>();
        private readonly HashSet<PlayerController2D> eliminatedPlayers = new HashSet<PlayerController2D>();
        private bool eliminationSpectating;
        private PlayerController2D spectatedPlayer;
        private readonly Dictionary<PlayerController2D, Dictionary<Collider2D, bool>> onlineEliminationColliderStates =
            new Dictionary<PlayerController2D, Dictionary<Collider2D, bool>>();
        private readonly Dictionary<PlayerController2D, Vector3> assignedPlayerStartPositions =
            new Dictionary<PlayerController2D, Vector3>();
        private Material respawnBurstMaterial;
        private Coroutine redrawRespawnRoutine;
        private SpeciesSwapMessage pendingIncomingSpeciesSwap;
        private SpeciesSwapMessage pendingOutgoingSpeciesSwap;
        private DrawManager.Species? pendingSpeciesSwapPrompt;
        private PlayerController2D pendingSpeciesSwapRequester;
        private DrawManager.DrawingState pendingSpeciesSwapDrawingState;
        private float pendingIncomingSpeciesSwapExpiresAt;
        private float pendingOutgoingSpeciesSwapExpiresAt;
        private PlayerController2D redrawReturnPlayer;
        private Vector3 redrawReturnPosition;
        private bool hasRedrawReturnPosition;
        private float redrawReturnBottomY;
        private bool hasRedrawReturnBottom;
        private readonly HashSet<PlayerController2D> localPlayersAtGoal = new HashSet<PlayerController2D>();
        private readonly HashSet<string> onlinePlayerIdsAtGoal = new HashSet<string>();
        private readonly HashSet<string> collectedObjectIds = new HashSet<string>();
        private readonly HashSet<string> slimeBrokenObjectIds = new HashSet<string>();
        private StageRuleMode stageRuleMode;
        private StageObjectType collectionTarget = StageObjectType.CollectibleFish;
        private int requiredCollectionCount;
        private int totalCollectionTargetCount;
        private int collectedCount;
        private float challengeRemaining;
        private bool challengeFailed;
        private float nextChallengeSyncAt;
        private float nextSlimeBreakSyncAt;
        private bool challengeStarting;
        private float challengeStartCountdownRemaining;
        private float challengeTimeUpReturnRemaining;
        private string lastChallengeCountdownSfxText = string.Empty;
        private bool challengeStartPositionsCaptured;
        private StageEliminationChallengeController survivalController;
        private StageBlockBreakerController blockBreakerController;
        private StageChallengeReadyRoomController challengeReadyRoom;
        private bool challengeRunStarted;
        private Coroutine spawnFitValidationRoutine;
        private bool spawnCorrectionRequired;
        private bool drawingOpenedForSpawnCorrection;
        private TrailerCoopDemoController trailerDemo;
        private SteamHeaderCaptureController steamHeaderCapture;
        private readonly Dictionary<PlayerController2D, Vector3> localChallengeStartPositions =
            new Dictionary<PlayerController2D, Vector3>();
        public bool IsTimedCollectionChallenge => stageRuleMode == StageRuleMode.TimedCollection;
        public bool IsUntimedCollectionChallenge => IsTimedCollectionChallenge && currentStageId == "9-2";
        public bool IsSurvivalChallenge => stageRuleMode == StageRuleMode.Survival;
        private bool UsesEliminationController => IsSurvivalChallenge
            || currentStageId == "9-2"
            || currentStageId == "11-1"
            || currentStageId == "2-3"
            || currentStageId == "5-3";
        public bool IsBlockBreakerChallenge => stageRuleMode == StageRuleMode.BlockBreaker;
        public bool IsDrawingMode => drawing;
        public bool IsStageCleared => cleared;
        public bool IsTitleMode => titleMode;
        public bool IsGameplayActive => stageStarted && !titleMode && !stageEditing && !drawing && !cleared;
        public bool ShouldUseLocalSplitScreen => stageStarted && !titleMode && !stageEditing
            && GetComponent<LocalCoopSessionManager>()?.HasDedicatedSecondaryPlayers == true;
        public bool ShouldEnableLocalGameplayControls => IsGameplayActive && !optionOverlayActive
            && !gameplayMenuOverlayActive && !titleTextInputActive;
        public bool ShouldEnableLocalCharacterControls => stageStarted && !stageEditing
            && !drawing && !cleared && !optionOverlayActive
            && !gameplayMenuOverlayActive && !titleTextInputActive;
        public float ChallengeRemainingSeconds => challengeRemaining;
        public bool ChallengeTimeUp => challengeFailed;
        public string ChallengeFailureDisplayText => LocalizationManager.T(
            currentStageId == "12-1" || currentStageId == "12-2" || currentStageId == "12-3"
                ? "game_over"
                : "challenge_time_up");
        public StageObjectType ChallengeCollectionTarget => collectionTarget;
        public int ChallengeCollectedCount => collectedCount;
        public int ChallengeRequiredCollectionCount => requiredCollectionCount;
        public int ChallengeTotalCollectionTargetCount => totalCollectionTargetCount;
        public bool ChallengeStarting => challengeStarting;
        public bool IsChallengeReadyRoomActive => challengeReadyRoom != null && !challengeRunStarted;
        public bool CanUseGameplayCharacterControls
        {
            get
            {
                if (!stageStarted || drawing) return true;
                // A redraw floor is the shared safe place for both redrawing and
                // offline character management, including after a challenge has
                // left its ready room.
                return StageRedrawZone.IsPlayerInZone(player);
            }
        }
        private bool HasDedicatedSecondaryInput =>
            GetComponent<LocalCoopSessionManager>()?.HasDedicatedSecondaryPlayers == true;
        // 8-3 suppresses the normal PlayerController2D movement and moves each
        // player through its lane controller instead.
        private bool UsesStageManagedPlayerMovement => currentStageId == "8-3";
        public bool CanAddGameplayCharacter => CanUseGameplayCharacterControls
            && !HasDedicatedSecondaryInput
            && localPlayers.Count < MaxLocalPlayersPerConnection;
        public bool CanDeleteGameplayCharacter => CanUseGameplayCharacterControls
            && !HasDedicatedSecondaryInput
            && localPlayers.Count > 1;
        // Switching which already-created offline character is controlled does
        // not mutate stage layout or player count, so it is safe during normal
        // play on every stage. Adding/deleting still requires a safe zone.
        public bool CanSwitchGameplayCharacter => localPlayers.Count > 1
            && !HasDedicatedSecondaryInput
            && (CanUseGameplayCharacterControls
                || (stageStarted && !drawing && !cleared && !stageEditing));
        public string CurrentStageId => currentStageId;
        public bool RequiresUniquePlayerSpecies => StageSpeciesRules.RequiresUniqueSpecies(currentStageId);
        public string ChallengeStartCountdownText
        {
            get
            {
                if (!challengeStarting)
                {
                    return string.Empty;
                }
                if (currentStageId == "9-2")
                {
                    if (challengeStartCountdownRemaining > 2f) return "3";
                    if (challengeStartCountdownRemaining > 1f) return "2";
                    return "1";
                }
                if (challengeStartCountdownRemaining > 3f) return "3";
                if (challengeStartCountdownRemaining > 2f) return "2";
                if (challengeStartCountdownRemaining > 1f) return "1";
                return LocalizationManager.T("survival_start");
            }
        }

        [System.Serializable]
        private sealed class SpeciesSwapMessage
        {
            public string RequestId;
            public string RequesterId;
            public string TargetId;
            public int RequesterSpecies;
            public int TargetSpecies;
            public bool Accepted;
        }

        [System.Serializable]
        private sealed class ChallengeSessionMessage
        {
            public string TargetPlayerId;
            public bool RunStarted;
        }

        [System.Serializable]
        private sealed class PlayerAbilityEffectMessage
        {
            public string Effect;
            public Vector2 Direction;
            public string SourcePlayerId;
        }

        [System.Serializable]
        private sealed class SlimeBoxBreakMessage
        {
            public string StageId;
            public string ObjectId;
            public Vector2 HitPoint;
        }

        [System.Serializable]
        private sealed class PlayerGoalState
        {
            public bool Inside;
            public string GamePlayerId;
        }

        private sealed class RespawnAnimationState
        {
            public Coroutine Routine;
            public Vector3 OriginalScale;
            public Vector3 DeathPosition;
            public Rigidbody2D Body;
            public bool BodyWasSimulated;
            public float RespawnAt;
            public float RecoveryDeadline;
        }

        private sealed class IndependentRedrawState
        {
            public Vector3 ReturnPosition;
            public bool HasReturnBottom;
            public float ReturnBottomY;
            public Coroutine ValidationRoutine;
        }

        [System.Serializable]
        private sealed class CollectionState
        {
            public string CollectibleId;
            public string CollectorId;
            public int Count;
            public float RemainingSeconds;
            public bool PlayFeedback;
        }

        [System.Serializable]
        private sealed class CollectionSnapshotState
        {
            public string[] CollectibleIds;
            public int Count;
            public float RemainingSeconds;
        }

        private void Awake()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController2D>();
            }

            primaryPlayer = player;
            if (primaryPlayer != null)
            {
                localPlayers.Add(primaryPlayer);
                Vector3 authoredScale = primaryPlayer.transform.localScale;
                if (Mathf.Abs(authoredScale.x) > 0.1f && Mathf.Abs(authoredScale.y) > 0.1f)
                    stablePlayerLocalScale = authoredScale;
            }

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (drawManager == null)
            {
                drawManager = FindFirstObjectByType<DrawManager>();
            }

            if (stageLoader == null)
            {
                stageLoader = FindFirstObjectByType<StageLoader>();
            }

            if (stageEditor == null)
            {
                stageEditor = FindFirstObjectByType<RuntimeStageEditor>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindFirstObjectByType<OnlineManager>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<CameraFollow2D>();
            }

            ConfigureActivePlayerTargets();
            ApplyDefaultPlayerColors();
            SubscribeOnlineEvents();
            if (GetComponent<PlayerEmoteController>() == null)
            {
                gameObject.AddComponent<PlayerEmoteController>();
            }
            if (GetComponent<MultiplayerPlayerStatusHud>() == null)
            {
                gameObject.AddComponent<MultiplayerPlayerStatusHud>();
            }
            if (GetComponent<LocalCoopSessionManager>() == null)
            {
                gameObject.AddComponent<LocalCoopSessionManager>();
            }
        }

        private void Start()
        {
            EnterTitle();
            if (!PlayerNameSettings.IsConfigured) uiManager?.OpenOption(false);
        }

        private void OnEnable()
        {
            SubscribeOnlineEvents();
        }

        private void OnDisable()
        {
            GetComponent<LocalCoopSessionManager>()?.CancelAllDrawingSessions();
            CancelRespawnAnimations();
            if (onlineManager != null && onlineStateSubscribed)
            {
                onlineManager.StateChanged -= HandleOnlineStateChanged;
                onlineManager.GimmickDataReceived -= HandleOnlineGimmickData;
            }

            onlineStateSubscribed = false;
        }

        private void OnDestroy()
        {
            if (respawnBurstMaterial != null)
            {
                Destroy(respawnBurstMaterial);
            }
        }

        private void SubscribeOnlineEvents()
        {
            if (onlineStateSubscribed)
            {
                return;
            }

            if (onlineManager == null)
            {
                onlineManager = FindFirstObjectByType<OnlineManager>();
            }

            if (onlineManager != null)
            {
                onlineManager.StateChanged += HandleOnlineStateChanged;
                onlineManager.GimmickDataReceived += HandleOnlineGimmickData;
                onlineStateSubscribed = true;
            }
        }

        private void HandleOnlineStateChanged(OnlineConnectionState state, OnlineLobbyInfo lobby, string message)
        {
            if (lobby == null)
            {
                bool unexpectedDisconnect = state == OnlineConnectionState.Error
                    && !onlineSessionEnding
                    && !string.IsNullOrEmpty(onlineSessionLobbyId);
                knownOnlineLobbyPlayers.Clear();
                onlineSessionLobbyId = null;
                lastAppliedOnlineRetryRevision = -1;
                onlineStageLoadRequired = true;
                stageSelectReturnToMultiLobby = false;
                stageSelectRemoteWaiting = false;
                if (unexpectedDisconnect)
                {
                    EnterTitle();
                    uiManager?.SetMulti(true);
                    uiManager?.ShowGameplayNotice(
                        string.IsNullOrWhiteSpace(message)
                            ? LocalizationManager.T("online_session_disconnected")
                            : message,
                        6f);
                }
                return;
            }

            // EOS callbacks and P2P packets already queued before LeaveLobby can
            // arrive for a few frames afterwards. Never let an ended lobby's old
            // Playing snapshot reopen the stage over the title screen.
            if (onlineSessionEnding
                && (string.IsNullOrEmpty(endingOnlineLobbyId) || lobby.LobbyId == endingOnlineLobbyId))
            {
                return;
            }

            // Drawer-added characters share P1's device and are an offline-only
            // roster concept. Online local slots must each have a dedicated
            // device, including invite/direct-join paths that skip OpenMultiMenu.
            RemoveAutomaticSecondaryCharacters();

            if (onlineSessionLobbyId != lobby.LobbyId)
            {
                onlineSessionEnding = false;
                endingOnlineLobbyId = null;
                knownOnlineLobbyPlayers.Clear();
                onlineSessionLobbyId = lobby.LobbyId;
                lastAppliedOnlineRetryRevision = -1;
                onlineStageLoadRequired = true;
                stageSelectReturnToMultiLobby = false;
                stageSelectRemoteWaiting = false;
            }

            bool rosterMembershipChanged = HasOnlineRosterMembershipChanged(lobby);
            UpdateOnlineLobbyRoster(lobby);

            // Keep the shared camera/drawing target anchored to this machine's
            // primary avatar when an online stage starts. Dedicated P2 input
            // bindings remain enabled and OnlinePlayerSync transmits them too.
            if (state == OnlineConnectionState.Playing && primaryPlayer != null && player != primaryPlayer)
            {
                SetActivePlayer(primaryPlayer, true);
            }

            bool localHost = IsLocalOnlineHost(lobby);
            if (state == OnlineConnectionState.Playing
                && localHost
                && rosterMembershipChanged
                && RequiresRosterDependentStageRestart(currentStageId))
            {
                // These arenas bake player count/slots into lanes, rooms, boss
                // forms or difficulty during initialization. Rebuilding through
                // the existing authoritative retry path is safer than mutating
                // only the count and leaving incompatible runtime objects alive.
                ScheduleFullStageRetry();
                return;
            }
            if (state == OnlineConnectionState.Playing && !localHost)
            {
                string desiredStage = DemoAccessPolicy.NormalizeStageId(
                    !string.IsNullOrEmpty(lobby.StageId) ? lobby.StageId : "1-1");
                if (onlineStageLoadRequired || !stageStarted || currentStageId != desiredStage || stageSelectRemoteWaiting)
                {
                    onlineStageLoadRequired = false;
                    SelectStage(desiredStage);
                }
                return;
            }

            if (localHost)
            {
                if (state == OnlineConnectionState.Playing)
                {
                    TryClearWhenAllOnlinePlayersAtGoal();
                }
                return;
            }

            if (message == LocalizationManager.T("online_stage_select_opened"))
            {
                onlineStageLoadRequired = true;
                if (!stageSelectRemoteWaiting)
                {
                    OpenStageSelectWaitingForHost();
                }
            }
            else if (message == LocalizationManager.T("online_stage_select_closed"))
            {
                if (stageSelectRemoteWaiting)
                {
                    CloseStageSelectWaitingForHost();
                }
            }
        }

        private void UpdateOnlineLobbyRoster(OnlineLobbyInfo lobby)
        {
            OnlinePlayerInfo[] players = lobby?.Players;
            if (players == null) return;

            HashSet<string> currentIds = new HashSet<string>();
            for (int i = 0; i < players.Length; i++)
            {
                OnlinePlayerInfo member = players[i];
                if (member == null || string.IsNullOrEmpty(member.PlayerId)) continue;
                currentIds.Add(member.PlayerId);
            }

            if (!onlineSessionEnding && knownOnlineLobbyPlayers.Count > 0)
            {
                foreach (KeyValuePair<string, string> previous in knownOnlineLobbyPlayers)
                {
                    if (currentIds.Contains(previous.Key)
                        || OnlinePlayerIdentity.IsOwnedByConnection(
                            previous.Key, onlineManager?.LocalPlayerId)) continue;
                    string displayName = string.IsNullOrWhiteSpace(previous.Value)
                        ? LocalizationManager.Format("online_player_number", 1)
                        : previous.Value;
                    uiManager?.ShowGameplayNotice(
                        LocalizationManager.Format("online_player_left_notice", displayName), 3f);
                }
            }

            knownOnlineLobbyPlayers.Clear();
            for (int i = 0; i < players.Length; i++)
            {
                OnlinePlayerInfo member = players[i];
                if (member == null || string.IsNullOrEmpty(member.PlayerId)) continue;
                knownOnlineLobbyPlayers[member.PlayerId] = string.IsNullOrWhiteSpace(member.DisplayName)
                    ? LocalizationManager.Format("online_player_number", i + 1)
                    : member.DisplayName;
            }
        }

        private bool HasOnlineRosterMembershipChanged(OnlineLobbyInfo lobby)
        {
            OnlinePlayerInfo[] players = lobby?.Players;
            if (players == null || knownOnlineLobbyPlayers.Count == 0) return false;
            int validCount = 0;
            for (int i = 0; i < players.Length; i++)
            {
                string id = players[i]?.PlayerId;
                if (string.IsNullOrEmpty(id)) continue;
                validCount++;
                if (!knownOnlineLobbyPlayers.ContainsKey(id)) return true;
            }
            return validCount != knownOnlineLobbyPlayers.Count;
        }

        private static bool RequiresRosterDependentStageRestart(string stageId)
        {
            switch (stageId)
            {
                case "2-3":  // per-player chase lanes
                case "5-3":  // player-count crate arena
                case "8-3":  // player-count tilt-board layout
                case "9-3":  // ready rooms and rain density
                case "10-1": // per-player ice lanes and team time
                case "10-3": // player-count ricochet rooms
                case "13-3": // per-player circuit rooms
                case "14-3": // linked rooms / laser relay
                case "15-1": // flying boss pads and HP
                case "15-3": // mimic forms and HP
                    return true;
                default:
                    return false;
            }
        }

        public void EnterTitle()
        {
            GetComponent<LocalCoopSessionManager>()?.CancelAllDrawingSessions();
            if (drawing)
            {
                spawnCorrectionRequired = false;
                drawingOpenedForSpawnCorrection = false;
                drawManager?.CancelEditing();
                ExitDrawingMode();
            }
            if (currentStageId != "title")
            {
                Debug.LogWarning($"[PICO STAGE] EnterTitle requested from stage={currentStageId} " +
                    $"onlineState={onlineManager?.State} lobby={onlineManager?.CurrentLobby?.LobbyId ?? "none"}.");
            }
            StopTrailerDemo();
            StopSteamHeaderCapture();
            ResetSpeciesSwapState();
            CancelRespawnAnimations();
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(false);
            currentStageId = "title";
            GameBgm.PlayTitle();
            drawManager?.SetAllowedSpecies(StageSpeciesMask.All);
            titleMode = true;
            titleTextInputActive = false;
            stageStarted = true;
            stageEditing = false;
            drawing = false;
            cleared = false;
            ResetGoalProgress();
            Time.timeScale = 1f;
            ClearTransientStageVisuals();
            StartCoroutine(ClearTransientStageVisualsAfterTransition());
            stageLoader?.HideStages();
            stageLoader?.ShowTitlePlayground();
            ConfigureTitlePlaygroundCamera();
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetTitle(true);
            ConfigureStageRule(null);
            drawManager?.SetActive(false);
            RestoreLocalPlayerPhysicsForStageEntry();
            RespawnPlayers();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void OpenSingleMenu()
        {
            titleMode = false;
            OpenStageSelect();
        }

        public void OpenMultiMenu()
        {
            RemoveAutomaticSecondaryCharacters();
            uiManager?.SetMulti(true);
        }

        private void RemoveAutomaticSecondaryCharacters()
        {
            for (int i = localPlayers.Count - 1; i >= 1; i--)
            {
                PlayerController2D local = localPlayers[i];
                LocalPlayerInputBinding binding = local != null ? local.InputBinding : null;
                if (binding == null
                    || binding.Kind == LocalPlayerInputBinding.DeviceKind.AutoPrimary)
                {
                    RemoveLocalCharacter(local);
                }
            }
        }

        public void SetTitleTextInputActive(bool active)
        {
            if (titleTextInputActive == active)
            {
                return;
            }
            titleTextInputActive = active;
            if (!titleMode)
            {
                return;
            }

            PlayerController2D localPlayer = player != null ? player : primaryPlayer;
            if (localPlayer == null)
            {
                return;
            }
            if (active)
            {
                localPlayer.ResetMotion();
            }
            localPlayer.SetControlsEnabled(!active && !optionOverlayActive
                && stageStarted && !drawing && !cleared && !stageEditing);
        }

        public void OpenOptionMenu()
        {
            uiManager?.OpenOption(stageStarted && !titleMode);
        }

        public void SetOptionOverlayActive(bool active)
        {
            optionOverlayActive = active;
            if (active)
            {
                for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            }
            SetAllPlayerControls(!active && !gameplayMenuOverlayActive
                && stageStarted && !drawing && !cleared
                && !stageEditing && !titleTextInputActive);
        }

        public void SetGameplayMenuOverlayActive(bool active)
        {
            gameplayMenuOverlayActive = active;
            if (active)
            {
                for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            }
            SetAllPlayerControls(!active && !optionOverlayActive
                && stageStarted && !drawing && !cleared
                && !stageEditing && !titleTextInputActive);
        }

        public void OpenTrailerDebugMenu()
        {
            TrailerDebugMenuController menu = FindFirstObjectByType<TrailerDebugMenuController>();
            menu?.Show();
        }

        public void StartTrailerCoopDemo()
        {
            StopTrailerDemo();
            StopSteamHeaderCapture();
            titleMode = false;
            stageStarted = false;
            stageEditing = false;
            drawing = false;
            cleared = false;
            currentStageId = "trailer-debug-01";
            Time.timeScale = 1f;
            uiManager?.SetTitle(false);
            uiManager?.SetStageSelect(false);
            drawManager?.SetActive(false);
            stageLoader?.HideStages();
            SetCameraFollowEnabled(false);

            GameObject root = new GameObject("Trailer Coop Demo");
            trailerDemo = root.AddComponent<TrailerCoopDemoController>();
            trailerDemo.Configure(this, player != null ? player.gameObject : null, cameraFollow, drawManager);
        }

        public void ExitTrailerCoopDemo()
        {
            EnterTitle();
        }

        public void StartSteamHeaderCapture()
        {
            StopTrailerDemo();
            StopSteamHeaderCapture();
            titleMode = false;
            stageStarted = false;
            stageEditing = false;
            drawing = false;
            cleared = false;
            currentStageId = "steam-header-capture";
            Time.timeScale = 1f;
            uiManager?.SetTitle(false);
            uiManager?.SetStageSelect(false);
            drawManager?.SetActive(false);
            stageLoader?.HideStages();
            SetCameraFollowEnabled(false);

            GameObject root = new GameObject("Steam Header Capture");
            steamHeaderCapture = root.AddComponent<SteamHeaderCaptureController>();
            steamHeaderCapture.Configure(
                this,
                player != null ? player.gameObject : null,
                cameraFollow,
                drawManager);
        }

        public void ExitSteamHeaderCapture()
        {
            EnterTitle();
        }

        private void StopTrailerDemo()
        {
            if (trailerDemo == null)
            {
                trailerDemo = FindFirstObjectByType<TrailerCoopDemoController>();
            }
            if (trailerDemo == null)
            {
                return;
            }

            trailerDemo.RestoreScene();
            Destroy(trailerDemo.gameObject);
            trailerDemo = null;
        }

        private void StopSteamHeaderCapture()
        {
            if (steamHeaderCapture == null)
            {
                steamHeaderCapture = FindFirstObjectByType<SteamHeaderCaptureController>();
            }
            if (steamHeaderCapture == null)
            {
                return;
            }

            steamHeaderCapture.RestoreScene();
            Destroy(steamHeaderCapture.gameObject);
            steamHeaderCapture = null;
        }

        public void CloseTitleSubmenu()
        {
            uiManager?.SetMulti(false);
            uiManager?.CloseOption();
        }

        public void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public bool IsOnlineStageActive => IsOnlineInStage();
        public bool IsOnlineStageHost => IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby);
        public DrawManager.Species ActiveLocalSpecies => player != null
            ? player.CurrentSpecies
            : DrawManager.Species.Human;

        public void RequestLeaveSession()
        {
            uiManager?.ShowLeaveSessionConfirm(IsOnlineStageHost);
        }

        public void ConfirmLeaveSession()
        {
            if (IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                // Clients leave as soon as they receive the shutdown message.
                // Suppress those resulting roster removals on the host: they
                // are part of closing the room, not individual player exits.
                onlineSessionEnding = true;
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindSessionEnded,
                    Json = "{}"
                });
                StartCoroutine(CompleteLeaveSessionAfterBroadcast());
                return;
            }

            CompleteLeaveSession();
        }

        private IEnumerator CompleteLeaveSessionAfterBroadcast()
        {
            yield return new WaitForSecondsRealtime(0.15f);
            CompleteLeaveSession();
        }

        private void CompleteLeaveSession()
        {
            endingOnlineLobbyId = onlineManager?.CurrentLobby?.LobbyId ?? onlineSessionLobbyId;
            onlineSessionEnding = true;
            onlineManager?.LeaveLobby();
            uiManager?.HideLeaveSessionConfirm();
            uiManager?.HideGameplayNotice();
            EnterTitle();
        }

        private void Update()
        {
            if (!stageStarted)
            {
                return;
            }

            if (onlineCarryHeld
                && onlineCarryLastConfirmedAt > 0f
                && Time.unscaledTime - onlineCarryLastConfirmedAt > 1.25f)
            {
                EndOnlineCarry(Vector2.zero);
            }

            RecoverStalledRespawns();
            UpdateEliminationSpectatorControls();

            UpdateSpeciesSwapTimeouts();
            BroadcastSlimeBreakStatesIfNeeded();

            if (stageEditing)
            {
                return;
            }

            LocalPlayerInputBinding localInput = player != null ? player.InputBinding : null;
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            bool gamepadCancel = localCoop != null
                ? localCoop.AnyLocalCancelPressed()
                : localInput != null && localInput.CancelPressed();
            bool gamepadMenu = localCoop != null
                ? localCoop.AnyLocalMenuPressed()
                : localInput != null && localInput.MenuPressed();
            bool gamepadDrawToggle = localInput != null && localInput.DrawTogglePressed();
            if (gamepadMenu && localCoop != null && localCoop.IsJoinCommandThisFrame())
                gamepadMenu = false;
            if (gamepadCancel && localCoop != null && localCoop.IsLeaveCommandThisFrame())
                gamepadCancel = false;

            bool independentDrawingActive = localCoop != null && localCoop.AnyDrawingSessionActive;

            if ((Input.GetKeyDown(KeyCode.Escape) || gamepadCancel && !drawing)
                && !independentDrawingActive)
            {
                if (testingEditedStage)
                {
                    ReturnToStageEditor();
                    return;
                }
                else if (uiManager != null && uiManager.IsTitleSubmenuShowing)
                {
                    if (!uiManager.TryCloseOptionPopup())
                    {
                        CloseTitleSubmenu();
                    }
                }
                else if (drawing)
                {
                    RequestCancelDrawingMode();
                }
                else if (!cleared && (Input.GetKeyDown(KeyCode.Escape)
                    || uiManager != null && uiManager.IsGameplayOverlayShowing))
                {
                    uiManager?.ToggleMenuFromEscape();
                }
            }

            if (gamepadMenu && !drawing && !cleared && !titleMode
                && !independentDrawingActive)
            {
                uiManager?.ToggleMenuFromEscape();
            }

            bool localSplitHandlesDrawing = localCoop != null
                && localCoop.HasDedicatedSecondaryPlayers;
            if (gamepadDrawToggle && !localSplitHandlesDrawing
                && !cleared && !titleMode && !optionOverlayActive && !gameplayMenuOverlayActive)
            {
                if (drawing) RequestCancelDrawingMode();
                else EnterDrawingMode();
            }

            // The player list can briefly retain an automatic/inactive slot
            // while returning from local co-op. Only a genuinely assigned
            // secondary input should hand TAB over to the split-screen handler.
            if (Input.GetKeyDown(KeyCode.Tab) && !cleared
                && (localCoop == null || !localCoop.HasDedicatedSecondaryPlayers))
            {
                if (drawing)
                {
                    RequestCancelDrawingMode();
                }
                else
                {
                    uiManager?.ToggleGameplayHudDrawer();
                }
            }

            if ((Input.GetKeyDown(KeyCode.Q) || localInput != null && localInput.ClearPressed())
                && !titleMode && !drawing && !cleared
                && !optionOverlayActive && !IsOnlineInStage()
                && (localCoop == null || !localCoop.HasDedicatedSecondaryPlayers)
                && player != null && (player.ControlsEnabled || UsesStageManagedPlayerMovement))
            {
                SwitchCharacter();
            }

            if (!drawing && !cleared)
            {
                RespawnFallenPlayers();
            }

            UpdateTimedCollectionChallenge();
        }

        public void ClearStage()
        {
            // A completed stage controller can remain alive for the frame in
            // which the stage-select UI opens. Ignore its repeated completion
            // callback once gameplay has stopped, otherwise the clear overlay
            // is immediately opened again on top of stage select (notably 9-3).
            if (cleared || !stageStarted)
            {
                return;
            }

            if (IsOnlineInStage())
            {
                OnlineLobbyInfo lobby = onlineManager.CurrentLobby;
                if (!IsLocalOnlineHost(lobby))
                {
                    onlineManager.SendGimmickData(new OnlineGimmickData
                    {
                        ObjectId = currentStageId,
                        Kind = GimmickKindClearRequest,
                        Json = "{}"
                    });
                    return;
                }

                ApplyClearStage();
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindClear,
                    Json = "{}"
                });
                return;
            }

            ApplyClearStage();
        }

        public void TryCollect(StageCollectible collectible)
        {
            if (collectible == null || cleared || challengeFailed || challengeStarting
                || stageRuleMode != StageRuleMode.TimedCollection
                || collectible.CollectibleType != collectionTarget)
            {
                return;
            }

            if (IsOnlineInStage() && !IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = collectible.ObjectId,
                    Kind = GimmickKindCollectRequest,
                    Json = "{}"
                });
                return;
            }

            string collectorId = IsOnlineInStage()
                ? GetOnlinePlayerId(collectible.CollectorTransform != null
                    ? collectible.CollectorTransform.GetComponent<PlayerController2D>()
                    : null)
                : null;
            ApplyCollectible(collectible.ObjectId, IsOnlineInStage(), collectorId);
        }

        private void ApplyCollectible(string objectId, bool broadcast, string collectorId = null)
        {
            if (string.IsNullOrEmpty(objectId) || !collectedObjectIds.Add(objectId))
            {
                return;
            }

            StageCollectible collectible = FindCollectible(objectId);
            if (collectible == null || collectible.CollectibleType != collectionTarget)
            {
                collectedObjectIds.Remove(objectId);
                return;
            }

            Transform collector = !string.IsNullOrEmpty(collectorId)
                ? GetOnlinePlayerTransform(collectorId)
                : collectible.CollectorTransform;
            collectible.ApplyCollected(collector, true);
            GameSfx.PlayAt(
                collectible.CollectibleType == StageObjectType.CollectibleCoin
                    ? SfxId.CoinCollect
                    : SfxId.CollectibleCollect,
                collectible.transform.position);
            collectedCount++;
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);

            CollectionState state = new CollectionState
            {
                CollectibleId = objectId,
                CollectorId = collectorId,
                Count = collectedCount,
                RemainingSeconds = challengeRemaining,
                PlayFeedback = true
            };
            if (broadcast && onlineManager != null)
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindCollectState,
                    Json = JsonUtility.ToJson(state)
                });
            }

            if (collectedCount >= requiredCollectionCount)
            {
                ClearStage();
            }
        }

        private void ApplyCollectibleState(CollectionState state)
        {
            if (state == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(state.CollectibleId))
            {
                collectedObjectIds.Add(state.CollectibleId);
                Transform collector = !string.IsNullOrEmpty(state.CollectorId)
                    ? GetOnlinePlayerTransform(state.CollectorId)
                    : null;
                StageCollectible collectible = FindCollectible(state.CollectibleId);
                if (collectible != null)
                {
                    Vector3 soundPosition = collectible.transform.position;
                    collectible.ApplyCollected(collector, state.PlayFeedback);
                    if (state.PlayFeedback)
                    {
                        GameSfx.PlayAt(
                            collectible.CollectibleType == StageObjectType.CollectibleCoin
                                ? SfxId.CoinCollect
                                : SfxId.CollectibleCollect,
                            soundPosition);
                    }
                }
            }
            collectedCount = Mathf.Max(collectedCount, state.Count);
            if (state.RemainingSeconds > 0f)
            {
                challengeRemaining = state.RemainingSeconds;
            }
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);
        }

        private StageCollectible FindCollectible(string objectId)
        {
            return stageLoader != null ? stageLoader.FindLoadedCollectible(objectId) : null;
        }

        private void ConfigureStageRule(StageData data)
        {
            stageRuleMode = data != null ? data.ruleMode : StageRuleMode.Normal;
            collectionTarget = data != null && IsCollectibleType(data.collectionTarget)
                ? data.collectionTarget
                : StageObjectType.CollectibleFish;
            challengeRemaining = Mathf.Clamp(data != null ? data.timeLimitSeconds : 60f, 5f, 1800f);
            if (data != null && data.id == "9-2")
            {
                challengeRemaining = 0f;
            }
            else if (data != null && data.id == "2-2")
            {
                int playerCount = Mathf.Clamp(GetInkBudgetPlayerCount(), 1, 4);
                challengeRemaining = playerCount >= 3 ? 45f : 60f;
            }
            else if (data != null && data.id == "12-2")
            {
                int playerCount = Mathf.Clamp(GetInkBudgetPlayerCount(), 1, 4);
                challengeRemaining = playerCount >= 4 ? 60f : playerCount == 3 ? 90f : 120f;
            }
            else if (data != null && data.id == "12-3")
            {
                int playerCount = Mathf.Clamp(GetInkBudgetPlayerCount(), 1, 4);
                challengeRemaining = playerCount >= 4
                    ? 50f
                    : playerCount == 3
                        ? 60f
                        : playerCount == 2 ? 75f : 90f;
            }
            collectedCount = 0;
            challengeFailed = false;
            challengeStarting = stageRuleMode == StageRuleMode.TimedCollection;
            challengeStartCountdownRemaining = challengeStarting
                ? currentStageId == "9-2" ? 3f : ChallengeStartCountdownDuration
                : 0f;
            challengeTimeUpReturnRemaining = 0f;
            lastChallengeCountdownSfxText = string.Empty;
            challengeStartPositionsCaptured = false;
            collectedObjectIds.Clear();
            nextChallengeSyncAt = Time.unscaledTime + 1f;

            totalCollectionTargetCount = CountCollectibles(collectionTarget);
            int configuredCount = data != null ? data.requiredCollectionCount : 1;
            requiredCollectionCount = configuredCount > 0
                ? Mathf.Clamp(configuredCount, 1, 2000)
                : totalCollectionTargetCount;
            if (data != null && data.id == "9-2")
            {
                int playerCount = Mathf.Clamp(GetInkBudgetPlayerCount(), 1, 4);
                requiredCollectionCount = Mathf.Min(totalCollectionTargetCount, playerCount * 150);
            }
            requiredCollectionCount = Mathf.Max(1, requiredCollectionCount);
            uiManager?.SetChallengeHud(
                stageRuleMode == StageRuleMode.TimedCollection,
                challengeRemaining,
                collectionTarget,
                collectedCount,
                requiredCollectionCount,
                false);
            uiManager?.SetChallengeCountdown(challengeStarting, ChallengeStartCountdownText);
        }

        private bool RequiresChallengeReadyRoom()
        {
            return stageRuleMode == StageRuleMode.Survival
                || stageRuleMode == StageRuleMode.BlockBreaker
                || currentStageId == "2-2"
                || currentStageId == "2-3"
                || currentStageId == "9-2"
                || currentStageId == "9-3"
                || currentStageId == "10-1"
                || currentStageId == "10-3"
                || currentStageId == "11-1"
                || currentStageId == "12-1"
                || currentStageId == "12-2"
                || currentStageId == "12-3"
                || currentStageId == "13-2"
                || currentStageId == "14-1";
        }

        private void UpdateTimedCollectionChallenge()
        {
            if (stageRuleMode != StageRuleMode.TimedCollection || cleared
                || stageEditing || !stageStarted || drawing || IsChallengeReadyRoomActive)
            {
                return;
            }

            if (challengeFailed)
            {
                uiManager?.SetChallengeCountdown(true, ChallengeFailureDisplayText);
                challengeTimeUpReturnRemaining = Mathf.Max(
                    0f,
                    challengeTimeUpReturnRemaining - Time.unscaledDeltaTime);
                if (challengeTimeUpReturnRemaining <= 0f
                    && (!IsOnlineInStage() || IsLocalOnlineHost(onlineManager.CurrentLobby)))
                {
                    RestartAfterChallengeTimeUp();
                }
                return;
            }

            if (challengeStarting)
            {
                if (currentStageId == "9-2")
                {
                    SetAllPlayerControls(true);
                }
                else
                {
                    HoldPlayersAtChallengeStart();
                }
                string countdownText = ChallengeStartCountdownText;
                uiManager?.SetChallengeCountdown(true, countdownText);
                if (countdownText != lastChallengeCountdownSfxText)
                {
                    lastChallengeCountdownSfxText = countdownText;
                    GameSfx.Play(string.Equals(countdownText, LocalizationManager.T("survival_start"), System.StringComparison.Ordinal)
                        ? SfxId.StageCountdownGo
                        : SfxId.StageCountdownTick);
                }
                challengeStartCountdownRemaining = Mathf.Max(
                    0f,
                    challengeStartCountdownRemaining - Time.unscaledDeltaTime);
                if (challengeStartCountdownRemaining <= 0f)
                {
                    challengeStarting = false;
                    challengeStartPositionsCaptured = false;
                    uiManager?.SetChallengeCountdown(false, string.Empty);
                    SetAllPlayerControls(true);
                }
                return;
            }

            if (!IsUntimedCollectionChallenge && !LocalMultiplayerDebugMode.NoTimeLimit)
                challengeRemaining = Mathf.Max(0f, challengeRemaining - Time.unscaledDeltaTime);
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);
            if (IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby)
                && Time.unscaledTime >= nextChallengeSyncAt)
            {
                nextChallengeSyncAt = Time.unscaledTime + 1f;
                BroadcastCollectionSnapshot();
            }
            if (IsUntimedCollectionChallenge
                || LocalMultiplayerDebugMode.NoTimeLimit
                || challengeRemaining > 0f
                || (IsOnlineInStage() && !IsLocalOnlineHost(onlineManager.CurrentLobby)))
            {
                return;
            }

            ApplyChallengeFailed();
            if (IsOnlineInStage())
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindChallengeFailed,
                    Json = "{}"
                });
            }
        }

        private void HoldPlayersAtChallengeStart()
        {
            if (!challengeStartPositionsCaptured)
            {
                localChallengeStartPositions.Clear();
                for (int i = 0; i < localPlayers.Count; i++)
                    if (localPlayers[i] != null)
                        localChallengeStartPositions[localPlayers[i]] = localPlayers[i].transform.position;
                challengeStartPositionsCaptured = true;
            }

            SetAllPlayerControls(false);
            foreach (KeyValuePair<PlayerController2D, Vector3> pair in localChallengeStartPositions)
            {
                if (pair.Key == null) continue;
                pair.Key.transform.position = pair.Value;
                pair.Key.ResetMotion();
            }
        }

        private void RestartAfterChallengeTimeUp()
        {
            if (IsOnlineInStage())
            {
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindRetry,
                    Json = "{}"
                });
                ApplyFullStageRetry();
                return;
            }

            ApplyFullStageRetry();
        }

        private void BroadcastCollectionSnapshot()
        {
            if (onlineManager == null)
            {
                return;
            }

            CollectionSnapshotState state = new CollectionSnapshotState
            {
                CollectibleIds = new List<string>(collectedObjectIds).ToArray(),
                Count = collectedCount,
                RemainingSeconds = challengeRemaining
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindCollectSnapshot,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyCollectionSnapshot(CollectionSnapshotState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.CollectibleIds != null)
            {
                for (int i = 0; i < state.CollectibleIds.Length; i++)
                {
                    string objectId = state.CollectibleIds[i];
                    if (string.IsNullOrEmpty(objectId) || !collectedObjectIds.Add(objectId))
                    {
                        continue;
                    }

                    FindCollectible(objectId)?.ApplyCollected(null, false);
                }
            }
            collectedCount = Mathf.Max(collectedCount, state.Count);
            if (state.RemainingSeconds > 0f)
            {
                challengeRemaining = state.RemainingSeconds;
            }
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);
        }

        private void ApplyChallengeFailed()
        {
            if (challengeFailed || cleared)
            {
                return;
            }
            challengeFailed = true;
            GameSfx.Play(SfxId.StageFailed);
            challengeStarting = false;
            challengeTimeUpReturnRemaining = ChallengeTimeUpReturnDelay;
            CancelDrawingMode();
            SetAllPlayerControls(false);
            uiManager?.SetChallengeCountdown(true, ChallengeFailureDisplayText);
            uiManager?.SetChallengeHud(true, 0f, collectionTarget, collectedCount, requiredCollectionCount, true);
        }

        private static bool IsCollectibleType(StageObjectType type)
        {
            return type == StageObjectType.CollectibleFish
                || type == StageObjectType.CollectibleCoin
                || type == StageObjectType.CollectibleStar;
        }

        private int CountCollectibles(StageObjectType type)
        {
            return stageLoader != null ? stageLoader.CountLoadedCollectibles(type) : 0;
        }

        public void SetPlayerGoalState(PlayerController2D goalPlayer, bool inside)
        {
            if (goalPlayer == null || cleared || !stageStarted || stageRuleMode != StageRuleMode.Normal)
            {
                return;
            }

            if (currentStageId == "10-1")
            {
                StageIceSpeedrunController speedrun = Object.FindFirstObjectByType<StageIceSpeedrunController>();
                speedrun?.SetPlayerGoalState(goalPlayer, inside);
                return;
            }

            bool firstPlayerWins = currentStageId == "11-1";

            if (IsOnlineInStage())
            {
                if (!localPlayers.Contains(goalPlayer) || onlineManager == null
                    || string.IsNullOrEmpty(onlineManager.LocalPlayerId))
                {
                    return;
                }

                string gamePlayerId = GetOnlinePlayerId(goalPlayer);
                SetOnlineGoalState(gamePlayerId, inside);
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindGoalState,
                    Json = JsonUtility.ToJson(new PlayerGoalState
                    {
                        Inside = inside,
                        GamePlayerId = gamePlayerId
                    })
                });
                if (inside && firstPlayerWins && IsLocalOnlineHost(onlineManager.CurrentLobby))
                    ClearStage();
                else
                    TryClearWhenAllOnlinePlayersAtGoal();
                return;
            }

            if (inside)
            {
                localPlayersAtGoal.Add(goalPlayer);
            }
            else
            {
                localPlayersAtGoal.Remove(goalPlayer);
            }

            if (inside && (firstPlayerWins || AreAllLocalPlayersAtGoal()))
            {
                ClearStage();
            }
        }

        private bool AreAllLocalPlayersAtGoal()
        {
            int requiredPlayers = 0;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D activePlayer = localPlayers[i];
                if (activePlayer == null || !activePlayer.isActiveAndEnabled || !activePlayer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                requiredPlayers++;
                if (!localPlayersAtGoal.Contains(activePlayer))
                {
                    return false;
                }
            }

            return requiredPlayers > 0;
        }

        private void SetOnlineGoalState(string playerId, bool inside)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }

            if (inside)
            {
                onlinePlayerIdsAtGoal.Add(playerId);
            }
            else
            {
                onlinePlayerIdsAtGoal.Remove(playerId);
            }
        }

        private void TryClearWhenAllOnlinePlayersAtGoal()
        {
            OnlineLobbyInfo lobby = onlineManager != null ? onlineManager.CurrentLobby : null;
            if (!IsLocalOnlineHost(lobby) || lobby?.Players == null || lobby.Players.Length == 0)
            {
                return;
            }

            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo lobbyPlayer = lobby.Players[i];
                StageSpikeChaseController spikeChase = currentStageId == "6-3"
                    ? Object.FindFirstObjectByType<StageSpikeChaseController>()
                    : null;
                if (lobbyPlayer != null && spikeChase != null && spikeChase.IsPlayerEliminated(lobbyPlayer.PlayerId))
                {
                    continue;
                }
                if (lobbyPlayer == null
                    || string.IsNullOrEmpty(lobbyPlayer.PlayerId)
                    || !onlinePlayerIdsAtGoal.Contains(lobbyPlayer.PlayerId))
                {
                    return;
                }
            }

            ClearStage();
        }

        private void ResetGoalProgress()
        {
            localPlayersAtGoal.Clear();
            onlinePlayerIdsAtGoal.Clear();
        }

        private void ApplyClearStage()
        {
            if (cleared)
            {
                return;
            }

            cleared = true;
            RecordStageClearProgress(currentStageId);
            GameSfx.Play(SfxId.StageClear);
            GetComponent<LocalCoopSessionManager>()?.CancelAllDrawingSessions();
            if (drawing) drawManager?.CancelEditing();
            ExitDrawingMode();
            SetAllPlayerControls(false);
            for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                pair.Value?.ResetMotion();
            }
            uiManager?.SetChallengeHud(false, 0f, collectionTarget, collectedCount, requiredCollectionCount, false);
            uiManager?.SetChallengeCountdown(false, string.Empty);
            uiManager?.SetCleared(true, currentStageId, GetNextStageId(currentStageId));
        }

        private void HandleOnlineGimmickData(OnlineGimmickData data)
        {
            if (data == null || onlineManager == null)
            {
                return;
            }

            if (data.Kind == OnlineManager.LocalGamePlayerRejectedKind
                && onlineManager.IsHostPlayer(data.PlayerId)
                && OnlinePlayerIdentity.IsOwnedByConnection(data.ObjectId, onlineManager.LocalPlayerId))
            {
                PlayerController2D rejected = GetOnlinePlayerController(data.ObjectId);
                if (rejected != null && rejected != primaryPlayer && localPlayers.Contains(rejected))
                {
                    RemoveLocalCharacter(rejected);
                    ShowLocalCoopNotice(LocalizationManager.T("local_player_rejected_full"));
                    GetComponent<LocalCoopSessionManager>()?.RefreshSplitScreen();
                }
                return;
            }

            if (OnlinePlayerIdentity.IsOwnedByConnection(data.PlayerId, onlineManager.LocalPlayerId)) return;

            if (data.Kind == GimmickKindChallengeSessionRequest
                && data.ObjectId == currentStageId
                && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                SendChallengeSessionState(data.PlayerId);
            }
            else if (data.Kind == GimmickKindChallengeSessionState
                && data.ObjectId == currentStageId
                && IsOnlineHostPlayer(data.PlayerId))
            {
                ChallengeSessionMessage session = JsonUtility.FromJson<ChallengeSessionMessage>(data.Json);
                if (session != null
                    && (string.IsNullOrEmpty(session.TargetPlayerId)
                        || session.TargetPlayerId == onlineManager.LocalPlayerId))
                {
                    if (challengeReadyRoom != null)
                        challengeReadyRoom.ApplyHostRunState(session.RunStarted);
                    else if (session.RunStarted)
                        challengeRunStarted = true;
                }
            }
            else if (data.Kind == GimmickKindPlayerAbilityEffect
                && data.ObjectId == currentStageId)
            {
                PlayerAbilityEffectMessage effect = JsonUtility.FromJson<PlayerAbilityEffectMessage>(data.Json);
                string sourcePlayerId = effect != null
                    && OnlinePlayerIdentity.IsOwnedByConnection(effect.SourcePlayerId, data.PlayerId)
                        ? effect.SourcePlayerId
                        : data.PlayerId;
                PlayerController2D source = OnlinePlayerIdentity.IsOwnedByConnection(
                        sourcePlayerId, onlineManager?.LocalPlayerId)
                    ? null
                    : GetOnlinePlayerController(sourcePlayerId);
                if (effect != null && source != null)
                {
                    if (effect.Effect == "cat_scratch")
                        source.GetComponent<PlayerCarryController>()?.PlayRemoteCatScratchEffect();
                    else if (effect.Effect == "slime_spike")
                        source.GetComponent<PlayerCarryController>()?.PlayRemoteSlimeSpikeEffect(effect.Direction);
                }
            }
            else if (data.Kind == GimmickKindClearRequest
                && data.ObjectId == currentStageId
                && stageStarted
                && !cleared
                && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                ClearStage();
            }
            else if (data.Kind == GimmickKindClear
                && data.ObjectId == currentStageId
                && IsOnlineHostPlayer(data.PlayerId))
            {
                // Persist the participant's own local clear record as soon as the
                // authoritative host notification arrives. This is intentionally
                // shared with single-player progress.
                RecordStageClearProgress(data.ObjectId);
                ApplyClearStage();
            }
            else if (data.Kind == GimmickKindGoalState && data.ObjectId == currentStageId)
            {
                PlayerGoalState goalState = JsonUtility.FromJson<PlayerGoalState>(data.Json);
                bool inside = goalState != null && goalState.Inside;
                string gamePlayerId = goalState != null
                    && OnlinePlayerIdentity.IsOwnedByConnection(goalState.GamePlayerId, data.PlayerId)
                        ? goalState.GamePlayerId
                        : data.PlayerId;
                if (inside && !CanOnlinePlayerEnterGoal(gamePlayerId)) return;
                SetOnlineGoalState(gamePlayerId, inside);
                if (currentStageId == "11-1" && inside
                    && IsLocalOnlineHost(onlineManager.CurrentLobby))
                {
                    ClearStage();
                }
                else TryClearWhenAllOnlinePlayersAtGoal();
            }
            else if (data.Kind == GimmickKindRetry
                && data.ObjectId == currentStageId
                && IsOnlineHostPlayer(data.PlayerId))
            {
                // A retry is sent immediately and is also repeated through the
                // session snapshot. Treat both as the same revision so a client
                // can never reload the stage twice for one host retry.
                int retryRevision = data.RetryRevision + 1;
                if (onlineManager?.CurrentLobby != null)
                {
                    retryRevision = Mathf.Max(retryRevision,
                        onlineManager.CurrentLobby.RetryRevision);
                }
                if (retryRevision <= lastAppliedOnlineRetryRevision) return;
                lastAppliedOnlineRetryRevision = retryRevision;
                ApplyFullStageRetry();
            }
            else if (data.Kind == GimmickKindCollectRequest
                && data.ObjectId != null
                && stageStarted
                && !cleared
                && stageRuleMode == StageRuleMode.TimedCollection
                && IsLocalOnlineHost(onlineManager.CurrentLobby)
                && CanOnlinePlayerCollect(data.PlayerId, data.ObjectId))
            {
                ApplyCollectible(data.ObjectId, true, data.PlayerId);
            }
            else if (data.Kind == GimmickKindCollectState && IsOnlineHostPlayer(data.PlayerId))
            {
                CollectionState state = JsonUtility.FromJson<CollectionState>(data.Json);
                if (state != null)
                {
                    ApplyCollectibleState(state);
                }
            }
            else if (data.Kind == GimmickKindCollectSnapshot && IsOnlineHostPlayer(data.PlayerId))
            {
                ApplyCollectionSnapshot(JsonUtility.FromJson<CollectionSnapshotState>(data.Json));
            }
            else if (data.Kind == GimmickKindChallengeFailed && IsOnlineHostPlayer(data.PlayerId))
            {
                ApplyChallengeFailed();
            }
            else if (data.Kind == GimmickKindSessionEnded && IsOnlineHostPlayer(data.PlayerId))
            {
                endingOnlineLobbyId = onlineManager?.CurrentLobby?.LobbyId ?? onlineSessionLobbyId;
                onlineSessionEnding = true;
                string notice = LocalizationManager.T("online_host_disconnected");
                onlineManager.LeaveLobby();
                uiManager?.HideLeaveSessionConfirm();
                uiManager?.HideGameplayNotice();
                EnterTitle();
                uiManager?.ShowGameplayNotice(notice, 4f);
            }
            else if (data.Kind == GimmickKindSlimeBoxBreakRequest
                && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                SlimeBoxBreakMessage request = JsonUtility.FromJson<SlimeBoxBreakMessage>(data.Json);
                if (request != null && request.StageId == currentStageId
                    && CanOnlinePlayerBreakSlimeBox(data.PlayerId, request.ObjectId)
                    && ApplySlimeBoxBreak(request.ObjectId, request.HitPoint))
                {
                    BroadcastSlimeBoxBreak(request.ObjectId, request.HitPoint);
                }
            }
            else if (data.Kind == GimmickKindSlimeBoxBreakState
                && IsOnlineHostPlayer(data.PlayerId))
            {
                SlimeBoxBreakMessage state = JsonUtility.FromJson<SlimeBoxBreakMessage>(data.Json);
                if (state != null && state.StageId == currentStageId)
                    ApplySlimeBoxBreak(state.ObjectId, state.HitPoint);
            }
            else if (data.Kind == GimmickKindSpeciesSwapRequest && data.ObjectId == currentStageId)
            {
                SpeciesSwapMessage request = JsonUtility.FromJson<SpeciesSwapMessage>(data.Json);
                if (request != null
                    && request.RequesterId == data.PlayerId
                    && OnlinePlayerIdentity.IsOwnedByConnection(
                        request.TargetId, onlineManager.LocalPlayerId))
                {
                    ShowIncomingSpeciesSwap(request);
                }
            }
            else if (data.Kind == GimmickKindSpeciesSwapResponse && data.ObjectId == currentStageId)
            {
                SpeciesSwapMessage response = JsonUtility.FromJson<SpeciesSwapMessage>(data.Json);
                if (response == null)
                {
                    return;
                }

                if (response.TargetId != data.PlayerId && !IsOnlineHostPlayer(data.PlayerId))
                {
                    return;
                }

                if (!response.Accepted && OnlinePlayerIdentity.IsOwnedByConnection(
                    response.RequesterId, onlineManager.LocalPlayerId))
                {
                    PlayerController2D rejectedRequester = GetSwapPlayer(response.RequesterId);
                    pendingOutgoingSpeciesSwap = null;
                    pendingOutgoingSpeciesSwapExpiresAt = 0f;
                    pendingSpeciesSwapDrawingState = null;
                    uiManager?.HideSpeciesSwapConfirm();
                    if (GetComponent<LocalCoopSessionManager>()?.ShowSpeciesSwapFailure(rejectedRequester) != true)
                        drawManager?.ShowSpeciesSwapResult(false);
                }
                else if (response.Accepted && IsLocalOnlineHost(onlineManager.CurrentLobby))
                {
                    TryApplyAndBroadcastSpeciesSwap(response);
                }
            }
            else if (data.Kind == GimmickKindSpeciesSwapApply
                && data.ObjectId == currentStageId
                && IsOnlineHostPlayer(data.PlayerId))
            {
                SpeciesSwapMessage applied = JsonUtility.FromJson<SpeciesSwapMessage>(data.Json);
                ApplySpeciesSwap(applied);
            }
        }

        private void RecordStageClearProgress(string stageId)
        {
            if (!testingEditedStage)
            {
                StageProgressStore.MarkCleared(stageId);
            }
        }

        public void Retry()
        {
            if (!stageStarted)
            {
                return;
            }

            if (IsOnlineInStage())
            {
                if (!IsLocalOnlineHost(onlineManager.CurrentLobby))
                {
                    return;
                }

                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindRetry,
                    Json = "{}"
                });
                ApplyFullStageRetry();
                return;
            }

            ApplyFullStageRetry();
        }

        internal void ScheduleFullStageRetry()
        {
            if (fullStageRetryScheduled) return;
            fullStageRetryScheduled = true;
            StartCoroutine(ApplyFullStageRetryNextFrame());
        }

        private IEnumerator ApplyFullStageRetryNextFrame()
        {
            yield return null;
            fullStageRetryScheduled = false;
            Retry();
        }

        private void ApplyFullStageRetry()
        {
            string stageToReload = currentStageId;
            cleared = false;
            ResetGoalProgress();
            CancelDrawingMode();
            PreparePlayersForFullStageRetry();
            if (!string.IsNullOrEmpty(stageToReload) && stageToReload != "title")
            {
                SelectStage(stageToReload);
            }
            else
            {
                RespawnPlayers();
                uiManager?.SetCleared(false);
            }
        }

        public void RetryCurrentStageAfterElimination()
        {
            if (!stageStarted || cleared || string.IsNullOrEmpty(currentStageId)) return;
            if (IsOnlineInStage() && !IsLocalOnlineHost(onlineManager.CurrentLobby)) return;
            RestartAfterChallengeTimeUp();
        }

        private void PreparePlayersForFullStageRetry()
        {
            SetAllPlayerControls(false);
            ResetAllCarryState();
            PlayerController2D[] allPlayers = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allPlayers.Length; i++)
            {
                PlayerController2D current = allPlayers[i];
                if (current == null) continue;
                current.GetComponent<PlayerCarryController>()?.ForceDrop();
                current.RecoverAfterCarryRelease();
                current.GetComponent<BodyBuilder>()?.RestoreAfterRespawnOrRetry();
                current.ResetMotion();
                current.SetControlsEnabled(false);
            }
        }

        public void GoToNextStage()
        {
            if (IsOnlineInStage() && !IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                return;
            }

            string nextStageId = GetNextStageId(currentStageId);
            if (string.IsNullOrEmpty(nextStageId))
            {
                if (IsOnlineInStage()) OpenStageSelectFromMultiLobby();
                else OpenStageSelect();
                return;
            }

            // Stage-select starts already notify the lobby through SelectStage's
            // stageSelectReturnToMultiLobby path. Clear-result progression does
            // not use that path, so explicitly publish the next stage before the
            // host loads it or clients remain on the cleared stage.
            if (IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                onlineManager.StartGame(nextStageId);
            }
            SelectStage(nextStageId);
        }

        public bool CanEnterDrawingMode()
        {
            // TITLE's DRAW button intentionally uses the same full-screen editor,
            // but it is not an in-stage redraw. The per-player co-op guard below
            // rejects titleMode, so keep the title entry path separate.
            if (titleMode)
            {
                return player != null && stageStarted && !stageEditing && !cleared
                    && !optionOverlayActive && !titleTextInputActive && !challengeFailed;
            }

            return CanPlayerEnterDrawingMode(player);
        }

        public bool CanPlayerEnterDrawingMode(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || !stageStarted || titleMode || stageEditing
                || cleared || optionOverlayActive || gameplayMenuOverlayActive
                || titleTextInputActive || challengeFailed
                || (challengeStarting && !IsChallengeReadyRoomActive && !spawnCorrectionRequired))
            {
                return false;
            }

            if (StageRedrawZone.HasActiveZones())
            {
                if (!StageRedrawZone.IsPlayerInZone(targetPlayer)) return false;
            }
            else if (RequiresChallengeReadyRoom() && challengeRunStarted && currentStageId != "10-3")
            {
                if (!StageRedrawZone.IsPlayerInZone(targetPlayer)) return false;
            }

            return true;
        }

        public void BeginIndependentLocalRedraw(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || independentRedrawStates.ContainsKey(targetPlayer)) return;
            ReleasePlayerFromAllCarriers(targetPlayer);
            IndependentRedrawState state = new IndependentRedrawState
            {
                ReturnPosition = targetPlayer.transform.position,
                HasReturnBottom = targetPlayer.IsGrounded
            };
            if (state.HasReturnBottom && TryGetPlayerSolidBounds(targetPlayer, out Bounds bounds))
                state.ReturnBottomY = bounds.min.y;
            else
                state.HasReturnBottom = false;
            independentRedrawStates[targetPlayer] = state;
            targetPlayer.ResetMotion();
            SetPlayerRedrawingState(targetPlayer, true);
        }

        public void CancelIndependentLocalRedraw(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null
                || !independentRedrawStates.TryGetValue(targetPlayer, out IndependentRedrawState state)) return;
            if (state.ValidationRoutine != null) StopCoroutine(state.ValidationRoutine);
            RestoreIndependentRedrawPlayer(targetPlayer, state, state.ReturnPosition, true);
            independentRedrawStates.Remove(targetPlayer);
        }

        public void RequestIndependentLocalRedrawCommit(
            PlayerController2D targetPlayer,
            DrawManager session)
        {
            if (targetPlayer == null || session == null
                || !independentRedrawStates.TryGetValue(targetPlayer, out IndependentRedrawState state)
                || state.ValidationRoutine != null) return;
            state.ValidationRoutine = StartCoroutine(
                ValidateIndependentLocalRedrawAndComplete(targetPlayer, session, state));
        }

        private IEnumerator ValidateIndependentLocalRedrawAndComplete(
            PlayerController2D targetPlayer,
            DrawManager session,
            IndependentRedrawState state)
        {
            yield return null;
            state.ValidationRoutine = null;
            if (targetPlayer == null || session == null || !independentRedrawStates.ContainsKey(targetPlayer))
                yield break;

            // Several local players can press Confirm in the same frame. Re-run
            // species, connection and shared-ink validation at the serialized
            // commit point so two temporary bodies cannot both pass against the
            // same stale confirmed team total.
            if (!session.TryApplyDrawing(true))
            {
                TeleportPlayerWithoutPhysics(targetPlayer, state.ReturnPosition);
                targetPlayer.ResetMotion();
                SetPlayerRedrawingState(targetPlayer, true);
                yield break;
            }

            SetPlayerRedrawingState(targetPlayer, false);
            Physics2D.SyncTransforms();
            bool fits;
            Vector3 safePosition;
            if (IsChallengeReadyRoomActive && challengeReadyRoom != null)
            {
                fits = challengeReadyRoom.TryGetFittedReturnPosition(targetPlayer, out safePosition);
            }
            else
            {
                fits = TryPlaceAtSafeSpawn(targetPlayer, state.ReturnPosition, out safePosition);
            }

            if (!fits)
            {
                TeleportPlayerWithoutPhysics(targetPlayer, state.ReturnPosition);
                targetPlayer.ResetMotion();
                SetPlayerRedrawingState(targetPlayer, true);
                if (IsChallengeReadyRoomActive) session.ShowReadyRoomFitError();
                else session.ShowStageFitError();
                yield break;
            }

            assignedPlayerStartPositions[targetPlayer] = safePosition;
            session.CommitEditing();
            RecordPlayerDrawingState(targetPlayer, session.CreateState());
            session.SendCurrentBodyData();
            session.SetActive(false);
            independentRedrawStates.Remove(targetPlayer);
            RestoreIndependentRedrawPlayer(targetPlayer, state, safePosition, false);
            GameSfx.Play(SfxId.DrawConfirm);
            string savedMessage = LocalizationManager.T("draw_save_complete");
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (localCoop == null || !localCoop.ShowLocalPlayerNotice(targetPlayer, savedMessage))
                uiManager?.ShowGameplayNotice(savedMessage);
        }

        private void RestoreIndependentRedrawPlayer(
            PlayerController2D targetPlayer,
            IndependentRedrawState state,
            Vector3 position,
            bool restoreOriginalBottom)
        {
            if (targetPlayer == null) return;
            TeleportPlayerWithoutPhysics(targetPlayer, position);
            targetPlayer.ResetMotion();
            SetPlayerRedrawingState(targetPlayer, false);
            Physics2D.SyncTransforms();
            if (restoreOriginalBottom && state != null && state.HasReturnBottom
                && TryGetPlayerSolidBounds(targetPlayer, out Bounds rebuiltBounds))
            {
                Vector3 corrected = targetPlayer.transform.position;
                corrected.y += state.ReturnBottomY - rebuiltBounds.min.y;
                TeleportPlayerWithoutPhysics(targetPlayer, corrected);
                Physics2D.SyncTransforms();
            }
            targetPlayer.SetControlsEnabled(ShouldEnableLocalGameplayControls);
        }

        public void EnterDrawingMode()
        {
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            // Characters added from the single-player drawer share P1 input and
            // use the normal full-screen DRAW flow. Only split-screen players
            // with separately assigned devices own independent DRAW sessions.
            if (localCoop != null && localCoop.HasDedicatedSecondaryPlayers)
            {
                localCoop.ToggleDrawing(player);
                return;
            }

            if (!CanEnterDrawingMode())
            {
                return;
            }

            // Reassert the current stage restriction immediately before opening
            // DRAW. Title/menu transitions may have restored the all-species
            // default while this panel was inactive.
            drawManager?.SetAllowedSpecies(StageSpeciesRules.GetAllowedForStage(currentStageId));
            drawing = true;
            drawingOpenedForSpawnCorrection = spawnCorrectionRequired;
            if (player != null)
            {
                ReleasePlayerFromAllCarriers(player);
                redrawReturnPlayer = player;
                redrawReturnPosition = player.transform.position;
                hasRedrawReturnPosition = true;
                Bounds redrawBounds = default;
                hasRedrawReturnBottom = player.IsGrounded;
                if (hasRedrawReturnBottom)
                {
                    hasRedrawReturnBottom = TryGetPlayerSolidBounds(player, out redrawBounds);
                }
                if (hasRedrawReturnBottom)
                {
                    redrawReturnBottomY = redrawBounds.min.y;
                }
                player.ResetMotion();
                SetPlayerRedrawingState(player, true);
            }

            // An online peer cannot pause the authoritative stage for everyone.
            // Keep 11-2 and other online stages running behind the DRAW overlay.
            Time.timeScale = titleMode || IsOnlineInStage() ? 1f : 0f;
            player?.SetControlsEnabled(false);
            uiManager?.SetDrawing(true);
            drawManager?.SetActive(true);
        }

        public void ExitDrawingMode()
        {
            ExitDrawingMode(false);
        }

        public void ConfirmDrawingMode(bool showSavedNotice = false)
        {
            if (!drawing)
            {
                if (showSavedNotice) drawManager?.ShowDrawingSaveError();
                return;
            }
            if (IsChallengeReadyRoomActive)
            {
                if (spawnFitValidationRoutine == null)
                    spawnFitValidationRoutine = StartCoroutine(ValidateReadyRoomRedrawFitAndComplete(showSavedNotice));
                return;
            }
            if ((!RequiresChallengeReadyRoom() || currentStageId == "10-3" && challengeRunStarted) && stageStarted)
            {
                if (spawnFitValidationRoutine == null)
                    spawnFitValidationRoutine = StartCoroutine(ValidateRedrawFitAndComplete(showSavedNotice));
                return;
            }
            drawManager?.CommitEditing();
            ExitDrawingMode(true);
            if (showSavedNotice)
                uiManager?.ShowGameplayNotice(LocalizationManager.T("draw_save_complete"));
        }

        private void ExitDrawingMode(bool returnToStart)
        {
            if (!drawing)
            {
                return;
            }

            drawing = false;
            drawingOpenedForSpawnCorrection = false;
            RestoreRedrawPose(returnToStart);
            Time.timeScale = 1f;
            uiManager?.SetDrawing(false);
            drawManager?.SetActive(false);
            pendingSpeciesSwapDrawingState = drawManager != null
                ? drawManager.CreateState()
                : null;
            // Publish only after the stage-side fit check has accepted the body.
            // Rejected temporary geometry must never replace a peer's valid body.
            SendLocalOnlineBodyData();
            // A same-species redraw is not detected by OnlinePlayerSync's species
            // watcher. Repeat the finalized body after DRAW has fully closed so
            // peers that were temporarily rebuilding/carrying still receive it.
            FindFirstObjectByType<OnlinePlayerSync>()?.RequestLocalBodyResync(3);
            RefreshOnlinePlayerCollisionSafety();
            player?.SetControlsEnabled(!cleared && !optionOverlayActive);
        }

        public void CancelDrawingMode()
        {
            if (!drawing)
            {
                GetComponent<LocalCoopSessionManager>()?.CancelAllDrawingSessions();
                return;
            }

            if (spawnCorrectionRequired && drawingOpenedForSpawnCorrection)
            {
                drawManager?.ShowStageFitError();
                return;
            }

            // A voluntary redraw may fail the fit check after applying its temporary
            // body. Back cancels that edit and restores the known-good snapshot.
            spawnCorrectionRequired = false;
            drawManager?.CancelEditing();
            ExitDrawingMode();
        }

        public void RequestCancelDrawingMode()
        {
            if (!drawing)
            {
                return;
            }

            // Spawn correction is mandatory, so Back cannot discard and leave this
            // screen. Keep the existing fit warning instead of offering an action
            // that CancelDrawingMode would reject after confirmation.
            if (spawnCorrectionRequired && drawingOpenedForSpawnCorrection)
            {
                CancelDrawingMode();
                return;
            }

            if (uiManager != null)
            {
                uiManager.ShowGameplayActionConfirm(GameplayButtonCommand.Command.CloseDrawing);
            }
            else
            {
                CancelDrawingMode();
            }
        }

        public void SelectStage(string stageId)
        {
            ClearTransientStageVisuals();
            CancelRespawnAnimations();
            RestoreOnlineEliminationColliders();
            if (spawnFitValidationRoutine != null) StopCoroutine(spawnFitValidationRoutine);
            spawnFitValidationRoutine = null;
            spawnCorrectionRequired = false;
            if (challengeReadyRoom != null) challengeReadyRoom.Abort();
            challengeReadyRoom = null;
            challengeRunStarted = false;
            assignedPlayerStartPositions.Clear();
            slimeBrokenObjectIds.Clear();
            nextSlimeBreakSyncAt = Time.unscaledTime + 1.5f;
            ResetSpeciesSwapState();
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(true);
            string nextStageId = string.IsNullOrEmpty(stageId) ? "1-0" : stageId;
            nextStageId = DemoAccessPolicy.NormalizeStageId(nextStageId);
            bool enteringDifferentStage = currentStageId != nextStageId;
            currentStageId = nextStageId;
            GameBgm.PlayForStage(currentStageId);
            ApplySpeciesRulesForCurrentStage();
            ResetGoalProgress();
            if (stageSelectEditMode)
            {
                OpenStageEditor(currentStageId);
                return;
            }

            bool notifyOnline = stageSelectReturnToMultiLobby
                && !stageSelectRemoteWaiting
                && onlineManager != null
                && onlineManager.CurrentLobby != null
                && onlineManager.State != OnlineConnectionState.Offline
                && IsLocalOnlineHost(onlineManager.CurrentLobby);
            stageSelectReturnToMultiLobby = false;
            stageSelectRemoteWaiting = false;
            if (notifyOnline)
            {
                onlineManager.StartGame(currentStageId);
            }

            titleMode = false;
            if (stageLoader != null)
            {
                if (currentStageId == "1-0")
                {
                    stageLoader.ShowFallbackStage();
                }
                else
                {
                    stageLoader.LoadStage(currentStageId);
                }
            }

            // Controller-owned arenas must replace their authored placeholder
            // before the common respawn, otherwise the first spawn can use the
            // obsolete JSON marker outside the runtime frame.
            Object.FindFirstObjectByType<StageSlimeCrateChallengeController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<StageAquariumSealController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<Stage71PoseRoomsController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<StageTiltBoardController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<StageLaserRelayController>()
                ?.EnsureInitializedForPlay();

            // A controller removed by StageLoader can finish its OnDisable in
            // the same frame and leave a last transient flash behind. Sweep once
            // more after the old controller and children have been torn down.
            ClearTransientStageVisuals();
            StartCoroutine(ClearTransientStageVisualsAfterTransition());

            ConfigureStageRule(stageLoader != null ? stageLoader.CurrentStageData : null);
            survivalController = UsesEliminationController
                ? Object.FindFirstObjectByType<StageEliminationChallengeController>()
                : null;
            blockBreakerController = IsBlockBreakerChallenge
                ? Object.FindFirstObjectByType<StageBlockBreakerController>()
                : null;

            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            RestoreLocalPlayerPhysicsForStageEntry();
            SetCameraFollowEnabled(true);
            uiManager?.SetTitle(false);
            uiManager?.SetMulti(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageSelectLocked(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            if (enteringDifferentStage && RequiresUniquePlayerSpecies)
            {
                AssignInitialUniqueSpecies();
            }
            RespawnPlayers();
            // Flying/final boss stages must mount or place players and lock their
            // arena camera after the common respawn and camera restoration above.
            Object.FindFirstObjectByType<StageFlyingPlatformBossController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<StageMirrorFinalBossController>()
                ?.EnsureInitializedForPlay();
            Object.FindFirstObjectByType<StageCosmicMimicBossController>()
                ?.EnsureInitializedForPlay();
            SetActivePlayer(IsOnlineInStage() && primaryPlayer != null
                ? primaryPlayer
                : player != null ? player : primaryPlayer, true);
            BeginChallengeReadyRoomIfNeeded();
            BeginNormalStageSpawnValidationIfNeeded();
            if (RequiresUniquePlayerSpecies)
            {
                SendLocalOnlineBodyData();
            }
        }

        public void OpenStageEditor(string stageId)
        {
            if (DemoAccessPolicy.IsDemoBuild) return;
            CancelRespawnAnimations();
            if (challengeReadyRoom != null) challengeReadyRoom.Abort();
            challengeReadyRoom = null;
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(true);
            currentStageId = string.IsNullOrEmpty(stageId) ? "1-1" : stageId;
            GameBgm.PlayForStage(currentStageId);
            ApplySpeciesRulesForCurrentStage();
            CancelDrawingMode();
            stageStarted = false;
            stageEditing = true;
            titleMode = false;
            cleared = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(false);
            for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            SetAllPlayerControls(false);
            stageLoader?.HideStages();
            uiManager?.HideMenu();
            uiManager?.SetStageSelect(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageEditor(true);
            uiManager?.SetChallengeHud(false, 0f, collectionTarget, 0, 1, false);
            uiManager?.SetChallengeCountdown(false, string.Empty);
            stageEditor?.Open(currentStageId);
        }

        public void CloseStageEditor()
        {
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(false);
            stageEditor?.Close();
            stageEditing = false;
            stageStarted = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(true);
            for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            SetAllPlayerControls(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(true);
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        public bool StageSelectEditMode => !DemoAccessPolicy.IsDemoBuild && stageSelectEditMode;

        public void SetStageSelectEditMode(bool editing)
        {
            stageSelectEditMode = !DemoAccessPolicy.IsDemoBuild && editing;
        }

        public bool ToggleStageSelectEditMode()
        {
            if (DemoAccessPolicy.IsDemoBuild)
            {
                stageSelectEditMode = false;
                return false;
            }
            stageSelectEditMode = !stageSelectEditMode;
            return stageSelectEditMode;
        }

        public void TestEditedStage()
        {
            if (stageEditor == null)
            {
                return;
            }

            bool[] hasDebugStarts = new bool[localPlayers.Count];
            Vector3[] debugStartPositions = new Vector3[localPlayers.Count];
            for (int i = 0; i < localPlayers.Count; i++)
            {
                hasDebugStarts[i] = stageEditor.TryGetDebugTestStartPosition(
                    i, out debugStartPositions[i]);
            }
            SetEditedStageTestMode(true);
            NotebookBackgroundDoodles.SetWorldVisible(true);
            stageEditor.TestPlay();
            Object.FindFirstObjectByType<StageSlimeCrateChallengeController>()
                ?.EnsureInitializedForPlay();
            ConfigureStageRule(stageLoader != null ? stageLoader.CurrentStageData : null);
            survivalController = UsesEliminationController
                ? Object.FindFirstObjectByType<StageEliminationChallengeController>()
                : null;
            blockBreakerController = IsBlockBreakerChallenge
                ? Object.FindFirstObjectByType<StageBlockBreakerController>()
                : null;
            stageEditing = false;
            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            RestoreLocalPlayerPhysicsForStageEntry();
            SetCameraFollowEnabled(true);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            if (RequiresUniquePlayerSpecies)
            {
                AssignInitialUniqueSpecies();
            }
            RespawnPlayers();
            for (int i = 0; i < localPlayers.Count && i < hasDebugStarts.Length; i++)
            {
                PlayerController2D localPlayer = localPlayers[i];
                if (!hasDebugStarts[i] || localPlayer == null) continue;
                TeleportPlayerWithoutPhysics(localPlayer, debugStartPositions[i]);
                localPlayer.ResetMotion();
            }
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void ReturnToStageEditor()
        {
            if (!testingEditedStage || stageEditor == null)
            {
                return;
            }

            CancelDrawingMode();
            NotebookBackgroundDoodles.SetWorldVisible(true);
            stageStarted = false;
            stageEditing = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(false);
            for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            SetAllPlayerControls(false);
            stageLoader?.HideStages();
            uiManager?.HideMenu();
            uiManager?.SetCleared(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageEditor(true);
            uiManager?.SetChallengeCountdown(false, string.Empty);
            stageEditor.ResumeAfterTestPlay();
            SetEditedStageTestMode(false);
        }

        private void SetEditedStageTestMode(bool testing)
        {
            testingEditedStage = testing;
            uiManager?.SetEditorTestMode(testing);
        }

        public void OpenStageSelect()
        {
            ClearTransientStageVisuals();
            StartCoroutine(ClearTransientStageVisualsAfterTransition());
            CancelRespawnAnimations();
            if (challengeReadyRoom != null) challengeReadyRoom.Abort();
            challengeReadyRoom = null;
            GameBgm.PlayTitle();
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(false);
            stageSelectReturnToMultiLobby = false;
            stageSelectRemoteWaiting = false;
            CancelDrawingMode();
            stageEditor?.Close();
            stageEditing = false;
            stageSelectEditMode = false;
            stageStarted = false;
            titleMode = false;
            cleared = false;
            Time.timeScale = 0f;
            SetCameraFollowEnabled(true);
            for (int i = 0; i < localPlayers.Count; i++) localPlayers[i]?.ResetMotion();
            SetAllPlayerControls(false);
            uiManager?.HideMenu();
            uiManager?.SetTitle(false);
            uiManager?.SetMulti(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageSelectLocked(false);
            uiManager?.SetStageSelect(true);
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        public void OpenStageSelectFromMultiLobby()
        {
            OpenStageSelect();
            stageSelectReturnToMultiLobby = true;
            stageSelectRemoteWaiting = false;
            uiManager?.SetStageSelectLocked(false);
            onlineManager?.OpenStageSelect();
        }

        public void OpenStageSelectWaitingForHost()
        {
            OpenStageSelect();
            stageSelectReturnToMultiLobby = true;
            stageSelectRemoteWaiting = true;
            uiManager?.SetStageSelectLocked(true);
        }

        public void CloseStageSelectWaitingForHost()
        {
            if (!stageSelectRemoteWaiting)
            {
                return;
            }

            stageSelectReturnToMultiLobby = false;
            stageSelectRemoteWaiting = false;
            titleMode = true;
            stageStarted = true;
            stageEditing = false;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetStageSelectLocked(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetTitle(false);
            uiManager?.SetMulti(true);
            ShowOnlineLobbyScreen();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void BackFromStageSelect()
        {
            if (stageSelectRemoteWaiting)
            {
                return;
            }

            if (!stageSelectReturnToMultiLobby)
            {
                EnterTitle();
                return;
            }

            stageSelectReturnToMultiLobby = false;
            stageSelectRemoteWaiting = false;
            onlineManager?.CloseStageSelect();
            titleMode = true;
            stageStarted = true;
            stageEditing = false;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageSelectLocked(false);
            uiManager?.SetTitle(false);
            uiManager?.SetMulti(true);
            ShowOnlineLobbyScreen();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        private static void ShowOnlineLobbyScreen()
        {
            // Keep the active room visible after closing stage selection. The
            // multi panel's OnEnable timing must not send this flow back to the
            // room creation screen while the online session is still alive.
            MultiMenuController multiMenu = Object.FindFirstObjectByType<MultiMenuController>();
            multiMenu?.ShowLobby();
        }

        private bool IsLocalOnlineHost(OnlineLobbyInfo lobby)
        {
            if (onlineManager == null || lobby == null || lobby.Players == null)
            {
                return false;
            }

            string localPlayerId = onlineManager.LocalPlayerId;
            for (int i = 0; i < lobby.Players.Length; i++)
            {
                OnlinePlayerInfo playerInfo = lobby.Players[i];
                if (playerInfo != null && playerInfo.IsHost && playerInfo.PlayerId == localPlayerId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOnlineHostPlayer(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null || string.IsNullOrEmpty(playerId)) return false;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId)
                    return true;
            }
            return false;
        }

        private bool IsOnlineInStage()
        {
            return onlineManager != null
                && onlineManager.CurrentLobby != null
                && onlineManager.State == OnlineConnectionState.Playing;
        }

        private static string GetNextStageId(string stageId)
        {
            if (DemoAccessPolicy.IsDemoBuild)
            {
                return DemoAccessPolicy.GetNextStageId(stageId);
            }

            if (string.IsNullOrEmpty(stageId) || stageId == "title")
            {
                return "1-1";
            }

            if (stageId == "1-0")
            {
                return "1-1";
            }

            string[] parts = stageId.Split('-');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out int world)
                || !int.TryParse(parts[1], out int stage))
            {
                return "1-1";
            }

            if (stage < 3)
            {
                return $"{world}-{stage + 1}";
            }

            if (world < 15)
            {
                return $"{world + 1}-1";
            }

            return null;
        }

        public Transform ActivePlayerTransform => player != null ? player.transform : null;
        public PlayerController2D ActivePlayerController => player;
        public IReadOnlyList<PlayerController2D> LocalPlayers => localPlayers;
        public bool IsLocallyControlledPlayer(PlayerController2D candidate)
        {
            return candidate != null && localPlayers.Contains(candidate);
        }
        public int GetLocalPlayerIndex(PlayerController2D targetPlayer)
        {
            int index = localPlayers.IndexOf(targetPlayer);
            return index >= 0 ? index : 0;
        }
        public void ShowLocalCoopNotice(string message)
        {
            if (!string.IsNullOrEmpty(message)) uiManager?.ShowGameplayNotice(message);
        }
        public Transform RemotePlayerTransform => RemotePlayerController != null ? RemotePlayerController.transform : null;
        public PlayerController2D RemotePlayerController
        {
            get
            {
                if (IsOnlineInStage())
                    foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                        if (pair.Value != null) return pair.Value;
                return secondaryPlayer;
            }
        }
        public string RemotePlayerId => remotePlayerId;

        public bool ShouldBlockSharedLocalInput(PlayerController2D candidate)
        {
            if (candidate == null || IsOnlineInStage()
                || primaryPlayer == null || localPlayers.Count <= 1)
            {
                return false;
            }

            if (HasIndependentLocalInput(candidate))
            {
                return false;
            }

            bool isLocalSwitchablePlayer = localPlayers.Contains(candidate);
            return isLocalSwitchablePlayer && candidate != player;
        }

        private bool HasIndependentLocalInput(PlayerController2D candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (localCoop == null || !localCoop.HasDedicatedSecondaryPlayers)
            {
                return false;
            }

            LocalPlayerInputBinding binding = candidate.InputBinding;
            return binding != null
                && binding.Kind != LocalPlayerInputBinding.DeviceKind.AutoPrimary;
        }

        public void GetClearCelebrationPlayers(List<PlayerController2D> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < localPlayers.Count; i++) AddClearCelebrationPlayer(results, localPlayers[i]);
            if (IsOnlineInStage())
            {
                foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                {
                    AddClearCelebrationPlayer(results, pair.Value);
                }
                results.Sort((left, right) =>
                    GetClearCelebrationPlayerSlot(left).CompareTo(
                        GetClearCelebrationPlayerSlot(right)));
            }
        }

        public Color GetClearCelebrationPlayerColor(
            PlayerController2D candidate,
            int fallbackIndex)
        {
            int slot = GetClearCelebrationPlayerSlot(candidate);
            if (slot >= 0 && slot < 4)
            {
                return PlayerColorPalette.GetColor(slot);
            }

            BodyBuilder builder = candidate != null ? candidate.GetComponent<BodyBuilder>() : null;
            return builder != null
                ? builder.PlayerColor
                : PlayerColorPalette.GetColor(fallbackIndex);
        }

        private int GetClearCelebrationPlayerSlot(PlayerController2D candidate)
        {
            if (candidate == null || !IsOnlineInStage()) return int.MaxValue;
            string playerId = GetOnlinePlayerId(candidate);
            if (string.IsNullOrEmpty(playerId))
            {
                foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                {
                    if (pair.Value == candidate)
                    {
                        playerId = pair.Key;
                        break;
                    }
                }
            }
            int slot = PlayerColorPalette.GetLobbyPlayerSlot(
                onlineManager?.CurrentLobby,
                playerId);
            return slot >= 0 && slot < 4 ? slot : int.MaxValue;
        }

        private static void AddClearCelebrationPlayer(
            List<PlayerController2D> results,
            PlayerController2D candidate)
        {
            if (candidate != null && !results.Contains(candidate))
            {
                results.Add(candidate);
            }
        }

        public Transform GetOnlinePlayerTransform(string playerId)
        {
            PlayerController2D controller = GetOnlinePlayerController(playerId);
            return controller != null ? controller.transform : null;
        }

        public PlayerController2D GetOnlinePlayerController(string playerId)
        {
            if (onlineManager != null && OnlinePlayerIdentity.IsOwnedByConnection(playerId, onlineManager.LocalPlayerId))
            {
                for (int i = 0; i < localPlayers.Count; i++)
                    if (OnlinePlayerIdentity.Compose(onlineManager.LocalPlayerId, i) == playerId) return localPlayers[i];
            }

            return !string.IsNullOrEmpty(playerId) && onlineRemotePlayers.TryGetValue(playerId, out PlayerController2D remote)
                ? remote
                : null;
        }

        public string GetOnlinePlayerId(PlayerController2D controller)
        {
            if (controller == null)
            {
                return null;
            }

            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                if (pair.Value == controller)
                {
                    return pair.Key;
                }
            }

            int localIndex = localPlayers.IndexOf(controller);
            return localIndex >= 0 && onlineManager != null
                ? OnlinePlayerIdentity.Compose(onlineManager.LocalPlayerId, localIndex)
                : null;
        }

        public bool IsPlayerRespawning(PlayerController2D controller)
        {
            return controller != null && respawnAnimations.ContainsKey(controller);
        }

        public bool IsPlayerEliminated(PlayerController2D controller)
        {
            return controller != null && eliminatedPlayers.Contains(controller);
        }

        internal void ApplyOnlineRemoteEliminationState(string playerId, bool eliminated)
        {
            PlayerController2D remote = GetOnlinePlayerController(playerId);
            if (remote == null || remote == primaryPlayer) return;

            if (eliminated)
            {
                if (!onlineEliminationColliderStates.TryGetValue(
                        remote,
                        out Dictionary<Collider2D, bool> states))
                {
                    states = new Dictionary<Collider2D, bool>();
                    onlineEliminationColliderStates[remote] = states;
                }
                // BodyData can rebuild a remote drawing after elimination. Scan on
                // every owner snapshot so newly generated colliders cannot become
                // invisible walls while the player remains a spectator.
                Collider2D[] colliders = remote.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D collider = colliders[i];
                    if (collider == null) continue;
                    if (!states.ContainsKey(collider)) states[collider] = collider.enabled;
                    collider.enabled = false;
                }
                ReleasePlayerFromAllCarriers(remote);
                remote.GetComponent<PlayerCarryController>()?.ForceDrop();
                remote.SetControlsEnabled(false);
                eliminatedPlayers.Add(remote);
                return;
            }

            if (onlineEliminationColliderStates.TryGetValue(remote, out Dictionary<Collider2D, bool> savedStates))
            {
                foreach (KeyValuePair<Collider2D, bool> state in savedStates)
                    if (state.Key != null) state.Key.enabled = state.Value;
                onlineEliminationColliderStates.Remove(remote);
            }
            eliminatedPlayers.Remove(remote);
        }

        private void RestoreOnlineEliminationColliders()
        {
            foreach (KeyValuePair<PlayerController2D, Dictionary<Collider2D, bool>> playerState
                in onlineEliminationColliderStates)
            {
                foreach (KeyValuePair<Collider2D, bool> colliderState in playerState.Value)
                    if (colliderState.Key != null) colliderState.Key.enabled = colliderState.Value;
            }
            onlineEliminationColliderStates.Clear();
            eliminatedPlayers.Clear();
        }

        internal void ForceCloseDrawingForChallengeLaunch()
        {
            if (!drawing) return;
            if (spawnFitValidationRoutine != null)
            {
                StopCoroutine(spawnFitValidationRoutine);
                spawnFitValidationRoutine = null;
            }
            spawnCorrectionRequired = false;
            drawingOpenedForSpawnCorrection = false;
            drawManager?.CancelEditing();
            drawing = false;
            SetPlayerRedrawingState(player, false);
            Time.timeScale = 1f;
            uiManager?.SetDrawing(false);
            drawManager?.SetActive(false);
        }

        public float GetPlayerRespawnGraceRemaining(PlayerController2D controller)
        {
            if (controller == null || !respawnGraceUntil.TryGetValue(controller, out float until)) return 0f;
            return Mathf.Max(0f, until - Time.unscaledTime);
        }

        public float GetPlayerRespawnRemaining(PlayerController2D controller)
        {
            if (controller == null
                || !respawnAnimations.TryGetValue(controller, out RespawnAnimationState state)) return 0f;
            return Mathf.Max(0f, state.RespawnAt - Time.unscaledTime);
        }

        public void SetOnlineRemotePlayerId(string playerId)
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                remotePlayerId = playerId;
            }
        }

        public Rigidbody2D ActivePlayerBody
        {
            get
            {
                return player != null ? player.GetComponent<Rigidbody2D>() : null;
            }
        }

        public void EnsureOnlineRemotePlayer()
        {
            EnsureOnlineRemotePlayer(remotePlayerId);
        }

        private PlayerController2D EnsureOnlineRemotePlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)
                || onlineManager != null
                    && OnlinePlayerIdentity.IsOwnedByConnection(playerId, onlineManager.LocalPlayerId))
            {
                return null;
            }

            if (onlineRemotePlayers.TryGetValue(playerId, out PlayerController2D existing) && existing != null)
            {
                RestoreOnlineProxyScale(existing);
                return existing;
            }

            GameObject clone = Instantiate(
                primaryPlayer.gameObject,
                primaryPlayer.transform.position + new Vector3((onlineRemotePlayers.Count + 1) * 1.25f, 0.35f, 0f),
                primaryPlayer.transform.rotation,
                primaryPlayer.transform.parent);
            clone.name = "Online Player " + (onlineRemotePlayers.Count + 2);
            PlayerController2D remote = clone.GetComponent<PlayerController2D>();
            if (remote == null)
            {
                Destroy(clone);
                return null;
            }
            remote.ResetMotion();
            remote.SetControlsEnabled(false);

            if (remote == null)
            {
                return null;
            }

            remote.SetControlsEnabled(false);
            RestoreOnlineProxyScale(remote);
            remote.GetComponent<BodyBuilder>()?.SetNetworkBodyReady(false);
            Rigidbody2D remoteBody = remote.GetComponent<Rigidbody2D>();
            if (remoteBody != null)
            {
                // Later remote avatars are cloned from the live local player.
                // A clone created during the 3%-scale respawn collapse otherwise
                // remains a permanent dot because network state does not carry
                // Transform scale or Rigidbody simulation state.
                remoteBody.simulated = true;
                remoteBody.bodyType = RigidbodyType2D.Kinematic;
                remoteBody.gravityScale = 0f;
                remoteBody.freezeRotation = true;
                remoteBody.linearVelocity = Vector2.zero;
                remoteBody.angularVelocity = 0f;
            }
            onlineRemotePlayers[playerId] = remote;
            IgnoreOnlinePlayerCollisions(remote);
            EnsureEliminationSpectatorRelay(remote);
            if (string.IsNullOrEmpty(remotePlayerId))
            {
                SetOnlineRemotePlayerId(playerId);
            }

            int colorIndex = PlayerColorPalette.GetLobbyColorIndex(
                onlineManager != null ? onlineManager.CurrentLobby : null,
                playerId,
                onlineRemotePlayers.Count);
            SetPlayerColor(remote, colorIndex);
            return remote;
        }

        public void SyncOnlinePlayers(OnlineLobbyInfo lobby, string localPlayerId)
        {
            HashSet<string> activeIds = new HashSet<string>();
            if (lobby?.Players != null)
            {
                for (int i = 0; i < lobby.Players.Length; i++)
                {
                    OnlinePlayerInfo info = lobby.Players[i];
                    if (info == null || string.IsNullOrEmpty(info.PlayerId)
                        || OnlinePlayerIdentity.IsOwnedByConnection(info.PlayerId, localPlayerId))
                    {
                        continue;
                    }

                    activeIds.Add(info.PlayerId);
                    EnsureOnlineRemotePlayer(info.PlayerId);
                }
            }

            List<string> removed = new List<string>();
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                if (!activeIds.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            for (int i = 0; i < removed.Count; i++)
            {
                string id = removed[i];
                if (onlineCarryHeld && onlineCarrierPlayerId == id)
                {
                    EndOnlineCarry(Vector2.zero);
                }
                PlayerController2D remote = onlineRemotePlayers[id];
                onlineRemotePlayers.Remove(id);
                if (remote != null)
                {
                    onlineEliminationColliderStates.Remove(remote);
                    eliminatedPlayers.Remove(remote);
                    if (remote == secondaryPlayer)
                    {
                        secondaryPlayer = null;
                    }
                    drawingStates.Remove(remote);
                    Destroy(remote.gameObject);
                }
            }
            drawManager?.RefreshUniqueSpeciesAvailability();
            RefreshControlledPlayerMarkers();
        }

        public void ApplyOnlineRemoteState(string playerId, Vector2 position, Vector2 velocity, float rotation)
        {
            PlayerController2D remote = EnsureOnlineRemotePlayer(playerId);
            if (remote == null)
            {
                return;
            }

            // Remote proxies never use the local death/respawn scale animation.
            // Reassert the authored player scale on every accepted snapshot so
            // an already-corrupted P3/P4 proxy repairs itself without rejoining.
            RestoreOnlineProxyScale(remote);

            Rigidbody2D remoteBody = remote.GetComponent<Rigidbody2D>();
            if (remoteBody != null)
            {
                remoteBody.bodyType = RigidbodyType2D.Kinematic;
                remoteBody.gravityScale = 0f;
                remoteBody.freezeRotation = true;
                remoteBody.position = position;
                remoteBody.linearVelocity = Vector2.zero;
                remoteBody.rotation = rotation;
            }
            else
            {
                remote.transform.position = position;
                remote.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            }

            remote.GetComponent<BodyBuilder>()?.SetRemoteAnimationVelocity(velocity);

            remote.SetControlsEnabled(false);
            // Remote bodies are snapshot-driven kinematic proxies. Letting Box2D
            // solve only one side makes the proxy push the local Dynamic body,
            // while the local body can never push the proxy. Keep physical pairs
            // ignored consistently and resolve horizontal player contact on the
            // authoritative owner of each local player instead.
            ResolveOnlinePlayerHorizontalContact(playerId, remote, velocity);
        }

        private void ResolveOnlinePlayerHorizontalContact(
            string remoteId,
            PlayerController2D remote,
            Vector2 remoteVelocity)
        {
            if (!IsGameplayActive || remote == null || IsOnlineRemotePlayerHeldByLocal(remoteId))
            {
                return;
            }

            for (int i = 0; i < localPlayers.Count; i++)
                ResolveOnlinePlayerHorizontalContactForLocal(localPlayers[i], remote, remoteVelocity);
        }

        private void ResolveOnlinePlayerHorizontalContactForLocal(
            PlayerController2D local,
            PlayerController2D remote,
            Vector2 remoteVelocity)
        {
            if (local == null || local == remote
                || onlineCarryHeld && onlineCarriedLocalPlayer == local
                || IsPlayerEliminated(local) || IsPlayerRespawning(local)) return;

            Rigidbody2D localBody = local.GetComponent<Rigidbody2D>();
            if (localBody == null || !localBody.simulated
                || localBody.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            Physics2D.SyncTransforms();
            if (!TryGetPlayerSolidBounds(local, out Bounds localBounds)
                || !TryGetPlayerSolidBounds(remote, out Bounds remoteBounds))
            {
                return;
            }

            float verticalOverlap = Mathf.Min(localBounds.max.y, remoteBounds.max.y)
                - Mathf.Max(localBounds.min.y, remoteBounds.min.y);
            float minimumVerticalContact = Mathf.Min(localBounds.size.y, remoteBounds.size.y) * 0.16f;
            if (verticalOverlap <= Mathf.Max(0.06f, minimumVerticalContact)) return;

            float horizontalOverlap = Mathf.Min(localBounds.max.x, remoteBounds.max.x)
                - Mathf.Max(localBounds.min.x, remoteBounds.min.x);
            if (horizontalOverlap <= 0f) return;

            float direction;
            float centerDelta = localBounds.center.x - remoteBounds.center.x;
            if (Mathf.Abs(centerDelta) > 0.01f)
                direction = Mathf.Sign(centerDelta);
            else
                direction = local.transform.position.x <= remote.transform.position.x ? -1f : 1f;

            // Both owners resolve roughly half the penetration. This lets a
            // moving player push the other owner through the next snapshots,
            // without a kinematic proxy tunnelling either player through floors.
            float correction = Mathf.Min(horizontalOverlap * 0.52f + 0.008f, 0.22f);
            Vector2 correctedPosition = localBody.position + Vector2.right * direction * correction;
            localBody.position = correctedPosition;
            local.transform.position = correctedPosition;

            Vector2 localVelocity = localBody.linearVelocity;
            float localAwaySpeed = localVelocity.x * direction;
            float incomingRemoteSpeed = remoteVelocity.x * direction;
            if (localAwaySpeed < 0f) localAwaySpeed = 0f;
            if (incomingRemoteSpeed > 0f)
                localAwaySpeed = Mathf.Max(localAwaySpeed, incomingRemoteSpeed * 0.72f);
            localVelocity.x = localAwaySpeed * direction;
            localBody.linearVelocity = localVelocity;
            Physics2D.SyncTransforms();
        }

        public void ApplyOnlineRemoteRedrawing(string playerId, bool redrawing)
        {
            PlayerController2D remote = EnsureOnlineRemotePlayer(playerId);
            if (remote == null)
            {
                return;
            }

            SetPlayerRedrawingState(remote, redrawing);
            if (redrawing)
            {
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerCarryController localCarry = localPlayers[i] != null
                        ? localPlayers[i].GetComponent<PlayerCarryController>()
                        : null;
                    localCarry?.ReleaseIfHolding(remote.transform);
                    localCarry?.ReleaseIfDraggingFriend(remote.transform);
                }
            }
        }

        private static void SetPlayerRedrawingState(PlayerController2D target, bool redrawing)
        {
            if (target == null)
            {
                return;
            }
            PlayerRedrawStateController state = target.GetComponent<PlayerRedrawStateController>();
            if (state == null)
            {
                state = target.gameObject.AddComponent<PlayerRedrawStateController>();
            }
            state.SetRedrawing(redrawing);
        }

        public bool IsOnlineRemotePlayerHeldByLocal(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)
                || !onlineRemotePlayers.TryGetValue(playerId, out PlayerController2D remote)
                || remote == null)
            {
                return false;
            }
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerCarryController carry = localPlayers[i] != null
                    ? localPlayers[i].GetComponent<PlayerCarryController>()
                    : null;
                if (carry != null && (carry.IsHoldingTarget(remote.transform)
                    || carry.IsDraggingFriend(remote.transform))) return true;
            }
            return false;
        }

        public string GetOnlineCarrierPlayerId(PlayerController2D target)
        {
            // onlineCarryHeld describes this machine's local player being moved
            // by another peer. Weapon recoil must be applied to that carrier,
            // because the carried player's Rigidbody is kinematic.
            return target != null && target == onlineCarriedLocalPlayer && onlineCarryHeld
                ? onlineCarrierPlayerId
                : null;
        }

        public string GetOnlineCarriedLocalPlayerId(string carrierPlayerId)
        {
            return onlineCarryHeld && onlineCarrierPlayerId == carrierPlayerId
                ? GetOnlinePlayerId(onlineCarriedLocalPlayer)
                : null;
        }

        public bool IsOnlineBodyRebuildBlocked(string remotePlayerIdToCheck)
        {
            if (string.IsNullOrEmpty(remotePlayerIdToCheck))
            {
                return false;
            }

            return IsOnlineRemotePlayerHeldByLocal(remotePlayerIdToCheck)
                || onlineCarryHeld && onlineCarrierPlayerId == remotePlayerIdToCheck;
        }

        public void ApplyOnlineRemoteState(Vector2 position, Vector2 velocity, float rotation)
        {
            if (string.IsNullOrEmpty(remotePlayerId))
            {
                return;
            }

            ApplyOnlineRemoteState(remotePlayerId, position, velocity, rotation);
        }

        public void ApplyOnlineCarryData(OnlineCarryData carryData, string localPlayerId)
        {
            if (carryData == null || string.IsNullOrEmpty(localPlayerId) || carryData.TargetPlayerId != localPlayerId)
            {
                return;
            }

            PlayerController2D targetLocalPlayer = GetOnlinePlayerController(localPlayerId);
            if (targetLocalPlayer == null || !localPlayers.Contains(targetLocalPlayer)) return;

            bool beginsCarry = carryData.Action == "pickup"
                || carryData.Action == "cat_grab"
                || carryData.Action == "friend_grab";
            if (beginsCarry
                && carryData.CarrierPlayerId == lastOnlineThrowCarrierId
                && (carryData.EventSequence <= 0
                    || carryData.EventSequence > lastOnlineThrowEventSequence))
            {
                // A reliable new pickup is allowed immediately, even while the
                // short post-throw stale-snapshot guard is active.
                staleCarrySnapshotGuardUntil = 0f;
            }
            if (beginsCarry && RejectOrResolveMutualOnlineCarry(carryData.CarrierPlayerId, localPlayerId))
            {
                return;
            }

            if (carryData.Action == "pickup")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, false, Vector2.zero, targetLocalPlayer);
            }
            else if (carryData.Action == "cat_grab")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, true, carryData.LocalOffset, targetLocalPlayer);
            }
            else if (carryData.Action == "friend_grab")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, true, carryData.LocalOffset, targetLocalPlayer);
            }
            else if (carryData.Action == "throw")
            {
                lastOnlineThrowCarrierId = carryData.CarrierPlayerId;
                lastOnlineThrowEventSequence = carryData.EventSequence;
                staleCarrySnapshotGuardUntil = Time.unscaledTime + 1.25f;
                bool wasHeldByOnlineCarrier = onlineCarryHeld;
                if (carryData.EventSequence > 0 && targetLocalPlayer != null)
                {
                    // Start the authoritative owner from the exact release point
                    // used by the thrower's prediction. The carried owner follows
                    // an interpolated remote carrier and can otherwise be a frame
                    // or two behind before the same velocity is applied.
                    targetLocalPlayer.transform.position = carryData.ReleasePosition;
                    if (onlineCarryBody != null)
                    {
                        onlineCarryBody.position = carryData.ReleasePosition;
                    }
                    Physics2D.SyncTransforms();
                }
                EndOnlineCarry(carryData.ReleaseVelocity);
                if (!wasHeldByOnlineCarrier)
                {
                    // The carrier's 30 Hz state can announce an empty hand just
                    // before this reliable throw event arrives. Reconciliation
                    // then releases the local body with carrier velocity, and the
                    // old EndOnlineCarry early-return discarded the real impulse.
                    // The event is sequenced/deduplicated by OnlinePlayerSync, so
                    // it is safe to apply the authoritative release again here.
                    ApplyLateOnlineThrow(targetLocalPlayer, carryData.ReleaseVelocity);
                }
            }
            else if (carryData.Action == "drop")
            {
                EndOnlineCarry(Vector2.zero);
            }
            else if (carryData.Action == "cat_release")
            {
                EndOnlineCarry(carryData.ReleaseVelocity);
            }
            else if (carryData.Action == "friend_release")
            {
                EndOnlineCarry(carryData.ReleaseVelocity);
            }
        }

        private void ApplyLateOnlineThrow(PlayerController2D targetLocalPlayer, Vector2 releaseVelocity)
        {
            if (targetLocalPlayer == null) return;
            Rigidbody2D body = targetLocalPlayer.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = true;
                body.bodyType = RigidbodyType2D.Dynamic;
                body.WakeUp();
                body.rotation = 0f;
                body.linearVelocity = releaseVelocity;
                body.angularVelocity = 0f;
            }
            targetLocalPlayer.RecoverAfterCarryRelease();
            targetLocalPlayer.SetHumanCarryStruggling(false);
            targetLocalPlayer.SetControlsEnabled(stageStarted && !drawing && !cleared
                && !stageEditing && !optionOverlayActive);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PICO CARRY] Applied late online throw speed={releaseVelocity.magnitude:0.0}");
#endif
        }

        public void ReconcileOnlineCarryState(
            string carrierPlayerId,
            string carriedPlayerId,
            string carryAction,
            Vector2 carryOffset,
            int carryEventSequence,
            string localPlayerId,
            Vector2 carrierVelocity)
        {
            if (string.IsNullOrEmpty(carrierPlayerId) || string.IsNullOrEmpty(localPlayerId))
            {
                return;
            }

            bool carrierClaimsLocal = carriedPlayerId == localPlayerId;
            PlayerController2D targetLocalPlayer = GetOnlinePlayerController(localPlayerId);
            if (targetLocalPlayer == null || !localPlayers.Contains(targetLocalPlayer)) return;
            bool attachedCarry = carryAction == "cat_grab" || carryAction == "friend_grab";
            if (carrierClaimsLocal
                && carrierPlayerId == lastOnlineThrowCarrierId
                && Time.unscaledTime < staleCarrySnapshotGuardUntil
                && (carryEventSequence <= 0
                    || carryEventSequence <= lastOnlineThrowEventSequence))
            {
                // Carry events are reliable, while continuous player snapshots
                // use a separate realtime channel. A pre-throw snapshot can
                // therefore arrive after the throw and reattach the participant,
                // changing its Rigidbody back to Kinematic and erasing the launch
                // velocity. Only reject claims at or before the applied throw;
                // a newly sequenced pickup remains valid immediately.
                return;
            }
            if (carrierClaimsLocal
                && RejectOrResolveMutualOnlineCarry(carrierPlayerId, localPlayerId))
            {
                return;
            }
            if (onlineCarryHeld && onlineCarrierPlayerId == carrierPlayerId)
            {
                if (carrierClaimsLocal)
                {
                    if (onlineCarryIsCatGrab != attachedCarry)
                    {
                        // A release followed immediately by another grab from the
                        // same peer can arrive as one continuous state transition.
                        // Rebuild it so a previous Human pickup cannot leave the
                        // participant's controls (and F throw) disabled forever.
                        EndOnlineCarry(Vector2.zero);
                        BeginOnlineCarry(carrierPlayerId, attachedCarry, carryOffset, targetLocalPlayer);
                        return;
                    }
                    onlineCarryLastConfirmedAt = Time.unscaledTime;
                }
                else if (Time.unscaledTime - onlineCarryBeganAt >= 0.3f
                    && Time.unscaledTime - onlineCarryLastConfirmedAt >= 0.18f)
                {
                    // A release event can race with body/state rebuilding. The
                    // carrier's continuous state is the recovery source of truth.
                    EndOnlineCarry(carrierVelocity);
                }
                return;
            }

            if (!carrierClaimsLocal)
            {
                return;
            }

            BeginOnlineCarry(carrierPlayerId, attachedCarry, carryOffset, targetLocalPlayer);
        }

        private bool RejectOrResolveMutualOnlineCarry(string incomingCarrierId, string localPlayerId)
        {
            PlayerController2D targetLocalPlayer = GetOnlinePlayerController(localPlayerId);
            if (targetLocalPlayer == null
                || string.IsNullOrEmpty(incomingCarrierId)
                || !onlineRemotePlayers.TryGetValue(incomingCarrierId, out PlayerController2D incomingCarrier)
                || incomingCarrier == null)
            {
                return false;
            }

            PlayerCarryController localCarry = targetLocalPlayer.GetComponent<PlayerCarryController>();
            if (localCarry == null
                || !localCarry.IsHoldingTarget(incomingCarrier.transform)
                    && !localCarry.IsDraggingFriend(incomingCarrier.transform))
            {
                return false;
            }

            // Both peers make the same decision without waiting for another
            // round trip: the lexicographically smaller player id is the carrier.
            // This collapses simultaneous A->B and B->A requests to one edge.
            bool localCarryWins = string.CompareOrdinal(localPlayerId, incomingCarrierId) < 0;
            if (localCarryWins)
            {
                if (onlineCarryHeld && onlineCarrierPlayerId == incomingCarrierId)
                {
                    EndOnlineCarry(Vector2.zero);
                }
                return true;
            }

            localCarry.ReleaseIfHolding(incomingCarrier.transform);
            localCarry.ReleaseIfDraggingFriend(incomingCarrier.transform);
            return false;
        }

        private void LateUpdate()
        {
            if (onlineCarryHeld)
            {
                FollowOnlineCarrier();
            }
        }

        private void BeginOnlineCarry(
            string carrierPlayerId,
            bool catGrab,
            Vector2 localOffset,
            PlayerController2D targetLocalPlayer)
        {
            PlayerController2D carrier = EnsureOnlineRemotePlayer(carrierPlayerId);
            if (targetLocalPlayer == null || carrier == null)
            {
                return;
            }

            if (onlineCarryHeld)
            {
                if (onlineCarrierPlayerId == carrierPlayerId && onlineCarriedLocalPlayer == targetLocalPlayer)
                {
                    if (onlineCarryIsCatGrab != catGrab)
                    {
                        EndOnlineCarry(Vector2.zero);
                        BeginOnlineCarry(carrierPlayerId, catGrab, localOffset, targetLocalPlayer);
                        return;
                    }
                    onlineCarryLastConfirmedAt = Time.unscaledTime;
                    return;
                }

                // A missed release must not let an old carrier permanently lock
                // the participant out of movement and Human F input.
                EndOnlineCarry(Vector2.zero);
            }

            onlineCarryHeld = true;
            onlineCarriedLocalPlayer = targetLocalPlayer;
            onlineCarrierPlayerId = carrierPlayerId;
            onlineCarryBeganAt = Time.unscaledTime;
            onlineCarryLastConfirmedAt = Time.unscaledTime;
            onlineCarryIsCatGrab = catGrab;
            onlineCarryLocalOffset = localOffset;
            targetLocalPlayer.SetHumanCarryStruggling(!catGrab);
            onlineCarryHasBounds = TryGetPlayerSolidBounds(targetLocalPlayer, out Bounds carryBounds);
            if (onlineCarryHasBounds)
            {
                onlineCarryBoundsCenterOffset = carryBounds.center - targetLocalPlayer.transform.position;
                onlineCarryBoundsExtents = carryBounds.extents;
            }
            targetLocalPlayer.SetControlsEnabled(catGrab && stageStarted && !drawing && !cleared
                && !stageEditing && !optionOverlayActive);
            onlineCarryBody = targetLocalPlayer.GetComponent<Rigidbody2D>();
            if (onlineCarryBody != null)
            {
                onlineCarryPreviousBodyType = onlineCarryBody.bodyType;
                onlineCarryPreviousGravityScale = onlineCarryBody.gravityScale;
                onlineCarryPreviousFreezeRotation = onlineCarryBody.freezeRotation;
                onlineCarryBody.bodyType = RigidbodyType2D.Kinematic;
                onlineCarryBody.gravityScale = 0f;
                onlineCarryBody.freezeRotation = true;
                onlineCarryBody.linearVelocity = Vector2.zero;
                onlineCarryBody.angularVelocity = 0f;
            }

            onlineCarryColliders.Clear();
            onlineCarryColliderEnabledStates.Clear();
            targetLocalPlayer.GetComponentsInChildren(onlineCarryColliders);
            for (int i = 0; i < onlineCarryColliders.Count; i++)
            {
                onlineCarryColliderEnabledStates.Add(
                    onlineCarryColliders[i] != null && onlineCarryColliders[i].enabled);
                if (onlineCarryColliders[i] != null)
                {
                    onlineCarryColliders[i].enabled = false;
                }
            }

            FollowOnlineCarrier();
        }

        private void FollowOnlineCarrier()
        {
            PlayerController2D carrier = GetOnlinePlayerController(onlineCarrierPlayerId);
            PlayerController2D carried = onlineCarriedLocalPlayer;
            if (carried == null || carrier == null)
            {
                return;
            }

            Vector3 anchor = onlineCarryIsCatGrab
                ? carrier.transform.position + carrier.transform.TransformVector(onlineCarryLocalOffset)
                : carrier.transform.position + Vector3.up * 1.15f;
            BodyBuilder remoteBuilder = carrier.GetComponent<BodyBuilder>();
            if (remoteBuilder != null && !onlineCarryIsCatGrab)
            {
                anchor = remoteBuilder.GetCarryAnchorWorld(carrier.FacingDirection);
                remoteBuilder.SetCarryPose(true, carrier.FacingDirection, anchor);
            }

            anchor = ConstrainOnlineCarriedPlayerToSolidGeometry(anchor, carrier);
            anchor = ConstrainOnlineCarriedPlayerToStageBoundary(anchor);

            carried.transform.position = anchor;
            if (!onlineCarryIsCatGrab)
            {
                carried.transform.rotation = Quaternion.identity;
            }
            if (onlineCarryBody != null)
            {
                onlineCarryBody.position = anchor;
                onlineCarryBody.linearVelocity = Vector2.zero;
                onlineCarryBody.angularVelocity = 0f;
            }
        }

        private Vector3 ConstrainOnlineCarriedPlayerToSolidGeometry(
            Vector3 targetAnchor,
            PlayerController2D carrier)
        {
            PlayerController2D carried = onlineCarriedLocalPlayer;
            if (!onlineCarryHasBounds || carried == null) return targetAnchor;

            Vector2 currentPosition = carried.transform.position;
            Vector2 movement = (Vector2)targetAnchor - currentPosition;
            float distance = movement.magnitude;
            if (distance <= 0.0001f) return targetAnchor;

            Vector2 direction = movement / distance;
            Vector2 castCenter = currentPosition + onlineCarryBoundsCenterOffset;
            Vector2 castSize = new Vector2(
                Mathf.Max(0.05f, onlineCarryBoundsExtents.x * 2f),
                Mathf.Max(0.05f, onlineCarryBoundsExtents.y * 2f));
            ContactFilter2D filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = false
            };

            // The locally owned body has its colliders disabled while another
            // online player carries it. Sweep its cached full-body bounds so a
            // jumping carrier cannot move it across a thin wall in one frame.
            onlineCarrySweepHits.Clear();
            Physics2D.BoxCast(
                castCenter,
                castSize,
                0f,
                direction,
                filter,
                onlineCarrySweepHits,
                distance);

            float allowedDistance = distance;
            for (int i = 0; i < onlineCarrySweepHits.Count; i++)
            {
                RaycastHit2D hit = onlineCarrySweepHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger) continue;
                PlayerController2D hitPlayer = hitCollider.GetComponentInParent<PlayerController2D>();
                if (hitPlayer == carried || hitPlayer == carrier || hitPlayer != null) continue;
                if (Vector2.Dot(direction, hit.normal) >= -0.01f) continue;

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(0f, hit.distance - 0.06f));
            }

            if (allowedDistance >= distance - 0.0001f) return targetAnchor;

            Vector3 constrainedAnchor = currentPosition + direction * allowedDistance;
            constrainedAnchor.z = targetAnchor.z;
            return constrainedAnchor;
        }

        private Vector3 ConstrainOnlineCarriedPlayerToStageBoundary(Vector3 anchor)
        {
            if (!onlineCarryHasBounds) return anchor;

            StageEditorObject[] stageObjects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < stageObjects.Length; i++)
            {
                StageEditorObject boundary = stageObjects[i];
                if (boundary == null || boundary.type != StageObjectType.StageBoundary) continue;
                const float margin = 0.10f;
                if (!StageObjectFactory.TryGetStageBoundaryInnerEdges(
                        boundary, out float left, out float right, out float top))
                {
                    continue;
                }
                left += margin;
                right -= margin;
                top -= margin;
                Vector2 center = (Vector2)anchor + onlineCarryBoundsCenterOffset;
                if (center.x - onlineCarryBoundsExtents.x < left)
                    anchor.x += left - (center.x - onlineCarryBoundsExtents.x);
                else if (center.x + onlineCarryBoundsExtents.x > right)
                    anchor.x += right - (center.x + onlineCarryBoundsExtents.x);
                center = (Vector2)anchor + onlineCarryBoundsCenterOffset;
                if (center.y + onlineCarryBoundsExtents.y > top)
                    anchor.y += top - (center.y + onlineCarryBoundsExtents.y);
                break;
            }
            return anchor;
        }

        private void EndOnlineCarry(Vector2 releaseVelocity)
        {
            if (!onlineCarryHeld)
            {
                return;
            }

            onlineCarryHeld = false;
            PlayerController2D carrier = GetOnlinePlayerController(onlineCarrierPlayerId);
            BodyBuilder remoteBuilder = carrier != null ? carrier.GetComponent<BodyBuilder>() : null;
            remoteBuilder?.SetCarryPose(false, carrier.FacingDirection, carrier.transform.position);
            Collider2D[] releasedColliders = onlineCarryColliders.ToArray();

            for (int i = 0; i < releasedColliders.Length; i++)
            {
                if (releasedColliders[i] != null)
                {
                    releasedColliders[i].enabled = i < onlineCarryColliderEnabledStates.Count
                        ? onlineCarryColliderEnabledStates[i]
                        : true;
                }
            }

            onlineCarryColliders.Clear();
            onlineCarryColliderEnabledStates.Clear();
            onlineCarryHasBounds = false;
            PlayerController2D carried = onlineCarriedLocalPlayer;
            carried?.ResetMotion();
            if (onlineCarryBody != null)
            {
                bool isThrow = releaseVelocity.sqrMagnitude > 0.0001f;
                onlineCarryBody.simulated = true;
                // The locally owned gameplay player must be dynamic after a
                // throw. If a redraw/respawn/carry race happened to capture a
                // transient Kinematic type at pickup, restoring it here accepts
                // the velocity but never moves the body.
                onlineCarryBody.bodyType = isThrow
                    ? RigidbodyType2D.Dynamic
                    : onlineCarryPreviousBodyType;
                onlineCarryBody.gravityScale = onlineCarryPreviousGravityScale;
                onlineCarryBody.freezeRotation = onlineCarryPreviousFreezeRotation;
                onlineCarryBody.WakeUp();
                onlineCarryBody.linearVelocity = releaseVelocity;
                // Player drawings contain many irregular colliders. Rotating the
                // participant-side body after a throw can wedge it into a corner;
                // throwing changes translation, not the character's upright pose.
                onlineCarryBody.rotation = 0f;
                onlineCarryBody.angularVelocity = 0f;
            }

            carried?.RecoverAfterCarryRelease();

            carried?.SetControlsEnabled(stageStarted && !drawing && !cleared
                && !stageEditing && !optionOverlayActive);
            carried?.SetHumanCarryStruggling(false);
            onlineCarryBody = null;
            onlineCarriedLocalPlayer = null;
            onlineCarrierPlayerId = null;
            onlineCarryBeganAt = 0f;
            onlineCarryLastConfirmedAt = 0f;
            onlineCarryIsCatGrab = false;
            onlineCarryLocalOffset = Vector2.zero;
            // Online player pairs are already kept non-colliding by
            // IgnoreOnlinePlayerCollisions. Do not unignore and immediately
            // re-ignore every hand-drawn segment here: dense drawings turn that
            // redundant O(n*m) work into a visible throw hitch on participants.
        }

        private void IgnoreOnlinePlayerCollisions(PlayerController2D remote)
        {
            if (remote == null || onlineManager == null || onlineManager.CurrentLobby == null) return;
            for (int i = 0; i < localPlayers.Count; i++)
                SetPlayerPairCollisionIgnored(localPlayers[i], remote, true);
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                if (pair.Value != null && pair.Value != remote)
                    SetPlayerPairCollisionIgnored(pair.Value, remote, true);
        }

        internal void RefreshOnlinePlayerCollisionSafety()
        {
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                IgnoreOnlinePlayerCollisions(pair.Value);
        }

        private static void SetPlayerPairCollisionIgnored(
            PlayerController2D firstPlayer,
            PlayerController2D secondPlayer,
            bool ignored)
        {
            if (firstPlayer == null || secondPlayer == null || firstPlayer == secondPlayer) return;
            Collider2D[] first = firstPlayer.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] second = secondPlayer.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] == null || first[i].isTrigger) continue;
                for (int j = 0; j < second.Length; j++)
                    if (second[j] != null && !second[j].isTrigger && first[i] != second[j])
                        Physics2D.IgnoreCollision(first[i], second[j], ignored);
            }
        }

        internal void SetOfflinePlayerCollisionIgnored(bool ignored)
        {
            if (IsOnlineInStage()) return;
            for (int i = 0; i < localPlayers.Count; i++)
                for (int j = i + 1; j < localPlayers.Count; j++)
                    SetPlayerPairCollisionIgnored(localPlayers[i], localPlayers[j], ignored);
        }

        public bool ApplyOnlineRemoteBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || drawManager == null || string.IsNullOrEmpty(bodyData.Json))
            {
                return false;
            }

            PlayerController2D remotePlayer = EnsureOnlineRemotePlayer(bodyData.PlayerId);
            if (remotePlayer == null)
            {
                return false;
            }

            if (IsOnlineBodyRebuildBlocked(bodyData.PlayerId))
            {
                // OnlinePlayerSync normally defers this data. Never tear down a
                // live carry merely to rebuild the remote player's drawing: that
                // races with continuous carry state and can leave the carried
                // player's Rigidbody kinematic or its colliders disabled.
                return false;
            }

            DrawManager.DrawingState remoteState = drawManager.CreateStateFromBodyJson(bodyData.Json);
            if (remoteState == null)
            {
                return false;
            }

            SaveDrawingState(player);
            BodyBuilder remoteBuilder = remotePlayer.GetComponent<BodyBuilder>();
            PlayerAbilityController remoteAbilities = remotePlayer.GetComponent<PlayerAbilityController>();
            drawManager.SetBuildTarget(remoteBuilder, remoteAbilities);
            drawManager.LoadState(remoteState, true);
            drawingStates[remotePlayer] = CloneDrawingState(remoteState);
            remoteBuilder?.SetNetworkBodyReady(true);
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            remotePlayer.SetControlsEnabled(false);
            LiftPlayerOutOfGround(remotePlayer);
            IgnoreOnlinePlayerCollisions(remotePlayer);
            ResolveSimultaneousUniqueSpeciesConflict(bodyData.PlayerId, remoteState.Species);
            // BodyDataReceived can reach DrawManager before this method has
            // replaced the remote player's confirmed species. Refresh again after
            // the authoritative state is applied so a resolved warning disappears.
            drawManager.RefreshUniqueSpeciesAvailability();
            return true;
        }

        public void SendLocalOnlineBodyData()
        {
            if (drawManager == null || onlineManager == null)
            {
                return;
            }

            OnlineConnectionState state = onlineManager.State;
            if (state != OnlineConnectionState.InLobby
                && state != OnlineConnectionState.Matching
                && state != OnlineConnectionState.Playing)
            {
                return;
            }

            if (onlineManager.CurrentLobby == null)
            {
                return;
            }

            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (localPlayers.Count > 1 && localCoop != null)
                localCoop.SendAllLocalBodies();
            else
                drawManager.SendCurrentBodyData();
        }

        public void SendLocalOnlineBodyData(string requestedGamePlayerId)
        {
            if (onlineManager == null || string.IsNullOrEmpty(requestedGamePlayerId)) return;
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (localCoop != null
                && localCoop.SendLocalBody(requestedGamePlayerId, onlineManager.LocalPlayerId)) return;
            if (requestedGamePlayerId == onlineManager.LocalPlayerId) SendLocalOnlineBodyData();
        }

        public void AddCharacter()
        {
            if (!CanUseGameplayCharacterControls)
            {
                ShowReadyRoomOnlyCharacterChangeNotice();
                return;
            }

            AddCharacterInternal();
        }

        private void AddCharacterInternal()
        {
            AddLocalCharacter();
        }

        public PlayerController2D AddLocalCharacter(bool deferReadyReset = false)
        {
            if (localPlayers.Count >= MaxLocalPlayersPerConnection || primaryPlayer == null)
            {
                return null;
            }

            // The shared-character drawer can add another avatar after Q has
            // selected P2/P3. DrawManager currently represents that selected
            // avatar, so saving it as primary would overwrite P1's body state.
            PlayerController2D sourcePlayer = player != null && localPlayers.Contains(player)
                ? player
                : primaryPlayer;
            SaveDrawingState(sourcePlayer);
            int localIndex = localPlayers.Count;
            Vector3 offset = new Vector3(
                Mathf.Sign(sourcePlayer.FacingDirection) * 1.25f,
                0.35f,
                0f);
            GameObject clone = Instantiate(
                sourcePlayer.gameObject,
                sourcePlayer.transform.position + offset,
                sourcePlayer.transform.rotation,
                sourcePlayer.transform.parent);
            clone.name = $"Player {localIndex + 1}";
            PlayerController2D addedPlayer = clone.GetComponent<PlayerController2D>();
            if (addedPlayer == null)
            {
                Destroy(clone);
                return null;
            }

            localPlayers.Add(addedPlayer);
            if (secondaryPlayer == null) secondaryPlayer = addedPlayer;
            // A gameplay "add character" is still controlled by P1 through
            // character switching. Instantiate copied P1's binding component,
            // so explicitly clear that copied device assignment here. The local
            // co-op join path binds its gamepad immediately after this returns.
            LocalPlayerInputBinding addedBinding = addedPlayer.InputBinding;
            if (addedBinding == null)
                addedBinding = addedPlayer.gameObject.AddComponent<LocalPlayerInputBinding>();
            addedBinding.BindAutoPrimary();
            addedBinding.SetLocalPlayerIndex(localIndex);
            addedPlayer.transform.localScale = stablePlayerLocalScale;
            addedPlayer.ResetMotion();
            addedPlayer.SetControlsEnabled(false);
            if (drawingStates.TryGetValue(sourcePlayer, out DrawManager.DrawingState state))
            {
                drawingStates[addedPlayer] = CloneDrawingState(state);
            }

            if (RequiresUniquePlayerSpecies)
            {
                int speciesSlot = localIndex;
                if (onlineManager?.CurrentLobby != null)
                {
                    int connectionSlot = GetLobbyPlayerSlot(onlineManager.LocalPlayerId);
                    if (connectionSlot >= 0) speciesSlot = connectionSlot + localIndex;
                }
                AssignSpeciesToPlayer(addedPlayer, GetUniqueSpeciesForSlot(speciesSlot));
                ConfigureActivePlayerTargets();
                LoadDrawingState(player);
            }

            BodyBuilder bodyBuilder = addedPlayer.GetComponent<BodyBuilder>();
            if (bodyBuilder != null)
            {
                bodyBuilder.SetFacingDirection(sourcePlayer.FacingDirection);
                bodyBuilder.SetPlayerColor(PlayerColorPalette.GetColor(localIndex));
            }

            ApplyDefaultPlayerColors();
            LiftPlayerOutOfGround(addedPlayer);
            RefreshControlledPlayerMarkers();
            drawManager?.RefreshInkBudgetDisplay();
            // A dedicated local-coop join binds its device immediately after
            // this method returns. SetLocalReady raises StateChanged
            // synchronously, and the online roster handler removes secondary
            // AutoPrimary characters. Defer that notification until the caller
            // has installed the dedicated binding so the new seat is not
            // mistaken for a drawer-added offline character.
            if (!deferReadyReset) onlineManager?.SetLocalReady(false);
            return addedPlayer;
        }

        private void RestoreOnlineProxyScale(PlayerController2D remote)
        {
            if (remote == null || remote == primaryPlayer) return;
            remote.transform.localScale = stablePlayerLocalScale;
        }

        public void DeleteAddedCharacter()
        {
            if (!CanUseGameplayCharacterControls)
            {
                ShowReadyRoomOnlyCharacterChangeNotice();
                return;
            }
            if (localPlayers.Count <= 1)
            {
                return;
            }

            RemoveLocalCharacter(localPlayers[localPlayers.Count - 1]);
        }

        public bool RemoveLocalCharacter(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || targetPlayer == primaryPlayer || !localPlayers.Contains(targetPlayer))
                return false;

            int removedIndex = localPlayers.IndexOf(targetPlayer);
            if (onlineManager != null && !string.IsNullOrEmpty(onlineManager.LocalPlayerId))
            {
                // Local indices are compact game-player ids. If a middle seat
                // leaves, retire every old id that will shift before publishing
                // the compacted roster on the next state tick.
                for (int i = removedIndex; i < localPlayers.Count; i++)
                    onlineManager.AnnounceLocalGamePlayerLeft(
                        OnlinePlayerIdentity.Compose(onlineManager.LocalPlayerId, i));
            }

            if (player == targetPlayer)
            {
                SetActivePlayer(primaryPlayer, true);
            }

            primaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            targetPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();

            GameObject target = targetPlayer.gameObject;
            drawingStates.Remove(targetPlayer);
            localPlayers.Remove(targetPlayer);
            for (int i = removedIndex; i < localPlayers.Count; i++)
                localPlayers[i]?.InputBinding?.SetLocalPlayerIndex(i);
            secondaryPlayer = localPlayers.Count > 1 ? localPlayers[1] : null;
            Destroy(target);
            onlineManager?.SetLocalReady(false);
            RefreshControlledPlayerMarkers();
            drawManager?.RefreshInkBudgetDisplay();
            return true;
        }

        public int GetInkBudgetPlayerCount()
        {
            if (IsOnlineInStage())
            {
                return onlineManager != null ? onlineManager.GetInkBudgetPlayerCount() : 1;
            }

            int count = 0;
            for (int i = 0; i < localPlayers.Count; i++)
                if (IsActiveBudgetPlayer(localPlayers[i])) count++;
            return Mathf.Max(1, count);
        }

        public float GetConfirmedInkExcludingActivePlayer(PlayerController2D editingPlayer = null)
        {
            editingPlayer ??= player;
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (IsOnlineInStage())
            {
                float onlineTotal = onlineManager != null
                    ? onlineManager.GetConfirmedInkExcludingPlayer(GetOnlinePlayerId(editingPlayer))
                    : 0f;
                // Local split-screen players share one connection, but their two
                // DRAW sessions can be edited simultaneously. Replace stale
                // confirmed values with each session's live value.
                if (onlineManager != null && localCoop != null)
                {
                    for (int i = 0; i < localPlayers.Count; i++)
                    {
                        PlayerController2D local = localPlayers[i];
                        if (local == editingPlayer || !IsActiveBudgetPlayer(local)
                            || !localCoop.TryGetLocalDrawSessionInk(local, out float liveInk)) continue;
                        onlineTotal += liveInk - onlineManager.GetConfirmedInkForPlayer(GetOnlinePlayerId(local));
                    }
                }
                return Mathf.Max(0f, onlineTotal);
            }

            float total = 0f;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (local == editingPlayer || !IsActiveBudgetPlayer(local)) continue;
                total += localCoop != null
                    && localCoop.TryGetLocalDrawSessionInk(local, out float liveInk)
                        ? liveInk
                        : GetConfirmedPlayerInk(local);
            }
            return total;
        }

        public string GetInkUsageBreakdown(float activePlayerInk, PlayerController2D editingPlayer = null)
        {
            editingPlayer ??= player;
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            List<string> entries = new List<string>();
            if (IsOnlineInStage() && onlineManager != null)
            {
                OnlinePlayerInfo[] lobbyPlayers = onlineManager.CurrentLobby?.Players;
                if (lobbyPlayers == null) return string.Empty;
                List<OnlinePlayerInfo> ordered = new List<OnlinePlayerInfo>();
                for (int i = 0; i < lobbyPlayers.Length; i++)
                    if (lobbyPlayers[i] != null && !string.IsNullOrEmpty(lobbyPlayers[i].PlayerId))
                        ordered.Add(lobbyPlayers[i]);
                ordered.Sort((left, right) => left.PlayerSlot.CompareTo(right.PlayerSlot));
                for (int i = 0; i < ordered.Count; i++)
                {
                    OnlinePlayerInfo info = ordered[i];
                    int slot = info.PlayerSlot >= 0 ? info.PlayerSlot : i;
                    float ink;
                    if (info.PlayerId == GetOnlinePlayerId(editingPlayer)) ink = activePlayerInk;
                    else
                    {
                        ink = onlineManager.GetConfirmedInkForPlayer(info.PlayerId);
                        for (int localIndex = 0; localIndex < localPlayers.Count; localIndex++)
                        {
                            PlayerController2D local = localPlayers[localIndex];
                            if (GetOnlinePlayerId(local) == info.PlayerId && localCoop != null
                                && localCoop.TryGetLocalDrawSessionInk(local, out float liveInk))
                            {
                                ink = liveInk;
                                break;
                            }
                        }
                    }
                    entries.Add($"P{slot + 1} {ink:0.#}");
                }
            }
            else
            {
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerController2D local = localPlayers[i];
                    if (!IsActiveBudgetPlayer(local)) continue;
                    float ink = editingPlayer == local
                        ? activePlayerInk
                        : localCoop != null
                            && localCoop.TryGetLocalDrawSessionInk(local, out float liveInk)
                                ? liveInk
                                : GetConfirmedPlayerInk(local);
                    entries.Add($"P{i + 1} {ink:0.#}");
                }
            }
            return string.Join("  ", entries);
        }

        private static bool IsActiveBudgetPlayer(PlayerController2D target)
        {
            return target != null
                && target.isActiveAndEnabled
                && target.gameObject.activeInHierarchy;
        }

        private static float GetConfirmedPlayerInk(PlayerController2D target)
        {
            PlayerAbilityController abilities = target != null
                ? target.GetComponent<PlayerAbilityController>()
                : null;
            return abilities != null ? Mathf.Max(0f, abilities.CurrentProfile.TotalInk) : 0f;
        }

        public void SwitchCharacter()
        {
            if (primaryPlayer == null || localPlayers.Count <= 1)
            {
                return;
            }
            if (!CanSwitchGameplayCharacter)
            {
                ShowReadyRoomOnlyCharacterChangeNotice();
                return;
            }
            int current = Mathf.Max(0, localPlayers.IndexOf(player));
            SetActivePlayer(localPlayers[(current + 1) % localPlayers.Count], true);
        }

        public void ShowReadyRoomOnlyCharacterChangeNotice()
        {
            string key = stageStarted && !drawing
                && (!RequiresChallengeReadyRoom() || IsChallengeReadyRoomActive)
                && !StageRedrawZone.IsPlayerInZone(player)
                    ? "character_switch_redraw_zone_only"
                    : "character_change_ready_room_only";
            uiManager?.ShowGameplayNotice(LocalizationManager.T(key));
        }

        private void RestoreRedrawPose(bool returnToStart)
        {
            if (player == null)
            {
                return;
            }

            if (redrawRespawnRoutine != null)
            {
                StopCoroutine(redrawRespawnRoutine);
            }

            PlayerController2D redrawPlayer = player;
            Vector3 returnPosition;
            if (returnToStart && assignedPlayerStartPositions.TryGetValue(redrawPlayer, out Vector3 assignedPosition))
            {
                returnPosition = assignedPosition;
                redrawPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();
                if (onlineCarryHeld)
                {
                    EndOnlineCarry(Vector2.zero);
                }
            }
            else if (returnToStart && spawnPoint != null)
            {
                returnPosition = spawnPoint.position + GetRespawnOffset(redrawPlayer);
                redrawPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();
                if (onlineCarryHeld)
                {
                    EndOnlineCarry(Vector2.zero);
                }
            }
            else
            {
                returnPosition = hasRedrawReturnPosition && redrawReturnPlayer == redrawPlayer
                    ? redrawReturnPosition
                    : redrawPlayer.transform.position;
            }
            bool alignRebuiltBottom;
            float desiredBottomY;
            if (returnToStart && TryFindGroundSurfaceBelow(returnPosition, out float spawnSurfaceY))
            {
                alignRebuiltBottom = true;
                desiredBottomY = spawnSurfaceY + groundSeparation;
            }
            else
            {
                alignRebuiltBottom = !returnToStart
                    && hasRedrawReturnBottom
                    && redrawReturnPlayer == redrawPlayer;
                desiredBottomY = redrawReturnBottomY;
            }
            hasRedrawReturnPosition = false;
            hasRedrawReturnBottom = false;
            redrawReturnPlayer = null;
            redrawPlayer.SetControlsEnabled(false);
            redrawPlayer.ResetMotion();
            redrawRespawnRoutine = StartCoroutine(CompleteRedrawRespawn(
                redrawPlayer,
                returnPosition,
                alignRebuiltBottom,
                desiredBottomY,
                returnToStart));
        }

        public void RecordAssignedPlayerStart(PlayerController2D targetPlayer, Vector3 position)
        {
            if (targetPlayer != null)
            {
                assignedPlayerStartPositions[targetPlayer] = position;
            }
        }

        private void BeginChallengeReadyRoomIfNeeded()
        {
            challengeRunStarted = !RequiresChallengeReadyRoom();
            if (!RequiresChallengeReadyRoom() || testingEditedStage || stageLoader == null
                || stageLoader.LoadedStageRoot == null)
            {
                challengeReadyRoom = null;
                return;
            }

            GameObject readyRoot = new GameObject("Challenge Redraw Ready Room");
            challengeReadyRoom = readyRoot.AddComponent<StageChallengeReadyRoomController>();
            challengeReadyRoom.Configure(this, stageLoader.LoadedStageRoot);
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        internal void RequestChallengeSessionState()
        {
            if (!IsOnlineInStage() || IsLocalOnlineHost(onlineManager.CurrentLobby)
                || onlineManager == null || string.IsNullOrEmpty(currentStageId)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindChallengeSessionRequest,
                Json = "{}"
            });
        }

        private void SendChallengeSessionState(string targetPlayerId)
        {
            if (onlineManager == null || string.IsNullOrEmpty(targetPlayerId)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindChallengeSessionState,
                Json = JsonUtility.ToJson(new ChallengeSessionMessage
                {
                    TargetPlayerId = targetPlayerId,
                    RunStarted = challengeRunStarted
                })
            });
        }

        internal void CompleteChallengeReadyRoom()
        {
            challengeRunStarted = true;
            challengeReadyRoom = null;
        }

        internal bool TryResolveChallengeStartPosition(
            PlayerController2D targetPlayer,
            Vector3 preferred,
            out Vector3 safePosition)
        {
            safePosition = preferred;
            if (targetPlayer == null) return true;

            Vector3 original = targetPlayer.transform.position;
            bool resolved = TryPlaceAtSafeSpawn(targetPlayer, preferred, out safePosition, true);
            TeleportPlayerWithoutPhysics(targetPlayer, original);
            targetPlayer.ResetMotion();
            Physics2D.SyncTransforms();
            return resolved;
        }

        internal void HandleChallengeStartFitRejected(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null) return;
            if (IsOnlineInStage() && !IsLocallyControlledPlayer(targetPlayer)) return;

            // The host validates every ready-room avatar, but only the owning
            // machine may open DRAW. Select the rejected local/offline avatar
            // before loading the editor target so a large teammate can never
            // force the currently selected, smaller body to be redrawn.
            spawnCorrectionRequired = true;
            if (player != targetPlayer)
                SetActivePlayer(targetPlayer, false);

            // Repeated authoritative packets are expected online. Do not reopen
            // DRAW or replace its edit snapshot when the same rejection arrives.
            if (!drawing)
                EnterDrawingMode();
            drawManager?.ShowStageFitError();
        }

        internal void CancelChallengeReadyRoomReference(StageChallengeReadyRoomController room)
        {
            if (challengeReadyRoom == room)
            {
                challengeReadyRoom = null;
            }
        }

        private void BeginNormalStageSpawnValidationIfNeeded()
        {
            if (RequiresChallengeReadyRoom() || testingEditedStage || currentStageId == "1-0") return;
            spawnFitValidationRoutine = StartCoroutine(ValidateInitialSpawnFit());
        }

        private IEnumerator ValidateInitialSpawnFit()
        {
            // Let stage-specific Start methods finish placing players first.
            yield return null;

            List<PlayerController2D> candidates = new List<PlayerController2D>();
            for (int i = 0; i < localPlayers.Count; i++)
                if (localPlayers[i] != null) candidates.Add(localPlayers[i]);

            for (int i = 0; i < candidates.Count; i++)
            {
                PlayerController2D candidate = candidates[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
                Vector3 preferred = candidate.transform.position;
                if (TryPlaceAtSafeSpawn(candidate, preferred, out Vector3 safePosition, true))
                {
                    RecordAssignedPlayerStart(candidate, safePosition);
                    continue;
                }

                HandleChallengeStartFitRejected(candidate);
                spawnFitValidationRoutine = null;
                yield break;
            }

            spawnCorrectionRequired = false;
            spawnFitValidationRoutine = null;
        }

        private IEnumerator ValidateRedrawFitAndComplete(bool showSavedNotice)
        {
            // BodyBuilder replaces generated colliders at the end of the frame.
            yield return null;
            PlayerController2D redrawPlayer = player;
            if (redrawPlayer == null)
            {
                if (showSavedNotice) drawManager?.ShowDrawingSaveError();
                spawnFitValidationRoutine = null;
                yield break;
            }

            Vector3 hiddenPosition = redrawPlayer.transform.position;
            // RedrawZoneベースのinPlace判定。
            // 既存のハードコードIDはゾーンをJSONに追加することで置き換え済み。
            // 後方互換のためハードコードIDも残す。
            bool redrawInPlace = StageRedrawZone.IsPlayerInZone(redrawPlayer)
                || currentStageId == "6-3" || currentStageId == "8-3" || currentStageId == "14-3"
                || currentStageId == "10-3" && challengeRunStarted;
            Vector3 preferred = redrawInPlace && hasRedrawReturnPosition && redrawReturnPlayer == redrawPlayer
                ? redrawReturnPosition
                : assignedPlayerStartPositions.TryGetValue(redrawPlayer, out Vector3 assigned)
                    ? assigned
                    : spawnPoint != null ? spawnPoint.position + GetRespawnOffset(redrawPlayer) : hiddenPosition;

            SetPlayerRedrawingState(redrawPlayer, false);
            Physics2D.SyncTransforms();
            if (!TryPlaceAtSafeSpawn(redrawPlayer, preferred, out Vector3 safePosition, !redrawInPlace))
            {
                TeleportPlayerWithoutPhysics(redrawPlayer, hiddenPosition);
                redrawPlayer.ResetMotion();
                SetPlayerRedrawingState(redrawPlayer, true);
                spawnCorrectionRequired = true;
                drawManager?.ShowStageFitError();
                spawnFitValidationRoutine = null;
                yield break;
            }

            assignedPlayerStartPositions[redrawPlayer] = safePosition;
            if (redrawInPlace)
            {
                redrawReturnPlayer = redrawPlayer;
                redrawReturnPosition = safePosition;
                hasRedrawReturnPosition = true;
            }
            TeleportPlayerWithoutPhysics(redrawPlayer, hiddenPosition);
            SetPlayerRedrawingState(redrawPlayer, true);
            spawnCorrectionRequired = false;
            spawnFitValidationRoutine = null;
            drawManager?.CommitEditing();
            ExitDrawingMode(!redrawInPlace);
            if (showSavedNotice)
                uiManager?.ShowGameplayNotice(LocalizationManager.T("draw_save_complete"));
        }

        private IEnumerator ValidateReadyRoomRedrawFitAndComplete(bool showSavedNotice)
        {
            // Wait for the selected species to finish rebuilding its colliders.
            yield return null;
            PlayerController2D redrawPlayer = player;
            if (redrawPlayer == null || challengeReadyRoom == null)
            {
                if (showSavedNotice) drawManager?.ShowDrawingSaveError();
                spawnFitValidationRoutine = null;
                yield break;
            }

            SetPlayerRedrawingState(redrawPlayer, false);
            Physics2D.SyncTransforms();
            if (!challengeReadyRoom.TryGetFittedReturnPosition(redrawPlayer, out Vector3 fittedPosition))
            {
                SetPlayerRedrawingState(redrawPlayer, true);
                drawManager?.ShowReadyRoomFitError();
                spawnFitValidationRoutine = null;
                yield break;
            }

            redrawReturnPlayer = redrawPlayer;
            redrawReturnPosition = fittedPosition;
            hasRedrawReturnPosition = true;
            hasRedrawReturnBottom = false;
            SetPlayerRedrawingState(redrawPlayer, true);
            spawnCorrectionRequired = false;
            spawnFitValidationRoutine = null;
            drawManager?.CommitEditing();
            ExitDrawingMode(false);
            if (showSavedNotice)
                uiManager?.ShowGameplayNotice(LocalizationManager.T("draw_save_complete"));
        }

        private bool TryPlaceAtSafeSpawn(
            PlayerController2D targetPlayer,
            Vector3 preferred,
            out Vector3 safePosition,
            bool enforceStartSlot = false)
        {
            safePosition = preferred;
            if (targetPlayer == null || !TryGetPlayerSolidBounds(targetPlayer, out Bounds initialBounds))
                return true;

            Vector3 original = targetPlayer.transform.position;
            float minimumRootX = float.NegativeInfinity;
            float maximumRootX = float.PositiveInfinity;
            if (enforceStartSlot
                && !TryGetStageStartSlot(
                    targetPlayer,
                    initialBounds,
                    preferred,
                    out preferred.x,
                    out minimumRootX,
                    out maximumRootX))
            {
                return false;
            }

            // A one-way platform can be entered from below, so it must not make
            // a large drawing fail the challenge-start fit validation.
            bool requiresGround = TryFindGroundSurfaceBelow(preferred, out _, false);
            const float step = 0.75f;
            const int searchRings = 12;
            for (int ring = 0; ring <= searchRings; ring++)
            {
                int variants = ring == 0 ? 1 : 2;
                for (int side = 0; side < variants; side++)
                {
                    float direction = side == 0 ? 1f : -1f;
                    Vector3 candidate = preferred + Vector3.right * (ring * step * direction);
                    if (candidate.x < minimumRootX || candidate.x > maximumRootX) continue;
                    if (requiresGround && !TryFindGroundSurfaceBelow(candidate, out _, false)) continue;
                    TeleportPlayerWithoutPhysics(targetPlayer, candidate);
                    AlignPlayerBottomToGround(targetPlayer, candidate, false);
                    Physics2D.SyncTransforms();
                    if (!HasStageSolidOverlap(targetPlayer))
                    {
                        safePosition = targetPlayer.transform.position;
                        targetPlayer.ResetMotion();
                        return true;
                    }
                }
            }

            TeleportPlayerWithoutPhysics(targetPlayer, original);
            targetPlayer.ResetMotion();
            return false;
        }

        private bool TryGetStageStartSlot(
            PlayerController2D targetPlayer,
            Bounds bodyBounds,
            Vector3 preferred,
            out float preferredRootX,
            out float minimumRootX,
            out float maximumRootX)
        {
            preferredRootX = preferred.x;
            minimumRootX = float.NegativeInfinity;
            maximumRootX = float.PositiveInfinity;
            if (targetPlayer == null || spawnPoint == null) return true;

            int playerCount;
            int playerSlot;
            if (IsOnlineInStage())
            {
                playerCount = GetLobbyPlayerCount();
                playerSlot = GetLobbyPlayerSlot(GetOnlinePlayerId(targetPlayer));
            }
            else
            {
                playerCount = Mathf.Max(1, localPlayers.Count);
                playerSlot = localPlayers.IndexOf(targetPlayer);
            }
            playerCount = Mathf.Clamp(playerCount, 1, 4);
            if (playerSlot < 0 || playerSlot >= playerCount) return true;

            const float bodyGap = 0.24f;
            const float laneInset = bodyGap * 0.5f;
            float bodyCenterOffset = bodyBounds.center.x - targetPlayer.transform.position.x;
            float bodyLeftOffset = bodyBounds.min.x - targetPlayer.transform.position.x;
            float bodyRightOffset = bodyBounds.max.x - targetPlayer.transform.position.x;

            // 12-2 deliberately starts between a row of jump pads. Preserve its
            // authored safe centers while still rejecting a body wider than the
            // space assigned to one participant.
            if (currentStageId == "12-2")
            {
                const float specialLaneWidth = 5.6f;
                float laneCenter = preferred.x;
                float laneLeft = laneCenter - specialLaneWidth * 0.5f;
                float laneRight = laneCenter + specialLaneWidth * 0.5f;
                minimumRootX = laneLeft + laneInset - bodyLeftOffset;
                maximumRootX = laneRight - laneInset - bodyRightOffset;
                preferredRootX = laneCenter - bodyCenterOffset;
                return minimumRootX <= maximumRootX;
            }

            // 9-3 is authored at double spatial scale and intentionally gives
            // each player a much wider start lane for large slime drawings.
            if (currentStageId == "9-3")
            {
                const float largeLaneWidth = 15.5f;
                float laneCenter = preferred.x;
                float laneLeft = laneCenter - largeLaneWidth * 0.5f;
                float laneRight = laneCenter + largeLaneWidth * 0.5f;
                minimumRootX = laneLeft + laneInset - bodyLeftOffset;
                maximumRootX = laneRight - laneInset - bodyRightOffset;
                preferredRootX = laneCenter - bodyCenterOffset;
                return minimumRootX <= maximumRootX;
            }

            if (!TryGetStageStartHorizontalRange(out float availableLeft, out float availableRight))
                return true;

            float availableWidth = availableRight - availableLeft;
            if (availableWidth <= bodyGap * playerCount) return false;

            float widestBody = GetWidestKnownStartBody(bodyBounds.size.x);
            float desiredLaneWidth = Mathf.Max(2.2f, widestBody + bodyGap);
            float laneWidth = Mathf.Min(desiredLaneWidth, availableWidth / playerCount);
            float groupWidth = laneWidth * playerCount;
            float desiredCenter = spawnPoint.position.x;
            float groupCenter = Mathf.Clamp(
                desiredCenter,
                availableLeft + groupWidth * 0.5f,
                availableRight - groupWidth * 0.5f);
            float groupLeft = groupCenter - groupWidth * 0.5f;
            float slotLeft = groupLeft + laneWidth * playerSlot;
            float slotRight = slotLeft + laneWidth;
            float slotCenter = (slotLeft + slotRight) * 0.5f;

            minimumRootX = slotLeft + laneInset - bodyLeftOffset;
            maximumRootX = slotRight - laneInset - bodyRightOffset;
            preferredRootX = slotCenter - bodyCenterOffset;
            return minimumRootX <= maximumRootX;
        }

        private bool TryGetStageStartHorizontalRange(out float left, out float right)
        {
            left = 0f;
            right = 0f;
            if (spawnPoint == null) return false;

            if (TryGetSpawnSurfaceHorizontalBounds(spawnPoint.position, out left, out right))
            {
                const float platformEdgeInset = 0.18f;
                left += platformEdgeInset;
                right -= platformEdgeInset;
                return right > left;
            }

            StageEditorObject[] stageObjects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < stageObjects.Length; i++)
            {
                StageEditorObject boundary = stageObjects[i];
                if (boundary == null || boundary.type != StageObjectType.StageBoundary) continue;
                if (!StageObjectFactory.TryGetStageBoundaryInnerEdges(
                        boundary, out float boundaryLeft, out float boundaryRight, out _)) continue;
                if (spawnPoint.position.x < boundaryLeft || spawnPoint.position.x > boundaryRight) continue;
                const float boundaryInset = 0.16f;
                left = boundaryLeft + boundaryInset;
                right = boundaryRight - boundaryInset;
                return right > left;
            }
            return false;
        }

        private float GetWidestKnownStartBody(float fallbackWidth)
        {
            float widest = Mathf.Max(0.1f, fallbackWidth);
            for (int i = 0; i < localPlayers.Count; i++)
                if (TryGetPlayerSolidBounds(localPlayers[i], out Bounds localBounds))
                    widest = Mathf.Max(widest, localBounds.size.x);
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
                if (TryGetPlayerSolidBounds(pair.Value, out Bounds remoteBounds))
                    widest = Mathf.Max(widest, remoteBounds.size.x);
            return widest;
        }

        private bool HasStageSolidOverlap(PlayerController2D targetPlayer)
        {
            if (TryGetPlayerSolidBounds(targetPlayer, out Bounds bodyBounds))
            {
                StageEditorObject[] stageObjects = Object.FindObjectsByType<StageEditorObject>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < stageObjects.Length; i++)
                {
                    StageEditorObject boundary = stageObjects[i];
                    if (boundary == null || boundary.type != StageObjectType.StageBoundary) continue;
                    // StageBoundary intentionally has no bottom. Validate the
                    // entire drawing against its ceiling and side walls because
                    // a sparse, oversized body can sit completely beyond a thin
                    // wall without any individual line collider overlapping it.
                    const float insideMargin = 0.08f;
                    if (!StageObjectFactory.TryGetStageBoundaryInnerEdges(
                            boundary, out float left, out float right, out float top))
                    {
                        continue;
                    }
                    left += insideMargin;
                    right -= insideMargin;
                    top -= insideMargin;
                    if (bodyBounds.min.x < left || bodyBounds.max.x > right || bodyBounds.max.y > top)
                        return true;
                }
            }

            Collider2D[] playerColliders = targetPlayer.GetComponentsInChildren<Collider2D>(true);
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;
            List<Collider2D> overlaps = new List<Collider2D>();
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger) continue;
                overlaps.Clear();
                playerCollider.Overlap(filter, overlaps);
                for (int hit = 0; hit < overlaps.Count; hit++)
                {
                    Collider2D other = overlaps[hit];
                    if (other == null || other.isTrigger || other.transform.IsChildOf(targetPlayer.transform)) continue;
                    // Player layers can be included in the stage-solid mask on
                    // multiplayer scenes. Another participant is not part of the
                    // room or stage geometry and must never make a body fail the
                    // spawn-size validation.
                    if (other.GetComponentInParent<PlayerController2D>() != null) continue;
                    if (IsOneWayPassThroughSurface(other)) continue;
                    if (playerCollider.Distance(other).isOverlapped) return true;
                }
            }
            return false;
        }

        private bool TryFindGroundSurfaceBelow(
            Vector3 position,
            out float surfaceY,
            bool includeOneWay = true)
        {
            Vector2 origin = new Vector2(position.x, position.y + 0.35f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 8f, groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger
                    || collider.GetComponentInParent<PlayerController2D>() != null)
                {
                    continue;
                }
                if (!includeOneWay && IsOneWayPassThroughSurface(collider)) continue;

                surfaceY = hits[i].point.y;
                return true;
            }
            surfaceY = 0f;
            return false;
        }

        private static bool IsOneWayPassThroughSurface(Collider2D collider)
        {
            if (collider == null) return false;
            PlatformEffector2D effector = collider.GetComponentInParent<PlatformEffector2D>();
            if (effector != null && effector.useOneWay) return true;

            StageEditorObject marker = collider.GetComponentInParent<StageEditorObject>();
            return marker != null
                && (marker.type == StageObjectType.OneWayPlatform
                    || marker.type == StageObjectType.MovingOneWayPlatform
                    || marker.type == StageObjectType.EscortPlayerOneWayFloor);
        }

        private IEnumerator CompleteRedrawRespawn(
            PlayerController2D redrawPlayer,
            Vector3 returnPosition,
            bool alignRebuiltBottom,
            float desiredBottomY,
            bool resolveSpawnOverlap)
        {
            // BodyBuilder destroys the old hand-drawn colliders at end of frame.
            // Wait until they are gone before testing the rebuilt body against
            // the stage, otherwise the stale body can push the player above it.
            yield return null;

            if (redrawPlayer != null)
            {
                TeleportPlayerWithoutPhysics(redrawPlayer, returnPosition);
                redrawPlayer.ResetMotion();
                SetPlayerRedrawingState(redrawPlayer, false);
                // Species changes replace all generated colliders. Register the
                // new human geometry before resolving its contact with the floor.
                Physics2D.SyncTransforms();
                if (alignRebuiltBottom && TryGetPlayerSolidBounds(redrawPlayer, out Bounds rebuiltBounds))
                {
                    float correction = desiredBottomY - rebuiltBounds.min.y;
                    if (Mathf.Abs(correction) > 0.0001f)
                    {
                        Vector3 corrected = redrawPlayer.transform.position;
                        corrected.y += correction;
                        TeleportPlayerWithoutPhysics(redrawPlayer, corrected);
                        Physics2D.SyncTransforms();
                    }
                }
                // An in-place redraw was already validated at its exact return
                // position. Running the generic spawn lift again can choose the
                // far side of a nearby ceiling and eject the body above the room.
                if (resolveSpawnOverlap) LiftPlayerOutOfGround(redrawPlayer);
                Physics2D.SyncTransforms();
                redrawPlayer.SetControlsEnabled(
                    stageStarted && !drawing && !cleared && !stageEditing);
            }

            redrawRespawnRoutine = null;
        }

        private static bool TryGetPlayerSolidBounds(PlayerController2D targetPlayer, out Bounds bounds)
        {
            bounds = new Bounds(targetPlayer != null ? targetPlayer.transform.position : Vector3.zero, Vector3.zero);
            if (targetPlayer == null) return false;

            Collider2D[] colliders = targetPlayer.GetComponentsInChildren<Collider2D>(true);
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }
            return found;
        }

        private void LiftPlayerOutOfGround(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            Collider2D[] colliders = targetPlayer.GetComponentsInChildren<Collider2D>();
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayer);
            filter.useTriggers = false;
            Collider2D[] hits = new Collider2D[12];

            for (int iteration = 0; iteration < 24; iteration++)
            {
                bool overlapped = false;
                float requiredUp = 0f;
                float requiredDown = 0f;

                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D playerCollider = colliders[i];
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                    {
                        continue;
                    }

                    int hitCount = playerCollider.Overlap(filter, hits);
                    for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                    {
                        Collider2D hit = hits[hitIndex];
                        if (hit == null || hit.isTrigger)
                        {
                            continue;
                        }

                        // Redraw completion must obey the same collision rules as
                        // the fit validator. In particular, 14-3's blue player-only
                        // platforms are intentionally pass-through; treating one as
                        // solid here pushed a rebuilt character onto its upper side.
                        if (hit.transform.IsChildOf(targetPlayer.transform)
                            || hit.GetComponentInParent<PlayerController2D>() != null
                            || IsOneWayPassThroughSurface(hit))
                        {
                            continue;
                        }

                        overlapped = true;
                        if (hit.bounds.center.y >= playerCollider.bounds.center.y)
                        {
                            requiredDown = Mathf.Max(
                                requiredDown,
                                playerCollider.bounds.max.y - hit.bounds.min.y + groundSeparation);
                        }
                        else
                        {
                            requiredUp = Mathf.Max(
                                requiredUp,
                                hit.bounds.max.y - playerCollider.bounds.min.y + groundSeparation);
                        }
                    }
                }

                if (!overlapped)
                {
                    return;
                }

                float verticalMove;
                if (requiredUp > 0f && requiredDown > 0f)
                {
                    verticalMove = requiredUp <= requiredDown ? requiredUp : -requiredDown;
                }
                else
                {
                    verticalMove = requiredUp > 0f ? requiredUp : -requiredDown;
                }

                targetPlayer.transform.position += Vector3.up * verticalMove;
                Physics2D.SyncTransforms();
            }
        }

        private void RespawnPlayers()
        {
            ResetAllCarryState();
            for (int i = 0; i < localPlayers.Count; i++)
                RespawnPlayer(localPlayers[i], GetRespawnOffset(localPlayers[i]), localPlayers[i] == player);
        }

        private void RestoreLocalPlayerPhysicsForStageEntry()
        {
            eliminationSpectating = false;
            spectatedPlayer = null;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                RestorePlayerPhysics(localPlayers[i]);
                EnsureEliminationSpectatorRelay(localPlayers[i]);
            }
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                RestorePlayerPhysics(pair.Value, false);
                if (pair.Value != null)
                {
                    pair.Value.SetControlsEnabled(false);
                    EnsureEliminationSpectatorRelay(pair.Value);
                }
            }
        }

        private void EnsureEliminationSpectatorRelay(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null) return;
            StageEliminationSpectatorRelay relay = targetPlayer.GetComponent<StageEliminationSpectatorRelay>();
            if (relay == null) relay = targetPlayer.gameObject.AddComponent<StageEliminationSpectatorRelay>();
            relay.Configure(this, targetPlayer);
        }

        internal void FollowSurvivorAfterElimination(PlayerController2D eliminatedPlayer)
        {
            if (!stageStarted || cleared || !challengeRunStarted || !RequiresChallengeReadyRoom()) return;
            // Each machine owns one gameplay camera. A remote avatar being hidden
            // must never switch the camera of a still-living local player.
            if (eliminatedPlayer == null || eliminatedPlayer != primaryPlayer) return;
            if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            // Arena controllers deliberately disable follow so all rooms remain
            // visible. Spectating must not turn a fixed camera back on.
            if (cameraFollow == null || !cameraFollow.enabled) return;
            PlayerController2D[] candidates = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            PlayerController2D survivor = null;
            float nearest = float.PositiveInfinity;
            Vector2 origin = eliminatedPlayer != null
                ? (Vector2)eliminatedPlayer.transform.position
                : cameraFollow != null ? (Vector2)cameraFollow.transform.position : Vector2.zero;
            for (int i = 0; i < candidates.Length; i++)
            {
                PlayerController2D candidate = candidates[i];
                if (candidate == null || candidate == eliminatedPlayer || !candidate.gameObject.activeInHierarchy) continue;
                Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
                if (body != null && !body.simulated) continue;
                float distance = Vector2.SqrMagnitude((Vector2)candidate.transform.position - origin);
                if (distance < nearest)
                {
                    nearest = distance;
                    survivor = candidate;
                }
            }
            if (survivor == null) return;
            eliminationSpectating = true;
            spectatedPlayer = survivor;
            SetSpectatedPlayer(survivor, true);
            cameraFollow.enabled = true;
        }

        private void UpdateEliminationSpectatorControls()
        {
            if (!eliminationSpectating) return;
            if (primaryPlayer != null && primaryPlayer.gameObject.activeInHierarchy
                && !IsPlayerEliminated(primaryPlayer))
            {
                eliminationSpectating = false;
                spectatedPlayer = null;
                cameraFollow?.SetTarget(primaryPlayer.transform);
                return;
            }

            List<PlayerController2D> survivors = GetSpectatorCandidates();
            if (survivors.Count == 0) return;
            int currentIndex = survivors.IndexOf(spectatedPlayer);
            if (currentIndex < 0)
            {
                SetSpectatedPlayer(survivors[0], true);
                return;
            }

            LocalPlayerInputBinding localInput = primaryPlayer != null ? primaryPlayer.InputBinding : null;
            int direction = Input.GetKeyDown(KeyCode.RightArrow)
                    || localInput != null && localInput.NextPartPressed() ? 1
                : Input.GetKeyDown(KeyCode.LeftArrow)
                    || localInput != null && localInput.PreviousPartPressed() ? -1 : 0;
            if (direction == 0 || survivors.Count < 2) return;
            int nextIndex = (currentIndex + direction + survivors.Count) % survivors.Count;
            SetSpectatedPlayer(survivors[nextIndex], true);
        }

        private List<PlayerController2D> GetSpectatorCandidates()
        {
            List<PlayerController2D> survivors = new List<PlayerController2D>();
            PlayerController2D[] candidates = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                PlayerController2D candidate = candidates[i];
                if (candidate == null || candidate == primaryPlayer
                    || IsPlayerEliminated(candidate) || !candidate.gameObject.activeInHierarchy) continue;
                Rigidbody2D body = candidate.GetComponent<Rigidbody2D>();
                if (body != null && !body.simulated) continue;
                survivors.Add(candidate);
            }
            survivors.Sort((left, right) => GetSpectatorSortKey(left).CompareTo(GetSpectatorSortKey(right)));
            return survivors;
        }

        private int GetSpectatorSortKey(PlayerController2D candidate)
        {
            if (!IsOnlineInStage())
            {
                int localIndex = localPlayers.IndexOf(candidate);
                if (localIndex >= 0) return localIndex;
            }
            string id = GetOnlinePlayerId(candidate);
            int slot = PlayerColorPalette.GetLobbyPlayerSlot(onlineManager?.CurrentLobby, id);
            return slot >= 0 ? slot : 99;
        }

        private void SetSpectatedPlayer(PlayerController2D survivor, bool showNotice)
        {
            if (survivor == null) return;
            spectatedPlayer = survivor;
            cameraFollow?.SetTarget(survivor.transform);
            if (!showNotice) return;
            int localIndex = !IsOnlineInStage() ? localPlayers.IndexOf(survivor) : -1;
            string id = localIndex >= 0
                ? $"offline_local_{localIndex}"
                : GetOnlinePlayerId(survivor);
            uiManager?.ShowGameplayNotice(
                LocalizationManager.Format("spectator_following", GetPlayerDisplayName(id)),
                2.5f);
        }

        internal void RestoreCameraAfterRevival(PlayerController2D revivedPlayer)
        {
            if (revivedPlayer == null || revivedPlayer != primaryPlayer || !revivedPlayer.gameObject.activeInHierarchy) return;
            if (cameraFollow == null && Camera.main != null) cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            if (cameraFollow == null || !cameraFollow.enabled) return;
            eliminationSpectating = false;
            spectatedPlayer = null;
            cameraFollow.SetTarget(revivedPlayer.transform);
        }

        private static void RestorePlayerPhysics(
            PlayerController2D targetPlayer,
            bool restoreGameplayBodyState = true)
        {
            if (targetPlayer == null) return;
            if (!targetPlayer.gameObject.activeSelf) targetPlayer.gameObject.SetActive(true);
            targetPlayer.GetComponent<BodyBuilder>()?.RestoreAfterRespawnOrRetry();
            if (restoreGameplayBodyState)
            {
                targetPlayer.RestoreGameplayPhysicsState();
                return;
            }
            Rigidbody2D body = targetPlayer.GetComponent<Rigidbody2D>();
            if (body == null) return;
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void ResetAllCarryState()
        {
            for (int i = 0; i < localPlayers.Count; i++)
                localPlayers[i]?.GetComponent<PlayerCarryController>()?.ForceDrop();
            if (onlineCarryHeld)
            {
                EndOnlineCarry(Vector2.zero);
            }
        }

        private void ReleasePlayerFromAllCarriers(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null) return;
            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                PlayerCarryController carrier = carriers[i];
                if (carrier == null) continue;
                carrier.ReleaseIfHolding(targetPlayer.transform);
                carrier.ReleaseIfDraggingFriend(targetPlayer.transform);
            }
            if (targetPlayer == onlineCarriedLocalPlayer && onlineCarryHeld)
                EndOnlineCarry(Vector2.zero);
        }

        public void RespawnFromHazard(PlayerController2D targetPlayer)
        {
            if (!stageStarted || cleared || targetPlayer == null)
            {
                return;
            }

            if (UsesEliminationController)
            {
                if (survivalController == null)
                    survivalController = Object.FindFirstObjectByType<StageEliminationChallengeController>();
                if (survivalController != null)
                {
                    survivalController.RequestElimination(targetPlayer);
                    return;
                }
            }
            if (IsBlockBreakerChallenge && blockBreakerController != null)
            {
                blockBreakerController.RequestElimination(targetPlayer);
                return;
            }

            // Remote avatars are visual replicas. Their owning client performs the
            // respawn and the regular player-state sync sends the new position.
            if (IsOnlineInStage() && !localPlayers.Contains(targetPlayer))
            {
                return;
            }

            BeginRespawnAnimation(targetPlayer, true);
        }

        private void RespawnFallenPlayers()
        {
            for (int i = 0; i < localPlayers.Count; i++) RespawnIfFallen(localPlayers[i]);
        }

        private void RespawnIfFallen(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            // 8-3 is a controller-owned top-down magnetic board. Its players
            // are intentionally allowed below the normal side-view fall line
            // and are kept inside their assigned lane by that controller.
            if (currentStageId == "8-3")
            {
                return;
            }

            // Some special stages keep players inside a controller-owned arena
            // instead of using world gravity. Their valid movement area may extend
            // below the ordinary stage fall line, so only the stage controller's
            // explicit attack checks may eliminate them.
            if (UsesEliminationController
                && survivalController != null
                && !survivalController.UsesGlobalFallBoundary)
            {
                return;
            }

            float resetY = fallResetY;
            if (IsChallengeReadyRoomActive
                && challengeReadyRoom.TryGetAssignedRespawnPoint(targetPlayer, out Vector3 readyRoomFloor))
            {
                // The authored stage is suspended while the ready rooms are in
                // use. Its fall boundary can sit above a valid lower-row booth,
                // which previously caused an endless death/respawn loop there.
                // Only treat the player as fallen after they are clearly below
                // the generated booth floor.
                resetY = readyRoomFloor.y - 3f;
            }
            else if (stageLoader != null && stageLoader.TryGetStageFallBoundaryY(out float stageBoundaryY))
            {
                resetY = stageBoundaryY - LowestStageObjectFallMargin;
            }

            if (targetPlayer.transform.position.y >= resetY)
            {
                return;
            }

            if (UsesEliminationController && survivalController != null)
            {
                survivalController.RequestElimination(targetPlayer);
                return;
            }
            if (IsBlockBreakerChallenge && blockBreakerController != null)
            {
                blockBreakerController.RequestElimination(targetPlayer);
                return;
            }

            BeginRespawnAnimation(targetPlayer, false);
        }

        private void BeginRespawnAnimation(PlayerController2D targetPlayer, bool playHitSound)
        {
            if (targetPlayer == null || respawnAnimations.ContainsKey(targetPlayer))
            {
                return;
            }

            if (respawnGraceUntil.TryGetValue(targetPlayer, out float protectedUntil)
                && Time.unscaledTime < protectedUntil)
            {
                return;
            }

            ReleasePlayerFromAllCarriers(targetPlayer);
            targetPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();
            targetPlayer.RecoverAfterCarryRelease();
            targetPlayer.SetControlsEnabled(false);
            targetPlayer.ResetMotion();
            if (playHitSound)
            {
                GameSfx.PlayAt(SfxId.PlayerHit, targetPlayer.transform.position);
            }

            Rigidbody2D body = targetPlayer.GetComponent<Rigidbody2D>();
            RespawnAnimationState state = new RespawnAnimationState
            {
                OriginalScale = targetPlayer.transform.localScale,
                DeathPosition = targetPlayer.transform.position,
                Body = body,
                // Hazard respawns only run for locally owned gameplay bodies.
                // Always restore simulation even if a carry/elimination transition
                // happened to disable the Rigidbody in the same frame.
                BodyWasSimulated = true,
                RespawnAt = Time.unscaledTime + (currentStageId == "13-1"
                    ? 5f
                    : RespawnCollapseDuration + RespawnPauseDuration),
                RecoveryDeadline = Time.unscaledTime + (currentStageId == "13-1" ? 7f : 2.5f)
            };
            respawnAnimations[targetPlayer] = state;
            state.Routine = StartCoroutine(PlayRespawnAnimation(targetPlayer, state));
        }

        private IEnumerator PlayRespawnAnimation(PlayerController2D targetPlayer, RespawnAnimationState state)
        {
            Color effectColor = GetPlayerEffectColor(targetPlayer);
            CreateRespawnBurst(targetPlayer.transform.position, effectColor, false);
            GameSfx.PlayAt(SfxId.PlayerDeath, targetPlayer.transform.position);

            if (state.Body != null)
            {
                state.Body.simulated = false;
            }

            float elapsed = 0f;
            while (elapsed < RespawnCollapseDuration && targetPlayer != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / RespawnCollapseDuration);
                float squash = Mathf.Sin(progress * Mathf.PI) * 0.18f;
                float remaining = 1f - progress;
                targetPlayer.transform.localScale = Vector3.Scale(
                    state.OriginalScale,
                    new Vector3((remaining + squash), Mathf.Max(0.03f, remaining - squash * 0.45f), 1f));
                yield return null;
            }

            if (targetPlayer == null)
            {
                respawnAnimations.Remove(targetPlayer);
                yield break;
            }

            targetPlayer.transform.localScale = Vector3.Scale(state.OriginalScale, new Vector3(0.03f, 0.03f, 1f));
            while (Time.unscaledTime < state.RespawnAt)
                yield return null;

            if (targetPlayer == null || !stageStarted || cleared)
            {
                RestoreRespawnAnimationState(targetPlayer, state);
                respawnAnimations.Remove(targetPlayer);
                yield break;
            }

            // Keep physics disabled through the teleport. Re-enabling before the
            // position change can make continuous collision/interpolation touch
            // every object between the death point and the spawn point.
            // Resolve the final-size body's bottom before the appear animation.
            // Aligning while the body is scaled to 3% makes a large drawing grow
            // downward through the floor during the animation.
            targetPlayer.transform.localScale = state.OriginalScale;
            Vector3 respawnDestination = targetPlayer.transform.position;
            Vector3 respawnOffset;
            bool checkpointResolved = false;
            Vector3 visitedRedraw;
            if (IsChallengeReadyRoomActive
                && challengeReadyRoom.TryGetAssignedRespawnPosition(targetPlayer, out Vector3 assignedRoomRespawn))
            {
                respawnDestination = assignedRoomRespawn;
                checkpointResolved = true;
            }
            else if (!RequiresChallengeReadyRoom()
                && StageRedrawZone.TryGetLastVisitedRespawnPoint(
                    targetPlayer, state.DeathPosition, out visitedRedraw))
            {
                checkpointResolved = TryPlaceAtSafeSpawn(
                    targetPlayer, visitedRedraw, out respawnDestination);
            }
            if (checkpointResolved)
            {
                respawnOffset = spawnPoint != null
                    ? respawnDestination - spawnPoint.position
                    : Vector3.zero;
            }
            else if (spawnPoint != null
                && assignedPlayerStartPositions.TryGetValue(targetPlayer, out Vector3 assignedRespawn))
            {
                // Initial spawn validation already proved this exact location can
                // support the player's current drawing. Recomputing from lobby
                // count on death can move P1 back toward a short platform edge.
                respawnDestination = assignedRespawn;
                respawnOffset = assignedRespawn - spawnPoint.position;
            }
            else
            {
                respawnOffset = GetRespawnOffset(targetPlayer);
                respawnDestination = spawnPoint != null
                    ? spawnPoint.position + respawnOffset
                    : targetPlayer.transform.position;
            }
            if (!checkpointResolved && spawnPoint != null
                && TryPlaceAtSafeSpawn(targetPlayer, respawnDestination, out Vector3 safeRespawn))
            {
                respawnDestination = safeRespawn;
                respawnOffset = safeRespawn - spawnPoint.position;
            }
            RespawnPlayer(
                targetPlayer,
                respawnOffset,
                false,
                resolveGroundOverlap: false,
                ensurePhysicsSimulation: false);
            targetPlayer.transform.localScale = Vector3.Scale(
                state.OriginalScale,
                new Vector3(0.03f, 0.03f, 1f));
            Physics2D.SyncTransforms();
            CreateRespawnBurst(targetPlayer.transform.position, effectColor, true);

            elapsed = 0f;
            while (elapsed < RespawnAppearDuration && targetPlayer != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / RespawnAppearDuration);
                float bounce = progress < 0.72f
                    ? Mathf.Lerp(0.18f, 1.12f, progress / 0.72f)
                    : Mathf.Lerp(1.12f, 1f, (progress - 0.72f) / 0.28f);
                targetPlayer.transform.localScale = Vector3.Scale(state.OriginalScale, new Vector3(bounce, bounce, 1f));
                yield return null;
            }

            if (targetPlayer != null)
            {
                targetPlayer.transform.localScale = state.OriginalScale;
                targetPlayer.GetComponent<BodyBuilder>()?.RestoreAfterRespawnOrRetry();
                if (IsChallengeReadyRoomActive)
                {
                    // Network/body rebuild callbacks can move the transform while
                    // the body is hidden. Reassert the assigned-room destination
                    // immediately before physics resumes.
                    TeleportPlayerWithoutPhysics(targetPlayer, respawnDestination);
                }
                Physics2D.SyncTransforms();
                AlignPlayerBottomToGround(targetPlayer, respawnDestination);
                LiftPlayerOutOfGround(targetPlayer);
                if (state.Body != null)
                {
                    state.Body.simulated = state.BodyWasSimulated;
                }
                Physics2D.SyncTransforms();
                targetPlayer.ResetMotion();
                bool independentlyBound = HasIndependentLocalInput(targetPlayer);
                targetPlayer.SetControlsEnabled((independentlyBound || targetPlayer == player)
                    && stageStarted && !drawing && !cleared && !stageEditing);
                respawnGraceUntil[targetPlayer] = Time.unscaledTime + RespawnGraceDuration;
                if (!IsChallengeReadyRoomActive
                    && targetPlayer == primaryPlayer && cameraFollow != null && cameraFollow.enabled)
                {
                    cameraFollow.SetTarget(targetPlayer.transform);
                }
            }

            respawnAnimations.Remove(targetPlayer);
        }

        private void RecoverStalledRespawns()
        {
            if (respawnAnimations.Count == 0) return;
            List<KeyValuePair<PlayerController2D, RespawnAnimationState>> stalled = null;
            foreach (KeyValuePair<PlayerController2D, RespawnAnimationState> pair in respawnAnimations)
            {
                if (pair.Key == null || Time.unscaledTime > pair.Value.RecoveryDeadline)
                {
                    stalled ??= new List<KeyValuePair<PlayerController2D, RespawnAnimationState>>();
                    stalled.Add(pair);
                }
            }
            if (stalled == null) return;

            for (int i = 0; i < stalled.Count; i++)
            {
                PlayerController2D targetPlayer = stalled[i].Key;
                RespawnAnimationState state = stalled[i].Value;
                if (state.Routine != null) StopCoroutine(state.Routine);
                respawnAnimations.Remove(targetPlayer);
                if (targetPlayer == null) continue;

                targetPlayer.transform.localScale = state.OriginalScale;
                RestorePlayerPhysics(targetPlayer);
                Vector3 safePosition = targetPlayer.transform.position;
                bool readyRoomResolved = IsChallengeReadyRoomActive
                    && challengeReadyRoom.TryGetAssignedRespawnPosition(targetPlayer, out safePosition);
                if (!readyRoomResolved)
                {
                    Vector3 preferred = assignedPlayerStartPositions.TryGetValue(targetPlayer, out Vector3 assigned)
                        ? assigned
                        : spawnPoint != null ? spawnPoint.position + GetRespawnOffset(targetPlayer) : targetPlayer.transform.position;
                    if (!TryPlaceAtSafeSpawn(targetPlayer, preferred, out safePosition))
                        safePosition = preferred;
                }
                TeleportPlayerWithoutPhysics(targetPlayer, safePosition);
                AlignPlayerBottomToGround(targetPlayer, safePosition);
                LiftPlayerOutOfGround(targetPlayer);
                targetPlayer.ResetMotion();
                bool independentlyBound = HasIndependentLocalInput(targetPlayer);
                targetPlayer.SetControlsEnabled((independentlyBound || targetPlayer == player)
                    && stageStarted && !drawing && !cleared && !stageEditing
                    && !optionOverlayActive && !titleTextInputActive);
                respawnGraceUntil[targetPlayer] = Time.unscaledTime + RespawnGraceDuration;
            }
        }

        internal void RequestSlimeBoxBreak(Transform target, Vector2 hitPoint)
        {
            StageEditorObject marker = target != null
                ? target.GetComponentInParent<StageEditorObject>()
                : null;
            if (marker == null || string.IsNullOrEmpty(marker.objectId)
                || !IsSlimeBreakableBox(marker.type)) return;

            if (!IsOnlineInStage())
            {
                ApplySlimeBoxBreak(marker.objectId, hitPoint);
                return;
            }

            SlimeBoxBreakMessage message = new SlimeBoxBreakMessage
            {
                StageId = currentStageId,
                ObjectId = marker.objectId,
                HitPoint = hitPoint
            };
            if (IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                if (ApplySlimeBoxBreak(message.ObjectId, hitPoint))
                    BroadcastSlimeBoxBreak(message.ObjectId, hitPoint);
                return;
            }

            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindSlimeBoxBreakRequest,
                Json = JsonUtility.ToJson(message)
            });
        }

        private bool ApplySlimeBoxBreak(string objectId, Vector2 hitPoint)
        {
            if (string.IsNullOrEmpty(objectId) || slimeBrokenObjectIds.Contains(objectId)) return false;
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            StageEditorObject target = null;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].objectId == objectId
                    && IsSlimeBreakableBox(objects[i].type))
                {
                    target = objects[i];
                    break;
                }
            }
            if (target == null) return false;
            if (hitPoint == Vector2.zero) hitPoint = target.transform.position;

            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
                carriers[i]?.ReleaseIfHolding(target.transform);
            Object.FindFirstObjectByType<StageGimmickSyncManager>()
                ?.UnregisterRuntimeObject(target.transform);
            slimeBrokenObjectIds.Add(objectId);
            SlimeBoxBreakFeedback.Play(target.transform, hitPoint, target.type);
            // UI/game cameras sit well away from the 2D world on Z, so a spatial
            // source can become almost inaudible. Box breaking is important
            // feedback and should be heard consistently by the local player.
            GameSfx.Play(SfxId.CrateBreak, 1.5f);
            target.gameObject.SetActive(false);
            return true;
        }

        private bool CanOnlinePlayerCollect(string playerId, string objectId)
        {
            PlayerController2D collector = GetOnlinePlayerController(playerId);
            if (collector == null || string.IsNullOrEmpty(objectId)) return false;
            StageCollectible[] collectibles = Object.FindObjectsByType<StageCollectible>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < collectibles.Length; i++)
            {
                StageCollectible collectible = collectibles[i];
                if (collectible == null || collectible.ObjectId != objectId) continue;
                return GetClosestColliderDistance(collector.transform, collectible.transform)
                    <= GetLagCompensatedInteractionDistance(collector, 1.75f);
            }
            return false;
        }

        private bool CanOnlinePlayerEnterGoal(string playerId)
        {
            PlayerController2D candidate = GetOnlinePlayerController(playerId);
            if (candidate == null) return false;
            Goal[] goals = Object.FindObjectsByType<Goal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < goals.Length; i++)
                if (goals[i] != null
                    && GetClosestColliderDistance(candidate.transform, goals[i].transform)
                        <= GetLagCompensatedInteractionDistance(candidate, 2.5f))
                    return true;
            return false;
        }

        private bool CanOnlinePlayerBreakSlimeBox(string playerId, string objectId)
        {
            PlayerController2D attacker = GetOnlinePlayerController(playerId);
            if (attacker == null || attacker.CurrentSpecies != DrawManager.Species.Slime
                || string.IsNullOrEmpty(objectId)) return false;
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject target = objects[i];
                if (target == null || target.objectId != objectId || !IsSlimeBreakableBox(target.type)) continue;
                return GetClosestColliderDistance(attacker.transform, target.transform)
                    <= GetLagCompensatedInteractionDistance(attacker, 6f);
            }
            return false;
        }

        private static float GetClosestColliderDistance(Transform first, Transform second)
        {
            if (first == null || second == null) return float.PositiveInfinity;
            Collider2D[] firstColliders = first.GetComponentsInChildren<Collider2D>(false);
            Collider2D[] secondColliders = second.GetComponentsInChildren<Collider2D>(false);
            float closest = Vector2.Distance(first.position, second.position);
            for (int i = 0; i < firstColliders.Length; i++)
            {
                Collider2D a = firstColliders[i];
                if (a == null || !a.enabled || a.isTrigger) continue;
                for (int j = 0; j < secondColliders.Length; j++)
                {
                    Collider2D b = secondColliders[j];
                    if (b == null || !b.enabled || b.isTrigger) continue;
                    closest = Mathf.Min(closest, Mathf.Max(0f, a.Distance(b).distance));
                }
            }
            return closest;
        }

        private static float GetLagCompensatedInteractionDistance(
            PlayerController2D player,
            float baseDistance)
        {
            Rigidbody2D body = player != null ? player.GetComponent<Rigidbody2D>() : null;
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            // Player snapshots arrive at 30 Hz and are predicted on the host.
            // Keep a bounded reserve for RTT/jitter without allowing requests
            // from the other side of a room.
            return baseDistance + Mathf.Min(2.5f, speed * 0.22f);
        }

        private static bool IsSlimeBreakableBox(StageObjectType type)
        {
            return type == StageObjectType.WoodBox
                || type == StageObjectType.IronBox
                || type == StageObjectType.TriangleBox
                || type == StageObjectType.FloatingBox
                || type == StageObjectType.RubberBox;
        }

        private void BroadcastSlimeBoxBreak(string objectId, Vector2 hitPoint)
        {
            if (!IsOnlineInStage() || onlineManager == null
                || !IsLocalOnlineHost(onlineManager.CurrentLobby)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindSlimeBoxBreakState,
                Json = JsonUtility.ToJson(new SlimeBoxBreakMessage
                {
                    StageId = currentStageId,
                    ObjectId = objectId,
                    HitPoint = hitPoint
                })
            });
        }

        private void BroadcastSlimeBreakStatesIfNeeded()
        {
            if (slimeBrokenObjectIds.Count == 0 || Time.unscaledTime < nextSlimeBreakSyncAt
                || !IsOnlineInStage() || onlineManager == null
                || !IsLocalOnlineHost(onlineManager.CurrentLobby)) return;
            nextSlimeBreakSyncAt = Time.unscaledTime + 1.5f;
            foreach (string objectId in slimeBrokenObjectIds)
                BroadcastSlimeBoxBreak(objectId, Vector2.zero);
        }

        internal void BroadcastLocalAbilityEffect(
            PlayerController2D source,
            string effect,
            Vector2 direction = default)
        {
            if (source == null || !localPlayers.Contains(source) || !IsOnlineInStage()
                || onlineManager == null || string.IsNullOrEmpty(effect)) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindPlayerAbilityEffect,
                Json = JsonUtility.ToJson(new PlayerAbilityEffectMessage
                {
                    Effect = effect,
                    Direction = direction,
                    SourcePlayerId = GetOnlinePlayerId(source)
                })
            });
        }

        private void CancelRespawnAnimations()
        {
            if (respawnAnimations.Count == 0)
            {
                respawnGraceUntil.Clear();
                return;
            }

            List<KeyValuePair<PlayerController2D, RespawnAnimationState>> active =
                new List<KeyValuePair<PlayerController2D, RespawnAnimationState>>(respawnAnimations);
            respawnAnimations.Clear();
            respawnGraceUntil.Clear();
            for (int i = 0; i < active.Count; i++)
            {
                RespawnAnimationState state = active[i].Value;
                if (state.Routine != null)
                {
                    StopCoroutine(state.Routine);
                }
                RestoreRespawnAnimationState(active[i].Key, state);
            }
        }

        private static void RestoreRespawnAnimationState(
            PlayerController2D targetPlayer,
            RespawnAnimationState state)
        {
            if (targetPlayer != null)
            {
                targetPlayer.transform.localScale = state.OriginalScale;
            }
            if (state.Body != null)
            {
                state.Body.simulated = state.BodyWasSimulated;
            }
        }

        private Color GetPlayerEffectColor(PlayerController2D targetPlayer)
        {
            BodyBuilder builder = targetPlayer != null ? targetPlayer.GetComponent<BodyBuilder>() : null;
            return builder != null ? builder.PlayerColor : new Color(0.2f, 0.55f, 1f, 1f);
        }

        internal void PlayOnlinePlayerLifeEffect(string playerId, Vector3 position, bool appearing)
        {
            PlayerController2D targetPlayer = GetOnlinePlayerController(playerId);
            Color effectColor = GetPlayerEffectColor(targetPlayer);
            CreateRespawnBurst(position, effectColor, appearing);
            GameSfx.PlayAt(appearing ? SfxId.PlayerRespawn : SfxId.PlayerDeath, position, 0.8f);
        }

        internal bool NotifyPlayerEliminated(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || !stageStarted || cleared || !challengeRunStarted
                || (!UsesEliminationController && !IsBlockBreakerChallenge))
            {
                return false;
            }

            CreateRespawnBurst(targetPlayer.transform.position, GetPlayerEffectColor(targetPlayer), false);
            eliminatedPlayers.Add(targetPlayer);
            FollowSurvivorAfterElimination(targetPlayer);
            return true;
        }

        internal void NotifyPlayerRevived(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            eliminatedPlayers.Remove(targetPlayer);
            if (!stageStarted || cleared || titleMode || stageEditing)
            {
                return;
            }
            CreateRespawnBurst(targetPlayer.transform.position, GetPlayerEffectColor(targetPlayer), true);
        }

        private void CreateRespawnBurst(Vector3 position, Color color, bool appearing)
        {
            GameObject burst = new GameObject(appearing ? "Revive Doodle Burst" : "Defeat Doodle Burst");
            burst.transform.position = position;
            const int rayCount = 10;
            LineRenderer[] lines = new LineRenderer[rayCount + 1];

            for (int i = 0; i < rayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rayCount;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                LineRenderer line = CreateRespawnEffectLine(burst.transform, "Ray " + i, color, false);
                line.positionCount = 3;
                line.SetPosition(0, direction * 0.20f);
                line.SetPosition(1, direction * 0.43f + new Vector2(-direction.y, direction.x) * 0.035f);
                line.SetPosition(2, direction * 0.66f);
                lines[i] = line;
            }

            LineRenderer ring = CreateRespawnEffectLine(burst.transform, "Ring", color, true);
            ring.positionCount = 25;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / (ring.positionCount - 1);
                float radius = 0.24f + Mathf.Sin(i * 2.7f) * 0.018f;
                ring.SetPosition(i, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            lines[rayCount] = ring;
            StartCoroutine(AnimateRespawnBurst(burst, lines, color, appearing));
        }

        private LineRenderer CreateRespawnEffectLine(Transform parent, string lineName, Color color, bool loop)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = loop;
            line.widthMultiplier = 0.055f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 220;
            if (respawnBurstMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    respawnBurstMaterial = new Material(shader) { name = "Respawn Doodle Material" };
                }
            }
            line.sharedMaterial = respawnBurstMaterial;
            return line;
        }

        private IEnumerator AnimateRespawnBurst(
            GameObject burst,
            LineRenderer[] lines,
            Color color,
            bool appearing)
        {
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration && burst != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                float scale = appearing
                    ? Mathf.Lerp(1.65f, 0.48f, eased)
                    : Mathf.Lerp(0.45f, 1.45f, eased);
                burst.transform.localScale = Vector3.one * scale;
                Color faded = color;
                faded.a *= appearing
                    ? Mathf.Sin(progress * Mathf.PI)
                    : 1f - progress;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] != null)
                    {
                        lines[i].startColor = faded;
                        lines[i].endColor = faded;
                    }
                }
                yield return null;
            }

            if (burst != null)
            {
                Destroy(burst);
            }
        }

        private void RespawnPlayer(
            PlayerController2D targetPlayer,
            Vector3 offset,
            bool enableControls,
            bool resolveGroundOverlap = true,
            bool ensurePhysicsSimulation = true)
        {
            if (targetPlayer == null || spawnPoint == null)
            {
                return;
            }

            // Elimination stages hide defeated players. Reactivate before the
            // physics-disabled teleport so an old controller cannot leave the
            // player disabled across a full stage reload.
            if (!targetPlayer.gameObject.activeSelf) targetPlayer.gameObject.SetActive(true);
            // A no-respawn challenge deliberately disables simulation when a
            // player is eliminated. Full stage retry reuses the same player
            // object, so physics must be restored before the safe teleport or
            // the disabled state leaks into every subsequently selected stage.
            Rigidbody2D respawnBody = targetPlayer.GetComponent<Rigidbody2D>();
            if (respawnBody != null && ensurePhysicsSimulation) respawnBody.simulated = true;
            Vector3 destination = spawnPoint.position + offset;
            TeleportPlayerWithoutPhysics(targetPlayer, destination);
            AlignPlayerBottomToGround(targetPlayer, destination);
            targetPlayer.ResetMotion();
            targetPlayer.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared
                && !stageEditing && !optionOverlayActive);
            if (resolveGroundOverlap)
            {
                LiftPlayerOutOfGround(targetPlayer);
            }
            if (stageStarted)
            {
                GameSfx.PlayAt(SfxId.PlayerRespawn, targetPlayer.transform.position);
            }
        }

        private void AlignPlayerBottomToGround(
            PlayerController2D targetPlayer,
            Vector3 spawnPosition,
            bool includeOneWay = true)
        {
            if (targetPlayer == null) return;
            Physics2D.SyncTransforms();
            if (!TryFindGroundSurfaceBelow(spawnPosition, out float surfaceY, includeOneWay)
                || !TryGetPlayerSolidBounds(targetPlayer, out Bounds bodyBounds))
            {
                return;
            }

            float correction = surfaceY + groundSeparation - bodyBounds.min.y;
            if (Mathf.Abs(correction) <= 0.0001f) return;
            Vector3 corrected = targetPlayer.transform.position;
            corrected.y += correction;
            TeleportPlayerWithoutPhysics(targetPlayer, corrected);
        }

        private static void TeleportPlayerWithoutPhysics(PlayerController2D targetPlayer, Vector3 destination)
        {
            if (targetPlayer == null)
            {
                return;
            }

            Rigidbody2D body = targetPlayer.GetComponent<Rigidbody2D>();
            bool restoreSimulation = body != null && body.simulated;
            if (body != null)
            {
                body.simulated = false;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            targetPlayer.transform.position = destination;
            if (body != null)
            {
                body.position = destination;
            }
            Physics2D.SyncTransforms();

            if (body != null && restoreSimulation)
            {
                body.simulated = true;
                Physics2D.SyncTransforms();
            }
        }

        private Vector3 GetRespawnOffset(PlayerController2D targetPlayer)
        {
            int playerCount;
            int playerSlot;
            if (targetPlayer != null && IsOnlineInStage())
            {
                string targetId = GetOnlinePlayerId(targetPlayer);
                playerCount = GetLobbyPlayerCount();
                playerSlot = GetLobbyPlayerSlot(targetId);
            }
            else
            {
                playerCount = localPlayers.Count;
                playerSlot = targetPlayer != null ? localPlayers.IndexOf(targetPlayer) : 0;
            }

            if (playerCount <= 1 || playerSlot < 0)
            {
                return Vector3.zero;
            }

            if (currentStageId == "12-2")
            {
                // The authored center spawn is directly above a jump pad. Use
                // the safe gaps between pads and keep every online owner apart.
                float[] safeX = { -3f, 3f, -9f, 9f };
                return new Vector3(safeX[Mathf.Clamp(playerSlot, 0, safeX.Length - 1)], 0f, 0f);
            }

            if (currentStageId == "9-3")
            {
                // 9-3 intentionally allows very large slime drawings. Keep the
                // owners far enough apart when leaving the ready room so four
                // large bodies do not overlap and trigger a redraw immediately.
                // The 9-3 gameplay arena is authored at double spatial scale;
                // spread its unchanged-size player drawings by the same ratio.
                const float largeBodySpacing = 16f;
                float firstLargeBodyOffset = -(playerCount - 1) * largeBodySpacing * 0.5f;
                return new Vector3(firstLargeBodyOffset + playerSlot * largeBodySpacing, 0f, 0f);
            }

            const float preferredSpacing = 2.2f;
            float spacing = preferredSpacing;
            float firstOffset = -(playerCount - 1) * preferredSpacing * 0.5f;
            if (spawnPoint != null
                && TryGetSpawnSurfaceHorizontalBounds(spawnPoint.position, out float surfaceLeft, out float surfaceRight))
            {
                // A number of stages deliberately put the marker close to the
                // left edge of a small starting platform. Fit the whole group into
                // that platform instead of blindly spreading around the marker.
                // Use the actual drawing bounds as well: at four players the first
                // slot in 2-1 is close to the wall/platform seam, and a body wider
                // than the old fixed margin can miss the floor and respawn forever.
                const float edgeMargin = 0.75f;
                float leftInset = edgeMargin;
                float rightInset = edgeMargin;
                if (TryGetPlayerSolidBounds(targetPlayer, out Bounds playerBounds))
                {
                    float boundsLeftFromRoot = playerBounds.min.x - targetPlayer.transform.position.x;
                    float boundsRightFromRoot = playerBounds.max.x - targetPlayer.transform.position.x;
                    leftInset = Mathf.Max(edgeMargin, -boundsLeftFromRoot + groundSeparation);
                    rightInset = Mathf.Max(edgeMargin, boundsRightFromRoot + groundSeparation);
                }
                float allowedMin = surfaceLeft + leftInset - spawnPoint.position.x;
                float allowedMax = surfaceRight - rightInset - spawnPoint.position.x;
                float availableWidth = Mathf.Max(0f, allowedMax - allowedMin);
                spacing = Mathf.Min(preferredSpacing, availableWidth / (playerCount - 1));
                float groupWidth = spacing * (playerCount - 1);
                float centeredStart = -groupWidth * 0.5f;
                firstOffset = Mathf.Clamp(centeredStart, allowedMin, allowedMax - groupWidth);
            }

            return new Vector3(firstOffset + playerSlot * spacing, 0f, 0f);
        }

        private bool TryGetSpawnSurfaceHorizontalBounds(
            Vector3 spawnPosition,
            out float surfaceLeft,
            out float surfaceRight)
        {
            surfaceLeft = 0f;
            surfaceRight = 0f;
            Vector2 origin = new Vector2(spawnPosition.x, spawnPosition.y + 0.5f);
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, 8f, groundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger
                    || collider.GetComponentInParent<PlayerController2D>() != null
                    || hits[i].normal.y < 0.35f)
                {
                    continue;
                }

                surfaceLeft = collider.bounds.min.x;
                surfaceRight = collider.bounds.max.x;
                float surfaceY = collider.bounds.max.y;

                // Procedural arenas such as 11-2 build one visually continuous
                // start floor from many neighboring colliders. Treat the whole
                // connected row as one surface; using only the collider directly
                // below the marker would give each of four players less than one
                // floor tile and incorrectly force ordinary bodies into redraw.
                Collider2D[] all = Object.FindObjectsByType<Collider2D>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                bool expanded;
                do
                {
                    expanded = false;
                    for (int candidateIndex = 0; candidateIndex < all.Length; candidateIndex++)
                    {
                        Collider2D candidate = all[candidateIndex];
                        if (candidate == null || candidate == collider || candidate.isTrigger
                            || candidate.GetComponentInParent<PlayerController2D>() != null
                            || (groundLayer.value & 1 << candidate.gameObject.layer) == 0
                            || !IsHorizontalSpawnSupport(candidate)
                            || Mathf.Abs(candidate.bounds.max.y - surfaceY) > 0.18f)
                        {
                            continue;
                        }

                        const float seamTolerance = 0.12f;
                        bool touches = candidate.bounds.max.x >= surfaceLeft - seamTolerance
                            && candidate.bounds.min.x <= surfaceRight + seamTolerance;
                        if (!touches) continue;
                        float nextLeft = Mathf.Min(surfaceLeft, candidate.bounds.min.x);
                        float nextRight = Mathf.Max(surfaceRight, candidate.bounds.max.x);
                        if (nextLeft < surfaceLeft - 0.001f || nextRight > surfaceRight + 0.001f)
                        {
                            surfaceLeft = nextLeft;
                            surfaceRight = nextRight;
                            expanded = true;
                        }
                    }
                }
                while (expanded);
                return surfaceRight > surfaceLeft;
            }
            return false;
        }

        private static bool IsHorizontalSpawnSupport(Collider2D collider)
        {
            if (collider == null) return false;
            if (collider.CompareTag("Ground")) return true;
            StageEditorObject marker = collider.GetComponentInParent<StageEditorObject>();
            if (marker == null) return false;
            switch (marker.type)
            {
                case StageObjectType.Platform:
                case StageObjectType.HalfPlatform:
                case StageObjectType.OneWayPlatform:
                case StageObjectType.IceFloor:
                case StageObjectType.CloudPlatform:
                case StageObjectType.BreakableFloor:
                case StageObjectType.FallingFloor:
                case StageObjectType.MovingPlatform:
                case StageObjectType.MovingOneWayPlatform:
                case StageObjectType.EscortPlayerOnlyFloor:
                case StageObjectType.Seesaw:
                    return true;
                default:
                    return false;
            }
        }

        private void SetActivePlayer(PlayerController2D nextPlayer, bool enableControls)
        {
            if (nextPlayer == null)
            {
                return;
            }

            if (player != null && player != nextPlayer)
            {
                SaveDrawingState(player);
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                bool previousIndependentlyBound = HasIndependentLocalInput(player);
                player.SetControlsEnabled(previousIndependentlyBound && ShouldEnableLocalGameplayControls);
            }

            player = nextPlayer;
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            player.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared
                && !stageEditing && !optionOverlayActive);
        }

        private void SetAllPlayerControls(bool enabled)
        {
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (local == null) continue;
                bool independentlyBound = HasIndependentLocalInput(local);
                bool independentlyDrawing = localCoop != null && localCoop.IsPlayerDrawing(local);
                local.SetControlsEnabled(enabled && !independentlyDrawing
                    && (independentlyBound || local == player));
            }
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                if (pair.Value != null && pair.Value != primaryPlayer)
                {
                    pair.Value.SetControlsEnabled(false);
                }
            }
        }

        private void ConfigureActivePlayerTargets()
        {
            if (player == null)
            {
                return;
            }

            cameraFollow?.SetTarget(player.transform);
            drawManager?.SetBuildTarget(player.GetComponent<BodyBuilder>(), player.GetComponent<PlayerAbilityController>());
            RefreshControlledPlayerMarkers();
        }

        private void RefreshControlledPlayerMarkers()
        {
            bool showMarker = localPlayers.Count > 1 || IsOnlineInStage();
            for (int i = 0; i < localPlayers.Count; i++)
                SetControlledPlayerMarker(
                    localPlayers[i],
                    showMarker && localPlayers[i] == player,
                    UsesStageManagedPlayerMovement && IsGameplayActive);
        }

        private static void SetControlledPlayerMarker(
            PlayerController2D targetPlayer,
            bool controlled,
            bool forceVisible = false)
        {
            if (targetPlayer == null)
            {
                return;
            }

            ControlledPlayerMarker marker = targetPlayer.GetComponent<ControlledPlayerMarker>();
            if (marker == null)
            {
                marker = targetPlayer.gameObject.AddComponent<ControlledPlayerMarker>();
            }

            marker.SetControlled(controlled, forceVisible);
        }

        public void ApplyDefaultPlayerColors()
        {
            for (int i = 0; i < localPlayers.Count; i++) SetPlayerColor(localPlayers[i], i);
        }

        public void ApplyOnlinePlayerColors(OnlineLobbyInfo lobby, string localPlayerId, string remotePlayerId)
        {
            for (int i = 0; i < localPlayers.Count; i++)
            {
                string gamePlayerId = OnlinePlayerIdentity.Compose(localPlayerId, i);
                int localIndex = PlayerColorPalette.GetLobbyColorIndex(lobby, gamePlayerId, i);
                SetPlayerColor(localPlayers[i], localIndex);
            }
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                int index = PlayerColorPalette.GetLobbyColorIndex(lobby, pair.Key, 1);
                SetPlayerColor(pair.Value, index);
            }
        }

        public void ApplyOnlinePlayerColor(string playerId, int playerSlot)
        {
            if (string.IsNullOrEmpty(playerId) || playerSlot < 0 || playerSlot > 3)
            {
                return;
            }
            SetPlayerColor(GetOnlinePlayerController(playerId), playerSlot);
        }

        private static void SetPlayerColor(PlayerController2D targetPlayer, int playerIndex)
        {
            if (targetPlayer == null)
            {
                return;
            }

            BodyBuilder bodyBuilder = targetPlayer.GetComponent<BodyBuilder>();
            if (bodyBuilder != null)
            {
                bodyBuilder.SetPlayerColor(PlayerColorPalette.GetColor(playerIndex));
            }
        }

        private void SaveDrawingState(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || drawManager == null)
            {
                return;
            }

            drawingStates[targetPlayer] = drawManager.CreateState();
        }

        internal void RecordActivePlayerDrawingState()
        {
            SaveDrawingState(player);
            drawManager?.RefreshUniqueSpeciesAvailability();
        }

        public bool CanConfirmSpeciesForActivePlayer(DrawManager.Species species)
        {
            return CanConfirmSpeciesForPlayer(player, species);
        }

        public bool CanConfirmSpeciesForPlayer(PlayerController2D targetPlayer, DrawManager.Species species)
        {
            if (!StageSpeciesRules.IsAllowed(
                StageSpeciesRules.GetAllowedForStage(currentStageId), species))
            {
                return false;
            }
            if (!RequiresUniquePlayerSpecies)
            {
                return true;
            }

            List<PlayerController2D> otherPlayers = new List<PlayerController2D>();
            for (int i = 0; i < localPlayers.Count; i++) AddPlayerOnce(otherPlayers, localPlayers[i]);
            foreach (KeyValuePair<string, PlayerController2D> remote in onlineRemotePlayers)
            {
                AddPlayerOnce(otherPlayers, remote.Value);
            }

            for (int i = 0; i < otherPlayers.Count; i++)
            {
                PlayerController2D other = otherPlayers[i];
                if (other == null || other == targetPlayer)
                {
                    continue;
                }

                if (TryGetConfirmedSpecies(other, out DrawManager.Species usedSpecies)
                    && usedSpecies == species)
                {
                    return false;
                }
            }

            return true;
        }

        public void RecordPlayerDrawingState(PlayerController2D targetPlayer, DrawManager.DrawingState state)
        {
            if (targetPlayer == null || state == null) return;
            drawingStates[targetPlayer] = CloneDrawingState(state);
            drawManager?.RefreshUniqueSpeciesAvailability();
        }

        public DrawManager.DrawingState GetPlayerDrawingStateSnapshot(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null) return null;
            return drawingStates.TryGetValue(targetPlayer, out DrawManager.DrawingState state)
                && state != null
                ? CloneDrawingState(state)
                : null;
        }

        public bool PromptSpeciesSwapRequest(DrawManager.Species requestedSpecies)
        {
            return PromptSpeciesSwapRequest(requestedSpecies, player);
        }

        public bool PromptSpeciesSwapRequest(
            DrawManager.Species requestedSpecies,
            PlayerController2D requester)
        {
            if (!RequiresUniquePlayerSpecies || requester == null)
            {
                return false;
            }

            if (pendingOutgoingSpeciesSwap != null)
            {
                drawManager?.ShowSpeciesSwapPending();
                uiManager?.ShowSpeciesSwapPending(
                    GetPlayerDisplayName(pendingOutgoingSpeciesSwap.TargetId),
                    (DrawManager.Species)pendingOutgoingSpeciesSwap.TargetSpecies,
                    (DrawManager.Species)pendingOutgoingSpeciesSwap.RequesterSpecies);
                return true;
            }

            if (pendingIncomingSpeciesSwap != null)
            {
                return false;
            }

            PlayerController2D target = FindPlayerUsingSpecies(requestedSpecies, requester);
            if (target == null || !TryGetConfirmedSpecies(requester, out DrawManager.Species currentSpecies))
            {
                return false;
            }

            string targetId = GetSwapPlayerId(target);
            if (string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            pendingSpeciesSwapPrompt = requestedSpecies;
            pendingSpeciesSwapRequester = requester;
            uiManager?.ShowOutgoingSpeciesSwapConfirm(
                GetPlayerDisplayName(targetId),
                requestedSpecies,
                currentSpecies);
            return true;
        }

        public void ConfirmOutgoingSpeciesSwapRequest()
        {
            if (!pendingSpeciesSwapPrompt.HasValue)
            {
                return;
            }

            DrawManager.Species requestedSpecies = pendingSpeciesSwapPrompt.Value;
            PlayerController2D requester = pendingSpeciesSwapRequester;
            pendingSpeciesSwapPrompt = null;
            pendingSpeciesSwapRequester = null;
            uiManager?.HideSpeciesSwapConfirm();
            if (!RequestSpeciesSwap(requestedSpecies, requester))
            {
                drawManager?.ShowSpeciesSwapUnavailable();
            }
        }

        public void CancelOutgoingSpeciesSwapRequest()
        {
            pendingSpeciesSwapPrompt = null;
            pendingSpeciesSwapRequester = null;
            uiManager?.HideSpeciesSwapConfirm();
        }

        private bool RequestSpeciesSwap(
            DrawManager.Species requestedSpecies,
            PlayerController2D requester)
        {
            if (!RequiresUniquePlayerSpecies || requester == null)
            {
                return false;
            }

            if (pendingOutgoingSpeciesSwap != null)
            {
                drawManager?.ShowSpeciesSwapPending();
                uiManager?.ShowSpeciesSwapPending(
                    GetPlayerDisplayName(pendingOutgoingSpeciesSwap.TargetId),
                    (DrawManager.Species)pendingOutgoingSpeciesSwap.TargetSpecies,
                    (DrawManager.Species)pendingOutgoingSpeciesSwap.RequesterSpecies);
                return true;
            }

            if (pendingIncomingSpeciesSwap != null)
            {
                return false;
            }

            PlayerController2D target = FindPlayerUsingSpecies(requestedSpecies, requester);
            if (target == null || !TryGetConfirmedSpecies(requester, out DrawManager.Species currentSpecies))
            {
                return false;
            }

            string requesterId = GetSwapPlayerId(requester);
            string targetId = GetSwapPlayerId(target);
            if (string.IsNullOrEmpty(requesterId) || string.IsNullOrEmpty(targetId))
            {
                return false;
            }

            // Preserve the requester's unconfirmed edits separately while the
            // other player decides. The confirmed roster must retain the old
            // species until the host applies both sides atomically.
            LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
            if (localCoop?.TryGetDrawingState(requester, out DrawManager.DrawingState localState) == true)
                pendingSpeciesSwapDrawingState = localState;
            else if (requester == player && drawManager != null)
                pendingSpeciesSwapDrawingState = drawManager.CreateState();
            SpeciesSwapMessage request = new SpeciesSwapMessage
            {
                RequestId = System.Guid.NewGuid().ToString("N"),
                RequesterId = requesterId,
                TargetId = targetId,
                RequesterSpecies = (int)currentSpecies,
                TargetSpecies = (int)requestedSpecies
            };
            pendingOutgoingSpeciesSwap = request;
            pendingOutgoingSpeciesSwapExpiresAt = Time.unscaledTime + 20f;

            if (!IsOnlineInStage())
            {
                pendingIncomingSpeciesSwap = request;
                pendingIncomingSpeciesSwapExpiresAt = Time.unscaledTime + 15f;
                uiManager?.ShowSpeciesSwapConfirm(
                    GetPlayerDisplayName(requesterId),
                    requestedSpecies,
                    currentSpecies);
                return true;
            }

            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                PlayerId = requesterId,
                ObjectId = currentStageId,
                Kind = GimmickKindSpeciesSwapRequest,
                Json = JsonUtility.ToJson(request)
            });
            drawManager?.ShowSpeciesSwapPending();
            uiManager?.ShowSpeciesSwapPending(
                GetPlayerDisplayName(targetId),
                requestedSpecies,
                currentSpecies);
            return true;
        }

        public void AcceptSpeciesSwapRequest()
        {
            SpeciesSwapMessage request = pendingIncomingSpeciesSwap;
            pendingIncomingSpeciesSwap = null;
            pendingIncomingSpeciesSwapExpiresAt = 0f;
            uiManager?.HideSpeciesSwapConfirm();
            if (request == null)
            {
                return;
            }

            request.Accepted = true;
            if (!IsOnlineInStage())
            {
                ApplySpeciesSwap(request);
                return;
            }

            if (IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                TryApplyAndBroadcastSpeciesSwap(request);
                return;
            }

            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                PlayerId = request.TargetId,
                ObjectId = currentStageId,
                Kind = GimmickKindSpeciesSwapResponse,
                Json = JsonUtility.ToJson(request)
            });
        }

        public void RejectSpeciesSwapRequest()
        {
            SpeciesSwapMessage request = pendingIncomingSpeciesSwap;
            pendingIncomingSpeciesSwap = null;
            pendingIncomingSpeciesSwapExpiresAt = 0f;
            uiManager?.HideSpeciesSwapConfirm();
            if (request == null || !IsOnlineInStage())
            {
                PlayerController2D rejectedRequester = request != null
                    ? GetSwapPlayer(request.RequesterId)
                    : null;
                pendingOutgoingSpeciesSwap = null;
                pendingOutgoingSpeciesSwapExpiresAt = 0f;
                pendingSpeciesSwapDrawingState = null;
                if (GetComponent<LocalCoopSessionManager>()?.ShowSpeciesSwapFailure(rejectedRequester) != true)
                    drawManager?.ShowSpeciesSwapResult(false);
                return;
            }

            request.Accepted = false;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                PlayerId = request.TargetId,
                ObjectId = currentStageId,
                Kind = GimmickKindSpeciesSwapResponse,
                Json = JsonUtility.ToJson(request)
            });
        }

        private void ShowIncomingSpeciesSwap(SpeciesSwapMessage request)
        {
            if (request == null)
            {
                return;
            }

            if (pendingIncomingSpeciesSwap != null)
            {
                if (pendingIncomingSpeciesSwap.RequestId != request.RequestId)
                {
                    DeclineSpeciesSwapAsBusy(request);
                }
                return;
            }

            // Do not replace a request this player is already waiting on with a
            // second negotiation. The sender receives an immediate decline.
            if (pendingOutgoingSpeciesSwap != null)
            {
                DeclineSpeciesSwapAsBusy(request);
                return;
            }

            pendingIncomingSpeciesSwap = request;
            pendingIncomingSpeciesSwapExpiresAt = Time.unscaledTime + 15f;
            uiManager?.ShowSpeciesSwapConfirm(
                GetPlayerDisplayName(request.RequesterId),
                (DrawManager.Species)request.TargetSpecies,
                (DrawManager.Species)request.RequesterSpecies);
        }

        private void DeclineSpeciesSwapAsBusy(SpeciesSwapMessage request)
        {
            if (request == null || onlineManager == null)
            {
                return;
            }

            request.Accepted = false;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                PlayerId = request.TargetId,
                ObjectId = currentStageId,
                Kind = GimmickKindSpeciesSwapResponse,
                Json = JsonUtility.ToJson(request)
            });
        }

        private void TryApplyAndBroadcastSpeciesSwap(SpeciesSwapMessage request)
        {
            if (request == null || !request.Accepted)
            {
                return;
            }

            PlayerController2D requester = GetSwapPlayer(request.RequesterId);
            PlayerController2D target = GetSwapPlayer(request.TargetId);
            if (requester == null || target == null
                || !TryGetConfirmedSpecies(requester, out DrawManager.Species requesterSpecies)
                || !TryGetConfirmedSpecies(target, out DrawManager.Species targetSpecies)
                || requesterSpecies != (DrawManager.Species)request.RequesterSpecies
                || targetSpecies != (DrawManager.Species)request.TargetSpecies)
            {
                request.Accepted = false;
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindSpeciesSwapResponse,
                    Json = JsonUtility.ToJson(request)
                });
                return;
            }

            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindSpeciesSwapApply,
                Json = JsonUtility.ToJson(request)
            });
            ApplySpeciesSwap(request);
        }

        private void ApplySpeciesSwap(SpeciesSwapMessage request)
        {
            if (request == null)
            {
                return;
            }

            PlayerController2D requester = GetSwapPlayer(request.RequesterId);
            PlayerController2D target = GetSwapPlayer(request.TargetId);
            if (requester == null || target == null)
            {
                return;
            }

            bool localWasRequester = localPlayers.Contains(requester);
            bool localWasTarget = localPlayers.Contains(target);
            if (localWasRequester && pendingSpeciesSwapDrawingState != null)
            {
                drawingStates[requester] = pendingSpeciesSwapDrawingState;
            }

            AssignSpeciesToPlayer(requester, (DrawManager.Species)request.TargetSpecies);
            AssignSpeciesToPlayer(target, (DrawManager.Species)request.RequesterSpecies);
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            LiftPlayerOutOfGround(requester);
            LiftPlayerOutOfGround(target);
            pendingIncomingSpeciesSwap = null;
            pendingIncomingSpeciesSwapExpiresAt = 0f;

            if (localWasRequester)
            {
                pendingOutgoingSpeciesSwap = null;
                pendingOutgoingSpeciesSwapExpiresAt = 0f;
                pendingSpeciesSwapDrawingState = null;
                uiManager?.HideSpeciesSwapConfirm();
                LocalCoopSessionManager localCoop = GetComponent<LocalCoopSessionManager>();
                bool completedIndependent = localCoop?.CompleteSpeciesSwapForPlayer(requester) == true;
                if (!completedIndependent) drawManager?.ShowSpeciesSwapResult(true);
                if (!completedIndependent && drawing && requester == player)
                {
                    ConfirmDrawingMode();
                }
            }

            if (localWasRequester || localWasTarget)
            {
                SendLocalOnlineBodyData();
            }
        }

        private void UpdateSpeciesSwapTimeouts()
        {
            if (pendingIncomingSpeciesSwap != null
                && pendingIncomingSpeciesSwapExpiresAt > 0f
                && Time.unscaledTime >= pendingIncomingSpeciesSwapExpiresAt)
            {
                RejectSpeciesSwapRequest();
            }

            if (pendingOutgoingSpeciesSwap != null
                && pendingOutgoingSpeciesSwapExpiresAt > 0f
                && Time.unscaledTime >= pendingOutgoingSpeciesSwapExpiresAt)
            {
                PlayerController2D timedOutRequester = GetSwapPlayer(
                    pendingOutgoingSpeciesSwap.RequesterId);
                pendingOutgoingSpeciesSwap = null;
                pendingOutgoingSpeciesSwapExpiresAt = 0f;
                pendingSpeciesSwapDrawingState = null;
                uiManager?.HideSpeciesSwapConfirm();
                if (GetComponent<LocalCoopSessionManager>()?.ShowSpeciesSwapFailure(timedOutRequester) != true)
                    drawManager?.ShowSpeciesSwapResult(false);
            }
        }

        private void ResetSpeciesSwapState()
        {
            pendingIncomingSpeciesSwap = null;
            pendingOutgoingSpeciesSwap = null;
            pendingSpeciesSwapPrompt = null;
            pendingSpeciesSwapDrawingState = null;
            pendingIncomingSpeciesSwapExpiresAt = 0f;
            pendingOutgoingSpeciesSwapExpiresAt = 0f;
            uiManager?.HideSpeciesSwapConfirm();
        }

        private PlayerController2D FindPlayerUsingSpecies(
            DrawManager.Species species,
            PlayerController2D except)
        {
            List<PlayerController2D> candidates = new List<PlayerController2D>();
            for (int i = 0; i < localPlayers.Count; i++) AddPlayerOnce(candidates, localPlayers[i]);
            foreach (KeyValuePair<string, PlayerController2D> remote in onlineRemotePlayers)
            {
                AddPlayerOnce(candidates, remote.Value);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] != null && candidates[i] != except
                    && TryGetConfirmedSpecies(candidates[i], out DrawManager.Species used)
                    && used == species)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private string GetSwapPlayerId(PlayerController2D target)
        {
            if (IsOnlineInStage())
            {
                return GetOnlinePlayerId(target);
            }

            int localIndex = localPlayers.IndexOf(target);
            return localIndex >= 0 ? $"offline_local_{localIndex}" : null;
        }

        private PlayerController2D GetSwapPlayer(string playerId)
        {
            if (!string.IsNullOrEmpty(playerId)
                && playerId.StartsWith("offline_local_", System.StringComparison.Ordinal)
                && int.TryParse(playerId.Substring("offline_local_".Length), out int localIndex)
                && localIndex >= 0 && localIndex < localPlayers.Count)
                return localPlayers[localIndex];
            if (playerId == "offline_primary") return primaryPlayer;
            if (playerId == "offline_secondary") return secondaryPlayer;
            return GetOnlinePlayerController(playerId);
        }

        private string GetPlayerDisplayName(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers != null)
            {
                for (int i = 0; i < lobbyPlayers.Length; i++)
                {
                    if (lobbyPlayers[i] != null && lobbyPlayers[i].PlayerId == playerId)
                    {
                        return string.IsNullOrEmpty(lobbyPlayers[i].DisplayName)
                            ? LocalizationManager.Format("online_player_number", i + 1)
                            : lobbyPlayers[i].DisplayName;
                    }
                }
            }

            if (!string.IsNullOrEmpty(playerId)
                && playerId.StartsWith("offline_local_", System.StringComparison.Ordinal)
                && int.TryParse(playerId.Substring("offline_local_".Length), out int localIndex))
                return LocalizationManager.Format("online_player_number", localIndex + 1);
            return playerId == "offline_secondary"
                ? LocalizationManager.Format("online_player_number", 2)
                : LocalizationManager.Format("online_player_number", 1);
        }

        private bool TryGetConfirmedSpecies(PlayerController2D target, out DrawManager.Species species)
        {
            if (target != null
                && drawingStates.TryGetValue(target, out DrawManager.DrawingState state)
                && state != null)
            {
                species = state.Species;
                return true;
            }

            PlayerAbilityController abilities = target != null
                ? target.GetComponent<PlayerAbilityController>()
                : null;
            if (abilities != null)
            {
                species = abilities.CurrentProfile.Species;
                return true;
            }

            species = DrawManager.Species.Human;
            return false;
        }

        private void AssignInitialUniqueSpecies()
        {
            if (drawManager == null || !RequiresUniquePlayerSpecies)
            {
                return;
            }

            SaveDrawingState(player);

            // 15-3 preserves every valid choice. Its stage-rule pass replaces
            // only a forbidden/duplicate species with an unused allowed one.
            if (string.Equals(currentStageId, "15-3", System.StringComparison.OrdinalIgnoreCase))
            {
                ConfigureActivePlayerTargets();
                LoadDrawingState(player);
                return;
            }

            if (IsOnlineInStage() || onlineManager?.CurrentLobby != null)
            {
                int connectionSlot = GetLobbyPlayerSlot(onlineManager != null
                    ? onlineManager.LocalPlayerId
                    : null);
                for (int i = 0; i < localPlayers.Count; i++)
                {
                    PlayerController2D local = localPlayers[i];
                    int localSlot = GetLobbyPlayerSlot(GetOnlinePlayerId(local));
                    if (localSlot < 0) localSlot = Mathf.Max(0, connectionSlot) + i;
                    AssignSpeciesToPlayer(local, GetUniqueSpeciesForSlot(localSlot));
                }
            }
            else
            {
                for (int i = 0; i < localPlayers.Count; i++)
                    AssignSpeciesToPlayer(localPlayers[i], GetUniqueSpeciesForSlot(i));
            }

            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
        }

        private void AssignSpeciesToPlayer(PlayerController2D target, DrawManager.Species species)
        {
            if (target == null || drawManager == null)
            {
                return;
            }

            if (!drawingStates.TryGetValue(target, out DrawManager.DrawingState state) || state == null)
            {
                state = CloneDrawingState(drawManager.CreateState());
            }

            state.Species = species;
            state.Part = DrawManager.BodyPart.Torso;
            drawingStates[target] = state;
            drawManager.SetBuildTarget(
                target.GetComponent<BodyBuilder>(),
                target.GetComponent<PlayerAbilityController>());
            drawManager.LoadState(state, true);
        }

        private DrawManager.Species GetUniqueSpeciesForSlot(int slot)
        {
            IReadOnlyList<DrawManager.Species> ordered = StageSpeciesRules.GetOrderedSpecies();
            StageSpeciesMask availability = StageSpeciesRules.GetAllowedForStage(currentStageId);
            List<DrawManager.Species> allowed = new List<DrawManager.Species>();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (StageSpeciesRules.IsAllowed(availability, ordered[i]))
                {
                    allowed.Add(ordered[i]);
                }
            }
            return allowed.Count > 0
                ? allowed[Mathf.Abs(slot) % allowed.Count]
                : DrawManager.Species.Human;
        }

        private void ResolveSimultaneousUniqueSpeciesConflict(
            string incomingPlayerId,
            DrawManager.Species incomingSpecies)
        {
            if (!RequiresUniquePlayerSpecies
                || primaryPlayer == null
                || onlineManager?.CurrentLobby?.Players == null
                || string.IsNullOrEmpty(incomingPlayerId))
            {
                return;
            }

            int incomingSlot = GetLobbyPlayerSlot(incomingPlayerId);
            if (incomingSlot < 0) return;
            PlayerController2D conflictingLocal = null;
            string conflictingLocalId = null;
            int localSlot = int.MaxValue;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (!TryGetConfirmedSpecies(local, out DrawManager.Species localSpecies)
                    || localSpecies != incomingSpecies) continue;
                string gamePlayerId = GetOnlinePlayerId(local);
                int slot = GetLobbyPlayerSlot(gamePlayerId);
                if (slot >= 0 && slot < localSlot)
                {
                    conflictingLocal = local;
                    conflictingLocalId = gamePlayerId;
                    localSlot = slot;
                }
            }
            if (conflictingLocal == null || localSlot <= incomingSlot) return;

            IReadOnlyList<DrawManager.Species> ordered = StageSpeciesRules.GetOrderedSpecies();
            for (int i = 0; i < ordered.Count; i++)
            {
                DrawManager.Species candidate = ordered[i];
                if (!StageSpeciesRules.IsAllowed(
                    StageSpeciesRules.GetAllowedForStage(currentStageId), candidate)) continue;
                if (!IsSpeciesUsedByAnotherPlayer(candidate))
                {
                    AssignSpeciesToPlayer(conflictingLocal, candidate);
                    ConfigureActivePlayerTargets();
                    LoadDrawingState(player);
                    LiftPlayerOutOfGround(conflictingLocal);
                    SendLocalOnlineBodyData(conflictingLocalId);
                    return;
                }
            }
        }

        private int GetLobbyPlayerSlot(string playerId)
        {
            return PlayerColorPalette.GetLobbyPlayerSlot(onlineManager?.CurrentLobby, playerId);
        }

        private int GetLobbyPlayerCount()
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return 1;

            HashSet<string> playerIds = new HashSet<string>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && !string.IsNullOrEmpty(players[i].PlayerId))
                    playerIds.Add(players[i].PlayerId);
            }
            return Mathf.Max(1, playerIds.Count);
        }

        private bool IsSpeciesUsedByAnotherPlayer(DrawManager.Species species)
        {
            foreach (KeyValuePair<string, PlayerController2D> remote in onlineRemotePlayers)
            {
                if (remote.Value != null
                    && TryGetConfirmedSpecies(remote.Value, out DrawManager.Species remoteSpecies)
                    && remoteSpecies == species)
                {
                    return true;
                }
            }

            for (int i = 1; i < localPlayers.Count; i++)
                if (TryGetConfirmedSpecies(localPlayers[i], out DrawManager.Species localSpecies)
                    && localSpecies == species) return true;
            return false;
        }

        private void ApplySpeciesRulesForCurrentStage()
        {
            if (drawManager == null)
            {
                return;
            }

            StageSpeciesMask availability = StageSpeciesRules.GetAllowedForStage(currentStageId);
            if (player != null)
            {
                SaveDrawingState(player);
            }

            List<PlayerController2D> playersToRefresh = new List<PlayerController2D>();
            for (int i = 0; i < localPlayers.Count; i++)
                AddPlayerOnce(playersToRefresh, localPlayers[i]);
            foreach (KeyValuePair<string, PlayerController2D> remote in onlineRemotePlayers)
            {
                AddPlayerOnce(playersToRefresh, remote.Value);
            }

            if (string.Equals(currentStageId, "15-3", System.StringComparison.OrdinalIgnoreCase))
            {
                playersToRefresh.Sort((left, right) =>
                    GetSpeciesNormalizationOrder(left).CompareTo(GetSpeciesNormalizationOrder(right)));
                NormalizeUniqueAllowedSpecies(playersToRefresh, availability);
            }
            else
            {
                DrawManager.Species fallback = StageSpeciesRules.GetFirstAllowed(availability);
                foreach (KeyValuePair<PlayerController2D, DrawManager.DrawingState> entry in drawingStates)
                {
                    if (entry.Value != null
                        && !StageSpeciesRules.IsAllowed(availability, entry.Value.Species))
                    {
                        entry.Value.Species = fallback;
                        entry.Value.Part = DrawManager.BodyPart.Torso;
                    }
                }
            }

            drawManager.SetAllowedSpecies(availability);

            for (int i = 0; i < playersToRefresh.Count; i++)
            {
                PlayerController2D target = playersToRefresh[i];
                if (target == null || target == player || !drawingStates.TryGetValue(target, out DrawManager.DrawingState state))
                {
                    continue;
                }

                drawManager.SetBuildTarget(
                    target.GetComponent<BodyBuilder>(),
                    target.GetComponent<PlayerAbilityController>());
                drawManager.LoadState(state, true);
            }

            ConfigureActivePlayerTargets();
            if (player != null)
            {
                LoadDrawingState(player);
            }
        }

        private void NormalizeUniqueAllowedSpecies(
            IReadOnlyList<PlayerController2D> players,
            StageSpeciesMask availability)
        {
            HashSet<DrawManager.Species> used = new HashSet<DrawManager.Species>();
            List<DrawManager.DrawingState> replacements = new List<DrawManager.DrawingState>();
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController2D target = players[i];
                if (target == null
                    || !drawingStates.TryGetValue(target, out DrawManager.DrawingState state)
                    || state == null) continue;
                if (StageSpeciesRules.IsAllowed(availability, state.Species)
                    && used.Add(state.Species)) continue;
                replacements.Add(state);
            }

            IReadOnlyList<DrawManager.Species> ordered = StageSpeciesRules.GetOrderedSpecies();
            for (int i = 0; i < replacements.Count; i++)
            {
                DrawManager.Species replacement = StageSpeciesRules.GetFirstAllowed(availability);
                for (int speciesIndex = 0; speciesIndex < ordered.Count; speciesIndex++)
                {
                    DrawManager.Species candidate = ordered[speciesIndex];
                    if (!StageSpeciesRules.IsAllowed(availability, candidate) || used.Contains(candidate)) continue;
                    replacement = candidate;
                    break;
                }
                replacements[i].Species = replacement;
                replacements[i].Part = DrawManager.BodyPart.Torso;
                used.Add(replacement);
            }
        }

        private int GetSpeciesNormalizationOrder(PlayerController2D target)
        {
            int onlineSlot = GetLobbyPlayerSlot(GetOnlinePlayerId(target));
            if (onlineSlot >= 0) return onlineSlot;
            int localSlot = localPlayers.IndexOf(target);
            return localSlot >= 0 ? localSlot : int.MaxValue;
        }

        private static void AddPlayerOnce(List<PlayerController2D> players, PlayerController2D candidate)
        {
            if (candidate != null && !players.Contains(candidate))
            {
                players.Add(candidate);
            }
        }

        private void LoadDrawingState(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null || drawManager == null)
            {
                return;
            }

            if (!drawingStates.TryGetValue(targetPlayer, out DrawManager.DrawingState state))
            {
                drawingStates[targetPlayer] = drawManager.CreateState();
                return;
            }

            drawManager.LoadState(state, true);
        }

        private static DrawManager.DrawingState CloneDrawingState(DrawManager.DrawingState source)
        {
            DrawManager.DrawingState clone = new DrawManager.DrawingState
            {
                Species = source.Species,
                Part = source.Part
            };

            foreach (KeyValuePair<DrawManager.Species, Dictionary<DrawManager.BodyPart, List<Vector2>>> speciesPair in source.Points)
            {
                Dictionary<DrawManager.BodyPart, List<Vector2>> parts = new Dictionary<DrawManager.BodyPart, List<Vector2>>();
                foreach (KeyValuePair<DrawManager.BodyPart, List<Vector2>> partPair in speciesPair.Value)
                {
                    parts[partPair.Key] = new List<Vector2>(partPair.Value);
                }

                clone.Points[speciesPair.Key] = parts;
                if (source.StrokeWidths.TryGetValue(
                        speciesPair.Key,
                        out Dictionary<DrawManager.BodyPart, List<float>> sourceWidths))
                {
                    Dictionary<DrawManager.BodyPart, List<float>> widths = new Dictionary<DrawManager.BodyPart, List<float>>();
                    foreach (KeyValuePair<DrawManager.BodyPart, List<float>> widthPair in sourceWidths)
                    {
                        widths[widthPair.Key] = new List<float>(widthPair.Value);
                    }
                    clone.StrokeWidths[speciesPair.Key] = widths;
                }
            }

            return clone;
        }

        private void SetCameraFollowEnabled(bool enabled)
        {
            if (cameraFollow != null)
            {
                if (enabled)
                {
                    Camera stageCamera = cameraFollow.GetComponent<Camera>();
                    if (stageCamera != null && stageCamera.orthographic)
                    {
                        stageCamera.orthographicSize = Mathf.Max(
                            stageCamera.orthographicSize,
                            cameraFollow.MinimumOrthographicSize);
                        Vector3 cameraPosition = stageCamera.transform.position;
                        if (cameraPosition.z > -1f)
                        {
                            cameraPosition.z = -10f;
                            stageCamera.transform.position = cameraPosition;
                        }
                    }
                }
                cameraFollow.enabled = enabled;
            }
        }

        private void ConfigureTitlePlaygroundCamera()
        {
            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }

            if (cameraFollow == null)
            {
                return;
            }

            Camera titleCamera = cameraFollow.GetComponent<Camera>();
            cameraFollow.enabled = false;
            if (titleCamera == null || !titleCamera.orthographic)
            {
                return;
            }

            Vector3 position = titleCamera.transform.position;
            titleCamera.transform.position = new Vector3(0f, 0f, position.z > -1f ? -10f : position.z);
            titleCamera.orthographicSize = 8f;
        }

    }

    internal sealed class ControlledPlayerMarker : MonoBehaviour
    {
        private const string MarkerRootName = "Controlled Player Marker";
        private PlayerController2D controller;
        private Transform markerRoot;
        private TextMesh mainText;
        private TextMesh shadowText;
        private Collider2D[] bodyColliders;
        private float nextColliderRefreshAt;
        private bool controlled;
        private bool forceVisible;

        private void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            EnsureVisual();
            RefreshText();
            SetRenderersVisible(false);
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshText;
            RefreshText();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshText;
        }

        public void SetControlled(bool value, bool showWhileControlsSuppressed = false)
        {
            controlled = value;
            forceVisible = showWhileControlsSuppressed;
            SetRenderersVisible(IsVisible());
        }

        private void LateUpdate()
        {
            bool visible = IsVisible();
            SetRenderersVisible(visible);
            if (!visible)
            {
                return;
            }

            if (bodyColliders == null || Time.unscaledTime >= nextColliderRefreshAt)
            {
                bodyColliders = GetComponentsInChildren<Collider2D>(false);
                nextColliderRefreshAt = Time.unscaledTime + 0.25f;
            }

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider2D collider = bodyColliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            Vector3 position = hasBounds
                ? new Vector3(bounds.center.x, bounds.max.y + 0.62f, -0.5f)
                : transform.position + new Vector3(0f, 1.8f, -0.5f);
            position.y += Mathf.Sin(Time.unscaledTime * 4f) * 0.045f;
            markerRoot.position = position;
            markerRoot.rotation = Quaternion.identity;
        }

        private void EnsureVisual()
        {
            markerRoot = transform.Find(MarkerRootName);
            if (markerRoot == null)
            {
                GameObject root = new GameObject(MarkerRootName);
                markerRoot = root.transform;
                markerRoot.SetParent(transform, false);
            }

            Transform existingShadow = markerRoot.Find("Shadow");
            shadowText = existingShadow != null ? existingShadow.GetComponent<TextMesh>() : null;
            if (shadowText == null)
            {
                shadowText = CreateText("Shadow", new Color(0.08f, 0.06f, 0.03f, 0.92f), 499);
                shadowText.transform.localPosition = new Vector3(0.025f, -0.025f, 0.02f);
            }

            Transform existingMain = markerRoot.Find("Label");
            mainText = existingMain != null ? existingMain.GetComponent<TextMesh>() : null;
            if (mainText == null)
            {
                mainText = CreateText("Label", new Color(1f, 0.72f, 0.08f, 1f), 500);
            }
        }

        private TextMesh CreateText(string objectName, Color color, int sortingOrder)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(markerRoot, false);
            TextMesh text = textObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = font;
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = 0.085f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.lineSpacing = 0.72f;
            text.color = color;

            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (font != null)
            {
                renderer.sharedMaterial = font.material;
            }

            return text;
        }

        private void RefreshText()
        {
            EnsureVisual();
            string marker = LocalizationManager.T("player_controlled_marker") + "\n\u25BC";
            mainText.text = marker;
            shadowText.text = marker;
        }

        private bool IsVisible()
        {
            return controlled && controller != null
                && (controller.ControlsEnabled || forceVisible);
        }

        private void SetRenderersVisible(bool visible)
        {
            if (mainText != null)
            {
                mainText.GetComponent<Renderer>().enabled = visible;
            }

            if (shadowText != null)
            {
                shadowText.GetComponent<Renderer>().enabled = visible;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageEliminationSpectatorRelay : MonoBehaviour
    {
        private StageManager stageManager;
        private PlayerController2D player;
        private Rigidbody2D body;
        private bool configured;
        private bool notified;

        public void Configure(StageManager manager, PlayerController2D controller)
        {
            bool samePlayer = configured && stageManager == manager && player == controller;
            stageManager = manager;
            player = controller;
            body = controller != null ? controller.GetComponent<Rigidbody2D>() : null;
            configured = true;
            if (!samePlayer) notified = false;
        }

        private void OnEnable()
        {
            bool wasEliminated = notified;
            notified = false;
            if (configured && stageManager != null && player != null)
            {
                stageManager.RestoreCameraAfterRevival(player);
                if (wasEliminated) stageManager.NotifyPlayerRevived(player);
            }
        }

        private void Update()
        {
            if (configured && !notified && body != null && !body.simulated && !HasVisibleRenderer())
            {
                NotifyEliminated();
            }
            else if (configured && notified && body != null && body.simulated)
            {
                notified = false;
                stageManager?.RestoreCameraAfterRevival(player);
                stageManager?.NotifyPlayerRevived(player);
            }
        }

        private void OnDisable()
        {
            NotifyEliminated();
        }

        private bool HasVisibleRenderer()
        {
            if (player == null) return false;
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled) return true;
            }
            return false;
        }

        private void NotifyEliminated()
        {
            if (notified || !configured || stageManager == null || player == null || !Application.isPlaying) return;
            notified = stageManager.NotifyPlayerEliminated(player);
        }
    }
}
