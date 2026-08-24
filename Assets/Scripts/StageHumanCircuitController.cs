using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Stage 13-3: players become conductive links between broken wire terminals.
    /// Static terrain remains normal editable stage JSON; only circuit presentation,
    /// contact solving and door state live here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageHumanCircuitController : MonoBehaviour
    {
        private const string StageId = "13-3";
        private const string StateKind = "human_circuit_state";
        private const float ContactInterval = 0.085f;
        private const float TerminalContactDistance = 0.5f;
        private const float PlayerContactDistance = 0.16f;

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int CompletedMask;
            public float RelayProgress;
        }

        private sealed class Terminal
        {
            public Vector2 BasePosition;
            public Vector2 Position;
            public SpriteRenderer Core;
            public SpriteRenderer Halo;

            public void SetPosition(Vector2 position)
            {
                Position = position;
                if (Core != null) Core.transform.position = new Vector3(position.x, position.y, -0.28f);
                if (Halo != null) Halo.transform.position = new Vector3(position.x, position.y, -0.27f);
            }

            public void SetPowered(bool powered)
            {
                if (Core != null) Core.color = powered ? ActiveElectric : DarkTerminal;
                if (Halo != null)
                {
                    Halo.enabled = powered;
                    Halo.color = new Color(0.25f, 0.95f, 1f, 0.18f + Mathf.PingPong(Time.unscaledTime * 2.8f, 0.16f));
                }
            }
        }

        private sealed class Gap
        {
            public Terminal Left;
            public Terminal Right;
            public Vector2 MoveAxis;
            public float MoveAmplitude;
            public float MovePhase;
            public bool OpposedMotion;
            public bool Bridged;
            public bool PreviousBridged;

            public void UpdateMotion(float clock)
            {
                if (MoveAmplitude <= 0f)
                {
                    Left.SetPosition(Left.BasePosition);
                    Right.SetPosition(Right.BasePosition);
                    return;
                }

                Vector2 offset = MoveAxis * (Mathf.Sin(clock * 0.82f + MovePhase) * MoveAmplitude);
                Left.SetPosition(Left.BasePosition + offset);
                Right.SetPosition(Right.BasePosition + (OpposedMotion ? -offset : offset));
            }
        }

        private sealed class WirePiece
        {
            public Terminal StartTerminal;
            public Terminal EndTerminal;
            public Vector2 FixedStart;
            public Vector2 FixedEnd;
            public bool UsesStartTerminal;
            public bool UsesEndTerminal;
            public LineRenderer Base;
            public LineRenderer Glow;

            public void Refresh(bool powered)
            {
                Vector2 start = UsesStartTerminal ? StartTerminal.Position : FixedStart;
                Vector2 end = UsesEndTerminal ? EndTerminal.Position : FixedEnd;
                Base.SetPosition(0, start);
                Base.SetPosition(1, end);
                Glow.SetPosition(0, start);
                Glow.SetPosition(1, end);
                Glow.enabled = powered;
                if (powered)
                {
                    float pulse = 0.75f + Mathf.PingPong(Time.unscaledTime * 2.4f, 0.25f);
                    Glow.startColor = new Color(0.25f, 0.95f, 1f, pulse);
                    Glow.endColor = new Color(1f, 0.9f, 0.25f, pulse);
                }
            }
        }

        private sealed class BulbVisual
        {
            public SpriteRenderer Glass;
            public SpriteRenderer Halo;
            public LineRenderer Filament;

            public void SetPowered(bool powered)
            {
                Glass.color = powered ? new Color(1f, 0.87f, 0.18f, 1f) : new Color(0.22f, 0.25f, 0.27f, 1f);
                Halo.enabled = powered;
                if (powered)
                {
                    Halo.color = new Color(1f, 0.82f, 0.15f, 0.2f + Mathf.PingPong(Time.unscaledTime * 2f, 0.16f));
                }
                Filament.startColor = Filament.endColor = powered ? Color.white : new Color(0.08f, 0.09f, 0.1f, 1f);
            }
        }

        private sealed class CircuitPath
        {
            public readonly List<Gap> Gaps = new List<Gap>();
            public readonly List<WirePiece> Pieces = new List<WirePiece>();
            public BulbVisual Bulb;
            public int PoweredPieces;
            public bool Complete;

            public void RefreshVisuals(bool latched)
            {
                int powered = latched ? Pieces.Count : PoweredPieces;
                for (int i = 0; i < Pieces.Count; i++) Pieces[i].Refresh(i < powered);
                Bulb?.SetPowered(latched || Complete);
            }
        }

        private sealed class RoomCircuit
        {
            public int Index;
            public readonly List<CircuitPath> Paths = new List<CircuitPath>();
            public GameObject Door;
            public Collider2D[] DoorColliders;
            public Vector3 DoorBaseScale;
            public float DoorOpen;
            public TextMesh Status;
            public bool Completed;
            public float RelayProgress;
            public readonly List<Transform> RelayPulses = new List<Transform>();
            public readonly List<Vector2> RelayStarts = new List<Vector2>();
            public readonly List<Vector2> RelayEnds = new List<Vector2>();
        }

        private sealed class PlayerShape
        {
            public PlayerController2D Player;
            public Collider2D[] Colliders;
            public Bounds Bounds;
        }

        private sealed class PlayerElectricVisual
        {
            public GameObject Root;
            public LineRenderer Arc;

            public void Refresh(Bounds bounds, float seed)
            {
                if (Root == null || Arc == null) return;
                Root.SetActive(true);
                Vector2 min = (Vector2)bounds.min - Vector2.one * 0.11f;
                Vector2 max = (Vector2)bounds.max + Vector2.one * 0.11f;
                Vector2 center = bounds.center;
                Vector2[] outline =
                {
                    new Vector2(min.x,max.y), new Vector2(center.x,max.y + 0.09f), new Vector2(max.x,max.y),
                    new Vector2(max.x + 0.09f,center.y), new Vector2(max.x,min.y),
                    new Vector2(center.x,min.y - 0.09f), new Vector2(min.x,min.y),
                    new Vector2(min.x - 0.09f,center.y), new Vector2(min.x,max.y)
                };
                Arc.positionCount = outline.Length;
                for (int i = 0; i < outline.Length; i++)
                {
                    float jitter = Mathf.Sin(Time.unscaledTime * 18f + seed + i * 1.7f) * 0.045f;
                    Arc.SetPosition(i, new Vector3(outline[i].x + jitter, outline[i].y - jitter, -0.42f));
                }
                float alpha = 0.72f + Mathf.PingPong(Time.unscaledTime * 4f, 0.28f);
                Arc.startColor = new Color(0.2f, 0.92f, 1f, alpha);
                Arc.endColor = new Color(1f, 0.9f, 0.25f, alpha);
            }

            public void Hide()
            {
                if (Root != null) Root.SetActive(false);
            }
        }

        private static readonly Color DarkWire = new Color(0.15f, 0.19f, 0.22f, 0.72f);
        private static readonly Color DarkTerminal = new Color(0.2f, 0.24f, 0.27f, 1f);
        private static readonly Color ActiveElectric = new Color(0.25f, 0.95f, 1f, 1f);
        private static bool creatingEditorPreview;
        private static AudioClip zapClip;
        private static AudioClip successClip;

        private readonly List<RoomCircuit> rooms = new List<RoomCircuit>(5);
        private readonly List<PlayerShape> playerShapes = new List<PlayerShape>(4);
        private readonly Dictionary<PlayerController2D, PlayerElectricVisual> playerEffects =
            new Dictionary<PlayerController2D, PlayerElectricVisual>();
        private readonly HashSet<PlayerController2D> energizedPlayers = new HashSet<PlayerController2D>();
        private readonly HashSet<PlayerController2D> reachablePlayers = new HashSet<PlayerController2D>();
        private readonly Queue<PlayerController2D> playerQueue = new Queue<PlayerController2D>();

        private StageManager stageManager;
        private OnlineManager onlineManager;
        private AudioSource audioSource;
        private bool previewOnly;
        private int playerCount = 1;
        private int completedMask;
        private float nextContactAt;
        private float nextStateAt;
        private float nextZapAt;
        private float motionClock;
        private int stateSequence;
        private int lastStateSequence;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        internal static void CreateEditorPreview(Transform parent)
        {
            if (parent == null) return;
            GameObject preview = new GameObject("13-3 Circuit Preview");
            preview.transform.SetParent(parent, false);
            creatingEditorPreview = true;
            try { preview.AddComponent<StageHumanCircuitController>(); }
            finally { creatingEditorPreview = false; }
        }

        private void Awake()
        {
            previewOnly = creatingEditorPreview;
            stageManager = Object.FindFirstObjectByType<StageManager>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            if (previewOnly)
            {
                playerCount = 1;
                BuildStageCircuits();
                SetPreviewState();
            }
            else
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                audioSource.dopplerLevel = 0f;
            }
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
            if (previewOnly) return;
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }

            playerCount = Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
            BuildStageCircuits();
            for (int i = 0; i < rooms.Count; i++) rooms[i].Completed = (completedMask & (1 << i)) != 0;
            BroadcastState(true);
        }

        private void Update()
        {
            if (previewOnly)
            {
                motionClock += Time.unscaledDeltaTime;
                UpdateMovingGaps();
                RefreshAllVisuals();
                return;
            }
            if (stageManager == null || stageManager.CurrentStageId != StageId) return;

            motionClock += Time.deltaTime;
            UpdateMovingGaps();
            UpdateDoors();

            if (HasAuthority && Time.unscaledTime >= nextContactAt)
            {
                nextContactAt = Time.unscaledTime + ContactInterval;
                EvaluateActiveRoom(ContactInterval);
            }
            else if (!HasAuthority && Time.unscaledTime >= nextContactAt)
            {
                nextContactAt = Time.unscaledTime + ContactInterval;
                EvaluateClientPresentation();
                ApplyClientProgress();
            }

            RefreshAllVisuals();
            RefreshPlayerEffects();
            BroadcastState(false);
        }

        private void BuildStageCircuits()
        {
            if (rooms.Count > 0) return;
            rooms.Add(BuildRoomOne());
            rooms.Add(BuildRoomTwo());
            rooms.Add(BuildRoomThree());
            rooms.Add(BuildRoomFour());
            rooms.Add(BuildRoomFive());
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].Index = i;
                rooms[i].Door = FindStageObject("13-3_door_" + (i + 1));
                if (rooms[i].Door != null)
                {
                    rooms[i].DoorBaseScale = rooms[i].Door.transform.localScale;
                    rooms[i].DoorColliders = rooms[i].Door.GetComponentsInChildren<Collider2D>(true);
                }
                CreateRoomSign(i);
            }
        }

        private RoomCircuit BuildRoomOne()
        {
            RoomCircuit room = NewRoom(0, new Vector2(-10f, -3.4f));
            List<Vector2> gaps = EvenGapCenters(-5.2f, 5.2f, -3.4f, playerCount);
            room.Paths.Add(CreatePath(new Vector2(-9.2f, -3.4f), new Vector2(9.2f, -3.4f), gaps, Vector2.right));
            return room;
        }

        private RoomCircuit BuildRoomTwo()
        {
            RoomCircuit room = NewRoom(1, new Vector2(14f, -5.2f));
            Vector2[] candidates =
            {
                new Vector2(18f,-5.2f), new Vector2(24.5f,-0.5f),
                new Vector2(31.5f,2.8f), new Vector2(36.4f,5.6f)
            };
            List<Vector2> gaps = new List<Vector2>();
            for (int i = 0; i < playerCount; i++) gaps.Add(candidates[i]);
            room.Paths.Add(CreatePath(new Vector2(14f, -5.2f), new Vector2(38f, 5.6f), gaps, Vector2.right));
            return room;
        }

        private RoomCircuit BuildRoomThree()
        {
            RoomCircuit room = NewRoom(2, new Vector2(42f, -4.8f));
            Vector2[] candidates =
            {
                new Vector2(46.5f,-3.6f), new Vector2(52.4f,0.9f),
                new Vector2(59.2f,4.8f), new Vector2(65.2f,-1.8f)
            };
            List<Vector2> gaps = new List<Vector2>();
            for (int i = 0; i < playerCount; i++) gaps.Add(candidates[i]);
            CircuitPath path = CreatePath(new Vector2(42f, -4.8f), new Vector2(68f, 4.9f), gaps, Vector2.right);
            for (int i = 0; i < path.Gaps.Count; i++)
            {
                Gap gap = path.Gaps[i];
                gap.MoveAxis = i % 2 == 0 ? Vector2.up : Vector2.right;
                gap.MoveAmplitude = i % 2 == 0 ? 1.3f : 1.05f;
                gap.MovePhase = i * 1.37f;
                gap.OpposedMotion = i == 2;
            }
            room.Paths.Add(path);
            return room;
        }

        private RoomCircuit BuildRoomFour()
        {
            RoomCircuit room = NewRoom(3, new Vector2(72f, -2f));
            Vector2 sharedCenter = new Vector2(84.5f, -3.6f);
            room.Paths.Add(CreatePath(new Vector2(72f, -2f), new Vector2(97f, 3.8f),
                new List<Vector2> { sharedCenter }, Vector2.right));
            room.Paths.Add(CreatePath(new Vector2(72f, -2f), new Vector2(97f, -5.2f),
                new List<Vector2> { sharedCenter }, Vector2.up));
            return room;
        }

        private RoomCircuit BuildRoomFive()
        {
            RoomCircuit room = NewRoom(4, new Vector2(102f, -4.8f));
            Vector2 source = new Vector2(102f, -4.8f);
            Vector2 center = new Vector2(120f, -3.3f);
            Vector2[] ends = { new Vector2(138f, 3.7f), new Vector2(138f, -0.3f), new Vector2(138f, -4.7f) };
            Vector2[] directions =
            {
                new Vector2(0.55f, 0.835f).normalized,
                Vector2.right,
                new Vector2(0.55f, -0.835f).normalized
            };
            for (int i = 0; i < 3; i++)
            {
                room.Paths.Add(CreatePath(source, ends[i], new List<Vector2> { center }, directions[i]));
                room.RelayStarts.Add(source);
                room.RelayEnds.Add(ends[i]);
                room.RelayPulses.Add(CreateRelayPulse(source, i));
            }
            return room;
        }

        private Transform CreateRelayPulse(Vector2 position, int index)
        {
            GameObject pulse = new GameObject("Relay Spark");
            pulse.transform.SetParent(transform, false);
            pulse.transform.position = position;
            pulse.transform.localScale = Vector3.one * 0.72f;
            SpriteRenderer renderer = pulse.AddComponent<SpriteRenderer>();
            renderer.sprite = StageSurvivalController.GetCircleSprite();
            renderer.color = index == 0
                ? new Color(0.35f, 0.98f, 1f, 0.95f)
                : index == 1
                    ? new Color(1f, 0.9f, 0.2f, 0.95f)
                    : new Color(0.55f, 0.62f, 1f, 0.95f);
            renderer.sortingOrder = 39;
            pulse.SetActive(false);
            return pulse.transform;
        }

        private RoomCircuit NewRoom(int index, Vector2 source)
        {
            RoomCircuit room = new RoomCircuit { Index = index };
            CreatePowerSource(source, index);
            Vector2 statusPosition = index == 0 ? new Vector2(8.4f, 6.55f)
                : index == 1 ? new Vector2(37f, 6.55f)
                : index == 2 ? new Vector2(66.8f, 6.55f)
                : index == 3 ? new Vector2(96.5f, 6.55f)
                : new Vector2(136.5f, 6.55f);
            room.Status = StageEscortController.CreateText(transform, "Circuit Status " + index,
                new Vector3(statusPosition.x, statusPosition.y, -0.45f), 54, 0.12f,
                new Color(0.2f, 0.95f, 1f, 1f), 42);
            room.Status.text = string.Empty;
            return room;
        }

        private CircuitPath CreatePath(Vector2 source, Vector2 bulbPosition, List<Vector2> gapCenters, Vector2 direction)
        {
            CircuitPath path = new CircuitPath();
            float halfGap = playerCount >= 3 ? 1.25f : 1.45f;
            for (int i = 0; i < gapCenters.Count; i++)
            {
                Vector2 center = gapCenters[i];
                Vector2 left = center - direction * halfGap;
                Vector2 right = center + direction * halfGap;
                path.Gaps.Add(new Gap
                {
                    Left = CreateTerminal(left),
                    Right = CreateTerminal(right)
                });
            }

            if (path.Gaps.Count == 0)
            {
                path.Pieces.Add(CreateWirePiece(source, bulbPosition));
            }
            else
            {
                path.Pieces.Add(CreateWirePiece(source, path.Gaps[0].Left));
                for (int i = 0; i < path.Gaps.Count - 1; i++)
                    path.Pieces.Add(CreateWirePiece(path.Gaps[i].Right, path.Gaps[i + 1].Left));
                path.Pieces.Add(CreateWirePiece(path.Gaps[path.Gaps.Count - 1].Right, bulbPosition));
            }
            path.Bulb = CreateBulb(bulbPosition);
            return path;
        }

        private Terminal CreateTerminal(Vector2 position)
        {
            GameObject core = new GameObject("Broken Wire Terminal");
            core.transform.SetParent(transform, false);
            core.transform.position = new Vector3(position.x, position.y, -0.28f);
            core.transform.localScale = Vector3.one * 0.42f;
            SpriteRenderer coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = StageSurvivalController.GetCircleSprite();
            coreRenderer.color = DarkTerminal;
            coreRenderer.sortingOrder = 25;

            GameObject halo = new GameObject("Terminal Glow");
            halo.transform.SetParent(transform, false);
            halo.transform.position = new Vector3(position.x, position.y, -0.27f);
            halo.transform.localScale = Vector3.one * 0.95f;
            SpriteRenderer haloRenderer = halo.AddComponent<SpriteRenderer>();
            haloRenderer.sprite = StageSurvivalController.GetCircleSprite();
            haloRenderer.sortingOrder = 24;
            haloRenderer.enabled = false;
            return new Terminal
            {
                BasePosition = position,
                Position = position,
                Core = coreRenderer,
                Halo = haloRenderer
            };
        }

        private WirePiece CreateWirePiece(Vector2 start, Vector2 end)
        {
            return CreateWirePieceInternal(null, null, start, end, false, false);
        }

        private WirePiece CreateWirePiece(Vector2 start, Terminal end)
        {
            return CreateWirePieceInternal(null, end, start, Vector2.zero, false, true);
        }

        private WirePiece CreateWirePiece(Terminal start, Terminal end)
        {
            return CreateWirePieceInternal(start, end, Vector2.zero, Vector2.zero, true, true);
        }

        private WirePiece CreateWirePiece(Terminal start, Vector2 end)
        {
            return CreateWirePieceInternal(start, null, Vector2.zero, end, true, false);
        }

        private WirePiece CreateWirePieceInternal(
            Terminal startTerminal, Terminal endTerminal, Vector2 fixedStart, Vector2 fixedEnd,
            bool usesStart, bool usesEnd)
        {
            GameObject root = new GameObject("Circuit Wire");
            root.transform.SetParent(transform, false);
            LineRenderer dark = root.AddComponent<LineRenderer>();
            ConfigureLine(dark, 0.16f, DarkWire, 20);
            GameObject glowObject = new GameObject("Live Current");
            glowObject.transform.SetParent(root.transform, false);
            LineRenderer glow = glowObject.AddComponent<LineRenderer>();
            ConfigureLine(glow, 0.095f, ActiveElectric, 22);
            glow.enabled = false;
            WirePiece piece = new WirePiece
            {
                StartTerminal = startTerminal,
                EndTerminal = endTerminal,
                FixedStart = fixedStart,
                FixedEnd = fixedEnd,
                UsesStartTerminal = usesStart,
                UsesEndTerminal = usesEnd,
                Base = dark,
                Glow = glow
            };
            piece.Refresh(false);
            return piece;
        }

        private static void ConfigureLine(LineRenderer line, float width, Color color, int order)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.numCapVertices = 5;
            line.numCornerVertices = 3;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = order;
        }

        private BulbVisual CreateBulb(Vector2 position)
        {
            GameObject halo = new GameObject("Bulb Halo");
            halo.transform.SetParent(transform, false);
            halo.transform.position = new Vector3(position.x, position.y, -0.22f);
            halo.transform.localScale = Vector3.one * 2.6f;
            SpriteRenderer haloRenderer = halo.AddComponent<SpriteRenderer>();
            haloRenderer.sprite = StageSurvivalController.GetCircleSprite();
            haloRenderer.sortingOrder = 19;
            haloRenderer.enabled = false;

            GameObject glass = new GameObject("Large Circuit Bulb");
            glass.transform.SetParent(transform, false);
            glass.transform.position = new Vector3(position.x, position.y, -0.3f);
            glass.transform.localScale = Vector3.one * 1.45f;
            SpriteRenderer glassRenderer = glass.AddComponent<SpriteRenderer>();
            glassRenderer.sprite = StageSurvivalController.GetCircleSprite();
            glassRenderer.sortingOrder = 27;
            StageEscortController.AddBoxOutline(glass.transform, Vector2.down * 0.78f,
                new Vector2(0.68f, 0.5f), new Color(0.12f, 0.15f, 0.18f), 29);

            GameObject filamentObject = new GameObject("Bulb Filament");
            filamentObject.transform.SetParent(transform, false);
            LineRenderer filament = filamentObject.AddComponent<LineRenderer>();
            ConfigureLine(filament, 0.07f, Color.black, 30);
            filament.positionCount = 4;
            filament.SetPosition(0, position + new Vector2(-0.32f, -0.05f));
            filament.SetPosition(1, position + new Vector2(-0.12f, 0.24f));
            filament.SetPosition(2, position + new Vector2(0.12f, -0.05f));
            filament.SetPosition(3, position + new Vector2(0.32f, 0.24f));
            BulbVisual bulb = new BulbVisual { Glass = glassRenderer, Halo = haloRenderer, Filament = filament };
            bulb.SetPowered(false);
            return bulb;
        }

        private void CreatePowerSource(Vector2 position, int roomIndex)
        {
            GameObject root = new GameObject("Power Source Room " + (roomIndex + 1));
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(position.x, position.y, -0.28f);
            StageEscortController.AddFilledRect(root.transform, "Battery", Vector2.zero,
                new Vector2(1.25f, 1.65f), new Color(1f, 0.78f, 0.12f), 25);
            StageEscortController.AddBoxOutline(root.transform, Vector2.zero,
                new Vector2(1.25f, 1.65f), new Color(0.18f, 0.14f, 0.08f), 27);
            StageEscortController.AddLine(root.transform, new Vector2(0.15f, 0.58f), new Vector2(-0.22f, 0.05f),
                0.11f, Color.white, 29);
            StageEscortController.AddLine(root.transform, new Vector2(-0.22f, 0.05f), new Vector2(0.18f, 0.05f),
                0.11f, Color.white, 29);
            StageEscortController.AddLine(root.transform, new Vector2(0.18f, 0.05f), new Vector2(-0.16f, -0.58f),
                0.11f, Color.white, 29);
        }

        private void CreateRoomSign(int roomIndex)
        {
            float[] centers = { 0f, 26f, 55f, 85f, 120f };
            TextMesh text = StageEscortController.CreateText(transform, "Room Sign " + roomIndex,
                new Vector3(centers[roomIndex], 7.15f, -0.38f), 52, 0.11f,
                new Color(0.13f, 0.28f, 0.38f, 0.82f), 32);
            text.text = "ROOM " + (roomIndex + 1);
        }

        private void EvaluateActiveRoom(float deltaTime)
        {
            energizedPlayers.Clear();
            int roomIndex = FirstIncompleteRoom();
            if (roomIndex < 0) return;
            BuildPlayerShapes();
            RoomCircuit room = rooms[roomIndex];

            if (roomIndex == 4)
            {
                EvaluateRelayRoom(room, deltaTime);
                BroadcastState(false);
                return;
            }

            bool allPaths = true;
            for (int i = 0; i < room.Paths.Count; i++)
            {
                EvaluateSequentialPath(room.Paths[i]);
                allPaths &= room.Paths[i].Complete;
            }

            if (allPaths)
            {
                CompleteRoom(roomIndex);
            }
        }

        private void EvaluateClientPresentation()
        {
            energizedPlayers.Clear();
            int roomIndex = FirstIncompleteRoom();
            if (roomIndex < 0) return;
            BuildPlayerShapes();
            RoomCircuit room = rooms[roomIndex];
            if (roomIndex < 4)
            {
                for (int i = 0; i < room.Paths.Count; i++) EvaluateSequentialPath(room.Paths[i]);
                return;
            }

            for (int p = 0; p < room.Paths.Count; p++)
            {
                CircuitPath relay = room.Paths[p];
                int passed = 0;
                for (int i = 0; i < relay.Gaps.Count; i++)
                {
                    float point = (i + 1f) / (relay.Gaps.Count + 1f);
                    bool reached = room.RelayProgress >= point - 0.035f;
                    bool bridged = reached && IsGapBridged(relay.Gaps[i], true);
                    relay.Gaps[i].Bridged = bridged;
                    relay.Gaps[i].Left.SetPowered(reached);
                    relay.Gaps[i].Right.SetPowered(reached && bridged);
                    if (room.RelayProgress > point) passed++;
                }
                relay.PoweredPieces = Mathf.Clamp(passed + 1, 1, relay.Pieces.Count);
            }
        }

        private void EvaluateSequentialPath(CircuitPath path)
        {
            int bridgedPrefix = 0;
            for (int i = 0; i < path.Gaps.Count; i++)
            {
                Gap gap = path.Gaps[i];
                bool leftPowered = i == bridgedPrefix;
                bool bridged = leftPowered && IsGapBridged(gap, true);
                gap.Bridged = bridged;
                gap.Left.SetPowered(leftPowered);
                gap.Right.SetPowered(bridged);
                if (bridged)
                {
                    bridgedPrefix++;
                    if (!gap.PreviousBridged) PlayZap();
                }
                gap.PreviousBridged = bridged;
                if (!bridged) break;
            }
            for (int i = bridgedPrefix; i < path.Gaps.Count; i++)
            {
                if (i > bridgedPrefix || !path.Gaps[i].Bridged)
                {
                    path.Gaps[i].Left.SetPowered(i == bridgedPrefix);
                    path.Gaps[i].Right.SetPowered(false);
                    path.Gaps[i].PreviousBridged = false;
                }
            }
            path.PoweredPieces = Mathf.Min(path.Pieces.Count, bridgedPrefix + 1);
            path.Complete = bridgedPrefix == path.Gaps.Count;
        }

        private void EvaluateRelayRoom(RoomCircuit room, float deltaTime)
        {
            bool blocked = false;
            for (int p = 0; p < room.Paths.Count; p++)
            {
                CircuitPath path = room.Paths[p];
                int passed = 0;
                for (int i = 0; i < path.Gaps.Count; i++)
                {
                    float point = (i + 1f) / (path.Gaps.Count + 1f);
                    Gap gap = path.Gaps[i];
                    bool reached = room.RelayProgress >= point - 0.035f;
                    bool bridged = reached && IsGapBridged(gap, true);
                    gap.Bridged = bridged;
                    gap.Left.SetPowered(reached);
                    gap.Right.SetPowered(reached && bridged);
                    if (reached && bridged && !gap.PreviousBridged) PlayZap();
                    gap.PreviousBridged = bridged;
                    if (room.RelayProgress >= point && !bridged)
                    {
                        room.RelayProgress = point;
                        blocked = true;
                    }
                    if (room.RelayProgress > point) passed++;
                }
                path.PoweredPieces = Mathf.Clamp(passed + 1, 1, path.Pieces.Count);
                path.Complete = room.RelayProgress >= 1f;
            }

            if (!blocked) room.RelayProgress = Mathf.Min(1f, room.RelayProgress + deltaTime / 13.5f);
            for (int i = 0; i < room.RelayPulses.Count; i++)
            {
                Transform pulse = room.RelayPulses[i];
                if (pulse == null) continue;
                pulse.gameObject.SetActive(!room.Completed);
                pulse.position = Vector2.Lerp(room.RelayStarts[i], room.RelayEnds[i], room.RelayProgress);
                pulse.localScale = Vector3.one * (0.62f + Mathf.PingPong(Time.unscaledTime * 4f + i * 0.3f, 0.28f));
            }
            if (room.RelayProgress >= 1f) CompleteRoom(4);
        }

        private bool IsGapBridged(Gap gap, bool markEnergized)
        {
            reachablePlayers.Clear();
            playerQueue.Clear();
            for (int i = 0; i < playerShapes.Count; i++)
            {
                if (TouchesPoint(playerShapes[i], gap.Left.Position))
                {
                    reachablePlayers.Add(playerShapes[i].Player);
                    playerQueue.Enqueue(playerShapes[i].Player);
                }
            }

            while (playerQueue.Count > 0)
            {
                PlayerController2D current = playerQueue.Dequeue();
                PlayerShape currentShape = FindShape(current);
                if (currentShape == null) continue;
                for (int i = 0; i < playerShapes.Count; i++)
                {
                    PlayerShape other = playerShapes[i];
                    if (reachablePlayers.Contains(other.Player)) continue;
                    if (PlayersTouch(currentShape, other))
                    {
                        reachablePlayers.Add(other.Player);
                        playerQueue.Enqueue(other.Player);
                    }
                }
            }

            bool reachedRight = false;
            foreach (PlayerController2D player in reachablePlayers)
            {
                PlayerShape shape = FindShape(player);
                if (shape != null && TouchesPoint(shape, gap.Right.Position)) reachedRight = true;
                if (markEnergized) energizedPlayers.Add(player);
            }
            return reachedRight;
        }

        private void BuildPlayerShapes()
        {
            playerShapes.Clear();
            PlayerController2D[] found = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                PlayerController2D player = found[i];
                if (player == null || !player.gameObject.activeInHierarchy) continue;
                Collider2D[] allColliders = player.GetComponentsInChildren<Collider2D>(false);
                List<Collider2D> conductiveColliders = new List<Collider2D>(allColliders.Length);
                bool hasBounds = false;
                Bounds bounds = new Bounds(player.transform.position, Vector3.zero);
                for (int c = 0; c < allColliders.Length; c++)
                {
                    Collider2D collider = allColliders[c];
                    if (!IsConductiveCollider(collider)) continue;
                    conductiveColliders.Add(collider);
                    if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
                    else bounds.Encapsulate(collider.bounds);
                }
                if (hasBounds)
                {
                    playerShapes.Add(new PlayerShape
                    {
                        Player = player,
                        Colliders = conductiveColliders.ToArray(),
                        Bounds = bounds
                    });
                }
            }
        }

        private static bool IsConductiveCollider(Collider2D collider)
        {
            if (collider == null || !collider.enabled) return false;
            // Human arms are triggers so long hand-drawn arms cannot push the body
            // through walls. They remain part of the drawing for electrical contact.
            return !collider.isTrigger || collider.GetComponent<LineRenderer>() != null;
        }

        private static bool TouchesPoint(PlayerShape shape, Vector2 point)
        {
            Bounds expanded = shape.Bounds;
            expanded.Expand(TerminalContactDistance * 2f);
            if (!expanded.Contains(point)) return false;
            for (int i = 0; i < shape.Colliders.Length; i++)
            {
                Collider2D collider = shape.Colliders[i];
                if (!IsConductiveCollider(collider)) continue;
                Vector2 closest = collider.ClosestPoint(point);
                if ((closest - point).sqrMagnitude <= TerminalContactDistance * TerminalContactDistance) return true;
            }
            return false;
        }

        private static bool PlayersTouch(PlayerShape a, PlayerShape b)
        {
            Bounds expanded = a.Bounds;
            expanded.Expand(PlayerContactDistance * 2f);
            if (!expanded.Intersects(b.Bounds)) return false;
            for (int i = 0; i < a.Colliders.Length; i++)
            {
                Collider2D first = a.Colliders[i];
                if (!IsConductiveCollider(first)) continue;
                Bounds firstBounds = first.bounds;
                firstBounds.Expand(PlayerContactDistance * 2f);
                for (int j = 0; j < b.Colliders.Length; j++)
                {
                    Collider2D second = b.Colliders[j];
                    if (!IsConductiveCollider(second) || !firstBounds.Intersects(second.bounds)) continue;
                    ColliderDistance2D distance = Physics2D.Distance(first, second);
                    if (distance.isValid && distance.distance <= PlayerContactDistance) return true;
                }
            }
            return false;
        }

        private PlayerShape FindShape(PlayerController2D player)
        {
            for (int i = 0; i < playerShapes.Count; i++) if (playerShapes[i].Player == player) return playerShapes[i];
            return null;
        }

        private void UpdateMovingGaps()
        {
            if (rooms.Count < 3) return;
            CircuitPath moving = rooms[2].Paths[0];
            for (int i = 0; i < moving.Gaps.Count; i++) moving.Gaps[i].UpdateMotion(motionClock);
        }

        private void CompleteRoom(int index)
        {
            if (index < 0 || index >= rooms.Count || rooms[index].Completed) return;
            rooms[index].Completed = true;
            completedMask |= 1 << index;
            for (int p = 0; p < rooms[index].Paths.Count; p++) rooms[index].Paths[p].Complete = true;
            if (rooms[index].Status != null) rooms[index].Status.text = LocalizationManager.T("human_circuit_powered");
            PlaySuccess();
            StartCoroutine(SuccessFlash(index));
            BroadcastState(true);
        }

        private IEnumerator SuccessFlash(int roomIndex)
        {
            float[] centers = { 0f, 26f, 55f, 85f, 120f };
            GameObject flash = new GameObject("Circuit Success Flash");
            flash.transform.SetParent(transform, false);
            flash.transform.position = new Vector3(centers[roomIndex], 0f, -0.18f);
            SpriteRenderer renderer = flash.AddComponent<SpriteRenderer>();
            renderer.sprite = StageSurvivalController.GetCircleSprite();
            renderer.sortingOrder = 45;
            float duration = roomIndex == 4 ? 1.35f : 0.75f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = elapsed / duration;
                flash.transform.localScale = Vector3.one * Mathf.Lerp(1f, roomIndex == 4 ? 19f : 8f, t);
                renderer.color = new Color(0.25f, 0.95f, 1f, (1f - t) * 0.38f);
                yield return null;
            }
            Destroy(flash);
            yield return new WaitForSecondsRealtime(0.65f);
            if (rooms[roomIndex].Status != null) rooms[roomIndex].Status.text = string.Empty;
        }

        private void UpdateDoors()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomCircuit room = rooms[i];
                float target = room.Completed ? 1f : 0f;
                room.DoorOpen = Mathf.MoveTowards(room.DoorOpen, target, Time.deltaTime * 1.5f);
                if (room.Door == null) continue;
                Vector3 scale = room.DoorBaseScale;
                scale.y *= Mathf.Max(0.02f, 1f - room.DoorOpen);
                room.Door.transform.localScale = scale;
                bool solid = room.DoorOpen < 0.72f;
                if (room.DoorColliders != null)
                    for (int c = 0; c < room.DoorColliders.Length; c++)
                        if (room.DoorColliders[c] != null) room.DoorColliders[c].enabled = solid;
            }
        }

        private void RefreshAllVisuals()
        {
            for (int r = 0; r < rooms.Count; r++)
            {
                RoomCircuit room = rooms[r];
                for (int p = 0; p < room.Paths.Count; p++) room.Paths[p].RefreshVisuals(room.Completed);
            }
        }

        private void RefreshPlayerEffects()
        {
            foreach (KeyValuePair<PlayerController2D, PlayerElectricVisual> pair in playerEffects) pair.Value.Hide();
            for (int i = 0; i < playerShapes.Count; i++)
            {
                PlayerShape shape = playerShapes[i];
                if (!energizedPlayers.Contains(shape.Player)) continue;
                if (!playerEffects.TryGetValue(shape.Player, out PlayerElectricVisual visual))
                {
                    visual = CreatePlayerEffect(shape.Player);
                    playerEffects[shape.Player] = visual;
                }
                visual.Refresh(shape.Bounds, shape.Player.GetInstanceID() * 0.017f);
            }
        }

        private PlayerElectricVisual CreatePlayerEffect(PlayerController2D player)
        {
            GameObject root = new GameObject("Conductive Player Effect " + player.GetInstanceID());
            root.transform.SetParent(transform, false);
            LineRenderer arc = root.AddComponent<LineRenderer>();
            arc.useWorldSpace = true;
            arc.startWidth = arc.endWidth = 0.085f;
            arc.numCapVertices = 3;
            arc.numCornerVertices = 2;
            arc.material = new Material(Shader.Find("Sprites/Default"));
            arc.sortingOrder = 48;
            return new PlayerElectricVisual { Root = root, Arc = arc };
        }

        private void ApplyClientProgress()
        {
            if (rooms.Count < 5) return;
            for (int i = 0; i < rooms[4].RelayPulses.Count; i++)
            {
                Transform pulse = rooms[4].RelayPulses[i];
                if (pulse == null) continue;
                pulse.gameObject.SetActive(!rooms[4].Completed);
                pulse.position = Vector2.Lerp(rooms[4].RelayStarts[i], rooms[4].RelayEnds[i], rooms[4].RelayProgress);
            }
        }

        private int FirstIncompleteRoom()
        {
            for (int i = 0; i < rooms.Count; i++) if (!rooms[i].Completed) return i;
            return -1;
        }

        private GameObject FindStageObject(string id)
        {
            StageEditorObject[] markers = GetComponentsInChildren<StageEditorObject>(true);
            for (int i = 0; i < markers.Length; i++)
                if (markers[i] != null && markers[i].objectId == id) return markers[i].gameObject;
            Transform root = transform.Find(id);
            return root != null ? root.gameObject : null;
        }

        private static List<Vector2> EvenGapCenters(float minX, float maxX, float y, int count)
        {
            List<Vector2> result = new List<Vector2>(count);
            if (count <= 1)
            {
                result.Add(new Vector2((minX + maxX) * 0.5f, y));
                return result;
            }
            for (int i = 0; i < count; i++) result.Add(new Vector2(Mathf.Lerp(minX, maxX, i / (count - 1f)), y));
            return result;
        }

        private void SetPreviewState()
        {
            for (int r = 0; r < rooms.Count; r++)
            {
                for (int p = 0; p < rooms[r].Paths.Count; p++)
                {
                    CircuitPath path = rooms[r].Paths[p];
                    path.PoweredPieces = r == 0 ? 1 : 0;
                    for (int g = 0; g < path.Gaps.Count; g++)
                    {
                        path.Gaps[g].Left.SetPowered(r == 0 && g == 0);
                        path.Gaps[g].Right.SetPowered(false);
                    }
                }
            }
            RefreshAllVisuals();
        }

        private void BroadcastState(bool force)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null || !force && Time.unscaledTime < nextStateAt) return;
            nextStateAt = Time.unscaledTime + 0.18f;
            NetworkState state = new NetworkState
            {
                Sequence = ++stateSequence,
                CompletedMask = completedMask,
                RelayProgress = rooms.Count > 4 ? rooms[4].RelayProgress : 0f
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind || HasAuthority || !IsHostPlayer(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= lastStateSequence) return;
            lastStateSequence = state.Sequence;
            int oldMask = completedMask;
            completedMask = state.CompletedMask;
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].Completed = (completedMask & (1 << i)) != 0;
                if ((oldMask & (1 << i)) == 0 && rooms[i].Completed)
                {
                    if (rooms[i].Status != null) rooms[i].Status.text = LocalizationManager.T("human_circuit_powered");
                    PlaySuccess();
                    StartCoroutine(SuccessFlash(i));
                }
            }
            if (rooms.Count > 4) rooms[4].RelayProgress = state.RelayProgress;
        }

        private bool IsHostPlayer(string id)
        {
            if (onlineManager == null || string.IsNullOrEmpty(id)) return false;
            OnlinePlayerInfo[] roster = onlineManager.CurrentLobby != null ? onlineManager.CurrentLobby.Players : null;
            if (roster == null) return false;
            for (int i = 0; i < roster.Length; i++)
                if (roster[i] != null && roster[i].IsHost && roster[i].PlayerId == id) return true;
            return false;
        }

        private void PlayZap()
        {
            if (previewOnly || audioSource == null || Time.unscaledTime < nextZapAt) return;
            nextZapAt = Time.unscaledTime + 0.11f;
            audioSource.pitch = Random.Range(0.92f, 1.12f);
            audioSource.volume = GameSfx.MasterVolume * 0.8f;
            audioSource.PlayOneShot(GetZapClip());
        }

        private void PlaySuccess()
        {
            if (previewOnly || audioSource == null) return;
            audioSource.pitch = 1f;
            audioSource.volume = GameSfx.MasterVolume;
            audioSource.PlayOneShot(GetSuccessClip());
        }

        private static AudioClip GetZapClip()
        {
            if (zapClip != null) return zapClip;
            const int rate = 22050;
            const int count = 2205;
            float[] samples = new float[count];
            uint noise = 0x8243ab1u;
            for (int i = 0; i < count; i++)
            {
                noise = noise * 1664525u + 1013904223u;
                float n = ((noise >> 8) & 0xffff) / 32767.5f - 1f;
                float t = i / (float)rate;
                samples[i] = (n * 0.52f + Mathf.Sin(t * Mathf.PI * 2f * 1180f) * 0.35f)
                    * Mathf.Exp(-t * 38f);
            }
            zapClip = AudioClip.Create("13-3 Electric Snap", count, 1, rate, false);
            zapClip.SetData(samples, 0);
            return zapClip;
        }

        private static AudioClip GetSuccessClip()
        {
            if (successClip != null) return successClip;
            const int rate = 22050;
            const int count = 11025;
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float frequency = t < 0.16f ? 660f : t < 0.32f ? 880f : 1320f;
                float envelope = Mathf.Clamp01(1f - t / 0.5f);
                samples[i] = (Mathf.Sin(t * Mathf.PI * 2f * frequency)
                    + Mathf.Sin(t * Mathf.PI * 2f * frequency * 1.5f) * 0.25f) * envelope * 0.3f;
            }
            successClip = AudioClip.Create("13-3 Power On", count, 1, rate, false);
            successClip.SetData(samples, 0);
            return successClip;
        }
    }
}
