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
        private const string EliminateRequestKind = "slime_crate_eliminate_request";
        private const string EliminatedKind = "slime_crate_eliminated";
        private const float RoundSeconds = 30f;
        private const float CountdownSeconds = 3f;
        private const float ArenaHalfWidth = 17.8f;
        private const float ArenaBottom = -9.2f;
        private const float ArenaTop = 9.2f;
        private const float RedrawFloorY = -0.9f;

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

        private StageManager stageManager;
        private StageLoader stageLoader;
        private StageObjectFactory objectFactory;
        private StageGimmickSyncManager syncManager;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private CameraFollow2D cameraFollow;
        private Transform arenaRoot;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private ChallengePhase phase;
        private float remaining;
        private float phaseRemaining;
        private float nextStateAt;
        private float previousCameraMinimum;
        private int round = 1;
        private int roundAttempt;
        private int playerCount = 1;
        private int targetCount = 50;
        private int stateSequence;
        private int lastStateSequence;
        private bool initialized;
        private bool cameraMinimumChanged;

        public override bool UsesGlobalFallBoundary => false;
        private string RoundObjectPrefix => "5-3_r" + round + "_a" + roundAttempt;
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
                        uiManager?.SetChallengeCountdown(true, LocalizationManager.T("survival_start"));
                        BroadcastState(true);
                    }
                    break;

                case ChallengePhase.Playing:
                    ApplyLocalControls();
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
                if (onlineManager == null || id != onlineManager.LocalPlayerId) return;
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
            targetCount = 50 * playerCount;
            phase = ChallengePhase.Countdown;
            remaining = RoundSeconds;
            phaseRemaining = CountdownSeconds;
            brokenIds.Clear();
            eliminatedIds.Clear();
            pendingBreakIds.Clear();
            pendingEliminationIds.Clear();
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
                new Vector2(0f, RedrawFloorY), new Vector2(10f, 0.65f));
            StageRedrawZoneFactory.CreateRuntimeFloorZone(
                arenaRoot,
                RoundObjectPrefix + "_redraw_zone",
                new Vector2(0f, RedrawFloorY),
                9.7f,
                3.1f,
                0.65f);
            stageLoader?.SetRuntimeSpawnPosition(new Vector2(0f, RedrawFloorY + 1.2f));

            CreateInwardSprings();
            CreateCrates();
            if (round == 3) CreateEnemies();
            CreateMonitor();
        }

        private void CreateBackdrop()
        {
            GameObject backdrop = new GameObject("Round Paper Tint");
            backdrop.transform.SetParent(arenaRoot, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            backdrop.transform.localScale = new Vector3(34.8f, 17.7f, 1f);
            SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = round == 1
                ? new Color(0.9f, 1f, 0.93f, 0.38f)
                : round == 2
                    ? new Color(1f, 0.94f, 0.76f, 0.34f)
                    : new Color(0.91f, 0.84f, 1f, 0.34f);
            renderer.sortingOrder = -45;
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
            float[] horizontal = { -12f, -6f, 0f, 6f, 12f };
            for (int i = 0; i < horizontal.Length; i++)
            {
                CreateSpring("bottom_" + i, new Vector2(horizontal[i], ArenaBottom + 0.72f), 0f);
                CreateSpring("top_" + i, new Vector2(horizontal[i], ArenaTop - 0.72f), 180f);
            }

            float[] vertical = { -5.2f, 0f, 5.2f };
            for (int i = 0; i < vertical.Length; i++)
            {
                CreateSpring("left_" + i, new Vector2(-ArenaHalfWidth + 0.72f, vertical[i]), -90f);
                CreateSpring("right_" + i, new Vector2(ArenaHalfWidth - 0.72f, vertical[i]), 90f);
            }
        }

        private void CreateSpring(string suffix, Vector2 position, float rotation)
        {
            if (objectFactory == null) return;
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.Spring, position);
            data.objectId = RoundObjectPrefix + "_spring_" + suffix;
            data.size = new Vector2(2.25f, 0.82f);
            data.rotation = rotation;
            data.actionStrength = 19f;
            objectFactory.Create(data, arenaRoot);
        }

        private void CreateCrates()
        {
            if (objectFactory == null) return;
            List<Vector2> positions = BuildCratePositions();
            StageObjectType[] mixedTypes =
            {
                StageObjectType.WoodBox,
                StageObjectType.TriangleBox,
                StageObjectType.Ball,
                StageObjectType.Barrel,
                StageObjectType.RubberBox,
                StageObjectType.IronBox
            };

            for (int i = 0; i < targetCount; i++)
            {
                StageObjectType type = round == 2
                    ? mixedTypes[(i * 5 + i / 7) % mixedTypes.Length]
                    : StageObjectType.WoodBox;
                float size = round == 1
                    ? 0.72f + (i * 17 % 7) * 0.065f
                    : round == 2
                        ? 0.66f + (i * 11 % 8) * 0.07f
                        : 0.5f + (i * 13 % 5) * 0.045f;
                string id = RoundObjectPrefix + "_crate_" + i.ToString("D3");
                StageObjectData data = StageObjectFactory.CreateDefaultData(type, positions[i]);
                data.objectId = id;
                data.size = Vector2.one * size;
                data.rotation = type == StageObjectType.TriangleBox && (i & 1) == 1 ? 180f : (i % 9 - 4) * 2f;
                GameObject crateObject = objectFactory.Create(data, arenaRoot);
                if (crateObject == null) continue;

                Rigidbody2D body = crateObject.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.gravityScale = 0.72f;
                    body.mass = Mathf.Max(0.7f, size * size * 1.7f);
                    body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                    body.sleepMode = RigidbodySleepMode2D.StartAwake;
                    body.WakeUp();
                }

                StageValueCrate crate = crateObject.GetComponent<StageValueCrate>();
                if (crate == null) crate = crateObject.AddComponent<StageValueCrate>();
                crate.Configure(this, id, 0);
                crates[id] = crate;
                RegisterSynchronizedObject(crateObject.transform);
            }
        }

        private List<Vector2> BuildCratePositions()
        {
            List<Vector2> positions = new List<Vector2>(300);
            const int columns = 25;
            const int rows = 12;
            for (int rowIndex = 0; rowIndex < rows; rowIndex++)
            {
                float y = Mathf.Lerp(-7.15f, 7.1f, rowIndex / (rows - 1f));
                for (int column = 0; column < columns; column++)
                {
                    float x = Mathf.Lerp(-14.65f, 14.65f, column / (columns - 1f));
                    if (Mathf.Abs(x) < 5.65f && y > RedrawFloorY - 1.1f && y < RedrawFloorY + 3.25f)
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

        private void CreateEnemies()
        {
            if (objectFactory == null) return;
            int count = Mathf.Clamp(playerCount + 1, 2, 4);
            for (int i = 0; i < count; i++)
            {
                StageObjectType type = i % 2 == 0
                    ? StageObjectType.EnemyWalker
                    : StageObjectType.EnemyJumper;
                float x = count == 1 ? 0f : Mathf.Lerp(-4.1f, 4.1f, i / (count - 1f));
                StageObjectData data = StageObjectFactory.CreateDefaultData(
                    type, new Vector2(x, RedrawFloorY + 1.15f));
                data.objectId = RoundObjectPrefix + "_enemy_" + i.ToString("D2");
                data.size = Vector2.one * 0.82f;
                data.movementSpeed = 1.65f + i * 0.12f;
                data.rotation = i % 2 == 0 ? 0f : 180f;
                GameObject enemy = objectFactory.Create(data, arenaRoot);
                if (enemy != null) RegisterSynchronizedObject(enemy.transform);
            }
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("5-3 Slime Crate Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.localPosition = new Vector3(0f, 10.55f, 0.35f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(20.5f, 2.65f), -32);
            monitorMain = StageEscortController.CreateText(
                monitor.transform, "Progress", new Vector3(0f, 0.48f, -0.03f),
                52, 0.105f, new Color(0.22f, 0.55f, 0.18f), -26);
            monitorSub = StageEscortController.CreateText(
                monitor.transform, "Hint", new Vector3(0f, -0.48f, -0.04f),
                40, 0.078f, new Color(0.48f, 0.18f, 0.66f), -25);
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
                float x = participantIds.Count <= 1
                    ? 0f
                    : Mathf.Lerp(-3.45f, 3.45f, i / (participantIds.Count - 1f));
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
                if (IsOnline && player.transform != stageManager.ActivePlayerTransform) continue;
                Vector2 position = player.transform.position;
                if (position.y < ArenaBottom - 2f || position.y > ArenaTop + 2f
                    || Mathf.Abs(position.x) > ArenaHalfWidth + 2f)
                    RequestElimination(player);
            }
        }

        private bool AreAllPlayersEliminated()
        {
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
            PlayerController2D active = stageManager != null && stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            if (active == null || !active.gameObject.activeSelf) return;
            bool canMove = phase == ChallengePhase.Countdown || phase == ChallengePhase.Playing;
            active.SetControlsEnabled(canMove && !stageManager.IsDrawingMode
                && !eliminatedIds.Contains(ResolvePlayerId(active)));
        }

        private void SetLocalControls(bool enabled)
        {
            PlayerController2D active = stageManager != null && stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            if (active != null && active.gameObject.activeSelf)
                active.SetControlsEnabled(enabled && !stageManager.IsDrawingMode);
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
            if (monitorMain == null || monitorSub == null) return;
            monitorMain.text = LocalizationManager.Format(
                "slime_crate_progress", round, brokenIds.Count, targetCount,
                Mathf.CeilToInt(remaining));
            switch (phase)
            {
                case ChallengePhase.Countdown:
                    monitorSub.text = LocalizationManager.T("slime_crate_hint");
                    break;
                case ChallengePhase.RoundClear:
                    monitorSub.text = LocalizationManager.Format("slime_crate_round_clear", round);
                    break;
                case ChallengePhase.Failed:
                    monitorSub.text = LocalizationManager.Format("slime_crate_retry", round);
                    break;
                case ChallengePhase.Complete:
                    monitorSub.text = LocalizationManager.T("slime_crate_complete");
                    break;
                default:
                    monitorSub.text = LocalizationManager.T(
                        round == 3 ? "slime_crate_enemy_hint" : "slime_crate_hint");
                    break;
            }
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
                    && IsCurrentRoundCrate(request.CrateId))
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
            targetCount = state.TargetCount > 0 ? state.TargetCount : playerCount * 50;
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
            for (int i = 0; i < synchronizedObjects.Count; i++)
                if (synchronizedObjects[i] != null)
                    syncManager?.UnregisterRuntimeObject(synchronizedObjects[i]);
            synchronizedObjects.Clear();
            crates.Clear();
            monitorMain = null;
            monitorSub = null;
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
                arenaRoot = null;
            }
        }
    }
}
