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
                case StageObjectType.JumpPad:
                    return CreateJumpPad(data, parent);
                case StageObjectType.Spawn:
                    return CreateMarker(data, parent, new Color(0.1f, 0.3f, 1f), "START");
                case StageObjectType.Goal:
                    return CreateGoal(data, parent);
                case StageObjectType.BalanceScale:
                case StageObjectType.Seesaw:
                case StageObjectType.Catapult:
                    return CreateBalanceScale(data, parent);
                case StageObjectType.Weight:
                case StageObjectType.WoodBox:
                case StageObjectType.IronBox:
                case StageObjectType.Ball:
                case StageObjectType.Barrel:
                case StageObjectType.Rock:
                case StageObjectType.IceBlock:
                case StageObjectType.FloatingBox:
                case StageObjectType.RubberBox:
                case StageObjectType.Bomb:
                case StageObjectType.Battery:
                case StageObjectType.Bucket:
                case StageObjectType.FallingRock:
                    return CreateWeight(data, parent);
                case StageObjectType.Checkpoint:
                case StageObjectType.WarpEntrance:
                case StageObjectType.WarpExit:
                case StageObjectType.RespawnPoint:
                case StageObjectType.MidGoal:
                case StageObjectType.Button:
                case StageObjectType.WeightButton:
                case StageObjectType.PressurePlate:
                    return CreateButtonSwitch(data, parent);
                case StageObjectType.Lever:
                case StageObjectType.ToggleSwitch:
                case StageObjectType.TimerSwitch:
                case StageObjectType.RedSwitch:
                case StageObjectType.BlueSwitch:
                case StageObjectType.GreenSwitch:
                case StageObjectType.YellowSwitch:
                case StageObjectType.RemoteControl:
                case StageObjectType.Key:
                case StageObjectType.Coin:
                case StageObjectType.Star:
                case StageObjectType.Spring:
                case StageObjectType.Fan:
                case StageObjectType.Magnet:
                case StageObjectType.Cannon:
                case StageObjectType.Gear:
                case StageObjectType.BigGear:
                case StageObjectType.RopePulley:
                case StageObjectType.Saw:
                case StageObjectType.BlackHole:
                case StageObjectType.Pendulum:
                case StageObjectType.Keyhole:
                case StageObjectType.Clock:
                case StageObjectType.Counter:
                case StageObjectType.TrafficLight:
                case StageObjectType.GoalEffect:
                    return CreateProp(data, parent);
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
                case StageObjectType.ClimbableWall:
                case StageObjectType.Door:
                case StageObjectType.LockedDoor:
                case StageObjectType.Shutter:
                case StageObjectType.Fence:
                case StageObjectType.LaserGate:
                case StageObjectType.ColorGate:
                case StageObjectType.OneWayGate:
                case StageObjectType.TimedGate:
                case StageObjectType.BreakableWall:
                case StageObjectType.HiddenWall:
                    size = new Vector2(0.55f, 2.2f);
                    break;
                case StageObjectType.Spawn:
                case StageObjectType.Checkpoint:
                case StageObjectType.WarpEntrance:
                case StageObjectType.WarpExit:
                case StageObjectType.RespawnPoint:
                case StageObjectType.MidGoal:
                    size = new Vector2(0.7f, 0.7f);
                    break;
                case StageObjectType.Goal:
                    size = new Vector2(1.15f, 2.05f);
                    break;
                case StageObjectType.BalanceScale:
                case StageObjectType.Seesaw:
                case StageObjectType.Catapult:
                    size = new Vector2(4.5f, 0.6f);
                    break;
                case StageObjectType.Weight:
                case StageObjectType.WoodBox:
                case StageObjectType.IronBox:
                case StageObjectType.Ball:
                case StageObjectType.Barrel:
                case StageObjectType.Rock:
                case StageObjectType.IceBlock:
                case StageObjectType.FloatingBox:
                case StageObjectType.RubberBox:
                case StageObjectType.Bomb:
                case StageObjectType.Battery:
                case StageObjectType.Bucket:
                case StageObjectType.FallingRock:
                    size = new Vector2(0.9f, 0.9f);
                    break;
                case StageObjectType.Rope:
                case StageObjectType.Ladder:
                case StageObjectType.Laser:
                case StageObjectType.Water:
                case StageObjectType.Poison:
                case StageObjectType.Fire:
                case StageObjectType.Electricity:
                    size = new Vector2(0.55f, 2.2f);
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
            Color stroke = GetObjectColor(data.type);
            GameObject obj = CreateBox(data.objectId, data.position, data.size, new Color(stroke.r, stroke.g, stroke.b, 0.035f), parent);
            obj.name = data.type.ToString();
            obj.layer = groundLayer;
            obj.tag = "Ground";
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            if (StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Trigger || StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Hazard)
            {
                collider.isTrigger = true;
            }

            AddPencilFillLocal(obj.transform, data.size, stroke);
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

            Vector2 size = data.size;
            float height = Mathf.Max(3.2f, size.y);
            float halfSpan = Mathf.Max(1.0f, size.x * 0.42f);
            float trayWidth = Mathf.Max(0.85f, size.x * 0.26f);

            AddDoodleLine("Top Beam", root.transform, new[] { new Vector3(-halfSpan, height * 0.38f, 0f), new Vector3(halfSpan, height * 0.38f, 0f) }, Color.black, 0.055f, 18);

            GameObject leftTray = CreateBalanceTray("Left Tray", root.transform, new Vector2(-halfSpan, -height * 0.08f), trayWidth);
            GameObject rightTray = CreateBalanceTray("Right Tray", root.transform, new Vector2(halfSpan, -height * 0.08f), trayWidth);
            AddDoodleLine("Left Rope", root.transform, new[] { new Vector3(-halfSpan, height * 0.38f, 0f), new Vector3(-halfSpan, -height * 0.08f, 0f) }, Color.black, 0.026f, 17);
            AddDoodleLine("Right Rope", root.transform, new[] { new Vector3(halfSpan, height * 0.38f, 0f), new Vector3(halfSpan, -height * 0.08f, 0f) }, Color.black, 0.026f, 17);

            VerticalBalanceScale scale = root.AddComponent<VerticalBalanceScale>();
            scale.Configure(leftTray.GetComponent<Rigidbody2D>(), rightTray.GetComponent<Rigidbody2D>(), height * 0.36f);
            ConfigureBalanceTrayReporters(leftTray, scale, -1);
            ConfigureBalanceTrayReporters(rightTray, scale, 1);

            BoxCollider2D editorCollider = root.AddComponent<BoxCollider2D>();
            editorCollider.size = new Vector2(Mathf.Max(1f, data.size.x), Mathf.Max(1f, height + 1.2f));
            editorCollider.isTrigger = true;
            AddEditorMetadata(root, data);
            return root;
        }

        private GameObject CreateBalanceTray(string name, Transform parent, Vector2 localPosition, float width)
        {
            GameObject tray = CreateBox(name, Vector2.zero, new Vector2(width, 0.2f), new Color(0.08f, 0.08f, 0.08f, 0.04f), parent);
            tray.transform.localPosition = localPosition;
            tray.layer = groundLayer;
            tray.tag = "Ground";

            Rigidbody2D body = tray.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useFullKinematicContacts = true;

            BoxCollider2D collider = tray.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            GameObject sensor = new GameObject("Load Sensor");
            sensor.transform.SetParent(tray.transform, false);
            sensor.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            BoxCollider2D sensorCollider = sensor.AddComponent<BoxCollider2D>();
            sensorCollider.isTrigger = true;
            sensorCollider.size = new Vector2(1.12f, 1.15f);
            sensor.AddComponent<VerticalBalanceTray>();

            AddPencilFillLocal(tray.transform, new Vector2(width, 0.2f), new Color(0.1f, 0.1f, 0.1f));
            AddSketchBoxOutline(tray.transform, new Vector2(width, 0.2f), Color.black, 0.045f);
            tray.AddComponent<VerticalBalanceTray>();
            return tray;
        }

        private static void ConfigureBalanceTrayReporters(GameObject tray, VerticalBalanceScale scale, int side)
        {
            VerticalBalanceTray[] reporters = tray.GetComponentsInChildren<VerticalBalanceTray>(true);
            for (int i = 0; i < reporters.Length; i++)
            {
                reporters[i].Configure(scale, side);
            }
        }

        private GameObject CreateWeight(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = CreateBox(data.objectId, data.position, data.size, new Color(stroke.r, stroke.g, stroke.b, 0.12f), parent);
            obj.name = data.type.ToString();
            obj.layer = pushableLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = data.type == StageObjectType.Weight || data.type == StageObjectType.IronBox || data.type == StageObjectType.Rock ? 50f : 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            obj.AddComponent<CarryableObject>();

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            AddPencilFillLocal(obj.transform, data.size, stroke);
            AddSketchBoxOutline(obj.transform, data.size, Color.black, 0.055f);
            AddDoodleLine("Weight Handle", obj.transform, new[]
            {
                new Vector3(-0.18f, 0.5f, -0.01f),
                new Vector3(-0.08f, 0.68f, -0.01f),
                new Vector3(0.08f, 0.68f, -0.01f),
                new Vector3(0.18f, 0.5f, -0.01f)
            }, Color.black, 0.04f, 20);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateProp(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            AddDoodleCircle(obj.transform, Mathf.Max(0.28f, data.size.x * 0.38f), stroke, 0.055f);

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = Mathf.Max(0.35f, data.size.x * 0.45f);
            collider.isTrigger = StageObjectCatalog.Get(data.type).Kind != StageObjectKind.Pushable;
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateJumpPad(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
            obj.layer = groundLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D solid = obj.AddComponent<BoxCollider2D>();
            solid.size = new Vector2(0.85f, 0.16f);
            solid.offset = new Vector2(0f, -0.42f);

            GameObject trigger = new GameObject("Jump Trigger");
            trigger.transform.SetParent(obj.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            BoxCollider2D triggerCollider = trigger.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(0.95f, 0.7f);
            JumpPad jumpPad = trigger.AddComponent<JumpPad>();
            jumpPad.Configure(obj.transform);

            AddDoodleLine("Spring Left", obj.transform, new[]
            {
                new Vector3(-0.2f, -0.42f, -0.02f),
                new Vector3(-0.36f, -0.22f, -0.02f),
                new Vector3(-0.12f, -0.04f, -0.02f),
                new Vector3(-0.34f, 0.16f, -0.02f),
                new Vector3(-0.1f, 0.34f, -0.02f)
            }, Color.black, 0.028f, 20);
            AddDoodleLine("Spring Right", obj.transform, new[]
            {
                new Vector3(0.2f, -0.42f, -0.02f),
                new Vector3(0.36f, -0.22f, -0.02f),
                new Vector3(0.12f, -0.04f, -0.02f),
                new Vector3(0.34f, 0.16f, -0.02f),
                new Vector3(0.1f, 0.34f, -0.02f)
            }, Color.black, 0.028f, 20);
            AddDoodleLine("Spring Top", obj.transform, new[] { new Vector3(-0.46f, 0.4f, -0.02f), new Vector3(0.46f, 0.4f, -0.02f) }, Color.black, 0.055f, 21);
            AddDoodleLine("Spring Base", obj.transform, new[] { new Vector3(-0.42f, -0.44f, -0.02f), new Vector3(0.42f, -0.44f, -0.02f) }, Color.black, 0.055f, 21);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateButtonSwitch(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = data.type.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 baseSize = new Vector2(data.size.x, data.size.y * 0.34f);
            GameObject baseBox = CreateBox("Button Base", Vector2.zero, baseSize, new Color(0.08f, 0.08f, 0.08f, 0.04f), root.transform);
            baseBox.transform.localPosition = new Vector3(0f, -data.size.y * 0.16f, 0f);
            AddPencilFillLocal(baseBox.transform, baseSize, new Color(0.08f, 0.08f, 0.08f));
            AddSketchBoxOutline(baseBox.transform, baseSize, Color.black, 0.045f);

            Vector2 capSize = new Vector2(data.size.x * 0.72f, data.size.y * 0.28f);
            GameObject cap = CreateBox("Button Cap", Vector2.zero, capSize, new Color(0.95f, 0.1f, 0.08f, 0.16f), root.transform);
            cap.transform.localPosition = new Vector3(0f, data.size.y * 0.12f, -0.02f);
            AddPencilFillLocal(cap.transform, capSize, new Color(0.85f, 0.08f, 0.05f));
            AddSketchBoxOutline(cap.transform, capSize, Color.black, 0.04f);

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = data.size;
            AddEditorMetadata(root, data);
            return root;
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
            GameObject obj = new GameObject(data.objectId);
            obj.name = "Goal";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(data.size.x, data.size.y, 1f);
            obj.layer = goalLayer;
            obj.tag = "Goal";
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.7f, 0.92f);
            collider.offset = new Vector2(0f, -0.08f);
            collider.isTrigger = true;
            obj.AddComponent<Goal>();

            GameObject beam = new GameObject("Goal Beam");
            beam.transform.SetParent(obj.transform, false);
            beam.transform.localPosition = new Vector3(0f, -0.08f, -0.02f);
            LineRenderer beamLine = beam.AddComponent<LineRenderer>();
            beamLine.useWorldSpace = false;
            beamLine.positionCount = 4;
            beamLine.loop = true;
            beamLine.SetPositions(new[]
            {
                new Vector3(-0.28f, 0.28f, 0f),
                new Vector3(0.28f, 0.28f, 0f),
                new Vector3(0.5f, -0.48f, 0f),
                new Vector3(-0.5f, -0.48f, 0f)
            });
            beamLine.startWidth = 0.028f;
            beamLine.endWidth = 0.028f;
            beamLine.material = GetLineMaterial();
            beamLine.startColor = new Color(0.2f, 0.75f, 1f, 0.55f);
            beamLine.endColor = new Color(0.2f, 0.75f, 1f, 0.55f);
            beamLine.sortingOrder = 16;

            GameObject ufo = new GameObject("UFO");
            ufo.transform.SetParent(obj.transform, false);
            ufo.transform.localPosition = new Vector3(0f, 0.46f, -0.03f);
            ufo.AddComponent<UfoGoalVisual>();
            AddDoodleLine("UFO Body", ufo.transform, new[]
            {
                new Vector3(-0.34f, 0f, 0f),
                new Vector3(-0.18f, 0.12f, 0f),
                new Vector3(0.18f, 0.12f, 0f),
                new Vector3(0.34f, 0f, 0f),
                new Vector3(0.12f, -0.09f, 0f),
                new Vector3(-0.12f, -0.09f, 0f),
                new Vector3(-0.34f, 0f, 0f)
            }, Color.black, 0.04f, 20);
            AddDoodleLine("UFO Dome", ufo.transform, new[]
            {
                new Vector3(-0.12f, 0.1f, 0f),
                new Vector3(-0.04f, 0.22f, 0f),
                new Vector3(0.08f, 0.22f, 0f),
                new Vector3(0.16f, 0.1f, 0f)
            }, new Color(0.1f, 0.45f, 1f), 0.032f, 21);
            AddDoodleLine("Beam Rays", obj.transform, new[]
            {
                new Vector3(-0.18f, 0.22f, -0.02f),
                new Vector3(-0.42f, -0.45f, -0.02f),
                new Vector3(0f, 0.2f, -0.02f),
                new Vector3(0.02f, -0.5f, -0.02f),
                new Vector3(0.18f, 0.22f, -0.02f),
                new Vector3(0.42f, -0.45f, -0.02f)
            }, new Color(0.2f, 0.75f, 1f, 0.42f), 0.022f, 17);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateMarker(StageObjectData data, Transform parent, Color color, string label)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            AddDoodleCircle(obj.transform, 0.28f, color, 0.045f);
            AddDoodleLine("Flag Pole", obj.transform, new[] { new Vector3(0.18f, -0.25f, 0f), new Vector3(0.18f, 0.36f, 0f) }, color, 0.04f, 18);
            AddDoodleLine("Flag", obj.transform, new[] { new Vector3(0.18f, 0.32f, 0f), new Vector3(0.52f, 0.22f, 0f), new Vector3(0.18f, 0.12f, 0f) }, color, 0.04f, 18);

            CircleCollider2D collider = obj.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            collider.isTrigger = true;

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static Color GetObjectColor(StageObjectType type)
        {
            switch (StageObjectCatalog.Get(type).Category)
            {
                case StageObjectCategory.StartGoal:
                    return new Color(0.1f, 0.45f, 1f);
                case StageObjectCategory.Switch:
                    return new Color(0.95f, 0.2f, 0.2f);
                case StageObjectCategory.DoorGate:
                    return new Color(0.25f, 0.25f, 0.25f);
                case StageObjectCategory.Movable:
                    return new Color(0.52f, 0.34f, 0.18f);
                case StageObjectCategory.Action:
                    return new Color(0.1f, 0.65f, 0.25f);
                case StageObjectCategory.Trap:
                    return new Color(0.92f, 0.12f, 0.1f);
                case StageObjectCategory.Gimmick:
                    return new Color(0.55f, 0.25f, 0.9f);
                default:
                    if (type == StageObjectType.IceFloor || type == StageObjectType.IceBlock || type == StageObjectType.CloudPlatform)
                    {
                        return new Color(0.2f, 0.65f, 1f);
                    }

                    return new Color(0.05f, 0.05f, 0.05f);
            }
        }

        private static void AddObjectGlyph(Transform parent, StageObjectData data)
        {
            string label = StageObjectCatalog.Get(data.type).Label;
            string glyph = string.IsNullOrEmpty(label) ? data.type.ToString() : label.Substring(0, 1);
            if (data.type == StageObjectType.Goal || data.type == StageObjectType.MidGoal)
            {
                glyph = "G";
            }
            else if (data.type == StageObjectType.Spawn || data.type == StageObjectType.RespawnPoint)
            {
                glyph = "S";
            }
            else if (data.type == StageObjectType.BalanceScale || data.type == StageObjectType.Seesaw)
            {
                glyph = "↔";
            }
            else if (data.type == StageObjectType.Key)
            {
                glyph = "鍵";
            }
            else if (data.type == StageObjectType.Coin)
            {
                glyph = "￥";
            }
            else if (data.type == StageObjectType.Star)
            {
                glyph = "☆";
            }

            GameObject textObject = new GameObject("Glyph");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = glyph;
            text.fontSize = 30;
            text.characterSize = Mathf.Clamp(Mathf.Min(data.size.x, data.size.y) * 0.16f, 0.06f, 0.2f);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.02f, 0.02f, 0.02f, 0.82f);
        }

        private static void AddEditorMetadata(GameObject obj, StageObjectData data)
        {
            StageEditorObject marker = obj.AddComponent<StageEditorObject>();
            marker.objectId = data.objectId;
            marker.type = data.type;
            marker.size = data.size;
            marker.linkTargetId = data.linkTargetId;
            marker.linkAction = data.linkAction;
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
