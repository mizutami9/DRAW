using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StagePoseTowerRandomizer
    {
        private static readonly Dictionary<string, int> LoadCounts = new Dictionary<string, int>();
        private static readonly Dictionary<string, List<int>> RecentChoices = new Dictionary<string, List<int>>();
        private static int offlineSequence;

        public static void Prepare(StageData data)
        {
            if (data == null || data.id != "7-1" || data.objects == null) return;

            string context = ResolveRandomContext();
            int loadCount;
            if (context == "offline")
            {
                loadCount = ++offlineSequence;
            }
            else
            {
                LoadCounts.TryGetValue(context, out loadCount);
                loadCount++;
                LoadCounts[context] = loadCount;
            }

            int seed = context == "offline"
                ? System.Guid.NewGuid().GetHashCode()
                : StableHash(context + "|7-1|" + loadCount);
            System.Random random = new System.Random(seed);
            RandomizeFloor(data.objects, 0, 99, "7-1_hole_human", random, context + "|human");
            RandomizeFloor(data.objects, 100, 199, "7-1_hole_bird", random, context + "|bird");
            RandomizeFloor(data.objects, 200, 299, "7-1_hole_turtle", random, context + "|turtle");
            RandomizeBombDroppers(data.objects, random);
        }

        private static void RandomizeFloor(
            StageObjectData[] objects,
            int minimumPattern,
            int maximumPattern,
            string keyholeId,
            System.Random random,
            string historyKey)
        {
            List<StageObjectData> candidates = new List<StageObjectData>();
            StageObjectData keyhole = null;
            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData item = objects[i];
                if (item == null) continue;
                if (item.objectId == keyholeId) keyhole = item;
                if (item.type != StageObjectType.PoseCharacterKey
                    || item.spawnPattern < minimumPattern
                    || item.spawnPattern > maximumPattern) continue;
                item.linkTargetId = string.Empty;
                item.linkAction = string.Empty;
                candidates.Add(item);
            }
            if (keyhole == null || candidates.Count == 0) return;

            List<Vector2> shuffledPositions = new List<Vector2>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++) shuffledPositions.Add(candidates[i].position);
            for (int i = shuffledPositions.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                Vector2 swap = shuffledPositions[i];
                shuffledPositions[i] = shuffledPositions[swapIndex];
                shuffledPositions[swapIndex] = swap;
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                candidates[i].position = shuffledPositions[i];
                candidates[i].rotation = random.Next(-24, 25);
            }

            if (!RecentChoices.TryGetValue(historyKey, out List<int> recent))
            {
                recent = new List<int>(10);
                RecentChoices[historyKey] = recent;
            }
            List<int> available = new List<int>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!recent.Contains(candidates[i].spawnPattern)) available.Add(i);
            }
            if (available.Count == 0)
            {
                recent.Clear();
                for (int i = 0; i < candidates.Count; i++) available.Add(i);
            }
            int choice = available[random.Next(available.Count)];
            StageObjectData selected = candidates[choice];
            recent.Add(selected.spawnPattern);
            if (recent.Count > 10) recent.RemoveAt(0);
            selected.linkTargetId = keyholeId;
            selected.linkAction = "Unlock";
            keyhole.spawnPattern = selected.spawnPattern;
        }

        private static void RandomizeBombDroppers(StageObjectData[] objects, System.Random random)
        {
            List<StageObjectData> droppers = new List<StageObjectData>();
            for (int i = 0; i < objects.Length; i++)
            {
                StageObjectData item = objects[i];
                if (item != null && item.objectId != null
                    && item.objectId.StartsWith("7-1_bomb_dropper_", System.StringComparison.Ordinal))
                    droppers.Add(item);
            }

            Vector2[] slots =
            {
                new Vector2(-11.25f, 27.7f), new Vector2(-6.75f, 27.7f), new Vector2(-2.25f, 27.7f),
                new Vector2(2.25f, 27.7f), new Vector2(6.75f, 27.7f),
                new Vector2(-12.3f, 24.25f), new Vector2(-12.3f, 27.25f),
                new Vector2(-6f, 19.66f), new Vector2(0f, 19.66f), new Vector2(6f, 19.66f)
            };
            for (int i = slots.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                Vector2 swap = slots[i];
                slots[i] = slots[swapIndex];
                slots[swapIndex] = swap;
            }

            int count = Mathf.Min(droppers.Count, slots.Length);
            for (int i = 0; i < count; i++)
            {
                StageObjectData dropper = droppers[i];
                dropper.position = slots[i];
                Vector2 target = new Vector2(
                    Mathf.Lerp(-6.3f, 6.3f, (float)random.NextDouble()),
                    Mathf.Lerp(20.74f, 26.02f, (float)random.NextDouble()));
                Vector2 direction = (target - dropper.position).normalized;
                dropper.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
                dropper.actionStrength = Mathf.Lerp(3.1f, 5.2f, (float)random.NextDouble());
                dropper.bombFuseSeconds = Mathf.Lerp(7.6f, 12.4f, (float)random.NextDouble());
                dropper.spawnBoxSize = Mathf.Lerp(0.55f, 0.82f, (float)random.NextDouble());
            }
        }

        private static string ResolveRandomContext()
        {
            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            OnlineManager online = Object.FindFirstObjectByType<OnlineManager>();
            OnlineLobbyInfo lobby = online != null ? online.CurrentLobby : null;
            if (stageManager == null || !stageManager.IsOnlineStageActive || lobby == null)
                return "offline";
            if (!string.IsNullOrEmpty(lobby.LobbyId)) return "lobby:" + lobby.LobbyId;
            if (!string.IsNullOrEmpty(lobby.RoomCode)) return "room:" + lobby.RoomCode;
            return "online";
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash;
            }
        }
    }

    public static class StagePoseKeyFactory
    {
        private static readonly Color[] PoseColors =
        {
            new Color(0.93f, 0.24f, 0.2f, 1f),
            new Color(0.98f, 0.65f, 0.1f, 1f),
            new Color(0.14f, 0.66f, 0.9f, 1f),
            new Color(0.22f, 0.72f, 0.35f, 1f),
            new Color(0.63f, 0.3f, 0.9f, 1f),
            new Color(0.95f, 0.36f, 0.68f, 1f)
        };

        public static GameObject CreateKey(StageObjectData data, Transform parent, int pushableLayer)
        {
            data.size = new Vector2(Mathf.Max(0.9f, data.size.x), Mathf.Max(1.1f, data.size.y));
            GameObject root = CreateRoot(data, parent, StageObjectType.PoseCharacterKey.ToString());
            root.layer = pushableLayer;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.mass = 1.15f;
            body.gravityScale = 1.1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearDamping = 0.18f;
            body.angularDamping = 0.4f;
            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.68f, 0.88f);
            root.AddComponent<CarryableObject>();
            int species = Mathf.Clamp(data.spawnPattern / 100, 0, 2);
            int pose = Mathf.Abs(data.spawnPattern) % 100 % 30;
            StagePoseCharacterKey key = root.AddComponent<StagePoseCharacterKey>();
            key.Configure(species, pose);
            DrawPose(root.transform, species, pose, PoseColors[pose % PoseColors.Length], false);
            AddMetadata(root, data);
            return root;
        }

        public static GameObject CreateKeyhole(StageObjectData data, Transform parent)
        {
            GameObject root = CreateRoot(data, parent, StageObjectType.PoseCharacterKeyhole.ToString());
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(0.92f, 1.02f);
            trigger.isTrigger = true;
            int species = Mathf.Clamp(data.spawnPattern / 100, 0, 2);
            int pose = Mathf.Abs(data.spawnPattern) % 100 % 30;
            GameObject plate = StageGun.CreateSprite(root.transform, "Silhouette Plate", Vector2.zero, new Vector2(1.05f, 1.14f), new Color(0.86f, 0.87f, 0.78f, 0.96f), 19);
            plate.GetComponent<SpriteRenderer>().sprite = StageSurvivalController.GetCircleSprite();
            DrawPose(root.transform, species, pose, new Color(0.02f, 0.02f, 0.03f, 1f), true);
            StageGun.AddLine(root.transform, "Keyhole Glow", CirclePoints(28, 0.58f), 0.045f, new Color(0.15f, 0.8f, 1f, 0.8f), 25);
            if (data.objectId == "7-1_hole_human" || data.objectId == "7-1_hole_bird")
            {
                StagePosePassageUnlock passageUnlock = root.AddComponent<StagePosePassageUnlock>();
                passageUnlock.Configure(data.objectId == "7-1_hole_human"
                    ? "7-1_floor_2_one_way"
                    : "7-1_floor_3_one_way");
            }
            AddMetadata(root, data);
            return root;
        }

        public static GameObject CreateUpdraft(StageObjectData data, Transform parent)
        {
            GameObject root = CreateRoot(data, parent, StageObjectType.UpdraftZone.ToString());
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = Vector2.one;
            trigger.isTrigger = true;
            StageUpdraftZone zone = root.AddComponent<StageUpdraftZone>();
            zone.Configure(data.actionStrength > 0f ? data.actionStrength : 2.2f);
            for (int i = 0; i < 11; i++)
            {
                float x = -0.45f + i * 0.09f;
                float sway = i % 2 == 0 ? 0.026f : -0.026f;
                StageGun.AddLine(root.transform, "Updraft Wind Ribbon " + i, new[]
                {
                    new Vector2(x, -0.38f), new Vector2(x + sway, -0.18f),
                    new Vector2(x - sway, 0.04f), new Vector2(x + sway, 0.25f),
                    new Vector2(x, 0.43f)
                }, 0.028f, new Color(0.12f, 0.7f, 1f, 0.72f), 18);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = -0.4f + i * 0.2f;
                BuildUpwardFan(root.transform, x, data.size);
                float y = -0.24f + (i % 3) * 0.2f;
                StageGun.AddLine(root.transform, "Updraft Arrow " + i, new[]
                {
                    new Vector2(x, y - 0.08f), new Vector2(x, y + 0.09f),
                    new Vector2(x - 0.035f, y + 0.04f), new Vector2(x, y + 0.09f),
                    new Vector2(x + 0.035f, y + 0.04f)
                }, 0.022f, new Color(0.05f, 0.58f, 1f, 0.9f), 20);
            }
            AddMetadata(root, data);
            return root;
        }

        private static void BuildUpwardFan(Transform zone, float normalizedX, Vector2 zoneSize)
        {
            GameObject fan = new GameObject("Upward Fan");
            fan.transform.SetParent(zone, false);
            fan.transform.localPosition = new Vector3(normalizedX, -0.455f, -0.03f);
            fan.transform.localScale = new Vector3(2.35f / Mathf.Max(0.2f, zoneSize.x), 0.82f / Mathf.Max(0.2f, zoneSize.y), 1f);
            Color casing = new Color(0.23f, 0.64f, 0.92f, 0.96f);
            Color ink = new Color(0.04f, 0.22f, 0.4f, 1f);
            StageGun.CreateSprite(fan.transform, "Fan Stand", new Vector2(0f, -0.22f), new Vector2(1.05f, 0.28f), casing, 10);
            GameObject grill = StageGun.CreateSprite(fan.transform, "Fan Grill", new Vector2(0f, 0.08f), new Vector2(0.82f, 0.58f), new Color(0.78f, 0.94f, 1f, 0.98f), 11);
            grill.GetComponent<SpriteRenderer>().sprite = StageSurvivalController.GetCircleSprite();
            StageGun.AddLine(fan.transform, "Fan Blades", new[]
            {
                new Vector2(-0.35f, 0.08f), new Vector2(0.35f, 0.08f),
                new Vector2(0f, 0.08f), new Vector2(-0.24f, 0.29f),
                new Vector2(0f, 0.08f), new Vector2(0.24f, -0.13f)
            }, 0.065f, ink, 13);
            StageGun.AddLine(fan.transform, "Fan Up Arrow", new[]
            {
                new Vector2(0f, 0.42f), new Vector2(0f, 0.72f),
                new Vector2(-0.14f, 0.58f), new Vector2(0f, 0.72f), new Vector2(0.14f, 0.58f)
            }, 0.07f, new Color(0.1f, 0.65f, 1f, 0.95f), 14);
        }

        private static GameObject CreateRoot(StageObjectData data, Transform parent, string name)
        {
            GameObject root = new GameObject(data.objectId) { name = name };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(Mathf.Max(0.2f, data.size.x), Mathf.Max(0.2f, data.size.y), 1f);
            return root;
        }

        private static void AddMetadata(GameObject root, StageObjectData data)
        {
            StageEditorObject marker = root.AddComponent<StageEditorObject>();
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

        private static void DrawPose(Transform parent, int species, int pose, Color color, bool silhouette)
        {
            float armLeft = Mathf.Lerp(-145f, 35f, (pose % 6) / 5f) * Mathf.Deg2Rad;
            float armRight = Mathf.Lerp(-35f, 145f, ((pose / 2) % 6) / 5f) * Mathf.Deg2Rad;
            float legSpread = 0.12f + (pose % 5) * 0.055f;
            float width = silhouette ? 0.105f : 0.075f;
            if (species == 0)
            {
                AddCircle(parent, "Human Head", new Vector2(0f, 0.28f), new Vector2(0.34f, 0.34f), color, 23);
                StageGun.AddLine(parent, "Human Body", new[] { new Vector2(0f, 0.1f), new Vector2(0f, -0.28f) }, width, color, 24);
                StageGun.AddLine(parent, "Human Left Arm", new[] { new Vector2(0f, 0.02f), new Vector2(Mathf.Cos(armLeft) * 0.42f, 0.02f + Mathf.Sin(armLeft) * 0.42f) }, width, color, 24);
                StageGun.AddLine(parent, "Human Right Arm", new[] { new Vector2(0f, 0.02f), new Vector2(Mathf.Cos(armRight) * 0.42f, 0.02f + Mathf.Sin(armRight) * 0.42f) }, width, color, 24);
                StageGun.AddLine(parent, "Human Legs", new[] { new Vector2(0f, -0.28f), new Vector2(-legSpread, -0.55f), new Vector2(0f, -0.28f), new Vector2(legSpread, -0.55f) }, width, color, 24);
            }
            else if (species == 1)
            {
                AddCircle(parent, "Bird Body", new Vector2(0f, -0.02f), new Vector2(0.62f, 0.42f), color, 23);
                AddCircle(parent, "Bird Head", new Vector2(0.23f, 0.22f), new Vector2(0.32f, 0.32f), color, 24);
                float wing = 0.18f + (pose % 6) * 0.065f;
                StageGun.AddLine(parent, "Bird Wings", new[] { new Vector2(-0.05f, 0.02f), new Vector2(-0.42f, wing), new Vector2(-0.2f, -0.08f), new Vector2(0.12f, 0.04f), new Vector2(0.46f, wing * 0.8f) }, width, color, 25);
                StageGun.AddLine(parent, "Bird Beak", new[] { new Vector2(0.37f, 0.24f), new Vector2(0.58f, 0.17f), new Vector2(0.38f, 0.11f) }, width * 0.75f, color, 25);
                StageGun.AddLine(parent, "Bird Tail", new[] { new Vector2(-0.29f, -0.08f), new Vector2(-0.52f - legSpread, -0.22f), new Vector2(-0.42f, 0.02f) }, width, color, 24);
            }
            else
            {
                AddCircle(parent, "Turtle Shell", new Vector2(0f, -0.02f), new Vector2(0.68f, 0.55f), color, 23);
                AddCircle(parent, "Turtle Head", new Vector2(0.42f, 0.1f + (pose % 3) * 0.06f), new Vector2(0.27f, 0.25f), color, 24);
                float legY = -0.27f;
                float reach = 0.34f + (pose % 5) * 0.035f;
                StageGun.AddLine(parent, "Turtle Limbs", new[] { new Vector2(-0.2f, legY), new Vector2(-reach, -0.48f), new Vector2(0.2f, legY), new Vector2(reach, -0.48f) }, width, color, 24);
                StageGun.AddLine(parent, "Shell Pattern", new[] { new Vector2(-0.25f, 0f), new Vector2(0f, 0.2f), new Vector2(0.25f, 0f), new Vector2(0f, -0.2f), new Vector2(-0.25f, 0f) }, width * 0.65f, color * new Color(0.62f, 0.62f, 0.62f, 1f), 25);
            }
            DrawIdentityMarks(parent, pose, color, silhouette);
        }

        private static void DrawIdentityMarks(Transform parent, int pose, Color color, bool silhouette)
        {
            int code = pose + 1;
            Color markColor = silhouette ? new Color(0.02f, 0.02f, 0.03f, 1f) : color;
            Vector2[] positions =
            {
                new Vector2(-0.38f, 0.48f), new Vector2(-0.19f, 0.57f), new Vector2(0f, 0.61f),
                new Vector2(0.19f, 0.57f), new Vector2(0.38f, 0.48f)
            };
            for (int bit = 0; bit < positions.Length; bit++)
            {
                if ((code & (1 << bit)) == 0) continue;
                AddCircle(parent, "Pose Identity " + bit, positions[bit], Vector2.one * 0.105f, markColor, 27);
            }
        }

        private static void AddCircle(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject obj = StageGun.CreateSprite(parent, name, position, size, color, order);
            obj.GetComponent<SpriteRenderer>().sprite = StageSurvivalController.GetCircleSprite();
        }

        private static Vector2[] CirclePoints(int count, float radius)
        {
            Vector2[] points = new Vector2[count + 1];
            for (int i = 0; i <= count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }
    }

    public sealed class StagePoseCharacterKey : MonoBehaviour
    {
        private static readonly HashSet<StagePoseCharacterKey> ActiveKeys = new HashSet<StagePoseCharacterKey>();

        public static IEnumerable<StagePoseCharacterKey> Active => ActiveKeys;
        public int Species { get; private set; }
        public int Pose { get; private set; }
        public void Configure(int species, int pose) { Species = species; Pose = pose; }

        private void OnEnable() => ActiveKeys.Add(this);
        private void OnDisable() => ActiveKeys.Remove(this);
        private void OnDestroy() => ActiveKeys.Remove(this);
    }

    [DisallowMultipleComponent]
    public sealed class StagePosePassageUnlock : MonoBehaviour
    {
        private string targetObjectId;
        private Collider2D passageCollider;
        private PlatformEffector2D passageEffector;
        private GameObject lockedOutline;
        private bool unlocked;

        public void Configure(string objectId) => targetObjectId = objectId;

        private void Start()
        {
            ResolvePassage();
            ApplyVisualAndPhysics(unlocked);
        }

        public void ApplyUnlockedState()
        {
            unlocked = true;
            ResolvePassage();
            ApplyVisualAndPhysics(true);
        }

        private void ResolvePassage()
        {
            if (passageCollider != null || string.IsNullOrEmpty(targetObjectId)) return;
            StageEditorObject[] objects = Object.FindObjectsByType<StageEditorObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                StageEditorObject marker = objects[i];
                if (marker == null || marker.objectId != targetObjectId) continue;
                passageCollider = marker.GetComponent<Collider2D>();
                passageEffector = marker.GetComponent<PlatformEffector2D>();
                CreateLockedOutline(marker.transform);
                break;
            }
        }

        private void CreateLockedOutline(Transform passage)
        {
            if (passage == null || lockedOutline != null || passageCollider == null) return;
            Vector2 size = passageCollider.bounds.size;
            if (passageCollider is BoxCollider2D box) size = box.size;
            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;
            StageGun.AddLine(passage, "Locked Passage Outline", new[]
            {
                new Vector2(-halfX, -halfY), new Vector2(-halfX, halfY),
                new Vector2(halfX, halfY), new Vector2(halfX, -halfY),
                new Vector2(-halfX, -halfY)
            }, 0.055f, new Color(0.08f, 0.08f, 0.07f, 1f), 32);
            Transform outline = passage.Find("Locked Passage Outline");
            lockedOutline = outline != null ? outline.gameObject : null;
        }

        private void ApplyVisualAndPhysics(bool makeOneWay)
        {
            if (passageCollider == null) return;
            passageCollider.usedByEffector = makeOneWay;
            if (passageEffector != null) passageEffector.enabled = makeOneWay;
            if (lockedOutline != null) lockedOutline.SetActive(!makeOneWay);

            Transform passage = passageCollider.transform;
            for (int i = 0; i < passage.childCount; i++)
            {
                Transform child = passage.GetChild(i);
                if (child != null && child.name.StartsWith("One Way", System.StringComparison.Ordinal))
                    child.gameObject.SetActive(makeOneWay);
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageUpdraftZone : MonoBehaviour
    {
        private static readonly HashSet<StageUpdraftZone> ActiveZones = new HashSet<StageUpdraftZone>();
        private readonly HashSet<int> processedBodies = new HashSet<int>();
        private readonly Dictionary<int, float> birdHoverHeights = new Dictionary<int, float>();
        private readonly Dictionary<int, float> birdHoverPhases = new Dictionary<int, float>();
        private readonly List<Collider2D> overlapResults = new List<Collider2D>(128);
        private LineRenderer[] windRibbons;
        private Collider2D zoneCollider;
        private PlayerController2D[] playerCache = System.Array.Empty<PlayerController2D>();
        private float nextPlayerRefresh;
        private float floatSpeed;

        public void Configure(float speed) => floatSpeed = Mathf.Clamp(speed, 0.5f, 6f);

        private void OnEnable() => ActiveZones.Add(this);
        private void OnDisable() => ActiveZones.Remove(this);
        private void OnDestroy() => ActiveZones.Remove(this);

        public static bool TryGetPlayerLift(Vector2 position, out float liftSpeed)
        {
            liftSpeed = 0f;
            bool found = false;
            foreach (StageUpdraftZone zone in ActiveZones)
            {
                if (zone == null || !zone.isActiveAndEnabled) continue;
                Collider2D collider = zone.zoneCollider;
                if (collider == null)
                {
                    collider = zone.GetComponent<Collider2D>();
                    zone.zoneCollider = collider;
                }
                if (collider == null || !IsInsideWind(position, collider.bounds, 1.1f)) continue;
                liftSpeed = Mathf.Max(liftSpeed, Mathf.Max(3.2f, zone.floatSpeed * 2.4f));
                found = true;
            }
            return found;
        }

        private void Start()
        {
            zoneCollider = GetComponent<Collider2D>();
            windRibbons = GetComponentsInChildren<LineRenderer>(true);
        }

        private void Update()
        {
            if (windRibbons == null) return;
            for (int i = 0; i < windRibbons.Length; i++)
            {
                LineRenderer ribbon = windRibbons[i];
                if (ribbon == null || !ribbon.gameObject.name.StartsWith("Updraft Wind Ribbon")) continue;
                float alpha = 0.48f + Mathf.PingPong(Time.time * 0.55f + i * 0.17f, 0.36f);
                Color color = new Color(0.22f, 0.78f, 1f, alpha);
                ribbon.startColor = color;
                ribbon.endColor = new Color(color.r, color.g, color.b, alpha * 0.4f);
            }
        }

        private void FixedUpdate()
        {
            processedBodies.Clear();
            if (zoneCollider == null) return;

            // Do not rely solely on trigger callbacks here.  Stage objects can use
            // layers which are excluded by the 2D collision matrix, but the wind
            // still needs to affect every bird visibly inside the updraft.
            Bounds bounds = zoneCollider.bounds;
            if (Time.unscaledTime >= nextPlayerRefresh)
            {
                nextPlayerRefresh = Time.unscaledTime + 0.5f;
                playerCache = Object.FindObjectsByType<PlayerController2D>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            }
            float playerLift = Mathf.Max(3.2f, floatSpeed * 2.4f);
            for (int i = 0; i < playerCache.Length; i++)
            {
                PlayerController2D player = playerCache[i];
                if (player != null && IsInsideWind(player.transform.position, bounds, 1.1f))
                    player.NotifyStageUpdraft(playerLift);
            }
            overlapResults.Clear();
            ContactFilter2D filter = new ContactFilter2D().NoFilter();
            Physics2D.OverlapBox(
                bounds.center,
                bounds.size,
                transform.eulerAngles.z,
                filter,
                overlapResults);
            for (int i = 0; i < overlapResults.Count; i++) ApplyWind(overlapResults[i]);

            // Pose keys and hand-drawn players can live on collision layers which
            // are intentionally absent from normal overlap queries.  Their world
            // positions are therefore checked explicitly as a reliable fallback.
            foreach (StagePoseCharacterKey key in StagePoseCharacterKey.Active)
            {
                if (key == null || key.Species != 1) continue;
                Rigidbody2D body = key.GetComponent<Rigidbody2D>();
                if (body != null && IsInsideWind(body.position, bounds, 0.9f))
                    ApplyWindBody(body, true, false);
            }

        }

        private void OnTriggerStay2D(Collider2D other)
        {
            ApplyWind(other);
        }

        private void ApplyWind(Collider2D other)
        {
            if (other == null) return;
            Rigidbody2D body = other.attachedRigidbody;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null && body != null)
                player = body.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                player.NotifyStageUpdraft(Mathf.Max(3.2f, floatSpeed * 2.4f));
                return;
            }

            if (body == null || body.bodyType != RigidbodyType2D.Dynamic) return;
            bool birdKey = body.GetComponentInParent<StagePoseCharacterKey>()?.Species == 1;
            if (!birdKey) return;
            ApplyWindBody(body, true, false);
        }

        private void ApplyWindBody(Rigidbody2D body, bool birdKey, bool birdPlayer)
        {
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic) return;
            if (!birdKey && !birdPlayer) return;
            if (!processedBodies.Add(body.GetInstanceID())) return;
            if (birdKey)
            {
                int id = body.GetInstanceID();
                if (!birdHoverHeights.TryGetValue(id, out float hoverY))
                {
                    Bounds bounds = zoneCollider != null ? zoneCollider.bounds : new Bounds(transform.position, new Vector3(8f, 4f, 1f));
                    float normalizedHeight = Mathf.Abs(id % 997) / 996f;
                    hoverY = Mathf.Lerp(bounds.min.y + 1.35f, bounds.max.y - 1.1f, normalizedHeight);
                    birdHoverHeights[id] = hoverY;
                    birdHoverPhases[id] = Mathf.Abs(id % 97) * 0.19f;
                }
                float desiredY = hoverY + Mathf.Sin(Time.time * 1.65f + birdHoverPhases[id]) * 0.38f;
                float desiredVelocity = Mathf.Clamp((desiredY - body.position.y) * 4.8f, -4.2f, 4.2f);
                body.linearVelocity = new Vector2(
                    Mathf.Lerp(body.linearVelocity.x, Mathf.Sin(Time.time * 0.8f + birdHoverPhases[id]) * 0.22f, 0.035f),
                    Mathf.MoveTowards(body.linearVelocity.y, desiredVelocity, 38f * Time.fixedDeltaTime));
                return;
            }
            float playerBob = Mathf.Sin(Time.time * 2.1f + body.position.x * 0.22f) * 0.45f;
            float riseSpeed = Mathf.Max(2.4f, floatSpeed) + playerBob;
            body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.MoveTowards(body.linearVelocity.y, riseSpeed, 32f * Time.fixedDeltaTime));
        }

        private static bool IsInsideWind(Vector2 point, Bounds bounds, float margin)
        {
            return point.x >= bounds.min.x - margin && point.x <= bounds.max.x + margin
                && point.y >= bounds.min.y - margin && point.y <= bounds.max.y + margin;
        }
    }
}
