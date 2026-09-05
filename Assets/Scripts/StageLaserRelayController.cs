using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageLaserRelayController : MonoBehaviour
    {
        private const string StageId = "14-3";
        private const string StateKind = "laser_relay_state";
        private const float RoundSeconds = 60f;
        private const float FloorY = -6.4f;
        private const float RoomHalfWidth = 18f;
        private const float RayDistance = 80f;
        private const float BoxPreviewSeconds = 1.35f;
        private const float BoxCooldownSeconds = 0.55f;
        private const float GoalChargeSeconds = 1.2f;

        private static readonly Vector2[] BoxSizes =
        {
            new Vector2(0.9f, 0.9f), new Vector2(1.5f, 1.5f),
            new Vector2(2.2f, 2.2f), new Vector2(1.1f, 2.7f),
            new Vector2(2.5f, 0.8f)
        };

        private enum RelayPhase { Active, RoundClear, Failed, Complete }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Round;
            public int Phase;
            public float Remaining;
            public int GoalMask;
            public float[] GoalCharges;
            public int PreviewIndex;
            public bool ButtonPressed;
        }

        private sealed class BeamState
        {
            public int Index;
            public Vector2 Origin;
            public Vector2 Direction;
            public GameObject Source;
            public LineRenderer Glow;
            public LineRenderer Core;
            public readonly List<SpriteRenderer> ReflectionMarks = new List<SpriteRenderer>(4);
            public readonly List<Vector3> Points = new List<Vector3>(7);
            public readonly HashSet<PlayerController2D> ReflectedPlayers = new HashSet<PlayerController2D>();
            public bool RouteValid;
            public int ReachedGoalIndex = -1;
        }

        private sealed class GoalState
        {
            public int Index;
            public Vector2 Position;
            public SpriteRenderer Core;
            public SpriteRenderer Ring;
            public SpriteRenderer ChargeCore;
            public Transform RaysRoot;
            public Color PairColor;
            public float Charge;
        }

        private StageManager stageManager;
        private StageLoader stageLoader;
        private StageObjectFactory factory;
        private OnlineManager onlineManager;
        private CameraFollow2D cameraFollow;
        private Camera gameCamera;
        private Transform arenaRoot;
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private StageBoxDropper boxDropper;
        private Collider2D boxButton;
        private Transform buttonCap;
        private Vector3 buttonCapScale;
        private SpriteRenderer buttonGlow;
        private Transform boxPreview;
        private readonly List<BeamState> beams = new List<BeamState>(3);
        private readonly List<GoalState> goals = new List<GoalState>(3);
        private readonly List<PlayerController2D> orderedPlayers = new List<PlayerController2D>(4);
        private readonly HashSet<Collider2D> relayBlockers = new HashSet<Collider2D>();
        private readonly List<Vector2> relaySpawnPositions = new List<Vector2>(4);
        private readonly List<Vector2> authoredBeamOrigins = new List<Vector2>(3);
        private readonly List<Vector2> authoredGoalPositions = new List<Vector2>(3);
        private readonly List<StageObjectData> authoredWalls = new List<StageObjectData>();
        private Vector2 beamSourceAnchor;
        private Vector2 beamGoalAnchor;
        private float remaining;
        private float transitionRemaining;
        private float nextBroadcastAt;
        private float nextPreviewAt;
        private float nextBoxAt;
        private float roundReadyAt;
        private int round = 1;
        private int goalMask;
        private int previewIndex;
        private int sequence;
        private int receivedSequence;
        private int playerCount;
        private bool buttonPressed;
        private bool initialized;
        private bool cameraCaptured;
        private bool previousCameraFollowEnabled;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private RelayPhase phase;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            cameraFollow = Object.FindFirstObjectByType<CameraFollow2D>();
            gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (onlineManager == null) onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkState;
        }

        private void OnDisable()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkState;
            RestoreCamera();
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
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject obsolete = transform.GetChild(i).gameObject;
                obsolete.SetActive(false);
                Destroy(obsolete);
            }
            initialized = true;
            CaptureCamera();
            BeginRound(1);
        }

        private void Update()
        {
            if (!initialized || stageManager == null || stageManager.CurrentStageId != StageId) return;
            ConfigureCamera();
            RefreshPlayerOrder();
            TraceAllBeams();
            AnimateBeamEffects();

            if (!HasAuthority)
            {
                if (phase == RelayPhase.Active) remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                RefreshMonitor();
                return;
            }

            switch (phase)
            {
                case RelayPhase.Active:
                    remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    UpdateBoxStation();
                    UpdateGoalCharging();
                    if (AllGoalsReached()) ClearRound();
                    else if (remaining <= 0f) FailRound();
                    break;
                case RelayPhase.RoundClear:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(round + 1);
                    break;
                case RelayPhase.Failed:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(1);
                    break;
            }
            BroadcastState();
            RefreshMonitor();
        }

        private void BeginRound(int nextRound)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            phase = RelayPhase.Active;
            remaining = RoundSeconds;
            transitionRemaining = 0f;
            goalMask = 0;
            roundReadyAt = Time.unscaledTime + 0.45f;
            previewIndex = (round - 1) % BoxSizes.Length;
            buttonPressed = false;
            nextPreviewAt = Time.unscaledTime + BoxPreviewSeconds;
            nextBoxAt = 0f;
            playerCount = ResolveRelayPlayerCount();
            BuildArena();
            StartCoroutine(PositionPlayersAfterBuild());
            BroadcastState(true);
            RefreshMonitor();
        }

        private void BuildArena()
        {
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
            }
            beams.Clear();
            goals.Clear();
            orderedPlayers.Clear();
            relayBlockers.Clear();
            relaySpawnPositions.Clear();
            authoredBeamOrigins.Clear();
            authoredGoalPositions.Clear();
            authoredWalls.Clear();
            boxDropper = null;
            boxButton = null;

            GameObject arena = new GameObject("14-3 Laser Relay Round " + round);
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;
            CreateRoom();
            LoadAuthoredLayout();
            CreatePlayerCountBaffles();
            CreateBeams();
            CreateBoxStation();
            CreateMonitor();
            stageLoader?.SetRuntimeSpawnPosition(new Vector2(-9f, FloorY + 1.35f));
            ConfigureCamera();
        }

        private void CreateRoom()
        {
            const float wallThickness = 0.65f;
            const float floorThickness = 0.8f;
            const float ceilingThickness = 0.65f;
            float innerWidth = RoomHalfWidth * 2f - wallThickness;
            float floorTop = FloorY + floorThickness * 0.5f;
            float ceilingY = 7.4f;
            float ceilingBottom = ceilingY - ceilingThickness * 0.5f;
            float wallHeight = ceilingBottom - floorTop;
            float wallCenterY = (ceilingBottom + floorTop) * 0.5f;
            StageEscortController.AddFilledRect(arenaRoot, "Laser Relay Room", new Vector2(0f, 0.3f),
                new Vector2(RoomHalfWidth * 2f, 15f), new Color(0.91f, 0.94f, 0.83f, 0.38f), -60);
            CreateSolid("Laser Relay Floor", new Vector2(0f, FloorY), new Vector2(innerWidth, floorThickness));
            StageRedrawZoneFactory.CreateRuntimeFloorZone(arenaRoot,
                "14-3_runtime_redraw_zone_" + round,
                new Vector2(0f, FloorY), innerWidth, ceilingY - FloorY);
            CreateSolid("Laser Relay Ceiling", new Vector2(0f, ceilingY), new Vector2(innerWidth, ceilingThickness));
            CreateSolid("Laser Relay Left Wall", new Vector2(-RoomHalfWidth, wallCenterY),
                new Vector2(wallThickness, wallHeight));
            CreateSolid("Laser Relay Right Wall", new Vector2(RoomHalfWidth, wallCenterY),
                new Vector2(wallThickness, wallHeight));
        }

        private void CreateSolid(string name, Vector2 position, Vector2 size)
        {
            CreateSolid(name, position, size, 0f);
        }

        private void CreateSolid(string name, Vector2 position, Vector2 size, float rotation)
        {
            GameObject solid = new GameObject(name);
            solid.transform.SetParent(arenaRoot, false);
            solid.transform.position = position;
            solid.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            solid.layer = 6;
            solid.tag = "Ground";
            BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
            collider.size = size;
            relayBlockers.Add(collider);
            StageEscortController.AddFilledRect(solid.transform, "Paper", Vector2.zero, size,
                new Color(0.7f, 0.78f, 0.55f, 0.72f), 3);
            StageEscortController.AddPencilHatchingRect(solid.transform, "Pencil Hatching",
                Vector2.zero, size, new Color(0.18f, 0.34f, 0.12f, 0.3f), 4,
                Mathf.Min(size.x, size.y) < 0.75f ? 0.24f : 0.42f);
            StageEscortController.AddBoxOutline(solid.transform, Vector2.zero, size,
                new Color(0.18f, 0.28f, 0.12f, 0.9f), 5);
        }

        private int ResolveRelayPlayerCount()
        {
            int count = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            PlayerController2D[] activePlayers = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            count = Mathf.Max(count, activePlayers.Length);
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers != null)
            {
                int lobbyCount = 0;
                for (int i = 0; i < lobbyPlayers.Length; i++)
                    if (lobbyPlayers[i] != null && !string.IsNullOrEmpty(lobbyPlayers[i].PlayerId))
                        lobbyCount++;
                count = Mathf.Max(count, lobbyCount);
            }
            return Mathf.Clamp(count, 1, 4);
        }

        private void CreatePlayerCountBaffles()
        {
            if (authoredWalls.Count > 0 && relaySpawnPositions.Count > 0
                && authoredBeamOrigins.Count >= round && authoredGoalPositions.Count >= round)
            {
                for (int i = 0; i < authoredWalls.Count; i++)
                {
                    StageObjectData wall = authoredWalls[i];
                    CreateSolid("Authored Relay Wall " + (i + 1), wall.position, wall.size, wall.rotation);
                }
                beamSourceAnchor = authoredBeamOrigins[0];
                beamGoalAnchor = authoredGoalPositions[0];
                return;
            }

            // Round two mirrors the route. The other rounds retain the same
            // readable maze while increasing the number of simultaneous beams.
            float mirror = round == 2 ? -1f : 1f;
            beamSourceAnchor = MirrorX(new Vector2(-17.35f, -5.18f), mirror);

            if (playerCount == 1)
            {
                // One player stands on the main floor. The ceiling stem blocks
                // only the emitter-to-bulb shortcut; emitter -> player -> bulb
                // remains a clear, single-reflection route below its tip.
                CreateCeilingStem("Single Relay Ceiling Blocker", 0.15f, 1.2f, mirror);
                relaySpawnPositions.Add(MirrorX(new Vector2(-3.2f, -5.05f), mirror));
                beamGoalAnchor = MirrorX(new Vector2(10.4f, 5.82f), mirror);
            }
            else if (playerCount == 2)
            {
                CreateHookedShelf("Two Relay Lower Maze", false, -2.55f, -2.1f, -1f, mirror);
                CreateHookedShelf("Two Relay Upper Maze", false, 2.7f, -3.8f, -1f, mirror);
                relaySpawnPositions.Add(MirrorX(new Vector2(7.2f, -5.05f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(-2.75f, -1.3f), mirror));
                beamGoalAnchor = MirrorX(new Vector2(10.2f, 5.82f), mirror);
            }
            else if (playerCount == 3)
            {
                CreateHookedShelf("Three Relay Lower Maze", false, -2.7f, -2f, -1f, mirror);
                CreateHookedShelf("Three Relay Middle Maze", true, 0.75f, 2.15f, -1f, mirror);
                CreateHookedShelf("Three Relay Upper Maze", true, 4.35f, 3.6f, -1f, mirror);
                relaySpawnPositions.Add(MirrorX(new Vector2(7.3f, -5.05f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(-2.7f, -1.45f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(2.85f, 2.05f), mirror));
                beamGoalAnchor = MirrorX(new Vector2(-10.2f, 5.82f), mirror);
            }
            else
            {
                // Four distinct turning chambers, matching the player's sketch:
                // floor -> lower-left tip -> middle-right tip -> upper-left tip.
                CreateHookedShelf("Four Relay Lower Left Room", false, -3.35f, -1.8f, -1f, mirror);
                CreateHookedShelf("Four Relay Middle Right Room", true, -0.65f, 2.15f, -1f, mirror);
                CreateHookedShelf("Four Relay Upper Left Room", false, 2.05f, -2.15f, -1f, mirror);
                CreateHookedShelf("Four Relay Goal Room", true, 4.72f, 3.25f, -1f, mirror);
                CreateCeilingStem("Four Relay Ceiling Spine", 0.35f, 2.15f, mirror);
                relaySpawnPositions.Add(MirrorX(new Vector2(7.4f, -5.05f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(-2.55f, -2.12f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(2.85f, 0.58f), mirror));
                relaySpawnPositions.Add(MirrorX(new Vector2(-2.85f, 3.28f), mirror));
                beamGoalAnchor = MirrorX(new Vector2(-10.5f, 5.82f), mirror);
            }
        }

        private void CreateHookedShelf(
            string name, bool fromRight, float y, float innerTipX,
            float hookDirection, float mirror)
        {
            fromRight = mirror < 0f ? !fromRight : fromRight;
            innerTipX *= mirror;
            const float innerSide = 17.675f;
            float sideX = fromRight ? innerSide : -innerSide;
            float length = Mathf.Abs(sideX - innerTipX);
            float centerX = (sideX + innerTipX) * 0.5f;
            const float thickness = 0.72f;
            CreateSolid(name + " Shelf", new Vector2(centerX, y), new Vector2(length, thickness));

            const float hookHeight = 1.45f;
            float hookY = y + hookDirection * (hookHeight * 0.5f - thickness * 0.25f);
            CreateSolid(name + " Inner Hook", new Vector2(innerTipX, hookY),
                new Vector2(0.72f, hookHeight));
        }

        private void CreateCeilingStem(string name, float x, float bottomY, float mirror)
        {
            const float ceilingBottom = 7.075f;
            float height = Mathf.Max(0.5f, ceilingBottom - bottomY);
            CreateSolid(name, new Vector2(x * mirror, bottomY + height * 0.5f),
                new Vector2(0.82f, height));
        }

        private static Vector2 MirrorX(Vector2 point, float mirror)
        {
            point.x *= mirror;
            return point;
        }

        public static string GetEditorLayoutPrefix(int players, int targetRound)
        {
            return $"14-3-layout-p{Mathf.Clamp(players, 1, 4)}-r{Mathf.Clamp(targetRound, 1, 3)}-";
        }

        public static List<StageObjectData> CreateEditorLayoutDefaults(int players, int targetRound)
        {
            players = Mathf.Clamp(players, 1, 4);
            targetRound = Mathf.Clamp(targetRound, 1, 3);
            string prefix = GetEditorLayoutPrefix(players, targetRound);
            float mirror = targetRound == 2 ? -1f : 1f;
            List<StageObjectData> result = new List<StageObjectData>();
            List<Vector2> spawns = new List<Vector2>(4);
            Vector2 baseGoal;

            void Add(string kind, int index, StageObjectType type, Vector2 position, Vector2 size, float rotation = 0f)
            {
                result.Add(new StageObjectData
                {
                    objectId = $"{prefix}{kind}-{index:00}", type = type,
                    position = position, size = size, rotation = rotation, keepSeparate = true
                });
            }

            void Shelf(bool fromRight, float y, float innerTipX, float hookDirection)
            {
                fromRight = mirror < 0f ? !fromRight : fromRight;
                innerTipX *= mirror;
                const float innerSide = 17.675f;
                float sideX = fromRight ? innerSide : -innerSide;
                float length = Mathf.Abs(sideX - innerTipX);
                float centerX = (sideX + innerTipX) * 0.5f;
                int index = result.FindAll(item => item.objectId.Contains("-wall-")).Count + 1;
                Add("wall", index, StageObjectType.Wall, new Vector2(centerX, y), new Vector2(length, 0.72f));
                float hookY = y + hookDirection * (1.45f * 0.5f - 0.72f * 0.25f);
                Add("wall", index + 1, StageObjectType.Wall, new Vector2(innerTipX, hookY), new Vector2(0.72f, 1.45f));
            }

            void Stem(float x, float bottomY)
            {
                const float ceilingBottom = 7.075f;
                float height = Mathf.Max(0.5f, ceilingBottom - bottomY);
                int index = result.FindAll(item => item.objectId.Contains("-wall-")).Count + 1;
                Add("wall", index, StageObjectType.Wall,
                    new Vector2(x * mirror, bottomY + height * 0.5f), new Vector2(0.82f, height));
            }

            if (players == 1)
            {
                Stem(0.15f, 1.2f);
                spawns.Add(MirrorX(new Vector2(-3.2f, -5.05f), mirror));
                baseGoal = MirrorX(new Vector2(10.4f, 5.82f), mirror);
            }
            else if (players == 2)
            {
                Shelf(false, -2.55f, -2.1f, -1f);
                Shelf(false, 2.7f, -3.8f, -1f);
                spawns.Add(MirrorX(new Vector2(7.2f, -5.05f), mirror));
                spawns.Add(MirrorX(new Vector2(-2.75f, -1.3f), mirror));
                baseGoal = MirrorX(new Vector2(10.2f, 5.82f), mirror);
            }
            else if (players == 3)
            {
                Shelf(false, -2.7f, -2f, -1f);
                Shelf(true, 0.75f, 2.15f, -1f);
                Shelf(true, 4.35f, 3.6f, -1f);
                spawns.Add(MirrorX(new Vector2(7.3f, -5.05f), mirror));
                spawns.Add(MirrorX(new Vector2(-2.7f, -1.45f), mirror));
                spawns.Add(MirrorX(new Vector2(2.85f, 2.05f), mirror));
                baseGoal = MirrorX(new Vector2(-10.2f, 5.82f), mirror);
            }
            else
            {
                Shelf(false, -3.35f, -1.8f, -1f);
                Shelf(true, -0.65f, 2.15f, -1f);
                Shelf(false, 2.05f, -2.15f, -1f);
                Shelf(true, 4.72f, 3.25f, -1f);
                Stem(0.35f, 2.15f);
                spawns.Add(MirrorX(new Vector2(7.4f, -5.05f), mirror));
                spawns.Add(MirrorX(new Vector2(-2.55f, -2.12f), mirror));
                spawns.Add(MirrorX(new Vector2(2.85f, 0.58f), mirror));
                spawns.Add(MirrorX(new Vector2(-2.85f, 3.28f), mirror));
                baseGoal = MirrorX(new Vector2(-10.5f, 5.82f), mirror);
            }

            // Separate edge locations make rounds two and three visually distinct;
            // these are editable anchors, not a tightly packed parallel bundle.
            Vector2[] sourceSeeds =
            {
                MirrorX(new Vector2(-17.35f, -5.18f), mirror),
                MirrorX(new Vector2(-17.35f, 0.15f), mirror),
                MirrorX(new Vector2(-12.6f, 6.65f), mirror)
            };
            Vector2[] goalSeeds =
            {
                baseGoal,
                MirrorX(new Vector2(15.4f, 2.75f), mirror),
                MirrorX(new Vector2(7.4f, -3.9f), mirror)
            };
            for (int i = 0; i < targetRound; i++)
            {
                Vector2 sourceAim = spawns.Count > 0 ? spawns[0] - sourceSeeds[i] : Vector2.right;
                float sourceRotation = Mathf.Atan2(sourceAim.y, sourceAim.x) * Mathf.Rad2Deg;
                Add("source", i + 1, StageObjectType.BackgroundArrow, sourceSeeds[i],
                    new Vector2(2.7f, 1.8f), sourceRotation);
                Add("goal", i + 1, StageObjectType.BackgroundLightBulb, goalSeeds[i], new Vector2(1.35f, 2.05f));
            }
            for (int i = 0; i < spawns.Count; i++)
                Add("player", i + 1, StageObjectType.BackgroundStickFigure, spawns[i], new Vector2(0.9f, 1.8f));
            return result;
        }

        private void LoadAuthoredLayout()
        {
            StageObjectData[] data = stageLoader != null ? stageLoader.CurrentStageData?.objects : null;
            if (data == null) return;
            string prefix = GetEditorLayoutPrefix(playerCount, round);
            List<StageObjectData> matching = new List<StageObjectData>();
            for (int i = 0; i < data.Length; i++)
                if (data[i] != null && !string.IsNullOrEmpty(data[i].objectId)
                    && data[i].objectId.StartsWith(prefix, System.StringComparison.Ordinal))
                    matching.Add(data[i]);
            if (matching.Count == 0)
                matching.AddRange(CreateEditorLayoutDefaults(playerCount, round));
            matching.Sort((a, b) => string.CompareOrdinal(a.objectId, b.objectId));
            for (int i = 0; i < matching.Count; i++)
            {
                StageObjectData item = matching[i];
                if (item.objectId.Contains("-wall-")) authoredWalls.Add(item);
                else if (item.objectId.Contains("-source-")) authoredBeamOrigins.Add(item.position);
                else if (item.objectId.Contains("-goal-")) authoredGoalPositions.Add(item.position);
                else if (item.objectId.Contains("-player-")) relaySpawnPositions.Add(item.position);
            }
        }

        private void CreateBeams()
        {
            Color[] colors =
            {
                new Color(1f, 0.18f, 0.08f), new Color(0.12f, 0.62f, 1f), new Color(0.72f, 0.22f, 1f)
            };

            int beamCount = round;
            Vector2[] origins = new Vector2[beamCount];
            Vector2[] directions = new Vector2[beamCount];
            Vector2[] goalPositions = new Vector2[beamCount];
            Vector2 firstRelay = relaySpawnPositions.Count > 0
                ? relaySpawnPositions[0]
                : Vector2.zero;
            Vector2 sourceDirection = (firstRelay - beamSourceAnchor).normalized;
            Vector2 sourcePerpendicular = new Vector2(-sourceDirection.y, sourceDirection.x);
            Vector2 finalRelay = relaySpawnPositions.Count > 0
                ? relaySpawnPositions[relaySpawnPositions.Count - 1]
                : Vector2.zero;
            Vector2 goalDirection = (beamGoalAnchor - finalRelay).normalized;
            Vector2 goalPerpendicular = new Vector2(-goalDirection.y, goalDirection.x);

            for (int i = 0; i < beamCount; i++)
            {
                float centered = i - (beamCount - 1) * 0.5f;
                origins[i] = authoredBeamOrigins.Count >= beamCount
                    ? authoredBeamOrigins[i]
                    : beamSourceAnchor + sourcePerpendicular * (centered * 0.32f);
                directions[i] = authoredBeamOrigins.Count >= beamCount
                    ? (firstRelay - origins[i]).normalized
                    : (firstRelay + sourcePerpendicular * (centered * 0.24f) - origins[i]).normalized;
                goalPositions[i] = authoredGoalPositions.Count >= beamCount
                    ? authoredGoalPositions[i]
                    : beamGoalAnchor + goalPerpendicular * (centered * 1.15f);
            }

            for (int i = 0; i < origins.Length; i++)
            {
                BeamState beam = new BeamState { Index = i, Origin = origins[i], Direction = directions[i].normalized };
                CreateSource(beam, colors[i]);
                CreateBeamRenderers(beam, colors[i]);
                beams.Add(beam);
            }
            for (int i = 0; i < goalPositions.Length; i++) CreateGoal(goalPositions[i], i, colors[i]);
        }

        private void CreateSource(BeamState beam, Color color)
        {
            GameObject source = new GameObject("Laser Source " + (beam.Index + 1));
            source.transform.SetParent(arenaRoot, false);
            float artScale = round == 1 ? 1f : round == 2 ? 0.84f : 0.7f;
            source.transform.position = beam.Origin - beam.Direction * (1.05f * artScale);
            source.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(beam.Direction.y, beam.Direction.x) * Mathf.Rad2Deg);
            beam.Source = source;

            if (!StageGun.TryCreateResourceSprite(source.transform,
                    "StageObjects/NicoDraw/laser-relay-emitter", "Colored Pencil Laser Emitter",
                    new Vector2(2.7f, 1.8f) * artScale, 27))
            {
                StageEscortController.AddFilledRect(source.transform, "Emitter Fallback",
                    Vector2.zero, new Vector2(2.2f, 1.05f) * artScale, color, 27);
                StageEscortController.AddBoxOutline(source.transform, Vector2.zero,
                    new Vector2(2.2f, 1.05f) * artScale, new Color(0.1f, 0.12f, 0.13f), 28);
            }

            GameObject lens = new GameObject("Emitter Lens");
            lens.transform.SetParent(source.transform, false);
            lens.transform.localPosition = new Vector3(1.14f * artScale, 0f, 0f);
            lens.transform.localScale = new Vector3(0.22f, 0.5f, 1f) * artScale;
            SpriteRenderer lensRenderer = lens.AddComponent<SpriteRenderer>();
            lensRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            lensRenderer.color = new Color(1f, 0.95f, 0.72f, 0.98f);
            lensRenderer.sortingOrder = 29;
            for (int i = 0; i < 3; i++)
            {
                GameObject glow = new GameObject("Emitter Glow");
                glow.transform.SetParent(source.transform, false);
                glow.transform.localPosition = new Vector3(1.14f * artScale, 0f, 0f);
                glow.transform.localScale = Vector3.one * (0.42f + i * 0.2f) * artScale;
                SpriteRenderer halo = glow.AddComponent<SpriteRenderer>();
                halo.sprite = DoodleRuntimeAssets.CircleSprite;
                halo.color = new Color(color.r, color.g, color.b, 0.24f - i * 0.055f);
                halo.sortingOrder = 22 - i;
            }
        }

        private void CreateGoal(Vector2 position, int index, Color color)
        {
            GameObject goal = new GameObject("Laser Bulb Goal " + (index + 1));
            goal.transform.SetParent(arenaRoot, false);
            goal.transform.position = position;
            GoalState state = new GoalState
                { Index = index, Position = position, PairColor = color, Charge = 0f };

            GameObject ringObject = new GameObject("Bulb Glow");
            ringObject.transform.SetParent(goal.transform, false);
            ringObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            ringObject.transform.localScale = new Vector3(1.72f, 1.9f, 1f);
            state.Ring = ringObject.AddComponent<SpriteRenderer>();
            state.Ring.sprite = DoodleRuntimeAssets.CircleSprite;
            state.Ring.color = new Color(color.r, color.g, color.b, 0.04f);
            state.Ring.sortingOrder = 18;

            GameObject coreObject = new GameObject("Bulb Warm Glass");
            coreObject.transform.SetParent(goal.transform, false);
            coreObject.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            coreObject.transform.localScale = new Vector3(1.05f, 1.15f, 1f);
            state.Core = coreObject.AddComponent<SpriteRenderer>();
            state.Core.sprite = DoodleRuntimeAssets.CircleSprite;
            state.Core.color = new Color(0.72f, 0.86f, 1f, 0.06f);
            state.Core.sortingOrder = 20;

            if (!StageGun.TryCreateResourceSprite(goal.transform,
                    "StageObjects/NicoDraw/laser-relay-bulb", "Colored Pencil Bulb",
                    new Vector2(1.35f, 2.05f), 22))
            {
                StageEscortController.AddBoxOutline(goal.transform, Vector2.zero,
                    new Vector2(1.05f, 1.65f), new Color(0.12f, 0.16f, 0.16f), 22);
            }

            GameObject chargeObject = new GameObject("Bulb Charge");
            chargeObject.transform.SetParent(goal.transform, false);
            chargeObject.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            chargeObject.transform.localScale = Vector3.one * 0.12f;
            state.ChargeCore = chargeObject.AddComponent<SpriteRenderer>();
            state.ChargeCore.sprite = DoodleRuntimeAssets.CircleSprite;
            state.ChargeCore.color = new Color(color.r, color.g, color.b, 0.18f);
            state.ChargeCore.sortingOrder = 23;

            GameObject pairMarker = new GameObject("Bulb Pair Marker");
            pairMarker.transform.SetParent(goal.transform, false);
            pairMarker.transform.localPosition = new Vector3(0f, -0.72f, 0f);
            pairMarker.transform.localScale = new Vector3(0.34f, 0.17f, 1f);
            SpriteRenderer pairRenderer = pairMarker.AddComponent<SpriteRenderer>();
            pairRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
            pairRenderer.color = new Color(color.r, color.g, color.b, 0.88f);
            pairRenderer.sortingOrder = 24;

            state.RaysRoot = new GameObject("Bulb Light Rays").transform;
            state.RaysRoot.SetParent(goal.transform, false);
            for (int ray = 0; ray < 8; ray++)
            {
                float angle = ray * Mathf.PI * 0.25f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageEscortController.AddLine(state.RaysRoot, direction * 0.82f,
                    direction * 1.15f, 0.055f, new Color(color.r, color.g, color.b, 0.72f), 19);
            }
            state.RaysRoot.gameObject.SetActive(false);
            goals.Add(state);
        }

        private void CreateBeamRenderers(BeamState beam, Color color)
        {
            GameObject root = new GameObject("Continuous Safe Laser " + (beam.Index + 1));
            root.transform.SetParent(arenaRoot, false);
            beam.Glow = CreateLaserLine(root.transform, "Laser Glow", 0.28f,
                new Color(color.r, color.g, color.b, 0.22f), 16);
            beam.Core = CreateLaserLine(root.transform, "Laser Core", 0.085f,
                new Color(1f, 0.94f, 0.7f, 0.96f), 17);
            for (int i = 0; i < 4; i++)
            {
                GameObject mark = new GameObject("Reflection Flash " + i);
                mark.transform.SetParent(root.transform, false);
                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(color.r, color.g, color.b, 0.7f);
                renderer.sortingOrder = 19;
                mark.SetActive(false);
                beam.ReflectionMarks.Add(renderer);
            }
        }

        private static LineRenderer CreateLaserLine(
            Transform parent, string name, float width, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 5;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            return line;
        }

        private void TraceAllBeams()
        {
            for (int i = 0; i < beams.Count; i++) TraceBeam(beams[i]);
        }

        private void TraceBeam(BeamState beam)
        {
            beam.Points.Clear();
            beam.Points.Add(beam.Origin);
            beam.RouteValid = true;
            beam.ReachedGoalIndex = -1;
            beam.ReflectedPlayers.Clear();
            Vector2 origin = beam.Origin;
            Vector2 direction = beam.Direction.normalized;
            PlayerController2D lastPlayer = null;
            float remainingDistance = RayDistance;
            int markCount = 0;
            int reflectionCount = 0;

            while (remainingDistance > 0.1f && reflectionCount < 12)
            {
                TryFindNearestPlayerHit(origin, direction, remainingDistance, lastPlayer,
                    out RaycastHit2D playerHit, out PlayerController2D hitPlayer);
                TryFindNearestBlockerHit(origin, direction, remainingDistance, out RaycastHit2D blockerHit);
                GoalState pairedGoal = goals[beam.Index];
                bool goalHit = TryRayCircle(origin, direction, pairedGoal.Position, 0.7f,
                    remainingDistance, out float goalDistance);
                float playerDistance = hitPlayer != null ? playerHit.distance : float.PositiveInfinity;
                float blockerDistance = blockerHit.collider != null
                    ? blockerHit.distance : float.PositiveInfinity;

                if (goalHit && goalDistance < playerDistance && goalDistance < blockerDistance)
                {
                    Vector2 point = origin + direction * goalDistance;
                    beam.Points.Add(point);
                    beam.RouteValid = true;
                    beam.ReachedGoalIndex = pairedGoal.Index;
                    break;
                }

                if (blockerDistance < playerDistance)
                {
                    beam.Points.Add(blockerHit.point);
                    beam.RouteValid = false;
                    break;
                }

                if (hitPlayer == null)
                {
                    beam.Points.Add(origin + direction * remainingDistance);
                    beam.RouteValid = false;
                    break;
                }

                beam.Points.Add(playerHit.point);
                beam.ReflectedPlayers.Add(hitPlayer);
                reflectionCount++;

                if (markCount < beam.ReflectionMarks.Count)
                {
                    SpriteRenderer mark = beam.ReflectionMarks[markCount++];
                    mark.transform.position = playerHit.point;
                    mark.gameObject.SetActive(true);
                }
                Vector2 normal = playerHit.normal.sqrMagnitude > 0.2f
                    ? playerHit.normal.normalized
                    : -direction;
                direction = Vector2.Reflect(direction, normal).normalized;
                remainingDistance -= Mathf.Max(0.05f, playerHit.distance);
                origin = playerHit.point + direction * 0.12f;
                lastPlayer = hitPlayer;
            }

            for (int i = markCount; i < beam.ReflectionMarks.Count; i++)
                beam.ReflectionMarks[i].gameObject.SetActive(false);
            ApplyBeamLine(beam);
        }

        private static bool TryFindNearestPlayerHit(
            Vector2 origin, Vector2 direction, float distance, PlayerController2D lastPlayer,
            out RaycastHit2D chosen, out PlayerController2D player)
        {
            chosen = default;
            player = null;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger) continue;
                PlayerController2D candidate = collider.GetComponentInParent<PlayerController2D>();
                if (candidate == null || candidate == lastPlayer) continue;
                chosen = hits[i];
                player = candidate;
                return true;
            }
            return false;
        }

        private bool TryFindNearestBlockerHit(
            Vector2 origin, Vector2 direction, float distance, out RaycastHit2D chosen)
        {
            chosen = default;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D collider = hits[i].collider;
                if (collider == null || collider.isTrigger || !relayBlockers.Contains(collider)) continue;
                chosen = hits[i];
                return true;
            }
            return false;
        }

        private static bool TryRayCircle(
            Vector2 origin, Vector2 direction, Vector2 center, float radius, float maxDistance,
            out float distance)
        {
            Vector2 offset = center - origin;
            float projection = Vector2.Dot(offset, direction);
            float perpendicularSquared = offset.sqrMagnitude - projection * projection;
            float radiusSquared = radius * radius;
            if (projection < 0f || perpendicularSquared > radiusSquared)
            {
                distance = 0f;
                return false;
            }
            distance = projection - Mathf.Sqrt(Mathf.Max(0f, radiusSquared - perpendicularSquared));
            return distance >= 0f && distance <= maxDistance;
        }

        private void ApplyBeamLine(BeamState beam)
        {
            int count = Mathf.Max(2, beam.Points.Count);
            beam.Glow.positionCount = count;
            beam.Core.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                Vector3 point = i < beam.Points.Count ? beam.Points[i] : beam.Points[beam.Points.Count - 1];
                beam.Glow.SetPosition(i, point);
                beam.Core.SetPosition(i, point);
            }
            bool reached = beam.ReachedGoalIndex >= 0;
            bool latched = (goalMask & (1 << beam.Index)) != 0;
            Color glowColor = reached || latched
                ? new Color(0.2f, 1f, 0.42f, 0.42f)
                : beam.RouteValid
                    ? new Color(1f, 0.38f, 0.08f, 0.3f)
                    : new Color(1f, 0.08f, 0.06f, 0.2f);
            beam.Glow.startColor = glowColor;
            beam.Glow.endColor = glowColor;
            beam.Core.startColor = reached || latched
                ? new Color(0.76f, 1f, 0.76f, 0.98f)
                : new Color(1f, 0.92f, 0.62f, 0.96f);
            beam.Core.endColor = beam.Core.startColor;
        }

        private void UpdateGoalCharging()
        {
            bool ready = Time.unscaledTime >= roundReadyAt;
            for (int i = 0; i < goals.Count; i++)
            {
                GoalState goal = goals[i];
                bool lit = (goalMask & (1 << goal.Index)) != 0;
                if (lit)
                {
                    goal.Charge = 1f;
                    continue;
                }

                bool receivingPairedBeam = ready
                    && goal.Index < beams.Count
                    && beams[goal.Index].ReachedGoalIndex == goal.Index;
                goal.Charge = receivingPairedBeam
                    ? Mathf.MoveTowards(goal.Charge, 1f, Time.unscaledDeltaTime / GoalChargeSeconds)
                    : Mathf.MoveTowards(goal.Charge, 0f, Time.unscaledDeltaTime / 0.65f);
                if (goal.Charge < 0.999f) continue;
                goal.Charge = 1f;
                goalMask |= 1 << goal.Index;
                GameSfx.PlayAt(SfxId.GoalReached, goal.Position);
                BroadcastState(true);
            }
        }

        private bool AllGoalsReached()
        {
            int all = (1 << goals.Count) - 1;
            return goals.Count > 0 && (goalMask & all) == all;
        }

        private void RefreshPlayerOrder()
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            orderedPlayers.Clear();
            orderedPlayers.AddRange(players);
        }

        private void AnimateBeamEffects()
        {
            float pulse = 0.86f + Mathf.Sin(Time.unscaledTime * 12f) * 0.14f;
            for (int i = 0; i < beams.Count; i++)
            {
                BeamState beam = beams[i];
                beam.Core.widthMultiplier = pulse;
                for (int m = 0; m < beam.ReflectionMarks.Count; m++)
                    if (beam.ReflectionMarks[m].gameObject.activeSelf)
                        beam.ReflectionMarks[m].transform.localScale = Vector3.one
                            * (0.48f + Mathf.Sin(Time.unscaledTime * 9f + m) * 0.12f);
            }
            for (int i = 0; i < goals.Count; i++)
            {
                GoalState goal = goals[i];
                bool reached = (goalMask & (1 << goal.Index)) != 0;
                float charge = reached ? 1f : Mathf.Clamp01(goal.Charge);
                float glowPulse = 1f + Mathf.Sin(Time.unscaledTime * (5f + charge * 7f) + i) * 0.08f;
                goal.Ring.transform.localScale = new Vector3(1.72f, 1.9f, 1f) * glowPulse;
                goal.Ring.color = new Color(goal.PairColor.r, goal.PairColor.g, goal.PairColor.b,
                    Mathf.Lerp(0.035f, 0.56f, charge));
                goal.Core.color = Color.Lerp(
                    new Color(0.72f, 0.86f, 1f, 0.06f),
                    new Color(1f, 0.92f, 0.28f, 0.82f), charge);
                goal.ChargeCore.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.82f, charge);
                goal.ChargeCore.color = Color.Lerp(
                    new Color(goal.PairColor.r, goal.PairColor.g, goal.PairColor.b, 0.16f),
                    new Color(1f, 0.92f, 0.3f, 0.96f), charge);
                goal.RaysRoot.gameObject.SetActive(charge > 0.08f);
                goal.RaysRoot.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, charge) * glowPulse;
            }
        }

        private IEnumerator PositionPlayersAfterBuild()
        {
            yield return null;
            yield return new WaitForFixedUpdate();
            RefreshPlayerOrder();
            for (int i = 0; i < orderedPlayers.Count; i++)
            {
                PlayerController2D player = orderedPlayers[i];
                Vector2 position = i < relaySpawnPositions.Count
                    ? relaySpawnPositions[i]
                    : new Vector2(0f, FloorY + 1.35f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null) { body.position = position; body.linearVelocity = Vector2.zero; }
                else player.transform.position = position;
            }
            Physics2D.SyncTransforms();
        }

        private void CreateBoxStation()
        {
            if (factory == null) factory = Object.FindFirstObjectByType<StageObjectFactory>();
            if (factory == null) return;
            Vector2 dropperPosition = new Vector2(15f, 5.2f);
            StageObjectData dropperData = StageObjectFactory.CreateDefaultData(StageObjectType.BoxDropper, dropperPosition);
            dropperData.objectId = "14-3_box_generator_round_" + round;
            dropperData.size = new Vector2(2.6f, 1.8f);
            dropperData.actionStrength = 10f;
            dropperData.spawnPattern = 1;
            dropperData.spawnBoxSize = 0.9f;
            GameObject dropperObject = factory.Create(dropperData, arenaRoot);
            boxDropper = dropperObject != null ? dropperObject.GetComponent<StageBoxDropper>() : null;
            boxDropper?.ConfigureManualDispense();

            StageObjectData buttonData = StageObjectFactory.CreateDefaultData(StageObjectType.Button,
                new Vector2(14.5f, FloorY + 0.52f));
            buttonData.objectId = "14-3_box_button_round_" + round;
            buttonData.size = new Vector2(1.7f, 0.8f);
            GameObject buttonObject = factory.Create(buttonData, arenaRoot);
            if (buttonObject == null) return;
            boxButton = buttonObject.GetComponent<Collider2D>();
            buttonCap = buttonObject.transform.Find("Button Cap");
            buttonCapScale = buttonCap != null ? buttonCap.localScale : Vector3.one;
            GameObject glow = new GameObject("Button Glow");
            glow.transform.SetParent(buttonObject.transform, false);
            glow.transform.localScale = new Vector3(1.35f, 0.55f, 1f);
            buttonGlow = glow.AddComponent<SpriteRenderer>();
            buttonGlow.sprite = DoodleRuntimeAssets.CircleSprite;
            buttonGlow.sortingOrder = 26;
            GameObject preview = new GameObject("Next Box Preview");
            preview.transform.SetParent(dropperObject.transform, false);
            SpriteRenderer previewRenderer = preview.AddComponent<SpriteRenderer>();
            previewRenderer.sprite = DoodleRuntimeAssets.SquareSprite;
            previewRenderer.color = new Color(0.94f, 0.52f, 0.15f, 0.92f);
            previewRenderer.sortingOrder = 33;
            boxPreview = preview.transform;
            RefreshBoxStationVisual();
        }

        private void UpdateBoxStation()
        {
            if (Time.unscaledTime >= nextPreviewAt)
            {
                nextPreviewAt = Time.unscaledTime + BoxPreviewSeconds;
                previewIndex = (previewIndex + 1) % BoxSizes.Length;
            }
            bool pressed = IsPlayerPressing(boxButton);
            if (pressed && !buttonPressed && Time.unscaledTime >= nextBoxAt)
            {
                nextBoxAt = Time.unscaledTime + BoxCooldownSeconds;
                boxDropper?.DispenseSelectedSize(BoxSizes[previewIndex]);
                GameSfx.PlayAt(SfxId.UiButtonPress,
                    boxButton != null ? boxButton.transform.position : transform.position);
            }
            buttonPressed = pressed;
            RefreshBoxStationVisual();
        }

        private static bool IsPlayerPressing(Collider2D button)
        {
            if (button == null || !button.enabled) return false;
            Bounds bounds = button.bounds;
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Collider2D[] colliders = players[i].GetComponentsInChildren<Collider2D>(false);
                for (int c = 0; c < colliders.Length; c++)
                    if (colliders[c] != null && colliders[c].enabled && !colliders[c].isTrigger
                        && colliders[c].bounds.Intersects(bounds)) return true;
            }
            return false;
        }

        private void RefreshBoxStationVisual()
        {
            if (buttonCap != null)
            {
                Vector3 scale = buttonCapScale;
                scale.y *= buttonPressed ? 0.56f : 1f;
                buttonCap.localScale = scale;
            }
            if (buttonGlow != null) buttonGlow.color = buttonPressed
                ? new Color(0.18f, 0.95f, 0.28f, 0.72f)
                : new Color(0.95f, 0.16f, 0.1f, 0.16f);
            if (boxPreview != null)
            {
                Vector2 size = BoxSizes[Mathf.Clamp(previewIndex, 0, BoxSizes.Length - 1)];
                float fit = Mathf.Min(0.78f / size.x, 0.78f / size.y);
                boxPreview.localScale = new Vector3(size.x * fit, size.y * fit, 1f);
            }
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("14-3 Laser Relay Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.position = new Vector3(0f, 6.1f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(13.5f, 1.9f), 40);
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main",
                new Vector3(0f, 0.3f, -0.03f), 54, 0.11f, new Color(0.42f, 0.12f, 0.04f), 44);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub",
                new Vector3(0f, -0.36f, -0.04f), 42, 0.076f, new Color(0.05f, 0.32f, 0.48f), 45);
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null || monitorSub == null) return;
            switch (phase)
            {
                case RelayPhase.Active:
                    monitorMain.text = LocalizationManager.Format("laser_relay_monitor", round, remaining);
                    monitorSub.text = LocalizationManager.Format("laser_relay_progress",
                        CountBits(goalMask), goals.Count);
                    break;
                case RelayPhase.RoundClear:
                    monitorMain.text = LocalizationManager.Format("laser_relay_round_clear", round);
                    monitorSub.text = LocalizationManager.T("laser_relay_hint");
                    break;
                case RelayPhase.Failed:
                    monitorMain.text = LocalizationManager.T("laser_relay_timeout");
                    monitorSub.text = LocalizationManager.T("laser_relay_hint");
                    break;
            }
        }

        private void ClearRound()
        {
            if (phase != RelayPhase.Active) return;
            GameSfx.Play(SfxId.GoalReached);
            if (round >= 3)
            {
                phase = RelayPhase.Complete;
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            phase = RelayPhase.RoundClear;
            transitionRemaining = 1.8f;
            BroadcastState(true);
        }

        private void FailRound()
        {
            if (phase != RelayPhase.Active) return;
            phase = RelayPhase.Failed;
            transitionRemaining = 2.2f;
            GameSfx.Play(SfxId.PlayerDeath, 0.68f);
            BroadcastState(true);
        }

        private void CaptureCamera()
        {
            if (cameraCaptured) return;
            if (gameCamera == null) gameCamera = Camera.main;
            if (gameCamera == null) return;
            cameraCaptured = true;
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            previousCameraFollowEnabled = cameraFollow != null && cameraFollow.enabled;
        }

        private void ConfigureCamera()
        {
            if (!cameraCaptured) CaptureCamera();
            if (gameCamera == null) return;
            if (cameraFollow != null) cameraFollow.enabled = false;
            gameCamera.transform.position = new Vector3(0f, 0.45f, -10f);
            gameCamera.orthographicSize = Mathf.Max(
                8.4f,
                (RoomHalfWidth + 0.5f) / Mathf.Max(0.1f, gameCamera.aspect));
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousCameraFollowEnabled;
            cameraCaptured = false;
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            NetworkState state = new NetworkState
            {
                Sequence = ++sequence, Round = round, Phase = (int)phase,
                Remaining = remaining, GoalMask = goalMask,
                GoalCharges = GetGoalCharges(),
                PreviewIndex = previewIndex, ButtonPressed = buttonPressed
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId, Kind = StateKind, Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkState(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind
                || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;
            if (state.Round != round) BeginRound(state.Round);
            phase = (RelayPhase)state.Phase;
            remaining = state.Remaining;
            goalMask = state.GoalMask;
            if (state.GoalCharges != null)
                for (int i = 0; i < goals.Count && i < state.GoalCharges.Length; i++)
                    goals[i].Charge = Mathf.Clamp01(state.GoalCharges[i]);
            previewIndex = Mathf.Clamp(state.PreviewIndex, 0, BoxSizes.Length - 1);
            buttonPressed = state.ButtonPressed;
            RefreshBoxStationVisual();
            RefreshMonitor();
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] players = onlineManager?.CurrentLobby?.Players;
            if (players == null) return false;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].IsHost && players[i].PlayerId == playerId) return true;
            return false;
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0) { count += value & 1; value >>= 1; }
            return count;
        }

        private float[] GetGoalCharges()
        {
            float[] values = new float[goals.Count];
            for (int i = 0; i < goals.Count; i++) values[i] = goals[i].Charge;
            return values;
        }
    }

}
