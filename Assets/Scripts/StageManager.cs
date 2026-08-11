using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageManager : MonoBehaviour
    {
        private const string GimmickKindClearRequest = "stage_clear_request";
        private const string GimmickKindClear = "stage_clear";
        private const string GimmickKindGoalState = "player_goal_state";
        private const string GimmickKindRetry = "stage_retry";
        private const string GimmickKindSessionEnded = "session_ended";
        private const string GimmickKindCollectRequest = "collect_request";
        private const string GimmickKindCollectState = "collect_state";
        private const string GimmickKindChallengeFailed = "challenge_failed";
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
        private bool drawing;
        private bool cleared;
        private bool stageStarted;
        private bool stageEditing;
        private bool testingEditedStage;
        private bool stageSelectEditMode;
        private bool stageSelectReturnToMultiLobby;
        private bool stageSelectRemoteWaiting;
        private bool titleMode;
        private bool onlineStateSubscribed;
        private string currentStageId = "1-0";
        private string remotePlayerId;
        private string onlineCarrierPlayerId;
        private readonly Dictionary<string, PlayerController2D> onlineRemotePlayers =
            new Dictionary<string, PlayerController2D>();
        private bool onlineCarryHeld;
        private Rigidbody2D onlineCarryBody;
        private RigidbodyType2D onlineCarryPreviousBodyType;
        private float onlineCarryPreviousGravityScale;
        private bool onlineCarryPreviousFreezeRotation;
        private bool onlineCarryIsCatGrab;
        private float onlineCarryBeganAt;
        private float onlineCarryLastConfirmedAt;
        private Vector2 onlineCarryLocalOffset;
        private readonly List<Collider2D> onlineCarryColliders = new List<Collider2D>();
        private readonly Dictionary<PlayerController2D, DrawManager.DrawingState> drawingStates =
            new Dictionary<PlayerController2D, DrawManager.DrawingState>();
        private readonly Dictionary<PlayerController2D, RespawnAnimationState> respawnAnimations =
            new Dictionary<PlayerController2D, RespawnAnimationState>();
        private readonly Dictionary<PlayerController2D, float> respawnGraceUntil =
            new Dictionary<PlayerController2D, float>();
        private Material respawnBurstMaterial;
        private Coroutine redrawRespawnRoutine;
        private PlayerController2D redrawReturnPlayer;
        private Vector3 redrawReturnPosition;
        private bool hasRedrawReturnPosition;
        private readonly HashSet<PlayerController2D> localPlayersAtGoal = new HashSet<PlayerController2D>();
        private readonly HashSet<string> onlinePlayerIdsAtGoal = new HashSet<string>();
        private readonly HashSet<string> collectedObjectIds = new HashSet<string>();
        private StageRuleMode stageRuleMode;
        private StageObjectType collectionTarget = StageObjectType.CollectibleFish;
        private int requiredCollectionCount;
        private int totalCollectionTargetCount;
        private int collectedCount;
        private float challengeRemaining;
        private bool challengeFailed;
        private float nextChallengeSyncAt;
        private bool challengeStarting;
        private float challengeStartCountdownRemaining;
        private float challengeTimeUpReturnRemaining;
        private bool challengeStartPositionsCaptured;
        private StageSurvivalController survivalController;
        private TrailerCoopDemoController trailerDemo;
        private SteamHeaderCaptureController steamHeaderCapture;
        private Vector3 primaryChallengeStartPosition;
        private Vector3 secondaryChallengeStartPosition;
        public bool IsTimedCollectionChallenge => stageRuleMode == StageRuleMode.TimedCollection;
        public bool IsSurvivalChallenge => stageRuleMode == StageRuleMode.Survival;
        public bool IsDrawingMode => drawing;
        public bool IsGameplayActive => stageStarted && !titleMode && !stageEditing && !drawing && !cleared;
        public float ChallengeRemainingSeconds => challengeRemaining;
        public bool ChallengeTimeUp => challengeFailed;
        public StageObjectType ChallengeCollectionTarget => collectionTarget;
        public int ChallengeCollectedCount => collectedCount;
        public int ChallengeRequiredCollectionCount => requiredCollectionCount;
        public int ChallengeTotalCollectionTargetCount => totalCollectionTargetCount;
        public bool ChallengeStarting => challengeStarting;
        public string CurrentStageId => currentStageId;
        public string ChallengeStartCountdownText
        {
            get
            {
                if (!challengeStarting)
                {
                    return string.Empty;
                }
                if (challengeStartCountdownRemaining > 3f) return "3";
                if (challengeStartCountdownRemaining > 2f) return "2";
                if (challengeStartCountdownRemaining > 1f) return "1";
                return "START!";
            }
        }

        [System.Serializable]
        private sealed class PlayerGoalState
        {
            public bool Inside;
        }

        private sealed class RespawnAnimationState
        {
            public Coroutine Routine;
            public Vector3 OriginalScale;
            public Rigidbody2D Body;
            public bool BodyWasSimulated;
        }

        [System.Serializable]
        private sealed class CollectionState
        {
            public string CollectibleId;
            public int Count;
            public float RemainingSeconds;
        }

        private void Awake()
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerController2D>();
            }

            primaryPlayer = player;

            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }

            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            if (stageLoader == null)
            {
                stageLoader = FindObjectOfType<StageLoader>();
            }

            if (stageEditor == null)
            {
                stageEditor = FindObjectOfType<RuntimeStageEditor>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindObjectOfType<CameraFollow2D>();
            }

            ConfigureActivePlayerTargets();
            ApplyDefaultPlayerColors();
            SubscribeOnlineEvents();
            if (GetComponent<PlayerEmoteController>() == null)
            {
                gameObject.AddComponent<PlayerEmoteController>();
            }
        }

        private void Start()
        {
            EnterTitle();
        }

        private void OnEnable()
        {
            SubscribeOnlineEvents();
        }

        private void OnDisable()
        {
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
                onlineManager = FindObjectOfType<OnlineManager>();
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
                return;
            }

            bool localHost = IsLocalOnlineHost(lobby);
            if (state == OnlineConnectionState.Playing && !localHost)
            {
                SelectStage(!string.IsNullOrEmpty(lobby.StageId) ? lobby.StageId : "1-1");
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
                OpenStageSelectWaitingForHost();
            }
            else if (message == LocalizationManager.T("online_stage_select_closed"))
            {
                CloseStageSelectWaitingForHost();
            }
        }

        public void EnterTitle()
        {
            StopTrailerDemo();
            StopSteamHeaderCapture();
            CancelRespawnAnimations();
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(false);
            currentStageId = "title";
            GameBgm.PlayTitle();
            drawManager?.SetAllowedSpecies(StageSpeciesMask.All);
            titleMode = true;
            stageStarted = true;
            stageEditing = false;
            drawing = false;
            cleared = false;
            ResetGoalProgress();
            Time.timeScale = 1f;
            stageLoader?.ShowFallbackStage();
            SetCameraFollowEnabled(true);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetTitle(true);
            ConfigureStageRule(null);
            drawManager?.SetActive(false);
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
            uiManager?.SetMulti(true);
        }

        public void OpenOptionMenu()
        {
            uiManager?.OpenOption(stageStarted && !titleMode);
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

        public void RequestLeaveSession()
        {
            uiManager?.ShowLeaveSessionConfirm(IsOnlineStageHost);
        }

        public void ConfirmLeaveSession()
        {
            if (IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
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
            onlineManager?.LeaveLobby();
            uiManager?.HideLeaveSessionConfirm();
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

            if (stageEditing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (testingEditedStage)
                {
                    ReturnToStageEditor();
                    return;
                }
                else if (uiManager != null && uiManager.IsTitleSubmenuShowing)
                {
                    CloseTitleSubmenu();
                }
                else if (drawing)
                {
                    CancelDrawingMode();
                }
                else if (!cleared)
                {
                    uiManager?.ToggleMenu();
                }
            }

            if (Input.GetKeyDown(KeyCode.Tab) && !cleared)
            {
                if (drawing)
                {
                    CancelDrawingMode();
                }
                else
                {
                    uiManager?.ToggleGameplayHudDrawer();
                }
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Retry();
            }

            if (!drawing && !cleared)
            {
                RespawnFallenPlayers();
            }

            UpdateTimedCollectionChallenge();
        }

        public void ClearStage()
        {
            if (cleared)
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

            ApplyCollectible(collectible.ObjectId, IsOnlineInStage());
        }

        private void ApplyCollectible(string objectId, bool broadcast)
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

            collectible.ApplyCollected();
            collectedCount++;
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);

            CollectionState state = new CollectionState
            {
                CollectibleId = objectId,
                Count = collectedCount,
                RemainingSeconds = challengeRemaining
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
                FindCollectible(state.CollectibleId)?.ApplyCollected();
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
            collectedCount = 0;
            challengeFailed = false;
            challengeStarting = stageRuleMode == StageRuleMode.TimedCollection;
            challengeStartCountdownRemaining = challengeStarting ? ChallengeStartCountdownDuration : 0f;
            challengeTimeUpReturnRemaining = 0f;
            challengeStartPositionsCaptured = false;
            collectedObjectIds.Clear();
            nextChallengeSyncAt = Time.unscaledTime + 1f;

            totalCollectionTargetCount = CountCollectibles(collectionTarget);
            int configuredCount = data != null ? data.requiredCollectionCount : 1;
            requiredCollectionCount = configuredCount > 0
                ? Mathf.Clamp(configuredCount, 1, 999)
                : totalCollectionTargetCount;
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

        private void UpdateTimedCollectionChallenge()
        {
            if (stageRuleMode != StageRuleMode.TimedCollection || cleared
                || stageEditing || !stageStarted)
            {
                return;
            }

            if (challengeFailed)
            {
                uiManager?.SetChallengeCountdown(true, "TIME UP");
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
                HoldPlayersAtChallengeStart();
                uiManager?.SetChallengeCountdown(true, ChallengeStartCountdownText);
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

            challengeRemaining = Mathf.Max(0f, challengeRemaining - Time.unscaledDeltaTime);
            uiManager?.SetChallengeHud(true, challengeRemaining, collectionTarget, collectedCount, requiredCollectionCount, false);
            if (IsOnlineInStage() && IsLocalOnlineHost(onlineManager.CurrentLobby)
                && Time.unscaledTime >= nextChallengeSyncAt)
            {
                nextChallengeSyncAt = Time.unscaledTime + 1f;
                BroadcastCollectionSnapshot();
            }
            if (challengeRemaining > 0f || (IsOnlineInStage() && !IsLocalOnlineHost(onlineManager.CurrentLobby)))
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
                if (primaryPlayer != null)
                {
                    primaryChallengeStartPosition = primaryPlayer.transform.position;
                }
                if (secondaryPlayer != null)
                {
                    secondaryChallengeStartPosition = secondaryPlayer.transform.position;
                }
                challengeStartPositionsCaptured = true;
            }

            SetAllPlayerControls(false);
            if (primaryPlayer != null)
            {
                primaryPlayer.transform.position = primaryChallengeStartPosition;
                primaryPlayer.ResetMotion();
            }
            if (secondaryPlayer != null)
            {
                secondaryPlayer.transform.position = secondaryChallengeStartPosition;
                secondaryPlayer.ResetMotion();
            }
        }

        private void RestartAfterChallengeTimeUp()
        {
            if (IsOnlineInStage())
            {
                ApplyFullStageRetry();
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindRetry,
                    Json = "{}"
                });
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

            if (collectedObjectIds.Count == 0)
            {
                SendCollectionSnapshotEntry(string.Empty);
                return;
            }

            foreach (string objectId in collectedObjectIds)
            {
                SendCollectionSnapshotEntry(objectId);
            }
        }

        private void SendCollectionSnapshotEntry(string objectId)
        {
            CollectionState state = new CollectionState
            {
                CollectibleId = objectId,
                Count = collectedCount,
                RemainingSeconds = challengeRemaining
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = currentStageId,
                Kind = GimmickKindCollectState,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void ApplyChallengeFailed()
        {
            if (challengeFailed || cleared)
            {
                return;
            }
            challengeFailed = true;
            challengeStarting = false;
            challengeTimeUpReturnRemaining = ChallengeTimeUpReturnDelay;
            if (drawing)
            {
                CancelDrawingMode();
            }
            SetAllPlayerControls(false);
            uiManager?.SetChallengeCountdown(true, "TIME UP");
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

            if (IsOnlineInStage())
            {
                if (goalPlayer != primaryPlayer || onlineManager == null || string.IsNullOrEmpty(onlineManager.LocalPlayerId))
                {
                    return;
                }

                SetOnlineGoalState(onlineManager.LocalPlayerId, inside);
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindGoalState,
                    Json = JsonUtility.ToJson(new PlayerGoalState { Inside = inside })
                });
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

            if (inside && AreAllLocalPlayersAtGoal())
            {
                ClearStage();
            }
        }

        private bool AreAllLocalPlayersAtGoal()
        {
            PlayerController2D[] activePlayers = FindObjectsOfType<PlayerController2D>();
            int requiredPlayers = 0;
            for (int i = 0; i < activePlayers.Length; i++)
            {
                PlayerController2D activePlayer = activePlayers[i];
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
            ExitDrawingMode();
            SetAllPlayerControls(false);
            player?.ResetMotion();
            secondaryPlayer?.ResetMotion();
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
            if (data == null || onlineManager == null || data.PlayerId == onlineManager.LocalPlayerId)
            {
                return;
            }

            if (data.Kind == GimmickKindClearRequest && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                ClearStage();
            }
            else if (data.Kind == GimmickKindClear)
            {
                ApplyClearStage();
            }
            else if (data.Kind == GimmickKindGoalState && data.ObjectId == currentStageId)
            {
                PlayerGoalState goalState = JsonUtility.FromJson<PlayerGoalState>(data.Json);
                SetOnlineGoalState(data.PlayerId, goalState != null && goalState.Inside);
                TryClearWhenAllOnlinePlayersAtGoal();
            }
            else if (data.Kind == GimmickKindRetry && data.ObjectId == currentStageId)
            {
                ApplyFullStageRetry();
            }
            else if (data.Kind == GimmickKindCollectRequest && IsLocalOnlineHost(onlineManager.CurrentLobby))
            {
                ApplyCollectible(data.ObjectId, true);
            }
            else if (data.Kind == GimmickKindCollectState)
            {
                CollectionState state = JsonUtility.FromJson<CollectionState>(data.Json);
                if (state != null)
                {
                    ApplyCollectibleState(state);
                }
            }
            else if (data.Kind == GimmickKindChallengeFailed)
            {
                ApplyChallengeFailed();
            }
            else if (data.Kind == GimmickKindSessionEnded)
            {
                onlineManager.LeaveLobby();
                uiManager?.HideLeaveSessionConfirm();
                EnterTitle();
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

                ApplyFullStageRetry();
                onlineManager.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = currentStageId,
                    Kind = GimmickKindRetry,
                    Json = "{}"
                });
                return;
            }

            ApplyFullStageRetry();
        }

        private void ApplyFullStageRetry()
        {
            string stageToReload = currentStageId;
            cleared = false;
            ResetGoalProgress();
            CancelDrawingMode();
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

        public void GoToNextStage()
        {
            string nextStageId = GetNextStageId(currentStageId);
            if (string.IsNullOrEmpty(nextStageId))
            {
                OpenStageSelect();
                return;
            }

            SelectStage(nextStageId);
        }

        public void EnterDrawingMode()
        {
            if (!stageStarted || challengeStarting || challengeFailed)
            {
                return;
            }

            drawing = true;
            if (player != null)
            {
                redrawReturnPlayer = player;
                redrawReturnPosition = player.transform.position;
                hasRedrawReturnPosition = true;
                player.ResetMotion();
                if (onlineCarryHeld)
                {
                    EndOnlineCarry(Vector2.zero);
                }
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

        public void ConfirmDrawingMode()
        {
            ExitDrawingMode(true);
        }

        private void ExitDrawingMode(bool returnToStart)
        {
            if (!drawing)
            {
                return;
            }

            drawing = false;
            RestoreRedrawPose(returnToStart);
            Time.timeScale = 1f;
            uiManager?.SetDrawing(false);
            drawManager?.SetActive(false);
            SaveDrawingState(player);
            // ConfirmDrawing sends once before closing. Send the finalized state
            // again after the active player's state has been saved, so species
            // switches cannot remain stale on another client.
            SendLocalOnlineBodyData();
            player?.SetControlsEnabled(!cleared);
        }

        public void CancelDrawingMode()
        {
            if (!drawing)
            {
                return;
            }

            drawManager?.CancelEditing();
            ExitDrawingMode();
        }

        public void SelectStage(string stageId)
        {
            CancelRespawnAnimations();
            SetEditedStageTestMode(false);
            NotebookBackgroundDoodles.SetWorldVisible(true);
            currentStageId = string.IsNullOrEmpty(stageId) ? "1-0" : stageId;
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

            ConfigureStageRule(stageLoader != null ? stageLoader.CurrentStageData : null);
            survivalController = IsSurvivalChallenge
                ? Object.FindFirstObjectByType<StageSurvivalController>()
                : null;

            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetTitle(false);
            uiManager?.SetMulti(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetStageSelectLocked(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            RespawnPlayers();
            SetActivePlayer(player != null ? player : primaryPlayer, true);
        }

        public void OpenStageEditor(string stageId)
        {
            CancelRespawnAnimations();
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
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
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
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(true);
            uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        public bool StageSelectEditMode => stageSelectEditMode;

        public void SetStageSelectEditMode(bool editing)
        {
            stageSelectEditMode = editing;
        }

        public bool ToggleStageSelectEditMode()
        {
            stageSelectEditMode = !stageSelectEditMode;
            return stageSelectEditMode;
        }

        public void TestEditedStage()
        {
            if (stageEditor == null)
            {
                return;
            }

            SetEditedStageTestMode(true);
            NotebookBackgroundDoodles.SetWorldVisible(true);
            stageEditor.TestPlay();
            ConfigureStageRule(stageLoader != null ? stageLoader.CurrentStageData : null);
            survivalController = IsSurvivalChallenge
                ? Object.FindFirstObjectByType<StageSurvivalController>()
                : null;
            stageEditing = false;
            stageStarted = true;
            drawing = false;
            cleared = false;
            Time.timeScale = 1f;
            SetCameraFollowEnabled(true);
            uiManager?.SetStageEditor(false);
            uiManager?.SetStageSelect(false);
            uiManager?.SetDrawing(false);
            uiManager?.SetCleared(false);
            RespawnPlayers();
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
            player?.ResetMotion();
            secondaryPlayer?.ResetMotion();
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
            CancelRespawnAnimations();
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
            player?.ResetMotion();
            player?.SetControlsEnabled(false);
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
            SetActivePlayer(player != null ? player : primaryPlayer, true);
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

        private bool IsOnlineInStage()
        {
            return onlineManager != null
                && onlineManager.CurrentLobby != null
                && onlineManager.State == OnlineConnectionState.Playing;
        }

        private static string GetNextStageId(string stageId)
        {
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
        public Transform RemotePlayerTransform => secondaryPlayer != null ? secondaryPlayer.transform : null;
        public PlayerController2D RemotePlayerController => secondaryPlayer;
        public string RemotePlayerId => remotePlayerId;

        public Transform GetOnlinePlayerTransform(string playerId)
        {
            PlayerController2D controller = GetOnlinePlayerController(playerId);
            return controller != null ? controller.transform : null;
        }

        public PlayerController2D GetOnlinePlayerController(string playerId)
        {
            if (onlineManager != null && playerId == onlineManager.LocalPlayerId)
            {
                return primaryPlayer;
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

            return controller == primaryPlayer && onlineManager != null ? onlineManager.LocalPlayerId : null;
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
                || onlineManager != null && playerId == onlineManager.LocalPlayerId)
            {
                return null;
            }

            if (onlineRemotePlayers.TryGetValue(playerId, out PlayerController2D existing) && existing != null)
            {
                return existing;
            }

            PlayerController2D remote;
            if (secondaryPlayer == null)
            {
                AddCharacter();
                remote = secondaryPlayer;
            }
            else if (!onlineRemotePlayers.ContainsValue(secondaryPlayer))
            {
                remote = secondaryPlayer;
            }
            else
            {
                GameObject clone = Instantiate(
                    primaryPlayer.gameObject,
                    primaryPlayer.transform.position + new Vector3(onlineRemotePlayers.Count * 1.25f, 0.35f, 0f),
                    primaryPlayer.transform.rotation,
                    primaryPlayer.transform.parent);
                clone.name = "Online Player " + (onlineRemotePlayers.Count + 2);
                remote = clone.GetComponent<PlayerController2D>();
                if (remote == null)
                {
                    Destroy(clone);
                    return null;
                }
                remote.ResetMotion();
                remote.SetControlsEnabled(false);
            }

            if (remote == null)
            {
                return null;
            }

            remote.SetControlsEnabled(false);
            Rigidbody2D remoteBody = remote.GetComponent<Rigidbody2D>();
            if (remoteBody != null)
            {
                remoteBody.bodyType = RigidbodyType2D.Kinematic;
                remoteBody.gravityScale = 0f;
                remoteBody.freezeRotation = true;
                remoteBody.linearVelocity = Vector2.zero;
                remoteBody.angularVelocity = 0f;
            }
            onlineRemotePlayers[playerId] = remote;
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
                    if (info == null || string.IsNullOrEmpty(info.PlayerId) || info.PlayerId == localPlayerId)
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
                PlayerController2D remote = onlineRemotePlayers[id];
                onlineRemotePlayers.Remove(id);
                if (remote != null)
                {
                    if (remote == secondaryPlayer)
                    {
                        secondaryPlayer = null;
                    }
                    drawingStates.Remove(remote);
                    Destroy(remote.gameObject);
                }
            }
        }

        public void ApplyOnlineRemoteState(string playerId, Vector2 position, Vector2 velocity, float rotation)
        {
            PlayerController2D remote = EnsureOnlineRemotePlayer(playerId);
            if (remote == null)
            {
                return;
            }

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

            remote.SetControlsEnabled(false);
        }

        public void ApplyOnlineRemoteRedrawing(string playerId, bool redrawing)
        {
            PlayerController2D remote = EnsureOnlineRemotePlayer(playerId);
            if (remote == null)
            {
                return;
            }

            SetPlayerRedrawingState(remote, redrawing);
            if (redrawing && player != null)
            {
                PlayerCarryController localCarry = player.GetComponent<PlayerCarryController>();
                localCarry?.ReleaseIfHolding(remote.transform);
                localCarry?.ReleaseIfDraggingFriend(remote.transform);
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
            if (player == null || string.IsNullOrEmpty(playerId)
                || !onlineRemotePlayers.TryGetValue(playerId, out PlayerController2D remote)
                || remote == null)
            {
                return false;
            }
            PlayerCarryController carry = player.GetComponent<PlayerCarryController>();
            return carry != null && (carry.IsHoldingTarget(remote.transform)
                || carry.IsDraggingFriend(remote.transform));
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

            bool beginsCarry = carryData.Action == "pickup"
                || carryData.Action == "cat_grab"
                || carryData.Action == "friend_grab";
            if (beginsCarry && RejectOrResolveMutualOnlineCarry(carryData.CarrierPlayerId, localPlayerId))
            {
                return;
            }

            if (carryData.Action == "pickup")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, false, Vector2.zero);
            }
            else if (carryData.Action == "cat_grab")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, true, carryData.LocalOffset);
            }
            else if (carryData.Action == "friend_grab")
            {
                BeginOnlineCarry(carryData.CarrierPlayerId, true, carryData.LocalOffset);
            }
            else if (carryData.Action == "throw")
            {
                EndOnlineCarry(carryData.ReleaseVelocity);
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

        public void ReconcileOnlineCarryState(
            string carrierPlayerId,
            string carriedPlayerId,
            string carryAction,
            Vector2 carryOffset,
            string localPlayerId,
            Vector2 carrierVelocity)
        {
            if (string.IsNullOrEmpty(carrierPlayerId) || string.IsNullOrEmpty(localPlayerId))
            {
                return;
            }

            bool carrierClaimsLocal = carriedPlayerId == localPlayerId;
            if (carrierClaimsLocal
                && RejectOrResolveMutualOnlineCarry(carrierPlayerId, localPlayerId))
            {
                return;
            }
            if (onlineCarryHeld && onlineCarrierPlayerId == carrierPlayerId)
            {
                if (carrierClaimsLocal)
                {
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

            bool attachedCarry = carryAction == "cat_grab" || carryAction == "friend_grab";
            BeginOnlineCarry(carrierPlayerId, attachedCarry, carryOffset);
        }

        private bool RejectOrResolveMutualOnlineCarry(string incomingCarrierId, string localPlayerId)
        {
            if (player == null
                || string.IsNullOrEmpty(incomingCarrierId)
                || !onlineRemotePlayers.TryGetValue(incomingCarrierId, out PlayerController2D incomingCarrier)
                || incomingCarrier == null)
            {
                return false;
            }

            PlayerCarryController localCarry = player.GetComponent<PlayerCarryController>();
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

        private void BeginOnlineCarry(string carrierPlayerId, bool catGrab, Vector2 localOffset)
        {
            PlayerController2D carrier = EnsureOnlineRemotePlayer(carrierPlayerId);
            if (player == null || carrier == null || onlineCarryHeld)
            {
                return;
            }

            onlineCarryHeld = true;
            onlineCarrierPlayerId = carrierPlayerId;
            onlineCarryBeganAt = Time.unscaledTime;
            onlineCarryLastConfirmedAt = Time.unscaledTime;
            onlineCarryIsCatGrab = catGrab;
            onlineCarryLocalOffset = localOffset;
            player.SetControlsEnabled(catGrab && stageStarted && !drawing && !cleared && !stageEditing);
            onlineCarryBody = player.GetComponent<Rigidbody2D>();
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
            player.GetComponentsInChildren(onlineCarryColliders);
            for (int i = 0; i < onlineCarryColliders.Count; i++)
            {
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
            if (player == null || carrier == null)
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

            player.transform.position = anchor;
            if (!onlineCarryIsCatGrab)
            {
                player.transform.rotation = Quaternion.identity;
            }
            if (onlineCarryBody != null)
            {
                onlineCarryBody.position = anchor;
                onlineCarryBody.linearVelocity = Vector2.zero;
                onlineCarryBody.angularVelocity = 0f;
            }
        }

        private void EndOnlineCarry(Vector2 releaseVelocity)
        {
            if (!onlineCarryHeld)
            {
                return;
            }

            bool wasCatGrab = onlineCarryIsCatGrab;
            onlineCarryHeld = false;
            PlayerController2D carrier = GetOnlinePlayerController(onlineCarrierPlayerId);
            BodyBuilder remoteBuilder = carrier != null ? carrier.GetComponent<BodyBuilder>() : null;
            remoteBuilder?.SetCarryPose(false, carrier.FacingDirection, carrier.transform.position);
            Collider2D[] releasedColliders = onlineCarryColliders.ToArray();
            Collider2D[] carrierColliders = carrier != null
                ? carrier.GetComponentsInChildren<Collider2D>(false)
                : new Collider2D[0];

            for (int i = 0; i < releasedColliders.Length; i++)
            {
                if (releasedColliders[i] != null)
                {
                    releasedColliders[i].enabled = true;
                }
            }
            SetOnlineCarryCollisionIgnored(releasedColliders, carrierColliders, true);

            onlineCarryColliders.Clear();
            player?.ResetMotion();
            if (onlineCarryBody != null)
            {
                onlineCarryBody.bodyType = onlineCarryPreviousBodyType;
                onlineCarryBody.gravityScale = onlineCarryPreviousGravityScale;
                onlineCarryBody.freezeRotation = onlineCarryPreviousFreezeRotation;
                onlineCarryBody.linearVelocity = releaseVelocity;
                onlineCarryBody.angularVelocity = wasCatGrab ? 0f : releaseVelocity.x * -18f;
            }

            player?.SetControlsEnabled(stageStarted && !drawing && !cleared && !stageEditing);
            onlineCarryBody = null;
            onlineCarrierPlayerId = null;
            onlineCarryBeganAt = 0f;
            onlineCarryLastConfirmedAt = 0f;
            onlineCarryIsCatGrab = false;
            onlineCarryLocalOffset = Vector2.zero;
            StartCoroutine(RestoreOnlineCarryCollisions(releasedColliders, carrierColliders));
        }

        private static IEnumerator RestoreOnlineCarryCollisions(Collider2D[] released, Collider2D[] carrier)
        {
            float minimumRestoreAt = Time.time + 0.22f;
            float restoreDeadline = Time.time + 1.2f;
            while (Time.time < minimumRestoreAt
                || (Time.time < restoreDeadline && OnlineCarryCollidersOverlap(released, carrier)))
            {
                yield return new WaitForFixedUpdate();
            }

            SetOnlineCarryCollisionIgnored(released, carrier, false);
        }

        private static bool OnlineCarryCollidersOverlap(Collider2D[] released, Collider2D[] carrier)
        {
            for (int i = 0; i < released.Length; i++)
            {
                Collider2D first = released[i];
                if (first == null || !first.enabled)
                {
                    continue;
                }

                for (int j = 0; j < carrier.Length; j++)
                {
                    Collider2D second = carrier[j];
                    if (second != null && second.enabled && first != second && first.Distance(second).isOverlapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void SetOnlineCarryCollisionIgnored(Collider2D[] released, Collider2D[] carrier, bool ignored)
        {
            for (int i = 0; i < released.Length; i++)
            {
                Collider2D first = released[i];
                if (first == null)
                {
                    continue;
                }

                for (int j = 0; j < carrier.Length; j++)
                {
                    Collider2D second = carrier[j];
                    if (second != null && first != second)
                    {
                        Physics2D.IgnoreCollision(first, second, ignored);
                    }
                }
            }
        }

        public void ApplyOnlineRemoteBodyData(OnlineBodyData bodyData)
        {
            if (bodyData == null || drawManager == null || string.IsNullOrEmpty(bodyData.Json))
            {
                return;
            }

            PlayerController2D remotePlayer = EnsureOnlineRemotePlayer(bodyData.PlayerId);
            if (remotePlayer == null)
            {
                return;
            }

            if (onlineCarryHeld && onlineCarrierPlayerId == bodyData.PlayerId)
            {
                EndOnlineCarry(Vector2.zero);
            }
            player?.GetComponent<PlayerCarryController>()?.ReleaseIfHolding(remotePlayer.transform);
            player?.GetComponent<PlayerCarryController>()?.ReleaseIfDraggingFriend(remotePlayer.transform);

            DrawManager.DrawingState remoteState = drawManager.CreateStateFromBodyJson(bodyData.Json);
            if (remoteState == null)
            {
                return;
            }

            SaveDrawingState(player);
            BodyBuilder remoteBuilder = remotePlayer.GetComponent<BodyBuilder>();
            PlayerAbilityController remoteAbilities = remotePlayer.GetComponent<PlayerAbilityController>();
            drawManager.SetBuildTarget(remoteBuilder, remoteAbilities);
            drawManager.LoadState(remoteState, true);
            drawingStates[remotePlayer] = CloneDrawingState(remoteState);
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            remotePlayer.SetControlsEnabled(false);
            LiftPlayerOutOfGround(remotePlayer);
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

            drawManager.SendCurrentBodyData();
        }

        public void AddCharacter()
        {
            if (secondaryPlayer != null || primaryPlayer == null)
            {
                return;
            }

            SaveDrawingState(primaryPlayer);
            Vector3 offset = new Vector3(Mathf.Sign(primaryPlayer.FacingDirection) * 1.25f, 0.35f, 0f);
            GameObject clone = Instantiate(primaryPlayer.gameObject, primaryPlayer.transform.position + offset, primaryPlayer.transform.rotation, primaryPlayer.transform.parent);
            clone.name = "Player 2";
            secondaryPlayer = clone.GetComponent<PlayerController2D>();
            if (secondaryPlayer == null)
            {
                Destroy(clone);
                return;
            }

            secondaryPlayer.ResetMotion();
            secondaryPlayer.SetControlsEnabled(false);
            if (drawingStates.TryGetValue(primaryPlayer, out DrawManager.DrawingState state))
            {
                drawingStates[secondaryPlayer] = CloneDrawingState(state);
            }

            BodyBuilder bodyBuilder = secondaryPlayer.GetComponent<BodyBuilder>();
            if (bodyBuilder != null)
            {
                bodyBuilder.SetFacingDirection(primaryPlayer.FacingDirection);
                bodyBuilder.SetPlayerColor(PlayerColorPalette.GetColor(1));
            }

            ApplyDefaultPlayerColors();
            LiftPlayerOutOfGround(secondaryPlayer);
            RefreshControlledPlayerMarkers();
            drawManager?.RefreshInkBudgetDisplay();
        }

        public void DeleteAddedCharacter()
        {
            if (secondaryPlayer == null)
            {
                return;
            }

            if (player == secondaryPlayer)
            {
                SetActivePlayer(primaryPlayer, true);
            }

            primaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            secondaryPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();

            GameObject target = secondaryPlayer.gameObject;
            drawingStates.Remove(secondaryPlayer);
            secondaryPlayer = null;
            Destroy(target);
            RefreshControlledPlayerMarkers();
            drawManager?.RefreshInkBudgetDisplay();
        }

        public int GetInkBudgetPlayerCount()
        {
            if (IsOnlineInStage())
            {
                return onlineManager != null ? onlineManager.GetInkBudgetPlayerCount() : 1;
            }

            int count = 0;
            if (IsActiveBudgetPlayer(primaryPlayer))
            {
                count++;
            }
            if (secondaryPlayer != primaryPlayer && IsActiveBudgetPlayer(secondaryPlayer))
            {
                count++;
            }
            return Mathf.Max(1, count);
        }

        public float GetConfirmedInkExcludingActivePlayer()
        {
            if (IsOnlineInStage())
            {
                return onlineManager != null ? onlineManager.GetConfirmedInkExcludingLocal() : 0f;
            }

            float total = 0f;
            if (primaryPlayer != player && IsActiveBudgetPlayer(primaryPlayer))
            {
                total += GetConfirmedPlayerInk(primaryPlayer);
            }
            if (secondaryPlayer != player
                && secondaryPlayer != primaryPlayer
                && IsActiveBudgetPlayer(secondaryPlayer))
            {
                total += GetConfirmedPlayerInk(secondaryPlayer);
            }
            return total;
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
            if (secondaryPlayer == null || primaryPlayer == null)
            {
                return;
            }

            SetActivePlayer(player == secondaryPlayer ? primaryPlayer : secondaryPlayer, true);
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
            if (returnToStart && spawnPoint != null)
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
            hasRedrawReturnPosition = false;
            redrawReturnPlayer = null;
            redrawPlayer.SetControlsEnabled(false);
            redrawPlayer.ResetMotion();
            redrawRespawnRoutine = StartCoroutine(CompleteRedrawRespawn(redrawPlayer, returnPosition));
        }

        private IEnumerator CompleteRedrawRespawn(PlayerController2D redrawPlayer, Vector3 returnPosition)
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
                LiftPlayerOutOfGround(redrawPlayer);
                redrawPlayer.SetControlsEnabled(
                    stageStarted && !drawing && !cleared && !stageEditing);
            }

            redrawRespawnRoutine = null;
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
            }
        }

        private void RespawnPlayers()
        {
            ResetAllCarryState();
            RespawnPlayer(primaryPlayer, GetRespawnOffset(primaryPlayer), primaryPlayer == player);
            RespawnPlayer(secondaryPlayer, GetRespawnOffset(secondaryPlayer), secondaryPlayer == player);
        }

        private void ResetAllCarryState()
        {
            player?.GetComponent<PlayerCarryController>()?.ForceDrop();
            secondaryPlayer?.GetComponent<PlayerCarryController>()?.ForceDrop();
            if (onlineCarryHeld)
            {
                EndOnlineCarry(Vector2.zero);
            }
        }

        public void RespawnFromHazard(PlayerController2D targetPlayer)
        {
            if (!stageStarted || cleared || targetPlayer == null)
            {
                return;
            }

            if (IsSurvivalChallenge && survivalController != null)
            {
                survivalController.RequestElimination(targetPlayer);
                return;
            }

            // Remote avatars are visual replicas. Their owning client performs the
            // respawn and the regular player-state sync sends the new position.
            if (IsOnlineInStage() && targetPlayer != primaryPlayer)
            {
                return;
            }

            BeginRespawnAnimation(targetPlayer, true);
        }

        private void RespawnFallenPlayers()
        {
            RespawnIfFallen(primaryPlayer);
            if (!IsOnlineInStage())
            {
                RespawnIfFallen(secondaryPlayer);
            }
        }

        private void RespawnIfFallen(PlayerController2D targetPlayer)
        {
            if (targetPlayer == null)
            {
                return;
            }

            float resetY = fallResetY;
            if (stageLoader != null && stageLoader.TryGetStageFallBoundaryY(out float stageBoundaryY))
            {
                resetY = stageBoundaryY - LowestStageObjectFallMargin;
            }

            if (targetPlayer.transform.position.y >= resetY)
            {
                return;
            }

            if (IsSurvivalChallenge && survivalController != null)
            {
                survivalController.RequestElimination(targetPlayer);
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

            targetPlayer.GetComponent<PlayerCarryController>()?.ForceDrop();
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
                Body = body,
                BodyWasSimulated = body == null || body.simulated
            };
            respawnAnimations[targetPlayer] = state;
            state.Routine = StartCoroutine(PlayRespawnAnimation(targetPlayer, state));
        }

        private IEnumerator PlayRespawnAnimation(PlayerController2D targetPlayer, RespawnAnimationState state)
        {
            Color effectColor = GetPlayerEffectColor(targetPlayer);
            CreateRespawnBurst(targetPlayer.transform.position, effectColor);
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
            yield return new WaitForSecondsRealtime(RespawnPauseDuration);

            if (targetPlayer == null || !stageStarted || cleared)
            {
                RestoreRespawnAnimationState(targetPlayer, state);
                respawnAnimations.Remove(targetPlayer);
                yield break;
            }

            // Keep physics disabled through the teleport. Re-enabling before the
            // position change can make continuous collision/interpolation touch
            // every object between the death point and the spawn point.
            RespawnPlayer(targetPlayer, GetRespawnOffset(targetPlayer), false, false);
            if (state.Body != null)
            {
                state.Body.simulated = state.BodyWasSimulated;
            }
            Physics2D.SyncTransforms();
            LiftPlayerOutOfGround(targetPlayer);
            CreateRespawnBurst(targetPlayer.transform.position, effectColor);

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
                targetPlayer.ResetMotion();
                targetPlayer.SetControlsEnabled(
                    targetPlayer == player && stageStarted && !drawing && !cleared && !stageEditing);
                respawnGraceUntil[targetPlayer] = Time.unscaledTime + RespawnGraceDuration;
            }

            respawnAnimations.Remove(targetPlayer);
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

        private void CreateRespawnBurst(Vector3 position, Color color)
        {
            GameObject burst = new GameObject("Respawn Doodle Burst");
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
            StartCoroutine(AnimateRespawnBurst(burst, lines, color));
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

        private IEnumerator AnimateRespawnBurst(GameObject burst, LineRenderer[] lines, Color color)
        {
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration && burst != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                burst.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.45f, eased);
                Color faded = color;
                faded.a *= 1f - progress;
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
            bool resolveGroundOverlap = true)
        {
            if (targetPlayer == null || spawnPoint == null)
            {
                return;
            }

            TeleportPlayerWithoutPhysics(targetPlayer, spawnPoint.position + offset);
            targetPlayer.ResetMotion();
            targetPlayer.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared && !stageEditing);
            if (resolveGroundOverlap)
            {
                LiftPlayerOutOfGround(targetPlayer);
            }
            if (stageStarted)
            {
                GameSfx.PlayAt(SfxId.PlayerRespawn, targetPlayer.transform.position);
            }
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
            return targetPlayer != null && targetPlayer == secondaryPlayer ? new Vector3(1.25f, 0f, 0f) : Vector3.zero;
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
                player.SetControlsEnabled(false);
            }

            player = nextPlayer;
            ConfigureActivePlayerTargets();
            LoadDrawingState(player);
            player.SetControlsEnabled(enableControls && stageStarted && !drawing && !cleared && !stageEditing);
        }

        private void SetAllPlayerControls(bool enabled)
        {
            primaryPlayer?.SetControlsEnabled(enabled);
            secondaryPlayer?.SetControlsEnabled(enabled);
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
            bool showMarker = primaryPlayer != null && secondaryPlayer != null;
            SetControlledPlayerMarker(primaryPlayer, showMarker && player == primaryPlayer);
            SetControlledPlayerMarker(secondaryPlayer, showMarker && player == secondaryPlayer);
        }

        private static void SetControlledPlayerMarker(PlayerController2D targetPlayer, bool controlled)
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

            marker.SetControlled(controlled);
        }

        public void ApplyDefaultPlayerColors()
        {
            SetPlayerColor(primaryPlayer, 0);
            SetPlayerColor(secondaryPlayer, 1);
        }

        public void ApplyOnlinePlayerColors(OnlineLobbyInfo lobby, string localPlayerId, string remotePlayerId)
        {
            int localIndex = PlayerColorPalette.GetLobbyColorIndex(lobby, localPlayerId, 0);
            int remoteIndex = PlayerColorPalette.GetLobbyColorIndex(lobby, remotePlayerId, 1);
            SetPlayerColor(primaryPlayer, localIndex);
            SetPlayerColor(secondaryPlayer, remoteIndex);
            foreach (KeyValuePair<string, PlayerController2D> pair in onlineRemotePlayers)
            {
                int index = PlayerColorPalette.GetLobbyColorIndex(lobby, pair.Key, 1);
                SetPlayerColor(pair.Value, index);
            }
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

        private void ApplySpeciesRulesForCurrentStage()
        {
            if (drawManager == null)
            {
                return;
            }

            StageSpeciesMask availability = StageSpeciesRules.GetAllowedForStage(currentStageId);
            drawManager.SetAllowedSpecies(availability);
            DrawManager.Species fallback = StageSpeciesRules.GetFirstAllowed(availability);

            if (player != null)
            {
                SaveDrawingState(player);
            }

            foreach (KeyValuePair<PlayerController2D, DrawManager.DrawingState> entry in drawingStates)
            {
                if (entry.Value != null && !StageSpeciesRules.IsAllowed(availability, entry.Value.Species))
                {
                    entry.Value.Species = fallback;
                    entry.Value.Part = DrawManager.BodyPart.Torso;
                }
            }

            List<PlayerController2D> playersToRefresh = new List<PlayerController2D>();
            AddPlayerOnce(playersToRefresh, primaryPlayer);
            AddPlayerOnce(playersToRefresh, secondaryPlayer);
            foreach (KeyValuePair<string, PlayerController2D> remote in onlineRemotePlayers)
            {
                AddPlayerOnce(playersToRefresh, remote.Value);
            }

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
            }

            return clone;
        }

        private void SetCameraFollowEnabled(bool enabled)
        {
            if (cameraFollow != null)
            {
                cameraFollow.enabled = enabled;
            }
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
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshText;
        }

        public void SetControlled(bool value)
        {
            controlled = value;
            SetRenderersVisible(controlled && controller != null && controller.ControlsEnabled);
        }

        private void LateUpdate()
        {
            bool visible = controlled && controller != null && controller.ControlsEnabled;
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
            string marker = LocalizationManager.T("player_controlled_marker") + "\n▼";
            mainText.text = marker;
            shadowText.text = marker;
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
}
