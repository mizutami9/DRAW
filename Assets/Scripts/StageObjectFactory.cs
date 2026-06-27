using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class StageObjectFactory : MonoBehaviour
    {
        [SerializeField] private int groundLayer = 6;
        [SerializeField] private int goalLayer = 8;
        [SerializeField] private int pushableLayer = 9;

        private static Material lineMaterial;
        private static Sprite squareSprite;

        public GameObject Create(StageObjectData data, Transform parent)
        {
            if (data == null)
            {
                return null;
            }

            switch (data.type)
            {
                case StageObjectType.Spawn:
                    return CreateMarker(data, parent, new Color(0.1f, 0.3f, 1f), "SPAWN");
                case StageObjectType.Goal:
                    return CreateGoal(data, parent);
                case StageObjectType.BalanceScale:
                    return CreateBalanceScale(data, parent);
                case StageObjectType.Weight:
                    return CreateWeight(data, parent);
                case StageObjectType.Wall:
                case StageObjectType.Platform:
                default:
                    return CreateSolid(data, parent);
            }
        }

        public static StageObjectData CreateDefaultData(StageObjectType type, Vector2 position)
        {
            Vector2 size;
            switch (type)
            {
                case StageObjectType.Wall:
                    size = new Vector2(0.55f, 2.2f);
                    break;
                case StageObjectType.Spawn:
                    size = new Vector2(0.7f, 0.7f);
                    break;
                case StageObjectType.Goal:
                    size = new Vector2(1.15f, 2.05f);
                    break;
                case StageObjectType.BalanceScale:
                    size = new Vector2(4.5f, 0.6f);
                    break;
                case StageObjectType.Weight:
                    size = new Vector2(0.9f, 0.9f);
                    break;
                default:
                    size = new Vector2(3f, 0.4f);
                    break;
            }

            return new StageObjectData
            {
                objectId = $"{type}_{System.Guid.NewGuid():N}".Substring(0, 14),
                type = type,
                position = position,
                size = size,
                rotation = 0f
            };
        }

        private GameObject CreateSolid(StageObjectData data, Transform parent)
        {
            GameObject obj = CreateBox(data.objectId, data.position, data.size, new Color(0.08f, 0.08f, 0.08f, 0.025f), parent);
            obj.name = data.type.ToString();
            obj.layer = groundLayer;
            obj.tag = "Ground";
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            AddPencilFillLocal(obj.transform, data.size, new Color(0.05f, 0.05f, 0.05f));
            AddSketchBoxOutline(obj.transform, data.size, Color.black, 0.055f);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateBalanceScale(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = "BalanceScale";
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            GameObject anchor = new GameObject("Pivot");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = Vector3.zero;
            Rigidbody2D anchorBody = anchor.AddComponent<Rigidbody2D>();
            anchorBody.bodyType = RigidbodyType2D.Static;

            Vector2 size = data.size;
            GameObject beam = CreateBox("Beam", Vector2.zero, new Vector2(size.x, Mathf.Max(0.18f, size.y * 0.28f)), new Color(0.08f, 0.08f, 0.08f, 0.04f), root.transform);
            beam.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            beam.layer = groundLayer;
            beam.tag = "Ground";

            Rigidbody2D beamBody = beam.AddComponent<Rigidbody2D>();
            beamBody.bodyType = RigidbodyType2D.Kinematic;
            beamBody.mass = 2.4f;
            beamBody.angularDamping = 1.3f;
            beamBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            beamBody.useFullKinematicContacts = true;

            BoxCollider2D beamCollider = beam.AddComponent<BoxCollider2D>();
            beamCollider.size = Vector2.one;

            AddPencilFillLocal(beam.transform, new Vector2(size.x, Mathf.Max(0.18f, size.y * 0.28f)), new Color(0.12f, 0.12f, 0.12f));
            AddSketchBoxOutline(beam.transform, new Vector2(size.x, Mathf.Max(0.18f, size.y * 0.28f)), Color.black, 0.055f);
            AddBalanceScaleStopper(beam.transform, -0.47f);
            AddBalanceScaleStopper(beam.transform, 0.47f);

            AddDoodleLine("Stand Left", root.transform, new[] { new Vector3(-0.35f, -0.55f, 0f), new Vector3(0f, 0.22f, 0f) }, Color.black, 0.045f, 18);
            AddDoodleLine("Stand Right", root.transform, new[] { new Vector3(0.35f, -0.55f, 0f), new Vector3(0f, 0.22f, 0f) }, Color.black, 0.045f, 18);
            AddDoodleLine("Stand Base", root.transform, new[] { new Vector3(-0.55f, -0.55f, 0f), new Vector3(0.55f, -0.55f, 0f) }, Color.black, 0.045f, 18);

            BalanceScale scale = root.AddComponent<BalanceScale>();
            scale.SetBeam(beamBody);
            BalanceScaleBeam beamLoadReporter = beam.AddComponent<BalanceScaleBeam>();
            beamLoadReporter.SetScale(scale);

            GameObject loadSensor = new GameObject("LoadSensor");
            loadSensor.transform.SetParent(beam.transform, false);
            loadSensor.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            BalanceScaleBeam sensorReporter = loadSensor.AddComponent<BalanceScaleBeam>();
            sensorReporter.SetScale(scale);
            BoxCollider2D sensorCollider = loadSensor.AddComponent<BoxCollider2D>();
            sensorCollider.isTrigger = true;
            sensorCollider.size = new Vector2(1f, 1.5f);

            BoxCollider2D editorCollider = root.AddComponent<BoxCollider2D>();
            editorCollider.size = new Vector2(Mathf.Max(1f, data.size.x), Mathf.Max(1f, data.size.y + 1.2f));
            editorCollider.isTrigger = true;
            AddEditorMetadata(root, data);
            return root;
        }

        private GameObject CreateWeight(StageObjectData data, Transform parent)
        {
            GameObject obj = CreateBox(data.objectId, data.position, data.size, new Color(0.35f, 0.35f, 0.35f, 0.12f), parent);
            obj.name = "Weight";
            obj.layer = pushableLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = 5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            obj.AddComponent<CarryableObject>();

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            AddPencilFillLocal(obj.transform, data.size, new Color(0.3f, 0.3f, 0.3f));
            AddSketchBoxOutline(obj.transform, data.size, Color.black, 0.055f);
            AddDoodleLine("Weight Handle", obj.transform, new[]
            {
                new Vector3(-0.22f, 0.52f, -0.01f),
                new Vector3(-0.12f, 0.68f, -0.01f),
                new Vector3(0.12f, 0.68f, -0.01f),
                new Vector3(0.22f, 0.52f, -0.01f)
            }, Color.black, 0.045f, 20);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddBalanceScaleStopper(Transform beam, float localX)
        {
            GameObject stopper = new GameObject(localX < 0f ? "Left Stopper" : "Right Stopper");
            stopper.transform.SetParent(beam, false);
            stopper.transform.localPosition = new Vector3(localX, 1.08f, -0.01f);
            stopper.transform.localRotation = Quaternion.identity;

            BoxCollider2D collider = stopper.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.04f, 0.85f);
            collider.offset = Vector2.zero;

            AddDoodleLine(
                stopper.name + " Front",
                stopper.transform,
                new[] { new Vector3(-0.02f, -0.42f, 0f), new Vector3(-0.02f, 0.42f, 0f) },
                Color.black,
                0.05f,
                16);
            AddDoodleLine(
                stopper.name + " Back",
                stopper.transform,
                new[] { new Vector3(0.02f, -0.4f, 0f), new Vector3(0.02f, 0.4f, 0f) },
                new Color(0.12f, 0.12f, 0.12f, 0.8f),
                0.026f,
                17);
        }

        private GameObject CreateGoal(StageObjectData data, Transform parent)
        {
            GameObject obj = CreateBox(data.objectId, data.position, data.size, new Color(0f, 0.85f, 0.35f, 0.1f), parent);
            obj.name = "Goal";
            obj.layer = goalLayer;
            obj.tag = "Goal";
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = true;
            obj.AddComponent<Goal>();

            AddPencilFillLocal(obj.transform, data.size, new Color(0f, 0.65f, 0.24f));
            AddSketchBoxOutline(obj.transform, data.size, Color.black, 0.055f);
            AddDoorDoodle(obj.transform);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateMarker(StageObjectData data, Transform parent, Color color, string label)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            AddDoodleCircle(obj.transform, 0.32f, color, 0.05f);

            TextMesh text = obj.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = 24;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            collider.isTrigger = true;

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddEditorMetadata(GameObject obj, StageObjectData data)
        {
            StageEditorObject marker = obj.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
        }

        private static GameObject CreateBox(string name, Vector2 position, Vector2 size, Color color, Transform parent)
        {
            GameObject obj = new GameObject(string.IsNullOrEmpty(name) ? "StageObject" : name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = 3;
            return obj;
        }

        private static void AddSketchBoxOutline(Transform parent, Vector2 size, Color color, float width)
        {
            Vector3[] points =
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.48f, 0f),
                new Vector3(0.48f, 0.5f, 0f),
                new Vector3(-0.49f, 0.48f, 0f),
                new Vector3(-0.5f, -0.5f, 0f)
            };
            AddDoodleLine("Outline", parent, points, color, width / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f), 12);
        }

        private static void AddDoorDoodle(Transform parent)
        {
            Vector3[] door =
            {
                new Vector3(-0.24f, -0.5f, -0.01f),
                new Vector3(-0.24f, 0.3f, -0.01f),
                new Vector3(0.24f, 0.3f, -0.01f),
                new Vector3(0.24f, -0.5f, -0.01f)
            };
            AddDoodleLine("Door", parent, door, Color.black, 0.045f, 15);
        }

        private static void AddPencilFillLocal(Transform parent, Vector2 size, Color color)
        {
            Color pencil = new Color(color.r, color.g, color.b, 0.22f);
            int index = 0;
            float inverseScale = 1f / Mathf.Max(Mathf.Max(size.x, size.y), 0.1f);

            for (int layer = 0; layer < 3; layer++)
            {
                float y = -0.42f + layer * 0.08f;
                while (y < 0.44f)
                {
                    float x = -0.52f + layer * 0.06f + Mathf.Sin(index * 1.3f) * 0.03f;
                    while (x < 0.5f)
                    {
                        Vector3 start = new Vector3(Mathf.Clamp(x, -0.5f, 0.5f), Mathf.Clamp(y + Mathf.Sin(index) * 0.03f, -0.48f, 0.48f), -0.02f);
                        Vector3 end = new Vector3(Mathf.Clamp(start.x + 0.22f + Mathf.Abs(Mathf.Sin(index * 0.7f)) * 0.18f, -0.5f, 0.5f), Mathf.Clamp(start.y + 0.25f, -0.48f, 0.48f), -0.02f);
                        AddDoodleLine($"Pencil {index}", parent, new[] { start, end }, pencil, 0.012f * inverseScale, 4);
                        x += 0.16f + Mathf.Abs(Mathf.Sin(index * 1.9f)) * 0.07f;
                        index++;
                    }

                    y += 0.17f;
                }
            }
        }

        private static void AddDoodleCircle(Transform parent, float radius, Color color, float width)
        {
            Vector3[] points = new Vector3[22];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                float angle = t * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(i * 1.7f) * 0.04f;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius * wobble;
            }

            AddDoodleLine("Circle", parent, points, color, width, 20);
        }

        private static void AddDoodleLine(string name, Transform parent, Vector3[] points, Color color, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.material = GetLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = sortingOrder;
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial != null)
            {
                return lineMaterial;
            }

            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            return lineMaterial;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null)
            {
                return squareSprite;
            }

            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return squareSprite;
        }
    }
}
