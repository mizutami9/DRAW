using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageCatEscapeController : StageEliminationChallengeController
    {
        private const string StageId = "2-3";
        private const string StateKind = "cat_escape_state";
        private const string ScratchRequestKind = "cat_escape_scratch_request";
        private const string ButtonRequestKind = "cat_escape_button_request";
        private const string EliminateRequestKind = "cat_escape_eliminate_request";
        private const float StartDelay = 3f;
        private const float FallEliminationY = -80f;
        private const float MainCatFadeSeconds = 0.9f;
        private const float LaneCatFadeSeconds = 1.15f;
        private const int SwitchSetCount = 3;
        private static readonly float[] LaneY = { -28f, -42f, -56f, -70f };

        [System.Serializable]
        private sealed class EscapeState
        {
            public int Sequence;
            public int Phase;
            public float Clock;
            public float BossX;
            public float[] LaneBossXs;
            public string[] TargetIds;
            public int[] TargetHits;
            public bool[] Buttons;
            public string[] EliminatedIds;
            public float MainCatFade;
            public float[] LaneCatFades;
            public bool MainCatRetreating;
            public bool[] LaneChases;
            public int RoomPhase;
            public float RoomClock;
        }

        [System.Serializable] private sealed class IdRequest { public string Id; }
        [System.Serializable] private sealed class ButtonRequest { public int Lane; public int Set; }
        [System.Serializable] private sealed class EliminationRequest { public string PlayerId; }

        private readonly Dictionary<string, StageCatEscapeScratchTarget> scratchTargets
            = new Dictionary<string, StageCatEscapeScratchTarget>();
        private readonly List<StageEnemyCharacter>[] laneEnemies =
        {
            new List<StageEnemyCharacter>(), new List<StageEnemyCharacter>(),
            new List<StageEnemyCharacter>(), new List<StageEnemyCharacter>()
        };
        private readonly StageCatEscapeGate[,] switchGates = new StageCatEscapeGate[SwitchSetCount, 4];
        private readonly StageCatEscapeGate[] enemyGates = new StageCatEscapeGate[4];
        private readonly bool[] buttons = new bool[SwitchSetCount * 4];
        private readonly Transform[] laneCats = new Transform[4];
        private readonly float[] laneBossXs = { 99f, 99f, 99f, 99f };
        private readonly float[] laneCatFades = new float[4];
        private readonly bool[] laneChases = new bool[4];
        private readonly GameObject[] trapdoorFloors = new GameObject[4];
        private readonly GameObject[] positionMarkers = new GameObject[4];
        private readonly HashSet<string> participants = new HashSet<string>();
        private readonly HashSet<string> eliminated = new HashSet<string>();
        private readonly HashSet<string> assignedToShaft = new HashSet<string>();
        private readonly Dictionary<string, int> playerLanes = new Dictionary<string, int>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private StageObjectFactory factory;
        private StageGimmickSyncManager syncManager;
        private Transform giantCat;
        private TextMesh monitorTitle;
        private TextMesh monitorStatus;
        private TextMesh roomStatus;
        private int playerCount = 1;
        private int phase;
        private float clock;
        private float bossX = -13f;
        private float mainCatFade;
        private float nextBroadcastAt;
        private int sequence;
        private int receivedSequence;
        private bool receivedFreshRunState;
        private bool controlsReleased;
        private bool mainCatRetreating;
        private StageCatEscapeGate roomEntranceGate;
        private int roomPhase;
        private float roomClock;
        private int terrainSerial;

        public override bool UsesGlobalFallBoundary => false;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            syncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing) { enabled = false; return; }
            ResetRunState();
            int reported = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            int spawned = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None).Length;
            playerCount = Mathf.Clamp(Mathf.Max(reported, spawned), 1, 4);
            BuildCourse();
            BuildMonitor();
            BuildGiantCats();
            ApplyRoomVisualState();
            RefreshMonitor();
        }

        private void ResetRunState()
        {
            phase = 0;
            clock = 0f;
            bossX = -13f;
            mainCatFade = 0f;
            mainCatRetreating = false;
            controlsReleased = false;
            roomPhase = 0;
            roomClock = 0f;
            nextBroadcastAt = 0f;
            sequence = 0;
            receivedSequence = 0;
            receivedFreshRunState = false;
            terrainSerial = 0;
            participants.Clear();
            eliminated.Clear();
            assignedToShaft.Clear();
            playerLanes.Clear();
            scratchTargets.Clear();
            for (int lane = 0; lane < 4; lane++)
            {
                laneBossXs[lane] = 99f;
                laneCatFades[lane] = 0f;
                laneChases[lane] = false;
                laneEnemies[lane].Clear();
            }
            for (int i = 0; i < buttons.Length; i++) buttons[i] = false;
        }

        private void Update()
        {
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            if (phase == 0)
            {
                if (stageManager.IsChallengeReadyRoomActive) return;
                PositionPlayersAtStart();
                CaptureParticipants();
                phase = 1;
                clock = StartDelay;
                SetLocalControls(false);
                BroadcastState(true);
            }
            else if (phase == 1 && HasAuthority)
            {
                clock = Mathf.Max(0f, clock - Time.deltaTime);
                if (clock <= 0f)
                {
                    phase = 2;
                    controlsReleased = true;
                    SetLocalControls(true);
                    GameSfx.Play(SfxId.StageCountdownGo, 1.15f);
                }
                BroadcastState(false);
            }
            else if (phase == 2 && HasAuthority)
            {
                UpdateChase();
                BroadcastState(false);
            }

            if (phase == 2) UpdateEnemyGates();

            if (phase == 2 && !controlsReleased)
            {
                controlsReleased = true;
                SetLocalControls(true);
            }
            UpdateBossVisuals();
            RefreshMonitor();
        }

        private void UpdateChase()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            bool anyoneBeforeSplit = false;
            bool[] occupiedLanes = new bool[4];
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                string id = ResolvePlayerId(player);
                if (!string.IsNullOrEmpty(id) && eliminated.Contains(id)) continue;
                if (player.transform.position.y < FallEliminationY)
                {
                    RequestElimination(player);
                    if (phase != 2) return;
                    continue;
                }
                float x = player.transform.position.x;
                bool inShaftOrLane = !string.IsNullOrEmpty(id) && assignedToShaft.Contains(id);
                int lane = ResolvePlayerLane(player);
                bool landedInLane = inShaftOrLane && player.transform.position.y <= LaneY[lane] + 6.8f;
                bool safeInRoom = roomPhase > 0 || IsInsideDropRoom(player);
                if (!inShaftOrLane && !safeInRoom && x < 112f) anyoneBeforeSplit = true;
                else if (landedInLane && x < 240f) occupiedLanes[lane] = true;
                if (!mainCatRetreating && mainCatFade >= 0.99f
                    && !inShaftOrLane && !safeInRoom && x < 112f && x < bossX + 3.4f)
                    RequestElimination(player);
                if (laneChases[lane] && laneCatFades[lane] >= 0.99f
                    && landedInLane && x < 240f && x < laneBossXs[lane] + 2.8f) RequestElimination(player);
            }

            if (roomPhase == 0)
            {
                mainCatFade = Mathf.MoveTowards(mainCatFade, 1f, Time.deltaTime / MainCatFadeSeconds);
                if (AreAllLivingPlayersInRoom()) BeginRoomSequence();
                else if (anyoneBeforeSplit && mainCatFade >= 0.99f)
                    bossX = Mathf.Min(103f, bossX + 4.8f * Time.deltaTime);
            }
            else
            {
                UpdateRoomSequence();
            }

            for (int lane = 0; lane < playerCount; lane++)
            {
                if (!occupiedLanes[lane]) continue;
                if (!laneChases[lane])
                {
                    laneChases[lane] = true;
                    laneCatFades[lane] = 0f;
                    laneBossXs[lane] = 99f;
                }
                else
                {
                    laneCatFades[lane] = Mathf.MoveTowards(
                        laneCatFades[lane], 1f, Time.deltaTime / LaneCatFadeSeconds);
                    if (laneCatFades[lane] >= 0.99f) laneBossXs[lane] += 4.35f * Time.deltaTime;
                }
            }
        }

        private void BeginRoomSequence()
        {
            roomPhase = 1;
            roomClock = 0.75f;
            mainCatRetreating = false;
            roomEntranceGate?.SetOpen(false);
            SetLivingControls(false);
            BroadcastState(true);
        }

        private void UpdateRoomSequence()
        {
            if (roomPhase == 1)
            {
                roomClock -= Time.deltaTime;
                if (roomClock <= 0f)
                {
                    roomPhase = 2;
                    roomClock = 0.7f;
                }
            }
            else if (roomPhase == 2)
            {
                mainCatFade = Mathf.MoveTowards(mainCatFade, 1f, Time.deltaTime * 2f);
                bossX = Mathf.MoveTowards(bossX, 103f, 22f * Time.deltaTime);
                if (bossX >= 102.95f)
                {
                    roomClock -= Time.deltaTime;
                    if (roomClock <= 0f)
                    {
                        roomPhase = 3;
                        roomClock = 1.45f;
                        mainCatRetreating = true;
                    }
                }
            }
            else if (roomPhase == 3)
            {
                roomClock -= Time.deltaTime;
                bossX -= 11f * Time.deltaTime;
                mainCatFade = Mathf.Clamp01(roomClock / 1.45f);
                if (roomClock <= 0f)
                {
                    roomPhase = 4;
                    mainCatFade = 0f;
                    ShowPositionMarkers(true);
                    SetLivingControls(true);
                }
            }
            else if (roomPhase == 4 && AreAllLivingPlayersOnMarkers())
            {
                BeginTrapdoorCountdown();
            }
            else if (roomPhase == 5)
            {
                roomClock = Mathf.Max(0f, roomClock - Time.deltaTime);
                if (roomClock <= 0f) OpenTrapdoors();
            }
        }

        private void BeginTrapdoorCountdown()
        {
            roomPhase = 5;
            roomClock = 3f;
            SetLivingControls(false);
            BroadcastState(true);
        }

        private void FailRun()
        {
            if (phase != 2) return;
            phase = 3;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null) continue;
                player.SetControlsEnabled(false);
                player.ResetMotion();
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.simulated = false;
            }
            BroadcastState(true);
            stageManager?.Retry();
        }

        public override void RequestElimination(PlayerController2D player)
        {
            if (player == null || phase != 2 || player.IsInvulnerable || player.IsTurtleShelled) return;
            string id = ResolvePlayerId(player);
            if (string.IsNullOrEmpty(id) || eliminated.Contains(id)) return;
            if (IsOnline && !HasAuthority)
            {
                if (id == onlineManager?.LocalPlayerId)
                    Send(EliminateRequestKind, new EliminationRequest { PlayerId = id });
                return;
            }
            ApplyElimination(id);
            BroadcastState(true);
            FailRun();
        }

        private bool IsInsideDropRoom(PlayerController2D player)
        {
            if (player == null) return false;
            float roomRight = GetRoomRight();
            Vector2 position = player.transform.position;
            return position.x >= GetRoomLeft() + 4.25f && position.x <= roomRight - 1f
                && position.y >= -0.25f && position.y <= 15.2f;
        }

        private bool AreAllLivingPlayersInRoom()
        {
            bool foundLiving = false;
            foreach (string id in participants)
            {
                if (eliminated.Contains(id)) continue;
                PlayerController2D player = ResolvePlayer(id);
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                foundLiving = true;
                if (!IsInsideDropRoom(player)) return false;
            }
            return foundLiving;
        }

        private void GetRoomOccupancy(out int inside, out int living)
        {
            inside = 0;
            living = 0;
            foreach (string id in participants)
            {
                if (eliminated.Contains(id)) continue;
                PlayerController2D player = ResolvePlayer(id);
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                living++;
                if (IsInsideDropRoom(player)) inside++;
            }
        }

        private bool AreAllLivingPlayersOnMarkers()
        {
            bool foundLiving = false;
            foreach (string id in participants)
            {
                if (eliminated.Contains(id)) continue;
                PlayerController2D player = ResolvePlayer(id);
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                foundLiving = true;
                int lane = ResolvePlayerLane(player);
                if (!player.IsGrounded || Mathf.Abs(player.transform.position.x - GetShaftX(lane)) > 0.72f)
                    return false;
            }
            return foundLiving;
        }

        private void OpenTrapdoors()
        {
            roomPhase = 6;
            roomClock = 0f;
            ShowPositionMarkers(false);
            foreach (string id in participants)
                if (!eliminated.Contains(id)) assignedToShaft.Add(id);
            for (int i = 0; i < playerCount; i++)
                if (trapdoorFloors[i] != null) trapdoorFloors[i].SetActive(false);
            GameSfx.Play(SfxId.CrateBreak, 0.8f);
            Physics2D.SyncTransforms();
            SetLivingControls(true);
            BroadcastState(true);
        }

        private static float GetShaftX(int lane) => 114f + lane * 7f;
        private static float GetRoomLeft() => GetShaftX(0) - 5.5f;
        private float GetRoomRight() => GetShaftX(playerCount - 1) + 5.5f;

        private int ResolvePlayerLane(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            if (!string.IsNullOrEmpty(id) && playerLanes.TryGetValue(id, out int assignedLane))
                return assignedLane;
            if (IsOnline)
                return Mathf.Clamp(PlayerColorPalette.GetLobbyPlayerSlot(
                    onlineManager?.CurrentLobby, stageManager.GetOnlinePlayerId(player)), 0, playerCount - 1);
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            return Mathf.Clamp(System.Array.IndexOf(players, player), 0, playerCount - 1);
        }

        internal void RequestScratch(string id, PlayerController2D source)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (IsOnline && !HasAuthority)
            {
                if (source != null && source.transform == stageManager.ActivePlayerTransform)
                    Send(ScratchRequestKind, new IdRequest { Id = id });
                return;
            }
            ApplyScratch(id);
        }

        private void ApplyScratch(string id)
        {
            if (!scratchTargets.TryGetValue(id, out StageCatEscapeScratchTarget target)
                || target == null || target.IsBroken) return;
            target.ApplyHit();
            BroadcastState(true);
        }

        internal void RequestButton(int set, int lane, PlayerController2D source)
        {
            set = Mathf.Clamp(set, 0, SwitchSetCount - 1);
            lane = Mathf.Clamp(lane, 0, playerCount - 1);
            if (IsOnline && !HasAuthority)
            {
                if (source != null && source.transform == stageManager.ActivePlayerTransform)
                    Send(ButtonRequestKind, new ButtonRequest { Lane = lane, Set = set });
                return;
            }
            ApplyButton(set, lane);
        }

        private void ApplyButton(int set, int lane)
        {
            int index = set * 4 + lane;
            if (buttons[index]) return;
            buttons[index] = true;
            int targetLane = playerCount == 1 ? 0 : (lane + set + 1) % playerCount;
            switchGates[set, targetLane]?.SetOpen(true);
            BroadcastState(true);
        }

        private void UpdateEnemyGates()
        {
            for (int lane = 0; lane < playerCount; lane++)
            {
                bool clear = true;
                for (int i = 0; i < laneEnemies[lane].Count; i++)
                    if (laneEnemies[lane][i] != null && !laneEnemies[lane][i].IsDefeated) { clear = false; break; }
                if (clear) enemyGates[lane]?.SetOpen(true);
            }
        }

        private void BuildCourse()
        {
            CreateFloor(-10f, 14f, 0f);
            CreateFloor(18f, 29f, 0f);
            CreateFloor(33f, 45f, 0.8f);
            CreateFloor(49f, 108.5f, 0f);
            CreatePlatform(15.8f, 2.1f, 4.2f);
            CreatePlatform(31f, 2.8f, 3.4f);
            CreatePlatform(47f, 2.1f, 3.2f);
            CreatePlatform(25f, 4.8f, 4f);
            CreatePlatform(41f, 5.4f, 4.5f);

            int boxIndex = 0;
            for (int column = 0; column < 19; column++)
            {
                int stackHeight = 1 + ((column * 7 + 3) % 5);
                float x = 52f + column * 2.9f;
                for (int row = 0; row < stackHeight; row++)
                {
                    float y = 0.9f + row * 1.8f;
                    CreateScratchBox("2-3_box_field_" + boxIndex++, new Vector2(x, y),
                        new Vector2(1.7f, 1.7f), 1, false);
                }
            }

            CreateDropRoom();
            for (int lane = 0; lane < playerCount; lane++) BuildLane(lane);

            CreateTeamWall("2-3_team_wall_10", 270f, 10);
            CreateTeamWall("2-3_team_wall_20", 286f, 20);
            CreateTeamWall("2-3_team_wall_30", 303f, 30);
            for (int lane = 0; lane < playerCount; lane++)
                CreateRamp(314f, LaneY[lane], 380f, -35f);
            CreateFloor(379f, 428f, -35f);
            CreateGoal(new Vector2(419f, -33.8f));
        }

        private void BuildLane(int lane)
        {
            float y = LaneY[lane];
            float shaftX = GetShaftX(lane);
            float shaftTop = -0.7f;
            CreateWall(shaftX - 3.5f, y, shaftTop);
            CreateWall(shaftX + 3.5f, y + 6.8f, shaftTop);
            CreateFloor(shaftX - 3.5f, 314f, y);
            CreateCeiling(shaftX + 3.5f, 314f, y + 6.8f);
            for (int box = 0; box < 10; box++)
                CreateScratchBox("2-3_lane_" + lane + "_box_" + box,
                    new Vector2(shaftX + 8f + (box % 5) * 4.1f, y + 1.05f + (box / 5) * 2.05f),
                    new Vector2(1.7f, 1.7f), 1, false);

            float[] buttonXs = { 148f, 174f, 200f };
            float[] gateXs = { 158f, 184f, 210f };
            for (int set = 0; set < SwitchSetCount; set++)
            {
                CreateButton(set, lane, new Vector2(buttonXs[set], y + 0.42f));
                switchGates[set, lane] = CreateGate(
                    "Switch Gate " + set + "-" + lane,
                    new Vector2(gateXs[set], y + 2.9f), new Vector2(1f, 5.8f));
            }

            for (int e = 0; e < 3; e++)
            {
                StageObjectData data = StageObjectFactory.CreateDefaultData(
                    e == 1 ? StageObjectType.EnemyJumper : StageObjectType.EnemyWalker,
                    new Vector2(226f + e * 8f, y + 1.1f));
                data.objectId = "2-3_lane_" + lane + "_enemy_" + e;
                data.movementSpeed = 1.6f + e * 0.2f;
                GameObject enemyObject = factory != null ? factory.Create(data, transform) : null;
                StageEnemyCharacter enemy = enemyObject != null ? enemyObject.GetComponent<StageEnemyCharacter>() : null;
                if (enemy != null) laneEnemies[lane].Add(enemy);
                if (enemyObject != null) syncManager?.RegisterRuntimeObject(enemyObject.transform);
            }
            enemyGates[lane] = CreateGate("Enemy Gate " + lane, new Vector2(254f, y + 2.9f), new Vector2(1f, 5.8f));
        }

        private void CreateFloor(float left, float right, float y)
        {
            CreateTerrain(StageObjectType.Platform, "Run Floor", new Vector2((left + right) * 0.5f, y - 0.35f),
                new Vector2(right - left, 0.7f));
        }

        private void CreateCeiling(float left, float right, float y)
        {
            CreateTerrain(StageObjectType.Platform, "Lane Ceiling", new Vector2((left + right) * 0.5f, y),
                new Vector2(right - left, 0.55f));
        }

        private void CreatePlatform(float x, float y, float width)
        {
            CreateTerrain(StageObjectType.Platform, "Athletic Platform", new Vector2(x, y), new Vector2(width, 0.55f));
        }

        private void CreateRamp(float fromX, float fromY, float toX, float toY)
        {
            Vector2 from = new Vector2(fromX, fromY);
            Vector2 to = new Vector2(toX, toY);
            Vector2 direction = (to - from).normalized;
            Vector2 upwardNormal = new Vector2(-direction.y, direction.x);
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.Platform,
                (from + to) * 0.5f - upwardNormal * 0.35f);
            data.objectId = "2-3_terrain_" + terrainSerial++;
            data.size = new Vector2(Vector2.Distance(from, to), 0.7f);
            data.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            data.keepSeparate = true;
            GameObject created = factory != null ? factory.Create(data, transform) : null;
            if (created != null)
            {
                created.name = "Lane Merge Ramp";
                syncManager?.RegisterRuntimeObject(created.transform);
            }
        }

        private void CreateWall(float x, float bottom, float top)
        {
            CreateTerrain(StageObjectType.Wall, "Shaft Wall", new Vector2(x, (bottom + top) * 0.5f),
                new Vector2(0.55f, top - bottom));
        }

        private GameObject CreateTerrain(StageObjectType type, string name, Vector2 position, Vector2 size)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(type, position);
            data.objectId = "2-3_terrain_" + terrainSerial++;
            data.size = size;
            data.keepSeparate = true;
            GameObject created = factory != null ? factory.Create(data, transform) : null;
            if (created != null)
            {
                created.name = name;
                syncManager?.RegisterRuntimeObject(created.transform);
                return created;
            }
            return CreateSolid(name, position, size, new Color(0.93f, 0.9f, 0.72f), new Color(0.12f, 0.34f, 0.48f));
        }

        private GameObject CreateSolid(string name, Vector2 position, Vector2 size, Color fill, Color ink)
        {
            GameObject root = new GameObject(name) { layer = 6, tag = "Ground" };
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            StageEscortController.AddFilledRect(root.transform, "Crayon Fill", Vector2.zero, size, fill, -5);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero, size, ink, -3);
            return root;
        }

        private void CreateScratchBox(string id, Vector2 position, Vector2 size, int hits, bool wall)
        {
            GameObject root;
            if (wall)
            {
                root = new GameObject("Team Scratch Wall");
                root.transform.SetParent(transform, false);
                root.transform.position = position;
                int rows = Mathf.Max(1, Mathf.CeilToInt(size.y / 1.4f));
                for (int row = 0; row < rows; row++)
                    CreateWoodBox(id + "_piece_" + row, root.transform,
                        position + new Vector2(0f, (row - (rows - 1) * 0.5f) * (size.y / rows)),
                        new Vector2(size.x, size.y / rows), true);
            }
            else root = CreateWoodBox(id, transform, position, size, false);
            StageCatEscapeScratchTarget target = root.AddComponent<StageCatEscapeScratchTarget>();
            target.Configure(this, id, hits, wall);
            scratchTargets[id] = target;
        }

        private void CreateTeamWall(string id, float x, int hits)
        {
            float bottom = LaneY[playerCount - 1] - 0.1f;
            float top = LaneY[0] + 6.7f;
            GameObject root = new GameObject("Shared Scratch Wall " + hits)
            {
                layer = 6,
                tag = "Ground"
            };
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(x, (bottom + top) * 0.5f, 0f);
            BoxCollider2D sharedCollider = root.AddComponent<BoxCollider2D>();
            sharedCollider.size = new Vector2(2.2f, top - bottom);

            for (int lane = 0; lane < playerCount; lane++)
            {
                float localY = LaneY[lane] + 3.3f - root.transform.position.y;
                Color fill = hits == 10
                    ? new Color(0.28f, 0.52f, 0.72f)
                    : hits == 20
                        ? new Color(0.52f, 0.35f, 0.68f)
                        : new Color(0.68f, 0.25f, 0.3f);
                StageEscortController.AddFilledRect(root.transform, "Scratch Wall Panel " + lane,
                    new Vector2(0f, localY), new Vector2(2.15f, 6.55f), fill, 16);
                StageEscortController.AddBoxOutline(root.transform, new Vector2(0f, localY),
                    new Vector2(2.2f, 6.6f), new Color(0.12f, 0.1f, 0.16f), 18);
                for (int claw = -1; claw <= 1; claw++)
                {
                    float offset = claw * 0.42f;
                    StageGun.AddLine(root.transform, "Claw Mark " + lane + "-" + claw, new[]
                    {
                        new Vector2(-0.66f + offset, localY + 1.48f),
                        new Vector2(0.58f + offset, localY - 0.05f)
                    }, 0.1f, new Color(1f, 0.88f, 0.65f), 20);
                }
            }

            StageCatEscapeScratchTarget target = root.AddComponent<StageCatEscapeScratchTarget>();
            target.Configure(this, id, hits, true);
            for (int lane = 0; lane < playerCount; lane++)
                target.AddCounterAt(new Vector3(0f,
                    LaneY[lane] + 1.55f - root.transform.position.y, -0.08f));
            scratchTargets[id] = target;
        }

        private GameObject CreateWoodBox(string id, Transform parent, Vector2 position, Vector2 size, bool fixedInPlace)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.WoodBox, position);
            data.objectId = id;
            data.size = size;
            GameObject box = factory != null ? factory.Create(data, parent) : null;
            if (box == null) return CreateSolid("Wood Box", position, size,
                new Color(0.9f, 0.66f, 0.3f), new Color(0.35f, 0.16f, 0.08f));
            if (fixedInPlace)
            {
                Rigidbody2D body = box.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.constraints = RigidbodyConstraints2D.FreezeAll;
                }
                CarryableObject carryable = box.GetComponent<CarryableObject>();
                if (carryable != null) carryable.enabled = false;
            }
            else
            {
                Rigidbody2D body = box.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.sleepMode = RigidbodySleepMode2D.StartAwake;
                    body.WakeUp();
                }
            }
            syncManager?.RegisterRuntimeObject(box.transform);
            return box;
        }

        private StageCatEscapeGate CreateGate(string name, Vector2 position, Vector2 size)
        {
            GameObject root = CreateSolid(name, position, size,
                new Color(0.37f, 0.42f, 0.48f), new Color(0.9f, 0.2f, 0.16f));
            StageCatEscapeGate gate = root.AddComponent<StageCatEscapeGate>();
            gate.Configure(size);
            return gate;
        }

        private void CreateButton(int set, int lane, Vector2 position)
        {
            GameObject root = new GameObject("Cross Lane Button " + lane);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(2f, 0.65f);
            trigger.isTrigger = true;
            int targetLane = playerCount == 1 ? 0 : (lane + set + 1) % playerCount;
            Color targetColor = PlayerColorPalette.GetColor(targetLane);
            StageEscortController.AddFilledRect(root.transform, "Button Base", new Vector2(0f, -0.18f),
                new Vector2(2.1f, 0.24f), new Color(0.18f, 0.2f, 0.22f), 10);
            StageEscortController.AddFilledRect(root.transform, "Colored Pressure Plate", new Vector2(0f, 0.02f),
                new Vector2(1.45f, 0.2f), targetColor, 12);
            StageEscortController.AddBoxOutline(root.transform, new Vector2(0f, 0.02f),
                new Vector2(1.55f, 0.3f), targetColor, 13);
            root.AddComponent<StageCatEscapeButton>().Configure(this, set, lane);
        }

        private void CreateDropRoom()
        {
            float left = GetRoomLeft();
            float right = GetRoomRight();
            const float roomCeilingY = 15.5f;
            CreateWall(left, 4.5f, roomCeilingY);
            CreateWall(right, -0.7f, roomCeilingY);
            CreateCeiling(left, right, roomCeilingY);
            roomEntranceGate = CreateGate("Drop Room Entrance", new Vector2(left, 1.9f),
                new Vector2(0.75f, 5.2f));
            roomEntranceGate.SetOpen(true);

            GameObject monitor = new GameObject("Drop Room Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3((left + right) * 0.5f, 6.65f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform,
                new Vector2(Mathf.Min(9f, right - left - 1f), 1.65f), 28);
            roomStatus = StageEscortController.CreateText(monitor.transform, "Room Count",
                new Vector3(0f, 0f, -0.04f), 38, 0.095f,
                new Color(0.12f, 0.4f, 0.72f), 31);

            for (int lane = 0; lane < playerCount; lane++)
            {
                float doorLeft = lane == 0
                    ? left
                    : (GetShaftX(lane - 1) + GetShaftX(lane)) * 0.5f;
                float doorRight = lane == playerCount - 1
                    ? right
                    : (GetShaftX(lane) + GetShaftX(lane + 1)) * 0.5f;
                trapdoorFloors[lane] = CreateTerrain(StageObjectType.Platform,
                    "Drop Room Trapdoor " + lane,
                    new Vector2((doorLeft + doorRight) * 0.5f, -0.35f),
                    new Vector2(doorRight - doorLeft, 0.7f));
                GameObject marker = new GameObject("Player Position " + lane);
                marker.transform.SetParent(transform, false);
                marker.transform.position = new Vector3(GetShaftX(lane), 0.12f, 0f);
                Color color = PlayerColorPalette.GetColor(lane);
                StageEscortController.AddFilledRect(marker.transform, "Marker Fill", Vector2.zero,
                    new Vector2(1.45f, 0.22f), new Color(color.r, color.g, color.b, 0.34f), 24);
                StageEscortController.AddBoxOutline(marker.transform, Vector2.zero,
                    new Vector2(1.55f, 0.32f), color, 26);
                marker.SetActive(false);
                positionMarkers[lane] = marker;
            }
        }

        private void CreateGoal(Vector2 position)
        {
            StageObjectData data = StageObjectFactory.CreateDefaultData(StageObjectType.Goal, position);
            data.objectId = "2-3_goal";
            data.size = new Vector2(2.2f, 3.2f);
            factory?.Create(data, transform);
        }

        private void BuildMonitor()
        {
            GameObject monitor = new GameObject("2-3 Start Monitor");
            monitor.transform.SetParent(transform, false);
            monitor.transform.position = new Vector3(4f, 6.4f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(10.5f, 2.5f), -25);
            monitorTitle = StageEscortController.CreateText(monitor.transform, "Title",
                new Vector3(0f, 0.5f, -0.03f), 48, 0.105f, new Color(0.12f, 0.32f, 0.5f), -20);
            monitorStatus = StageEscortController.CreateText(monitor.transform, "Status",
                new Vector3(0f, -0.55f, -0.03f), 54, 0.12f, new Color(0.9f, 0.32f, 0.08f), -20);
        }

        private void RefreshMonitor()
        {
            if (monitorTitle == null) return;
            monitorTitle.text = LocalizationManager.T("cat_escape_title");
            if (phase == 0) monitorStatus.text = "READY";
            else if (phase == 1) monitorStatus.text = LocalizationManager.Format("cat_escape_ready", Mathf.CeilToInt(clock));
            else monitorStatus.text = LocalizationManager.T("cat_escape_go");
            if (roomStatus != null)
            {
                if (roomPhase <= 4)
                {
                    GetRoomOccupancy(out int inside, out int living);
                    roomStatus.text = LocalizationManager.Format("cat_escape_room_count", inside, living);
                }
                else if (roomPhase == 5)
                {
                    roomStatus.text = LocalizationManager.Format(
                        "cat_escape_room_countdown", Mathf.CeilToInt(roomClock));
                }
                else
                {
                    roomStatus.text = string.Empty;
                }
            }
        }

        private void BuildGiantCats()
        {
            giantCat = CreateGiantCat("Giant Pursuer", new Vector2(bossX, 2.1f), 1.25f);
            for (int i = 0; i < playerCount; i++)
            {
                laneCats[i] = CreateGiantCat("Tunnel Giant Cat " + i,
                    new Vector2(laneBossXs[i], LaneY[i] + 1.7f), 0.72f);
                laneCats[i].gameObject.SetActive(false);
            }
        }

        private Transform CreateGiantCat(string name, Vector2 position, float scale)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.position = position;
            root.localScale = Vector3.one * scale;
            Sprite doodle = Resources.Load<Sprite>("StageObjects/NicoDraw/giant-cat-chaser");
            if (doodle != null)
            {
                GameObject image = new GameObject("Child Doodle Giant Cat");
                image.transform.SetParent(root, false);
                SpriteRenderer renderer = image.AddComponent<SpriteRenderer>();
                renderer.sprite = doodle;
                renderer.sortingOrder = 35;
                Vector2 bounds = doodle.bounds.size;
                float imageScale = bounds.x > 0.01f ? 8.8f / bounds.x : 1f;
                image.transform.localScale = Vector3.one * imageScale;
                image.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                return root;
            }
            Color fur = new Color(0.34f, 0.18f, 0.42f);
            StageGun.CreateSprite(root, "Huge Body", new Vector2(-0.2f, 0f), new Vector2(4.8f, 3.1f), fur, 30);
            StageGun.CreateSprite(root, "Huge Head", new Vector2(2f, 0.65f), new Vector2(2.5f, 2.4f), fur, 31);
            StageGun.AddLine(root, "Ears", new[]
            {
                new Vector2(1.15f, 1.35f), new Vector2(1.45f, 2.35f), new Vector2(2f, 1.5f),
                new Vector2(2.45f, 2.35f), new Vector2(2.8f, 1.25f)
            }, 0.16f, new Color(0.16f, 0.05f, 0.2f), 34);
            for (int i = 0; i < 3; i++)
                StageGun.AddLine(root, "Claw " + i, new[]
                {
                    new Vector2(2.5f, -0.4f + i * 0.25f), new Vector2(4.1f, -0.8f + i * 0.16f)
                }, 0.11f, new Color(0.92f, 0.2f, 0.18f), 35);
            return root;
        }

        private void UpdateBossVisuals()
        {
            if (giantCat != null)
            {
                giantCat.position = new Vector3(bossX, 2.1f, 0f);
                bool visible = phase == 2 && mainCatFade > 0.01f && bossX < 112f;
                giantCat.gameObject.SetActive(visible);
                if (visible) SetCatAlpha(giantCat, mainCatFade);
            }
            for (int i = 0; i < playerCount; i++)
            {
                if (laneCats[i] == null) continue;
                laneCats[i].position = new Vector3(laneBossXs[i], LaneY[i] + 1.7f, 0f);
                bool visible = phase == 2 && laneChases[i] && laneCatFades[i] > 0.01f
                    && laneBossXs[i] < 232f;
                laneCats[i].gameObject.SetActive(visible);
                if (visible) SetCatAlpha(laneCats[i], laneCatFades[i]);
            }
        }

        private static void SetCatAlpha(Transform cat, float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            SpriteRenderer[] sprites = cat.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                Color color = sprites[i].color;
                color.a = alpha;
                sprites[i].color = color;
            }
            LineRenderer[] lines = cat.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                Color start = lines[i].startColor;
                Color end = lines[i].endColor;
                start.a = alpha;
                end.a = alpha;
                lines[i].startColor = start;
                lines[i].endColor = end;
            }
        }

        private void SetLocalControls(bool enabledValue)
        {
            PlayerController2D local = stageManager?.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>() : null;
            local?.SetControlsEnabled(enabledValue && !IsEliminated(local));
        }

        private void SetLivingControls(bool enabledValue)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && !IsEliminated(players[i]))
                {
                    players[i].SetControlsEnabled(enabledValue);
                    if (!enabledValue) players[i].ResetMotion();
                }
        }

        private void ShowPositionMarkers(bool visible)
        {
            for (int i = 0; i < playerCount; i++)
                if (positionMarkers[i] != null) positionMarkers[i].SetActive(visible);
        }

        private void ApplyRoomVisualState()
        {
            roomEntranceGate?.SetOpen(roomPhase == 0);
            ShowPositionMarkers(roomPhase == 4 || roomPhase == 5);
            for (int i = 0; i < playerCount; i++)
                if (trapdoorFloors[i] != null) trapdoorFloors[i].SetActive(roomPhase < 6);
            SetLocalControls(phase == 2 && (roomPhase == 0 || roomPhase == 4 || roomPhase >= 6));
        }

        private void PositionPlayersAtStart()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null) continue;
                Vector3 position = new Vector3(-4.5f + i * 1.55f, 4f, -0.2f);
                player.transform.position = position;
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
                player.ResetMotion();
                StageMirrorFinalBossController.AlignCharacterBottomToSurface(player.transform, 0.02f);
                if (body != null) body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            Physics2D.SyncTransforms();
        }

        private void CaptureParticipants()
        {
            participants.Clear();
            playerLanes.Clear();
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            System.Array.Sort(players, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            for (int i = 0; i < players.Length; i++)
            {
                string id = ResolvePlayerId(players[i]);
                if (string.IsNullOrEmpty(id)) continue;
                participants.Add(id);
                int lane = IsOnline
                    ? PlayerColorPalette.GetLobbyPlayerSlot(onlineManager?.CurrentLobby, id)
                    : i;
                playerLanes[id] = Mathf.Clamp(lane, 0, playerCount - 1);
            }
        }

        private string ResolvePlayerId(PlayerController2D player)
        {
            if (player == null) return null;
            return IsOnline ? stageManager.GetOnlinePlayerId(player) : "local_" + player.GetInstanceID();
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

        private bool IsEliminated(PlayerController2D player)
        {
            string id = ResolvePlayerId(player);
            return !string.IsNullOrEmpty(id) && eliminated.Contains(id);
        }

        private void ApplyElimination(string id)
        {
            if (string.IsNullOrEmpty(id) || !eliminated.Add(id)) return;
            PlayerController2D player = ResolvePlayer(id);
            if (player != null)
            {
                player.GetComponent<PlayerCarryController>()?.ForceDrop();
                player.ResetMotion();
                player.SetControlsEnabled(false);
                stageManager?.NotifyPlayerEliminated(player);
                player.gameObject.SetActive(false);
            }
            GameSfx.Play(SfxId.PlayerDeath);
        }

        private void BroadcastState(bool immediate)
        {
            if (!HasAuthority || !IsOnline || onlineManager == null) return;
            if (!immediate && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.25f;
            List<string> ids = new List<string>();
            List<int> hits = new List<int>();
            foreach (KeyValuePair<string, StageCatEscapeScratchTarget> pair in scratchTargets)
            {
                ids.Add(pair.Key);
                hits.Add(pair.Value != null ? pair.Value.CurrentHits : 0);
            }
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(new EscapeState
                {
                    Sequence = ++sequence, Phase = phase, Clock = clock,
                    BossX = bossX, LaneBossXs = (float[])laneBossXs.Clone(),
                    TargetIds = ids.ToArray(), TargetHits = hits.ToArray(),
                    Buttons = (bool[])buttons.Clone(),
                    EliminatedIds = new List<string>(eliminated).ToArray(),
                    MainCatFade = mainCatFade, LaneCatFades = (float[])laneCatFades.Clone(),
                    MainCatRetreating = mainCatRetreating,
                    LaneChases = (bool[])laneChases.Clone(),
                    RoomPhase = roomPhase, RoomClock = roomClock
                })
            });
        }

        private void HandleNetworkData(OnlineGimmickData message)
        {
            if (message == null || message.ObjectId != StageId || onlineManager == null) return;
            if (message.Kind == ScratchRequestKind && HasAuthority)
            {
                IdRequest request = JsonUtility.FromJson<IdRequest>(message.Json);
                if (request != null && IsRequestNearTarget(message.PlayerId, request.Id)) ApplyScratch(request.Id);
            }
            else if (message.Kind == ButtonRequestKind && HasAuthority)
            {
                ButtonRequest request = JsonUtility.FromJson<ButtonRequest>(message.Json);
                PlayerController2D source = request != null ? stageManager.GetOnlinePlayerController(message.PlayerId) : null;
                if (request != null && source != null)
                {
                    int set = Mathf.Clamp(request.Set, 0, SwitchSetCount - 1);
                    int lane = Mathf.Clamp(request.Lane, 0, playerCount - 1);
                    float buttonX = 148f + set * 26f;
                    Vector2 buttonPosition = new Vector2(buttonX, LaneY[lane] + 0.42f);
                    if (Vector2.Distance(source.transform.position, buttonPosition) <= 4f) ApplyButton(set, lane);
                }
            }
            else if (message.Kind == StateKind && !HasAuthority && onlineManager.IsHostPlayer(message.PlayerId))
            {
                if (stageManager != null && stageManager.IsChallengeReadyRoomActive) return;
                EscapeState incoming = JsonUtility.FromJson<EscapeState>(message.Json);
                if (incoming == null) return;
                if (!receivedFreshRunState)
                {
                    bool looksLikeFreshStart = incoming.RoomPhase == 0
                        && incoming.Phase <= 2 && incoming.BossX <= -10f;
                    if (!looksLikeFreshStart) return;
                    receivedFreshRunState = true;
                }
                if (incoming.Sequence <= receivedSequence) return;
                receivedSequence = incoming.Sequence;
                phase = incoming.Phase;
                clock = incoming.Clock;
                bossX = incoming.BossX;
                mainCatFade = incoming.MainCatFade;
                mainCatRetreating = incoming.MainCatRetreating;
                roomPhase = incoming.RoomPhase;
                roomClock = incoming.RoomClock;
                if (incoming.LaneBossXs != null)
                    for (int i = 0; i < Mathf.Min(playerCount, incoming.LaneBossXs.Length); i++)
                        laneBossXs[i] = incoming.LaneBossXs[i];
                if (incoming.LaneCatFades != null)
                    for (int i = 0; i < Mathf.Min(playerCount, incoming.LaneCatFades.Length); i++)
                        laneCatFades[i] = incoming.LaneCatFades[i];
                if (incoming.LaneChases != null)
                    for (int i = 0; i < Mathf.Min(playerCount, incoming.LaneChases.Length); i++)
                        laneChases[i] = incoming.LaneChases[i];
                if (incoming.TargetIds != null && incoming.TargetHits != null)
                    for (int i = 0; i < Mathf.Min(incoming.TargetIds.Length, incoming.TargetHits.Length); i++)
                        if (scratchTargets.TryGetValue(incoming.TargetIds[i], out StageCatEscapeScratchTarget target))
                            target?.ApplyNetworkHits(incoming.TargetHits[i]);
                if (incoming.Buttons != null)
                    for (int i = 0; i < Mathf.Min(buttons.Length, incoming.Buttons.Length); i++)
                        if (incoming.Buttons[i]) ApplyButtonVisual(i);
                if (incoming.EliminatedIds != null)
                    for (int i = 0; i < incoming.EliminatedIds.Length; i++)
                        ApplyElimination(incoming.EliminatedIds[i]);
                if (roomPhase >= 6)
                    foreach (string id in participants)
                        if (!eliminated.Contains(id)) assignedToShaft.Add(id);
                ApplyRoomVisualState();
            }
            else if (message.Kind == EliminateRequestKind && HasAuthority)
            {
                EliminationRequest request = JsonUtility.FromJson<EliminationRequest>(message.Json);
                if (request != null && request.PlayerId == message.PlayerId)
                {
                    PlayerController2D player = ResolvePlayer(request.PlayerId);
                    if (player != null) RequestElimination(player);
                }
            }
        }

        private bool IsRequestNearTarget(string playerId, string id)
        {
            PlayerController2D source = stageManager.GetOnlinePlayerController(playerId);
            return source != null && scratchTargets.TryGetValue(id, out StageCatEscapeScratchTarget target)
                && target != null && target.DistanceTo(source.transform.position) <= 5f;
        }

        private void ApplyButtonVisual(int index)
        {
            int set = Mathf.Clamp(index / 4, 0, SwitchSetCount - 1);
            int lane = Mathf.Clamp(index % 4, 0, playerCount - 1);
            buttons[index] = true;
            int targetLane = playerCount == 1 ? 0 : (lane + set + 1) % playerCount;
            switchGates[set, targetLane]?.SetOpen(true);
        }

        private void Send<T>(string kind, T payload)
        {
            onlineManager?.SendGimmickData(new OnlineGimmickData
            { ObjectId = StageId, Kind = kind, Json = JsonUtility.ToJson(payload) });
        }
    }

    public sealed class StageCatEscapeScratchTarget : MonoBehaviour
    {
        private StageCatEscapeController owner;
        private string targetId;
        private int requiredHits;
        private int currentHits;
        private bool wall;
        private readonly List<TextMesh> labels = new List<TextMesh>();
        public bool IsBroken { get; private set; }
        public int CurrentHits => currentHits;

        public void Configure(StageCatEscapeController controller, string id, int hits, bool isWall)
        {
            owner = controller;
            targetId = id;
            requiredHits = Mathf.Max(1, hits);
            wall = isWall;
        }

        public void AddCounterAt(Vector3 localPosition)
        {
            TextMesh counter = StageEscortController.CreateText(transform, "Scratch Count",
                localPosition, 50, 0.08f, Color.white, 22);
            labels.Add(counter);
            RefreshLabel();
        }

        public void HitByCatScratch(PlayerController2D source) => owner?.RequestScratch(targetId, source);

        public float DistanceTo(Vector2 position)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D current = colliders[i];
                if (current == null || !current.enabled) continue;
                closest = Mathf.Min(closest, Vector2.Distance(position, current.ClosestPoint(position)));
            }
            return float.IsPositiveInfinity(closest)
                ? Vector2.Distance(position, transform.position)
                : closest;
        }

        public void ApplyHit() => ApplyNetworkHits(currentHits + 1);

        public void ApplyNetworkHits(int hits)
        {
            if (IsBroken) return;
            currentHits = Mathf.Clamp(hits, 0, requiredHits);
            RefreshLabel();
            if (currentHits < requiredHits) return;
            IsBroken = true;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            GameSfx.PlayAt(SfxId.CrateBreak, transform.position, wall ? 1.4f : 1f);
            if (!wall)
            {
                gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null) labels[i].gameObject.SetActive(false);
            Rigidbody2D[] pieces = GetComponentsInChildren<Rigidbody2D>(true);
            if (pieces.Length == 0)
            {
                gameObject.SetActive(false);
                return;
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                Rigidbody2D body = pieces[i];
                if (body == null) continue;
                body.transform.SetParent(transform.parent, true);
                Collider2D[] pieceColliders = body.GetComponents<Collider2D>();
                for (int c = 0; c < pieceColliders.Length; c++) pieceColliders[c].enabled = true;
                body.bodyType = RigidbodyType2D.Dynamic;
                body.constraints = RigidbodyConstraints2D.None;
                body.linearVelocity = new Vector2(Random.Range(-2.4f, 2.4f), Random.Range(2f, 5f));
                body.angularVelocity = Random.Range(-150f, 150f);
                Destroy(body.gameObject, 1.6f);
            }
            Destroy(gameObject, 1.7f);
        }

        private void RefreshLabel()
        {
            string text = LocalizationManager.Format("cat_escape_wall_counter", currentHits, requiredHits);
            for (int i = 0; i < labels.Count; i++)
                if (labels[i] != null) labels[i].text = text;
        }
    }

    public sealed class StageCatEscapeGate : MonoBehaviour
    {
        private Collider2D gateCollider;
        private Renderer[] renderers;
        public void Configure(Vector2 size)
        {
            gateCollider = GetComponent<Collider2D>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }
        public void SetOpen(bool open)
        {
            if (gateCollider != null) gateCollider.enabled = !open;
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = !open;
        }
    }

    public sealed class StageCatEscapeButton : MonoBehaviour
    {
        private StageCatEscapeController owner;
        private int set;
        private int lane;
        private bool pressed;
        public void Configure(StageCatEscapeController controller, int setIndex, int laneIndex)
        {
            owner = controller;
            set = setIndex;
            lane = laneIndex;
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (pressed) return;
            PlayerController2D player = other != null ? other.GetComponentInParent<PlayerController2D>() : null;
            if (player == null) return;
            pressed = true;
            owner?.RequestButton(set, lane, player);
        }
    }

}
