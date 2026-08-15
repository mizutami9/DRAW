using System;
using System.Collections.Generic;
using System.Text;
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
        private static Sprite circleSprite;
        private static Sprite scaleBodySprite;
        private static Font digitalClockFont;

        public GameObject Create(StageObjectData data, Transform parent)
        {
            if (data == null)
            {
                return null;
            }

            if (IsBackgroundDecorationType(data.type))
            {
                return CreateBackgroundDecoration(data, parent);
            }

            switch (data.type)
            {
                case StageObjectType.StageBoundary:
                    return CreateStageBoundary(data, parent);
                case StageObjectType.BackgroundTree:
                case StageObjectType.BackgroundGrass:
                case StageObjectType.BackgroundFlower:
                case StageObjectType.BackgroundBush:
                case StageObjectType.BackgroundCloud:
                case StageObjectType.BackgroundPush:
                case StageObjectType.BackgroundArrow:
                    return CreateBackgroundDecoration(data, parent);
                case StageObjectType.JumpPad:
                case StageObjectType.Spring:
                    return CreateJumpPad(data, parent);
                case StageObjectType.Key:
                    return CreateKey(data, parent);
                case StageObjectType.Keyhole:
                    return CreateKeyhole(data, parent);
                case StageObjectType.InkScale:
                    return CreateInkScale(data, parent);
                case StageObjectType.BoxDropper:
                case StageObjectType.SpikeDropper:
                case StageObjectType.BombDropper:
                case StageObjectType.EnemyDropper:
                    return CreateBoxDropper(data, parent);
                case StageObjectType.Spike:
                    return CreateSpike(data, parent);
                case StageObjectType.Spawn:
                    return CreateMarker(data, parent, new Color(0.1f, 0.3f, 1f), "START");
                case StageObjectType.Goal:
                    return CreateGoal(data, parent);
                case StageObjectType.CollectibleFish:
                case StageObjectType.CollectibleCoin:
                case StageObjectType.CollectibleStar:
                    return CreateCollectible(data, parent);
                case StageObjectType.ChallengeClock:
                    return CreateChallengeClock(data, parent);
                case StageObjectType.BeamEmitter:
                    return CreateBeamEmitter(data, parent);
                case StageObjectType.MissileLauncher:
                    return CreateMissileLauncher(data, parent);
                case StageObjectType.Dynamite:
                    return CreateDynamite(data, parent);
                case StageObjectType.EnemyWalker:
                case StageObjectType.EnemyJumper:
                case StageObjectType.EnemyCharger:
                case StageObjectType.EnemyFlyer:
                case StageObjectType.EnemyShooter:
                    return CreateEnemy(data, parent);
                case StageObjectType.Elevator:
                    return CreateElevator(data, parent);
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
                case StageObjectType.PickupFuseBomb:
                case StageObjectType.Battery:
                case StageObjectType.Bucket:
                case StageObjectType.FallingRock:
                case StageObjectType.TriangleBox:
                    return CreateWeight(data, parent);
                case StageObjectType.BreakableWall:
                    return CreateBombBreakableWall(data, parent);
                case StageObjectType.Checkpoint:
                case StageObjectType.WarpEntrance:
                case StageObjectType.WarpExit:
                case StageObjectType.RespawnPoint:
                case StageObjectType.MidGoal:
                case StageObjectType.Button:
                case StageObjectType.WeightButton:
                case StageObjectType.SimultaneousButton:
                case StageObjectType.HoldButton:
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
                case StageObjectType.Coin:
                case StageObjectType.Star:
                case StageObjectType.Fan:
                case StageObjectType.Magnet:
                case StageObjectType.Cannon:
                case StageObjectType.Gear:
                case StageObjectType.BigGear:
                case StageObjectType.RopePulley:
                case StageObjectType.Saw:
                case StageObjectType.BlackHole:
                case StageObjectType.Pendulum:
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
            if (type == StageObjectType.BackgroundKeyNeeded)
            {
                size = new Vector2(3.6f, 2.4f);
            }
            else if (IsBackgroundDecorationType(type))
            {
                size = new Vector2(2.4f, 2.4f);
            }
            else
            {
                switch (type)
                {
                    case StageObjectType.StageBoundary:
                        size = new Vector2(30f, 18f);
                        break;
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
                        size = new Vector2(0.5f, 2f);
                        break;
                    case StageObjectType.Spawn:
                    case StageObjectType.Checkpoint:
                    case StageObjectType.WarpEntrance:
                    case StageObjectType.WarpExit:
                    case StageObjectType.RespawnPoint:
                    case StageObjectType.MidGoal:
                        size = new Vector2(0.7f, 0.7f);
                        break;
                    case StageObjectType.Dynamite:
                        size = new Vector2(1.4f, 1.25f);
                        break;
                    case StageObjectType.EnemyWalker:
                    case StageObjectType.EnemyJumper:
                    case StageObjectType.EnemyCharger:
                    case StageObjectType.EnemyFlyer:
                    case StageObjectType.EnemyShooter:
                        size = new Vector2(1.25f, 1.3f);
                        break;
                    case StageObjectType.Goal:
                        size = new Vector2(1.15f, 2.05f);
                        break;
                    case StageObjectType.CollectibleFish:
                    case StageObjectType.CollectibleCoin:
                    case StageObjectType.CollectibleStar:
                        size = Vector2.one * 0.8f;
                        break;
                    case StageObjectType.ChallengeClock:
                        size = new Vector2(3.2f, 1.25f);
                        break;
                    case StageObjectType.Key:
                        size = new Vector2(1.35f, 1.6f);
                        break;
                    case StageObjectType.Keyhole:
                        size = new Vector2(1.35f, 1.6f);
                        break;
                    case StageObjectType.BalanceScale:
                    case StageObjectType.Seesaw:
                    case StageObjectType.Catapult:
                        size = new Vector2(4.5f, 0.5f);
                        break;
                    case StageObjectType.InkScale:
                        size = new Vector2(3f, 0.9f);
                        break;
                    case StageObjectType.OneWayPlatform:
                        size = new Vector2(3f, 0.25f);
                        break;
                    case StageObjectType.BoxDropper:
                    case StageObjectType.SpikeDropper:
                    case StageObjectType.BombDropper:
                    case StageObjectType.EnemyDropper:
                        size = new Vector2(1.8f, 1.4f);
                        break;
                    case StageObjectType.BeamEmitter:
                    case StageObjectType.MissileLauncher:
                        size = new Vector2(1.25f, 0.9f);
                        break;
                    case StageObjectType.Button:
                    case StageObjectType.WeightButton:
                    case StageObjectType.SimultaneousButton:
                    case StageObjectType.HoldButton:
                    case StageObjectType.PressurePlate:
                        size = new Vector2(1f, 0.5f);
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
                    case StageObjectType.PickupFuseBomb:
                    case StageObjectType.Battery:
                    case StageObjectType.Bucket:
                    case StageObjectType.FallingRock:
                    case StageObjectType.TriangleBox:
                        size = new Vector2(0.9f, 0.9f);
                        break;
                    case StageObjectType.Rope:
                    case StageObjectType.Ladder:
                    case StageObjectType.Laser:
                    case StageObjectType.Water:
                    case StageObjectType.Poison:
                    case StageObjectType.Fire:
                    case StageObjectType.Electricity:
                        size = new Vector2(0.5f, 2f);
                        break;
                    default:
                        size = new Vector2(3f, 0.5f);
                        break;
                }
            }

            return new StageObjectData
            {
                objectId = StageObjectId.New(),
                type = type,
                position = position,
                size = size,
                rotation = 0f,
                actionStrength = type == StageObjectType.InkScale
                    ? 300f
                    : type == StageObjectType.BreakableWall
                        ? 1f
                    : type == StageObjectType.JumpPad || type == StageObjectType.Spring
                        ? 27f
                        : type == StageObjectType.MovingPlatform
                            ? 6f
                            : type == StageObjectType.Elevator
                                ? 8f
                            : type == StageObjectType.FallingFloor
                                ? 0.4f
                                : type == StageObjectType.Belt
                                    || type == StageObjectType.ConveyorLeft
                                    || type == StageObjectType.ConveyorRight
                                        ? 3f
                                        : type == StageObjectType.BoxDropper || type == StageObjectType.SpikeDropper || type == StageObjectType.BombDropper || type == StageObjectType.EnemyDropper || type == StageObjectType.BeamEmitter || type == StageObjectType.MissileLauncher ? 2f : 0f,
                movementAngle = type == StageObjectType.ConveyorLeft ? 180f : 0f,
                movementSpeed = type == StageObjectType.MovingPlatform ? 3.2f
                    : type == StageObjectType.EnemyWalker ? 2.4f
                    : type == StageObjectType.EnemyJumper ? 2.1f
                    : type == StageObjectType.EnemyCharger ? 1.7f
                    : type == StageObjectType.EnemyFlyer ? 2.7f
                    : type == StageObjectType.EnemyShooter ? 0.5f
                    : type == StageObjectType.MissileLauncher ? 8f
                    : 0f,
                spawnPattern = type == StageObjectType.BombDropper ? 1 : 0,
                spawnBoxSize = type == StageObjectType.BoxDropper || type == StageObjectType.SpikeDropper || type == StageObjectType.BombDropper || type == StageObjectType.EnemyDropper ? 0.9f : 0f,
                bombFuseSeconds = type == StageObjectType.Bomb
                    || type == StageObjectType.PickupFuseBomb
                    || type == StageObjectType.BombDropper
                    || type == StageObjectType.Dynamite ? 5f : 0f
            };
        }

        private GameObject CreateDynamite(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Dynamite.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = new Vector2(Mathf.Max(0.65f, data.size.x), Mathf.Max(0.65f, data.size.y));
            BoxCollider2D trigger = obj.AddComponent<BoxCollider2D>();
            trigger.size = size;
            trigger.isTrigger = true;

            StageDynamite dynamite = obj.AddComponent<StageDynamite>();
            dynamite.Configure(data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 5f, size);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateEnemy(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            // Enemy rotation is used as its initial facing direction. Keeping the
            // physics body upright avoids a 180-degree left-facing enemy becoming
            // visually upside down.
            obj.transform.rotation = Quaternion.identity;
            obj.layer = pushableLayer;

            Vector2 size = new Vector2(Mathf.Max(0.7f, data.size.x), Mathf.Max(0.7f, data.size.y));
            Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = data.type == StageObjectType.EnemyFlyer ? 0f : 1.65f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D enemyCollider = obj.AddComponent<CapsuleCollider2D>();
            enemyCollider.size = size;
            enemyCollider.direction = CapsuleDirection2D.Vertical;

            StageEnemyCharacter enemy = obj.AddComponent<StageEnemyCharacter>();
            float facing = Mathf.Cos(data.rotation * Mathf.Deg2Rad);
            enemy.Configure(data.type, size, data.movementSpeed > 0f ? data.movementSpeed : 0f, facing);
            AddEditorMetadata(obj, data);
            return obj;
        }

        public GameObject CreateSpawnedEnemy(
            StageObjectType type,
            string objectId,
            Vector2 position,
            float size,
            Transform parent,
            float speed,
            float facing)
        {
            StageObjectData data = CreateDefaultData(type, position);
            data.objectId = string.IsNullOrEmpty(objectId) ? StageObjectId.New() : objectId;
            data.size = Vector2.one * Mathf.Clamp(size, 0.7f, 2f);
            data.movementSpeed = Mathf.Clamp(speed > 0f ? speed : 2.4f, 0.5f, 8f);
            data.rotation = facing < 0f ? 180f : 0f;
            GameObject enemyObject = CreateEnemy(data, parent);
            StageEnemyCharacter enemy = enemyObject != null ? enemyObject.GetComponent<StageEnemyCharacter>() : null;
            enemy?.SetSpawnedByDevice();
            return enemyObject;
        }

        private GameObject CreateStageBoundary(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.StageBoundary.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.identity;

            float width = Mathf.Max(4f, data.size.x);
            float height = Mathf.Max(4f, data.size.y);
            float thickness = Mathf.Clamp(data.pathThickness > 0f ? data.pathThickness : 0.5f, 0.5f, 1.5f);
            Color stroke = new Color(0.16f, 0.17f, 0.2f, 1f);

            CreateBoundarySide(
                "Boundary Ceiling",
                new Vector2(0f, height * 0.5f - thickness * 0.5f),
                new Vector2(width, thickness),
                stroke,
                root.transform);

            float wallHeight = Mathf.Max(0.35f, height - thickness);
            float wallCenterY = -thickness * 0.5f;
            CreateBoundarySide(
                "Boundary Left Wall",
                new Vector2(-width * 0.5f + thickness * 0.5f, wallCenterY),
                new Vector2(thickness, wallHeight),
                stroke,
                root.transform);
            CreateBoundarySide(
                "Boundary Right Wall",
                new Vector2(width * 0.5f - thickness * 0.5f, wallCenterY),
                new Vector2(thickness, wallHeight),
                stroke,
                root.transform);

            data.size = new Vector2(width, height);
            data.pathThickness = thickness;
            AddEditorMetadata(root, data);
            return root;
        }

        private GameObject CreateSpike(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.Spike.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = new Vector2(
                Mathf.Max(0.35f, data.size.x),
                Mathf.Max(0.25f, data.size.y));
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(size.x, size.y * 0.82f);
            trigger.offset = new Vector2(0f, -size.y * 0.09f);
            trigger.isTrigger = true;
            root.AddComponent<StageSpikeHazard>();

            int spikeCount = Mathf.Clamp(Mathf.RoundToInt(size.x / Mathf.Max(0.28f, size.y * 0.72f)), 1, 48);
            float spikeWidth = size.x / spikeCount;
            Color fill = new Color(0.94f, 0.22f, 0.18f, 0.88f);
            Color pencil = new Color(0.43f, 0.04f, 0.035f, 1f);
            for (int i = 0; i < spikeCount; i++)
            {
                float left = -size.x * 0.5f + i * spikeWidth;
                float right = left + spikeWidth;
                float center = (left + right) * 0.5f;
                Vector3[] vertices =
                {
                    new Vector3(left, -size.y * 0.5f, -0.01f),
                    new Vector3(right, -size.y * 0.5f, -0.01f),
                    new Vector3(center, size.y * 0.5f, -0.01f)
                };
                Mesh mesh = new Mesh
                {
                    name = "Spike Fill Mesh",
                    vertices = vertices,
                    triangles = new[] { 0, 2, 1 },
                    colors = new[] { fill, fill, fill }
                };
                mesh.RecalculateBounds();

                GameObject visual = new GameObject("Spike Fill");
                visual.transform.SetParent(root.transform, false);
                MeshFilter filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = GetLineMaterial();
                renderer.sortingOrder = 18;

                AddDoodleLine(
                    "Spike Outline",
                    root.transform,
                    new[] { vertices[0], vertices[2], vertices[1], vertices[0] },
                    pencil,
                    0.045f,
                    19);
            }

            AddEditorMetadata(root, data);
            return root;
        }

        private void CreateBoundarySide(
            string name,
            Vector2 localPosition,
            Vector2 size,
            Color stroke,
            Transform parent)
        {
            GameObject side = new GameObject(name);
            side.transform.SetParent(parent, false);
            side.transform.localPosition = localPosition;
            side.layer = groundLayer;
            side.tag = "Ground";

            BoxCollider2D collider = side.AddComponent<BoxCollider2D>();
            collider.size = size;

            AddSolidPaperBase(side.transform, size);
            AddSolidWash(side.transform, size, stroke);
            AddSolidPencilFill(side.transform, size, stroke);
            AddSolidStraightBoxOutline(side.transform, size);
        }

        private GameObject CreateSolid(StageObjectData data, Transform parent)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                return CreateConnectedRectSolid(data, parent);
            }

            if (data.pathPoints != null && data.pathPoints.Length >= 2)
            {
                return CreatePathSolid(data, parent);
            }

            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.size = data.size;
            if (StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Trigger || StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Hazard)
            {
                collider.isTrigger = true;
            }
            if (data.type == StageObjectType.OneWayPlatform)
            {
                // PlatformEffector2D uses the object's local up direction, so a
                // rotated one-way floor still supports characters from its drawn
                // top side while allowing them to pass through from below.
                collider.usedByEffector = true;
                PlatformEffector2D effector = obj.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.surfaceArc = 165f;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }

            AddSolidPaperBase(obj.transform, data.size);
            if (data.type == StageObjectType.OneWayPlatform)
            {
                AddOneWayPlatformTint(obj.transform, data.size);
            }
            AddSolidWash(obj.transform, data.size, stroke);
            AddSolidPencilFill(obj.transform, data.size, stroke);
            if (data.type == StageObjectType.OneWayPlatform)
            {
                AddOneWayPlatformSurfaceVisual(obj.transform, data.size);
            }
            else
            {
                AddSolidStraightBoxOutline(obj.transform, data.size);
            }
            bool isConveyor = data.type == StageObjectType.Belt
                || data.type == StageObjectType.ConveyorLeft
                || data.type == StageObjectType.ConveyorRight;
            if (isConveyor)
            {
                AddConveyorBeltVisual(obj.transform, data);
            }
            if (data.type == StageObjectType.MovingPlatform)
            {
                if (parent != null && parent.name == "RuntimeStageEditorRoot")
                {
                    AddMovingPlatformDirectionIndicator(obj.transform, data);
                }
                Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            AddEditorMetadata(obj, data);
            if (isConveyor)
            {
                obj.AddComponent<StageConveyorBelt>();
            }
            if (data.type == StageObjectType.FallingFloor)
            {
                obj.AddComponent<StageCrumblingFloor>();
            }
            return obj;
        }

        private GameObject CreateElevator(StageObjectData data, Transform parent)
        {
            float travel = Mathf.Clamp(data.actionStrength > 0f ? data.actionStrength : 8f, 1f, 30f);
            // An elevator is a thin rideable platform. Older editor data could
            // contain a tall drag-created rectangle; do not turn that entire
            // shaft area into one enormous moving block.
            Vector2 cabinSize = new Vector2(
                Mathf.Max(1.2f, data.size.x),
                Mathf.Clamp(data.size.y, 0.35f, 0.8f));
            data.actionStrength = travel;
            data.size = cabinSize;

            GameObject root = new GameObject(data.objectId);
            root.name = StageObjectType.Elevator.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.layer = groundLayer;

            float railInset = Mathf.Max(0.35f, cabinSize.x * 0.32f);
            Color railInk = new Color(0.18f, 0.34f, 0.55f, 0.72f);
            AddDoodleLine(
                "Elevator Left Rail",
                root.transform,
                new[] { new Vector3(-railInset, 0f, 0.06f), new Vector3(-railInset, travel, 0.06f) },
                railInk,
                0.065f,
                4);
            AddDoodleLine(
                "Elevator Right Rail",
                root.transform,
                new[] { new Vector3(railInset, 0f, 0.06f), new Vector3(railInset, travel, 0.06f) },
                railInk,
                0.065f,
                4);

            float arrowX = railInset + 0.22f;
            for (float y = 1.2f; y < travel - 0.3f; y += 1.5f)
            {
                AddDoodleLine(
                    "Elevator Up Arrow",
                    root.transform,
                    new[]
                    {
                        new Vector3(arrowX, y - 0.22f, 0.05f),
                        new Vector3(arrowX, y + 0.22f, 0.05f),
                        new Vector3(arrowX - 0.14f, y + 0.07f, 0.05f),
                        new Vector3(arrowX, y + 0.22f, 0.05f),
                        new Vector3(arrowX + 0.14f, y + 0.07f, 0.05f)
                    },
                    railInk,
                    0.04f,
                    5);
            }

            GameObject cabin = new GameObject("Elevator Cabin");
            cabin.transform.SetParent(root.transform, false);
            cabin.transform.localPosition = Vector3.zero;
            cabin.layer = groundLayer;
            cabin.tag = "Ground";

            BoxCollider2D collider = cabin.AddComponent<BoxCollider2D>();
            collider.size = cabinSize;
            AddSolidPaperBase(cabin.transform, cabinSize);
            AddSolidWash(cabin.transform, cabinSize, new Color(0.12f, 0.48f, 0.86f, 1f));
            AddSolidPencilFill(cabin.transform, cabinSize, new Color(0.12f, 0.48f, 0.86f, 1f));
            AddSolidStraightBoxOutline(cabin.transform, cabinSize);

            Rigidbody2D body = cabin.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            AddEditorMetadata(root, data);
            if (parent == null || parent.name != "RuntimeStageEditorRoot")
            {
                StageElevator elevator = root.AddComponent<StageElevator>();
                elevator.Configure(body, travel, 2.2f);
            }
            return root;
        }

        private static void AddSolidStraightBoxOutline(Transform parent, Vector2 size)
        {
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            Vector3[] outline =
            {
                new Vector3(-x, -y, 0f),
                new Vector3(x, -y, 0f),
                new Vector3(x, y, 0f),
                new Vector3(-x, y, 0f),
                new Vector3(-x, -y, 0f)
            };
            AddDoodleLine("Solid Straight Outline", parent, outline, Color.black, 0.055f, 12);
            Vector3 accent = new Vector3(0.015f, -0.015f, 0f);
            Vector3[] accentOutline = new Vector3[outline.Length];
            for (int i = 0; i < outline.Length; i++) accentOutline[i] = outline[i] + accent;
            AddDoodleLine("Solid Straight Accent", parent, accentOutline, new Color(0.1f, 0.48f, 0.95f, 0.42f), 0.026f, 11);
        }

        private static void AddOneWayPlatformTint(Transform parent, Vector2 size)
        {
            GameObject tintObject = new GameObject("One Way Platform Blue Fill");
            tintObject.transform.SetParent(parent, false);
            tintObject.transform.localPosition = new Vector3(0f, 0f, 0.025f);
            tintObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = tintObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.56f, 0.86f, 1f, 0.72f);
            renderer.sortingOrder = 3;
        }

        private static void AddOneWayPlatformSurfaceVisual(Transform parent, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            Color surfaceBlue = new Color(0.02f, 0.42f, 0.9f, 0.95f);
            AddDoodleLine(
                "One Way Platform Top Surface",
                parent,
                new[]
                {
                    new Vector3(-halfWidth, top + 0.015f, 0f),
                    new Vector3(halfWidth, top + 0.015f, 0f)
                },
                surfaceBlue,
                0.075f,
                14);

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();
            Color dashedBlue = new Color(0.02f, 0.42f, 0.9f, 0.78f);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(-halfWidth, bottom, 0f),
                new Vector3(halfWidth, bottom, 0f),
                dashedBlue);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(-halfWidth, bottom, 0f),
                new Vector3(-halfWidth, top, 0f),
                dashedBlue);
            AppendDashedPlatformEdge(
                vertices, colors, triangles,
                new Vector3(halfWidth, bottom, 0f),
                new Vector3(halfWidth, top, 0f),
                dashedBlue);
            CreatePencilMesh(parent, "One Way Platform Dashed Outline", vertices, colors, triangles, 13);
        }

        private static void AppendDashedPlatformEdge(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            Color color)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                return;
            }

            Vector3 direction = delta / length;
            const float dashLength = 0.18f;
            const float gapLength = 0.11f;
            for (float cursor = 0f; cursor < length; cursor += dashLength + gapLength)
            {
                float dashEnd = Mathf.Min(cursor + dashLength, length);
                AppendPencilQuad(
                    vertices,
                    colors,
                    triangles,
                    from + direction * cursor,
                    from + direction * dashEnd,
                    0.05f,
                    color);
            }
        }

        private static void AddMovingPlatformDirectionIndicator(Transform parent, StageObjectData data)
        {
            // movementAngle is stored in stage space, while this guide is a child
            // of the (possibly rotated) platform. Cancel the platform rotation so
            // the preview points at the same destination as the runtime movement.
            float radians = (data.movementAngle - data.rotation) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float previewLength = Mathf.Clamp(data.actionStrength > 0f ? data.actionStrength : 6f, 1f, 100f);
            Vector2 start = Vector2.zero;
            Vector2 end = direction * previewLength;
            Color guide = new Color(0.1f, 0.48f, 0.95f, 0.5f);

            AddDoodleLine(
                "Movement Distance Guide",
                parent,
                new[] { (Vector3)start, (Vector3)end },
                guide,
                0.045f,
                20);

            Vector2 side = new Vector2(-direction.y, direction.x);
            float arrowSize = Mathf.Clamp(0.26f + previewLength * 0.025f, 0.3f, 0.62f);
            Vector2 arrowBase = end - direction * arrowSize;
            AddDoodleLine(
                "Movement Arrow",
                parent,
                new[]
                {
                    (Vector3)(arrowBase + side * arrowSize * 0.58f),
                    (Vector3)end,
                    (Vector3)(arrowBase - side * arrowSize * 0.58f)
                },
                new Color(0.06f, 0.34f, 0.9f, 0.78f),
                0.055f,
                21);

            AddDoodleCircleAt(
                parent,
                end,
                Mathf.Clamp(0.14f + previewLength * 0.008f, 0.16f, 0.3f),
                new Color(0.06f, 0.34f, 0.9f, 0.68f),
                0.04f,
                20);
        }

        private static void AddConveyorBeltVisual(Transform parent, StageObjectData data)
        {
            float halfWidth = Mathf.Max(0.2f, data.size.x * 0.5f);
            float halfHeight = Mathf.Max(0.1f, data.size.y * 0.5f);
            float direction = Mathf.Cos(data.movementAngle * Mathf.Deg2Rad) >= 0f ? 1f : -1f;
            Color beltInk = new Color(0.08f, 0.34f, 0.72f, 0.95f);
            float arrowY = Mathf.Clamp(halfHeight * 0.15f, -0.08f, 0.08f);
            int arrowCount = Mathf.Clamp(Mathf.RoundToInt(data.size.x / 1.2f), 2, 7);
            for (int i = 0; i < arrowCount; i++)
            {
                float x = Mathf.Lerp(-halfWidth * 0.72f, halfWidth * 0.72f, (i + 0.5f) / arrowCount);
                float tip = x + direction * Mathf.Min(0.28f, data.size.x / (arrowCount * 2.6f));
                AddDoodleLine(
                    "Conveyor Arrow",
                    parent,
                    new[]
                    {
                        new Vector3(x - direction * 0.16f, arrowY, -0.04f),
                        new Vector3(tip, arrowY, -0.04f),
                        new Vector3(tip - direction * 0.12f, arrowY + 0.1f, -0.04f),
                        new Vector3(tip, arrowY, -0.04f),
                        new Vector3(tip - direction * 0.12f, arrowY - 0.1f, -0.04f)
                    },
                    beltInk,
                    0.045f,
                    20);
            }

            AddDoodleCircleAt(parent, new Vector2(-halfWidth + halfHeight * 0.72f, 0f), halfHeight * 0.48f, beltInk, 0.04f, 19);
            AddDoodleCircleAt(parent, new Vector2(halfWidth - halfHeight * 0.72f, 0f), halfHeight * 0.48f, beltInk, 0.04f, 19);
        }

        public void RefreshBridgeConnectionVisuals(IList<StageObjectData> objects, Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("Bridge Terrain Connections");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            if (objects == null || objects.Count == 0)
            {
                return;
            }

            GameObject connectionRoot = new GameObject("Bridge Terrain Connections");
            connectionRoot.transform.SetParent(parent, false);
            for (int childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform oldMask = parent.GetChild(childIndex).Find("Terrain Connection Masks");
                if (oldMask != null) DestroyImmediate(oldMask.gameObject);
            }

            HashSet<string> dynamicTargets = new HashSet<string>();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData source = objects[i];
                if (source == null || string.IsNullOrEmpty(source.linkTargetId))
                {
                    continue;
                }

                if (source.linkAction == "RevealGrowRightToLeft"
                    || source.linkAction == "RevealGrow"
                    || source.linkAction == "Hide")
                {
                    dynamicTargets.Add(source.linkTargetId);
                }
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData a = objects[i];
                if (!CanJoinTerrainVisual(a))
                {
                    continue;
                }

                List<Rect> rectsA = new List<Rect>();
                AppendStageRects(a, rectsA);
                for (int j = i + 1; j < objects.Count; j++)
                {
                    StageObjectData b = objects[j];
                    if (!CanJoinTerrainVisual(b))
                    {
                        continue;
                    }

                    List<Rect> rectsB = new List<Rect>();
                    AppendStageRects(b, rectsB);
                    StageObjectData dynamicPart = dynamicTargets.Contains(a.objectId) ? a : dynamicTargets.Contains(b.objectId) ? b : null;
                    Transform maskParent = connectionRoot.transform;
                    if (dynamicPart != null)
                    {
                        Transform target = FindStageObjectTransform(parent, dynamicPart.objectId);
                        if (target != null)
                        {
                            Transform maskRoot = target.Find("Terrain Connection Masks");
                            if (maskRoot == null)
                            {
                                GameObject maskObject = new GameObject("Terrain Connection Masks");
                                maskObject.transform.SetParent(target, false);
                                maskRoot = maskObject.transform;
                            }
                            maskParent = maskRoot;
                        }
                    }

                    for (int rectAIndex = 0; rectAIndex < rectsA.Count; rectAIndex++)
                    {
                        for (int rectBIndex = 0; rectBIndex < rectsB.Count; rectBIndex++)
                        {
                            if (TryGetSharedEdge(rectsA[rectAIndex], rectsB[rectBIndex], out bool vertical, out Vector2 seamCenter, out float seamLength))
                            {
                                AddTerrainSeamMask(maskParent, vertical, seamCenter, seamLength);
                            }
                        }
                    }
                }
            }
        }

        private static bool CanJoinTerrainVisual(StageObjectData data)
        {
            return data != null
                && (data.type == StageObjectType.Platform || data.type == StageObjectType.Wall)
                && (data.pathPoints == null || data.pathPoints.Length < 2)
                && IsAxisAligned(data.rotation);
        }

        private static bool TryGetSharedEdge(Rect a, Rect b, out bool vertical, out Vector2 center, out float length)
        {
            const float tolerance = 0.24f;
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (yMax - yMin > 0.08f && (Mathf.Abs(a.xMax - b.xMin) <= tolerance || Mathf.Abs(b.xMax - a.xMin) <= tolerance))
            {
                float x = Mathf.Abs(a.xMax - b.xMin) <= tolerance ? (a.xMax + b.xMin) * 0.5f : (b.xMax + a.xMin) * 0.5f;
                vertical = true;
                center = new Vector2(x, (yMin + yMax) * 0.5f);
                length = yMax - yMin;
                return true;
            }

            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            if (xMax - xMin > 0.08f && (Mathf.Abs(a.yMax - b.yMin) <= tolerance || Mathf.Abs(b.yMax - a.yMin) <= tolerance))
            {
                float y = Mathf.Abs(a.yMax - b.yMin) <= tolerance ? (a.yMax + b.yMin) * 0.5f : (b.yMax + a.yMin) * 0.5f;
                vertical = false;
                center = new Vector2((xMin + xMax) * 0.5f, y);
                length = xMax - xMin;
                return true;
            }

            vertical = false;
            center = Vector2.zero;
            length = 0f;
            return false;
        }

        private static void AddTerrainSeamMask(Transform maskParent, bool vertical, Vector2 worldCenter, float length)
        {
            float visibleLength = Mathf.Max(0.04f, length - 0.07f);
            GameObject mask = new GameObject("Connected Terrain Seam Mask");
            mask.transform.SetParent(maskParent, false);
            mask.transform.localPosition = maskParent.InverseTransformPoint(new Vector3(worldCenter.x, worldCenter.y, 0f));
            mask.transform.localScale = vertical ? new Vector3(0.11f, visibleLength, 1f) : new Vector3(visibleLength, 0.11f, 1f);
            SpriteRenderer renderer = mask.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.985f, 0.975f, 0.93f, 1f);
            renderer.sortingOrder = 18;

            Color pencil = new Color(0.22f, 0.2f, 0.16f, 0.22f);
            int strokes = Mathf.Max(2, Mathf.CeilToInt(visibleLength / 0.18f));
            for (int i = 0; i < strokes; i++)
            {
                float along = -visibleLength * 0.5f + visibleLength * (i + 0.5f) / strokes;
                Vector3 from = vertical ? new Vector3(-0.075f, along - 0.045f, 0f) : new Vector3(along - 0.045f, -0.075f, 0f);
                Vector3 to = vertical ? new Vector3(0.075f, along + 0.045f, 0f) : new Vector3(along + 0.045f, 0.075f, 0f);
                AddDoodleLine("Connected Terrain Seam Pencil", maskParent, new[] { mask.transform.localPosition + from, mask.transform.localPosition + to }, pencil, 0.012f, 19);
            }
        }

        private static Transform FindStageObjectTransform(Transform parent, string objectId)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                StageEditorObject marker = parent.GetChild(i).GetComponent<StageEditorObject>();
                if (marker != null && marker.objectId == objectId) return marker.transform;
            }
            return null;
        }

        public void FitSeparateBridges(IList<StageObjectData> objects)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data != null && Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation)) < 2f)
                {
                    data.rotation = 0f;
                }
            }

            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData bridge = objects[i];
                if (!IsSeparateHorizontalBridge(bridge))
                {
                    continue;
                }

                Rect bridgeRect = RectFromStageData(bridge.position, bridge.size);
                float leftEdge = 0f;
                float rightEdge = 0f;
                float bestLeftGap = float.MaxValue;
                float bestRightGap = float.MaxValue;
                bool hasLeft = false;
                bool hasRight = false;
                const float fitDistance = 1.25f;

                for (int candidateIndex = 0; candidateIndex < objects.Count; candidateIndex++)
                {
                    StageObjectData candidate = objects[candidateIndex];
                    if (!CanProvideBridgeBank(candidate, bridge))
                    {
                        continue;
                    }

                    List<Rect> parts = new List<Rect>();
                    AppendStageRects(candidate, parts);
                    for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                    {
                        Rect part = parts[partIndex];
                        if (part.yMax <= bridgeRect.yMin || part.yMin >= bridgeRect.yMax)
                        {
                            continue;
                        }

                        float leftGap = Mathf.Abs(part.xMax - bridgeRect.xMin);
                        if (part.center.x < bridgeRect.center.x && leftGap <= fitDistance && leftGap < bestLeftGap)
                        {
                            bestLeftGap = leftGap;
                            leftEdge = part.xMax;
                            hasLeft = true;
                        }

                        float rightGap = Mathf.Abs(part.xMin - bridgeRect.xMax);
                        if (part.center.x > bridgeRect.center.x && rightGap <= fitDistance && rightGap < bestRightGap)
                        {
                            bestRightGap = rightGap;
                            rightEdge = part.xMin;
                            hasRight = true;
                        }
                    }
                }

                if (!hasLeft || !hasRight || rightEdge - leftEdge < 0.2f)
                {
                    continue;
                }

                bridge.position = new Vector2((leftEdge + rightEdge) * 0.5f, bridge.position.y);
                bridge.size = new Vector2(rightEdge - leftEdge, bridge.size.y);
            }
        }

        private static bool IsSeparateHorizontalBridge(StageObjectData data)
        {
            return data != null
                && data.keepSeparate
                && data.type == StageObjectType.Platform
                && data.size.x > data.size.y * 1.5f
                && Mathf.Abs(Mathf.DeltaAngle(0f, data.rotation)) < 2f;
        }

        private static bool CanProvideBridgeBank(StageObjectData candidate, StageObjectData bridge)
        {
            return candidate != null
                && candidate != bridge
                && StageObjectCatalog.Get(candidate.type).Category == StageObjectCategory.Terrain
                && StageObjectCatalog.Get(candidate.type).Kind == StageObjectKind.Solid
                && (candidate.pathPoints == null || candidate.pathPoints.Length < 2)
                && IsAxisAligned(candidate.rotation);
        }

        private static void AppendStageRects(StageObjectData data, List<Rect> results)
        {
            if (data.connectedRects != null && data.connectedRects.Length > 0)
            {
                for (int i = 0; i < data.connectedRects.Length; i++)
                {
                    StageRectPartData part = data.connectedRects[i];
                    if (part != null)
                    {
                        results.Add(RectFromStageData(data.position + part.position, part.size));
                    }
                }
                return;
            }

            results.Add(RectFromStageData(data.position, data.size, data.rotation));
        }

        private static Rect RectFromStageData(Vector2 position, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static Rect RectFromStageData(Vector2 position, Vector2 size, float rotation)
        {
            Vector2 sourceHalf = size * 0.5f;
            float radians = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(radians));
            float sin = Mathf.Abs(Mathf.Sin(radians));
            Vector2 half = new Vector2(
                sourceHalf.x * cos + sourceHalf.y * sin,
                sourceHalf.x * sin + sourceHalf.y * cos);
            return Rect.MinMaxRect(position.x - half.x, position.y - half.y, position.x + half.x, position.y + half.y);
        }

        private static bool IsAxisAligned(float rotation)
        {
            float horizontal = Mathf.Abs(Mathf.DeltaAngle(0f, rotation));
            float vertical = Mathf.Abs(Mathf.Abs(Mathf.DeltaAngle(0f, rotation)) - 90f);
            return horizontal < 2f || vertical < 2f;
        }

        private GameObject CreateConnectedRectSolid(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type + " Connected";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            List<float> xs = new List<float>();
            List<float> ys = new List<float>();
            for (int i = 0; i < data.connectedRects.Length; i++)
            {
                StageRectPartData part = data.connectedRects[i];
                if (part == null) continue;
                Vector2 half = part.size * 0.5f;
                AddUniqueCoordinate(xs, part.position.x - half.x);
                AddUniqueCoordinate(xs, part.position.x + half.x);
                AddUniqueCoordinate(ys, part.position.y - half.y);
                AddUniqueCoordinate(ys, part.position.y + half.y);

                BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
                collider.offset = part.position;
                collider.size = part.size;

                GameObject fillRoot = new GameObject($"Connected Fill {i}");
                fillRoot.transform.SetParent(obj.transform, false);
                fillRoot.transform.localPosition = part.position;
                AddSolidPaperBase(fillRoot.transform, part.size);
                AddSolidWash(fillRoot.transform, part.size, stroke);
                AddSolidPencilFill(fillRoot.transform, part.size, stroke);
            }

            xs.Sort();
            ys.Sort();
            bool[,] occupied = new bool[Mathf.Max(0, xs.Count - 1), Mathf.Max(0, ys.Count - 1)];
            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    Vector2 center = new Vector2((xs[x] + xs[x + 1]) * 0.5f, (ys[y] + ys[y + 1]) * 0.5f);
                    occupied[x, y] = IsInsideConnectedRect(data.connectedRects, center);
                }
            }

            for (int x = 0; x < xs.Count - 1; x++)
            {
                for (int y = 0; y < ys.Count - 1; y++)
                {
                    if (!occupied[x, y]) continue;
                    if (y == 0 || !occupied[x, y - 1]) AddConnectedEdge(obj.transform, new Vector2(xs[x], ys[y]), new Vector2(xs[x + 1], ys[y]));
                    if (y == ys.Count - 2 || !occupied[x, y + 1]) AddConnectedEdge(obj.transform, new Vector2(xs[x + 1], ys[y + 1]), new Vector2(xs[x], ys[y + 1]));
                    if (x == 0 || !occupied[x - 1, y]) AddConnectedEdge(obj.transform, new Vector2(xs[x], ys[y + 1]), new Vector2(xs[x], ys[y]));
                    if (x == xs.Count - 2 || !occupied[x + 1, y]) AddConnectedEdge(obj.transform, new Vector2(xs[x + 1], ys[y]), new Vector2(xs[x + 1], ys[y + 1]));
                }
            }

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddUniqueCoordinate(List<float> values, float value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (Mathf.Abs(values[i] - value) < 0.001f) return;
            }
            values.Add(value);
        }

        private static bool IsInsideConnectedRect(StageRectPartData[] parts, Vector2 point)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                StageRectPartData part = parts[i];
                if (part == null) continue;
                Vector2 half = part.size * 0.5f;
                if (point.x >= part.position.x - half.x && point.x <= part.position.x + half.x
                    && point.y >= part.position.y - half.y && point.y <= part.position.y + half.y)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddConnectedEdge(Transform parent, Vector2 from, Vector2 to)
        {
            AddDoodleLine("Connected Outer Edge", parent, new[] { (Vector3)from, (Vector3)to }, Color.black, 0.055f, 12);
            AddDoodleLine("Connected Blue Edge", parent, new[] { (Vector3)(from + new Vector2(0.015f, -0.015f)), (Vector3)(to + new Vector2(0.015f, -0.015f)) }, new Color(0.1f, 0.48f, 0.95f, 0.42f), 0.026f, 11);
        }

        private GameObject CreatePathSolid(StageObjectData data, Transform parent)
        {
            Color stroke = GetObjectColor(data.type);
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type + " Path";
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;
            obj.tag = "Ground";

            Vector2[] center = data.pathPoints;
            float thickness = Mathf.Max(0.2f, data.pathThickness > 0f ? data.pathThickness : 0.5f);
            float half = thickness * 0.5f;
            Vector2[] left = new Vector2[center.Length];
            Vector2[] right = new Vector2[center.Length];
            for (int i = 0; i < center.Length; i++)
            {
                Vector2 previous = center[Mathf.Max(0, i - 1)];
                Vector2 next = center[Mathf.Min(center.Length - 1, i + 1)];
                Vector2 tangent = (next - previous).normalized;
                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent = Vector2.right;
                }

                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                left[i] = center[i] + normal * half;
                right[i] = center[i] - normal * half;
            }

            Vector2[] polygon = new Vector2[center.Length * 2];
            Vector3[] outline = new Vector3[polygon.Length + 1];
            for (int i = 0; i < center.Length; i++)
            {
                polygon[i] = left[i];
                polygon[center.Length + i] = right[center.Length - 1 - i];
            }

            for (int i = 0; i < polygon.Length; i++)
            {
                outline[i] = polygon[i];
            }
            outline[outline.Length - 1] = polygon[0];

            PolygonCollider2D collider = obj.AddComponent<PolygonCollider2D>();
            collider.points = polygon;
            if (StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Trigger || StageObjectCatalog.Get(data.type).Kind == StageObjectKind.Hazard)
            {
                collider.isTrigger = true;
            }

            Vector3[] centerLine = new Vector3[center.Length];
            for (int i = 0; i < center.Length; i++)
            {
                centerLine[i] = center[i];
            }

            AddDoodleLine("Continuous Path Paper Base", obj.transform, centerLine, new Color(0.985f, 0.975f, 0.93f, 1f), thickness, 2);
            AddDoodleLine("Continuous Path Fill", obj.transform, centerLine, new Color(stroke.r, stroke.g, stroke.b, 0.045f), thickness, 3);
            int pencilLineCount = Mathf.Clamp(Mathf.CeilToInt(thickness / 0.13f), 2, 28);
            for (int lineIndex = 1; lineIndex < pencilLineCount; lineIndex++)
            {
                float offset = Mathf.Lerp(-half * 0.82f, half * 0.82f, lineIndex / (float)pencilLineCount);
                Vector3[] pencilPath = new Vector3[center.Length];
                for (int pointIndex = 0; pointIndex < center.Length; pointIndex++)
                {
                    Vector2 normal = (left[pointIndex] - center[pointIndex]).normalized;
                    float jitter = Mathf.Sin(pointIndex * 1.73f + lineIndex * 2.11f) * 0.018f;
                    pencilPath[pointIndex] = center[pointIndex] + normal * (offset + jitter);
                }

                float alpha = 0.11f + (lineIndex % 3) * 0.035f;
                AddDoodleLine(
                    $"Continuous Pencil {lineIndex}",
                    obj.transform,
                    pencilPath,
                    new Color(stroke.r, stroke.g, stroke.b, alpha),
                    0.012f + (lineIndex % 2) * 0.004f,
                    5);
            }
            AddDoodleLine("Continuous Path Outline", obj.transform, outline, Color.black, 0.055f, 12);
            AddDoodleLine("Continuous Path Accent", obj.transform, outline, new Color(0.1f, 0.48f, 0.95f, 0.42f), 0.026f, 11);
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
            float trayWidth = Mathf.Max(2.9f, size.x * 0.76f);

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
            const float trayHeight = 0.68f;
            Color trayColor = new Color(0.08f, 0.42f, 0.95f);
            Vector2 traySize = new Vector2(width, trayHeight);
            GameObject tray = CreateBox(name, Vector2.zero, traySize, new Color(trayColor.r, trayColor.g, trayColor.b, 0.08f), parent);
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
            sensor.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            BoxCollider2D sensorCollider = sensor.AddComponent<BoxCollider2D>();
            sensorCollider.isTrigger = true;
            sensorCollider.size = new Vector2(1.05f, 1.15f);
            sensor.AddComponent<VerticalBalanceTray>();

            AddPencilFillLocal(tray.transform, traySize, trayColor);
            AddSketchBoxOutline(tray.transform, traySize, Color.black, 0.06f);
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

        private GameObject CreateBoxDropper(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            Color casing = data.type == StageObjectType.EnemyDropper
                ? new Color(0.68f, 0.28f, 0.78f, 1f)
                : new Color(0.94f, 0.56f, 0.16f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color ink = new Color(0.32f, 0.12f, 0.04f, 1f);
            AddDoodleLine("Dropper Funnel", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.62f, halfHeight * 0.42f, -0.05f),
                new Vector3(halfWidth * 0.62f, halfHeight * 0.42f, -0.05f),
                new Vector3(halfWidth * 0.23f, -halfHeight * 0.16f, -0.05f),
                new Vector3(-halfWidth * 0.23f, -halfHeight * 0.16f, -0.05f),
                new Vector3(-halfWidth * 0.62f, halfHeight * 0.42f, -0.05f)
            }, ink, 0.06f, 21);
            AddDoodleLine("Dropper Arrow", obj.transform, new[]
            {
                new Vector3(0f, -halfHeight * 0.08f, -0.05f),
                new Vector3(0f, -halfHeight * 0.55f, -0.05f),
                new Vector3(-halfWidth * 0.12f, -halfHeight * 0.4f, -0.05f),
                new Vector3(0f, -halfHeight * 0.55f, -0.05f),
                new Vector3(halfWidth * 0.12f, -halfHeight * 0.4f, -0.05f)
            }, new Color(0.9f, 0.16f, 0.08f, 1f), 0.065f, 22);
            if (data.type == StageObjectType.EnemyDropper)
            {
                AddEnemyDropperPreview(obj.transform, data.spawnPattern, data.size);
            }
            else if (data.type == StageObjectType.SpikeDropper)
            {
                AddSpikeDropperPreview(obj.transform, data.size);
            }
            else if (data.type == StageObjectType.BombDropper)
            {
                AddBombDropperPreview(obj.transform, data.spawnPattern, data.size);
            }
            else
            {
                AddBoxDropperPatternPreview(obj.transform, data.spawnPattern, data.size);
            }

            AddEditorMetadata(obj, data);
            if (data.type == StageObjectType.EnemyDropper)
            {
                StageEnemyDropper dropper = obj.AddComponent<StageEnemyDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize);
            }
            else if (data.type == StageObjectType.SpikeDropper)
            {
                StageSpikeDropper dropper = obj.AddComponent<StageSpikeDropper>();
                dropper.Configure(this, parent, data.size, data.actionStrength, data.spawnBoxSize);
            }
            else if (data.type == StageObjectType.BombDropper)
            {
                StageBombDropper dropper = obj.AddComponent<StageBombDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize,
                    data.bombFuseSeconds);
            }
            else
            {
                StageBoxDropper dropper = obj.AddComponent<StageBoxDropper>();
                dropper.Configure(
                    this,
                    parent,
                    data.size,
                    data.actionStrength,
                    data.spawnPattern,
                    data.spawnBoxSize);
            }
            return obj;
        }

        private static void AddEnemyDropperPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.2f, 0.16f, 0.3f);
            float y = size.y * 0.25f;
            Color ink = new Color(0.34f, 0.08f, 0.46f, 1f);
            AddDoodleCircleAt(parent, new Vector2(0f, y), radius, ink, 0.055f, 24);
            float eyeOffset = radius * 0.38f;
            AddDoodleCircleAt(parent, new Vector2(-eyeOffset, y + radius * 0.12f), radius * 0.12f, ink, 0.04f, 25);
            AddDoodleCircleAt(parent, new Vector2(eyeOffset, y + radius * 0.12f), radius * 0.12f, ink, 0.04f, 25);
            if (pattern == 1)
            {
                AddDoodleLine("Enemy Spawner Jump Mark", parent, new[]
                {
                    new Vector3(-radius, y - radius, -0.06f),
                    new Vector3(-radius * 0.35f, y - radius * 1.45f, -0.06f),
                    new Vector3(radius * 0.35f, y - radius, -0.06f),
                    new Vector3(radius, y - radius * 1.45f, -0.06f)
                }, ink, 0.045f, 24);
            }
        }

        private GameObject CreateBeamEmitter(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.BeamEmitter.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            Color casing = new Color(0.25f, 0.72f, 0.86f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color beamColor = new Color(1f, 0.12f, 0.08f, 1f);
            AddDoodleLine("Beam Direction", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.34f, 0f, -0.06f),
                new Vector3(halfWidth * 0.42f, 0f, -0.06f),
                new Vector3(halfWidth * 0.18f, halfHeight * 0.24f, -0.06f),
                new Vector3(halfWidth * 0.42f, 0f, -0.06f),
                new Vector3(halfWidth * 0.18f, -halfHeight * 0.24f, -0.06f)
            }, beamColor, 0.065f, 22);

            float gaugeWidth = Mathf.Max(0.48f, data.size.x * 0.62f);
            float gaugeHeight = Mathf.Clamp(data.size.y * 0.13f, 0.09f, 0.14f);
            float gaugeY = -halfHeight * 0.68f;
            GameObject gaugeBackObject = new GameObject("Beam Charge Gauge Back");
            gaugeBackObject.transform.SetParent(obj.transform, false);
            gaugeBackObject.transform.localPosition = new Vector3(0f, gaugeY, -0.075f);
            gaugeBackObject.transform.localScale = new Vector3(gaugeWidth + 0.08f, gaugeHeight + 0.06f, 1f);
            SpriteRenderer gaugeBack = gaugeBackObject.AddComponent<SpriteRenderer>();
            gaugeBack.sprite = GetSquareSprite();
            gaugeBack.color = new Color(0.055f, 0.075f, 0.09f, 0.96f);
            gaugeBack.sortingOrder = 23;

            GameObject gaugeFillObject = new GameObject("Beam Charge Gauge Fill");
            gaugeFillObject.transform.SetParent(obj.transform, false);
            gaugeFillObject.transform.localPosition = new Vector3(-gaugeWidth * 0.175f, gaugeY, -0.08f);
            gaugeFillObject.transform.localScale = new Vector3(gaugeWidth * 0.65f, gaugeHeight, 1f);
            SpriteRenderer gaugeFill = gaugeFillObject.AddComponent<SpriteRenderer>();
            gaugeFill.sprite = GetSquareSprite();
            gaugeFill.color = new Color(1f, 0.68f, 0.08f, 1f);
            gaugeFill.sortingOrder = 24;

            for (int tick = 1; tick < 4; tick++)
            {
                float tickX = Mathf.Lerp(-gaugeWidth * 0.5f, gaugeWidth * 0.5f, tick / 4f);
                AddDoodleLine($"Beam Charge Tick {tick}", obj.transform, new[]
                {
                    new Vector3(tickX, gaugeY - gaugeHeight * 0.6f, -0.085f),
                    new Vector3(tickX, gaugeY + gaugeHeight * 0.6f, -0.085f)
                }, new Color(0.04f, 0.055f, 0.065f, 0.82f), 0.014f, 25);
            }

            GameObject readyLampObject = new GameObject("Beam Ready Lamp");
            readyLampObject.transform.SetParent(obj.transform, false);
            readyLampObject.transform.localPosition = new Vector3(-halfWidth * 0.38f, halfHeight * 0.3f, -0.085f);
            readyLampObject.transform.localScale = Vector3.one * Mathf.Clamp(data.size.y * 0.13f, 0.1f, 0.16f);
            SpriteRenderer readyLamp = readyLampObject.AddComponent<SpriteRenderer>();
            readyLamp.sprite = GetCircleSprite();
            readyLamp.color = new Color(0.18f, 0.4f, 0.46f, 1f);
            readyLamp.sortingOrder = 25;

            GameObject muzzle = new GameObject("Beam Muzzle");
            muzzle.transform.SetParent(obj.transform, false);
            muzzle.transform.localPosition = new Vector3(halfWidth + 0.08f, 0f, -0.08f);
            AddDoodleLine("Muzzle Top", muzzle.transform, new[]
            {
                new Vector3(-0.13f, halfHeight * 0.28f, 0f),
                new Vector3(0.13f, halfHeight * 0.18f, 0f)
            }, Color.black, 0.05f, 23);
            AddDoodleLine("Muzzle Bottom", muzzle.transform, new[]
            {
                new Vector3(-0.13f, -halfHeight * 0.28f, 0f),
                new Vector3(0.13f, -halfHeight * 0.18f, 0f)
            }, Color.black, 0.05f, 23);

            GameObject pulse = new GameObject("Beam Pulse");
            pulse.transform.SetParent(obj.transform, false);
            LineRenderer pulseLine = pulse.AddComponent<LineRenderer>();
            pulseLine.useWorldSpace = true;
            pulseLine.positionCount = 2;
            pulseLine.startWidth = 0.14f;
            pulseLine.endWidth = 0.1f;
            pulseLine.material = GetLineMaterial();
            pulseLine.startColor = new Color(1f, 0.18f, 0.08f, 0.98f);
            pulseLine.endColor = new Color(1f, 0.72f, 0.12f, 0.92f);
            pulseLine.sortingOrder = 40;
            pulseLine.enabled = false;

            StageBeamEmitter emitter = obj.AddComponent<StageBeamEmitter>();
            emitter.Configure(
                muzzle.transform,
                pulseLine,
                gaugeFillObject.transform,
                gaugeFill,
                readyLamp,
                gaugeWidth,
                data.actionStrength);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateMissileLauncher(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.MissileLauncher.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.layer = groundLayer;

            BoxCollider2D selectionCollider = obj.AddComponent<BoxCollider2D>();
            selectionCollider.size = data.size;
            selectionCollider.isTrigger = true;

            Color casing = new Color(0.86f, 0.25f, 0.18f, 1f);
            AddSolidPaperBase(obj.transform, data.size);
            AddSolidWash(obj.transform, data.size, casing);
            AddSolidPencilFill(obj.transform, data.size, casing);
            AddSolidStraightBoxOutline(obj.transform, data.size);

            float halfWidth = data.size.x * 0.5f;
            float halfHeight = data.size.y * 0.5f;
            Color missileInk = new Color(0.55f, 0.04f, 0.035f, 1f);
            AddDoodleLine("Missile Body Preview", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.36f, -halfHeight * 0.13f, -0.06f),
                new Vector3(halfWidth * 0.22f, -halfHeight * 0.13f, -0.06f),
                new Vector3(halfWidth * 0.46f, 0f, -0.06f),
                new Vector3(halfWidth * 0.22f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.36f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.36f, -halfHeight * 0.13f, -0.06f)
            }, missileInk, 0.055f, 23);
            AddDoodleLine("Missile Fins Preview", obj.transform, new[]
            {
                new Vector3(-halfWidth * 0.2f, -halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.34f, -halfHeight * 0.32f, -0.06f),
                new Vector3(0f, -halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.2f, halfHeight * 0.13f, -0.06f),
                new Vector3(-halfWidth * 0.34f, halfHeight * 0.32f, -0.06f),
                new Vector3(0f, halfHeight * 0.13f, -0.06f)
            }, missileInk, 0.045f, 23);

            GameObject muzzle = new GameObject("Missile Muzzle");
            muzzle.transform.SetParent(obj.transform, false);
            muzzle.transform.localPosition = new Vector3(halfWidth + 0.18f, 0f, -0.08f);
            AddDoodleCircleAt(
                muzzle.transform,
                Vector2.zero,
                Mathf.Max(0.15f, halfHeight * 0.48f),
                Color.black,
                0.065f,
                24);

            StageMissileLauncher launcher = obj.AddComponent<StageMissileLauncher>();
            launcher.Configure(
                parent,
                muzzle.transform,
                data.actionStrength,
                data.movementSpeed > 0f ? data.movementSpeed : 8f);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddSpikeDropperPreview(Transform parent, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.14f, 0.12f, 0.22f);
            float y = size.y * 0.29f;
            Color color = new Color(0.62f, 0.05f, 0.04f, 0.96f);
            AddDoodleLine("Dropper Spike", parent, new[]
            {
                new Vector3(-radius, y - radius, -0.06f),
                new Vector3(0f, y + radius, -0.06f),
                new Vector3(radius, y - radius, -0.06f),
                new Vector3(-radius, y - radius, -0.06f)
            }, color, 0.045f, 23);
        }

        private static void AddBombDropperPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.14f, 0.12f, 0.22f);
            float y = size.y * 0.29f;
            Color color = pattern == 2
                ? new Color(0.15f, 0.55f, 0.95f, 1f)
                : pattern == 1
                    ? new Color(0.95f, 0.2f, 0.12f, 1f)
                    : new Color(0.58f, 0.2f, 0.68f, 1f);
            AddDoodleCircleAt(parent, new Vector2(0f, y), radius, color, 0.045f, 23);
            AddDoodleLine("Bomb Dropper Fuse", parent, new[]
            {
                new Vector3(radius * 0.35f, y + radius * 0.72f, -0.06f),
                new Vector3(radius * 0.78f, y + radius * 1.35f, -0.06f)
            }, new Color(0.3f, 0.16f, 0.05f, 1f), 0.04f, 24);
        }

        private static void AddBoxDropperPatternPreview(Transform parent, int pattern, Vector2 size)
        {
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.095f, 0.09f, 0.16f);
            float y = size.y * 0.29f;
            Color color = new Color(0.14f, 0.25f, 0.48f, 0.95f);
            int first = pattern == 0 ? 1 : Mathf.Clamp(pattern, 1, 3);
            int last = pattern == 0 ? 3 : first;
            int count = last - first + 1;
            for (int shape = first; shape <= last; shape++)
            {
                float x = count == 1
                    ? 0f
                    : Mathf.Lerp(-radius * 2.3f, radius * 2.3f, (shape - first) / (float)(count - 1));
                if (shape == 1)
                {
                    AddDoodleLine("Dropper Square", parent, new[]
                    {
                        new Vector3(x - radius, y - radius, -0.06f),
                        new Vector3(x + radius, y - radius, -0.06f),
                        new Vector3(x + radius, y + radius, -0.06f),
                        new Vector3(x - radius, y + radius, -0.06f),
                        new Vector3(x - radius, y - radius, -0.06f)
                    }, color, 0.035f, 23);
                }
                else if (shape == 2)
                {
                    AddDoodleCircleAt(parent, new Vector2(x, y), radius, color, 0.035f, 23);
                }
                else
                {
                    AddDoodleLine("Dropper Triangle", parent, new[]
                    {
                        new Vector3(x, y + radius, -0.06f),
                        new Vector3(x + radius, y - radius, -0.06f),
                        new Vector3(x - radius, y - radius, -0.06f),
                        new Vector3(x, y + radius, -0.06f)
                    }, color, 0.035f, 23);
                }
            }
        }

        public GameObject CreateDroppedBox(
            StageObjectType boxType,
            string objectId,
            Vector2 position,
            float size,
            Transform parent,
            float bombFuseSeconds = 5f)
        {
            if (boxType != StageObjectType.WoodBox
                && boxType != StageObjectType.Ball
                && boxType != StageObjectType.TriangleBox
                && boxType != StageObjectType.Spike
                && boxType != StageObjectType.Bomb
                && boxType != StageObjectType.PickupFuseBomb)
            {
                boxType = StageObjectType.WoodBox;
            }

            StageObjectData data = CreateDefaultData(boxType, position);
            data.objectId = string.IsNullOrEmpty(objectId) ? StageObjectId.New() : objectId;
            data.bombFuseSeconds = Mathf.Clamp(bombFuseSeconds > 0f ? bombFuseSeconds : 5f, 1f, 15f);
            // Runtime challenge spawners can use oversized bombs. Editor-authored
            // droppers still keep their own 0.5-2.0 input range.
            float clampedSize = Mathf.Clamp(size, 0.5f, 3f);
            data.size = boxType == StageObjectType.Spike
                ? new Vector2(clampedSize, clampedSize * 0.8f)
                : Vector2.one * clampedSize;
            return boxType == StageObjectType.Spike
                ? CreateDroppedSpike(data, parent)
                : CreateWeight(data, parent);
        }

        private GameObject CreateDroppedSpike(StageObjectData data, Transform parent)
        {
            GameObject spike = CreateSpike(data, parent);
            spike.layer = pushableLayer;

            BoxCollider2D solid = spike.AddComponent<BoxCollider2D>();
            solid.size = new Vector2(data.size.x * 0.9f, data.size.y * 0.22f);
            solid.offset = new Vector2(0f, -data.size.y * 0.38f);

            Rigidbody2D body = spike.AddComponent<Rigidbody2D>();
            body.mass = Mathf.Max(1.5f, data.size.x * data.size.y * 2f);
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.35f;
            return spike;
        }

        private GameObject CreateWeight(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = pushableLayer;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
            rb.mass = data.type == StageObjectType.Weight
                || data.type == StageObjectType.IronBox
                || data.type == StageObjectType.Rock
                || data.type == StageObjectType.FallingRock
                    ? 50f
                    : data.type == StageObjectType.Barrel ? 4f : 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.sleepMode = RigidbodySleepMode2D.StartAsleep;
            rb.linearDamping = 0.12f;
            rb.angularDamping = 0.18f;
            obj.AddComponent<CarryableObject>();

            AddMovableCollider(obj, data.type);
            DrawMovableObject(obj.transform, data.type);
            AddEditorMetadata(obj, data);
            if (data.type == StageObjectType.Bomb || data.type == StageObjectType.PickupFuseBomb)
            {
                StageBomb bomb = obj.AddComponent<StageBomb>();
                bomb.Configure(
                    data.type == StageObjectType.PickupFuseBomb,
                    data.bombFuseSeconds > 0f ? data.bombFuseSeconds : 5f);
            }
            return obj;
        }

        private GameObject CreateBombBreakableWall(StageObjectData data, Transform parent)
        {
            GameObject wall = CreateSolid(data, parent);
            StageBombBreakableWall bombWall = wall.AddComponent<StageBombBreakableWall>();
            bombWall.Configure(Mathf.Clamp(Mathf.RoundToInt(data.actionStrength > 0f ? data.actionStrength : 1f), 1, 50), data.size);
            return wall;
        }

        private static void AddMovableCollider(GameObject obj, StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.Ball:
                case StageObjectType.Bomb:
                case StageObjectType.PickupFuseBomb:
                    CircleCollider2D circle = obj.AddComponent<CircleCollider2D>();
                    circle.radius = 0.48f;
                    break;
                case StageObjectType.Barrel:
                    CapsuleCollider2D capsule = obj.AddComponent<CapsuleCollider2D>();
                    capsule.size = new Vector2(0.82f, 0.98f);
                    capsule.direction = CapsuleDirection2D.Vertical;
                    break;
                case StageObjectType.Rock:
                case StageObjectType.FallingRock:
                    PolygonCollider2D rock = obj.AddComponent<PolygonCollider2D>();
                    rock.points = new[]
                    {
                        new Vector2(-0.48f, -0.4f),
                        new Vector2(-0.42f, 0.15f),
                        new Vector2(-0.16f, 0.46f),
                        new Vector2(0.27f, 0.42f),
                        new Vector2(0.49f, 0.05f),
                        new Vector2(0.38f, -0.42f)
                    };
                    break;
                case StageObjectType.Bucket:
                    PolygonCollider2D bucket = obj.AddComponent<PolygonCollider2D>();
                    bucket.points = new[]
                    {
                        new Vector2(-0.43f, 0.34f),
                        new Vector2(0.43f, 0.34f),
                        new Vector2(0.32f, -0.46f),
                        new Vector2(-0.32f, -0.46f)
                    };
                    break;
                case StageObjectType.TriangleBox:
                    PolygonCollider2D triangle = obj.AddComponent<PolygonCollider2D>();
                    triangle.points = new[]
                    {
                        new Vector2(-0.49f, -0.48f),
                        new Vector2(0.49f, -0.48f),
                        new Vector2(0f, 0.49f)
                    };
                    break;
                default:
                    BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
                    box.size = new Vector2(0.96f, 0.96f);
                    break;
            }
        }

        private static void DrawMovableObject(Transform parent, StageObjectType type)
        {
            Color dark = new Color(0.12f, 0.09f, 0.06f, 0.96f);
            switch (type)
            {
                case StageObjectType.WoodBox:
                    // Keep the box opaque and readable without creating roughly one
                    // hundred tiny LineRenderer objects per crate. Large crate piles
                    // otherwise become CPU- and draw-call-heavy.
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.7f, 0.42f, 0.18f, 1f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.28f, 0.14f, 0.045f), 0.055f);
                    AddDoodleLine("Wood Left Plank", parent, new[] { new Vector3(-0.27f, -0.48f), new Vector3(-0.27f, 0.48f) }, new Color(0.34f, 0.17f, 0.05f), 0.025f, 18);
                    AddDoodleLine("Wood Right Plank", parent, new[] { new Vector3(0.27f, -0.48f), new Vector3(0.27f, 0.48f) }, new Color(0.34f, 0.17f, 0.05f), 0.025f, 18);
                    AddDoodleLine("Wood Brace A", parent, new[] { new Vector3(-0.42f, -0.4f), new Vector3(0.42f, 0.4f) }, new Color(0.28f, 0.13f, 0.04f), 0.04f, 19);
                    AddDoodleLine("Wood Brace B", parent, new[] { new Vector3(-0.42f, 0.4f), new Vector3(0.42f, -0.4f) }, new Color(0.28f, 0.13f, 0.04f), 0.04f, 19);
                    break;
                case StageObjectType.IronBox:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.48f, 0.53f, 0.58f, 0.9f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.12f, 0.16f, 0.2f), 0.06f);
                    AddDoodleLine("Iron Inset", parent, new[]
                    {
                        new Vector3(-0.34f, -0.34f), new Vector3(0.34f, -0.34f),
                        new Vector3(0.34f, 0.34f), new Vector3(-0.34f, 0.34f),
                        new Vector3(-0.34f, -0.34f)
                    }, new Color(0.22f, 0.27f, 0.31f), 0.035f, 18);
                    AddRivets(parent, new Color(0.12f, 0.16f, 0.2f));
                    break;
                case StageObjectType.Ball:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.22f, 0.53f, 0.9f, 0.88f));
                    AddDoodleCircle(parent, 0.48f, new Color(0.06f, 0.2f, 0.5f), 0.055f);
                    AddDoodleLine("Ball Curve", parent, new[]
                    {
                        new Vector3(-0.36f, -0.08f), new Vector3(-0.12f, 0.05f),
                        new Vector3(0.12f, 0.12f), new Vector3(0.35f, 0.06f)
                    }, new Color(0.82f, 0.92f, 1f, 0.9f), 0.04f, 18);
                    break;
                case StageObjectType.Barrel:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.55f, 0.29f, 0.11f, 0.9f), new Vector2(0.82f, 1f));
                    AddDoodleLine("Barrel Outline", parent, new[]
                    {
                        new Vector3(-0.3f, -0.48f), new Vector3(-0.42f, -0.28f),
                        new Vector3(-0.42f, 0.28f), new Vector3(-0.3f, 0.48f),
                        new Vector3(0.3f, 0.48f), new Vector3(0.42f, 0.28f),
                        new Vector3(0.42f, -0.28f), new Vector3(0.3f, -0.48f),
                        new Vector3(-0.3f, -0.48f)
                    }, dark, 0.055f, 18);
                    AddDoodleLine("Barrel Top Band", parent, new[] { new Vector3(-0.38f, 0.28f), new Vector3(0.38f, 0.28f) }, new Color(0.18f, 0.19f, 0.2f), 0.075f, 19);
                    AddDoodleLine("Barrel Bottom Band", parent, new[] { new Vector3(-0.38f, -0.28f), new Vector3(0.38f, -0.28f) }, new Color(0.18f, 0.19f, 0.2f), 0.075f, 19);
                    AddDoodleLine("Barrel Wood Seam", parent, new[] { new Vector3(0f, -0.46f), new Vector3(0f, 0.46f) }, new Color(0.34f, 0.16f, 0.05f), 0.025f, 18);
                    break;
                case StageObjectType.Rock:
                case StageObjectType.FallingRock:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.46f, 0.45f, 0.43f, 0.9f), new Vector2(1f, 0.88f));
                    AddRockOutline(parent, type == StageObjectType.FallingRock);
                    break;
                case StageObjectType.IceBlock:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.55f, 0.86f, 1f, 0.7f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.12f, 0.54f, 0.82f), 0.05f);
                    AddDoodleLine("Ice Shine", parent, new[] { new Vector3(-0.34f, 0.36f), new Vector3(0.18f, -0.2f), new Vector3(0.36f, -0.08f) }, new Color(0.92f, 0.99f, 1f), 0.045f, 18);
                    break;
                case StageObjectType.FloatingBox:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.64f, 0.52f, 0.9f, 0.78f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.32f, 0.18f, 0.62f), 0.05f);
                    AddDoodleLine("Float Arrow", parent, new[] { new Vector3(0f, -0.25f), new Vector3(0f, 0.27f), new Vector3(-0.17f, 0.1f), new Vector3(0f, 0.27f), new Vector3(0.17f, 0.1f) }, Color.white, 0.045f, 19);
                    break;
                case StageObjectType.RubberBox:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.95f, 0.46f, 0.28f, 0.86f));
                    AddSketchBoxOutline(parent, Vector2.one, new Color(0.55f, 0.15f, 0.08f), 0.055f);
                    AddDoodleLine("Rubber Zigzag", parent, new[] { new Vector3(-0.38f, 0.05f), new Vector3(-0.16f, 0.22f), new Vector3(0.05f, -0.14f), new Vector3(0.35f, 0.12f) }, new Color(1f, 0.86f, 0.5f), 0.06f, 19);
                    break;
                case StageObjectType.Bomb:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.12f, 0.12f, 0.14f, 0.95f), new Vector2(0.9f, 0.9f));
                    AddDoodleCircleAt(parent, new Vector2(0f, -0.05f), 0.4f, Color.black, 0.055f, 18);
                    AddDoodleLine("Bomb Fuse", parent, new[] { new Vector3(0.2f, 0.28f), new Vector3(0.32f, 0.48f), new Vector3(0.45f, 0.42f) }, new Color(0.36f, 0.2f, 0.08f), 0.055f, 20);
                    AddDoodleLine("Bomb Spark", parent, new[] { new Vector3(0.41f, 0.39f), new Vector3(0.49f, 0.49f), new Vector3(0.45f, 0.36f), new Vector3(0.53f, 0.4f) }, new Color(1f, 0.64f, 0.08f), 0.045f, 21);
                    break;
                case StageObjectType.PickupFuseBomb:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.08f, 0.18f, 0.3f, 0.96f), new Vector2(0.9f, 0.9f));
                    AddDoodleCircleAt(parent, new Vector2(0f, -0.05f), 0.4f, new Color(0.02f, 0.08f, 0.16f), 0.055f, 18);
                    AddDoodleLine("Pickup Bomb Fuse", parent, new[] { new Vector3(0.2f, 0.28f), new Vector3(0.32f, 0.48f), new Vector3(0.45f, 0.42f) }, new Color(0.36f, 0.2f, 0.08f), 0.055f, 20);
                    AddDoodleCircleAt(parent, new Vector2(-0.2f, 0.02f), 0.1f, new Color(0.25f, 0.72f, 1f, 1f), 0.04f, 21);
                    break;
                case StageObjectType.Battery:
                    AddMovableBase(parent, GetSquareSprite(), new Color(0.54f, 0.78f, 0.25f, 0.88f), new Vector2(0.72f, 0.94f));
                    AddDoodleLine("Battery Outline", parent, new[] { new Vector3(-0.36f, -0.47f), new Vector3(0.36f, -0.47f), new Vector3(0.36f, 0.4f), new Vector3(-0.36f, 0.4f), new Vector3(-0.36f, -0.47f) }, dark, 0.05f, 18);
                    AddDoodleLine("Battery Terminal", parent, new[] { new Vector3(-0.14f, 0.43f), new Vector3(0.14f, 0.43f), new Vector3(0.14f, 0.5f), new Vector3(-0.14f, 0.5f) }, dark, 0.05f, 19);
                    AddDoodleLine("Battery Plus", parent, new[] { new Vector3(-0.12f, 0f), new Vector3(0.12f, 0f), new Vector3(0f, -0.12f), new Vector3(0f, 0.12f) }, Color.white, 0.05f, 20);
                    break;
                case StageObjectType.Bucket:
                    AddDoodleLine("Bucket Body", parent, new[]
                    {
                        new Vector3(-0.43f, 0.3f), new Vector3(-0.31f, -0.46f),
                        new Vector3(0.31f, -0.46f), new Vector3(0.43f, 0.3f),
                        new Vector3(-0.43f, 0.3f)
                    }, new Color(0.2f, 0.42f, 0.58f), 0.06f, 18);
                    AddDoodleLine("Bucket Handle", parent, new[] { new Vector3(-0.36f, 0.25f), new Vector3(-0.2f, 0.48f), new Vector3(0.2f, 0.48f), new Vector3(0.36f, 0.25f) }, new Color(0.18f, 0.22f, 0.24f), 0.045f, 19);
                    break;
                case StageObjectType.TriangleBox:
                    AddTriangleBoxVisual(parent);
                    break;
                case StageObjectType.Weight:
                default:
                    AddMovableBase(parent, GetCircleSprite(), new Color(0.24f, 0.25f, 0.27f, 0.92f), new Vector2(0.92f, 0.78f));
                    AddDoodleLine("Weight Base", parent, new[] { new Vector3(-0.45f, -0.4f), new Vector3(0.45f, -0.4f), new Vector3(0.34f, 0.22f), new Vector3(-0.34f, 0.22f), new Vector3(-0.45f, -0.4f) }, Color.black, 0.06f, 18);
                    AddDoodleLine("Weight Handle", parent, new[] { new Vector3(-0.18f, 0.22f), new Vector3(-0.1f, 0.45f), new Vector3(0.1f, 0.45f), new Vector3(0.18f, 0.22f) }, Color.black, 0.055f, 20);
                    break;
            }
        }

        private static void AddTriangleBoxVisual(Transform parent)
        {
            Color fill = new Color(0.76f, 0.48f, 0.2f, 0.96f);
            Color outline = new Color(0.3f, 0.14f, 0.045f, 1f);
            Vector3 left = new Vector3(-0.49f, -0.48f, 0.02f);
            Vector3 right = new Vector3(0.49f, -0.48f, 0.02f);
            Vector3 top = new Vector3(0f, 0.49f, 0.02f);

            Mesh mesh = new Mesh
            {
                name = "Triangle Box Fill Mesh",
                vertices = new[] { left, top, right },
                triangles = new[] { 0, 2, 1 },
                colors = new[] { fill, fill, fill }
            };
            mesh.RecalculateBounds();

            GameObject visual = new GameObject("Triangle Box Fill");
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = 3;

            AddDoodleLine(
                "Triangle Box Outline",
                parent,
                new[] { left, top, right, left },
                outline,
                0.06f,
                18);
            AddDoodleLine(
                "Triangle Box Brace Left",
                parent,
                new[] { new Vector3(-0.38f, -0.39f), new Vector3(0f, 0.34f) },
                new Color(0.4f, 0.19f, 0.055f),
                0.035f,
                19);
            AddDoodleLine(
                "Triangle Box Brace Right",
                parent,
                new[] { new Vector3(0.38f, -0.39f), new Vector3(0f, 0.34f) },
                new Color(0.4f, 0.19f, 0.055f),
                0.035f,
                19);
        }

        private static void AddMovableBase(Transform parent, Sprite sprite, Color color, Vector2 scale = default)
        {
            GameObject visual = new GameObject("Movable Fill");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            visual.transform.localScale = scale == default ? Vector3.one : new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 3;
        }

        private static void AddRivets(Transform parent, Color color)
        {
            Vector2[] positions =
            {
                new Vector2(-0.39f, -0.39f),
                new Vector2(0.39f, -0.39f),
                new Vector2(-0.39f, 0.39f),
                new Vector2(0.39f, 0.39f)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                AddDoodleCircleAt(parent, positions[i], 0.045f, color, 0.025f, 20);
            }
        }

        private static void AddRockOutline(Transform parent, bool dangerous)
        {
            Color outline = dangerous ? new Color(0.5f, 0.12f, 0.08f) : new Color(0.22f, 0.21f, 0.2f);
            AddDoodleLine("Rock Outline", parent, new[]
            {
                new Vector3(-0.48f, -0.38f), new Vector3(-0.42f, 0.12f),
                new Vector3(-0.16f, 0.45f), new Vector3(0.25f, 0.41f),
                new Vector3(0.48f, 0.04f), new Vector3(0.37f, -0.4f),
                new Vector3(-0.48f, -0.38f)
            }, outline, 0.06f, 18);
            AddDoodleLine("Rock Facet", parent, new[] { new Vector3(-0.2f, 0.35f), new Vector3(0.05f, 0.05f), new Vector3(0.34f, 0.27f) }, new Color(outline.r, outline.g, outline.b, 0.7f), 0.035f, 19);
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

        private GameObject CreateKey(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Key.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = pushableLayer;

            Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
            body.mass = 0.55f;
            body.gravityScale = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            CapsuleCollider2D collider = obj.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.5f, 0.92f);

            obj.AddComponent<CarryableObject>();

            Color gold = new Color(1f, 0.7f, 0.02f, 1f);
            AddDoodleCircleAt(obj.transform, new Vector2(0f, 0.27f), 0.22f, gold, 0.065f, 22);
            AddDoodleCircleAt(obj.transform, new Vector2(0f, 0.27f), 0.105f, gold, 0.035f, 22);
            AddDoodleLine("Key Shaft", obj.transform, new[]
            {
                new Vector3(0f, 0.07f, -0.02f),
                new Vector3(0f, -0.39f, -0.02f)
            }, gold, 0.09f, 22);
            AddDoodleLine("Key Teeth", obj.transform, new[]
            {
                new Vector3(0f, -0.32f, -0.02f),
                new Vector3(0.2f, -0.32f, -0.02f),
                new Vector3(0.2f, -0.2f, -0.02f),
                new Vector3(0.11f, -0.2f, -0.02f),
                new Vector3(0.11f, -0.11f, -0.02f)
            }, gold, 0.075f, 22);
            AddDoodleLine("Key Tip", obj.transform, new[]
            {
                new Vector3(-0.075f, -0.4f, -0.02f),
                new Vector3(0.075f, -0.4f, -0.02f)
            }, gold, 0.075f, 22);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateKeyhole(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.Keyhole.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);

            BoxCollider2D trigger = obj.AddComponent<BoxCollider2D>();
            // The key is often dropped at an angle, so the usable insertion area
            // needs a little room beyond the visible keyhole silhouette.
            trigger.size = new Vector2(0.95f, 1.1f);
            trigger.isTrigger = true;

            AddFilledKeyholeSilhouette(obj.transform);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private GameObject CreateInkScale(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.InkScale.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            obj.transform.localScale = new Vector3(
                Mathf.Max(0.2f, data.size.x),
                Mathf.Max(0.2f, data.size.y),
                1f);
            obj.layer = groundLayer;

            SpriteRenderer body = obj.AddComponent<SpriteRenderer>();
            body.sprite = GetScaleBodySprite();
            body.color = new Color(0.96f, 0.77f, 0.24f, 0.94f);
            body.sortingOrder = 8;

            BoxCollider2D platform = obj.AddComponent<BoxCollider2D>();
            platform.size = new Vector2(1.08f, 0.16f);
            platform.offset = new Vector2(0f, 0.43f);

            BoxCollider2D weighingArea = obj.AddComponent<BoxCollider2D>();
            weighingArea.isTrigger = true;
            // A vertical weighing column also catches players stacked on top of
            // another player. InkWeightScale filters out airborne characters.
            weighingArea.size = new Vector2(0.94f, 8f);
            weighingArea.offset = new Vector2(0f, 4.48f);

            GameObject plateObject = new GameObject("Scale Top Plate");
            plateObject.transform.SetParent(obj.transform, false);
            plateObject.transform.localPosition = new Vector3(0f, 0.43f, -0.025f);
            plateObject.transform.localScale = new Vector3(1.08f, 0.16f, 1f);
            SpriteRenderer plate = plateObject.AddComponent<SpriteRenderer>();
            plate.sprite = GetSquareSprite();
            plate.color = new Color(0.84f, 0.86f, 0.82f, 1f);
            plate.sortingOrder = 18;

            AddDoodleLine("Scale Plate Outline", obj.transform, new[]
            {
                new Vector3(-0.54f, 0.35f), new Vector3(-0.54f, 0.51f),
                new Vector3(0.54f, 0.51f), new Vector3(0.54f, 0.35f),
                new Vector3(-0.54f, 0.35f)
            }, new Color(0.1f, 0.1f, 0.09f), 0.035f, 20);
            AddDoodleLine("Scale Body Outline", obj.transform, new[]
            {
                new Vector3(-0.49f, -0.48f), new Vector3(0.49f, -0.48f),
                new Vector3(0.4f, 0.35f), new Vector3(-0.4f, 0.35f),
                new Vector3(-0.49f, -0.48f)
            }, new Color(0.14f, 0.11f, 0.05f), 0.045f, 19);

            GameObject displayObject = new GameObject("Scale Display Window");
            displayObject.transform.SetParent(obj.transform, false);
            displayObject.transform.localPosition = new Vector3(0f, 0.08f, -0.035f);
            displayObject.transform.localScale = new Vector3(0.72f, 0.34f, 1f);
            SpriteRenderer display = displayObject.AddComponent<SpriteRenderer>();
            display.sprite = GetSquareSprite();
            display.color = new Color(0.98f, 0.97f, 0.84f, 1f);
            display.sortingOrder = 20;
            AddDoodleLine("Scale Display Outline", obj.transform, new[]
            {
                new Vector3(-0.36f, -0.09f), new Vector3(0.36f, -0.09f),
                new Vector3(0.36f, 0.25f), new Vector3(-0.36f, 0.25f),
                new Vector3(-0.36f, -0.09f)
            }, new Color(0.12f, 0.11f, 0.08f), 0.025f, 22);

            GameObject gaugeBackObject = new GameObject("Scale Gauge Back");
            gaugeBackObject.transform.SetParent(obj.transform, false);
            gaugeBackObject.transform.localPosition = new Vector3(0f, -0.29f, -0.035f);
            gaugeBackObject.transform.localScale = new Vector3(0.64f, 0.1f, 1f);
            SpriteRenderer gaugeBack = gaugeBackObject.AddComponent<SpriteRenderer>();
            gaugeBack.sprite = GetSquareSprite();
            gaugeBack.color = new Color(0.12f, 0.11f, 0.09f, 0.92f);
            gaugeBack.sortingOrder = 20;

            GameObject gaugeFillObject = new GameObject("Scale Gauge Fill");
            gaugeFillObject.transform.SetParent(obj.transform, false);
            gaugeFillObject.transform.localPosition = new Vector3(-0.31f, -0.29f, -0.045f);
            gaugeFillObject.transform.localScale = new Vector3(0f, 0.065f, 1f);
            SpriteRenderer gaugeFill = gaugeFillObject.AddComponent<SpriteRenderer>();
            gaugeFill.sprite = GetSquareSprite();
            gaugeFill.color = new Color(0.3f, 0.82f, 0.96f, 1f);
            gaugeFill.sortingOrder = 21;

            for (int tick = 0; tick <= 4; tick++)
            {
                float x = Mathf.Lerp(-0.32f, 0.32f, tick / 4f);
                AddDoodleLine($"Scale Gauge Tick {tick}", obj.transform, new[]
                {
                    new Vector3(x, -0.35f, -0.05f), new Vector3(x, -0.23f, -0.05f)
                }, new Color(0.12f, 0.11f, 0.09f, 0.7f), 0.012f, 23);
            }

            AddScaleFoot(obj.transform, -0.32f);
            AddScaleFoot(obj.transform, 0.32f);

            GameObject textObject = new GameObject("Scale Meter Text");
            textObject.transform.SetParent(obj.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.08f, -0.055f);
            float meterScale = Mathf.Max(0.35f, Mathf.Min(
                Mathf.Max(0.2f, data.size.x) / 3f,
                Mathf.Max(0.2f, data.size.y) / 0.9f));
            textObject.transform.localScale = new Vector3(
                meterScale / Mathf.Max(0.2f, data.size.x),
                meterScale / Mathf.Max(0.2f, data.size.y),
                1f);
            TextMesh meterText = textObject.AddComponent<TextMesh>();
            meterText.text = $"0 / {Mathf.RoundToInt(data.actionStrength > 0f ? data.actionStrength : 300f)}";
            Font handwrittenFont = FindHandwrittenFont();
            if (handwrittenFont != null)
            {
                meterText.font = handwrittenFont;
            }
            meterText.fontSize = 42;
            meterText.characterSize = 0.08f;
            meterText.anchor = TextAnchor.MiddleCenter;
            meterText.alignment = TextAlignment.Center;
            meterText.color = new Color(0.08f, 0.07f, 0.05f, 1f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer != null)
            {
                if (handwrittenFont != null)
                {
                    textRenderer.sharedMaterial = handwrittenFont.material;
                }
                textRenderer.sortingOrder = 23;
            }

            InkWeightScale scale = obj.AddComponent<InkWeightScale>();
            scale.Configure(
                data.actionStrength > 0f ? data.actionStrength : 300f,
                meterText,
                body,
                gaugeFillObject.transform,
                gaugeFill);

            AddEditorMetadata(obj, data);
            return obj;
        }

        private static void AddScaleFoot(Transform parent, float x)
        {
            GameObject footObject = new GameObject("Scale Foot");
            footObject.transform.SetParent(parent, false);
            footObject.transform.localPosition = new Vector3(x, -0.53f, -0.025f);
            footObject.transform.localScale = new Vector3(0.2f, 0.08f, 1f);
            SpriteRenderer foot = footObject.AddComponent<SpriteRenderer>();
            foot.sprite = GetSquareSprite();
            foot.color = new Color(0.16f, 0.14f, 0.1f, 1f);
            foot.sortingOrder = 18;
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
            jumpPad.Configure(obj.transform, data.actionStrength > 0f ? data.actionStrength : 27f);

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

        private GameObject CreateBackgroundDecoration(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = data.type.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            GameObject visual = new GameObject("Background Visual");
            visual.transform.SetParent(root.transform, false);
            if (!TryApplyBackgroundSprite(visual, data.type, data.size))
            {
                visual.transform.localScale = new Vector3(
                    Mathf.Max(0.2f, data.size.x),
                    Mathf.Max(0.2f, data.size.y),
                    1f);

                Color line = GetBackgroundDecorationColor(data.type);
                DrawBackgroundDecoration(visual.transform, data.type, line);
            }

            BoxCollider2D selectionCollider = root.AddComponent<BoxCollider2D>();
            selectionCollider.size = new Vector2(Mathf.Max(0.2f, data.size.x), Mathf.Max(0.2f, data.size.y));
            selectionCollider.isTrigger = true;
            AddEditorMetadata(root, data);
            return root;
        }

        private static bool TryApplyBackgroundSprite(GameObject visual, StageObjectType type, Vector2 requestedSize)
        {
            string resourcePath = GetCrayonDecorationResourcePath(type);
            Sprite sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                switch (type)
                {
                    case StageObjectType.BackgroundTree:
                        resourcePath = "StageDecorations/tree-doodle";
                        break;
                    case StageObjectType.BackgroundGrass:
                        resourcePath = "StageDecorations/grass-doodle";
                        break;
                    case StageObjectType.BackgroundFlower:
                        resourcePath = "StageDecorations/flower-doodle";
                        break;
                    case StageObjectType.BackgroundBush:
                        resourcePath = "StageDecorations/bush-doodle";
                        break;
                    case StageObjectType.BackgroundCloud:
                        resourcePath = "StageDecorations/cloud-doodle";
                        break;
                    default:
                        resourcePath = null;
                        break;
                }

                sprite = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
            }

            if (sprite == null)
            {
                return false;
            }

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = -69;

            Vector2 spriteSize = sprite.bounds.size;
            float fitScale = Mathf.Min(
                Mathf.Max(0.2f, requestedSize.x) / Mathf.Max(0.01f, spriteSize.x),
                Mathf.Max(0.2f, requestedSize.y) / Mathf.Max(0.01f, spriteSize.y));
            visual.transform.localScale = new Vector3(fitScale, fitScale, 1f);
            return true;
        }

        private static bool IsBackgroundDecorationType(StageObjectType type)
        {
            return type.ToString().StartsWith("Background", StringComparison.Ordinal);
        }

        private static string GetCrayonDecorationResourcePath(StageObjectType type)
        {
            if (!IsBackgroundDecorationType(type))
            {
                return null;
            }

            string name = type.ToString().Substring("Background".Length);
            StringBuilder slug = new StringBuilder(name.Length + 6);
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (i > 0 && char.IsUpper(character))
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(character));
            }

            return "StageDecorations/CrayonSet/" + slug;
        }

        private static Color GetBackgroundDecorationColor(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.BackgroundTree:
                case StageObjectType.BackgroundGrass:
                case StageObjectType.BackgroundBush:
                    return new Color(0.14f, 0.52f, 0.25f, 0.72f);
                case StageObjectType.BackgroundFlower:
                    return new Color(0.92f, 0.3f, 0.38f, 0.72f);
                case StageObjectType.BackgroundCloud:
                    return new Color(0.18f, 0.5f, 0.9f, 0.62f);
                default:
                    return new Color(0.55f, 0.32f, 0.74f, 0.72f);
            }
        }

        private static void AddBackgroundCrayonFill(Transform parent, StageObjectType type, Color color)
        {
            Color fill = new Color(color.r, color.g, color.b, 0.095f);
            float minY = type == StageObjectType.BackgroundGrass || type == StageObjectType.BackgroundBush ? -0.35f : -0.42f;
            float maxY = type == StageObjectType.BackgroundGrass || type == StageObjectType.BackgroundBush ? 0.2f : 0.42f;
            for (int i = 0; i < 7; i++)
            {
                float y = Mathf.Lerp(minY, maxY, i / 6f);
                float inset = Mathf.Abs(y) * 0.3f;
                AddDoodleLine(
                    "Decoration Crayon Fill",
                    parent,
                    new[]
                    {
                        new Vector3(-0.42f + inset, y, 0f),
                        new Vector3(0.42f - inset, y + Mathf.Sin(i * 1.8f) * 0.025f, 0f)
                    },
                    fill,
                    0.075f + (i % 2) * 0.025f,
                    -72);
            }
        }

        private static void DrawBackgroundDecoration(Transform parent, StageObjectType type, Color color)
        {
            const float width = 0.028f;
            switch (type)
            {
                case StageObjectType.BackgroundTree:
                    DrawDetailedTree(parent);
                    break;
                case StageObjectType.BackgroundGrass:
                    DrawDetailedGrass(parent, Vector2.zero, 1f);
                    break;
                case StageObjectType.BackgroundFlower:
                    DrawDetailedFlower(parent);
                    break;
                case StageObjectType.BackgroundBush:
                    DrawDetailedBush(parent);
                    break;
                case StageObjectType.BackgroundCloud:
                    DrawDetailedCloud(parent);
                    break;
                case StageObjectType.BackgroundPush:
                    AddBackgroundCrayonFill(parent, type, color);
                    DrawBackgroundWord(parent, "PUSH", color);
                    break;
                case StageObjectType.BackgroundArrow:
                    AddBackgroundCrayonFill(parent, type, color);
                    AddDoodleLine("Arrow", parent, new[] { new Vector3(-0.48f, 0f), new Vector3(0.42f, 0f), new Vector3(0.15f, 0.25f), new Vector3(0.42f, 0f), new Vector3(0.15f, -0.25f) }, color, width * 1.3f, -69);
                    break;
            }
        }

        private static void DrawDetailedTree(Transform parent)
        {
            Color leaf = new Color(0.24f, 0.56f, 0.25f, 0.76f);
            Color trunk = new Color(0.48f, 0.29f, 0.15f, 0.8f);
            Vector2[] centers =
            {
                new Vector2(-0.22f, 0.2f), new Vector2(0f, 0.31f), new Vector2(0.23f, 0.18f),
                new Vector2(-0.34f, 0.02f), new Vector2(0.34f, 0.01f), new Vector2(0f, 0.05f)
            };
            float[] radii = { 0.24f, 0.28f, 0.25f, 0.2f, 0.2f, 0.3f };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], radii[i], leaf, i);
            }

            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-0.095f, 0.095f, i / 5f);
                AddDoodleLine(
                    "Tree Trunk Crayon",
                    parent,
                    new[]
                    {
                        new Vector3(x, -0.46f),
                        new Vector3(x * 0.45f + Mathf.Sin(i * 1.7f) * 0.02f, 0.13f)
                    },
                    new Color(trunk.r, trunk.g, trunk.b, 0.16f),
                    0.055f,
                    -71);
            }

            AddDoodleLine("Tree Trunk Outline", parent, new[] { new Vector3(-0.11f, -0.46f), new Vector3(-0.06f, 0.15f), new Vector3(0.07f, 0.15f), new Vector3(0.12f, -0.46f) }, trunk, 0.025f, -68);
            AddDoodleLine("Tree Branch", parent, new[] { new Vector3(-0.03f, -0.02f), new Vector3(-0.23f, 0.2f), new Vector3(-0.34f, 0.27f) }, trunk, 0.022f, -68);
            AddDoodleLine("Tree Branch", parent, new[] { new Vector3(0.04f, 0.03f), new Vector3(0.25f, 0.23f), new Vector3(0.35f, 0.29f) }, trunk, 0.022f, -68);

            DrawTreeCanopyOutline(parent, leaf);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(-0.32f, 0.11f), new Vector3(-0.19f, 0.18f), new Vector3(-0.09f, 0.13f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.38f), 0.014f, -68);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(0.08f, 0.31f), new Vector3(0.19f, 0.22f), new Vector3(0.34f, 0.27f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.38f), 0.014f, -68);
            AddDoodleLine("Leaf Pencil Detail", parent, new[]
            {
                new Vector3(-0.16f, -0.02f), new Vector3(-0.03f, 0.05f), new Vector3(0.12f, -0.01f)
            }, new Color(leaf.r, leaf.g, leaf.b, 0.34f), 0.014f, -68);

            AddDoodleLine("Tree Roots", parent, new[] { new Vector3(-0.22f, -0.48f), new Vector3(-0.03f, -0.43f), new Vector3(0.03f, -0.43f), new Vector3(0.23f, -0.48f) }, trunk, 0.022f, -68);
            DrawDetailedGrass(parent, new Vector2(0.3f, -0.33f), 0.38f);
        }

        private static void DrawTreeCanopyOutline(Transform parent, Color color)
        {
            Vector3[] points = new Vector3[65];
            Vector2 center = new Vector2(0f, 0.1f);
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                float lobes =
                    1f +
                    Mathf.Sin(angle * 7f + 0.4f) * 0.075f +
                    Mathf.Sin(angle * 11f - 0.7f) * 0.045f +
                    Mathf.Cos(angle * 3f + 0.2f) * 0.035f;
                points[i] = center + new Vector2(
                    Mathf.Cos(angle) * 0.47f * lobes,
                    Mathf.Sin(angle) * 0.43f * lobes);
            }

            AddDoodleLine("Tree Canopy Outline", parent, points, color, 0.025f, -68);
        }

        private static void DrawDetailedGrass(Transform parent, Vector2 offset, float scale)
        {
            Color grass = new Color(0.2f, 0.58f, 0.24f, 0.76f);
            for (int i = -6; i <= 6; i++)
            {
                float t = (i + 6) / 12f;
                float x = Mathf.Lerp(-0.48f, 0.48f, t) * scale + offset.x;
                float height = (0.28f + Mathf.Abs(Mathf.Sin(i * 1.73f)) * 0.2f) * scale;
                float lean = Mathf.Sin(i * 2.21f) * 0.12f * scale;
                Vector3[] blade =
                {
                    new Vector3(x, offset.y - 0.14f * scale),
                    new Vector3(x + lean * 0.35f, offset.y + height * 0.45f),
                    new Vector3(x + lean, offset.y + height)
                };
                AddDoodleLine("Grass Crayon", parent, blade, new Color(grass.r, grass.g, grass.b, 0.13f), 0.055f * scale, -71);
                AddDoodleLine("Grass Blade", parent, blade, grass, 0.018f * scale, -68);
            }
            AddDoodleLine("Grass Ground", parent, new[] { new Vector3(offset.x - 0.5f * scale, offset.y - 0.15f * scale), new Vector3(offset.x + 0.5f * scale, offset.y - 0.15f * scale) }, new Color(grass.r, grass.g, grass.b, 0.38f), 0.018f, -69);
        }

        private static void DrawDetailedFlower(Transform parent)
        {
            Color petal = new Color(0.94f, 0.32f, 0.43f, 0.76f);
            Color center = new Color(0.96f, 0.68f, 0.12f, 0.82f);
            Color stem = new Color(0.18f, 0.55f, 0.25f, 0.78f);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.21f + new Vector2(0f, 0.18f);
                AddCrayonBlob(parent, position, 0.14f, petal, i);
                BackgroundWobblyCircle(parent, position, 0.14f, petal, 0.02f, i);
            }
            AddCrayonBlob(parent, new Vector2(0f, 0.18f), 0.12f, center, 9);
            BackgroundWobblyCircle(parent, new Vector2(0f, 0.18f), 0.12f, center, 0.022f, 9);
            AddDoodleLine("Flower Stem", parent, new[] { new Vector3(0f, 0.08f), new Vector3(0.01f, -0.48f) }, stem, 0.025f, -68);
            AddDoodleLine("Flower Leaf", parent, new[] { new Vector3(0f, -0.17f), new Vector3(-0.25f, -0.3f), new Vector3(-0.02f, -0.35f) }, stem, 0.022f, -68);
        }

        private static void DrawDetailedBush(Transform parent)
        {
            Color bush = new Color(0.18f, 0.55f, 0.25f, 0.74f);
            Vector2[] centers =
            {
                new Vector2(-0.33f, -0.05f), new Vector2(-0.13f, 0.12f),
                new Vector2(0.1f, 0.15f), new Vector2(0.33f, -0.04f), new Vector2(0f, -0.1f)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], 0.25f, bush, i);
                BackgroundWobblyCircle(parent, centers[i], 0.25f, bush, 0.022f, i);
            }
            AddDoodleLine("Bush Ground", parent, new[] { new Vector3(-0.48f, -0.3f), new Vector3(0.48f, -0.3f) }, bush, 0.022f, -68);
        }

        private static void DrawDetailedCloud(Transform parent)
        {
            Color cloud = new Color(0.22f, 0.56f, 0.92f, 0.66f);
            Vector2[] centers =
            {
                new Vector2(-0.3f, -0.03f), new Vector2(-0.08f, 0.16f),
                new Vector2(0.18f, 0.18f), new Vector2(0.34f, -0.02f), new Vector2(0f, -0.08f)
            };
            for (int i = 0; i < centers.Length; i++)
            {
                AddCrayonBlob(parent, centers[i], 0.24f, cloud, i);
                BackgroundWobblyCircle(parent, centers[i], 0.24f, cloud, 0.021f, i);
            }
            AddDoodleLine("Cloud Base", parent, new[] { new Vector3(-0.46f, -0.25f), new Vector3(0.48f, -0.25f) }, cloud, 0.022f, -68);
        }

        private static void AddCrayonBlob(Transform parent, Vector2 center, float radius, Color color, int seed)
        {
            for (int row = -3; row <= 3; row++)
            {
                float normalized = row / 3.5f;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized)) * radius;
                float y = center.y + normalized * radius + Mathf.Sin(seed * 1.7f + row) * 0.008f;
                AddDoodleLine(
                    "Organic Crayon Fill",
                    parent,
                    new[] { new Vector3(center.x - halfWidth, y), new Vector3(center.x + halfWidth, y + Mathf.Cos(seed + row) * 0.008f) },
                    new Color(color.r, color.g, color.b, 0.13f),
                    0.055f,
                    -71);
            }
        }

        private static void BackgroundWobblyCircle(Transform parent, Vector2 center, float radius, Color color, float width, int seed)
        {
            Vector3[] points = new Vector3[33];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                float wobble = 1f + Mathf.Sin(i * 2.13f + seed * 1.77f) * 0.055f + Mathf.Cos(i * 1.17f + seed) * 0.025f;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * wobble;
            }
            AddDoodleLine("Organic Pencil Outline", parent, points, color, width, -68);
        }

        private static void DrawBackgroundWord(Transform parent, string word, Color color)
        {
            float advance = 0.24f;
            float start = -(word.Length - 1) * advance * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                DrawBackgroundLetter(parent, word[i], start + i * advance, color);
            }
        }

        private static void DrawBackgroundLetter(Transform parent, char letter, float x, Color color)
        {
            const float w = 0.025f;
            Vector3 P(float px, float py) => new Vector3(x + px * 0.17f, py * 0.42f, 0f);
            switch (letter)
            {
                case 'P':
                    AddDoodleLine("Letter P", parent, new[] { P(-0.45f, -0.75f), P(-0.45f, 0.75f), P(0.35f, 0.75f), P(0.48f, 0.15f), P(-0.42f, 0.15f) }, color, w, -69);
                    break;
                case 'U':
                    AddDoodleLine("Letter U", parent, new[] { P(-0.45f, 0.75f), P(-0.45f, -0.55f), P(0f, -0.78f), P(0.45f, -0.55f), P(0.45f, 0.75f) }, color, w, -69);
                    break;
                case 'S':
                    AddDoodleLine("Letter S", parent, new[] { P(0.45f, 0.65f), P(0f, 0.8f), P(-0.45f, 0.45f), P(0.35f, -0.05f), P(0.45f, -0.55f), P(0f, -0.8f), P(-0.45f, -0.62f) }, color, w, -69);
                    break;
                case 'H':
                    AddDoodleLine("Letter H", parent, new[] { P(-0.45f, -0.75f), P(-0.45f, 0.75f) }, color, w, -69);
                    AddDoodleLine("Letter H", parent, new[] { P(0.45f, -0.75f), P(0.45f, 0.75f) }, color, w, -69);
                    AddDoodleLine("Letter H", parent, new[] { P(-0.45f, 0f), P(0.45f, 0f) }, color, w, -69);
                    break;
            }
        }

        private static void BackgroundCircle(Transform parent, Vector2 center, float radius, Color color, float width)
        {
            Vector3[] points = new Vector3[25];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / (points.Length - 1);
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            AddDoodleLine("Decoration Circle", parent, points, color, width, -69);
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

            bool simultaneous = data.type == StageObjectType.SimultaneousButton;
            bool hold = data.type == StageObjectType.HoldButton;
            Color capColor = simultaneous
                ? new Color(0.1f, 0.48f, 0.95f)
                : hold
                    ? new Color(0.95f, 0.62f, 0.08f)
                    : new Color(0.85f, 0.08f, 0.05f);
            Vector2 capSize = new Vector2(data.size.x * 0.72f, data.size.y * 0.28f);
            GameObject cap = CreateBox(
                "Button Cap",
                Vector2.zero,
                capSize,
                new Color(capColor.r, capColor.g, capColor.b, 0.2f),
                root.transform);
            cap.transform.localPosition = new Vector3(0f, data.size.y * 0.12f, -0.02f);
            AddPencilFillLocal(cap.transform, capSize, capColor);
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

        private GameObject CreateCollectible(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = data.type.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            float scale = Mathf.Clamp(Mathf.Min(data.size.x, data.size.y), 0.4f, 2f);
            obj.transform.localScale = Vector3.one * scale;

            Color color = data.type == StageObjectType.CollectibleFish
                ? new Color(0.12f, 0.58f, 0.9f, 1f)
                : data.type == StageObjectType.CollectibleCoin
                    ? new Color(1f, 0.68f, 0.08f, 1f)
                    : new Color(1f, 0.38f, 0.2f, 1f);
            if (data.type == StageObjectType.CollectibleFish)
            {
                DrawCollectibleFish(obj.transform);
            }
            else if (data.type == StageObjectType.CollectibleCoin)
            {
                AddDoodleCircle(obj.transform, 0.38f, color, 0.07f);
                AddDoodleCircle(obj.transform, 0.24f, color, 0.04f);
            }
            else
            {
                Vector3[] points = new Vector3[11];
                for (int i = 0; i < 10; i++)
                {
                    float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                    float radius = i % 2 == 0 ? 0.42f : 0.19f;
                    points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                }
                points[10] = points[0];
                AddDoodleLine("Star", obj.transform, points, color, 0.06f, 20);
            }

            CircleCollider2D trigger = obj.AddComponent<CircleCollider2D>();
            trigger.radius = 0.48f;
            trigger.isTrigger = true;
            StageCollectible collectible = obj.AddComponent<StageCollectible>();
            collectible.Configure(data.objectId, data.type);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private void DrawCollectibleFish(Transform parent)
        {
            Color bodyBlue = new Color(0.18f, 0.66f, 0.95f, 1f);
            Color tailBlue = new Color(0.08f, 0.47f, 0.86f, 1f);
            Color outlineBlue = new Color(0.025f, 0.23f, 0.55f, 1f);

            CreateColoredTriangle(
                parent,
                "Fish Tail Fill",
                new Vector3(-0.28f, 0f, -0.02f),
                new Vector3(-0.62f, 0.29f, -0.02f),
                new Vector3(-0.59f, -0.29f, -0.02f),
                tailBlue,
                18);
            AddDoodleLine("Fish Tail Outline", parent, new[]
            {
                new Vector3(-0.28f, 0f, -0.03f),
                new Vector3(-0.62f, 0.29f, -0.03f),
                new Vector3(-0.59f, -0.29f, -0.03f),
                new Vector3(-0.28f, 0f, -0.03f)
            }, outlineBlue, 0.045f, 21);

            GameObject body = new GameObject("Fish Blue Body");
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0.03f, 0f, -0.04f);
            body.transform.localScale = new Vector3(0.78f, 0.5f, 1f);
            SpriteRenderer bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = GetCircleSprite();
            bodyRenderer.color = bodyBlue;
            bodyRenderer.sortingOrder = 19;
            AddDoodleCircle(body.transform, 0.5f, outlineBlue, 0.055f);

            CreateColoredTriangle(
                parent,
                "Fish Fin Fill",
                new Vector3(-0.02f, -0.02f, -0.06f),
                new Vector3(-0.16f, -0.31f, -0.06f),
                new Vector3(0.18f, -0.17f, -0.06f),
                new Color(0.06f, 0.42f, 0.8f, 0.95f),
                22);
            AddDoodleLine("Fish Fin Outline", parent, new[]
            {
                new Vector3(-0.02f, -0.02f, -0.07f),
                new Vector3(-0.16f, -0.31f, -0.07f),
                new Vector3(0.18f, -0.17f, -0.07f)
            }, outlineBlue, 0.032f, 23);

            CreateFishEye(parent, new Vector2(0.25f, 0.09f));
            AddDoodleLine("Fish Gill", parent, new[]
            {
                new Vector3(0.1f, 0.18f, -0.08f),
                new Vector3(0.05f, 0f, -0.08f),
                new Vector3(0.1f, -0.18f, -0.08f)
            }, new Color(0.04f, 0.38f, 0.7f, 0.9f), 0.025f, 24);
            AddDoodleLine("Fish Mouth", parent, new[]
            {
                new Vector3(0.39f, -0.05f, -0.08f),
                new Vector3(0.31f, -0.09f, -0.08f)
            }, outlineBlue, 0.026f, 24);
            AddDoodleLine("Fish Highlight", parent, new[]
            {
                new Vector3(-0.1f, 0.16f, -0.08f),
                new Vector3(0.08f, 0.2f, -0.08f)
            }, new Color(0.72f, 0.92f, 1f, 0.9f), 0.03f, 24);
        }

        private static void CreateFishEye(Transform parent, Vector2 position)
        {
            GameObject white = new GameObject("Fish Eye White");
            white.transform.SetParent(parent, false);
            white.transform.localPosition = new Vector3(position.x, position.y, -0.08f);
            white.transform.localScale = Vector3.one * 0.13f;
            SpriteRenderer whiteRenderer = white.AddComponent<SpriteRenderer>();
            whiteRenderer.sprite = GetCircleSprite();
            whiteRenderer.color = Color.white;
            whiteRenderer.sortingOrder = 24;

            GameObject pupil = new GameObject("Fish Eye Pupil");
            pupil.transform.SetParent(parent, false);
            pupil.transform.localPosition = new Vector3(position.x + 0.018f, position.y, -0.09f);
            pupil.transform.localScale = Vector3.one * 0.055f;
            SpriteRenderer pupilRenderer = pupil.AddComponent<SpriteRenderer>();
            pupilRenderer.sprite = GetCircleSprite();
            pupilRenderer.color = new Color(0.015f, 0.08f, 0.18f, 1f);
            pupilRenderer.sortingOrder = 25;
        }

        private void CreateColoredTriangle(
            Transform parent,
            string objectName,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Color color,
            int sortingOrder)
        {
            GameObject visual = new GameObject(objectName);
            visual.transform.SetParent(parent, false);
            Mesh mesh = new Mesh
            {
                name = objectName + " Mesh",
                vertices = new[] { first, second, third },
                triangles = new[] { 0, 1, 2 },
                colors = new[] { color, color, color }
            };
            mesh.RecalculateBounds();
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = sortingOrder;
        }

        private GameObject CreateChallengeClock(StageObjectData data, Transform parent)
        {
            GameObject obj = new GameObject(data.objectId);
            obj.name = StageObjectType.ChallengeClock.ToString();
            obj.transform.SetParent(parent, false);
            obj.transform.position = data.position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            Vector2 size = new Vector2(Mathf.Max(1.4f, data.size.x), Mathf.Max(0.65f, data.size.y));
            data.size = size;
            float clockScale = Mathf.Max(0.35f, Mathf.Min(size.x / 3.2f, size.y / 1.25f));
            CreateClockPanelLayer(
                obj.transform,
                "Timer Outer Case",
                size,
                new Color(0.09f, 0.12f, 0.16f, 0.34f),
                -24,
                new Vector3(0f, 0f, -0.02f));
            CreateClockPanelLayer(
                obj.transform,
                "Timer Bezel",
                new Vector2(size.x - 0.14f * clockScale, size.y - 0.14f * clockScale),
                new Color(0.27f, 0.31f, 0.35f, 0.34f),
                -23,
                new Vector3(0f, 0f, -0.03f));
            CreateClockPanelLayer(
                obj.transform,
                "Timer LCD",
                new Vector2(size.x - 0.3f * clockScale, size.y * 0.62f),
                new Color(0.012f, 0.035f, 0.038f, 0.34f),
                -22,
                new Vector3(0f, -size.y * 0.1f, -0.04f));
            AddDoodleLine("Timer Outer Outline", obj.transform, new[]
            {
                new Vector3(-size.x * 0.5f, -size.y * 0.5f, -0.05f),
                new Vector3(size.x * 0.5f, -size.y * 0.5f, -0.05f),
                new Vector3(size.x * 0.5f, size.y * 0.5f, -0.05f),
                new Vector3(-size.x * 0.5f, size.y * 0.5f, -0.05f),
                new Vector3(-size.x * 0.5f, -size.y * 0.5f, -0.05f)
            }, new Color(0.025f, 0.04f, 0.055f, 0.9f), 0.055f * clockScale, -19);

            GameObject statusLed = new GameObject("Timer Status LED");
            statusLed.transform.SetParent(obj.transform, false);
            statusLed.transform.localPosition = new Vector3(-size.x * 0.39f, size.y * 0.31f, -0.06f);
            statusLed.transform.localScale = Vector3.one * (0.14f * clockScale);
            SpriteRenderer ledRenderer = statusLed.AddComponent<SpriteRenderer>();
            ledRenderer.sprite = GetCircleSprite();
            ledRenderer.color = new Color(1f, 0.24f, 0.1f, 1f);
            ledRenderer.sortingOrder = -19;

            BoxCollider2D selection = obj.AddComponent<BoxCollider2D>();
            selection.size = size;
            selection.isTrigger = true;

            Font font = GetDigitalClockFont();
            float characterSize = 0.094f * clockScale;
            CreateClockText(
                obj.transform,
                "Timer Label",
                font,
                0.042f * clockScale,
                new Color(0.86f, 0.9f, 0.92f, 0.95f),
                -20,
                new Vector3(0f, size.y * 0.31f, -0.065f),
                "TIME LIMIT");
            TextMesh shadow = CreateClockText(
                obj.transform,
                "Clock Shadow",
                font,
                characterSize,
                new Color(0f, 0f, 0f, 0.72f),
                -20,
                new Vector3(0.025f * clockScale, -size.y * 0.1f - 0.025f * clockScale, -0.07f),
                "01:00.0");
            TextMesh digits = CreateClockText(
                obj.transform,
                "Clock Digits",
                font,
                characterSize,
                new Color(0.2f, 1f, 0.68f, 1f),
                -19,
                new Vector3(0f, -size.y * 0.1f, -0.08f),
                "01:00.0");
            TextMesh progress = CreateClockText(
                obj.transform,
                "Clock Fish Progress",
                font,
                0.034f * clockScale,
                new Color(0.72f, 0.9f, 1f, 1f),
                -19,
                new Vector3(0f, -size.y * 0.34f, -0.08f),
                "FISH  0 / 0");

            StageChallengeClock clock = obj.AddComponent<StageChallengeClock>();
            clock.Configure(digits, shadow, progress, ledRenderer, 60f);
            AddEditorMetadata(obj, data);
            return obj;
        }

        private static TextMesh CreateClockText(
            Transform parent,
            string objectName,
            Font font,
            float characterSize,
            Color color,
            int sortingOrder,
            Vector3 localPosition)
        {
            return CreateClockText(parent, objectName, font, characterSize, color, sortingOrder, localPosition, "01:00.0");
        }

        private static TextMesh CreateClockText(
            Transform parent,
            string objectName,
            Font font,
            float characterSize,
            Color color,
            int sortingOrder,
            Vector3 localPosition,
            string initialText)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.fontStyle = FontStyle.Bold;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.text = initialText;
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (font != null)
            {
                renderer.sharedMaterial = font.material;
            }
            return text;
        }

        private static void CreateClockPanelLayer(
            Transform parent,
            string objectName,
            Vector2 size,
            Color color,
            int sortingOrder,
            Vector3 localPosition)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Font GetDigitalClockFont()
        {
            if (digitalClockFont != null)
            {
                return digitalClockFont;
            }

            string[] preferred = { "Consolas", "Courier New", "Lucida Console" };
            string[] installed = Font.GetOSInstalledFontNames();
            for (int i = 0; i < preferred.Length; i++)
            {
                for (int j = 0; j < installed.Length; j++)
                {
                    if (string.Equals(preferred[i], installed[j], StringComparison.OrdinalIgnoreCase))
                    {
                        digitalClockFont = Font.CreateDynamicFontFromOSFont(installed[j], 64);
                        return digitalClockFont;
                    }
                }
            }

            digitalClockFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return digitalClockFont;
        }

        private static Color GetObjectColor(StageObjectType type)
        {
            if (type == StageObjectType.OneWayPlatform)
            {
                return new Color(0.02f, 0.48f, 0.92f);
            }

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
                case StageObjectCategory.Enemy:
                    return new Color(0.72f, 0.18f, 0.58f);
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
            marker.actionStrength = data.actionStrength;
            marker.movementAngle = data.movementAngle;
            marker.movementSpeed = data.movementSpeed;
            marker.spawnPattern = data.spawnPattern;
            marker.spawnBoxSize = data.spawnBoxSize;
            marker.bombFuseSeconds = data.bombFuseSeconds;
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

        private static void AddSolidWash(Transform parent, Vector2 size, Color color)
        {
            GameObject wash = new GameObject("Solid Fill Wash");
            wash.transform.SetParent(parent, false);
            wash.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            wash.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = wash.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(color.r, color.g, color.b, 0.025f);
            renderer.sortingOrder = 3;
        }

        private static void AddSolidPaperBase(Transform parent, Vector2 size)
        {
            GameObject baseObject = new GameObject("Solid Opaque Paper Base");
            baseObject.transform.SetParent(parent, false);
            baseObject.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            baseObject.transform.localScale = new Vector3(size.x, size.y, 1f);

            SpriteRenderer renderer = baseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = new Color(0.985f, 0.975f, 0.93f, 1f);
            renderer.sortingOrder = 2;
        }

        private static void AddSolidSketchBoxOutline(Transform parent, Vector2 size, Color color, float width, int sortingOrder, Vector3 offset = default)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            Vector3[] points =
            {
                offset + new Vector3(-halfWidth - 0.02f, -halfHeight + 0.02f, 0f),
                offset + new Vector3(halfWidth, -halfHeight + 0.04f, 0f),
                offset + new Vector3(halfWidth - 0.03f, halfHeight, 0f),
                offset + new Vector3(-halfWidth + 0.01f, halfHeight - 0.02f, 0f),
                offset + new Vector3(-halfWidth - 0.02f, -halfHeight + 0.02f, 0f)
            };
            AddDoodleLine("Solid Sketch Outline A", parent, points, color, width, sortingOrder);

            Vector3[] loosePoints =
            {
                offset + new Vector3(-halfWidth, -halfHeight - 0.02f, 0f),
                offset + new Vector3(halfWidth + 0.03f, -halfHeight + 0.01f, 0f),
                offset + new Vector3(halfWidth + 0.01f, halfHeight + 0.02f, 0f),
                offset + new Vector3(-halfWidth - 0.03f, halfHeight - 0.01f, 0f),
                offset + new Vector3(-halfWidth, -halfHeight - 0.02f, 0f)
            };
            AddDoodleLine("Solid Sketch Outline B", parent, loosePoints, color * 0.9f, width, sortingOrder + 1);
        }

        private static void AddSolidPencilFill(Transform parent, Vector2 size, Color color, int sortingOrder = 4)
        {
            float left = -size.x * 0.5f;
            float right = size.x * 0.5f;
            float bottom = -size.y * 0.5f;
            float top = size.y * 0.5f;
            Color pencil = new Color(color.r, color.g, color.b, 0.32f);
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
                            AppendPencilQuad(
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
                AppendPencilQuad(
                    vertices, colors, triangles,
                    new Vector3(left + 0.1f, y + Mathf.Sin(i * 1.3f) * 0.025f, 0f),
                    new Vector3(right - 0.1f, y + Mathf.Cos(i * 1.9f) * 0.025f, 0f),
                    0.01f,
                    new Color(color.r, color.g, color.b, 0.13f));
            }

            CreatePencilMesh(parent, "Solid Pencil Fill Mesh", vertices, colors, triangles, sortingOrder);
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
            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<int> triangles = new List<int>();

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
                        AppendPencilQuad(vertices, colors, triangles, start, end, 0.012f * inverseScale, pencil);
                        x += 0.16f + Mathf.Abs(Mathf.Sin(index * 1.9f)) * 0.07f;
                        index++;
                    }

                    y += 0.17f;
                }
            }

            CreatePencilMesh(parent, "Pencil Fill Mesh", vertices, colors, triangles, 4);
        }

        private static void AppendPencilQuad(
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
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
            triangles.Add(first + 1);
        }

        private static void CreatePencilMesh(
            Transform parent,
            string name,
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            int sortingOrder)
        {
            if (parent == null || vertices.Count == 0)
            {
                return;
            }

            GameObject visual = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = sortingOrder;
        }

        private static void AddDoodleCircle(Transform parent, float radius, Color color, float width)
        {
            AddDoodleCircleAt(parent, Vector2.zero, radius, color, width, 20);
        }

        private static void AddDoodleCircleAt(
            Transform parent,
            Vector2 center,
            float radius,
            Color color,
            float width,
            int sortingOrder)
        {
            Vector3[] points = new Vector3[22];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i / (float)(points.Length - 1);
                float angle = t * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(i * 1.7f) * 0.04f;
                points[i] = new Vector3(
                    center.x + Mathf.Cos(angle) * radius * wobble,
                    center.y + Mathf.Sin(angle) * radius * wobble,
                    0f);
            }

            AddDoodleLine("Circle", parent, points, color, width, sortingOrder);
        }

        private static void AddFilledKeyholeSilhouette(Transform parent)
        {
            const int circleSegments = 32;
            const float centerY = 0.22f;
            const float radius = 0.21f;

            Vector3[] vertices = new Vector3[circleSegments + 6];
            int[] triangles = new int[circleSegments * 3 + 6];
            Color[] colors = new Color[vertices.Length];
            Color black = new Color(0.025f, 0.022f, 0.018f, 1f);

            vertices[0] = new Vector3(0f, centerY, -0.02f);
            for (int i = 0; i <= circleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / circleSegments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    centerY + Mathf.Sin(angle) * radius,
                    -0.02f);
            }

            for (int i = 0; i < circleSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            int stemStart = circleSegments + 2;
            vertices[stemStart] = new Vector3(-0.085f, 0.09f, -0.02f);
            vertices[stemStart + 1] = new Vector3(0.085f, 0.09f, -0.02f);
            vertices[stemStart + 2] = new Vector3(0.19f, -0.4f, -0.02f);
            vertices[stemStart + 3] = new Vector3(-0.19f, -0.4f, -0.02f);

            int stemTriangleStart = circleSegments * 3;
            triangles[stemTriangleStart] = stemStart;
            triangles[stemTriangleStart + 1] = stemStart + 1;
            triangles[stemTriangleStart + 2] = stemStart + 2;
            triangles[stemTriangleStart + 3] = stemStart;
            triangles[stemTriangleStart + 4] = stemStart + 2;
            triangles[stemTriangleStart + 5] = stemStart + 3;

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = black;
            }

            Mesh mesh = new Mesh
            {
                name = "Filled Keyhole Silhouette Mesh",
                vertices = vertices,
                triangles = triangles,
                colors = colors
            };
            mesh.RecalculateBounds();

            GameObject visual = new GameObject("Filled Keyhole Silhouette");
            visual.transform.SetParent(parent, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetLineMaterial();
            renderer.sortingOrder = 22;
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

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Movable Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = 1f - Mathf.Clamp01(distance - radius + 1f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }

        private static Sprite GetScaleBodySprite()
        {
            if (scaleBodySprite != null)
            {
                return scaleBodySprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Ink Scale Body",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float vertical = y / (float)(size - 1);
                float halfWidth = Mathf.Lerp(0.49f, 0.4f, vertical);
                for (int x = 0; x < size; x++)
                {
                    float horizontal = x / (float)(size - 1) - 0.5f;
                    pixels[y * size + x] = Mathf.Abs(horizontal) <= halfWidth
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            scaleBodySprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            return scaleBodySprite;
        }

        private static Font FindHandwrittenFont()
        {
            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                Font font = loadedFonts[i];
                if (font != null && font.name.IndexOf("Yomogi", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
