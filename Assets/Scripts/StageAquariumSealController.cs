using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageAquariumSealController : MonoBehaviour
    {
        private const string StageId = "6-3";
        private const string StateKind = "aquarium_seal_state";
        private const float RoundOneSeconds = 90f;
        private const float RoundTwoSeconds = 120f;
        private const float RoundThreeSeconds = 150f;
        private const float ClearHoldSeconds = 0.65f;
        private const float BoxPreviewSeconds = 1.35f;
        private const float BoxCooldownSeconds = 0.55f;
        private const float FloorY = -6.2f;
        private const int MaximumWaterDrops = 90;
        private static readonly Vector2[] BoxSizes =
        {
            new Vector2(0.9f, 0.9f),
            new Vector2(1.5f, 1.5f),
            new Vector2(2.2f, 2.2f),
            new Vector2(1.1f, 2.7f),
            new Vector2(2.5f, 0.8f)
        };

        private enum SealPhase
        {
            Active,
            RoundClear,
            Failed,
            Complete
        }

        [System.Serializable]
        private sealed class NetworkState
        {
            public int Sequence;
            public int Round;
            public int Attempt;
            public int Phase;
            public float Remaining;
            public int SealedMask;
            public int PreviewIndex;
            public bool ButtonPressed;
            public float WaterDepth;
        }

        private sealed class HoleVisual
        {
            public Vector2 Center;
            public SpriteRenderer Core;
            public SpriteRenderer Rim;
            public Transform Leak;
            public bool Sealed;
        }

        private StageManager stageManager;
        private StageLoader stageLoader;
        private StageObjectFactory factory;
        private OnlineManager onlineManager;
        private UIManager uiManager;
        private CameraFollow2D cameraFollow;
        private Transform arenaRoot;
        private readonly List<HoleVisual> holes = new List<HoleVisual>(12);
        private TextMesh monitorMain;
        private TextMesh monitorSub;
        private StageBoxDropper boxDropper;
        private Collider2D boxButton;
        private Transform buttonCap;
        private Vector3 buttonCapScale;
        private SpriteRenderer buttonGlow;
        private Transform boxPreview;
        private SpriteRenderer accumulatedWaterRenderer;
        private LineRenderer accumulatedWaterSurface;
        private Camera gameCamera;
        private bool previousCameraFollowEnabled;
        private Vector3 previousCameraPosition;
        private float previousCameraSize;
        private bool cameraCaptured;
        private float roomWidth;
        private float remaining;
        private float accumulatedWaterDepth;
        private float nextWaterDropAt;
        private float allSealedTime;
        private float transitionRemaining;
        private float nextBroadcastAt;
        private float nextPreviewAt;
        private float nextBoxAt;
        private int round = 1;
        private int roundAttempt;
        private int sequence;
        private int receivedSequence;
        private int sealedMask;
        private int previewIndex;
        private bool buttonPressed;
        private bool initialized;
        private int waterDropCount;
        private int nextLeakingHole;
        private SealPhase phase;

        private bool IsOnline => stageManager != null && stageManager.IsOnlineStageActive;
        private bool HasAuthority => !IsOnline || stageManager.IsOnlineStageHost;

        private void Awake()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageLoader = Object.FindFirstObjectByType<StageLoader>();
            factory = Object.FindFirstObjectByType<StageObjectFactory>();
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            uiManager = Object.FindFirstObjectByType<UIManager>();
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

            AnimateAquarium();
            ConfigureAquariumCamera();
            ApplyControls();
            if (!HasAuthority)
            {
                if (phase == SealPhase.Active)
                {
                    if (!LocalMultiplayerDebugMode.NoTimeLimit)
                        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    EmitLeakingWater();
                }
                RefreshMonitor();
                return;
            }

            switch (phase)
            {
                case SealPhase.Active:
                    if (!LocalMultiplayerDebugMode.NoTimeLimit)
                        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
                    UpdateBoxStation();
                    EvaluateHoles();
                    if (!LocalMultiplayerDebugMode.NoTimeLimit && remaining <= 0f) FailRound();
                    break;
                case SealPhase.RoundClear:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(round + 1);
                    break;
                case SealPhase.Failed:
                    transitionRemaining -= Time.unscaledDeltaTime;
                    if (transitionRemaining <= 0f) BeginRound(round);
                    break;
            }

            BroadcastState();
            RefreshMonitor();
        }

        private void BeginRound(int nextRound, int synchronizedAttempt = -1)
        {
            round = Mathf.Clamp(nextRound, 1, 3);
            roundAttempt = synchronizedAttempt >= 0
                ? synchronizedAttempt
                : roundAttempt + 1;
            phase = SealPhase.Active;
            remaining = GetRoundSeconds(round);
            transitionRemaining = 0f;
            allSealedTime = 0f;
            accumulatedWaterDepth = 0f;
            waterDropCount = 0;
            nextLeakingHole = 0;
            nextWaterDropAt = 0f;
            sealedMask = 0;
            previewIndex = (round - 1) % BoxSizes.Length;
            buttonPressed = false;
            nextPreviewAt = Time.unscaledTime + BoxPreviewSeconds;
            nextBoxAt = 0f;
            BuildArena();
            StartCoroutine(PositionPlayersAfterStageSetup());
            BroadcastState(true);
            RefreshMonitor();
        }

        private static float GetRoundSeconds(int targetRound)
        {
            switch (targetRound)
            {
                case 2: return RoundTwoSeconds;
                case 3: return RoundThreeSeconds;
                default: return RoundOneSeconds;
            }
        }

        private void BuildArena()
        {
            // Online dropper boxes live under the sync manager rather than the
            // arena, so explicitly remove them before rebuilding this attempt.
            boxDropper?.ClearSpawnedBoxes();
            if (arenaRoot != null)
            {
                arenaRoot.gameObject.SetActive(false);
                Destroy(arenaRoot.gameObject);
            }

            holes.Clear();
            boxDropper = null;
            boxButton = null;
            buttonCap = null;
            buttonGlow = null;
            boxPreview = null;
            accumulatedWaterRenderer = null;
            accumulatedWaterSurface = null;

            int playerCount = GetPlayerCount();
            roomWidth = 28f + playerCount * 4f;
            GameObject arena = new GameObject("6-3 Aquarium Round " + round);
            arena.transform.SetParent(transform, false);
            arenaRoot = arena.transform;

            CreateAquariumBackground(playerCount);
            CreateRoomBoundary();
            CreateMonitor();
            CreateHoles(playerCount);
            CreateBoxStation();
            ConfigureAquariumCamera();

            Vector2 spawn = new Vector2(0f, FloorY + 1.35f);
            stageLoader?.SetRuntimeSpawnPosition(spawn);
            Transform local = stageManager != null ? stageManager.ActivePlayerTransform : null;
            if (cameraFollow != null && local != null) cameraFollow.SetTarget(local);
        }

        private void CreateAquariumBackground(int playerCount)
        {
            Vector2 waterSize = new Vector2(roomWidth - 1.2f, 12.8f);
            StageEscortController.AddFilledRect(arenaRoot, "Aquarium Water", new Vector2(0f, 0.45f), waterSize,
                new Color(0.2f, 0.72f, 0.88f, 0.18f), -58);

            // Layered translucent bands and moving-looking pencil caustics keep
            // the tank readable as water instead of one flat blue rectangle.
            Color[] depthColors =
            {
                new Color(0.2f, 0.72f, 0.9f, 0.11f),
                new Color(0.1f, 0.62f, 0.84f, 0.12f),
                new Color(0.06f, 0.48f, 0.72f, 0.14f),
                new Color(0.04f, 0.34f, 0.58f, 0.16f)
            };
            float bandHeight = waterSize.y / depthColors.Length;
            for (int i = 0; i < depthColors.Length; i++)
            {
                float y = 0.45f + waterSize.y * 0.5f - bandHeight * (i + 0.5f);
                StageEscortController.AddFilledRect(arenaRoot, "Water Depth Band " + i,
                    new Vector2(0f, y), new Vector2(waterSize.x, bandHeight + 0.08f), depthColors[i], -57 + i);
            }

            GameObject pool = new GameObject("Accumulated Leaked Water");
            pool.transform.SetParent(arenaRoot, false);
            accumulatedWaterRenderer = pool.AddComponent<SpriteRenderer>();
            accumulatedWaterRenderer.sprite = DoodleRuntimeAssets.SquareSprite;
            accumulatedWaterRenderer.color = new Color(0.08f, 0.58f, 0.84f, 0.48f);
            accumulatedWaterRenderer.sortingOrder = -35;

            GameObject surface = new GameObject("Accumulated Water Surface");
            surface.transform.SetParent(arenaRoot, false);
            accumulatedWaterSurface = surface.AddComponent<LineRenderer>();
            accumulatedWaterSurface.useWorldSpace = false;
            accumulatedWaterSurface.positionCount = 13;
            accumulatedWaterSurface.startWidth = 0.11f;
            accumulatedWaterSurface.endWidth = 0.11f;
            accumulatedWaterSurface.numCapVertices = 4;
            accumulatedWaterSurface.numCornerVertices = 3;
            accumulatedWaterSurface.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            accumulatedWaterSurface.startColor = new Color(0.62f, 0.94f, 1f, 0.72f);
            accumulatedWaterSurface.endColor = new Color(0.28f, 0.78f, 0.96f, 0.58f);
            accumulatedWaterSurface.sortingOrder = -33;
            RefreshAccumulatedWater();

            Color waterLine = new Color(0.08f, 0.52f, 0.72f, 0.34f);
            for (int i = 0; i < 11; i++)
            {
                float y = -5.25f + i * 1.08f;
                float drift = Mathf.Sin(i * 1.73f) * 0.4f;
                StageGun.AddLine(arenaRoot, "Water Pencil Line", new[]
                {
                    new Vector2(-roomWidth * 0.46f, y),
                    new Vector2(-roomWidth * 0.18f + drift, y + 0.08f),
                    new Vector2(roomWidth * 0.16f - drift, y - 0.06f),
                    new Vector2(roomWidth * 0.46f, y + 0.04f)
                }, 0.035f, waterLine, -51);
            }

            float surfaceY = 6.62f;
            for (int i = 0; i < 3; i++)
            {
                float offset = i * 0.1f;
                StageGun.AddLine(arenaRoot, "Aquarium Water Surface " + i, new[]
                {
                    new Vector2(-roomWidth * 0.47f, surfaceY - offset),
                    new Vector2(-roomWidth * 0.28f, surfaceY + 0.1f - offset),
                    new Vector2(-roomWidth * 0.08f, surfaceY - 0.06f - offset),
                    new Vector2(roomWidth * 0.14f, surfaceY + 0.08f - offset),
                    new Vector2(roomWidth * 0.33f, surfaceY - 0.04f - offset),
                    new Vector2(roomWidth * 0.47f, surfaceY + 0.05f - offset)
                }, 0.045f - i * 0.009f,
                    new Color(0.76f, 0.98f, 1f, 0.72f - i * 0.16f), -34 + i);
            }

            for (int i = 0; i < 15; i++)
            {
                float x = Mathf.Lerp(-roomWidth * 0.43f, roomWidth * 0.43f, Mathf.Repeat(i * 0.618f, 1f));
                float y = -4.7f + Mathf.Repeat(i * 1.91f, 10.2f);
                StageGun.AddLine(arenaRoot, "Underwater Light Caustic", new[]
                {
                    new Vector2(x - 0.75f, y), new Vector2(x, y + 0.18f), new Vector2(x + 0.72f, y - 0.03f)
                }, 0.035f, new Color(0.78f, 0.98f, 1f, 0.22f), -32);
            }

            Sprite fishSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/fish");
            Color[] fishColors =
            {
                new Color(0.28f, 0.78f, 0.96f, 0.62f),
                new Color(1f, 0.58f, 0.2f, 0.58f),
                new Color(0.52f, 0.82f, 0.34f, 0.58f),
                new Color(0.72f, 0.48f, 0.92f, 0.56f),
                new Color(1f, 0.78f, 0.22f, 0.62f),
                new Color(0.96f, 0.38f, 0.5f, 0.58f),
                new Color(0.18f, 0.82f, 0.72f, 0.58f),
                new Color(0.54f, 0.66f, 1f, 0.58f)
            };
            int fishCount = 12 + playerCount * 2;
            for (int i = 0; i < fishCount; i++)
            {
                int kind = i % 6;
                bool isLargeFish = i == 0;
                GameObject fish = new GameObject(isLargeFish
                    ? "Large Aquarium Fish"
                    : "Aquarium Fish " + kind + " " + i);
                fish.transform.SetParent(arenaRoot, false);
                CreateFishVisual(fish.transform, fishSprite, kind, fishColors[i % fishColors.Length]);
                if (isLargeFish) fish.transform.localScale = Vector3.one * 4.25f;
                AquariumFishSwimmer swimmer = fish.AddComponent<AquariumFishSwimmer>();
                swimmer.Configure(
                    -roomWidth * 0.5f + 0.55f,
                    roomWidth * 0.5f - 0.55f,
                    FloorY + 0.75f,
                    6.55f,
                    -4.75f + Mathf.Repeat(i * 2.17f, 10.6f),
                    isLargeFish ? 0.32f : 0.72f + (i % 5) * 0.19f,
                    i * 0.137f,
                    i % 2 == 0);
            }

            for (int i = 0; i < 30; i++)
            {
                GameObject bubble = new GameObject("Aquarium Bubble " + i);
                bubble.transform.SetParent(arenaRoot, false);
                bubble.transform.localPosition = new Vector3(
                    Mathf.Lerp(-roomWidth * 0.44f, roomWidth * 0.44f, Mathf.Repeat(i * 0.618f, 1f)),
                    -5.1f + Mathf.Repeat(i * 1.37f, 11f),
                    0f);
                bubble.transform.localScale = Vector3.one * (0.12f + i % 5 * 0.035f);
                SpriteRenderer renderer = bubble.AddComponent<SpriteRenderer>();
                renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                renderer.color = new Color(0.76f, 0.97f, 1f, 0.5f);
                renderer.sortingOrder = -31;
                AquariumBubbleMover mover = bubble.AddComponent<AquariumBubbleMover>();
                mover.Configure(FloorY + 0.65f, 6.5f, 0.24f + i % 5 * 0.07f, i * 0.41f);
            }
        }

        private static void CreateFishVisual(Transform root, Sprite fishSprite, int kind, Color color)
        {
            Sprite sprite = fishSprite != null ? fishSprite : DoodleRuntimeAssets.CircleSprite;
            if (kind == 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    CreateFishSprite(root, "School Fish " + i, sprite,
                        new Vector2((i - 1) * 0.5f, (i % 2 == 0 ? -1f : 1f) * 0.2f),
                        new Vector2(0.48f, 0.36f), color, -38);
                }
                return;
            }
            if (kind == 4)
            {
                GameObject ray = CreateFishSprite(root, "Ray Body", DoodleRuntimeAssets.SquareSprite,
                    Vector2.zero, new Vector2(0.92f, 0.62f), color, -38);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                StageGun.AddLine(root, "Ray Tail", new[]
                {
                    new Vector2(-0.56f, 0f), new Vector2(-1.05f, 0.06f), new Vector2(-1.48f, -0.08f)
                }, 0.045f, new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, color.a), -37);
                return;
            }
            if (kind == 5)
            {
                CreateFishSprite(root, "Puffer Body", DoodleRuntimeAssets.CircleSprite,
                    Vector2.zero, new Vector2(0.78f, 0.78f), color, -38);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    StageGun.AddLine(root, "Puffer Spine", new[] { direction * 0.35f, direction * 0.56f },
                        0.035f, new Color(color.r * 0.62f, color.g * 0.62f, color.b * 0.62f, color.a), -37);
                }
                return;
            }

            Vector2 size = kind == 1
                ? new Vector2(1.32f, 0.52f)
                : kind == 3
                    ? new Vector2(0.68f, 0.95f)
                    : new Vector2(0.88f, 0.66f);
            CreateFishSprite(root, "Fish Body", sprite, Vector2.zero, size, color, -38);
            Color detail = new Color(color.r * 0.58f, color.g * 0.58f, color.b * 0.58f, color.a * 0.9f);
            if (kind == 1)
            {
                for (int stripe = -1; stripe <= 1; stripe++)
                {
                    float x = stripe * 0.22f;
                    StageGun.AddLine(root, "Long Fish Stripe", new[]
                    {
                        new Vector2(x - 0.04f, -0.2f), new Vector2(x + 0.04f, 0.2f)
                    }, 0.045f, detail, -37);
                }
            }
            else if (kind == 3)
            {
                StageGun.AddLine(root, "Angel Fish Fins", new[]
                {
                    new Vector2(-0.08f, 0.24f), new Vector2(-0.28f, 0.66f), new Vector2(0.08f, 0.42f),
                    new Vector2(0.24f, -0.66f), new Vector2(0.04f, -0.3f)
                }, 0.055f, detail, -37);
            }
        }

        private static GameObject CreateFishSprite(
            Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            child.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.01f, spriteSize.x),
                size.y / Mathf.Max(0.01f, spriteSize.y), 1f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return child;
        }

        private void CreateRoomBoundary()
        {
            CreateSolid("Aquarium Floor", new Vector2(0f, FloorY), new Vector2(roomWidth, 0.75f));
            StageRedrawZoneFactory.CreateRuntimeFloorZone(arenaRoot,
                "6-3_runtime_redraw_zone_" + round,
                new Vector2(0f, FloorY), roomWidth, 13.2f);
            CreateSolid("Aquarium Ceiling", new Vector2(0f, 7.25f), new Vector2(roomWidth, 0.65f));
            CreateSolid("Aquarium Left Glass", new Vector2(-roomWidth * 0.5f, 0.5f), new Vector2(0.65f, 14.2f));
            CreateSolid("Aquarium Right Glass", new Vector2(roomWidth * 0.5f, 0.5f), new Vector2(0.65f, 14.2f));

            Color glass = new Color(0.08f, 0.35f, 0.48f, 0.88f);
            StageEscortController.AddBoxOutline(arenaRoot, new Vector2(0f, 0.5f),
                new Vector2(roomWidth, 14.2f), glass, 4);
        }

        private void CreateSolid(string name, Vector2 position, Vector2 size)
        {
            GameObject solid = new GameObject(name);
            solid.transform.SetParent(arenaRoot, false);
            solid.transform.position = position;
            solid.layer = 6;
            solid.tag = "Ground";
            BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
            collider.size = size;
            StageEscortController.AddFilledRect(solid.transform, "Glass Paper", Vector2.zero, size,
                new Color(0.72f, 0.9f, 0.92f, 0.52f), 3);
            StageEscortController.AddBoxOutline(solid.transform, Vector2.zero, size,
                new Color(0.08f, 0.34f, 0.46f, 0.92f), 5);
        }

        private void CreateMonitor()
        {
            GameObject monitor = new GameObject("6-3 Aquarium Monitor");
            monitor.transform.SetParent(arenaRoot, false);
            monitor.transform.position = new Vector3(0f, 5.55f, 0f);
            DoodleMonitorVisuals.Build(monitor.transform, new Vector2(13.8f, 2.65f), 23);
            monitorMain = StageEscortController.CreateText(monitor.transform, "Main",
                new Vector3(0f, 0.42f, -0.03f), 54, 0.125f,
                new Color(0.02f, 0.37f, 0.58f), 27);
            monitorSub = StageEscortController.CreateText(monitor.transform, "Sub",
                new Vector3(0f, -0.48f, -0.04f), 42, 0.085f,
                new Color(0.58f, 0.2f, 0.08f), 28);
        }

        private void CreateHoles(int playerCount)
        {
            float usableWidth = roomWidth - 10f;
            Vector2[] layout = GetFixedHoleLayout(playerCount);
            int holeCount = Mathf.Min(layout.Length, playerCount * round);
            for (int i = 0; i < holeCount; i++)
            {
                Vector2 normalized = layout[i];
                CreateHole(new Vector2(normalized.x * usableWidth, normalized.y), holes.Count);
            }
        }

        private static Vector2[] GetFixedHoleLayout(int playerCount)
        {
            // Entries are ordered by round: the first N are round one, the
            // next N round two, and the final N are lateral partners rather
            // than another row above. Every roster size gets its own pattern.
            switch (Mathf.Clamp(playerCount, 1, 4))
            {
                case 1:
                    return new[]
                    {
                        new Vector2(0f, -3.05f),
                        new Vector2(-0.07f, -1.2f),
                        new Vector2(0.01f, -1.2f)
                    };
                case 2:
                    return new[]
                    {
                        new Vector2(-0.23f, -3.15f), new Vector2(0.2f, -1.85f),
                        new Vector2(-0.06f, 0.15f), new Vector2(0.34f, -3.65f),
                        new Vector2(0.02f, 0.15f), new Vector2(0.42f, -3.65f)
                    };
                case 3:
                    return new[]
                    {
                        new Vector2(-0.33f, -3.0f), new Vector2(0f, -1.15f), new Vector2(0.3f, -3.55f),
                        new Vector2(-0.19f, 0.75f), new Vector2(0.13f, -3.2f), new Vector2(0.38f, -0.35f),
                        new Vector2(-0.11f, 0.75f), new Vector2(0.21f, -3.2f), new Vector2(0.46f, -0.35f)
                    };
                default:
                    return new[]
                    {
                        new Vector2(-0.36f, -2.85f), new Vector2(-0.13f, -0.65f),
                        new Vector2(0.14f, -3.45f), new Vector2(0.35f, -1.45f),
                        new Vector2(-0.43f, 1.05f), new Vector2(-0.25f, -3.75f),
                        new Vector2(0.02f, 1.65f), new Vector2(0.28f, -0.05f),
                        new Vector2(-0.31f, -2.85f), new Vector2(-0.08f, -0.65f),
                        new Vector2(0.19f, -3.45f), new Vector2(0.4f, -1.45f)
                    };
            }
        }

        private void CreateHole(Vector2 center, int index)
        {
            GameObject root = new GameObject("Fixed Aquarium Hole " + (index + 1));
            root.transform.SetParent(arenaRoot, false);
            root.transform.position = center;

            SpriteRenderer rim = null;

            GameObject coreObject = new GameObject("Open Hole");
            coreObject.transform.SetParent(root.transform, false);
            coreObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            coreObject.transform.localScale = Vector3.one * 0.3f;
            SpriteRenderer core = coreObject.AddComponent<SpriteRenderer>();
            core.sprite = DoodleRuntimeAssets.CircleSprite;
            core.color = new Color(0.025f, 0.1f, 0.16f, 0.94f);
            core.sortingOrder = 8;

            Color crackColor = new Color(0.035f, 0.055f, 0.065f, 0.92f);
            float[] angles = { 8f, 61f, 119f, 176f, 231f, 298f };
            for (int crack = 0; crack < angles.Length; crack++)
            {
                float radians = angles[crack] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector2 side = new Vector2(-direction.y, direction.x);
                float length = 0.48f + (crack % 3) * 0.12f;
                StageGun.AddLine(root.transform, "Glass Crack " + crack, new[]
                {
                    direction * 0.14f,
                    direction * (length * 0.56f) + side * (crack % 2 == 0 ? 0.08f : -0.07f),
                    direction * length
                }, 0.035f, crackColor, 7);
            }

            GameObject leak = new GameObject("Water Leak");
            leak.transform.SetParent(root.transform, false);
            leak.transform.localPosition = new Vector3(0.08f, -0.12f, 0f);
            Color leakColor = new Color(0.2f, 0.78f, 0.98f, 0.72f);
            float floorDistance = FloorY - center.y + 0.52f;
            StageGun.AddLine(leak.transform, "Leak A", new[]
            {
                new Vector2(-0.1f, 0f), new Vector2(0.04f, floorDistance * 0.34f),
                new Vector2(-0.06f, floorDistance * 0.7f), new Vector2(0.08f, floorDistance)
            }, 0.14f, leakColor, 6);
            StageGun.AddLine(leak.transform, "Leak B", new[]
            {
                new Vector2(0.04f, -0.05f), new Vector2(-0.1f, floorDistance * 0.42f),
                new Vector2(0.12f, floorDistance * 0.82f), new Vector2(0.02f, floorDistance)
            }, 0.075f, new Color(0.68f, 0.95f, 1f, 0.58f), 7);

            holes.Add(new HoleVisual { Center = center, Core = core, Rim = rim, Leak = leak.transform });
        }

        private void CreateBoxStation()
        {
            if (factory == null) factory = Object.FindFirstObjectByType<StageObjectFactory>();
            if (factory == null) return;

            Vector2 dropperPosition = new Vector2(roomWidth * 0.5f - 3.1f, 3.55f);
            StageObjectData dropperData = StageObjectFactory.CreateDefaultData(StageObjectType.BoxDropper, dropperPosition);
            dropperData.objectId = "6-3_box_generator_round_" + round;
            dropperData.size = new Vector2(2.6f, 1.8f);
            dropperData.actionStrength = 10f;
            dropperData.spawnPattern = 1;
            dropperData.spawnBoxSize = 0.9f;
            GameObject dropperObject = factory.Create(dropperData, arenaRoot);
            boxDropper = dropperObject != null ? dropperObject.GetComponent<StageBoxDropper>() : null;
            boxDropper?.ConfigureManualDispense();

            StageObjectData buttonData = StageObjectFactory.CreateDefaultData(
                StageObjectType.Button,
                new Vector2(roomWidth * 0.5f - 6.4f, FloorY + 0.52f));
            buttonData.objectId = "6-3_box_button_round_" + round;
            buttonData.size = new Vector2(1.7f, 0.8f);
            GameObject buttonObject = factory.Create(buttonData, arenaRoot);
            if (buttonObject == null) return;
            boxButton = buttonObject.GetComponent<Collider2D>();
            buttonCap = buttonObject.transform.Find("Button Cap");
            buttonCapScale = buttonCap != null ? buttonCap.localScale : Vector3.one;

            GameObject glowObject = new GameObject("Button Ready Glow");
            glowObject.transform.SetParent(buttonObject.transform, false);
            glowObject.transform.localPosition = new Vector3(0f, 0.08f, 0.03f);
            glowObject.transform.localScale = new Vector3(1.35f, 0.55f, 1f);
            buttonGlow = glowObject.AddComponent<SpriteRenderer>();
            buttonGlow.sprite = DoodleRuntimeAssets.CircleSprite;
            buttonGlow.sortingOrder = 26;

            GameObject previewObject = new GameObject("Next Box Preview");
            previewObject.transform.SetParent(dropperObject.transform, false);
            previewObject.transform.localPosition = new Vector3(0f, -0.02f, -0.03f);
            SpriteRenderer previewRenderer = previewObject.AddComponent<SpriteRenderer>();
            previewRenderer.sprite = DoodleRuntimeAssets.SquareSprite;
            previewRenderer.color = new Color(0.94f, 0.52f, 0.15f, 0.92f);
            previewRenderer.sortingOrder = 33;
            boxPreview = previewObject.transform;
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
                {
                    Collider2D collider = colliders[c];
                    if (collider != null && collider.enabled && !collider.isTrigger
                        && collider.bounds.Intersects(bounds)) return true;
                }
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
            if (buttonGlow != null)
            {
                buttonGlow.color = buttonPressed
                    ? new Color(0.18f, 0.95f, 0.28f, 0.72f)
                    : new Color(0.95f, 0.16f, 0.1f, 0.16f);
            }
            if (boxPreview != null)
            {
                Vector2 size = BoxSizes[Mathf.Clamp(previewIndex, 0, BoxSizes.Length - 1)];
                float fit = Mathf.Min(0.78f / size.x, 0.78f / size.y);
                boxPreview.localScale = new Vector3(size.x * fit, size.y * fit, 1f);
                boxPreview.localRotation = Quaternion.Euler(0f, 0f, previewIndex % 2 == 0 ? -2f : 2f);
            }
        }

        private void EvaluateHoles()
        {
            int nextMask = 0;
            for (int i = 0; i < holes.Count; i++)
            {
                if (IsHoleCoveredByPlayer(holes[i].Center)) nextMask |= 1 << i;
            }
            sealedMask = nextMask;
            ApplyHoleVisuals();
            EmitLeakingWater();

            int allMask = holes.Count >= 31 ? -1 : (1 << holes.Count) - 1;
            if (holes.Count > 0 && (sealedMask & allMask) == allMask)
            {
                allSealedTime += Time.unscaledDeltaTime;
                if (allSealedTime >= ClearHoldSeconds) ClearRound();
            }
            else
            {
                allSealedTime = 0f;
            }
        }

        private static bool IsHoleCoveredByPlayer(Vector2 center)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                Collider2D[] colliders = players[i].GetComponentsInChildren<Collider2D>(false);
                for (int c = 0; c < colliders.Length; c++)
                {
                    Collider2D collider = colliders[c];
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    if (Vector2.Distance(collider.ClosestPoint(center), center) <= 0.1f) return true;
                }
            }
            return false;
        }

        private void ApplyHoleVisuals()
        {
            for (int i = 0; i < holes.Count; i++)
            {
                HoleVisual hole = holes[i];
                bool isSealed = (sealedMask & (1 << i)) != 0;
                hole.Sealed = isSealed;
                if (hole.Leak != null) hole.Leak.gameObject.SetActive(!isSealed);
                if (hole.Core != null)
                    hole.Core.color = isSealed
                        ? new Color(0.18f, 0.86f, 0.46f, 0.8f)
                        : new Color(0.025f, 0.1f, 0.16f, 0.94f);
                if (hole.Rim != null)
                    hole.Rim.color = isSealed
                        ? new Color(0.08f, 0.58f, 0.28f, 0.86f)
                        : new Color(0.08f, 0.3f, 0.42f, 0.92f);
            }
        }

        private void AnimateAquarium()
        {
            RefreshAccumulatedWater();
            for (int i = 0; i < holes.Count; i++)
            {
                HoleVisual hole = holes[i];
                if (hole.Leak == null || hole.Sealed) continue;
                float pulse = 0.92f + Mathf.Sin(Time.unscaledTime * 7f + i * 1.3f) * 0.1f;
                hole.Leak.localScale = new Vector3(1f / pulse, 1f, 1f);
            }
        }

        private void EmitLeakingWater()
        {
            if (phase != SealPhase.Active || arenaRoot == null || holes.Count == 0) return;

            int openCount = holes.Count - CountBits(sealedMask);
            if (openCount <= 0) return;
            accumulatedWaterDepth = Mathf.Min(
                2.35f,
                accumulatedWaterDepth + openCount * Time.unscaledDeltaTime * 0.0042f);

            if (waterDropCount >= MaximumWaterDrops || Time.unscaledTime < nextWaterDropAt) return;
            nextWaterDropAt = Time.unscaledTime + Mathf.Max(0.065f, 0.16f / openCount);

            for (int attempt = 0; attempt < holes.Count; attempt++)
            {
                int index = nextLeakingHole++ % holes.Count;
                if ((sealedMask & (1 << index)) != 0) continue;
                CreateWaterDrop(holes[index].Center, waterDropCount++);
                break;
            }
        }

        private void CreateWaterDrop(Vector2 source, int index)
        {
            GameObject drop = new GameObject("Leaked Water Drop " + index);
            drop.transform.SetParent(arenaRoot, false);
            drop.transform.position = source + new Vector2(Random.Range(-0.1f, 0.1f), -0.34f);
            float size = Random.Range(0.24f, 0.38f);
            drop.transform.localScale = new Vector3(size * 0.72f, size * 1.45f, 1f);
            drop.layer = 31;

            SpriteRenderer renderer = drop.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = new Color(0.12f, 0.7f, 0.96f, Random.Range(0.4f, 0.58f));
            renderer.sortingOrder = 8;

            Rigidbody2D body = drop.AddComponent<Rigidbody2D>();
            body.mass = 0.00035f;
            body.gravityScale = 0.38f;
            body.linearDamping = 0.1f;
            body.angularDamping = 0.12f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.9f, -0.45f));
            drop.AddComponent<AquariumWaterBlobVisual>().Configure(size, FloorY + 0.42f, this);
        }

        internal void NotifyWaterDropDestroyed()
        {
            waterDropCount = Mathf.Max(0, waterDropCount - 1);
        }

        private void RefreshAccumulatedWater()
        {
            if (accumulatedWaterRenderer == null) return;
            accumulatedWaterRenderer.enabled = accumulatedWaterDepth > 0.005f;
            float depth = Mathf.Max(0.025f, accumulatedWaterDepth);
            accumulatedWaterRenderer.transform.localPosition = new Vector3(
                0f, FloorY + 0.375f + depth * 0.5f, 0f);
            accumulatedWaterRenderer.transform.localScale = new Vector3(
                Mathf.Max(1f, roomWidth - 0.78f), depth, 1f);
            accumulatedWaterRenderer.color = new Color(
                0.06f, 0.55f, 0.84f, Mathf.Lerp(0.12f, 0.55f, accumulatedWaterDepth / 2.35f));

            if (accumulatedWaterSurface == null) return;
            accumulatedWaterSurface.enabled = accumulatedWaterDepth > 0.005f;
            float surfaceY = FloorY + 0.375f + accumulatedWaterDepth;
            float width = Mathf.Max(1f, roomWidth - 0.9f);
            for (int i = 0; i < accumulatedWaterSurface.positionCount; i++)
            {
                float t = i / (float)(accumulatedWaterSurface.positionCount - 1);
                float x = Mathf.Lerp(-width * 0.5f, width * 0.5f, t);
                float wave = Mathf.Sin(Time.unscaledTime * 2.2f + t * Mathf.PI * 6f) * 0.055f;
                accumulatedWaterSurface.SetPosition(i, new Vector3(x, surfaceY + wave, 0f));
            }
        }

        private void ClearRound()
        {
            if (phase != SealPhase.Active) return;
            GameSfx.Play(SfxId.GoalReached);
            if (round >= 3)
            {
                phase = SealPhase.Complete;
                BroadcastState(true);
                stageManager.ClearStage();
                return;
            }
            phase = SealPhase.RoundClear;
            transitionRemaining = 1.8f;
            BroadcastState(true);
        }

        private void FailRound()
        {
            if (phase != SealPhase.Active) return;
            phase = SealPhase.Failed;
            transitionRemaining = 2.2f;
            GameSfx.Play(SfxId.PlayerDeath, 0.72f);
            BroadcastState(true);
        }

        private void CaptureCamera()
        {
            if (cameraCaptured) return;
            if (gameCamera == null) gameCamera = Camera.main;
            if (cameraFollow == null && gameCamera != null)
                cameraFollow = gameCamera.GetComponent<CameraFollow2D>();
            if (gameCamera == null) return;

            cameraCaptured = true;
            previousCameraPosition = gameCamera.transform.position;
            previousCameraSize = gameCamera.orthographicSize;
            previousCameraFollowEnabled = cameraFollow != null && cameraFollow.enabled;
        }

        private void ConfigureAquariumCamera()
        {
            if (!cameraCaptured) CaptureCamera();
            if (gameCamera == null) return;
            if (cameraFollow != null) cameraFollow.enabled = false;

            float aspect = Mathf.Max(0.1f, gameCamera.aspect);
            float sizeForWidth = (roomWidth * 0.5f + 0.45f) / aspect;
            gameCamera.transform.position = new Vector3(0f, 0.5f, -10f);
            gameCamera.orthographicSize = Mathf.Max(7.75f, sizeForWidth);
        }

        private void RestoreCamera()
        {
            if (!cameraCaptured || gameCamera == null) return;
            gameCamera.transform.position = previousCameraPosition;
            gameCamera.orthographicSize = previousCameraSize;
            if (cameraFollow != null) cameraFollow.enabled = previousCameraFollowEnabled;
            cameraCaptured = false;
        }

        private void ApplyControls()
        {
            bool enabled = phase == SealPhase.Active && !stageManager.IsDrawingMode;
            PlayerController2D local = stageManager.ActivePlayerTransform != null
                ? stageManager.ActivePlayerTransform.GetComponent<PlayerController2D>()
                : null;
            local?.SetControlsEnabled(enabled);
            if (!IsOnline) stageManager.RemotePlayerController?.SetControlsEnabled(enabled);
        }

        private IEnumerator PositionPlayersAfterStageSetup()
        {
            yield return null;
            if (arenaRoot == null || stageManager == null || stageManager.CurrentStageId != StageId) yield break;

            PlayerController2D[] activePlayers = Object.FindObjectsByType<PlayerController2D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            int total = GetPlayerCount();
            for (int i = 0; i < activePlayers.Length; i++)
            {
                PlayerController2D player = activePlayers[i];
                if (IsOnline && player.transform != stageManager.ActivePlayerTransform) continue;
                int slot = ResolvePlayerSlot(player, i, total);
                float t = (slot + 1f) / (total + 1f);
                Vector2 position = new Vector2(Mathf.Lerp(-roomWidth * 0.32f, roomWidth * 0.32f, t), FloorY + 1.35f);
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.simulated = true;
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                else player.transform.position = position;
                player.ResetMotion();
                AlignPlayerToFloor(player);
                stageManager.RecordAssignedPlayerStart(player, player.transform.position);
            }
        }

        private static void AlignPlayerToFloor(PlayerController2D player)
        {
            if (player == null) return;
            Physics2D.SyncTransforms();
            Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(false);
            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                if (!found) bounds = collider.bounds;
                else bounds.Encapsulate(collider.bounds);
                found = true;
            }
            if (!found) return;

            float correction = FloorY + 0.75f * 0.5f + 0.06f - bounds.min.y;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 corrected = (Vector2)player.transform.position + Vector2.up * correction;
            if (body != null) body.position = corrected;
            else player.transform.position = corrected;
            Physics2D.SyncTransforms();
        }

        private int GetPlayerCount()
        {
            return Mathf.Clamp(stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1, 1, 4);
        }

        private int ResolvePlayerSlot(PlayerController2D player, int fallback, int total)
        {
            if (!IsOnline || onlineManager?.CurrentLobby?.Players == null)
                return Mathf.Clamp(fallback, 0, total - 1);
            string id = stageManager.GetOnlinePlayerId(player);
            OnlinePlayerInfo[] lobbyPlayers = onlineManager.CurrentLobby.Players;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].PlayerId == id)
                    return Mathf.Clamp(i, 0, total - 1);
            return Mathf.Clamp(fallback, 0, total - 1);
        }

        private void RefreshMonitor()
        {
            if (monitorMain == null || monitorSub == null) return;
            switch (phase)
            {
                case SealPhase.Active:
                    monitorMain.text = LocalizationManager.Format("aquarium_seal_monitor", round, remaining);
                    int count = CountBits(sealedMask);
                    monitorSub.text = count > 0
                        ? LocalizationManager.Format("aquarium_seal_progress", count, holes.Count)
                        : LocalizationManager.T("aquarium_seal_box_hint");
                    break;
                case SealPhase.RoundClear:
                    monitorMain.text = LocalizationManager.Format("aquarium_seal_round_clear", round);
                    monitorSub.text = LocalizationManager.T("aquarium_seal_box_hint");
                    break;
                case SealPhase.Failed:
                    monitorMain.text = LocalizationManager.T("aquarium_seal_timeout");
                    monitorSub.text = LocalizationManager.T("aquarium_seal_box_hint");
                    break;
            }
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }

        private void BroadcastState(bool force = false)
        {
            if (!IsOnline || !HasAuthority || onlineManager == null
                || !force && Time.unscaledTime < nextBroadcastAt) return;
            nextBroadcastAt = Time.unscaledTime + 0.1f;
            NetworkState state = new NetworkState
            {
                Sequence = ++sequence,
                Round = round,
                Attempt = roundAttempt,
                Phase = (int)phase,
                Remaining = remaining,
                SealedMask = sealedMask,
                PreviewIndex = previewIndex,
                ButtonPressed = buttonPressed,
                WaterDepth = accumulatedWaterDepth
            };
            onlineManager.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = StageId,
                Kind = StateKind,
                Json = JsonUtility.ToJson(state)
            });
        }

        private void HandleNetworkState(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != StageId || data.Kind != StateKind
                || HasAuthority || !IsHost(data.PlayerId)) return;
            NetworkState state = JsonUtility.FromJson<NetworkState>(data.Json);
            if (state == null || state.Sequence <= receivedSequence) return;
            receivedSequence = state.Sequence;
            SealPhase incoming = (SealPhase)state.Phase;
            if (state.Round != round || state.Attempt != roundAttempt)
                BeginRound(state.Round, state.Attempt);
            phase = incoming;
            remaining = state.Remaining;
            sealedMask = state.SealedMask;
            previewIndex = Mathf.Clamp(state.PreviewIndex, 0, BoxSizes.Length - 1);
            buttonPressed = state.ButtonPressed;
            accumulatedWaterDepth = Mathf.Max(accumulatedWaterDepth, state.WaterDepth);
            ApplyHoleVisuals();
            RefreshBoxStationVisual();
            RefreshMonitor();
        }

        private bool IsHost(string playerId)
        {
            OnlinePlayerInfo[] lobbyPlayers = onlineManager?.CurrentLobby?.Players;
            if (lobbyPlayers == null) return false;
            for (int i = 0; i < lobbyPlayers.Length; i++)
                if (lobbyPlayers[i] != null && lobbyPlayers[i].IsHost
                    && lobbyPlayers[i].PlayerId == playerId) return true;
            return false;
        }
    }

    public sealed class AquariumFishSwimmer : MonoBehaviour
    {
        private float left;
        private float right;
        private float bottom;
        private float top;
        private float y;
        private float speed;
        private float phase;
        private bool reverse;
        private float halfWidth = 0.5f;
        private float halfHeight = 0.4f;

        public void Configure(
            float leftX,
            float rightX,
            float bottomY,
            float topY,
            float height,
            float moveSpeed,
            float startPhase,
            bool moveReverse)
        {
            left = leftX;
            right = rightX;
            bottom = bottomY;
            top = topY;
            y = height;
            speed = moveSpeed;
            phase = startPhase;
            reverse = moveReverse;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                Vector3 center = transform.position;
                halfWidth = Mathf.Max(0.1f,
                    Mathf.Max(Mathf.Abs(bounds.min.x - center.x), Mathf.Abs(bounds.max.x - center.x)));
                halfHeight = Mathf.Max(0.1f,
                    Mathf.Max(Mathf.Abs(bounds.min.y - center.y), Mathf.Abs(bounds.max.y - center.y)));
            }
        }

        private void Update()
        {
            float centerLeft = left + halfWidth + 0.1f;
            float centerRight = right - halfWidth - 0.1f;
            float width = Mathf.Max(0.1f, centerRight - centerLeft);
            float rawDistance = Time.unscaledTime * speed + phase * width;
            float travelDistance = Mathf.PingPong(rawDistance, width);
            bool movingRight = Mathf.Repeat(rawDistance, width * 2f) < width;
            if (reverse)
            {
                travelDistance = width - travelDistance;
                movingRight = !movingRight;
            }

            float safeY = Mathf.Clamp(y, bottom + halfHeight + 0.1f, top - halfHeight - 0.1f);
            float bob = Mathf.Sin(Time.unscaledTime * 1.7f + phase * 11f) * 0.18f;
            safeY = Mathf.Clamp(safeY + bob, bottom + halfHeight + 0.08f, top - halfHeight - 0.08f);
            transform.localPosition = new Vector3(
                centerLeft + travelDistance,
                safeY,
                0f);
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (movingRight ? 1f : -1f);
            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(Time.unscaledTime * 1.3f + phase * 7f) * 2.2f);
        }
    }

    public sealed class AquariumBubbleMover : MonoBehaviour
    {
        private float bottom;
        private float top;
        private float speed;
        private float phase;
        private float x;

        public void Configure(float bottomY, float topY, float riseSpeed, float startPhase)
        {
            bottom = bottomY;
            top = topY;
            speed = riseSpeed;
            phase = startPhase;
            x = transform.localPosition.x;
        }

        private void Update()
        {
            float height = Mathf.Max(0.1f, top - bottom);
            float travel = Mathf.Repeat(Time.unscaledTime * speed + phase, height);
            transform.localPosition = new Vector3(
                x + Mathf.Sin(Time.unscaledTime * 0.8f + phase * 3f) * 0.12f,
                bottom + travel,
                0f);
        }
    }

    public sealed class AquariumWaterBlobVisual : MonoBehaviour
    {
        private StageAquariumSealController owner;
        private SpriteRenderer blobRenderer;
        private TrailRenderer trail;
        private Rigidbody2D blobBody;
        private Color initialBlobColor;
        private Color initialTrailStart;
        private Color initialTrailEnd;
        private float spawnedAt;
        private float landedAt = -1f;
        private float landingY;
        private float lifeAfterLanding;
        private bool notified;

        public void Configure(float size, float configuredLandingY, StageAquariumSealController configuredOwner)
        {
            owner = configuredOwner;
            spawnedAt = Time.unscaledTime;
            landingY = configuredLandingY;
            lifeAfterLanding = Random.Range(1.5f, 2.4f);
            blobRenderer = GetComponent<SpriteRenderer>();
            blobBody = GetComponent<Rigidbody2D>();
            if (blobRenderer != null) initialBlobColor = blobRenderer.color;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.32f;
            trail.minVertexDistance = 0.035f;
            trail.startWidth = size * 0.62f;
            trail.endWidth = size * 0.08f;
            trail.numCapVertices = 4;
            trail.numCornerVertices = 3;
            trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            trail.startColor = new Color(0.18f, 0.76f, 0.98f, 0.42f);
            trail.endColor = new Color(0.66f, 0.94f, 1f, 0.08f);
            initialTrailStart = trail.startColor;
            initialTrailEnd = trail.endColor;
            trail.sortingOrder = 7;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (landedAt < 0f)
            {
                if (transform.position.y <= landingY)
                {
                    Vector3 position = transform.position;
                    position.y = landingY;
                    transform.position = position;
                    if (blobBody != null) blobBody.simulated = false;
                    landedAt = now;
                    return;
                }

                // Also clean up a drop that somehow missed every floor.
                if (now - spawnedAt >= 8f) RemoveDrop();
                return;
            }

            float age = now - landedAt;
            float fade = Mathf.Clamp01((lifeAfterLanding - age) / 0.65f);
            if (blobRenderer != null)
            {
                Color color = initialBlobColor;
                color.a *= fade;
                blobRenderer.color = color;
            }
            if (trail != null)
            {
                Color start = initialTrailStart;
                Color end = initialTrailEnd;
                start.a *= fade;
                end.a *= fade;
                trail.startColor = start;
                trail.endColor = end;
            }
            if (fade <= 0f) RemoveDrop();
        }

        private void RemoveDrop()
        {
            if (blobBody != null) blobBody.simulated = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (notified) return;
            notified = true;
            owner?.NotifyWaterDropDestroyed();
        }
    }
}
