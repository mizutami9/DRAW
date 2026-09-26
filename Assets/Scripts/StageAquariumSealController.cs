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
        private const float MinimumGroupedHoleDistance = 0.78f;
        private const float MaximumGroupedHoleDistance = 1.08f;
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

        private enum AquariumSolidVisual
        {
            Sand,
            Rock
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
        private StageCountdownPresenter countdownPresenter;
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
        private float roundReadyAt;
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
            countdownPresenter = new StageCountdownPresenter(uiManager);
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
            countdownPresenter?.Hide();
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
            float countdownRemaining = roundReadyAt - Time.unscaledTime;
            if (phase == SealPhase.Failed)
            {
                countdownPresenter?.ShowGameOver();
            }
            else if (phase == SealPhase.Active && countdownRemaining > 0f)
            {
                countdownPresenter?.Show(countdownRemaining);
                RefreshMonitor();
                return;
            }
            else countdownPresenter?.Hide();
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
            roundReadyAt = Time.unscaledTime + 4f;
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
                new Color(0.42f, 0.79f, 0.9f, 0.16f), -58);

            // Layered translucent bands and moving-looking pencil caustics keep
            // the tank readable as water instead of one flat blue rectangle.
            Color[] depthColors =
            {
                new Color(0.64f, 0.9f, 0.96f, 0.055f),
                new Color(0.45f, 0.81f, 0.92f, 0.072f),
                new Color(0.3f, 0.71f, 0.86f, 0.09f),
                new Color(0.18f, 0.57f, 0.76f, 0.11f)
            };
            float bandHeight = waterSize.y / depthColors.Length;
            for (int i = 0; i < depthColors.Length; i++)
            {
                float y = 0.45f + waterSize.y * 0.5f - bandHeight * (i + 0.5f);
                StageEscortController.AddFilledRect(arenaRoot, "Water Depth Band " + i,
                    new Vector2(0f, y), new Vector2(waterSize.x, bandHeight + 0.08f), depthColors[i], -57 + i);
            }

            CreateAquariumDistantScenery(waterSize);

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

            Color waterLine = new Color(0.2f, 0.6f, 0.76f, 0.16f);
            for (int i = 0; i < 6; i++)
            {
                float y = -4.75f + i * 1.82f;
                float direction = (i & 1) == 0 ? -1f : 1f;
                float center = direction * roomWidth * 0.12f + Mathf.Sin(i * 1.73f) * 0.55f;
                float halfSpan = roomWidth * (0.18f + (i % 3) * 0.025f);
                StageGun.AddLine(arenaRoot, "Water Pencil Line", new[]
                {
                    new Vector2(center - halfSpan, y),
                    new Vector2(center - halfSpan * 0.28f, y + 0.08f),
                    new Vector2(center + halfSpan * 0.34f, y - 0.055f),
                    new Vector2(center + halfSpan, y + 0.035f)
                }, 0.03f, waterLine, -46);
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

            for (int i = 0; i < 8; i++)
            {
                float rayCenter = i % 3 == 0
                    ? -roomWidth * 0.28f
                    : i % 3 == 1 ? roomWidth * 0.02f : roomWidth * 0.29f;
                float x = rayCenter + Mathf.Sin(i * 2.13f) * 1.25f;
                float y = -4.55f + Mathf.Repeat(i * 2.31f, 9.4f);
                StageGun.AddLine(arenaRoot, "Underwater Light Caustic", new[]
                {
                    new Vector2(x - 0.75f, y), new Vector2(x, y + 0.18f), new Vector2(x + 0.72f, y - 0.03f)
                }, 0.032f, new Color(0.78f, 0.98f, 1f, 0.18f), -42);
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
            int fishCount = 7 + playerCount;
            for (int i = 0; i < fishCount; i++)
            {
                int kind = i % 6;
                bool isLargeFish = i == 0;
                GameObject fish = new GameObject(isLargeFish
                    ? "Large Aquarium Fish"
                    : "Aquarium Fish " + kind + " " + i);
                fish.transform.SetParent(arenaRoot, false);
                CreateFishVisual(fish.transform, fishSprite, kind, fishColors[i % fishColors.Length]);
                if (isLargeFish) fish.transform.localScale = Vector3.one * 2.7f;
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

            CreateAquariumBubbleColumns();

            CreateAquariumFloorPlants();
        }

        private void CreateAquariumDistantScenery(Vector2 waterSize)
        {
            float halfWidth = waterSize.x * 0.5f;
            float surfaceY = 6.52f;
            float lowerY = FloorY + 0.7f;
            float[] rayCenters =
            {
                -roomWidth * 0.28f,
                roomWidth * 0.02f,
                roomWidth * 0.29f
            };

            for (int ray = 0; ray < rayCenters.Length; ray++)
            {
                float lean = (ray - 1) * 0.62f;
                float topHalfWidth = 0.5f + ray * 0.12f;
                float bottomHalfWidth = 1.75f + (ray % 2) * 0.45f;
                float bottomY = lowerY + 0.3f + (ray % 2) * 1.05f;
                Vector2 topLeft = new Vector2(rayCenters[ray] - topHalfWidth, surfaceY);
                Vector2 topRight = new Vector2(rayCenters[ray] + topHalfWidth, surfaceY - 0.04f);
                Vector2 bottomLeft = new Vector2(
                    rayCenters[ray] + lean - bottomHalfWidth,
                    bottomY + 0.12f);
                Vector2 bottomRight = new Vector2(
                    rayCenters[ray] + lean + bottomHalfWidth,
                    bottomY - 0.08f);
                AddAquariumFilledPolygon(
                    arenaRoot,
                    "Distant Hand Drawn Light Shaft " + ray,
                    new[] { topLeft, topRight, bottomRight, bottomLeft },
                    new Color(0.84f, 0.98f, 1f, 0.038f),
                    -51);

                Vector2 innerTopLeft = Vector2.Lerp(topLeft, topRight, 0.28f);
                Vector2 innerTopRight = Vector2.Lerp(topLeft, topRight, 0.65f);
                Vector2 innerBottomLeft = Vector2.Lerp(bottomLeft, bottomRight, 0.33f);
                Vector2 innerBottomRight = Vector2.Lerp(bottomLeft, bottomRight, 0.61f);
                AddAquariumFilledPolygon(
                    arenaRoot,
                    "Distant Light Shaft Crayon Wash " + ray,
                    new[] { innerTopLeft, innerTopRight, innerBottomRight, innerBottomLeft },
                    new Color(0.92f, 1f, 1f, 0.025f),
                    -50);

                Color edge = new Color(0.76f, 0.96f, 1f, 0.09f);
                StageGun.AddLine(arenaRoot, "Light Shaft Left Pencil " + ray, new[]
                {
                    topLeft,
                    Vector2.Lerp(topLeft, bottomLeft, 0.47f) + new Vector2(0.08f, 0.03f),
                    bottomLeft
                }, 0.026f, edge, -50);
                StageGun.AddLine(arenaRoot, "Light Shaft Right Pencil " + ray, new[]
                {
                    topRight,
                    Vector2.Lerp(topRight, bottomRight, 0.54f) + new Vector2(-0.06f, -0.025f),
                    bottomRight
                }, 0.022f, edge, -50);
            }

            Vector2[] leftRock =
            {
                new Vector2(-halfWidth, FloorY + 0.34f),
                new Vector2(-halfWidth, FloorY + 2.45f),
                new Vector2(-halfWidth + 1.15f, FloorY + 1.72f),
                new Vector2(-halfWidth + 2.1f, FloorY + 2.2f),
                new Vector2(-halfWidth + 3.55f, FloorY + 0.34f)
            };
            Vector2[] rightRock =
            {
                new Vector2(halfWidth - 3.75f, FloorY + 0.34f),
                new Vector2(halfWidth - 2.45f, FloorY + 1.8f),
                new Vector2(halfWidth - 1.25f, FloorY + 1.42f),
                new Vector2(halfWidth, FloorY + 2.65f),
                new Vector2(halfWidth, FloorY + 0.34f)
            };
            Color rockFill = new Color(0.11f, 0.32f, 0.37f, 0.13f);
            Color rockInk = new Color(0.08f, 0.27f, 0.32f, 0.2f);
            AddAquariumFilledPolygon(arenaRoot, "Distant Left Rock Shadow", leftRock, rockFill, -53);
            AddAquariumFilledPolygon(arenaRoot, "Distant Right Rock Shadow", rightRock, rockFill, -53);
            StageGun.AddLine(arenaRoot, "Distant Left Rock Pencil",
                CloseAquariumPolygon(leftRock), 0.04f, rockInk, -52);
            StageGun.AddLine(arenaRoot, "Distant Right Rock Pencil",
                CloseAquariumPolygon(rightRock), 0.04f, rockInk, -52);

            DrawAquariumDistantCoralShadow(
                new Vector2(-halfWidth + 2.15f, FloorY + 0.34f),
                1f,
                0);
            DrawAquariumDistantCoralShadow(
                new Vector2(halfWidth - 2.35f, FloorY + 0.34f),
                -1f,
                1);
            CreateAquariumDistantFishShadows();
        }

        private void CreateAquariumDistantFishShadows()
        {
            Sprite fishSprite = Resources.Load<Sprite>("StageObjects/NicoDraw/fish");
            if (fishSprite == null)
            {
                return;
            }

            int shadowCount = Mathf.Clamp(Mathf.RoundToInt(roomWidth / 8f), 4, 6);
            for (int shadow = 0; shadow < shadowCount; shadow++)
            {
                GameObject fish = new GameObject("Distant Aquarium Fish Shadow " + shadow);
                fish.transform.SetParent(arenaRoot, false);
                float size = 0.58f + (shadow % 3) * 0.16f;
                Color color = new Color(
                    0.07f + (shadow % 2) * 0.025f,
                    0.25f + (shadow % 3) * 0.018f,
                    0.32f + (shadow % 2) * 0.025f,
                    0.13f + (shadow % 3) * 0.018f);
                CreateFishSprite(
                    fish.transform,
                    "Distant Fish Silhouette",
                    fishSprite,
                    Vector2.zero,
                    new Vector2(size * 1.28f, size),
                    color,
                    shadow % 3 == 0 ? -48 : -49);

                AquariumFishSwimmer swimmer = fish.AddComponent<AquariumFishSwimmer>();
                swimmer.Configure(
                    -roomWidth * 0.5f + 0.7f,
                    roomWidth * 0.5f - 0.7f,
                    FloorY + 1.1f,
                    6.15f,
                    -3.9f + Mathf.Repeat(shadow * 2.47f, 8.8f),
                    0.19f + (shadow % 3) * 0.055f,
                    shadow * 0.173f,
                    shadow % 2 != 0);
            }
        }

        private void CreateAquariumBubbleColumns()
        {
            float[] sourceFractions = { -0.39f, -0.23f, 0.08f, 0.28f, 0.4f };
            int bubbleIndex = 0;
            for (int column = 0; column < sourceFractions.Length; column++)
            {
                float sourceX = roomWidth * sourceFractions[column];
                int count = 4 + column % 3;
                bool distant = (column & 1) == 0;
                int outerOrder = distant ? -47 : -31;
                int shineOrder = distant ? -46 : -30;
                for (int bubble = 0; bubble < count; bubble++)
                {
                    GameObject bubbleObject = new GameObject("Aquarium Bubble Column " + column + " Bubble " + bubble);
                    bubbleObject.transform.SetParent(arenaRoot, false);
                    float xOffset = Mathf.Sin(column * 2.1f + bubble * 1.73f) * 0.13f;
                    bubbleObject.transform.localPosition = new Vector3(
                        sourceX + xOffset,
                        FloorY + 0.62f,
                        0f);
                    float normalizedHeight = bubble / (float)Mathf.Max(1, count - 1);
                    float size = (0.08f + normalizedHeight * 0.1f) * (1f + (bubbleIndex % 3) * 0.12f);
                    bubbleObject.transform.localScale = Vector3.one * size;

                    SpriteRenderer renderer = bubbleObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = DoodleRuntimeAssets.CircleSprite;
                    renderer.color = distant
                        ? new Color(0.7f, 0.93f, 0.98f, 0.22f)
                        : new Color(0.76f, 0.97f, 1f, 0.42f);
                    renderer.sortingOrder = outerOrder;

                    GameObject shine = new GameObject("Bubble Crayon Shine");
                    shine.transform.SetParent(bubbleObject.transform, false);
                    shine.transform.localPosition = new Vector3(-0.22f, 0.24f, -0.01f);
                    shine.transform.localScale = Vector3.one * 0.23f;
                    SpriteRenderer shineRenderer = shine.AddComponent<SpriteRenderer>();
                    shineRenderer.sprite = DoodleRuntimeAssets.CircleSprite;
                    shineRenderer.color = new Color(0.95f, 1f, 1f, distant ? 0.34f : 0.62f);
                    shineRenderer.sortingOrder = shineOrder;

                    AquariumBubbleMover mover = bubbleObject.AddComponent<AquariumBubbleMover>();
                    mover.Configure(
                        FloorY + 0.62f,
                        6.5f,
                        0.2f + (column % 3) * 0.055f + bubble * 0.012f,
                        column * 1.83f + bubble * (0.88f + column * 0.07f));
                    bubbleIndex++;
                }
            }
        }

        private void DrawAquariumDistantCoralShadow(Vector2 root, float direction, int variant)
        {
            Color coral = variant == 0
                ? new Color(0.12f, 0.43f, 0.39f, 0.18f)
                : new Color(0.16f, 0.37f, 0.45f, 0.16f);
            for (int branch = 0; branch < 4; branch++)
            {
                float offset = (branch - 1.5f) * 0.24f;
                float height = 0.72f + branch % 3 * 0.24f;
                Vector2 basePoint = root + new Vector2(offset, 0f);
                Vector2 fork = basePoint + new Vector2(direction * (0.08f + branch * 0.025f), height * 0.57f);
                Vector2 tip = basePoint + new Vector2(direction * (0.16f + branch * 0.035f), height);
                StageGun.AddLine(arenaRoot, "Distant Coral Shadow", new[] { basePoint, fork, tip },
                    0.075f, coral, -52);
                StageGun.AddLine(arenaRoot, "Distant Coral Shadow Fork", new[]
                {
                    fork,
                    fork + new Vector2(-direction * (0.2f + branch * 0.025f), 0.28f)
                }, 0.055f, coral, -52);
            }
        }

        private static void AddAquariumFilledPolygon(
            Transform parent,
            string name,
            Vector2[] points,
            Color color,
            int sortingOrder)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            Vector3[] vertices = new Vector3[points.Length];
            Color[] colors = new Color[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                vertices[i] = points[i];
                colors[i] = color;
            }

            int[] triangles = new int[(points.Length - 2) * 3];
            for (int triangle = 0; triangle < points.Length - 2; triangle++)
            {
                triangles[triangle * 3] = 0;
                triangles[triangle * 3 + 1] = triangle + 1;
                triangles[triangle * 3 + 2] = triangle + 2;
            }

            Mesh mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };
            mesh.RecalculateBounds();

            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            renderer.sortingOrder = sortingOrder;
        }

        private static Vector2[] CloseAquariumPolygon(Vector2[] points)
        {
            Vector2[] closed = new Vector2[points.Length + 1];
            for (int i = 0; i < points.Length; i++) closed[i] = points[i];
            closed[closed.Length - 1] = points[0];
            return closed;
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

        private void CreateAquariumFloorPlants()
        {
            float left = -roomWidth * 0.5f + 1.45f;
            float right = roomWidth * 0.5f - 1.45f;
            float buttonX = roomWidth * 0.5f - 6.4f;
            float cursor = left;
            int created = 0;
            int targetCount = Mathf.Clamp(Mathf.RoundToInt(roomWidth / 4.6f), 6, 9);
            uint state = unchecked((uint)GetAquariumVisualSeed("6-3-floor-plants"));

            for (int attempt = 0; attempt < 32 && created < targetCount; attempt++)
            {
                cursor += AquariumVisualRange(ref state, 2.35f, 4.85f);
                if (cursor > right)
                {
                    break;
                }

                float x = cursor + AquariumVisualRange(ref state, -0.38f, 0.38f);
                if (Mathf.Abs(x) < 3.25f || Mathf.Abs(x - buttonX) < 2.65f)
                {
                    continue;
                }

                GameObject group = new GameObject("Aquarium Floor Plant Group " + created);
                group.transform.SetParent(arenaRoot, false);
                group.transform.localPosition = new Vector3(x, FloorY + 0.34f, 0f);
                float scale = AquariumVisualRange(ref state, 0.82f, 1.24f);
                group.transform.localScale = new Vector3(
                    AquariumVisual01(ref state) > 0.5f ? scale : -scale,
                    scale,
                    1f);

                switch (created % 4)
                {
                    case 0:
                        DrawRibbonSeaweed(group.transform, ref state);
                        break;
                    case 1:
                        DrawFeatherSeaweed(group.transform, ref state);
                        break;
                    case 2:
                        DrawBranchCoral(group.transform, ref state);
                        break;
                    default:
                        DrawTubeCoral(group.transform, ref state);
                        break;
                }

                created++;
            }
        }

        private static void DrawRibbonSeaweed(Transform root, ref uint state)
        {
            Color dark = new Color(0.07f, 0.3f, 0.24f, 0.88f);
            Color fillA = new Color(0.2f, 0.58f, 0.36f, 0.62f);
            Color fillB = new Color(0.17f, 0.5f, 0.43f, 0.58f);
            Color crayon = new Color(0.48f, 0.76f, 0.47f, 0.32f);
            int bladeCount = 3 + Mathf.FloorToInt(AquariumVisual01(ref state) * 2f);
            for (int blade = 0; blade < bladeCount; blade++)
            {
                float baseX = (blade - (bladeCount - 1) * 0.5f) * 0.23f
                    + AquariumVisualRange(ref state, -0.04f, 0.04f);
                float height = AquariumVisualRange(ref state, 0.92f, 1.85f);
                float phase = AquariumVisualRange(ref state, -1.4f, 1.4f);
                float sway = AquariumVisualRange(ref state, 0.12f, 0.23f);
                Vector2[] points = new Vector2[9];
                for (int point = 0; point < points.Length; point++)
                {
                    float t = point / (float)(points.Length - 1);
                    points[point] = new Vector2(
                        baseX
                            + Mathf.Sin(t * Mathf.PI * 1.7f + phase) * sway * (0.28f + t * 0.72f)
                            + Mathf.Sin(t * 7.1f + blade) * 0.018f,
                        t * height);
                }

                float width = AquariumVisualRange(ref state, 0.25f, 0.36f);
                Color fill = (blade & 1) == 0 ? fillA : fillB;
                fill.a *= AquariumVisualRange(ref state, 0.84f, 1f);
                AddAquariumCrayonLeaf(
                    root,
                    "Broad Ribbon Seaweed",
                    points,
                    width,
                    dark,
                    fill,
                    crayon,
                    ref state);
            }

            DrawAquariumPlantBaseTuft(root, dark, fillA, crayon, ref state);
        }

        private static void DrawFeatherSeaweed(Transform root, ref uint state)
        {
            Color dark = new Color(0.08f, 0.28f, 0.25f, 0.9f);
            Color stemFill = new Color(0.18f, 0.46f, 0.34f, 0.68f);
            Color leafFill = new Color(0.34f, 0.64f, 0.4f, 0.6f);
            Color crayon = new Color(0.62f, 0.8f, 0.48f, 0.3f);
            int stemCount = 2 + Mathf.FloorToInt(AquariumVisual01(ref state) * 2f);
            for (int stemIndex = 0; stemIndex < stemCount; stemIndex++)
            {
                float baseX = (stemIndex - (stemCount - 1) * 0.5f) * 0.32f;
                float height = AquariumVisualRange(ref state, 1.05f, 1.75f);
                Vector2[] spine = new Vector2[8];
                for (int point = 0; point < spine.Length; point++)
                {
                    float t = point / (float)(spine.Length - 1);
                    spine[point] = new Vector2(
                        baseX
                            + Mathf.Sin(t * 3.2f + stemIndex * 0.8f) * 0.1f * t
                            + Mathf.Sin(t * 8.3f) * 0.012f,
                        height * t);
                }

                AddAquariumTaperedPlantStroke(
                    root,
                    "Leafy Seaweed Stem Outline",
                    spine,
                    0.14f,
                    dark,
                    -29,
                    false);
                AddAquariumTaperedPlantStroke(
                    root,
                    "Leafy Seaweed Stem Fill",
                    spine,
                    0.075f,
                    stemFill,
                    -28,
                    false);

                for (int leafIndex = 2; leafIndex < spine.Length - 1; leafIndex++)
                {
                    float side = (leafIndex & 1) == 0 ? 1f : -1f;
                    if ((stemIndex & 1) != 0) side = -side;
                    float length = AquariumVisualRange(ref state, 0.34f, 0.58f);
                    Vector2 start = spine[leafIndex];
                    Vector2 end = start + new Vector2(side * length, length * 0.44f);
                    Vector2 outward = new Vector2(side, 0f);
                    Vector2[] leaf = new[]
                    {
                        start,
                        Vector2.Lerp(start, end, 0.32f) + new Vector2(0f, length * 0.11f),
                        Vector2.Lerp(start, end, 0.68f) + outward * 0.025f,
                        end
                    };
                    AddAquariumCrayonLeaf(
                        root,
                        "Leafy Seaweed Leaf",
                        leaf,
                        AquariumVisualRange(ref state, 0.18f, 0.27f),
                        dark,
                        leafFill,
                        crayon,
                        ref state);
                }
            }

            DrawAquariumPlantBaseTuft(root, dark, stemFill, crayon, ref state);
        }

        private static void AddAquariumCrayonLeaf(
            Transform root,
            string name,
            Vector2[] centerLine,
            float width,
            Color outline,
            Color fill,
            Color crayon,
            ref uint state)
        {
            AddAquariumTaperedPlantStroke(
                root,
                name + " Outline",
                centerLine,
                width,
                outline,
                -29,
                true);
            AddAquariumTaperedPlantStroke(
                root,
                name + " Uneven Fill",
                centerLine,
                width * AquariumVisualRange(ref state, 0.66f, 0.73f),
                fill,
                -28,
                true);

            int first = centerLine.Length > 4 ? 1 : 0;
            int last = Mathf.Max(first + 1, centerLine.Length - 2);
            Vector2[] innerStroke = new Vector2[last - first + 1];
            float offset = AquariumVisualRange(ref state, -0.024f, 0.024f);
            for (int i = 0; i < innerStroke.Length; i++)
            {
                int sourceIndex = first + i;
                Vector2 tangent = sourceIndex + 1 < centerLine.Length
                    ? centerLine[sourceIndex + 1] - centerLine[sourceIndex]
                    : centerLine[sourceIndex] - centerLine[sourceIndex - 1];
                Vector2 normal = tangent.sqrMagnitude > 0.000001f
                    ? new Vector2(-tangent.y, tangent.x).normalized
                    : Vector2.right;
                innerStroke[i] = centerLine[sourceIndex]
                    + normal * (offset + Mathf.Sin(i * 2.37f) * width * 0.035f);
            }
            StageGun.AddLine(root, name + " Inner Vein", innerStroke,
                Mathf.Max(0.018f, width * 0.075f),
                new Color(outline.r, outline.g, outline.b, outline.a * 0.62f),
                -27);

            int dashCount = centerLine.Length >= 7 ? 3 : 2;
            for (int dash = 0; dash < dashCount; dash++)
            {
                int start = Mathf.Clamp(1 + dash * 2, 0, centerLine.Length - 2);
                int end = Mathf.Min(centerLine.Length - 1, start + 2);
                Vector2 direction = centerLine[end] - centerLine[start];
                Vector2 normal = direction.sqrMagnitude > 0.000001f
                    ? new Vector2(-direction.y, direction.x).normalized
                    : Vector2.right;
                float sideOffset = AquariumVisualRange(ref state, -width * 0.16f, width * 0.16f);
                StageGun.AddLine(root, name + " Crayon Fill", new[]
                {
                    centerLine[start] + normal * sideOffset,
                    Vector2.Lerp(centerLine[start], centerLine[end], 0.55f)
                        + normal * (sideOffset + AquariumVisualRange(ref state, -0.018f, 0.018f)),
                    centerLine[end] + normal * (sideOffset * 0.55f)
                }, AquariumVisualRange(ref state, width * 0.07f, width * 0.12f), crayon, -27);
            }
        }

        private static void AddAquariumTaperedPlantStroke(
            Transform root,
            string name,
            Vector2[] points,
            float width,
            Color color,
            int sortingOrder,
            bool broadLeaf)
        {
            GameObject stroke = new GameObject(name);
            stroke.transform.SetParent(root, false);
            LineRenderer line = stroke.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.widthMultiplier = width;
            line.widthCurve = broadLeaf
                ? new AnimationCurve(
                    new Keyframe(0f, 0.48f),
                    new Keyframe(0.16f, 0.9f),
                    new Keyframe(0.58f, 1f),
                    new Keyframe(0.84f, 0.7f),
                    new Keyframe(1f, 0.06f))
                : new AnimationCurve(
                    new Keyframe(0f, 0.8f),
                    new Keyframe(0.72f, 1f),
                    new Keyframe(1f, 0.2f));
            line.numCapVertices = 5;
            line.numCornerVertices = 5;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.82f);
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private static void DrawAquariumPlantBaseTuft(
            Transform root,
            Color outline,
            Color fill,
            Color crayon,
            ref uint state)
        {
            for (int tuft = 0; tuft < 3; tuft++)
            {
                float side = tuft - 1f;
                float height = AquariumVisualRange(ref state, 0.28f, 0.48f);
                Vector2[] points =
                {
                    new Vector2(side * 0.055f, -0.035f),
                    new Vector2(side * 0.16f, height * 0.42f),
                    new Vector2(side * 0.27f + AquariumVisualRange(ref state, -0.035f, 0.035f), height)
                };
                AddAquariumCrayonLeaf(
                    root,
                    "Seaweed Root Tuft",
                    points,
                    AquariumVisualRange(ref state, 0.16f, 0.23f),
                    outline,
                    fill,
                    crayon,
                    ref state);
            }
        }

        private static void DrawBranchCoral(Transform root, ref uint state)
        {
            Color coral = new Color(0.9f, 0.35f, 0.3f, 0.68f);
            Color highlight = new Color(1f, 0.62f, 0.45f, 0.46f);
            int trunkCount = 3 + Mathf.FloorToInt(AquariumVisual01(ref state) * 2f);
            for (int trunk = 0; trunk < trunkCount; trunk++)
            {
                float baseX = (trunk - (trunkCount - 1) * 0.5f) * 0.24f;
                float lean = AquariumVisualRange(ref state, -0.24f, 0.24f);
                float height = AquariumVisualRange(ref state, 0.7f, 1.35f);
                Vector2 fork = new Vector2(baseX + lean * 0.5f, height * 0.58f);
                Vector2 tip = new Vector2(baseX + lean, height);
                StageGun.AddLine(root, "Branch Coral Trunk", new[]
                {
                    new Vector2(baseX, 0f), fork, tip
                }, 0.105f, coral, -29);

                float side = (trunk & 1) == 0 ? -1f : 1f;
                Vector2 branchTip = fork + new Vector2(
                    side * AquariumVisualRange(ref state, 0.24f, 0.42f),
                    AquariumVisualRange(ref state, 0.2f, 0.42f));
                StageGun.AddLine(root, "Branch Coral Fork", new[] { fork, branchTip }, 0.08f, coral, -29);
                StageGun.AddLine(root, "Branch Coral Highlight", new[]
                {
                    new Vector2(baseX + 0.025f, 0.08f),
                    Vector2.Lerp(fork, tip, 0.66f) + new Vector2(0.025f, 0f)
                }, 0.025f, highlight, -28);
            }
        }

        private static void DrawTubeCoral(Transform root, ref uint state)
        {
            Color tube = new Color(0.55f, 0.34f, 0.72f, 0.66f);
            Color rim = new Color(0.82f, 0.62f, 0.9f, 0.6f);
            Color opening = new Color(0.2f, 0.35f, 0.42f, 0.72f);
            int tubeCount = 3 + Mathf.FloorToInt(AquariumVisual01(ref state) * 3f);
            for (int tubeIndex = 0; tubeIndex < tubeCount; tubeIndex++)
            {
                float x = (tubeIndex - (tubeCount - 1) * 0.5f) * 0.2f;
                float height = AquariumVisualRange(ref state, 0.48f, 1.12f);
                float lean = AquariumVisualRange(ref state, -0.12f, 0.12f);
                Vector2 tip = new Vector2(x + lean, height);
                StageGun.AddLine(root, "Tube Coral", new[]
                {
                    new Vector2(x, 0f),
                    new Vector2(x + lean * 0.4f, height * 0.55f),
                    tip
                }, AquariumVisualRange(ref state, 0.13f, 0.19f), tube, -29);
                CreateFishSprite(root, "Tube Coral Rim", DoodleRuntimeAssets.CircleSprite,
                    tip, new Vector2(0.2f, 0.105f), rim, -28);
                CreateFishSprite(root, "Tube Coral Opening", DoodleRuntimeAssets.CircleSprite,
                    tip, new Vector2(0.105f, 0.05f), opening, -27);
            }
        }

        private void CreateRoomBoundary()
        {
            CreateSolid(
                "Aquarium Floor",
                new Vector2(0f, FloorY),
                new Vector2(roomWidth, 0.75f),
                AquariumSolidVisual.Sand);
            StageRedrawZoneFactory.CreateRuntimeFloorZone(arenaRoot,
                "6-3_runtime_redraw_zone_" + round,
                new Vector2(0f, FloorY), roomWidth, 13.2f);
            CreateSolid(
                "Aquarium Ceiling",
                new Vector2(0f, 7.25f),
                new Vector2(roomWidth, 0.65f),
                AquariumSolidVisual.Rock);
            CreateSolid(
                "Aquarium Left Glass",
                new Vector2(-roomWidth * 0.5f, 0.5f),
                new Vector2(0.65f, 14.2f),
                AquariumSolidVisual.Rock);
            CreateSolid(
                "Aquarium Right Glass",
                new Vector2(roomWidth * 0.5f, 0.5f),
                new Vector2(0.65f, 14.2f),
                AquariumSolidVisual.Rock);
        }

        private void CreateSolid(
            string name,
            Vector2 position,
            Vector2 size,
            AquariumSolidVisual visual)
        {
            GameObject solid = new GameObject(name);
            solid.transform.SetParent(arenaRoot, false);
            solid.transform.position = position;
            solid.layer = 6;
            solid.tag = "Ground";
            BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
            collider.size = size;

            int seed = GetAquariumVisualSeed(name);
            if (visual == AquariumSolidVisual.Sand)
            {
                AddAquariumSandVisual(solid.transform, size, seed);
            }
            else
            {
                AddAquariumRockVisual(solid.transform, size, seed);
            }
        }

        private static void AddAquariumSandVisual(Transform parent, Vector2 size, int seed)
        {
            Color paper = new Color(0.9f, 0.78f, 0.48f, 0.94f);
            Color ochre = new Color(0.48f, 0.34f, 0.14f, 0.84f);
            Color dryCrayon = new Color(0.72f, 0.56f, 0.27f, 0.34f);
            Color pebbleFill = new Color(0.55f, 0.48f, 0.36f, 0.55f);
            uint state = unchecked((uint)seed);

            StageEscortController.AddFilledRect(parent, "Sand Paper", Vector2.zero, size, paper, 3);
            Vector2[] outline = CreateAquariumWobblyRect(size, ref state, 0.035f);
            StageGun.AddLine(parent, "Sand Hand Drawn Outline", outline, 0.065f, ochre, 5);
            Vector2[] echo = OffsetAquariumPoints(outline, new Vector2(0.016f, -0.014f));
            StageGun.AddLine(parent, "Sand Dry Outline", echo, 0.022f, dryCrayon, 6);

            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            int wavePointCount = Mathf.Clamp(Mathf.CeilToInt(size.x / 2.1f) + 1, 9, 32);
            for (int pass = 0; pass < 2; pass++)
            {
                Vector2[] wave = new Vector2[wavePointCount];
                for (int point = 0; point < wave.Length; point++)
                {
                    float t = point / (float)(wave.Length - 1);
                    float x = Mathf.Lerp(-halfWidth + 0.06f, halfWidth - 0.06f, t);
                    float y = halfHeight - 0.055f - pass * 0.075f
                        + Mathf.Sin(point * 1.37f + seed * 0.013f + pass * 0.8f) * 0.022f
                        + AquariumVisualRange(ref state, -0.012f, 0.012f);
                    wave[point] = new Vector2(x, y);
                }
                StageGun.AddLine(parent, "Sand Surface Wave " + pass, wave,
                    pass == 0 ? 0.052f : 0.027f,
                    pass == 0 ? ochre : dryCrayon,
                    pass == 0 ? 6 : 5);
            }

            int grainCount = Mathf.Clamp(Mathf.RoundToInt(size.x * 1.35f), 24, 72);
            for (int grain = 0; grain < grainCount; grain++)
            {
                Vector2 center = new Vector2(
                    AquariumVisualRange(ref state, -halfWidth + 0.1f, halfWidth - 0.1f),
                    AquariumVisualRange(ref state, -halfHeight + 0.08f, halfHeight - 0.1f));
                float length = AquariumVisualRange(ref state, 0.035f, 0.095f);
                float angle = AquariumVisualRange(ref state, -0.5f, 0.5f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                StageGun.AddLine(parent, "Sand Grain", new[]
                {
                    center - direction * (length * 0.5f),
                    center + direction * (length * 0.5f)
                }, AquariumVisualRange(ref state, 0.012f, 0.022f), dryCrayon, 4);
            }

            int pebbleCount = Mathf.Clamp(Mathf.RoundToInt(size.x / 4.8f), 6, 13);
            for (int pebble = 0; pebble < pebbleCount; pebble++)
            {
                float width = AquariumVisualRange(ref state, 0.13f, 0.28f);
                float height = width * AquariumVisualRange(ref state, 0.45f, 0.72f);
                Vector2 center = new Vector2(
                    AquariumVisualRange(ref state, -halfWidth + 0.25f, halfWidth - 0.25f),
                    AquariumVisualRange(ref state, -halfHeight * 0.3f, halfHeight - 0.12f));
                CreateFishSprite(parent, "Sand Pebble Fill", DoodleRuntimeAssets.CircleSprite,
                    center, new Vector2(width, height), pebbleFill, 4);
                StageGun.AddLine(parent, "Sand Pebble Pencil", new[]
                {
                    center + new Vector2(-width * 0.46f, 0f),
                    center + new Vector2(-width * 0.2f, height * 0.42f),
                    center + new Vector2(width * 0.27f, height * 0.38f),
                    center + new Vector2(width * 0.48f, -height * 0.06f),
                    center + new Vector2(width * 0.12f, -height * 0.43f),
                    center + new Vector2(-width * 0.32f, -height * 0.34f),
                    center + new Vector2(-width * 0.46f, 0f)
                }, 0.018f, ochre, 5);
            }
        }

        private static void AddAquariumRockVisual(Transform parent, Vector2 size, int seed)
        {
            Color paper = new Color(0.61f, 0.72f, 0.73f, 0.92f);
            Color wash = new Color(0.31f, 0.49f, 0.52f, 0.18f);
            Color graphite = new Color(0.2f, 0.34f, 0.37f, 0.9f);
            Color dryCrayon = new Color(0.56f, 0.7f, 0.7f, 0.4f);
            Color hatch = new Color(0.2f, 0.39f, 0.42f, 0.2f);
            uint state = unchecked((uint)seed);

            StageEscortController.AddFilledRect(parent, "Blue Gray Rock Paper", Vector2.zero, size, paper, 3);
            StageEscortController.AddFilledRect(parent, "Blue Gray Rock Wash", Vector2.zero,
                new Vector2(size.x * 0.92f, size.y * 0.92f), wash, 4);

            float wobble = Mathf.Min(0.052f, Mathf.Min(size.x, size.y) * 0.09f);
            Vector2[] outline = CreateAquariumWobblyRect(size, ref state, wobble);
            StageGun.AddLine(parent, "Rock Uneven Outline", outline, 0.072f, graphite, 5);
            StageGun.AddLine(parent, "Rock Dry Outline",
                OffsetAquariumPoints(outline, new Vector2(-0.018f, 0.014f)),
                0.022f, dryCrayon, 6);

            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            float longSide = Mathf.Max(size.x, size.y);
            int hatchCount = Mathf.Clamp(Mathf.RoundToInt(longSide / 0.82f), 6, 48);
            for (int stroke = 0; stroke < hatchCount; stroke++)
            {
                Vector2 center = new Vector2(
                    AquariumVisualRange(ref state, -halfWidth * 0.84f, halfWidth * 0.84f),
                    AquariumVisualRange(ref state, -halfHeight * 0.84f, halfHeight * 0.84f));
                float length = AquariumVisualRange(
                    ref state,
                    Mathf.Min(0.16f, longSide * 0.08f),
                    Mathf.Min(0.62f, longSide * 0.18f));
                Vector2 direction = new Vector2(0.82f, 0.57f);
                Vector2 from = center - direction * (length * 0.5f);
                Vector2 to = center + direction * (length * 0.5f);
                from.x = Mathf.Clamp(from.x, -halfWidth + 0.04f, halfWidth - 0.04f);
                from.y = Mathf.Clamp(from.y, -halfHeight + 0.04f, halfHeight - 0.04f);
                to.x = Mathf.Clamp(to.x, -halfWidth + 0.04f, halfWidth - 0.04f);
                to.y = Mathf.Clamp(to.y, -halfHeight + 0.04f, halfHeight - 0.04f);
                StageGun.AddLine(parent, "Rock Crayon Hatch", new[] { from, to },
                    AquariumVisualRange(ref state, 0.018f, 0.032f), hatch, 4);
            }

            bool horizontal = size.x >= size.y;
            int crackCount = Mathf.Clamp(Mathf.RoundToInt(longSide / 5.2f), 3, 9);
            for (int crack = 0; crack < crackCount; crack++)
            {
                Vector2 root = new Vector2(
                    AquariumVisualRange(ref state, -halfWidth * 0.68f, halfWidth * 0.68f),
                    AquariumVisualRange(ref state, -halfHeight * 0.55f, halfHeight * 0.55f));
                float sign = AquariumVisual01(ref state) > 0.5f ? 1f : -1f;
                Vector2 along = horizontal ? Vector2.right * sign : Vector2.up * sign;
                Vector2 across = horizontal ? Vector2.up : Vector2.right;
                float span = AquariumVisualRange(ref state, 0.22f, 0.58f);
                Vector2 middle = root + along * (span * 0.46f)
                    + across * AquariumVisualRange(ref state, -0.12f, 0.12f);
                Vector2 end = root + along * span
                    + across * AquariumVisualRange(ref state, -0.13f, 0.13f);
                middle.x = Mathf.Clamp(middle.x, -halfWidth + 0.06f, halfWidth - 0.06f);
                middle.y = Mathf.Clamp(middle.y, -halfHeight + 0.06f, halfHeight - 0.06f);
                end.x = Mathf.Clamp(end.x, -halfWidth + 0.06f, halfWidth - 0.06f);
                end.y = Mathf.Clamp(end.y, -halfHeight + 0.06f, halfHeight - 0.06f);
                StageGun.AddLine(parent, "Rock Pencil Crack", new[] { root, middle, end },
                    0.03f, graphite, 6);

                Vector2 branch = middle + across
                    * AquariumVisualRange(ref state, -0.2f, 0.2f)
                    + along * (span * 0.16f);
                branch.x = Mathf.Clamp(branch.x, -halfWidth + 0.06f, halfWidth - 0.06f);
                branch.y = Mathf.Clamp(branch.y, -halfHeight + 0.06f, halfHeight - 0.06f);
                StageGun.AddLine(parent, "Rock Pencil Crack Branch", new[] { middle, branch },
                    0.02f, graphite, 6);
            }
        }

        private static Vector2[] CreateAquariumWobblyRect(
            Vector2 size,
            ref uint state,
            float wobble)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            int horizontalSegments = Mathf.Clamp(Mathf.CeilToInt(size.x / 1.7f), 2, 28);
            int verticalSegments = Mathf.Clamp(Mathf.CeilToInt(size.y / 1.7f), 2, 16);
            List<Vector2> points = new List<Vector2>((horizontalSegments + verticalSegments) * 2 + 5);

            for (int i = 0; i <= horizontalSegments; i++)
            {
                points.Add(new Vector2(
                    Mathf.Lerp(-halfWidth, halfWidth, i / (float)horizontalSegments),
                    -halfHeight + AquariumVisualRange(ref state, -wobble, wobble)));
            }
            for (int i = 1; i <= verticalSegments; i++)
            {
                points.Add(new Vector2(
                    halfWidth + AquariumVisualRange(ref state, -wobble, wobble),
                    Mathf.Lerp(-halfHeight, halfHeight, i / (float)verticalSegments)));
            }
            for (int i = 1; i <= horizontalSegments; i++)
            {
                points.Add(new Vector2(
                    Mathf.Lerp(halfWidth, -halfWidth, i / (float)horizontalSegments),
                    halfHeight + AquariumVisualRange(ref state, -wobble, wobble)));
            }
            for (int i = 1; i <= verticalSegments; i++)
            {
                points.Add(new Vector2(
                    -halfWidth + AquariumVisualRange(ref state, -wobble, wobble),
                    Mathf.Lerp(halfHeight, -halfHeight, i / (float)verticalSegments)));
            }
            points.Add(points[0]);
            return points.ToArray();
        }

        private static Vector2[] OffsetAquariumPoints(Vector2[] source, Vector2 offset)
        {
            Vector2[] result = new Vector2[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[i] + offset;
            return result;
        }

        private static int GetAquariumVisualSeed(string text)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(text))
                {
                    for (int i = 0; i < text.Length; i++) hash = hash * 31 + text[i];
                }
                return hash;
            }
        }

        private static float AquariumVisual01(ref uint state)
        {
            if (state == 0u) state = 0xA341316Cu;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777215f;
        }

        private static float AquariumVisualRange(ref uint state, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, AquariumVisual01(ref state));
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
            Vector2[] clusterCenters = GetHoleClusterCenters(playerCount, round);
            int holesPerPlayer = Mathf.Clamp(round, 1, 3);
            for (int cluster = 0; cluster < clusterCenters.Length; cluster++)
            {
                Vector2 normalizedCenter = clusterCenters[cluster];
                Vector2 center = new Vector2(normalizedCenter.x * usableWidth, normalizedCenter.y);
                Vector2[] offsets = GetGroupedHoleOffsets(playerCount, round, cluster, holesPerPlayer);
                for (int member = 0; member < offsets.Length; member++)
                {
                    CreateHole(center + offsets[member], holes.Count);
                }
            }
        }

        private static Vector2[] GetHoleClusterCenters(int playerCount, int targetRound)
        {
            // One cluster is assigned to each player. Later rounds add holes
            // inside the cluster instead of adding distant holes that would
            // require one body to occupy several unrelated parts of the tank.
            Vector2[] centers;
            switch (Mathf.Clamp(playerCount, 1, 4))
            {
                case 1:
                    centers = new[] { new Vector2(0f, -2.1f) };
                    break;
                case 2:
                    centers = new[]
                    {
                        new Vector2(-0.23f, -2.75f), new Vector2(0.23f, -1.25f)
                    };
                    break;
                case 3:
                    centers = new[]
                    {
                        new Vector2(-0.32f, -2.65f), new Vector2(0f, -0.65f),
                        new Vector2(0.31f, -3.05f)
                    };
                    break;
                default:
                    centers = new[]
                    {
                        new Vector2(-0.36f, -2.55f), new Vector2(-0.12f, -0.55f),
                        new Vector2(0.13f, -3.15f), new Vector2(0.35f, -1.25f)
                    };
                    break;
            }

            // Keep the lateral lanes readable, but shuffle which height belongs
            // to each lane every round so the left-to-right height order does
            // not repeat throughout the challenge. This is deterministic for
            // online clients and independent of UnityEngine.Random state.
            System.Random heightRandom = new System.Random(
                6353 + playerCount * 431 + Mathf.Clamp(targetRound, 1, 3) * 1879);
            float[] heights = new float[centers.Length];
            for (int i = 0; i < centers.Length; i++) heights[i] = centers[i].y;
            for (int i = heights.Length - 1; i > 0; i--)
            {
                int other = heightRandom.Next(i + 1);
                float swap = heights[i];
                heights[i] = heights[other];
                heights[other] = swap;
            }
            for (int i = 0; i < centers.Length; i++) centers[i].y = heights[i];
            return centers;
        }

        private static Vector2[] GetGroupedHoleOffsets(
            int playerCount, int targetRound, int cluster, int count)
        {
            if (count <= 1) return new[] { Vector2.zero };

            // Use a local deterministic generator so all online clients build
            // the same arena. Each cluster still gets a varied horizontal,
            // vertical, or diagonal arrangement. The farthest two holes stay
            // inside the round-specific maximum while not overlapping.
            int seed = 6300 + playerCount * 101 + targetRound * 1009 + cluster * 7919;
            System.Random random = new System.Random(seed);
            float maximumDistance = targetRound == 2
                ? MaximumGroupedHoleDistance * 2f
                : MaximumGroupedHoleDistance;
            float distance = Mathf.Lerp(
                MinimumGroupedHoleDistance,
                maximumDistance,
                (float)random.NextDouble());
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;

            if (count == 2)
            {
                Vector2 halfOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (distance * 0.5f);
                return new[] { -halfOffset, halfOffset };
            }

            int pattern = random.Next(4);
            if (pattern <= 2)
            {
                // Straight rows are deliberately snapped to horizontal,
                // vertical, or diagonal rather than always forming a triangle.
                float lineAngle = pattern == 0
                    ? 0f
                    : pattern == 1
                        ? Mathf.PI * 0.5f
                        : (random.Next(2) == 0 ? Mathf.PI * 0.25f : -Mathf.PI * 0.25f);
                Vector2 halfOffset = new Vector2(Mathf.Cos(lineAngle), Mathf.Sin(lineAngle)) * (distance * 0.5f);
                return new[] { -halfOffset, Vector2.zero, halfOffset };
            }

            float radius = distance / Mathf.Sqrt(3f);
            Vector2[] offsets = new Vector2[3];
            for (int i = 0; i < offsets.Length; i++)
            {
                float memberAngle = angle + i * Mathf.PI * 2f / 3f;
                offsets[i] = new Vector2(Mathf.Cos(memberAngle), Mathf.Sin(memberAngle)) * radius;
            }
            return offsets;
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

            boxPreview = factory.CreateDroppedBoxPreview(
                StageObjectType.WoodBox, dropperObject.transform, 33);
            if (boxPreview != null)
                boxPreview.localPosition = new Vector3(0f, -0.02f, -0.03f);
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
                BodyBuilder body = players[i].GetComponent<BodyBuilder>();
                if (body != null)
                {
                    if (body.CoversWorldPoint(center, 0.1f)) return true;
                    continue;
                }

                // Compatibility fallback for a player prefab without BodyBuilder.
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
            bool enabled = phase == SealPhase.Active
                && Time.unscaledTime >= roundReadyAt
                && !stageManager.IsDrawingMode;
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
