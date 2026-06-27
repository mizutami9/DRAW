using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static class Phase0SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string GeneratedFolder = "Assets/Generated";
        private const string SquareTexturePath = GeneratedFolder + "/SquareTexture.asset";
        private const int GroundLayer = 6;
        private const int PlayerLayer = 7;
        private const int GoalLayer = 8;
        private const int PushableLayer = 9;
        private static Material doodleLineMaterial;

        [MenuItem("PICO/Build Phase 0 Scene")]
        public static void BuildScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Physics2D.gravity = new Vector2(0f, -28f);

            GameObject root = new GameObject("Phase 0 Prototype");
            root.AddComponent<LocalizationManager>();
            GameObject spawnPoint = CreateMarker("SpawnPoint", new Vector3(-6f, 1.8f, 0f), root.transform);
            Sprite squareSprite = CreateSquareSprite();
            Font font = CreateDefaultFont();

            GameObject player = CreatePlayer(spawnPoint.transform.position, root.transform, squareSprite);
            GameObject stageManager = new GameObject("StageManager");
            stageManager.transform.SetParent(root.transform);
            StageManager manager = stageManager.AddComponent<StageManager>();
            OnlineManager onlineManager = stageManager.AddComponent<OnlineManager>();
            StageObjectFactory objectFactory = stageManager.AddComponent<StageObjectFactory>();
            StageLoader stageLoader = stageManager.AddComponent<StageLoader>();
            GameObject debugStageRoot = new GameObject("DebugStageRoot");
            debugStageRoot.transform.SetParent(root.transform);
            GameObject runtimeStageRoot = new GameObject("RuntimeStageRoot");
            runtimeStageRoot.transform.SetParent(root.transform);
            GameObject runtimeStageEditorRoot = new GameObject("RuntimeStageEditorRoot");
            runtimeStageEditorRoot.transform.SetParent(root.transform);

            GameObject cameraObject = CreateCamera(player.transform, root.transform);
            CreateNotebookBackdrop(root.transform, squareSprite, font);
            CreateLevel(debugStageRoot.transform, squareSprite, font);
            GameObject goal = CreateGoal(new Vector3(38.8f, 0.58f, 0f), debugStageRoot.transform, squareSprite);
            CreateMapDoodles(debugStageRoot.transform, font);
            UIManager ui = CreateUi(root.transform, font, manager, onlineManager, out DrawManager drawManager, out RuntimeStageEditor runtimeStageEditor);

            goal.GetComponent<Goal>();
            AssignObject(manager, "player", player.GetComponent<PlayerController2D>());
            AssignObject(manager, "uiManager", ui);
            AssignObject(manager, "drawManager", drawManager);
            AssignObject(manager, "stageLoader", stageLoader);
            AssignObject(manager, "stageEditor", runtimeStageEditor);
            AssignObject(manager, "cameraFollow", cameraObject.GetComponent<CameraFollow2D>());
            AssignObject(manager, "spawnPoint", spawnPoint.transform);
            AssignLayerMask(manager, "groundLayer", 1 << GroundLayer);
            AssignObject(stageLoader, "stageRoot", runtimeStageRoot.transform);
            AssignObject(stageLoader, "fallbackStageRoot", debugStageRoot);
            AssignObject(stageLoader, "spawnPoint", spawnPoint.transform);
            AssignObject(stageLoader, "objectFactory", objectFactory);
            AssignObject(runtimeStageEditor, "stageLoader", stageLoader);
            AssignObject(runtimeStageEditor, "objectFactory", objectFactory);
            AssignObject(runtimeStageEditor, "editorRoot", runtimeStageEditorRoot.transform);
            AssignObject(runtimeStageEditor, "worldCamera", Camera.main);
            AssignObject(drawManager, "stageManager", manager);
            AssignObject(drawManager, "onlineManager", onlineManager);
            AssignObject(drawManager, "bodyBuilder", player.GetComponent<BodyBuilder>());
            AssignObject(drawManager, "abilityController", player.GetComponent<PlayerAbilityController>());

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            Selection.activeGameObject = player;

            Debug.Log($"Phase 0 scene generated: {ScenePath}");
        }

        private static GameObject CreatePlayer(Vector3 position, Transform parent, Sprite squareSprite)
        {
            GameObject player = CreateSpriteBox("Player", position, new Vector2(0.9f, 1.1f), new Color(0.12f, 0.35f, 0.95f), parent, squareSprite);
            player.transform.localScale = Vector3.one;
            player.layer = PlayerLayer;
            player.tag = "Player";
            SpriteRenderer fallbackRenderer = player.GetComponent<SpriteRenderer>();

            Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            rb.gravityScale = 3.3f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            GameObject groundCheck = CreateMarker("GroundCheck", new Vector3(0f, -1.25f, 0f), player.transform);
            PlayerController2D controller = player.AddComponent<PlayerController2D>();
            AssignObject(controller, "groundCheck", groundCheck.transform);
            AssignLayerMask(controller, "groundLayer", 1 << GroundLayer);
            AssignVector2(controller, "groundCheckSize", new Vector2(1.2f, 0.18f));
            AssignFloat(controller, "glideFallSpeed", -2.8f);
            AssignFloat(controller, "slimeStickDuration", 0.28f);

            PlayerAbilityController abilityController = player.AddComponent<PlayerAbilityController>();
            AssignObject(abilityController, "playerController", controller);
            AssignObject(abilityController, "rb", rb);

            GameObject swingPivot = CreateMarker("ArmSwingPivot", new Vector3(0f, 0.1f, 0f), player.transform);
            ArmSwingController armSwing = player.AddComponent<ArmSwingController>();
            AssignObject(armSwing, "abilityController", abilityController);
            AssignObject(armSwing, "playerController", controller);
            AssignObject(armSwing, "swingPivot", swingPivot.transform);
            AssignLayerMask(armSwing, "pushableLayerMask", (1 << PushableLayer) | (1 << PlayerLayer));
            AssignFloat(armSwing, "armThickness", 0.42f);
            AssignFloat(armSwing, "pushImpulse", 16f);
            AssignFloat(armSwing, "swingReachMultiplier", 2f);
            AssignFloat(armSwing, "characterLaunchMultiplier", 2.6f);
            AssignFloat(armSwing, "armInkLaunchScale", 0.018f);
            AssignFloat(armSwing, "characterLaunchUpSpeed", 30f);
            AssignFloat(armSwing, "characterLaunchSideSpeed", 7f);
            AssignBool(armSwing, "swingEnabled", false);

            GameObject bodyRoot = CreateMarker("GeneratedBody", Vector3.zero, player.transform);
            BodyBuilder bodyBuilder = player.AddComponent<BodyBuilder>();
            AssignObject(bodyBuilder, "bodyRoot", bodyRoot.transform);
            AssignObject(bodyBuilder, "fallbackCollider", collider);
            AssignObject(bodyBuilder, "fallbackRenderer", fallbackRenderer);

            PlayerCarryController carryController = player.AddComponent<PlayerCarryController>();
            AssignObject(carryController, "playerController", controller);
            AssignObject(carryController, "abilityController", abilityController);
            AssignObject(carryController, "bodyBuilder", bodyBuilder);
            AssignObject(carryController, "playerBody", rb);
            AssignLayerMask(carryController, "carryableLayerMask", (1 << PushableLayer) | (1 << PlayerLayer));
            AssignFloat(carryController, "throwSpeed", 22f);
            AssignFloat(carryController, "armInkThrowScale", 0.0175f);
            AssignFloat(carryController, "heldPlayerThrowMultiplier", 1.25f);
            AssignFloat(carryController, "throwAimSpeed", 1.35f);
            AssignFloat(carryController, "throwPreviewLength", 1.8f);

            return player;
        }

        private static GameObject CreateCamera(Transform player, Transform parent)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 1.4f, -10f);
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.97f, 0.96f, 0.91f);

            CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
            AssignObject(follow, "target", player);
            return cameraObject;
        }

        private static void CreateNotebookBackdrop(Transform parent, Sprite squareSprite, Font font)
        {
            GameObject paper = CreateSpriteBox("Notebook Paper", new Vector3(16f, 1.7f, 1.8f), new Vector2(54f, 9.6f), new Color(0.985f, 0.975f, 0.93f), parent, squareSprite);
            SetSortingOrder(paper, -100);

            for (int i = 0; i < 15; i++)
            {
                float y = -3.5f + i * 0.6f;
                AddDoodleLine(
                    $"Notebook Rule {i}",
                    parent,
                    new[] { new Vector3(-11f, y, 1.7f), new Vector3(43f, y + Mathf.Sin(i * 1.7f) * 0.03f, 1.7f) },
                    new Color(0.35f, 0.68f, 0.95f, 0.35f),
                    0.018f,
                    -90);
            }

            AddDoodleLine("Notebook Margin", parent, new[] { new Vector3(-8.3f, -3.7f, 1.7f), new Vector3(-8.2f, 6.1f, 1.7f) }, new Color(0.95f, 0.28f, 0.32f, 0.38f), 0.025f, -89);

            for (int i = 0; i < 4; i++)
            {
                Vector3 holePosition = new Vector3(-9.8f, 4.9f - i * 2.35f, 1.6f);
                AddDoodleCircle($"Notebook Hole {i}", parent, 0.24f, new Color(0.62f, 0.36f, 0.18f, 0.9f), 0.15f, -88, holePosition);
                AddDoodleCircle($"Notebook Hole Ring {i}", parent, 0.28f, new Color(0.46f, 0.28f, 0.14f, 0.65f), 0.025f, -87, holePosition);
            }

            TextMesh title = CreateDoodleText("Title Draw Body", "DRAW BODY", new Vector3(-7.3f, 5.05f, 0f), parent, font, 54, 0.13f, Color.black, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.transform.rotation = Quaternion.Euler(0f, 0f, -4f);
            CreateDoodleText("Subtitle", "\u63cf\u3044\u3066\u3001\u52d5\u3044\u3066\u3001\u7a81\u7834\u3057\u308d\uff01", new Vector3(-5.6f, 4.25f, 0f), parent, font, 26, 0.11f, Color.black, TextAnchor.MiddleLeft).transform.rotation = Quaternion.Euler(0f, 0f, -4f);
            AddDoodleLine("Title Red Underline", parent, new[] { new Vector3(-7.0f, 4.55f, 0f), new Vector3(-1.8f, 4.55f, 0f) }, new Color(0.95f, 0.1f, 0.12f), 0.035f, 20);
            AddDoodleLine("Title Yellow Underline", parent, new[] { new Vector3(-5.5f, 3.95f, 0f), new Vector3(0.9f, 4.05f, 0f) }, new Color(1f, 0.78f, 0.05f), 0.035f, 20);
        }

        private static void CreateMapDoodles(Transform parent, Font font)
        {
            CreateStartArrow(parent, font);
            CreateCloud(parent, new Vector3(3.1f, 4.45f, 0f), 0.78f);
            CreateCloud(parent, new Vector3(7.6f, 3.35f, 0f), 0.6f);
            CreateCloud(parent, new Vector3(22.2f, 4.9f, 0f), 0.82f);
            CreateSun(parent, new Vector3(31.5f, 4.65f, 0f));
            CreateFlag(parent, new Vector3(24.1f, 2.2f, 0f));
            CreateDoodleText("Goal Text", "GOAL!", new Vector3(37.7f, 4.65f, 0f), parent, font, 32, 0.12f, new Color(0.1f, 0.7f, 0.08f), TextAnchor.MiddleCenter).transform.rotation = Quaternion.Euler(0f, 0f, 5f);
            CreateSkull(parent, new Vector3(42.1f, -0.05f, 0f));
        }

        private static void CreateStartArrow(Transform parent, Font font)
        {
            Vector3[] points =
            {
                new Vector3(-9.25f, 0.9f, 0f),
                new Vector3(-7.15f, 0.9f, 0f),
                new Vector3(-7.15f, 1.35f, 0f),
                new Vector3(-5.95f, 0.35f, 0f),
                new Vector3(-7.15f, -0.65f, 0f),
                new Vector3(-7.15f, -0.2f, 0f),
                new Vector3(-9.25f, -0.2f, 0f),
                new Vector3(-9.25f, 0.9f, 0f)
            };
            AddDoodleLine("Start Arrow", parent, points, new Color(0.02f, 0.22f, 0.9f), 0.05f, 30);
            CreateDoodleText("Start Text", "START", new Vector3(-8.15f, 0.22f, 0f), parent, font, 25, 0.11f, new Color(0.02f, 0.22f, 0.9f), TextAnchor.MiddleCenter).transform.rotation = Quaternion.Euler(0f, 0f, -5f);
        }

        private static void CreateCloud(Transform parent, Vector3 center, float scale)
        {
            Vector3[] points =
            {
                center + new Vector3(-1.0f, -0.2f, 0f) * scale,
                center + new Vector3(-0.75f, 0.25f, 0f) * scale,
                center + new Vector3(-0.35f, 0.2f, 0f) * scale,
                center + new Vector3(-0.15f, 0.55f, 0f) * scale,
                center + new Vector3(0.35f, 0.48f, 0f) * scale,
                center + new Vector3(0.48f, 0.12f, 0f) * scale,
                center + new Vector3(0.92f, 0.05f, 0f) * scale,
                center + new Vector3(1.05f, -0.3f, 0f) * scale,
                center + new Vector3(0.4f, -0.35f, 0f) * scale,
                center + new Vector3(-0.4f, -0.34f, 0f) * scale,
                center + new Vector3(-1.0f, -0.2f, 0f) * scale
            };
            AddDoodleLine("Cloud", parent, points, new Color(0.0f, 0.25f, 1f), 0.05f, 15);
        }

        private static void CreateSun(Transform parent, Vector3 center)
        {
            AddDoodleCircle("Sun", parent, 0.55f, new Color(0.95f, 0.28f, 0.05f), 0.06f, 20, center);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                AddDoodleLine($"Sun Ray {i}", parent, new[] { center + direction * 0.8f, center + direction * 1.08f }, new Color(0.95f, 0.12f, 0.04f), 0.035f, 18);
            }
        }

        private static void CreateFlag(Transform parent, Vector3 basePosition)
        {
            AddDoodleLine("Flag Pole", parent, new[] { basePosition, basePosition + new Vector3(0f, 1.2f, 0f) }, Color.black, 0.04f, 18);
            Vector3[] flag =
            {
                basePosition + new Vector3(0f, 1.15f, 0f),
                basePosition + new Vector3(0.85f, 0.95f, 0f),
                basePosition + new Vector3(0f, 0.72f, 0f),
                basePosition + new Vector3(0f, 1.15f, 0f)
            };
            AddDoodleLine("Flag Cloth", parent, flag, new Color(0.05f, 0.8f, 0.15f), 0.06f, 19);
        }

        private static void CreateSkull(Transform parent, Vector3 center)
        {
            AddDoodleCircle("Skull Head", parent, 0.46f, Color.black, 0.05f, 20, center + new Vector3(0f, 0.3f, 0f));
            AddDoodleCircle("Skull Eye L", parent, 0.08f, Color.black, 0.05f, 21, center + new Vector3(-0.16f, 0.37f, 0f));
            AddDoodleCircle("Skull Eye R", parent, 0.08f, Color.black, 0.05f, 21, center + new Vector3(0.16f, 0.37f, 0f));
            AddDoodleLine("Skull Jaw", parent, new[] { center + new Vector3(-0.25f, -0.02f, 0f), center + new Vector3(0.25f, -0.02f, 0f) }, Color.black, 0.04f, 21);
        }

        private static void CreateLevel(Transform parent, Sprite squareSprite, Font font)
        {
            CreateGroundPlatform("Ground A", new Vector3(-1.5f, -0.55f, 0f), new Vector2(12f, 0.45f), parent, squareSprite);
            CreateGroundPlatform("Ground B", new Vector3(11f, -0.55f, 0f), new Vector2(10f, 0.45f), parent, squareSprite);
            CreateGroundPlatform("Ground C", new Vector3(23.5f, -0.55f, 0f), new Vector2(11f, 0.45f), parent, squareSprite);
            CreateGroundPlatform("Ground D", new Vector3(35.5f, -0.55f, 0f), new Vector2(9f, 0.45f), parent, squareSprite);

            CreateTextLabel("label_high_platform", new Vector3(1.8f, 2.9f, 0f), parent, font);
            CreatePlatform("High Platform Step 1", new Vector3(1.5f, 0.75f, 0f), new Vector2(2.2f, 0.35f), parent, squareSprite);
            CreatePlatform("High Platform Step 2", new Vector3(4.2f, 1.85f, 0f), new Vector2(2.0f, 0.35f), parent, squareSprite);

            CreateTextLabel("label_heavy_switch", new Vector3(8.8f, 2.2f, 0f), parent, font);
            GameObject heavyGate = CreateGate("Heavy Gate", new Vector3(11.9f, 0.75f, 0f), new Vector2(0.55f, 2.2f), parent, squareSprite, new Color(0.45f, 0.22f, 0.82f));
            GameObject heavySwitch = CreateSwitchPlate("Heavy Switch", new Vector3(8.5f, -0.2f, 0f), parent, squareSprite);
            WeightedSwitch weightedSwitch = heavySwitch.AddComponent<WeightedSwitch>();
            AssignObject(weightedSwitch, "targetGate", heavyGate.GetComponent<MovingGate>());
            AssignObject(weightedSwitch, "indicator", heavySwitch.GetComponent<SpriteRenderer>());

            CreateTextLabel("label_far_lever", new Vector3(17.5f, 2.2f, 0f), parent, font);
            GameObject leverGate = CreateGate("Lever Gate", new Vector3(20.6f, 0.75f, 0f), new Vector2(0.55f, 2.2f), parent, squareSprite, new Color(0.2f, 0.46f, 0.9f));
            GameObject lever = CreateLever("Far Lever", new Vector3(17.8f, 0.35f, 0f), parent, squareSprite);
            LeverSwitch leverSwitch = lever.AddComponent<LeverSwitch>();
            AssignObject(leverSwitch, "targetGate", leverGate.GetComponent<MovingGate>());
            AssignObject(leverSwitch, "indicator", lever.GetComponent<SpriteRenderer>());

            CreateTextLabel("label_narrow_hole", new Vector3(25.6f, 2.2f, 0f), parent, font);
            CreatePlatform("Narrow Top", new Vector3(25.7f, 1.1f, 0f), new Vector2(4.4f, 0.35f), parent, squareSprite);
            CreatePlatform("Narrow Ceiling Left", new Vector3(23.4f, 0.45f, 0f), new Vector2(1.2f, 1.0f), parent, squareSprite);
            CreatePlatform("Narrow Ceiling Right", new Vector3(28.0f, 0.45f, 0f), new Vector2(1.2f, 1.0f), parent, squareSprite);

            CreateTextLabel("label_ball_hit", new Vector3(33.2f, 2.2f, 0f), parent, font);
            CreatePushableBall(new Vector3(32.2f, 0.1f, 0f), parent, squareSprite);
            CreatePlatform("Ball Ramp", new Vector3(35.3f, 0.15f, 0f), new Vector2(3.0f, 0.25f), parent, squareSprite).transform.rotation = Quaternion.Euler(0f, 0f, -12f);
        }

        private static GameObject CreatePlatform(string name, Vector3 position, Vector2 size, Transform parent, Sprite squareSprite)
        {
            GameObject platform = CreateSpriteBox(name, position, size, new Color(0.08f, 0.08f, 0.08f, 0.025f), parent, squareSprite);
            AddPencilFillLocal(name + " Surface Pencil Fill", platform.transform, size, new Color(0.05f, 0.05f, 0.05f));
            AddSketchBoxOutline(platform.transform, size, new Color(0.02f, 0.02f, 0.02f), 0.055f, 8);
            AddSketchBoxOutline(platform.transform, size + new Vector2(0.04f, 0.04f), new Color(0.1f, 0.48f, 0.95f, 0.55f), 0.035f, 7, new Vector3(0.02f, -0.03f, 0f));
            platform.layer = GroundLayer;
            platform.tag = "Ground";
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            return platform;
        }

        private static GameObject CreateGroundPlatform(string name, Vector3 position, Vector2 size, Transform parent, Sprite squareSprite)
        {
            const float visualBottom = -4.15f;
            float top = position.y + size.y * 0.5f;
            float fillHeight = Mathf.Max(0.1f, top - visualBottom);
            Vector3 fillPosition = new Vector3(position.x, visualBottom + fillHeight * 0.5f, position.z + 0.04f);
            GameObject fill = CreateSpriteBox(name + " Fill Wash", fillPosition, new Vector2(size.x, fillHeight), new Color(0.08f, 0.08f, 0.08f, 0.025f), parent, squareSprite);
            SetSortingOrder(fill, 3);
            AddPencilFill(name + " Pencil Fill", parent, position.x, size.x, visualBottom, top);

            return CreatePlatform(name, position, size, parent, squareSprite);
        }

        private static void AddPencilFill(string name, Transform parent, float centerX, float width, float bottom, float top)
        {
            float left = centerX - width * 0.5f;
            float right = centerX + width * 0.5f;
            Color pencil = new Color(0.05f, 0.05f, 0.05f, 0.32f);

            int index = 0;
            for (int layer = 0; layer < 3; layer++)
            {
                float row = bottom + 0.06f + layer * 0.05f;
                float rowSpacing = 0.22f + layer * 0.025f;
                while (row < top - 0.04f)
                {
                    float x = left - 0.6f + Mathf.Sin(index * 1.37f) * 0.08f + layer * 0.1f;
                    while (x < right)
                    {
                        float jitterX = Mathf.Sin(index * 2.17f) * 0.045f;
                        float jitterY = Mathf.Cos(index * 1.61f) * 0.035f;
                        float length = 0.7f + Mathf.Abs(Mathf.Sin(index * 1.11f)) * 0.45f;
                        float rise = 0.22f + Mathf.Abs(Mathf.Cos(index * 1.91f)) * 0.16f;
                        float startX = Mathf.Max(left, x + jitterX);
                        float startY = Mathf.Clamp(row + jitterY, bottom + 0.04f, top - 0.04f);
                        float endX = Mathf.Min(right, startX + length);
                        float endY = Mathf.Clamp(startY + rise, bottom + 0.04f, top - 0.04f);

                        if (endX > left && startX < right && endY > bottom)
                        {
                            Color layerColor = new Color(pencil.r, pencil.g, pencil.b, 0.14f + layer * 0.045f + Mathf.Abs(Mathf.Sin(index * 0.71f)) * 0.07f);
                            AddDoodleLine(
                                $"{name} Pencil Stroke {index}",
                                parent,
                                new[]
                                {
                                    new Vector3(startX, startY, 0f),
                                    new Vector3(endX, endY, 0f)
                                },
                                layerColor,
                                0.01f + layer * 0.002f,
                                4);
                        }

                        x += 0.34f + Mathf.Sin(index * 3.23f) * 0.045f;
                        index++;
                    }

                    row += rowSpacing;
                }
            }

            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(bottom + 0.16f, top - 0.12f, (i + 1f) / 6f);
                AddDoodleLine(
                    $"{name} Soft Horizontal Grain {i}",
                    parent,
                    new[]
                    {
                        new Vector3(left + 0.1f, y + Mathf.Sin(i * 1.3f) * 0.025f, 0f),
                        new Vector3(right - 0.1f, y + Mathf.Cos(i * 1.9f) * 0.025f, 0f)
                    },
                    new Color(0.05f, 0.05f, 0.05f, 0.13f),
                    0.01f,
                    5);
            }
        }

        private static void AddPencilFillLocal(string name, Transform parent, Vector2 size, Color pencilColor)
        {
            float inverseScale = 1f / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f);
            Color pencil = new Color(pencilColor.r, pencilColor.g, pencilColor.b, 0.28f);
            int index = 0;

            for (int layer = 0; layer < 3; layer++)
            {
                float row = -0.44f + layer * 0.09f;
                while (row < 0.46f)
                {
                    float x = -0.58f + Mathf.Sin(index * 1.7f) * 0.035f + layer * 0.06f;
                    while (x < 0.5f)
                    {
                        float startX = Mathf.Clamp(x + Mathf.Sin(index * 2.3f) * 0.025f, -0.5f, 0.5f);
                        float startY = Mathf.Clamp(row + Mathf.Cos(index * 1.4f) * 0.035f, -0.48f, 0.48f);
                        float endX = Mathf.Clamp(startX + 0.18f + Mathf.Abs(Mathf.Sin(index * 0.9f)) * 0.18f, -0.5f, 0.5f);
                        float endY = Mathf.Clamp(startY + 0.28f + Mathf.Cos(index * 1.8f) * 0.08f, -0.48f, 0.48f);

                        if (endX > startX + 0.02f)
                        {
                            Color layerColor = new Color(pencil.r, pencil.g, pencil.b, 0.13f + layer * 0.05f + Mathf.Abs(Mathf.Sin(index * 0.73f)) * 0.07f);
                            AddDoodleLine(
                                $"{name} Stroke {index}",
                                parent,
                                new[]
                                {
                                    new Vector3(startX, startY, 0f),
                                    new Vector3(endX, endY, 0f)
                                },
                                layerColor,
                                (0.012f + layer * 0.003f) * inverseScale,
                                6);
                        }

                        x += 0.12f + Mathf.Abs(Mathf.Sin(index * 2.1f)) * 0.08f;
                        index++;
                    }

                    row += 0.18f + layer * 0.025f;
                }
            }
        }

        private static GameObject CreateGoal(Vector3 position, Transform parent, Sprite squareSprite)
        {
            GameObject goal = CreateSpriteBox("Goal", position, new Vector2(1.15f, 2.05f), new Color(0f, 0.85f, 0.35f, 0.12f), parent, squareSprite);
            goal.layer = GoalLayer;
            goal.tag = "Goal";
            AddPencilFillLocal("Goal Green Pencil Fill", goal.transform, new Vector2(1.15f, 2.05f), new Color(0.0f, 0.65f, 0.24f));
            AddDoorDoodle(goal.transform);

            BoxCollider2D collider = goal.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true;
            goal.AddComponent<Goal>();

            return goal;
        }

        private static GameObject CreatePushableBall(Vector3 position, Transform parent, Sprite squareSprite)
        {
            GameObject ball = CreateSpriteBox("Pushable Ball", position, new Vector2(0.65f, 0.65f), new Color(1f, 0.72f, 0.18f, 0.08f), parent, squareSprite);
            ball.layer = PushableLayer;
            AddPencilFillLocal("Ball Yellow Pencil Fill", ball.transform, new Vector2(0.65f, 0.65f), new Color(0.95f, 0.58f, 0.05f));
            AddDoodleCircle("BallOutline", ball.transform, 0.56f, new Color(0.02f, 0.02f, 0.02f), 0.045f, 24);
            AddDoodleLine("BallPatchA", ball.transform, new[] { new Vector3(-0.2f, 0.24f, 0f), new Vector3(0.16f, -0.2f, 0f) }, new Color(0.02f, 0.02f, 0.02f), 0.03f, 10);
            AddDoodleLine("BallPatchB", ball.transform, new[] { new Vector3(-0.24f, -0.02f, 0f), new Vector3(0.22f, 0.08f, 0f) }, new Color(0.02f, 0.02f, 0.02f), 0.03f, 10);

            Rigidbody2D rb = ball.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2.6f;
            rb.mass = 0.8f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D collider = ball.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;

            ball.AddComponent<PushableObject>();
            return ball;
        }

        private static GameObject CreateGate(string name, Vector3 position, Vector2 size, Transform parent, Sprite squareSprite, Color color)
        {
            GameObject gate = CreateSpriteBox(name, position, size, new Color(color.r, color.g, color.b, 0.08f), parent, squareSprite);
            gate.layer = GroundLayer;
            AddPencilFillLocal(name + " Color Pencil Fill", gate.transform, size, color);
            AddSketchBoxOutline(gate.transform, size, new Color(0.02f, 0.02f, 0.02f), 0.055f, 8);
            BoxCollider2D collider = gate.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            gate.AddComponent<MovingGate>();
            return gate;
        }

        private static GameObject CreateSwitchPlate(string name, Vector3 position, Transform parent, Sprite squareSprite)
        {
            Vector2 size = new Vector2(1.2f, 0.22f);
            Color color = new Color(0.85f, 0.2f, 0.18f);
            GameObject plate = CreateSpriteBox(name, position, size, new Color(color.r, color.g, color.b, 0.08f), parent, squareSprite);
            AddPencilFillLocal(name + " Red Pencil Fill", plate.transform, size, color);
            AddSketchBoxOutline(plate.transform, size, new Color(0.02f, 0.02f, 0.02f), 0.04f, 5);
            BoxCollider2D collider = plate.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true;
            return plate;
        }

        private static GameObject CreateLever(string name, Vector3 position, Transform parent, Sprite squareSprite)
        {
            Vector2 size = new Vector2(0.26f, 1.35f);
            Color color = new Color(1f, 0.72f, 0.18f);
            GameObject lever = CreateSpriteBox(name, position, size, new Color(color.r, color.g, color.b, 0.08f), parent, squareSprite);
            lever.layer = PushableLayer;
            AddPencilFillLocal(name + " Yellow Pencil Fill", lever.transform, size, color);
            AddDoodleCircle("LeverKnob", lever.transform, 0.22f, new Color(0.95f, 0.1f, 0.16f), 0.08f, 18, new Vector3(0f, 0.62f, 0f));
            AddDoodleLine("LeverOutline", lever.transform, new[] { new Vector3(0f, -0.55f, 0f), new Vector3(0.03f, 0.55f, 0f) }, new Color(0.02f, 0.02f, 0.02f), 0.045f, 9);
            BoxCollider2D collider = lever.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true;
            return lever;
        }

        private static void CreateTextLabel(string localizationKey, Vector3 position, Transform parent, Font font)
        {
            GameObject label = new GameObject(localizationKey);
            label.transform.SetParent(parent);
            label.transform.position = position;

            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = LocalizationManager.T(localizationKey);
            mesh.fontSize = 32;
            mesh.characterSize = 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.black;

            LocalizedText localizedText = label.AddComponent<LocalizedText>();
            AssignString(localizedText, "key", localizationKey);
        }

        private static UIManager CreateUi(Transform parent, Font font, StageManager stageManager, OnlineManager onlineManager, out DrawManager drawManager, out RuntimeStageEditor runtimeStageEditor)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(parent);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            GameObject drawPanel = CreateDrawPanel(
                canvasObject.transform,
                font,
                stageManager,
                out RectTransform drawArea,
                out RectTransform lineRoot,
                out RectTransform previewRoot,
                out Text inkText,
                out Image inkGaugeFill,
                out Text partText,
                out Text messageText,
                out Text abilityText,
                out Button clearButton,
                out Button decideButton,
                out Button[] partButtons);

            GameObject clearPanel = CreatePanel("ClearPanel", canvasObject.transform, new Color(0.05f, 0.2f, 0.08f, 0.68f));
            Text clearText = CreateText("ClearText", clearPanel.transform, font, 42, TextAnchor.MiddleCenter);
            clearText.text = LocalizationManager.T("status_clear");
            clearText.color = Color.white;
            AddLocalizedText(clearText.gameObject, "status_clear");
            Stretch(clearText.rectTransform);

            drawPanel.SetActive(false);
            clearPanel.SetActive(false);

            UIManager ui = canvasObject.AddComponent<UIManager>();
            AssignObject(ui, "drawingHintPanel", drawPanel);
            AssignObject(ui, "clearPanel", clearPanel);

            drawManager = canvasObject.AddComponent<DrawManager>();
            AssignObject(drawManager, "drawPanel", drawPanel);
            AssignObject(drawManager, "drawArea", drawArea);
            AssignObject(drawManager, "lineRoot", lineRoot);
            AssignObject(drawManager, "previewRoot", previewRoot);
            AssignObject(drawManager, "inkText", inkText);
            AssignObject(drawManager, "inkGaugeFill", inkGaugeFill);
            AssignObject(drawManager, "partText", partText);
            AssignObject(drawManager, "messageText", messageText);
            AssignObject(drawManager, "abilityText", abilityText);
            AssignFloat(drawManager, "maxInk", 350f);

            GameObject gameplayHud = CreateGameplayHud(canvasObject.transform, font, drawManager, stageManager);
            AssignObject(ui, "gameplayHudPanel", gameplayHud);
            GameObject titlePanel = CreateTitlePanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "titlePanel", titlePanel);
            GameObject multiPanel = CreateTitleMultiPanel(canvasObject.transform, font, stageManager, onlineManager);
            AssignObject(ui, "multiPanel", multiPanel);
            GameObject optionPanel = CreateTitleOptionPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "optionPanel", optionPanel);
            GameObject menuPanel = CreateMenuPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "menuPanel", menuPanel);
            GameObject stageSelectPanel = CreateStageSelectPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "stageSelectPanel", stageSelectPanel);
            GameObject stageEditorPanel = CreateRuntimeStageEditorPanel(canvasObject.transform, font, stageManager, out RectTransform editorUiBlocker, out Text editorStageText, out Text editorSelectedText, out Text editorStatusText, out Dropdown editorTypeDropdown);
            AssignObject(ui, "stageEditorPanel", stageEditorPanel);
            CreateDrawSpeciesPanel(drawPanel.transform, font, drawManager);

            runtimeStageEditor = canvasObject.AddComponent<RuntimeStageEditor>();
            AssignObject(runtimeStageEditor, "editorPanel", stageEditorPanel);
            AssignObject(runtimeStageEditor, "uiBlocker", editorUiBlocker);
            AssignObject(runtimeStageEditor, "stageManager", stageManager);
            AssignObject(runtimeStageEditor, "stageText", editorStageText);
            AssignObject(runtimeStageEditor, "selectedText", editorSelectedText);
            AssignObject(runtimeStageEditor, "statusText", editorStatusText);
            AssignObject(runtimeStageEditor, "objectTypeDropdown", editorTypeDropdown);
            RuntimeStageEditorButtonCommand[] editorCommands = stageEditorPanel.GetComponentsInChildren<RuntimeStageEditorButtonCommand>(true);
            for (int i = 0; i < editorCommands.Length; i++)
            {
                AssignObject(editorCommands[i], "editor", runtimeStageEditor);
            }

            AddDrawCommand(clearButton.gameObject, drawManager, DrawButtonCommand.Command.Clear);
            AddDrawCommand(decideButton.gameObject, drawManager, DrawButtonCommand.Command.Confirm);
            AddPartCommands(partButtons, drawManager);

            return ui;
        }

        private static void CreateDrawSpeciesPanel(Transform parent, Font font, DrawManager drawManager)
        {
            GameObject speciesPanel = CreatePanel("DrawSpeciesPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.86f));
            AddUiOutline(speciesPanel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform rect = speciesPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 22f);
            rect.sizeDelta = new Vector2(430f, 104f);

            Text title = CreateText("DrawSpeciesTitle", speciesPanel.transform, font, 18, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("character_switch");
            title.color = Color.black;
            AddLocalizedText(title.gameObject, "character_switch");
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            title.rectTransform.sizeDelta = new Vector2(0f, 26f);

            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                float x = -120f + i * 60f;
                Button button = CreateButton(
                    $"{species[i]}DrawSpeciesButton",
                    speciesPanel.transform,
                    font,
                    GetSpeciesIcon(species[i]),
                    new Vector2(x, 36f),
                    new Vector2(62f, 48f),
                    new Color(0.98f, 0.96f, 0.9f, 0.82f));
                SetButtonLabelColor(button, Color.black);

                Text label = CreateText($"{species[i]}DrawSpeciesLabel", speciesPanel.transform, font, 14, TextAnchor.MiddleCenter);
                label.text = GetSpeciesLabel(species[i]);
                label.color = Color.black;
                label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(x, 8f);
                label.rectTransform.sizeDelta = new Vector2(68f, 22f);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }
        }

        private static GameObject CreateMenuPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("MenuPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.94f));
            AddUiOutline(panel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 146f);
            rect.sizeDelta = new Vector2(260f, 220f);

            Text title = CreateText("MenuTitle", panel.transform, font, 22, TextAnchor.UpperCenter);
            title.text = "\u30e1\u30cb\u30e5\u30fc";
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            title.rectTransform.sizeDelta = new Vector2(0f, 34f);

            Text language = CreateText("LanguageTitle", panel.transform, font, 18, TextAnchor.MiddleCenter);
            language.text = "\u8a00\u8a9e\u8a2d\u5b9a";
            language.color = Color.black;
            language.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            language.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            language.rectTransform.pivot = new Vector2(0.5f, 0f);
            language.rectTransform.anchoredPosition = new Vector2(0f, 132f);
            language.rectTransform.sizeDelta = new Vector2(220f, 26f);

            Button japaneseButton = CreateButton("MenuJapaneseButton", panel.transform, font, "\u65e5\u672c\u8a9e", new Vector2(-58f, 78f), new Vector2(104f, 44f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            Button englishButton = CreateButton("MenuEnglishButton", panel.transform, font, "EN", new Vector2(58f, 78f), new Vector2(104f, 44f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            SetButtonLabelColor(japaneseButton, Color.black);
            SetButtonLabelColor(englishButton, Color.black);

            LanguageButtonCommand japaneseCommand = japaneseButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(japaneseCommand, "language", (int)LocalizationManager.Language.Japanese);

            LanguageButtonCommand englishCommand = englishButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(englishCommand, "language", (int)LocalizationManager.Language.English);

            Button stageSelectButton = CreateButton("MenuStageSelectButton", panel.transform, font, LocalizationManager.T("stage_select"), new Vector2(0f, 22f), new Vector2(220f, 44f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            SetButtonLabelColor(stageSelectButton, Color.black);
            AddLocalizedText(stageSelectButton.GetComponentInChildren<Text>().gameObject, "stage_select");
            AddGameplayCommand(stageSelectButton.gameObject, stageManager, GameplayButtonCommand.Command.StageSelect);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateStageSelectPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("StageSelectPanel", parent, new Color(0.965f, 0.945f, 0.88f, 0.98f));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Stretch(panelRect);

            Text title = CreateText("StageSelectTitle", panel.transform, font, 36, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("stage_select");
            title.color = Color.black;
            AddLocalizedText(title.gameObject, "stage_select");
            title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            title.rectTransform.sizeDelta = new Vector2(520f, 48f);

            Text help = CreateText("StageSelectHelp", panel.transform, font, 18, TextAnchor.UpperCenter);
            help.text = LocalizationManager.T("stage_select_help");
            help.color = Color.black;
            AddLocalizedText(help.gameObject, "stage_select_help");
            help.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            help.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(0f, -84f);
            help.rectTransform.sizeDelta = new Vector2(800f, 52f);

            Button debugButton = CreateButton("Stage_1_0_Button", panel.transform, font, LocalizationManager.T("debug_stage"), new Vector2(-470f, 560f), new Vector2(180f, 58f), new Color(0.72f, 0.95f, 0.72f, 0.95f));
            SetButtonLabelColor(debugButton, Color.black);
            AddLocalizedText(debugButton.GetComponentInChildren<Text>().gameObject, "debug_stage");
            AddStageSelectCommand(debugButton.gameObject, stageManager, "1-0");

            Button editButton = CreateButton("Stage_1_1_EditButton", panel.transform, font, LocalizationManager.T("stage_editor_open"), new Vector2(-260f, 560f), new Vector2(180f, 58f), new Color(0.78f, 0.9f, 1f, 0.95f));
            SetButtonLabelColor(editButton, Color.black);
            AddLocalizedText(editButton.GetComponentInChildren<Text>().gameObject, "stage_editor_open");
            AddStageEditCommand(editButton.gameObject, stageManager, "1-1");

            Button titleButton = CreateButton("StageSelectTitleBackButton", panel.transform, font, "TITLE", new Vector2(470f, 560f), new Vector2(160f, 58f), new Color(0.98f, 0.78f, 0.72f, 0.95f));
            SetButtonLabelColor(titleButton, Color.black);
            AddTitleCommand(titleButton.gameObject, stageManager, TitleButtonCommand.Command.Title);

            const int groups = 15;
            const int variants = 3;
            float startX = -470f;
            float startY = 472f;
            float groupSpacingX = 190f;
            float rowSpacingY = 118f;
            float buttonSpacingY = 34f;

            for (int group = 1; group <= groups; group++)
            {
                int column = (group - 1) % 5;
                int row = (group - 1) / 5;
                float x = startX + column * groupSpacingX;
                float y = startY - row * rowSpacingY;

                Text groupLabel = CreateText($"StageGroup{group}Label", panel.transform, font, 18, TextAnchor.MiddleCenter);
                groupLabel.text = $"{group}";
                groupLabel.color = Color.black;
                groupLabel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                groupLabel.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                groupLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                groupLabel.rectTransform.anchoredPosition = new Vector2(x, y + 38f);
                groupLabel.rectTransform.sizeDelta = new Vector2(160f, 24f);

                for (int variant = 1; variant <= variants; variant++)
                {
                    Button button = CreateButton(
                        $"Stage_{group}_{variant}_Button",
                        panel.transform,
                        font,
                        $"{group}-{variant}",
                        new Vector2(x, y - (variant - 1) * buttonSpacingY),
                        new Vector2(152f, 30f),
                        new Color(0.98f, 0.96f, 0.9f, 0.86f));
                    SetButtonLabelColor(button, Color.black);
                    AddStageSelectCommand(button.gameObject, stageManager, $"{group}-{variant}");
                }
            }

            Text locked = CreateText("LockedStageNote", panel.transform, font, 16, TextAnchor.MiddleCenter);
            locked.text = LocalizationManager.T("locked_stage");
            locked.color = new Color(0.25f, 0.25f, 0.25f);
            AddLocalizedText(locked.gameObject, "locked_stage");
            locked.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            locked.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            locked.rectTransform.pivot = new Vector2(0.5f, 0f);
            locked.rectTransform.anchoredPosition = new Vector2(0f, 34f);
            locked.rectTransform.sizeDelta = new Vector2(500f, 28f);

            return panel;
        }

        private static GameObject CreateRuntimeStageEditorPanel(Transform parent, Font font, StageManager stageManager, out RectTransform uiBlocker, out Text stageText, out Text selectedText, out Text statusText, out Dropdown objectTypeDropdown)
        {
            GameObject panel = new GameObject("RuntimeStageEditorPanel");
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            Stretch(panelRect);

            stageText = CreateText("RuntimeStageEditorTitle", panel.transform, font, 28, TextAnchor.UpperLeft);
            stageText.text = "Stage 1-1";
            stageText.color = Color.black;
            stageText.rectTransform.anchorMin = new Vector2(0f, 1f);
            stageText.rectTransform.anchorMax = new Vector2(0f, 1f);
            stageText.rectTransform.pivot = new Vector2(0f, 1f);
            stageText.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            stageText.rectTransform.sizeDelta = new Vector2(300f, 42f);

            GameObject toolPanel = CreatePanel("RuntimeStageEditorTools", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddUiOutline(toolPanel, new Color(0.12f, 0.11f, 0.1f, 0.72f), new Vector2(2f, -2f));
            RectTransform toolRect = toolPanel.GetComponent<RectTransform>();
            toolRect.anchorMin = new Vector2(1f, 1f);
            toolRect.anchorMax = new Vector2(1f, 1f);
            toolRect.pivot = new Vector2(1f, 1f);
            toolRect.anchoredPosition = new Vector2(-24f, -72f);
            toolRect.sizeDelta = new Vector2(304f, 420f);
            uiBlocker = toolRect;

            Text help = CreateText("RuntimeStageEditorHelp", toolPanel.transform, font, 15, TextAnchor.UpperLeft);
            help.text = LocalizationManager.T("stage_editor_help");
            help.color = Color.black;
            AddLocalizedText(help.gameObject, "stage_editor_help");
            help.rectTransform.anchorMin = new Vector2(0f, 1f);
            help.rectTransform.anchorMax = new Vector2(1f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            help.rectTransform.sizeDelta = new Vector2(-24f, 86f);

            Text typeLabel = CreateText("RuntimeStageEditorTypeLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            typeLabel.text = "\u7a2e\u5225";
            typeLabel.color = Color.black;
            typeLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            typeLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            typeLabel.rectTransform.pivot = new Vector2(0f, 1f);
            typeLabel.rectTransform.anchoredPosition = new Vector2(24f, -104f);
            typeLabel.rectTransform.sizeDelta = new Vector2(70f, 28f);

            objectTypeDropdown = CreateStageObjectDropdown(
                "RuntimeStageObjectTypeDropdown",
                toolPanel.transform,
                font,
                new Vector2(22f, -136f),
                new Vector2(260f, 42f));

            selectedText = CreateText("RuntimeStageEditorSelected", toolPanel.transform, font, 15, TextAnchor.UpperLeft);
            selectedText.text = "Add: Platform";
            selectedText.color = Color.black;
            selectedText.rectTransform.anchorMin = new Vector2(0f, 1f);
            selectedText.rectTransform.anchorMax = new Vector2(1f, 1f);
            selectedText.rectTransform.pivot = new Vector2(0.5f, 1f);
            selectedText.rectTransform.anchoredPosition = new Vector2(12f, -236f);
            selectedText.rectTransform.sizeDelta = new Vector2(-24f, 40f);

            Text sizeLabel = CreateText("RuntimeStageEditorSizeLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            sizeLabel.text = LocalizationManager.T("stage_editor_size");
            sizeLabel.color = Color.black;
            AddLocalizedText(sizeLabel.gameObject, "stage_editor_size");
            sizeLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            sizeLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            sizeLabel.rectTransform.pivot = new Vector2(0f, 1f);
            sizeLabel.rectTransform.anchoredPosition = new Vector2(24f, -246f);
            sizeLabel.rectTransform.sizeDelta = new Vector2(70f, 28f);

            Button widthMinus = CreateButton("RuntimeEditWidthMinus", toolPanel.transform, font, "Q W-", new Vector2(-100f, 116f), new Vector2(56f, 34f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            Button widthPlus = CreateButton("RuntimeEditWidthPlus", toolPanel.transform, font, "E W+", new Vector2(-34f, 116f), new Vector2(56f, 34f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            Button heightMinus = CreateButton("RuntimeEditHeightMinus", toolPanel.transform, font, "Z H-", new Vector2(34f, 116f), new Vector2(56f, 34f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            Button heightPlus = CreateButton("RuntimeEditHeightPlus", toolPanel.transform, font, "C H+", new Vector2(100f, 116f), new Vector2(56f, 34f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            SetButtonLabelColor(widthMinus, Color.black);
            SetButtonLabelColor(widthPlus, Color.black);
            SetButtonLabelColor(heightMinus, Color.black);
            SetButtonLabelColor(heightPlus, Color.black);
            AddRuntimeStageEditorCommand(widthMinus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.WidthMinus);
            AddRuntimeStageEditorCommand(widthPlus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.WidthPlus);
            AddRuntimeStageEditorCommand(heightMinus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.HeightMinus);
            AddRuntimeStageEditorCommand(heightPlus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.HeightPlus);

            Button snap = CreateButton("RuntimeEditSnapButton", toolPanel.transform, font, "G " + LocalizationManager.T("stage_editor_snap"), new Vector2(-78f, 68f), new Vector2(118f, 36f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            Button delete = CreateButton("RuntimeEditDeleteButton", toolPanel.transform, font, "Del " + LocalizationManager.T("stage_editor_delete"), new Vector2(78f, 68f), new Vector2(118f, 36f), new Color(0.98f, 0.78f, 0.72f, 0.92f));
            SetButtonLabelColor(snap, Color.black);
            SetButtonLabelColor(delete, Color.black);
            AddRuntimeStageEditorCommand(snap.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ToggleSnap);
            AddRuntimeStageEditorCommand(delete.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Delete);

            Button save = CreateButton("RuntimeEditSaveButton", toolPanel.transform, font, "F5 " + LocalizationManager.T("stage_editor_save"), new Vector2(-78f, 18f), new Vector2(118f, 38f), new Color(0.78f, 0.9f, 1f, 0.92f));
            Button test = CreateButton("RuntimeEditTestButton", toolPanel.transform, font, "F6 " + LocalizationManager.T("stage_editor_test"), new Vector2(78f, 18f), new Vector2(118f, 38f), new Color(0.76f, 0.95f, 0.76f, 0.92f));
            SetButtonLabelColor(save, Color.black);
            SetButtonLabelColor(test, Color.black);
            AddRuntimeStageEditorCommand(save.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Save);
            AddRuntimeStageEditorCommand(test.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Test);

            Button close = CreateButton("RuntimeEditCloseButton", toolPanel.transform, font, "Esc " + LocalizationManager.T("stage_editor_back"), new Vector2(0f, -32f), new Vector2(260f, 38f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            SetButtonLabelColor(close, Color.black);
            AddRuntimeStageEditorCommand(close.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Close);

            statusText = CreateText("RuntimeStageEditorStatus", panel.transform, font, 16, TextAnchor.LowerLeft);
            statusText.text = "";
            statusText.color = Color.black;
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(24f, 18f);
            statusText.rectTransform.sizeDelta = new Vector2(-48f, 30f);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateGameplayHud(Transform parent, Font font, DrawManager drawManager, StageManager stageManager)
        {
            GameObject hud = new GameObject("GameplayHud");
            hud.transform.SetParent(parent, false);
            RectTransform hudRect = hud.AddComponent<RectTransform>();
            Stretch(hudRect);

            GameObject speciesPanel = CreatePanel("GameplaySpeciesPanel", hud.transform, new Color(0.96f, 0.93f, 0.86f, 0.86f));
            AddUiOutline(speciesPanel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform speciesRect = speciesPanel.GetComponent<RectTransform>();
            speciesRect.anchorMin = new Vector2(0f, 0f);
            speciesRect.anchorMax = new Vector2(0f, 0f);
            speciesRect.pivot = new Vector2(0f, 0f);
            speciesRect.anchoredPosition = new Vector2(24f, 22f);
            speciesRect.sizeDelta = new Vector2(430f, 118f);

            Text title = CreateText("SpeciesPanelTitle", speciesPanel.transform, font, 20, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("character_switch");
            title.color = Color.black;
            AddLocalizedText(title.gameObject, "character_switch");
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            title.rectTransform.sizeDelta = new Vector2(0f, 28f);

            Button leftArrow = CreateButton("SpeciesLeftArrow", speciesPanel.transform, font, "\u25c0", new Vector2(-188f, 36f), new Vector2(42f, 54f), new Color(0.88f, 0.86f, 0.8f, 0.9f));
            Button rightArrow = CreateButton("SpeciesRightArrow", speciesPanel.transform, font, "\u25b6", new Vector2(188f, 36f), new Vector2(42f, 54f), new Color(0.88f, 0.86f, 0.8f, 0.9f));
            SetButtonLabelColor(leftArrow, Color.black);
            SetButtonLabelColor(rightArrow, Color.black);
            leftArrow.interactable = false;
            rightArrow.interactable = false;

            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                float x = -120f + i * 60f;
                Button button = CreateButton(
                    $"{species[i]}GameplaySpeciesButton",
                    speciesPanel.transform,
                    font,
                    GetSpeciesIcon(species[i]),
                    new Vector2(x, 44f),
                    new Vector2(54f, 48f),
                    new Color(0.98f, 0.96f, 0.9f, 0.82f));
                SetButtonLabelColor(button, Color.black);

                Text label = CreateText($"{species[i]}GameplaySpeciesLabel", speciesPanel.transform, font, 14, TextAnchor.MiddleCenter);
                label.text = GetSpeciesLabel(species[i]);
                label.color = Color.black;
                label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(x, 12f);
                label.rectTransform.sizeDelta = new Vector2(58f, 22f);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }

            GameObject actionPanel = CreatePanel("GameplayActionPanel", hud.transform, new Color(0.96f, 0.93f, 0.86f, 0.86f));
            AddUiOutline(actionPanel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform actionRect = actionPanel.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(1f, 0f);
            actionRect.anchorMax = new Vector2(1f, 0f);
            actionRect.pivot = new Vector2(1f, 0f);
            actionRect.anchoredPosition = new Vector2(-24f, 22f);
            actionRect.sizeDelta = new Vector2(420f, 174f);

            Button addCharacter = CreateButton("GameplayAddCharacterButton", actionPanel.transform, font, LocalizationManager.T("character_add"), new Vector2(-136f, 104f), new Vector2(118f, 44f), new Color(0.78f, 0.9f, 1f, 0.9f));
            Button deleteCharacter = CreateButton("GameplayDeleteCharacterButton", actionPanel.transform, font, LocalizationManager.T("character_delete"), new Vector2(0f, 104f), new Vector2(118f, 44f), new Color(0.98f, 0.78f, 0.72f, 0.9f));
            Button switchCharacter = CreateButton("GameplaySwitchCharacterButton", actionPanel.transform, font, LocalizationManager.T("character_control_switch"), new Vector2(136f, 104f), new Vector2(118f, 44f), new Color(0.75f, 0.95f, 0.75f, 0.9f));
            Button redraw = CreateButton("GameplayRedrawButton", actionPanel.transform, font, "\u270e\n" + LocalizationManager.T("redraw"), new Vector2(-136f, 18f), new Vector2(118f, 72f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            Button retry = CreateButton("GameplayRetryButton", actionPanel.transform, font, "\u21bb\n" + LocalizationManager.T("retry"), new Vector2(0f, 18f), new Vector2(118f, 72f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            Button menu = CreateButton("GameplayMenuButton", actionPanel.transform, font, "\u2630\n" + LocalizationManager.T("menu"), new Vector2(136f, 18f), new Vector2(118f, 72f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            SetButtonLabelColor(addCharacter, Color.black);
            SetButtonLabelColor(deleteCharacter, Color.black);
            SetButtonLabelColor(switchCharacter, Color.black);
            SetButtonLabelColor(redraw, Color.black);
            SetButtonLabelColor(retry, Color.black);
            SetButtonLabelColor(menu, Color.black);

            AddLocalizedText(addCharacter.GetComponentInChildren<Text>().gameObject, "character_add");
            AddLocalizedText(deleteCharacter.GetComponentInChildren<Text>().gameObject, "character_delete");
            AddLocalizedText(switchCharacter.GetComponentInChildren<Text>().gameObject, "character_control_switch");
            AddGameplayCommand(addCharacter.gameObject, stageManager, GameplayButtonCommand.Command.AddCharacter);
            AddGameplayCommand(deleteCharacter.gameObject, stageManager, GameplayButtonCommand.Command.DeleteCharacter);
            AddGameplayCommand(switchCharacter.gameObject, stageManager, GameplayButtonCommand.Command.SwitchCharacter);
            AddGameplayCommand(redraw.gameObject, stageManager, GameplayButtonCommand.Command.Redraw);
            AddGameplayCommand(retry.gameObject, stageManager, GameplayButtonCommand.Command.Retry);
            AddGameplayCommand(menu.gameObject, stageManager, GameplayButtonCommand.Command.Menu);

            return hud;
        }

        private static GameObject CreateTitlePanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = new GameObject("TitlePanel");
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            Stretch(rect);

            Text title = CreateText("TitleLogo", panel.transform, font, 58, TextAnchor.UpperCenter);
            title.text = "DRAW BODY";
            title.color = new Color(0.04f, 0.04f, 0.04f);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            title.rectTransform.sizeDelta = new Vector2(0f, 72f);

            Text subtitle = CreateText("TitleSubtitle", panel.transform, font, 22, TextAnchor.UpperCenter);
            subtitle.text = "walk, jump, draw, and toss";
            subtitle.color = new Color(0.18f, 0.18f, 0.16f, 0.85f);
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -108f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 34f);

            GameObject bar = CreatePanel("TitleMenuBar", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddUiOutline(bar, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 0f);
            barRect.sizeDelta = new Vector2(0f, 88f);

            AddTitleMenuButton("TitleSingleButton", bar.transform, font, "SINGLE", new Vector2(-300f, 18f), stageManager, TitleButtonCommand.Command.Single);
            AddTitleMenuButton("TitleMultiButton", bar.transform, font, "MULTI", new Vector2(-150f, 18f), stageManager, TitleButtonCommand.Command.Multi);
            AddTitleMenuButton("TitleDrawButton", bar.transform, font, "DRAW", new Vector2(0f, 18f), stageManager, TitleButtonCommand.Command.Draw);
            AddTitleMenuButton("TitleOptionButton", bar.transform, font, "OPTION", new Vector2(150f, 18f), stageManager, TitleButtonCommand.Command.Option);
            AddTitleMenuButton("TitleExitButton", bar.transform, font, "EXIT", new Vector2(300f, 18f), stageManager, TitleButtonCommand.Command.Exit);

            return panel;
        }

        private static GameObject CreateTitleMultiPanel(Transform parent, Font font, StageManager stageManager, OnlineManager onlineManager)
        {
            GameObject panel = CreatePanel("TitleMultiPanel", parent, new Color(0.965f, 0.945f, 0.88f, 0.78f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            Stretch(rect);

            GameObject choice = CreateMultiScreen("MultiChoiceScreen", panel.transform, font, "MULTI PLAY");
            AddMultiLargeButton("MultiRandomButton", choice.transform, font, "ランダムマッチ\nすぐに遊ぶ", new Vector2(0f, 336f), MultiMenuButtonCommand.Command.Random);
            AddMultiLargeButton("MultiRoomButton", choice.transform, font, "ルーム\n友達と遊ぶ", new Vector2(0f, 236f), MultiMenuButtonCommand.Command.Room);
            AddMultiSmallButton("MultiBackTitleButton", choice.transform, font, "戻る", new Vector2(0f, 126f), MultiMenuButtonCommand.Command.BackToTitle, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject random = CreateMultiScreen("MultiRandomScreen", panel.transform, font, "ランダムマッチ");
            Text randomStatus = CreateMultiBodyText("MultiRandomStatus", random.transform, font, "ランダムマッチ中...\n\n参加人数 2 / 4\n\n○ あなた\n○ Player2\n□ 募集中\n□ 募集中\n\n[READY] で開始待ち");
            AddMultiSmallButton("MultiRandomReadyButton", random.transform, font, "READY", new Vector2(-92f, 96f), MultiMenuButtonCommand.Command.Ready, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiRandomCancelButton", random.transform, font, "キャンセル", new Vector2(92f, 96f), MultiMenuButtonCommand.Command.Choice, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject room = CreateMultiScreen("MultiRoomScreen", panel.transform, font, "ROOM");
            AddMultiLargeButton("MultiCreateRoomNavButton", room.transform, font, "ルームを作る", new Vector2(0f, 330f), MultiMenuButtonCommand.Command.CreateRoom);
            AddMultiLargeButton("MultiJoinRoomNavButton", room.transform, font, "ルームに入る", new Vector2(0f, 244f), MultiMenuButtonCommand.Command.JoinRoom);
            AddMultiSmallButton("MultiRoomBackButton", room.transform, font, "戻る", new Vector2(0f, 126f), MultiMenuButtonCommand.Command.Choice, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject create = CreateMultiScreen("MultiCreateRoomScreen", panel.transform, font, "ルーム作成");
            CreateMultiBodyText("MultiCreateRoomBody", create.transform, font, "ルーム名\n[ みんなで落書き ]\n\n最大人数\n< 4人 >\n\n公開設定\n[公開]   [非公開]\n\nステージ\n[ホストが選ぶ]\n\n描き直し中の挙動\n[全員一時停止]");
            AddMultiSmallButton("MultiCreateButton", create.transform, font, "作成", new Vector2(-92f, 76f), MultiMenuButtonCommand.Command.CreateRoomAction, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiCreateBackButton", create.transform, font, "戻る", new Vector2(92f, 76f), MultiMenuButtonCommand.Command.Room, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject join = CreateMultiScreen("MultiJoinRoomScreen", panel.transform, font, "ルームに入る");
            CreateMultiBodyText("MultiJoinRoomBody", join.transform, font, "ルームID\n[ ABC123 ]\n\nルーム一覧\n\nみんなで遊ぼう    2/4\n初心者歓迎        1/4\n変な体部屋        3/4");
            AddMultiSmallButton("MultiJoinButton", join.transform, font, "参加", new Vector2(-150f, 76f), MultiMenuButtonCommand.Command.JoinRoomAction, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiRefreshButton", join.transform, font, "更新", new Vector2(0f, 76f), MultiMenuButtonCommand.Command.JoinRoom, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiJoinBackButton", join.transform, font, "戻る", new Vector2(150f, 76f), MultiMenuButtonCommand.Command.Room, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject lobby = CreateMultiScreen("MultiLobbyScreen", panel.transform, font, "ROOM LOBBY");
            Text lobbyStatus = CreateMultiBodyText("MultiLobbyStatus", lobby.transform, font, "Room: みんなで落書き\nID: ABC123\n2 / 4\n\nプレイヤーが動けるロビー\n箱・ボール・ジャンプ台で待機中に遊べます");
            AddMultiSmallButton("MultiLobbyDrawButton", lobby.transform, font, "DRAW", new Vector2(-222f, 76f), MultiMenuButtonCommand.Command.Draw, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiLobbyReadyButton", lobby.transform, font, "READY", new Vector2(-74f, 76f), MultiMenuButtonCommand.Command.Ready, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiLobbyStageButton", lobby.transform, font, "STAGE", new Vector2(74f, 76f), MultiMenuButtonCommand.Command.Lobby, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiLobbyExitButton", lobby.transform, font, "退出", new Vector2(222f, 76f), MultiMenuButtonCommand.Command.LeaveLobby, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            MultiMenuController controller = panel.AddComponent<MultiMenuController>();
            AssignObject(controller, "onlineManager", onlineManager);
            AssignObject(controller, "choiceScreen", choice);
            AssignObject(controller, "randomScreen", random);
            AssignObject(controller, "roomScreen", room);
            AssignObject(controller, "createRoomScreen", create);
            AssignObject(controller, "joinRoomScreen", join);
            AssignObject(controller, "lobbyScreen", lobby);
            AssignObject(controller, "randomStatusText", randomStatus);
            AssignObject(controller, "lobbyStatusText", lobbyStatus);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateTitleOptionPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("TitleOptionPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.94f));
            AddUiOutline(panel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(420f, 350f);

            Text title = CreateText("TitleOptionTitle", panel.transform, font, 30, TextAnchor.UpperCenter);
            title.text = "OPTION";
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            title.rectTransform.sizeDelta = new Vector2(0f, 44f);

            Text body = CreateText("TitleOptionBody", panel.transform, font, 22, TextAnchor.MiddleLeft);
            body.text = "BGM        □□□□□\nSE         □□□□□\nVibration  ON / OFF\nKeys       later\nLanguage   Menu";
            body.color = Color.black;
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(28f, 10f);
            body.rectTransform.sizeDelta = new Vector2(-92f, -112f);

            Button back = CreateButton("TitleOptionBackButton", panel.transform, font, "Back", new Vector2(0f, 22f), new Vector2(180f, 44f), new Color(0.98f, 0.78f, 0.72f, 0.92f));
            SetButtonLabelColor(back, Color.black);
            AddTitleCommand(back.gameObject, stageManager, TitleButtonCommand.Command.Back);

            panel.SetActive(false);
            return panel;
        }

        private static void AddTitleMenuButton(string name, Transform parent, Font font, string label, Vector2 position, StageManager stageManager, TitleButtonCommand.Command command)
        {
            Button button = CreateButton(name, parent, font, label, position, new Vector2(128f, 50f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            SetButtonLabelColor(button, Color.black);
            AddTitleCommand(button.gameObject, stageManager, command);
        }

        private static GameObject CreateMultiScreen(string name, Transform parent, Font font, string titleText)
        {
            GameObject screen = new GameObject(name);
            screen.transform.SetParent(parent, false);
            RectTransform screenRect = screen.AddComponent<RectTransform>();
            Stretch(screenRect);

            GameObject note = CreatePanel(name + "Note", screen.transform, new Color(0.96f, 0.93f, 0.86f, 0.96f));
            AddUiOutline(note, new Color(0.12f, 0.11f, 0.1f, 0.8f), new Vector2(2f, -2f));
            RectTransform noteRect = note.GetComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0.5f, 0.5f);
            noteRect.anchorMax = new Vector2(0.5f, 0.5f);
            noteRect.pivot = new Vector2(0.5f, 0.5f);
            noteRect.anchoredPosition = new Vector2(0f, 22f);
            noteRect.sizeDelta = new Vector2(560f, 500f);

            Text title = CreateText(name + "Title", note.transform, font, 34, TextAnchor.UpperCenter);
            title.text = titleText;
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            title.rectTransform.sizeDelta = new Vector2(0f, 48f);

            return note;
        }

        private static Text CreateMultiBodyText(string name, Transform parent, Font font, string bodyText)
        {
            Text body = CreateText(name, parent, font, 21, TextAnchor.MiddleCenter);
            body.text = bodyText;
            body.color = Color.black;
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            body.rectTransform.sizeDelta = new Vector2(-92f, -150f);
            return body;
        }

        private static void AddMultiLargeButton(string name, Transform parent, Font font, string label, Vector2 position, MultiMenuButtonCommand.Command command)
        {
            Button button = CreateButton(name, parent, font, label, position, new Vector2(330f, 72f), new Color(0.98f, 0.96f, 0.9f, 0.94f));
            SetButtonLabelColor(button, Color.black);
            AddMultiCommand(button.gameObject, command);
        }

        private static void AddMultiSmallButton(string name, Transform parent, Font font, string label, Vector2 position, MultiMenuButtonCommand.Command command, Color color)
        {
            Button button = CreateButton(name, parent, font, label, position, new Vector2(132f, 46f), color);
            SetButtonLabelColor(button, Color.black);
            AddMultiCommand(button.gameObject, command);
        }

        private static GameObject CreateDrawPanel(
            Transform parent,
            Font font,
            StageManager stageManager,
            out RectTransform drawArea,
            out RectTransform lineRoot,
            out RectTransform previewRoot,
            out Text inkText,
            out Image inkGaugeFill,
            out Text partText,
            out Text messageText,
            out Text abilityText,
            out Button clearButton,
            out Button decideButton,
            out Button[] partButtons)
        {
            GameObject panel = CreatePanel("DrawPanel", parent, new Color(0.965f, 0.945f, 0.88f, 0.96f));

            Text title = CreateText("DrawTitle", panel.transform, font, 24, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("draw_title");
            title.color = Color.black;
            AddLocalizedText(title.gameObject, "draw_title");
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(28f, -18f);
            title.rectTransform.sizeDelta = new Vector2(340f, 38f);

            Text help = CreateText("DrawHelp", panel.transform, font, 18, TextAnchor.UpperCenter);
            help.text = LocalizationManager.T("draw_help");
            help.color = Color.black;
            AddLocalizedText(help.gameObject, "draw_help");
            help.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            help.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(-60f, -588f);
            help.rectTransform.sizeDelta = new Vector2(420f, 78f);

            GameObject partBar = CreatePanel("PartButtonBar", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.78f));
            AddUiOutline(partBar, new Color(0.12f, 0.11f, 0.1f, 0.45f), new Vector2(1.5f, -1.5f));
            RectTransform partBarRect = partBar.GetComponent<RectTransform>();
            partBarRect.anchorMin = new Vector2(0.5f, 1f);
            partBarRect.anchorMax = new Vector2(0.5f, 1f);
            partBarRect.pivot = new Vector2(0.5f, 1f);
            partBarRect.anchoredPosition = new Vector2(-60f, -58f);
            partBarRect.sizeDelta = new Vector2(780f, 78f);
            partButtons = CreatePartButtons(partBar.transform, font);

            GameObject areaObject = CreatePanel("DrawArea", panel.transform, new Color(0.95f, 0.96f, 0.92f, 1f));
            AddUiOutline(areaObject, new Color(0.12f, 0.11f, 0.1f, 0.55f), new Vector2(2f, -2f));
            drawArea = areaObject.GetComponent<RectTransform>();
            drawArea.anchorMin = new Vector2(0.5f, 0.5f);
            drawArea.anchorMax = new Vector2(0.5f, 0.5f);
            drawArea.pivot = new Vector2(0.5f, 0.5f);
            drawArea.anchoredPosition = new Vector2(-205f, -72f);
            drawArea.sizeDelta = new Vector2(560f, 310f);

            GameObject lineRootObject = new GameObject("LineRoot");
            lineRootObject.transform.SetParent(areaObject.transform, false);
            lineRoot = lineRootObject.AddComponent<RectTransform>();
            Stretch(lineRoot);

            GameObject previewObject = CreatePanel("PreviewArea", panel.transform, new Color(0.9f, 0.93f, 0.95f, 1f));
            AddUiOutline(previewObject, new Color(0.12f, 0.11f, 0.1f, 0.55f), new Vector2(2f, -2f));
            RectTransform previewArea = previewObject.GetComponent<RectTransform>();
            previewArea.anchorMin = new Vector2(0.5f, 0.5f);
            previewArea.anchorMax = new Vector2(0.5f, 0.5f);
            previewArea.pivot = new Vector2(0.5f, 0.5f);
            previewArea.anchoredPosition = new Vector2(470f, -72f);
            previewArea.sizeDelta = new Vector2(240f, 310f);

            Text previewTitle = CreateText("PreviewTitle", previewObject.transform, font, 18, TextAnchor.UpperCenter);
            previewTitle.text = LocalizationManager.T("preview");
            previewTitle.color = Color.black;
            AddLocalizedText(previewTitle.gameObject, "preview");
            previewTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            previewTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            previewTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            previewTitle.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            previewTitle.rectTransform.sizeDelta = new Vector2(0f, 28f);

            GameObject previewRootObject = new GameObject("PreviewRoot");
            previewRootObject.transform.SetParent(previewObject.transform, false);
            previewRoot = previewRootObject.AddComponent<RectTransform>();
            previewRoot.anchorMin = new Vector2(0.5f, 0.5f);
            previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
            previewRoot.pivot = new Vector2(0.5f, 0.5f);
            previewRoot.anchoredPosition = new Vector2(0f, -12f);
            previewRoot.sizeDelta = new Vector2(180f, 260f);

            GameObject toolPanel = CreateDrawToolPanel(
                panel.transform,
                font,
                out inkGaugeFill,
                out Button penButton,
                out Button eraserButton,
                out Button undoButton,
                out Slider brushSizeSlider,
                out Text brushSizeValueText);
            RectTransform toolRect = toolPanel.GetComponent<RectTransform>();
            toolRect.anchoredPosition = new Vector2(140f, -72f);

            inkText = CreateText("InkText", toolPanel.transform, font, 16, TextAnchor.MiddleCenter);
            inkText.color = Color.black;
            inkText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            inkText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            inkText.rectTransform.pivot = new Vector2(0.5f, 1f);
            inkText.rectTransform.anchoredPosition = new Vector2(42f, -162f);
            inkText.rectTransform.sizeDelta = new Vector2(96f, 24f);

            partText = CreateText("PartText", panel.transform, font, 20, TextAnchor.MiddleLeft);
            partText.color = Color.black;
            partText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            partText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            partText.rectTransform.pivot = new Vector2(0.5f, 0f);
            partText.rectTransform.anchoredPosition = new Vector2(-500f, 642f);
            partText.rectTransform.sizeDelta = new Vector2(220f, 42f);
            partText.gameObject.SetActive(false);

            messageText = CreateText("ConnectionMessageText", panel.transform, font, 18, TextAnchor.MiddleCenter);
            messageText.text = LocalizationManager.T("msg_torso_first");
            messageText.color = Color.black;
            messageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            messageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            messageText.rectTransform.pivot = new Vector2(0.5f, 0f);
            messageText.rectTransform.anchoredPosition = new Vector2(-190f, 104f);
            messageText.rectTransform.sizeDelta = new Vector2(600f, 32f);
            messageText.gameObject.SetActive(false);

            abilityText = CreateText("AbilityPreviewText", panel.transform, font, 16, TextAnchor.MiddleCenter);
            abilityText.text = PlayerAbilityController.GetProfileSummary(new PlayerAbilityController.AbilityProfile());
            abilityText.color = Color.black;
            abilityText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            abilityText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            abilityText.rectTransform.pivot = new Vector2(0.5f, 0f);
            abilityText.rectTransform.anchoredPosition = new Vector2(430f, 112f);
            abilityText.rectTransform.sizeDelta = new Vector2(260f, 48f);
            abilityText.gameObject.SetActive(false);

            clearButton = toolPanel.transform.Find("ToolClearButton").GetComponent<Button>();
            decideButton = CreateButton("DecideButton", panel.transform, font, LocalizationManager.T("decide") + "\nENTER", new Vector2(420f, 24f), new Vector2(140f, 68f), new Color(0.75f, 0.95f, 0.75f, 0.9f));
            Button cancelButton = CreateButton("CancelDrawButton", panel.transform, font, LocalizationManager.T("cancel") + "\nESC", new Vector2(580f, 24f), new Vector2(140f, 68f), new Color(0.98f, 0.78f, 0.72f, 0.9f));
            SetButtonLabelColor(clearButton, Color.black);
            SetButtonLabelColor(decideButton, Color.black);
            SetButtonLabelColor(cancelButton, Color.black);
            AddGameplayCommand(cancelButton.gameObject, stageManager, GameplayButtonCommand.Command.CloseDrawing);
            AddDrawCommand(penButton.gameObject, null, DrawButtonCommand.Command.ToolMode, 0);
            AddDrawCommand(eraserButton.gameObject, null, DrawButtonCommand.Command.ToolMode, 1);
            AddDrawCommand(undoButton.gameObject, null, DrawButtonCommand.Command.Undo);
            AddBrushSizeSliderCommand(brushSizeSlider.gameObject, null, brushSizeValueText);

            for (int i = 0; i < partButtons.Length; i++)
            {
                partButtons[i].transform.SetAsLastSibling();
            }

            return panel;
        }

        private static Button[] CreatePartButtons(Transform parent, Font font)
        {
            DrawManager.BodyPart[] parts =
            {
                DrawManager.BodyPart.Head,
                DrawManager.BodyPart.Torso,
                DrawManager.BodyPart.LeftArm,
                DrawManager.BodyPart.RightArm,
                DrawManager.BodyPart.LeftLeg,
                DrawManager.BodyPart.RightLeg,
                DrawManager.BodyPart.LeftFrontLeg,
                DrawManager.BodyPart.RightFrontLeg,
                DrawManager.BodyPart.LeftBackLeg,
                DrawManager.BodyPart.RightBackLeg,
                DrawManager.BodyPart.Tail,
                DrawManager.BodyPart.LeftWing,
                DrawManager.BodyPart.RightWing,
                DrawManager.BodyPart.TailFeather,
                DrawManager.BodyPart.SlimeBody
            };

            Button[] buttons = new Button[parts.Length];
            int columns = 5;
            float spacingX = 118f;
            float spacingY = 42f;
            float startX = -spacingX * (columns - 1) * 0.5f;

            for (int i = 0; i < parts.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                string label = DrawManager.GetPartLabel(parts[i]);
                buttons[i] = CreateButton(
                    $"{parts[i]}Button",
                    parent,
                    font,
                    label,
                    new Vector2(startX + spacingX * column, -10f - spacingY * row),
                    new Vector2(108f, 36f),
                    parts[i] == DrawManager.BodyPart.Torso ? new Color(0.18f, 0.42f, 0.78f) : new Color(0.28f, 0.28f, 0.32f),
                    null,
                    true);
            }

            return buttons;
        }

        private static GameObject CreateDrawToolPanel(
            Transform parent,
            Font font,
            out Image inkGaugeFill,
            out Button penButton,
            out Button eraserButton,
            out Button undoButton,
            out Slider brushSizeSlider,
            out Text brushSizeValueText)
        {
            GameObject panel = CreatePanel("DrawToolPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddUiOutline(panel, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(2f, -2f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(170f, 310f);

            penButton = CreateButton("PenToolButton", panel.transform, font, LocalizationManager.T("pen"), new Vector2(-42f, 246f), new Vector2(70f, 38f), new Color(0.78f, 0.95f, 0.76f, 0.9f), "pen");
            eraserButton = CreateButton("EraserToolButton", panel.transform, font, LocalizationManager.T("eraser"), new Vector2(42f, 246f), new Vector2(76f, 38f), new Color(0.98f, 0.96f, 0.9f, 0.9f), "eraser");
            SetButtonLabelColor(penButton, Color.black);
            SetButtonLabelColor(eraserButton, Color.black);

            Text sizeTitle = CreateText("BrushSizeTitle", panel.transform, font, 16, TextAnchor.MiddleLeft);
            sizeTitle.text = LocalizationManager.T("brush_size");
            sizeTitle.color = Color.black;
            AddLocalizedText(sizeTitle.gameObject, "brush_size");
            sizeTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            sizeTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            sizeTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            sizeTitle.rectTransform.anchoredPosition = new Vector2(14f, -88f);
            sizeTitle.rectTransform.sizeDelta = new Vector2(-24f, 24f);

            brushSizeSlider = CreateBrushSlider("BrushSizeSlider", panel.transform, new Vector2(-12f, 174f), new Vector2(108f, 14f));
            brushSizeSlider.minValue = 3f;
            brushSizeSlider.maxValue = 10f;
            brushSizeSlider.value = 6f;
            brushSizeSlider.wholeNumbers = false;

            brushSizeValueText = CreateText("BrushSizeValueText", panel.transform, font, 14, TextAnchor.MiddleCenter);
            brushSizeValueText.text = "6px";
            brushSizeValueText.color = Color.black;
            brushSizeValueText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            brushSizeValueText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            brushSizeValueText.rectTransform.pivot = new Vector2(0.5f, 0f);
            brushSizeValueText.rectTransform.anchoredPosition = new Vector2(58f, 166f);
            brushSizeValueText.rectTransform.sizeDelta = new Vector2(48f, 28f);

            Text inkTitle = CreateText("InkUsageTitle", panel.transform, font, 16, TextAnchor.MiddleLeft);
            inkTitle.text = LocalizationManager.T("ink_usage");
            inkTitle.color = Color.black;
            AddLocalizedText(inkTitle.gameObject, "ink_usage");
            inkTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            inkTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            inkTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            inkTitle.rectTransform.anchoredPosition = new Vector2(14f, -142f);
            inkTitle.rectTransform.sizeDelta = new Vector2(-24f, 24f);

            GameObject gaugeBack = CreatePanel("InkGaugeBack", panel.transform, new Color(1f, 1f, 1f, 0.82f));
            RectTransform gaugeBackRect = gaugeBack.GetComponent<RectTransform>();
            gaugeBackRect.anchorMin = new Vector2(0.5f, 1f);
            gaugeBackRect.anchorMax = new Vector2(0.5f, 1f);
            gaugeBackRect.pivot = new Vector2(0.5f, 1f);
            gaugeBackRect.anchoredPosition = new Vector2(-24f, -176f);
            gaugeBackRect.sizeDelta = new Vector2(68f, 14f);

            GameObject gaugeFill = CreatePanel("InkGaugeFill", gaugeBack.transform, new Color(0.26f, 0.85f, 0.24f, 0.9f));
            inkGaugeFill = gaugeFill.GetComponent<Image>();
            inkGaugeFill.type = Image.Type.Filled;
            inkGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            inkGaugeFill.fillOrigin = 0;
            inkGaugeFill.fillAmount = 0f;

            Button clearButton = CreateButton("ToolClearButton", panel.transform, font, LocalizationManager.T("clear") + "     \u2672", new Vector2(0f, 86f), new Vector2(136f, 42f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            undoButton = CreateButton("ToolUndoButton", panel.transform, font, LocalizationManager.T("undo") + "  \u21b6", new Vector2(0f, 34f), new Vector2(136f, 42f), new Color(0.98f, 0.96f, 0.9f, 0.9f));
            SetButtonLabelColor(clearButton, Color.black);
            SetButtonLabelColor(undoButton, Color.black);

            return panel;
        }

        private static Slider CreateBrushSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            GameObject background = CreatePanel("Background", sliderObject.transform, new Color(1f, 1f, 1f, 0.85f));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, 4f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);

            GameObject fill = CreatePanel("Fill", fillArea.transform, new Color(0.42f, 0.86f, 0.42f, 0.9f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(10f, 4f);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = CreatePanel("Handle", handleArea.transform, new Color(0.08f, 0.08f, 0.08f, 1f));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(10f, 22f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size, Color color, string localizationKey = null, bool anchorToTop = false)
        {
            GameObject buttonObject = CreatePanel(name, parent, color);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchorMax = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.pivot = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Button button = buttonObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.15f;
            colors.pressedColor = color * 0.8f;
            button.colors = colors;

            Text text = CreateText("Label", buttonObject.transform, font, 20, TextAnchor.MiddleCenter);
            text.text = string.IsNullOrEmpty(localizationKey) ? label : LocalizationManager.T(localizationKey);
            text.color = Color.white;
            Stretch(text.rectTransform);
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(text.gameObject, localizationKey);
            }
            return button;
        }

        private static Dropdown CreateStageObjectDropdown(string name, Transform parent, Font font, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject dropdownObject = CreatePanel(name, parent, new Color(0.98f, 0.96f, 0.9f, 0.96f));
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            AddUiOutline(dropdownObject, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));

            Dropdown dropdown = dropdownObject.AddComponent<Dropdown>();
            Navigation navigation = dropdown.navigation;
            navigation.mode = Navigation.Mode.None;
            dropdown.navigation = navigation;
            dropdown.targetGraphic = dropdownObject.GetComponent<Image>();

            Text caption = CreateText("Label", dropdownObject.transform, font, 18, TextAnchor.MiddleLeft);
            caption.color = Color.black;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(12f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-42f, 0f);
            dropdown.captionText = caption;

            Text arrow = CreateText("Arrow", dropdownObject.transform, font, 18, TextAnchor.MiddleCenter);
            arrow.text = "\u25be";
            arrow.color = Color.black;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            arrow.rectTransform.sizeDelta = new Vector2(28f, 0f);

            GameObject template = CreatePanel("Template", dropdownObject.transform, new Color(0.96f, 0.93f, 0.86f, 0.98f));
            RectTransform templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 190f);
            AddUiOutline(template, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));

            GameObject viewport = CreatePanel("Viewport", template.transform, new Color(1f, 1f, 1f, 0.01f));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 190f);

            ScrollRect scrollRect = template.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            GameObject item = CreatePanel("Item", content.transform, new Color(0.98f, 0.96f, 0.9f, 0.98f));
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 34f);

            Toggle itemToggle = item.AddComponent<Toggle>();
            itemToggle.targetGraphic = item.GetComponent<Image>();
            ColorBlock toggleColors = itemToggle.colors;
            toggleColors.normalColor = new Color(0.98f, 0.96f, 0.9f, 0.98f);
            toggleColors.highlightedColor = new Color(0.88f, 0.95f, 0.88f, 0.98f);
            toggleColors.pressedColor = new Color(0.78f, 0.88f, 0.78f, 0.98f);
            itemToggle.colors = toggleColors;

            Text itemLabel = CreateText("Item Label", item.transform, font, 17, TextAnchor.MiddleLeft);
            itemLabel.color = Color.black;
            itemLabel.rectTransform.anchorMin = Vector2.zero;
            itemLabel.rectTransform.anchorMax = Vector2.one;
            itemLabel.rectTransform.offsetMin = new Vector2(12f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
            dropdown.itemText = itemLabel;

            StageObjectType[] paletteTypes =
            {
                StageObjectType.Platform,
                StageObjectType.Wall,
                StageObjectType.Spawn,
                StageObjectType.Goal,
                StageObjectType.BalanceScale,
                StageObjectType.Weight
            };
            System.Collections.Generic.List<Dropdown.OptionData> options = new System.Collections.Generic.List<Dropdown.OptionData>();
            for (int i = 0; i < paletteTypes.Length; i++)
            {
                options.Add(new Dropdown.OptionData(LocalizationManager.T(GetStageObjectLocalizationKey(paletteTypes[i]))));
            }

            dropdown.options = options;
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            dropdown.template = templateRect;
            template.SetActive(false);
            return dropdown;
        }

        private static void CreateLanguageButtons(Transform parent, Font font)
        {
            Button japaneseButton = CreateButton("JapaneseButton", parent, font, "\u65e5\u672c\u8a9e", new Vector2(500f, -12f), new Vector2(96f, 36f), new Color(0.18f, 0.42f, 0.78f), "lang_ja", true);
            Button englishButton = CreateButton("EnglishButton", parent, font, "EN", new Vector2(608f, -12f), new Vector2(70f, 36f), new Color(0.28f, 0.28f, 0.32f), "lang_en", true);

            LanguageButtonCommand japaneseCommand = japaneseButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(japaneseCommand, "language", (int)LocalizationManager.Language.Japanese);

            LanguageButtonCommand englishCommand = englishButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(englishCommand, "language", (int)LocalizationManager.Language.English);
        }

        private static void CreateSpeciesButtons(Transform parent, Font font, DrawManager drawManager)
        {
            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                Button button = CreateButton(
                    $"{species[i]}SpeciesButton",
                    parent,
                    font,
                    GetSpeciesLabel(species[i]),
                    new Vector2(-210f + i * 86f, -12f),
                    new Vector2(78f, 32f),
                    new Color(0.2f, 0.32f, 0.42f),
                    null,
                    true);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }
        }

        private static string GetSpeciesLabel(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat:
                    return LocalizationManager.T("cat");
                case DrawManager.Species.Bird:
                    return LocalizationManager.T("bird");
                case DrawManager.Species.Snake:
                    return LocalizationManager.T("snake");
                case DrawManager.Species.Slime:
                    return LocalizationManager.T("slime");
                default:
                    return LocalizationManager.T("human");
            }
        }

        private static string GetSpeciesIcon(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat:
                    return "=^.^=";
                case DrawManager.Species.Bird:
                    return "v";
                case DrawManager.Species.Snake:
                    return "S";
                case DrawManager.Species.Slime:
                    return "o";
                default:
                    return "o\n/|\\\n/ \\";
            }
        }

        private static void AddLocalizedText(GameObject gameObject, string localizationKey)
        {
            LocalizedText localizedText = gameObject.AddComponent<LocalizedText>();
            AssignString(localizedText, "key", localizationKey);
        }

        private static string GetPartKey(DrawManager.BodyPart part)
        {
            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    return "head";
                case DrawManager.BodyPart.Torso:
                    return "torso";
                case DrawManager.BodyPart.LeftArm:
                    return "left_arm";
                case DrawManager.BodyPart.RightArm:
                    return "right_arm";
                case DrawManager.BodyPart.LeftLeg:
                    return "left_leg";
                case DrawManager.BodyPart.RightLeg:
                    return "right_leg";
                case DrawManager.BodyPart.LeftFrontLeg:
                    return "left_front_leg";
                case DrawManager.BodyPart.RightFrontLeg:
                    return "right_front_leg";
                case DrawManager.BodyPart.LeftBackLeg:
                    return "left_back_leg";
                case DrawManager.BodyPart.RightBackLeg:
                    return "right_back_leg";
                case DrawManager.BodyPart.Tail:
                    return "tail";
                case DrawManager.BodyPart.LeftWing:
                    return "left_wing";
                case DrawManager.BodyPart.RightWing:
                    return "right_wing";
                case DrawManager.BodyPart.TailFeather:
                    return "tail_feather";
                case DrawManager.BodyPart.SlimeBody:
                    return "slime_body";
                default:
                    return string.Empty;
            }
        }

        private static void AddDrawCommand(GameObject buttonObject, DrawManager drawManager, DrawButtonCommand.Command command, int intValue = 0)
        {
            DrawButtonCommand buttonCommand = buttonObject.AddComponent<DrawButtonCommand>();
            AssignObject(buttonCommand, "drawManager", drawManager);
            AssignEnum(buttonCommand, "command", (int)command);
            AssignInt(buttonCommand, "intValue", intValue);
        }

        private static void AddBrushSizeSliderCommand(GameObject sliderObject, DrawManager drawManager, Text valueText)
        {
            BrushSizeSliderCommand sliderCommand = sliderObject.AddComponent<BrushSizeSliderCommand>();
            AssignObject(sliderCommand, "drawManager", drawManager);
            AssignObject(sliderCommand, "valueText", valueText);
        }

        private static void AddStageSelectCommand(GameObject buttonObject, StageManager stageManager, string stageId)
        {
            StageSelectButtonCommand command = buttonObject.AddComponent<StageSelectButtonCommand>();
            AssignObject(command, "stageManager", stageManager);
            AssignString(command, "stageId", stageId);
        }

        private static void AddStageEditCommand(GameObject buttonObject, StageManager stageManager, string stageId)
        {
            StageEditButtonCommand command = buttonObject.AddComponent<StageEditButtonCommand>();
            AssignObject(command, "stageManager", stageManager);
            AssignString(command, "stageId", stageId);
        }

        private static void AddGameplayCommand(GameObject buttonObject, StageManager stageManager, GameplayButtonCommand.Command command)
        {
            GameplayButtonCommand buttonCommand = buttonObject.AddComponent<GameplayButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddTitleCommand(GameObject buttonObject, StageManager stageManager, TitleButtonCommand.Command command, Text statusText = null)
        {
            TitleButtonCommand buttonCommand = buttonObject.AddComponent<TitleButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            if (statusText != null)
            {
                AssignObject(buttonCommand, "statusText", statusText);
            }

            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddMultiCommand(GameObject buttonObject, MultiMenuButtonCommand.Command command)
        {
            MultiMenuButtonCommand buttonCommand = buttonObject.AddComponent<MultiMenuButtonCommand>();
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static void AddRuntimeStageEditorCommand(GameObject buttonObject, StageManager stageManager, RuntimeStageEditorButtonCommand.Command command)
        {
            RuntimeStageEditorButtonCommand buttonCommand = buttonObject.AddComponent<RuntimeStageEditorButtonCommand>();
            AssignObject(buttonCommand, "stageManager", stageManager);
            AssignEnum(buttonCommand, "command", (int)command);
        }

        private static string GetStageObjectLocalizationKey(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Wall:
                    return "stage_object_wall";
                case StageObjectType.Spawn:
                    return "stage_object_spawn";
                case StageObjectType.Goal:
                    return "stage_object_goal";
                case StageObjectType.BalanceScale:
                    return "stage_object_balance";
                case StageObjectType.Weight:
                    return "stage_object_weight";
                default:
                    return "stage_object_platform";
            }
        }

        private static void AddPartCommands(Button[] partButtons, DrawManager drawManager)
        {
            DrawManager.BodyPart[] parts =
            {
                DrawManager.BodyPart.Head,
                DrawManager.BodyPart.Torso,
                DrawManager.BodyPart.LeftArm,
                DrawManager.BodyPart.RightArm,
                DrawManager.BodyPart.LeftLeg,
                DrawManager.BodyPart.RightLeg,
                DrawManager.BodyPart.LeftFrontLeg,
                DrawManager.BodyPart.RightFrontLeg,
                DrawManager.BodyPart.LeftBackLeg,
                DrawManager.BodyPart.RightBackLeg,
                DrawManager.BodyPart.Tail,
                DrawManager.BodyPart.LeftWing,
                DrawManager.BodyPart.RightWing,
                DrawManager.BodyPart.TailFeather,
                DrawManager.BodyPart.SlimeBody
            };

            for (int i = 0; i < partButtons.Length && i < parts.Length; i++)
            {
                PartButtonCommand command = partButtons[i].gameObject.AddComponent<PartButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "bodyPart", (int)parts[i]);
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            Stretch(image.rectTransform);
            return panel;
        }

        private static void AddUiOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private static void SetButtonLabelColor(Button button, Color color)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = color;
            }
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font CreateDefaultFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Arial" }, 18);
            if (font != null)
            {
                return font;
            }

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject CreateSpriteBox(string name, Vector3 position, Vector2 size, Color color, Transform parent, Sprite squareSprite)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = color;

            return obj;
        }

        private static void AddSketchBoxOutline(Transform parent, Vector2 size, Color color, float width, int sortingOrder, Vector3 offset = default)
        {
            Vector3[] points =
            {
                offset + new Vector3(-0.52f, -0.48f, 0f),
                offset + new Vector3(-0.49f, 0.53f, 0f),
                offset + new Vector3(0.51f, 0.50f, 0f),
                offset + new Vector3(0.53f, -0.51f, 0f),
                offset + new Vector3(-0.52f, -0.48f, 0f)
            };
            AddDoodleLine("Sketch Outline A", parent, points, color, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), sortingOrder);

            Vector3[] loosePoints =
            {
                offset + new Vector3(-0.50f, -0.52f, 0f),
                offset + new Vector3(-0.54f, 0.48f, 0f),
                offset + new Vector3(0.49f, 0.54f, 0f),
                offset + new Vector3(0.50f, -0.47f, 0f),
                offset + new Vector3(-0.50f, -0.52f, 0f)
            };
            AddDoodleLine("Sketch Outline B", parent, loosePoints, color * 0.9f, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), sortingOrder + 1);
        }

        private static void AddDoorDoodle(Transform parent)
        {
            Vector3[] door =
            {
                new Vector3(-0.28f, -0.48f, 0f),
                new Vector3(-0.28f, 0.44f, 0f),
                new Vector3(0.28f, 0.44f, 0f),
                new Vector3(0.28f, -0.48f, 0f)
            };
            AddDoodleLine("Goal Door Frame", parent, door, Color.black, 0.055f, 25);
            AddDoodleLine("Goal Door Top Scribble", parent, new[] { new Vector3(-0.34f, 0.48f, 0f), new Vector3(0.34f, 0.48f, 0f) }, Color.black, 0.055f, 25);
            AddDoodleLine("Goal Shine A", parent, new[] { new Vector3(-0.6f, 0.35f, 0f), new Vector3(-0.78f, 0.52f, 0f) }, new Color(1f, 0.75f, 0f), 0.035f, 26);
            AddDoodleLine("Goal Shine B", parent, new[] { new Vector3(0.58f, 0.35f, 0f), new Vector3(0.82f, 0.48f, 0f) }, new Color(1f, 0.75f, 0f), 0.035f, 26);
        }

        private static TextMesh CreateDoodleText(string name, string text, Vector3 position, Transform parent, Font font, int fontSize, float characterSize, Color color, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            textObject.transform.position = position;

            TextMesh mesh = textObject.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = fontSize;
            mesh.characterSize = characterSize;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;

            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 35;
            }

            return mesh;
        }

        private static LineRenderer AddDoodleLine(string name, Transform parent, Vector3[] points, Color color, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = GetDoodleLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private static void AddDoodleCircle(string name, Transform parent, float radius, Color color, float width, int sortingOrder)
        {
            AddDoodleCircle(name, parent, radius, color, width, sortingOrder, Vector3.zero);
        }

        private static void AddDoodleCircle(string name, Transform parent, float radius, Color color, float width, int sortingOrder, Vector3 center)
        {
            const int segments = 32;
            Vector3[] points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float wobble = 1f + Mathf.Sin(i * 2.17f) * 0.05f;
                points[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * wobble;
            }

            AddDoodleLine(name, parent, points, color, width, sortingOrder);
        }

        private static Material GetDoodleLineMaterial()
        {
            if (doodleLineMaterial != null)
            {
                return doodleLineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            doodleLineMaterial = new Material(shader);
            doodleLineMaterial.name = "Doodle Line Material";
            return doodleLineMaterial;
        }

        private static void SetSortingOrder(GameObject gameObject, int sortingOrder)
        {
            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

        private static Sprite CreateSquareSprite()
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareTexturePath);
            if (existing != null)
            {
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Generated");
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "GeneratedSquareTexture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "GeneratedSquareSprite";
            AssetDatabase.CreateAsset(texture, SquareTexturePath);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssets();
            return sprite;
        }

        private static GameObject CreateMarker(string name, Vector3 localPosition, Transform parent)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPosition;
            return marker;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignLayerMask(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignVector2(Object target, string propertyName, Vector2 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.vector2Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignFloat(Object target, string propertyName, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(Object target, string propertyName, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInt(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignEnum(Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.enumValueIndex = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignString(Object target, string propertyName, string value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
        }
    }
}
