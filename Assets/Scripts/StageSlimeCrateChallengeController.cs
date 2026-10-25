using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Stage 5-3: a three-round, slime-only crate breaking challenge.
    /// The host owns round progress while every client can request the crate hit
    /// produced by its locally controlled slime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSlimeCrateChallengeController : StageEliminationChallengeController,
        IStageValueCrateOwner
    {
        private const string StageId = "5-3";
        private const string StateKind = "slime_crate_state";
        private const string BreakRequestKind = "slime_crate_break_request";
        private const string BreakKind = "slime_crate_break";
        private const string SpawnKind = "slime_crate_spawn";
        private const string EnemyLaunchKind = "slime_crate_enemy_launch";
        private const string EliminateRequestKind = "slime_crate_eliminate_request";
        private const string EliminatedKind = "slime_crate_eliminated";
        private const float RoundSeconds = 45f;
        private const int CratesPerPlayer = 100;
        private const float SpringLaunchVelocity = 47.5f;
        private const float CrateLaunchInterval = 0.11f;
        private const float EnemyLaunchInterval = 3f;
        private const float CountdownSeconds = 3f;
        private const float BaseArenaHalfWidth = 17.8f;
        private const float ArenaBottom = -9.2f;
        private const float ArenaTop = 9.2f;
        private const float RedrawFloorY = -0.9f;
        private static readonly StageObjectType[] MixedCrateTypes =
        {
            StageObjectType.WoodBox,
            StageObjectType.TriangleBox,
            StageObjectType.Ball,
            StageObjectType.Barrel,
            StageObjectType.RubberBox,
            StageObjectType.IronBox
        };

        private enum ChallengePhase
        {
            Countdown,
            Playing,
            RoundClear,
            Failed,
            Complete
        }

        [System.Serializable]
        private sealed class ChallengeState
        {
            public int Sequence;
            public int Round;
            public int Attempt;
            public int Phase;
            public int PlayerCount;
            public int TargetCount;
            public int SpawnedCount;
            public int SpawnedEnemyCount;
            public float Remaining;
            public float PhaseRemaining;
            public string[] BrokenIds;
            public string[] EliminatedIds;
        }

        [System.Serializable]
        private sealed class BreakMessage
        {
            public int Round;
            public int Attempt;
            public string CrateId;
            public Vector2 HitPoint;
        }

        [System.Serializable]
        private sealed class SpawnMessage
        {
            public int Round;
            public int Attempt;
            public int CrateIndex;
        }

        [System.Serializable]
        private sealed class EnemyLaunchMessage
        {
            public int Round;
            public int Attempt;
            public int EnemyIndex;
            public int LauncherIndex;
        }

        [System.Serializable]
        private sealed class PlayerMessage
        {
            public int Round;
            public int Attempt;
            public string PlayerId;
        }

        private readonly Dictionary<string, StageValueCrate> crates =
            new Dictionary<string, StageValueCrate>();
        private readonly HashSet<string> brokenIds = new HashSet<string>();
        private readonly HashSet<string> eliminatedIds = new HashSet<string>();
        private readonly HashSet<string> pendingBreakIds = new HashSet<string>();
        private readonly HashSet<string> pendingEliminationIds = new HashSet<string>();
        private readonly List<string> participantIds = new List<string>();
        private readonly List<PlayerController2D> hiddenPlayers = new List<PlayerController2D>();
        private readonly List<Transform> synchronizedObjects = new List<Transform>();
        private readonly List<Transform> crateLaunchers = new List<Transform>();
        private readonly List<string> spawnedEnemyIds = new List<string>();

        private StageManager stageManager;
        private StageLoader stageLoader;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private CameraFollow2D cameraFollow;
        private Transform arenaRoot;
        private readonly List<TextMesh> monitorMainTexts = new List<TextMesh>();
        private readonly List<TextMesh> monitorSubTexts = new List<TextMesh>();
        private ChallengePhase phase;
        private float remaining;
        private float phaseRemaining;
        private float nextStateAt;
        private float nextCrateLaunchAt;
        private float nextEnemyLaunchAt;
        private float previousCameraMinimum;
        private int round = 1;
        private int roundAttempt;
        private int playerCount = 1;
        private int targetCount = CratesPerPlayer;
        private int spawnedCount;
        private int spawnedEnemyCount;
        private int stateSequence;
        private int lastStateSequence;
        private bool initialized;
        private bool cameraMinimumChanged;

        public override bool UsesGlobalFallBoundary => false;
        private string RoundObjectPrefix => "5-3_r" + round + "_a" + roundAttempt;
        private float ArenaHalfWidth => BaseArenaHalfWidth * (1f + 0.16f * (playerCount - 1));
        private float RedrawFloorWidth => 10f + 4f * (playerCount - 1);
        private int ConcurrentCrateLimit => Mathf.Min(targetCount, 100);
        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            objectFactory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            uiManager?.SetChallengeCountdown(false, string.Empty);
            RestoreHiddenPlayers();
            if (cameraMinimumChanged && cameraFollow != null)
                cameraFollow.SetMinimumOrthographicSize(previousCameraMinimum);
        }

        private void Start()
        {
            EnsureInitializedForPlay();
        }

        public void EnsureInitializedForPlay()
        {
            if (initialized) return;
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            enabled = true;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject obsolete = transform.GetChild(i).gameObject;
                obsolete.SetActive(false);
                Destroy(obsolete);
            }

            if (cameraFollow != null)
            {
                previousCameraMinimum = cameraFollow.MinimumOrthographicSize;
                cameraFollow.SetMinimumOrthographicSize(11.8f);
                cameraMinimumChanged = true;
            }

            initialized = true;
            BeginRound(1, 0);
        }

        private void Update()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;

            CheckOutOfBounds();
            if (!HasAuthority)
            {
                AdvanceReplicaClock();
                ApplyLocalControls();
                RefreshMonitor();
                return;
            }

            switch (phase)
            {
                case ChallengePhase.Countdown:
                    phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
                    ApplyLocalControls();
                    ShowCountdown();
                    if (phaseRemaining <= 0f)
                    {
                        phase = ChallengePhase.Playing;
                        phaseRemaining = 0.65f;
                        if (round == 3)
                            nextEnemyLaunchAt = Time.unscaledTime + EnemyLaunchInterval;
                        uiManager?.SetChallengeCountdown(true, LocalizationManager.T("survival_start"));
                        BroadcastState(true);
                    }
                    break;

                case ChallengePhase.Playing:
                    ApplyLocalControls();
                    ReplenishCrates();
                    LaunchScheduledEnemy();
                    if (phaseRemaining > 0f)
                    {
                        phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
                        if (phaseRemaining <= 0f)
                            uiManager?.SetChallengeCountdown(false, string.Empty);
                    }
                    if (!LocalMultiplayerDebugMode.NoTimeLimit)
                        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    if (brokenIds.Count >= targetCount)
                        CompleteRound();
                    else if (AreAllPlayersEliminated()
                        || !LocalMultiplayerDebugMode.NoTimeLimit && remaining <= 0f)
                        FailRound();
                    break;

                case ChallengePhase.RoundClear:
                    phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
                    if (phaseRemaining <= 0f) BeginRound(round + 1, 0);
                    break;

                case ChallengePhase.Failed:
                    phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
                    if (phaseRemaining <= 0f) BeginRound(round, 0);
                    break;

                case ChallengePhase.Complete:
                    phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
                    if (phaseRemaining <= 0f) stageManager.ClearStage();
                    break;
            }

            BroadcastState();
            RefreshMonitor();
        }

        public void RequestBreak(StageValueCrate crate, Vector2 hitPoint)
        {
            if (crate == null || phase != ChallengePhase.Playing || crate.IsBroken) return;
            if (!IsCurrentRoundCrate(crate.CrateId)) return;
            if (!HasAuthority)
            {
                if (!pendingBreakIds.Add(crate.CrateId)) return;
                Send(BreakRequestKind, new BreakMessage
                {
                    Round = round,
                    Attempt = roundAttempt,
                    CrateId = crate.CrateId,
                    HitPoint = hitPoint
                });
                return;
            }
            ApplyBreak(crate.CrateId, hitPoint, true);
        }

        public override void RequestElimination(PlayerController2D target)
        {
            if (target == null || phase != ChallengePhase.Playing) return;
            string id = ResolvePlayerId(target);
            if (string.IsNullOrEmpty(id) || eliminatedIds.Contains(id)) return;

            if (IsOnline)
            {
                if (onlineManager == null
                    || !OnlinePlayerIdentity.IsOwnedByConnection(id, onlineManager.LocalPlayerId)) return;
                if (!HasAuthority)
                {
                    if (!pendingEliminationIds.Add(id)) return;
                    Send(EliminateRequestKind, new PlayerMessage
                    {
                        Round = round,
                        Attempt = roundAttempt,
                        PlayerId = id
                    });
                    return;
                }
            }
            ConfirmElimination(id);
        }

        private void BeginRound(int nextRound, int authoritativePlayerCount, int authoritativeAttempt = 0)
        {
            if (stageManager != null && stageManager.IsDrawingMode)
                stageManager.CancelDrawingMode();
            round = Mathf.Clamp(nextRound, 1, 3);
            roundAttempt = authoritativeAttempt > 0 ? authoritativeAttempt : roundAttempt + 1;
            RefreshParticipants();
            playerCount = authoritativePlayerCount > 0
                ? Mathf.Clamp(authoritativePlayerCount, 1, 4)
                : ResolvePlayerCount();
            targetCount = CratesPerPlayer * playerCount;
            phase = ChallengePhase.Countdown;
            remaining = RoundSeconds;
            phaseRemaining = CountdownSeconds;
            brokenIds.Clear();
            eliminatedIds.Clear();
            pendingBreakIds.Clear();
            pendingEliminationIds.Clear();
            spawnedCount = 0;
            spawnedEnemyCount = 0;
            nextCrateLaunchAt = 0f;
            nextEnemyLaunchAt = 0f;
            ClearArena();
            BuildArena();
            ReviveAndPlacePlayers();
            ApplyLocalControls();
            ShowCountdown();
            BroadcastState(true);
            RefreshMonitor();
        }

        private void BuildArena()
        {
            GameObject arena = new GameObject("5-3 Slime Crate Arena Round " + round);
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;

            CreateBackdrop();
            CreateSolid("floor", StageObjectType.Platform,
                new Vector2(0f, ArenaBottom), new Vector2(ArenaHalfWidth * 2f, 0.75f));
            CreateSolid("ceiling", StageObjectType.Ceiling,
                new Vector2(0f, ArenaTop), new Vector2(ArenaHalfWidth * 2f, 0.75f));
            CreateSolid("left_wall", StageObjectType.Wall,
                new Vector2(-ArenaHalfWidth, 0f), new Vector2(0.75f, ArenaTop - ArenaBottom));
            CreateSolid("right_wall", StageObjectType.Wall,
                new Vector2(ArenaHalfWidth, 0f), new Vector2(0.75f, ArenaTop - ArenaBottom));

            CreateSolid("redraw_floor", StageObjectType.OneWayPlatform,
                new Vector2(0f, RedrawFloorY), new Vector2(RedrawFloorWidth, 0.65f));
            StageRedrawZoneFactory.CreateRuntimeFloorZone(
                arenaRoot,
                RoundObjectPrefix + "_redraw_zone",
                new Vector2(0f, RedrawFloorY),
                RedrawFloorWidth - 0.3f,
                3.1f,
                0.65f);
            stageLoader?.SetRuntimeSpawnPosition(new Vector2(0f, RedrawFloorY + 1.2f));

            CreateInwardSprings();
            CreateCrateLaunchers();
            CreateCrates();
            CreateMonitor();
        }

        private void CreateBackdrop()
        {
            StageObjectData boundary = StageObjectFactory.CreateDefaultData(
                StageObjectType.StageBoundary,
                Vector2.zero);
            boundary.objectId = RoundObjectPrefix + "_factory_backdrop_bounds";
            boundary.size = new Vector2(ArenaHalfWidth * 2f, 23.8f);
            objectFactory?.CreateFactoryStageBackdrop(
                StageId,
                new[] { boundary },
                arenaRoot);
        }

        private void CreateSolid(string suffix, StageObjectType type, Vector2 position, Vector2 size)
        {
            if (objectFactory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = RoundObjectPrefix + "_" + suffix;
            data.size = size;
            data.keepSeparate = true;
            objectFactory.Create(data, arenaRoot);
        }

        private void CreateInwardSprings()
        {
            int horizontalCount = Mathf.Max(16, Mathf.CeilToInt(ArenaHalfWidth * 2f / 2.2f));
            for (int i = 0; i < horizontalCount; i++)
            {
                float x = Mathf.Lerp(-ArenaHalfWidth + 1.18f, ArenaHalfWidth - 1.18f,
                    i / (horizontalCount - 1f));
                CreateSpring("bottom_" + i, new Vector2(x, ArenaBottom + 0.72f), 0f);
                CreateSpring("top_" + i, new Vector2(x, ArenaTop - 0.72f), 180f);
            }

            const int verticalCount = 8;
            for (int i = 0; i < verticalCount; i++)
            {
                float y = Mathf.Lerp(ArenaBottom + 1.18f, ArenaTop - 1.18f,
                    i / (verticalCount - 1f));
                CreateSpring("left_" + i, new Vector2(-ArenaHalfWidth + 0.72f, y), -90f);
                CreateSpring("right_" + i, new Vector2(ArenaHalfWidth - 0.72f, y), 90f);
            }
        }

        private void CreateSpring(string suffix, Vector2 position, float rotation)
        {
            if (objectFactory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.Spring, position);
            data.objectId = RoundObjectPrefix + "_spring_" + suffix;
            data.size = new Vector2(2.35f, 0.82f);
            data.rotation = rotation;
            data.actionStrength = SpringLaunchVelocity;
            objectFactory.Create(data, arenaRoot);
        }

        private void CreateCrateLaunchers()
        {
            if (objectFactory == null) return;
            int launcherCount = Mathf.Clamp(playerCount + 1, 2, 4);
            for (int i = 0; i < launcherCount; i++)
            {
                float x = launcherCount == 1
                    ? 0f
                    : Mathf.Lerp(-ArenaHalfWidth * 0.62f, ArenaHalfWidth * 0.62f,
                        i / (launcherCount - 1f));
                StageObjectData data = StageObjectFactory.CreateDefaultData(
                    StageObjectType.BoxDropper,
                    new Vector2(x, ArenaTop - 2.15f));
                data.objectId = RoundObjectPrefix + "_crate_launcher_" + i.ToString("D2");
                data.size = new Vector2(2.8f, 1.9f);
                data.actionStrength = 10f;
                data.spawnPattern = 1;
                data.spawnBoxSize = 2f;
                GameObject launcher = objectFactory.Create(data, arenaRoot);
                if (launcher == null) continue;

                launcher.GetComponent<StageBoxDropper>()?.ConfigureManualDispense(1);
                crateLaunchers.Add(launcher.transform);

                Color arrow = new Color(1f, 0.7f, 0.08f, 0.98f);
                StageEscortController.AddLine(
                    launcher.transform,
                    new Vector2(0f, -1.0f),
                    new Vector2(0f, -1.72f),
                    0.15f,
                    arrow,
                    36);
                StageEscortController.AddLine(
                    launcher.transform,
                    new Vector2(0f, -1.72f),
                    new Vector2(-0.32f, -1.38f),
                    0.15f,
                    arrow,
                    36);
                StageEscortController.AddLine(
                    launcher.transform,
                    new Vector2(0f, -1.72f),
                    new Vector2(0.32f, -1.38f),
                    0.15f,
                    arrow,
                    36);

                GameObject beacon = new GameObject("Crate Launcher Beacon");
                beacon.transform.SetParent(launcher.transform, false);
                beacon.transform.localPosition = new Vector3(0f, 0.72f, -0.08f);
                beacon.transform.localScale = Vector3.one * 0.34f;
                SpriteRenderer beaconRenderer = beacon.AddComponent<SpriteRenderer>();
                beaconRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
                beaconRenderer.color = new Color(1f, 0.28f, 0.08f, 0.95f);
                beaconRenderer.sortingOrder = 37;
            }
        }

        private void CreateCrates()
        {
            if (objectFactory == null) return;
            List<Vector2> positions = BuildCratePositions();
            int initialCount = Mathf.Min(ConcurrentCrateLimit, positions.Count);
            for (int i = 0; i < initialCount; i++)
                if (!CreateCrate(i, positions[i], Vector2.zero)) break;
        }

        private bool CreateCrate(int index, Vector2 position, Vector2 launchVelocity)
        {
            if (objectFactory == null || arenaRoot == null || index < 0 || index >= targetCount)
                return false;

            string id = RoundObjectPrefix + "_crate_" + index.ToString("D3");
            if (crates.ContainsKey(id))
            {
                spawnedCount = Mathf.Max(spawnedCount, index + 1);
                return true;
            }

            StageObjectType type = ResolveCrateType(index);
            float size = ResolveCrateSize(index);
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = id;
            data.size = Vector2.one * size;
            data.rotation = type == StageObjectType.TriangleBox && (index & 1) == 1
                ? 180f
                : (index % 9 - 4) * 2f;
            GameObject crateObject = objectFactory.Create(data, arenaRoot);
            if (crateObject == null) return false;

            Rigidbody2D body = crateObject.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.gravityScale = 0.72f;
                body.mass = Mathf.Max(0.7f, size * size * 1.7f);
                // Large launched crates need swept collision detection so they
                // cannot tunnel through an arena wall between fixed updates.
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.sleepMode = RigidbodySleepMode2D.StartAwake;
                body.linearVelocity = launchVelocity;
                body.WakeUp();
            }

            StageValueCrate crate = crateObject.GetComponent<StageValueCrate>();
            if (crate == null) crate = crateObject.AddComponent<StageValueCrate>();
            crate.Configure(this, id, 0);
            crates[id] = crate;
            spawnedCount = Mathf.Max(spawnedCount, index + 1);
            RegisterSynchronizedObject(crateObject.transform);
            return true;
        }

        private StageObjectType ResolveCrateType(int index)
        {
            if (round != 2) return StageObjectType.WoodBox;
            return MixedCrateTypes[(index * 5 + index / 7) % MixedCrateTypes.Length];
        }

        private float ResolveCrateSize(int index)
        {
            return (round == 1
                ? 0.72f + (index * 17 % 7) * 0.065f
                : round == 2
                    ? 0.66f + (index * 11 % 8) * 0.07f
                    : 0.5f + (index * 13 % 5) * 0.045f) * 2f;
        }

        private bool SpawnLaunchedCrate(int index, bool broadcast, bool animate)
        {
            ResolveLaunch(index, out Transform launcher, out Vector2 position, out Vector2 velocity);
            if (!CreateCrate(index, position, velocity)) return false;

            if (animate && launcher != null)
                launcher.GetComponent<StageDropperVisualAnimator>()?.PlayDispense();
            if (broadcast)
            {
                Send(SpawnKind, new SpawnMessage
                {
                    Round = round,
                    Attempt = roundAttempt,
                    CrateIndex = index
                });
            }
            return true;
        }

        private void ResolveLaunch(
            int index,
            out Transform launcher,
            out Vector2 position,
            out Vector2 velocity)
        {
            launcher = crateLaunchers.Count > 0
                ? crateLaunchers[(index + round + roundAttempt) % crateLaunchers.Count]
                : null;
            float size = ResolveCrateSize(index);
            position = launcher != null
                ? (Vector2)launcher.position + Vector2.down * (1.15f + size * 0.58f)
                : new Vector2(0f, ArenaTop - 3f);
            float sideSpeed = (((index * 37 + round * 11) % 9) - 4) * 0.55f;
            velocity = new Vector2(sideSpeed, -7.5f - index % 3 * 0.8f);
        }

        private void ReplenishCrates()
        {
            if (!HasAuthority || phase != ChallengePhase.Playing
                || Time.unscaledTime < nextCrateLaunchAt
                || spawnedCount >= targetCount
                || spawnedCount - brokenIds.Count >= ConcurrentCrateLimit)
                return;

            int nextIndex = spawnedCount;
            if (SpawnLaunchedCrate(nextIndex, true, true))
                nextCrateLaunchAt = Time.unscaledTime + CrateLaunchInterval;
        }

        private void EnsureSpawnedCount(int requiredCount, bool animateLast)
        {
            int clamped = Mathf.Clamp(requiredCount, 0, targetCount);
            while (spawnedCount < clamped)
            {
                int nextIndex = spawnedCount;
                if (!SpawnLaunchedCrate(nextIndex, false, animateLast && nextIndex == clamped - 1))
                    break;
            }
        }

        private List<Vector2> BuildCratePositions()
        {
            // Only the concurrent pool is laid out at round start. Replacements
            // are visibly launched from the factory chutes as crates are broken.
            List<Vector2> positions = new List<Vector2>(520);
            int columns = Mathf.Max(32, Mathf.CeilToInt(32f * ArenaHalfWidth / BaseArenaHalfWidth));
            const int rows = 18;
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                float y = Mathf.Lerp(-7.15f, 7.1f, rowIndex / (rows - 1f));
                for (int column = 0; column < columns; column++)
                {
                    float x = Mathf.Lerp(-ArenaHalfWidth + 3.15f, ArenaHalfWidth - 3.15f,
                        column / (columns - 1f));
                    if (Mathf.Abs(x) < RedrawFloorWidth * 0.5f + 0.65f
                        && y > RedrawFloorY - 1.1f && y < RedrawFloorY + 3.25f)
                        continue;
                    float jitterX = (((column * 19 + rowIndex * 7 + round * 3) % 7) - 3) * 0.035f;
                    float jitterY = (((column * 5 + rowIndex * 13 + round) % 5) - 2) * 0.035f;
                    positions.Add(new Vector2(x + jitterX, y + jitterY));
                }
            }

            uint state = (uint)(0x53C7 + round * 7919 + playerCount * 104729);
            for (int i = positions.Count - 1; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int swap = (int)(state % (uint)(i + 1));
                Vector2 value = positions[i];
                positions[i] = positions[swap];
                positions[swap] = value;
            }
            return positions;
        }

        private void LaunchScheduledEnemy()
        {
            if (!HasAuthority || round != 3 || phase != ChallengePhase.Playing
                || Time.unscaledTime < nextEnemyLaunchAt)
                return;

            int enemyIndex = spawnedEnemyCount;
            int launcherIndex = ResolveEnemyLauncherIndex(enemyIndex);
            if (SpawnLaunchedEnemy(enemyIndex, launcherIndex))
            {
                nextEnemyLaunchAt = Time.unscaledTime + EnemyLaunchInterval;
                Send(EnemyLaunchKind, new EnemyLaunchMessage
                {
                    Round = round,
                    Attempt = roundAttempt,
                    EnemyIndex = enemyIndex,
                    LauncherIndex = launcherIndex
                });
            }
        }

        private bool SpawnLaunchedEnemy(int enemyIndex, int launcherIndex)
        {
            if (objectFactory == null || enemyIndex < 0) return false;

            Transform launcher = crateLaunchers.Count > 0
                ? crateLaunchers[Mathf.Abs(launcherIndex) % crateLaunchers.Count]
                : null;
            StageObjectType type = enemyIndex % 2 == 0
                ? StageObjectType.EnemyWalker
                : StageObjectType.EnemyJumper;
            Vector2 position = launcher != null
                ? (Vector2)launcher.position + Vector2.down * 1.72f
                : new Vector2(0f, ArenaTop - 3f);
            float facing = enemyIndex % 2 == 0 ? 1f : -1f;
            Vector2 launchVelocity = new Vector2(facing * (1.6f + enemyIndex % 3 * 0.35f), -7.5f);
            string id = RoundObjectPrefix + "_launched_enemy_" + enemyIndex.ToString("D2");
            float speed = 1.65f + enemyIndex % 4 * 0.12f;

            GameObject enemy;
            if (syncManager != null)
            {
                enemy = syncManager.SpawnDropperEnemy(
                    id, type, position, 0.82f, speed, facing, launchVelocity);
            }
            else
            {
                StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
                data.objectId = id;
                data.size = Vector2.one * 0.82f;
                data.movementSpeed = speed;
                data.rotation = facing < 0f ? 180f : 0f;
                enemy = objectFactory.Create(data, arenaRoot);
                Rigidbody2D body = enemy != null ? enemy.GetComponent<Rigidbody2D>() : null;
                if (body != null) body.linearVelocity = launchVelocity;
                if (enemy != null) RegisterSynchronizedObject(enemy.transform);
            }

            if (enemy == null) return false;
            if (!spawnedEnemyIds.Contains(id)) spawnedEnemyIds.Add(id);
            spawnedEnemyCount = Mathf.Max(spawnedEnemyCount, enemyIndex + 1);
            launcher?.GetComponent<StageDropperVisualAnimator>()?.PlayDispense();
            return true;
        }

        private int ResolveEnemyLauncherIndex(int enemyIndex)
        {
            return crateLaunchers.Count > 0
                ? (enemyIndex * 3 + roundAttempt) % crateLaunchers.Count
                : 0;
        }

        private void PlayEnemyLaunchVisual(int launcherIndex)
        {
            if (crateLaunchers.Count == 0) return;
            Transform launcher = crateLaunchers[Mathf.Abs(launcherIndex) % crateLaunchers.Count];
            launcher?.GetComponent<StageDropperVisualAnimator>()?.PlayDispense();
        }

        private void CreateMonitor()
        {
            CreateMonitor("Upper", 10.55f);
            CreateMonitor("Lower", -10.55f);
        }

        private void CreateMonitor(string suffix, float y)
        {
            GameObject monitor = new GameObject("5-3 Slime Crate Monitor " + suffix);
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.localPosition = new Vector3(0f, y, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(20.5f, 2.65f), -32);
            TextMesh main = StageEscortController.CreateText(
                monitor.transform, "Progress", new Vector3(0f, 0.48f, -0.03f),
                52, 0.105f, new Color(0.22f, 0.55f, 0.18f), -26);
            TextMesh sub = StageEscortController.CreateText(
                monitor.transform, "Hint", new Vector3(0f, -0.48f, -0.04f),
                40, 0.078f, new Color(0.48f, 0.18f, 0.66f), -25);
            monitorMainTexts.Add(main);
            monitorSubTexts.Add(sub);
        }

        private void CompleteRound()
        {
            if (stageManager != null && stageManager.IsDrawingMode)
                stageManager.CancelDrawingMode();
            SetLocalControls(false);
            GameSfx.Play(SfxId.CoinCollect);
            if (round >= 3)
            {
                phase = ChallengePhase.Complete;
                phaseRemaining = 1.4f;
            }
            else
            {
                phase = ChallengePhase.RoundClear;
                phaseRemaining = 1.8f;
            }
            BroadcastState(true);
        }

        private void FailRound()
        {
            if (phase == ChallengePhase.Failed) return;
            if (stageManager != null && stageManager.IsDrawingMode)
                stageManager.CancelDrawingMode();
            phase = ChallengePhase.Failed;
            phaseRemaining = 2.6f;
            SetLocalControls(false);
            uiManager?.SetChallengeCountdown(true, LocalizationManager.T("game_over"));
            GameSfx.Play(SfxId.PlayerHit);
            BroadcastState(true);
        }

        private void ApplyBreak(string crateId, Vector2 hitPoint, bool broadcast)
        {
            if (string.IsNullOrEmpty(crateId) || !IsCurrentRoundCrate(crateId)
                || !brokenIds.Add(crateId)) return;
            pendingBreakIds.Remove(crateId);
            if (crates.TryGetValue(crateId, out StageValueCrate crate) && crate != null)
            {
                Transform crateTransform = crate.transform;
                syncManager?.UnregisterRuntimeObject(crateTransform);
                synchronizedObjects.Remove(crateTransform);
                crate.ApplyBroken(hitPoint);
            }
            if (broadcast) Send(BreakKind, new BreakMessage
            {
                Round = round,
                Attempt = roundAttempt,
                CrateId = crateId,
                HitPoint = hitPoint
            });
            if (broadcast && HasAuthority) ReplenishCrates();
        }

        private void ConfirmElimination(string id)
        {
            ApplyElimination(id);
            Send(EliminatedKind, new PlayerMessage
            {
                Round = round,
                Attempt = roundAttempt,
                PlayerId = id
            });
            BroadcastState(true);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminatedIds.Add(id)) return;
            pendingEliminationIds.Remove(id);
            PlayerController2D target = ResolvePlayer(id);
            if (target != null && !hiddenPlayers.Contains(target))
            {
                target.GetComponent<PlayerCarryController>()?.ForceDrop();
                target.ResetMotion();
                target.SetControlsEnabled(false);
                hiddenPlayers.Add(target);
                target.gameObject.SetActive(false);
            }
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void ReviveAndPlacePlayers()
        {
            RefreshParticipants();
            for (int i = 0; i < participantIds.Count; i++)
            {
                PlayerController2D player = ResolvePlayer(participantIds[i]);
                if (player == null) continue;
                player.gameObject.SetActive(true);
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                player.GetComponent<BodyBuilder>()?.RestoreAfterRespawnOrRetry();
                player.RestoreGameplayPhysicsState();
                player.ResetMotion();
                float spawnHalfWidth = Mathf.Max(3.45f, RedrawFloorWidth * 0.38f);
                float x = participantIds.Count <= 1
                    ? 0f
                    : Mathf.Lerp(-spawnHalfWidth, spawnHalfWidth,
                        i / (participantIds.Count - 1f));
                player.transform.position = new Vector3(x, RedrawFloorY + 1.35f, player.transform.position.z);
            }
            hiddenPlayers.Clear();
            Physics2D.SyncTransforms();
        }

        private void RestoreHiddenPlayers()
        {
            for (int i = 0; i < hiddenPlayers.Count; i++)
            {
                PlayerController2D player = hiddenPlayers[i];
                if (player == null) continue;
                player.gameObject.SetActive(true);
                player.RestoreGameplayPhysicsState();
            }
            hiddenPlayers.Clear();
        }

        private void CheckOutOfBounds()
        {
            if (phase != ChallengePhase.Playing) return;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null) continue;
                if (IsOnline && !stageManager.IsLocallyControlledPlayer(player)) continue;
                Vector2 position = player.transform.position;
                if (position.y < ArenaBottom - 2f || position.y > ArenaTop + 2f
                    || Mathf.Abs(position.x) > ArenaHalfWidth + 2f)
                    RequestElimination(player);
            }
        }

        private bool AreAllPlayersEliminated()
        {
            if (IsOnline)
                return onlineManager != null && onlineManager.AreAllCurrentLobbyPlayersIn(eliminatedIds);
            if (participantIds.Count == 0) RefreshParticipants();
            if (participantIds.Count == 0) return false;
            for (int i = 0; i < participantIds.Count; i++)
                if (!eliminatedIds.Contains(participantIds[i])) return false;
            return true;
        }

        private void RefreshParticipants()
        {
            participantIds.Clear();
            if (IsOnline)
            {
                OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
                if (lobbyPlayers == null) return;
                for (int i = 0; i < lobbyPlayers.Length; i++)
                {
                    string id = lobbyPlayers[i]?.PlayerId;
                    if (!string.IsNullOrEmpty(id) && !participantIds.Contains(id))
                        participantIds.Add(id);
                }
                return;
            }

            PlayerController2D[] localPlayers = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(localPlayers, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            for (int i = 0; i < localPlayers.Length; i++)
            {
                string id = ResolvePlayerId(localPlayers[i]);
                if (!string.IsNullOrEmpty(id)) participantIds.Add(id);
            }
        }

        private int ResolvePlayerCount()
        {
            int reported = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            return Mathf.Clamp(Mathf.Max(reported, participantIds.Count), 1, 4);
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnline
                ? stageManager.GetOnlinePlayerId(player)
                : "local_" + player.GetInstanceID();
        }

        private PlayerController2D ResolvePlayer(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (IsOnline) return stageManager.GetOnlinePlayerController(id);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (ResolvePlayerId(players[i]) == id) return players[i];
            return null;
        }

        private void ApplyLocalControls()
        {
            if (stageManager == null) return;
            bool canMove = phase == ChallengePhase.Countdown || phase == ChallengePhase.Playing;
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (local == null || !local.gameObject.activeSelf) continue;
                local.SetControlsEnabled(canMove && !stageManager.IsDrawingMode
                    && !eliminatedIds.Contains(ResolvePlayerId(local)));
            }
        }

        private void SetLocalControls(bool enabled)
        {
            if (stageManager == null) return;
            IReadOnlyList<PlayerController2D> localPlayers = stageManager.LocalPlayers;
            for (int i = 0; i < localPlayers.Count; i++)
            {
                PlayerController2D local = localPlayers[i];
                if (local != null && local.gameObject.activeSelf)
                    local.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
            }
        }

        private void ShowCountdown()
        {
            uiManager?.SetChallengeCountdown(true,
                Mathf.Max(1, Mathf.CeilToInt(phaseRemaining)).ToString());
        }

        private void AdvanceReplicaClock()
        {
            if (phase == ChallengePhase.Countdown || phase == ChallengePhase.RoundClear
                || phase == ChallengePhase.Failed || phase == ChallengePhase.Complete)
                phaseRemaining = Mathf.Max(0f, phaseRemaining - Time.unscaledDeltaTime);
            else if (phase == ChallengePhase.Playing && !LocalMultiplayerDebugMode.NoTimeLimit)
                remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);

            if (phase == ChallengePhase.Countdown) ShowCountdown();
            else if (phase == ChallengePhase.Failed)
                uiManager?.SetChallengeCountdown(true, LocalizationManager.T("game_over"));
            else if (phase == ChallengePhase.Playing && phaseRemaining <= 0f)
                uiManager?.SetChallengeCountdown(false, string.Empty);
        }

        private void RefreshMonitor()
        {
            if (monitorMainTexts.Count == 0) return;
            string progress = LocalizationManager.Format(
                "slime_crate_progress", round, brokenIds.Count, targetCount,
                Mathf.CeilToInt(remaining));
            string subMessage;
            switch (phase)
            {
                case ChallengePhase.Countdown:
                    subMessage = string.Empty;
                    break;
                case ChallengePhase.RoundClear:
                    subMessage = LocalizationManager.Format("slime_crate_round_clear", round);
                    break;
                case ChallengePhase.Failed:
                    subMessage = LocalizationManager.Format("slime_crate_retry", round);
                    break;
                case ChallengePhase.Complete:
                    subMessage = LocalizationManager.T("slime_crate_complete");
                    break;
                default:
                    subMessage = round == 3
                        ? LocalizationManager.T("slime_crate_enemy_hint")
                        : string.Empty;
                    break;
            }

            for (int i = 0; i < monitorMainTexts.Count; i++)
                if (monitorMainTexts[i] != null) monitorMainTexts[i].text = progress;
            for (int i = 0; i < monitorSubTexts.Count; i++)
                if (monitorSubTexts[i] != null) monitorSubTexts[i].text = subMessage;
        }

        private void BroadcastState(bool force = false)
        {
            if (!HasAuthority || !IsOnline || onlineManager == null
                || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.35f;
            Send(StateKind, new ChallengeState
            {
                Sequence = ++stateSequence,
                Round = round,
                Attempt = roundAttempt,
                Phase = (int)phase,
                PlayerCount = playerCount,
                TargetCount = targetCount,
                SpawnedCount = spawnedCount,
                SpawnedEnemyCount = spawnedEnemyCount,
                Remaining = remaining,
                PhaseRemaining = phaseRemaining,
                BrokenIds = new List<string>(brokenIds).ToArray(),
                EliminatedIds = new List<string>(eliminatedIds).ToArray()
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || string.IsNullOrEmpty(data.Kind)) return;
            if (data.Kind == BreakRequestKind && HasAuthority)
            {
                BreakMessage request = JsonUtility.FromJson<BreakMessage>(data.Json);
                if (request != null && request.Round == round && request.Attempt == roundAttempt
                    && IsCurrentRoundCrate(request.CrateId)
                    && CanPlayerBreakCrate(data.PlayerId, request.CrateId))
                    ApplyBreak(request.CrateId, request.HitPoint, true);
                return;
            }
            if (data.Kind == EliminateRequestKind && HasAuthority)
            {
                PlayerMessage request = JsonUtility.FromJson<PlayerMessage>(data.Json);
                if (request != null && request.Round == round && request.Attempt == roundAttempt
                    && request.PlayerId == data.PlayerId)
                    ConfirmElimination(request.PlayerId);
                return;
            }
            if (HasAuthority || onlineManager == null || !onlineManager.IsHostPlayer(data.PlayerId)) return;

            if (data.Kind == BreakKind)
            {
                BreakMessage hit = JsonUtility.FromJson<BreakMessage>(data.Json);
                if (hit != null && hit.Round == round && hit.Attempt == roundAttempt)
                    ApplyBreak(hit.CrateId, hit.HitPoint, false);
            }
            else if (data.Kind == SpawnKind)
            {
                SpawnMessage spawn = JsonUtility.FromJson<SpawnMessage>(data.Json);
                if (spawn != null && spawn.Round == round && spawn.Attempt == roundAttempt
                    && spawn.CrateIndex >= 0 && spawn.CrateIndex < targetCount)
                    EnsureSpawnedCount(spawn.CrateIndex + 1, true);
            }
            else if (data.Kind == EnemyLaunchKind)
            {
                EnemyLaunchMessage launch = JsonUtility.FromJson<EnemyLaunchMessage>(data.Json);
                if (launch != null && launch.Round == round && launch.Attempt == roundAttempt
                    && launch.EnemyIndex >= 0)
                {
                    spawnedEnemyCount = Mathf.Max(spawnedEnemyCount, launch.EnemyIndex + 1);
                    PlayEnemyLaunchVisual(launch.LauncherIndex);
                }
            }
            else if (data.Kind == EliminatedKind)
            {
                PlayerMessage eliminated = JsonUtility.FromJson<PlayerMessage>(data.Json);
                if (eliminated != null && eliminated.Round == round
                    && eliminated.Attempt == roundAttempt)
                    ApplyElimination(eliminated.PlayerId);
            }
            else if (data.Kind == StateKind)
            {
                ApplyNetworkState(JsonUtility.FromJson<ChallengeState>(data.Json));
            }
        }

        private void ApplyNetworkState(ChallengeState state)
        {
            if (state == null || state.Sequence <= lastStateSequence) return;
            ChallengePhase incomingPhase = (ChallengePhase)Mathf.Clamp(
                state.Phase, 0, (int)ChallengePhase.Complete);
            bool needsRoundRebuild = state.Round != round
                || state.Attempt != roundAttempt
                || Mathf.Clamp(state.PlayerCount, 1, 4) != playerCount;
            lastStateSequence = state.Sequence;
            if (needsRoundRebuild)
                BeginRound(state.Round, state.PlayerCount, state.Attempt);

            phase = incomingPhase;
            playerCount = Mathf.Clamp(state.PlayerCount, 1, 4);
            targetCount = state.TargetCount > 0
                ? state.TargetCount
                : playerCount * CratesPerPlayer;
            int authoritativeSpawnedCount = state.SpawnedCount > 0
                ? state.SpawnedCount
                : Mathf.Min(targetCount, ConcurrentCrateLimit);
            EnsureSpawnedCount(authoritativeSpawnedCount, false);
            spawnedEnemyCount = Mathf.Max(spawnedEnemyCount, state.SpawnedEnemyCount);
            remaining = state.Remaining;
            phaseRemaining = state.PhaseRemaining;
            if (phase != ChallengePhase.Countdown && phase != ChallengePhase.Playing
                && stageManager != null && stageManager.IsDrawingMode)
                stageManager.CancelDrawingMode();
            if (state.BrokenIds != null)
            {
                for (int i = 0; i < state.BrokenIds.Length; i++)
                {
                    string id = state.BrokenIds[i];
                    Vector2 point = crates.TryGetValue(id, out StageValueCrate crate) && crate != null
                        ? (Vector2)crate.transform.position
                        : Vector2.zero;
                    ApplyBreak(id, point, false);
                }
            }
            if (state.EliminatedIds != null)
                for (int i = 0; i < state.EliminatedIds.Length; i++)
                    ApplyElimination(state.EliminatedIds[i]);
            ApplyLocalControls();
            RefreshMonitor();
        }

        private bool IsCurrentRoundCrate(string id)
        {
            return !string.IsNullOrEmpty(id)
                && id.StartsWith(RoundObjectPrefix + "_crate_", System.StringComparison.Ordinal)
                && crates.ContainsKey(id);
        }

        private bool CanPlayerBreakCrate(string playerId, string crateId)
        {
            if (stageManager == null || string.IsNullOrEmpty(playerId)
                || !crates.TryGetValue(crateId, out StageValueCrate crate)
                || crate == null || crate.IsBroken) return false;
            PlayerController2D player = stageManager.GetOnlinePlayerController(playerId);
            if (player == null || player.CurrentSpecies != DrawManager.Species.Slime) return false;
            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(false);
            Collider2D[] crateColliders = crate.GetComponentsInChildren<Collider2D>(false);
            float closest = Vector2.Distance(player.transform.position, crate.transform.position);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D a = playerColliders[i];
                if (a == null || !a.enabled || a.isTrigger) continue;
                for (int j = 0; j < crateColliders.Length; j++)
                {
                    Collider2D b = crateColliders[j];
                    if (b == null || !b.enabled || b.isTrigger) continue;
                    closest = Mathf.Min(closest, Mathf.Max(0f, a.Distance(b).distance));
                }
            }
            // Slime spikes scale with the drawing, so leave enough tolerance for
            // a long legitimate spike while still rejecting arena-wide requests.
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            float lagAllowance = body != null
                ? Mathf.Min(2.5f, body.linearVelocity.magnitude * 0.22f)
                : 0f;
            return closest <= 7f + lagAllowance;
        }

        private void Send<T>(string kind, T value)
        {
            if (!IsOnline || onlineManager == null) return;
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = kind,
                Json = JsonUtility.ToJson(value)
            });
        }

        private void RegisterSynchronizedObject(Transform target)
        {
            if (target == null) return;
            synchronizedObjects.Add(target);
            syncManager?.RegisterRuntimeObject(target);
        }

        private void ClearArena()
        {
            if (HasAuthority && syncManager != null)
                for (int i = 0; i < spawnedEnemyIds.Count; i++)
                    syncManager.RemoveDropperEnemy(spawnedEnemyIds[i]);
            spawnedEnemyIds.Clear();
            for (int i = 0; i < synchronizedObjects.Count; i++)
                if (synchronizedObjects[i] != null)
                    syncManager?.UnregisterRuntimeObject(synchronizedObjects[i]);
            synchronizedObjects.Clear();
            crates.Clear();
            crateLaunchers.Clear();
            monitorMainTexts.Clear();
            monitorSubTexts.Clear();
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
                arenaRoot = null;
            }
        }
    }
}
