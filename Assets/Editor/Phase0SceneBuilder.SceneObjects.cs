using System.Collections.Generic;
using DrawBody.Prototype;
using UnityEngine;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
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
            AssignLayerMask(controller, "groundLayer", (1 << GroundLayer) | (1 << PushableLayer) | (1 << PlayerLayer));
            AssignVector2(controller, "groundCheckSize", new Vector2(1.2f, 0.18f));

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
            AssignFloat(carryController, "pickupReach", 0.9f);
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
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.97f, 0.96f, 0.91f);

            CameraFollow2D follow = cameraObject.AddComponent<CameraFollow2D>();
            AssignObject(follow, "target", player);
            return cameraObject;
        }

        private static void CreateNotebookBackdrop(Transform parent, Sprite squareSprite, Font font)
        {
            GameObject paper = CreateSpriteBox("Notebook Paper", new Vector3(80f, 8f, 1.8f), new Vector2(240f, 34f), new Color(0.985f, 0.975f, 0.93f), parent, squareSprite);
            SetSortingOrder(paper, -100);

            for (int i = 0; i < 57; i++)
            {
                float y = -8.8f + i * 0.6f;
                AddDoodleLine(
                    $"Notebook Rule {i}",
                    parent,
                    new[] { new Vector3(-40f, y, 1.7f), new Vector3(200f, y, 1.7f) },
                    new Color(0.35f, 0.68f, 0.95f, 0.35f),
                    0.018f,
                    -90);
            }

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
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

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
                            AppendEditorPencilQuad(
                                vertices, colors, triangles,
                                new Vector3(startX, startY, 0f),
                                new Vector3(endX, endY, 0f),
                                0.01f + layer * 0.002f,
                                layerColor);
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
                AppendEditorPencilQuad(
                    vertices, colors, triangles,
                    new Vector3(left + 0.1f, y + Mathf.Sin(i * 1.3f) * 0.025f, 0f),
                    new Vector3(right - 0.1f, y + Mathf.Cos(i * 1.9f) * 0.025f, 0f),
                    0.01f,
                    new Color(0.05f, 0.05f, 0.05f, 0.13f));
            }

            CreateEditorPencilMesh(name, parent, vertices, colors, triangles, 4);
        }

        private static void AddPencilFillLocal(string name, Transform parent, Vector2 size, Color pencilColor)
        {
            float inverseScale = 1f / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f);
            Color pencil = new Color(pencilColor.r, pencilColor.g, pencilColor.b, 0.28f);
            int index = 0;
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

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
                            AppendEditorPencilQuad(
                                vertices, colors, triangles,
                                new Vector3(startX, startY, 0f),
                                new Vector3(endX, endY, 0f),
                                (0.012f + layer * 0.003f) * inverseScale,
                                layerColor);
                        }

                        x += 0.12f + Mathf.Abs(Mathf.Sin(index * 2.1f)) * 0.08f;
                        index++;
                    }

                    row += 0.18f + layer * 0.025f;
                }
            }

            CreateEditorPencilMesh(name, parent, vertices, colors, triangles, 6);
        }

        private static void AppendEditorPencilQuad(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            float width,
            Color color)
        {
            Vector2 delta = (Vector2)(to - from);
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int first = vertices.Count;
            vertices.Add(from + (Vector3)normal);
            vertices.Add(from - (Vector3)normal);
            vertices.Add(to + (Vector3)normal);
            vertices.Add(to - (Vector3)normal);
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
            triangles.Add(first + 1);
        }

        private static void CreateEditorPencilMesh(
            string name,
            Transform parent,
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            int sortingOrder)
        {
            if (vertices.Count == 0)
            {
                return;
            }

            GameObject visual = new GameObject(name + " Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name + " Mesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetDoodleLineMaterial();
            renderer.sortingOrder = sortingOrder;
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
    }
}
