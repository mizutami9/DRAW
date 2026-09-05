using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StagePoseTowerRandomizer
    {
        private static readonly Dictionary<string, List<int>> RecentChoices = new Dictionary<string, List<int>>();

        public static void Prepare(StageData data)
        {
            if (data == null || data.id != "7-1" || data.objects == null) return;

            string context = ResolveRandomContext();
            int seed = context == "offline"
                ? System.Guid.NewGuid().GetHashCode()
                : StableHash(context + "|7-1");
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

            List<int> available = new List<int>(candidates.Count);
            bool keepLocalHistory = historyKey.StartsWith("offline|", System.StringComparison.Ordinal);
            List<int> recent = null;
            if (keepLocalHistory && !RecentChoices.TryGetValue(historyKey, out recent))
            {
                recent = new List<int>(10);
                RecentChoices[historyKey] = recent;
            }
            for (int i = 0; i < candidates.Count; i++)
                if (!keepLocalHistory || !recent.Contains(candidates[i].spawnPattern)) available.Add(i);
            if (available.Count == 0)
            {
                recent.Clear();
                for (int i = 0; i < candidates.Count; i++) available.Add(i);
            }
            int choice = available[random.Next(available.Count)];
            StageObjectData selected = candidates[choice];
            if (keepLocalHistory)
            {
                recent.Add(selected.spawnPattern);
                if (recent.Count > 10) recent.RemoveAt(0);
            }
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
            string revision = "|stage:" + lobby.StageRevision + "|retry:" + lobby.RetryRevision;
            if (!string.IsNullOrEmpty(lobby.LobbyId)) return "lobby:" + lobby.LobbyId + revision;
            if (!string.IsNullOrEmpty(lobby.RoomCode)) return "room:" + lobby.RoomCode + revision;
            return "online" + revision;
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
            plate.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
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
            grill.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
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
            obj.GetComponent<SpriteRenderer>().sprite = DoodleRuntimeAssets.CircleSprite;
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

    // ────────────────────────────────────────────────────────
    // 書き直し可能ゾーン (Redraw Zone)
    // ────────────────────────────────────────────────────────

    public static class StageRedrawZoneFactory
    {
        public static StageRedrawZone CreateRuntimeFloorZone(
            Transform parent, string objectId, Vector2 floorCenter,
            float floorWidth, float triggerHeight)
        {
            float safeWidth = Mathf.Max(0.5f, floorWidth);
            float safeHeight = Mathf.Max(0.5f, triggerHeight);
            StageObjectData data = StageObjectFactory.CreateDefaultData(
                StageObjectType.RedrawZone,
                floorCenter + Vector2.up * safeHeight * 0.5f);
            data.objectId = objectId;
            data.size = new Vector2(safeWidth, safeHeight);
            GameObject root = CreateRedrawZone(data, parent);
            StageRedrawZone zone = root != null ? root.GetComponent<StageRedrawZone>() : null;
            zone?.AlignToFloor(floorCenter, safeWidth);
            return zone;
        }

        public static GameObject CreateRedrawZone(StageObjectData data, Transform parent)
        {
            GameObject root = new GameObject(data.objectId) { name = StageObjectType.RedrawZone.ToString() };
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
            root.transform.localScale = new Vector3(
                Mathf.Max(0.5f, data.size.x),
                Mathf.Max(0.5f, data.size.y),
                1f);

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = Vector2.one;
            trigger.isTrigger = true;

            StageRedrawZone zone = root.AddComponent<StageRedrawZone>();
            zone.BuildVisuals(data.size);

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
            return root;
        }
    }

    /// <summary>
    /// 書き直し可能ゾーン。
    /// メモ帳のサイズ＝書き直し判定領域。
    /// ゆったりと漂う外周OKエフェクト。
    /// クレヨン画像「D R A W !」の2秒周期ウェーブ跳躍。
    /// </summary>
    public sealed class StageRedrawZone : MonoBehaviour
    {
        private static readonly System.Collections.Generic.HashSet<StageRedrawZone> ActiveZones
            = new System.Collections.Generic.HashSet<StageRedrawZone>();

        private Collider2D zoneCollider;

        private Transform badgeTransform;          // 文字群ルート
        private Transform[] charTransforms;        // 各クレヨン文字(D, R, A, W, !)のTransform
        private Vector3[] charBaseLocalPositions;  // 各文字の基本位置

        private SpriteRenderer paperRenderer;      // お絵描き風メモ用紙スプライト
        private SpriteRenderer baseWhitePaperRenderer; // 【白紙保証】100%不透明白ベース紙
        private SpriteRenderer gridOverlayRenderer; // 超淡い視認性優先の方眼

        private GameObject[] perimeterSparkles;
        private float[] sparklePhases;
        private float[] sparkleSpeeds;
        private SpriteRenderer auraGlowRenderer;
        private sealed class FloorStroke
        {
            public LineRenderer Renderer;
            public Color BaseColor;
            public float X;
        }

        private sealed class FloorParticle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 Start;
            public float StartedAt;
            public float Duration;
            public float Drift;
        }

        private readonly List<FloorStroke> floorStrokes = new List<FloorStroke>();
        private readonly List<FloorParticle> floorParticles = new List<FloorParticle>();
        private Transform floorVisualRoot;
        private Transform floorDrawMark;
        private Vector3 floorDrawMarkBasePosition;
        private float floorVisualWidth;
        private float nextFloorParticleBurstAt;

        private void OnEnable()  => ActiveZones.Add(this);
        private void OnDisable() => ActiveZones.Remove(this);
        private void OnDestroy() => ActiveZones.Remove(this);

        private void Start()
        {
            zoneCollider = GetComponent<Collider2D>();
        }

        // ─── 静的API ───

        public static bool HasActiveZones() => ActiveZones.Count > 0;

        public static bool IsPlayerInZone(PlayerController2D player)
        {
            if (player == null) return false;
            Vector2 pos = player.transform.position;
            foreach (StageRedrawZone zone in ActiveZones)
            {
                if (zone == null || !zone.isActiveAndEnabled) continue;
                Collider2D col = zone.zoneCollider != null ? zone.zoneCollider : zone.GetComponent<Collider2D>();
                if (col != null && col.OverlapPoint(pos)) return true;

                Vector3 localPos = zone.transform.InverseTransformPoint(pos);
                if (Mathf.Abs(localPos.x) <= 0.55f && Mathf.Abs(localPos.y) <= 0.55f) return true;
            }
            return false;
        }

        public void BuildVisuals(Vector2 size)
        {
            ClearFloorVisuals();
            BuildFloorOnlyVisuals(size);
        }

        /// <summary>
        /// Keeps the redraw trigger untouched while stretching the recognizable
        /// crayon treatment across the actual runtime-built floor.
        /// </summary>
        public void AlignFloorVisual(Vector2 worldCenter, float worldWidth)
        {
            ClearFloorVisuals();
            BuildFloorOnlyVisualsAtWorld(worldCenter, Mathf.Max(0.5f, worldWidth));
        }

        public void AlignToFloor(Vector2 worldCenter, float worldWidth)
        {
            worldWidth = Mathf.Max(0.5f, worldWidth);
            Vector3 worldPosition = transform.position;
            worldPosition.x = worldCenter.x;
            transform.position = worldPosition;

            float parentScaleX = transform.parent != null
                ? Mathf.Max(0.001f, Mathf.Abs(transform.parent.lossyScale.x))
                : 1f;
            Vector3 localScale = transform.localScale;
            localScale.x = worldWidth / parentScaleX;
            transform.localScale = localScale;

            AlignFloorVisual(worldCenter, worldWidth);
        }

        public void HideFloorVisual()
        {
            ClearFloorVisuals();
        }

        private void ClearFloorVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            floorStrokes.Clear();
            floorParticles.Clear();
            floorVisualRoot = null;
            floorDrawMark = null;
            floorVisualWidth = 0f;
        }

        private void BuildFloorOnlyVisuals(Vector2 size)
        {
            float width = Mathf.Max(0.5f, size.x);
            float height = Mathf.Max(0.5f, size.y);
            Transform floorRoot = new GameObject("Crayon Redraw Floor").transform;
            floorRoot.SetParent(transform, false);
            floorRoot.localPosition = new Vector3(0f, -0.5f, -0.08f);
            floorRoot.localScale = new Vector3(1f / width, 1f / height, 1f);
            floorVisualRoot = floorRoot;
            floorVisualWidth = width;
            nextFloorParticleBurstAt = Time.unscaledTime + Random.Range(3f, 5f);

            Vector2 floorSize = new Vector2(width + 0.08f, 0.64f);
            StageEscortController.AddFilledRect(floorRoot, "Uneven Cyan Undercoat",
                Vector2.zero, floorSize, new Color(0.54f, 0.87f, 0.94f, 0.17f), 37);

            AddCrayonPaintBands(floorRoot, width);

            AddLooseDoodles(floorRoot, width);
            AddFloorEndCrayon(floorRoot, -width * 0.5f, false);
            AddFloorEndCrayon(floorRoot, width * 0.5f, true);
            AddSmallDrawMark(floorRoot, width);
        }

        private void BuildFloorOnlyVisualsAtWorld(Vector2 worldCenter, float width)
        {
            float rootScaleX = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
            float rootScaleY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            Transform floorRoot = new GameObject("Crayon Redraw Floor").transform;
            floorRoot.SetParent(transform, true);
            floorRoot.position = new Vector3(worldCenter.x, worldCenter.y, transform.position.z - 0.08f);
            floorRoot.rotation = Quaternion.identity;
            floorRoot.localScale = new Vector3(1f / rootScaleX, 1f / rootScaleY, 1f);
            floorVisualRoot = floorRoot;
            floorVisualWidth = width;
            nextFloorParticleBurstAt = Time.unscaledTime + Random.Range(3f, 5f);

            StageEscortController.AddFilledRect(floorRoot, "Uneven Cyan Undercoat",
                Vector2.zero, new Vector2(width + 0.08f, 0.64f),
                new Color(0.54f, 0.87f, 0.94f, 0.17f), 37);
            AddCrayonPaintBands(floorRoot, width);
            AddLooseDoodles(floorRoot, width);
            AddFloorEndCrayon(floorRoot, -width * 0.5f, false);
            AddFloorEndCrayon(floorRoot, width * 0.5f, true);
            AddSmallDrawMark(floorRoot, width);
        }

        private void AddCrayonPaintBands(Transform parent, float width)
        {
            Color[] palette =
            {
                new Color(0.06f, 0.61f, 0.9f, 0.7f),
                new Color(0.94f, 0.22f, 0.28f, 0.67f),
                new Color(1f, 0.73f, 0.08f, 0.7f),
                new Color(0.06f, 0.72f, 0.62f, 0.66f),
                new Color(0.92f, 0.3f, 0.66f, 0.62f)
            };
            int patchCount = Mathf.Clamp(Mathf.CeilToInt(width / 1.35f), 4, 24);
            float patchWidth = width / patchCount;
            for (int patch = 0; patch < patchCount; patch++)
            {
                Color color = palette[patch % palette.Length];
                float left = -width * 0.5f + patch * patchWidth - 0.12f;
                float right = left + patchWidth + 0.24f;
                for (int pass = 0; pass < 6; pass++)
                {
                    GameObject strokeObject = new GameObject("Crayon Scrub " + patch + "-" + pass);
                    strokeObject.transform.SetParent(parent, false);
                    LineRenderer line = strokeObject.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 6;
                    line.startWidth = 0.065f + (pass % 3) * 0.012f;
                    line.endWidth = line.startWidth * (0.86f + (patch % 2) * 0.1f);
                    line.numCapVertices = 3;
                    line.numCornerVertices = 2;
                    line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                    line.startColor = color;
                    line.endColor = new Color(color.r, color.g, color.b, color.a * 0.82f);
                    line.sortingOrder = 38;
                    float baseY = -0.27f + pass * 0.105f + Mathf.Sin(patch * 1.7f + pass) * 0.025f;
                    for (int point = 0; point < 6; point++)
                    {
                        float t = point / 5f;
                        float x = Mathf.Lerp(left, right, t)
                            + Mathf.Sin(point * 2.9f + pass * 1.3f) * 0.035f;
                        float y = baseY + Mathf.Sin(point * 3.7f + patch + pass * 0.8f) * 0.045f;
                        line.SetPosition(point, new Vector3(x, y, 0f));
                    }
                    floorStrokes.Add(new FloorStroke
                    {
                        Renderer = line,
                        BaseColor = color,
                        X = (left + right) * 0.5f
                    });
                }
            }
        }

        private static void AddFloorEndCrayon(Transform parent, float x, bool flip)
        {
            Sprite crayon = Resources.Load<Sprite>("StageDecorations/CrayonSet/crayon");
            if (crayon == null || crayon.bounds.size.y <= 0f) return;
            GameObject marker = new GameObject(flip ? "End Crayon Right" : "End Crayon Left");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(x + (flip ? -0.14f : 0.14f), 0.48f, -0.03f);
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, -45f + (flip ? -3f : 3f));
            float scale = 1.18f / crayon.bounds.size.y;
            marker.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = crayon;
            renderer.color = new Color(1f, 1f, 1f, 0.94f);
            renderer.sortingOrder = 41;
        }

        private static void AddSmallDrawMark(Transform parent, float floorWidth)
        {
            string[] paths =
            {
                "StageDecorations/CrayonSet/redraw-letter-d",
                "StageDecorations/CrayonSet/redraw-letter-r",
                "StageDecorations/CrayonSet/redraw-letter-a",
                "StageDecorations/CrayonSet/redraw-letter-w",
                "StageDecorations/CrayonSet/redraw-letter-excl"
            };
            float startX = -0.68f;
            Transform mark = new GameObject("Small Floor DRAW Mark").transform;
            mark.SetParent(parent, false);
            mark.localPosition = Vector3.zero;
            StageRedrawZone owner = parent.GetComponentInParent<StageRedrawZone>();
            if (owner != null)
            {
                owner.floorDrawMark = mark;
                owner.floorDrawMarkBasePosition = mark.localPosition;
            }
            for (int i = 0; i < paths.Length; i++)
            {
                Sprite sprite = Resources.Load<Sprite>(paths[i]);
                if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f) continue;
                GameObject letter = new GameObject("Floor DRAW Letter " + i);
                letter.transform.SetParent(mark, false);
                letter.transform.localPosition = new Vector3(startX + i * 0.34f,
                    0.58f + (i % 2 == 0 ? 0.025f : -0.025f), -0.02f);
                letter.transform.localRotation = Quaternion.Euler(0f, 0f,
                    i % 2 == 0 ? -3f : 2.5f);
                float scale = 0.34f / sprite.bounds.size.y;
                letter.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer renderer = letter.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 40;
            }
            Color accent = new Color(0.04f, 0.58f, 0.86f, 0.68f);
            StageEscortController.AddLine(mark, new Vector2(-1.0f, 0.48f),
                new Vector2(-0.82f, 0.42f), 0.04f, accent, 40);
            StageEscortController.AddLine(mark, new Vector2(-1.04f, 0.62f),
                new Vector2(-0.84f, 0.6f), 0.04f, accent, 40);
            StageEscortController.AddLine(mark, new Vector2(0.83f, 0.42f),
                new Vector2(1.02f, 0.49f), 0.04f, accent, 40);
            StageEscortController.AddLine(mark, new Vector2(0.84f, 0.6f),
                new Vector2(1.04f, 0.63f), 0.04f, accent, 40);
        }

        private static void AddLooseDoodles(Transform parent, float width)
        {
            Color blue = new Color(0.08f, 0.5f, 0.82f, 0.34f);
            Color yellow = new Color(0.92f, 0.65f, 0.08f, 0.34f);
            Color red = new Color(0.84f, 0.18f, 0.16f, 0.3f);
            Color green = new Color(0.08f, 0.56f, 0.28f, 0.3f);
            float usable = Mathf.Max(1.8f, width - 2.4f);
            int motifCount = Mathf.Clamp(Mathf.FloorToInt(usable / 1.35f), 3, 12);
            for (int i = 0; i < motifCount; i++)
            {
                float x = Mathf.Lerp(-usable * 0.5f, usable * 0.5f,
                    motifCount <= 1 ? 0.5f : i / (float)(motifCount - 1));
                Color color = i % 4 == 0 ? yellow : i % 4 == 1 ? blue : i % 4 == 2 ? red : green;
                AddTinyDoodle(parent, new Vector2(x, -0.02f + Mathf.Sin(i * 1.8f) * 0.1f),
                    0.12f + i % 3 * 0.025f, i % 4, color);
            }

            for (int group = 0; group < Mathf.Clamp(Mathf.CeilToInt(width / 5f), 1, 5); group++)
            {
                float center = Mathf.Lerp(-width * 0.34f, width * 0.34f,
                    group / (float)Mathf.Max(1, Mathf.CeilToInt(width / 5f) - 1));
                Vector2 previous = new Vector2(center - 0.48f, -0.16f);
                for (int segment = 1; segment <= 7; segment++)
                {
                    Vector2 next = new Vector2(center - 0.48f + segment * 0.14f,
                        Mathf.Sin(segment * 2.35f + group) * 0.2f);
                    StageEscortController.AddLine(parent, previous, next, 0.04f,
                        segment % 3 == 0 ? yellow : blue, 39);
                    previous = next;
                }
            }
        }

        private static void AddTinyDoodle(
            Transform parent, Vector2 center, float radius, int kind, Color color)
        {
            List<Vector2> points = new List<Vector2>();
            if (kind == 0)
            {
                for (int i = 0; i < 9; i++)
                {
                    float angle = i / 8f * Mathf.PI * 2f;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            else if (kind == 1)
            {
                points.Add(center + new Vector2(0f, radius));
                points.Add(center + new Vector2(-radius, -radius));
                points.Add(center + new Vector2(radius, -radius * 0.82f));
                points.Add(points[0]);
            }
            else if (kind == 2)
            {
                points.Add(center + new Vector2(-radius, -radius));
                points.Add(center + new Vector2(-radius * 0.9f, radius));
                points.Add(center + new Vector2(radius, radius * 0.9f));
                points.Add(center + new Vector2(radius, -radius));
                points.Add(points[0]);
            }
            else
            {
                for (int i = 0; i <= 10; i++)
                {
                    float angle = -Mathf.PI * 0.5f + i / 10f * Mathf.PI * 2f;
                    float r = i % 2 == 0 ? radius : radius * 0.42f;
                    points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                }
            }
            for (int i = 1; i < points.Count; i++)
                StageEscortController.AddLine(parent, points[i - 1], points[i], 0.035f, color, 39);
        }

        private static void AddRepeatedPencilMarks(Transform parent, float width)
        {
            Sprite pencil = Resources.Load<Sprite>("StageDecorations/CrayonSet/pencil");
            if (pencil == null || pencil.bounds.size.y <= 0f) return;
            int count = Mathf.Clamp(Mathf.FloorToInt(width / 1.25f), 3, 18);
            for (int i = 0; i < count; i++)
            {
                GameObject mark = new GameObject("Faint Pencil Floor Mark " + i);
                mark.transform.SetParent(parent, false);
                mark.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.42f, width * 0.42f, count <= 1 ? 0.5f : i / (float)(count - 1)),
                    -0.02f + Mathf.Sin(i * 2.1f) * 0.08f, 0.02f);
                mark.transform.localRotation = Quaternion.Euler(0f, 0f, 43f + Mathf.Sin(i) * 8f);
                float scale = (0.2f + i % 3 * 0.025f) / pencil.bounds.size.y;
                mark.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer renderer = mark.AddComponent<SpriteRenderer>();
                renderer.sprite = pencil;
                renderer.color = new Color(0.12f, 0.48f, 0.68f, 0.13f);
                renderer.sortingOrder = 39;
            }
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            if (floorDrawMark != null)
            {
                floorDrawMark.localPosition = floorDrawMarkBasePosition + new Vector3(
                    Mathf.Sin(time * 5.1f) * 0.012f,
                    Mathf.Sin(time * 6.3f + 0.8f) * 0.009f,
                    0f);
                floorDrawMark.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(time * 4.4f) * 0.7f);
            }

            float waveProgress = Mathf.Repeat(time, 2f) * 0.5f;
            float waveX = Mathf.Lerp(-floorVisualWidth * 0.5f, floorVisualWidth * 0.5f, waveProgress);
            for (int i = 0; i < floorStrokes.Count; i++)
            {
                FloorStroke stroke = floorStrokes[i];
                if (stroke?.Renderer == null) continue;
                float strength = Mathf.Clamp01(1f - Mathf.Abs(stroke.X - waveX) / 0.9f);
                Color color = Color.Lerp(stroke.BaseColor,
                    new Color(Mathf.Min(1f, stroke.BaseColor.r * 1.3f + 0.08f),
                        Mathf.Min(1f, stroke.BaseColor.g * 1.3f + 0.08f),
                        Mathf.Min(1f, stroke.BaseColor.b * 1.3f + 0.08f),
                        Mathf.Min(0.9f, stroke.BaseColor.a + 0.24f)), strength);
                stroke.Renderer.startColor = color;
                stroke.Renderer.endColor = color;
            }

            if (floorVisualRoot != null && time >= nextFloorParticleBurstAt)
            {
                SpawnFloorParticleBurst(time);
                nextFloorParticleBurstAt = time + Random.Range(3f, 5f);
            }
            UpdateFloorParticles(time);
        }

        private void SpawnFloorParticleBurst(float time)
        {
            Color[] colors =
            {
                new Color(0.08f, 0.65f, 0.86f), new Color(0.95f, 0.66f, 0.08f),
                new Color(0.88f, 0.2f, 0.18f), new Color(0.1f, 0.64f, 0.3f)
            };
            int count = Random.Range(2, 4);
            for (int i = 0; i < count; i++)
            {
                GameObject particle = new GameObject("Floating Crayon Grain");
                particle.transform.SetParent(floorVisualRoot, false);
                Vector3 start = new Vector3(Random.Range(-floorVisualWidth * 0.42f,
                    floorVisualWidth * 0.42f), Random.Range(0.16f, 0.3f), -0.04f);
                particle.transform.localPosition = start;
                float scale = Random.Range(0.07f, 0.13f);
                particle.transform.localScale = Vector3.one * scale;
                SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
                renderer.sprite = i % 3 == 2 ? DoodleRuntimeAssets.CircleSprite : DoodleRuntimeAssets.SquareSprite;
                renderer.color = colors[Random.Range(0, colors.Length)];
                renderer.sortingOrder = 41;
                if (i % 3 == 1) particle.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                floorParticles.Add(new FloorParticle
                {
                    Transform = particle.transform,
                    Renderer = renderer,
                    Start = start,
                    StartedAt = time,
                    Duration = Random.Range(0.9f, 1.35f),
                    Drift = Random.Range(-0.18f, 0.18f)
                });
            }
        }

        private void UpdateFloorParticles(float time)
        {
            for (int i = floorParticles.Count - 1; i >= 0; i--)
            {
                FloorParticle particle = floorParticles[i];
                if (particle?.Transform == null || particle.Renderer == null)
                {
                    floorParticles.RemoveAt(i);
                    continue;
                }
                float progress = Mathf.Clamp01((time - particle.StartedAt) / particle.Duration);
                particle.Transform.localPosition = particle.Start + new Vector3(
                    particle.Drift * progress,
                    Mathf.Sin(progress * Mathf.PI * 0.5f) * 0.72f,
                    0f);
                particle.Transform.localRotation *= Quaternion.Euler(0f, 0f,
                    Time.unscaledDeltaTime * 42f);
                Color color = particle.Renderer.color;
                color.a = Mathf.Clamp01(1f - progress) * 0.72f;
                particle.Renderer.color = color;
                if (progress < 1f) continue;
                Destroy(particle.Transform.gameObject);
                floorParticles.RemoveAt(i);
            }
        }

        private void BuildLegacyVisuals(Vector2 size)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            // 1. 【要件】メモ帳のサイズ＝判定領域（スプライトサイズを1.32fに拡大して紙面領域を判定領域に完全フィット）
            GameObject paperObj = new GameObject("TornNotepadPaper");
            paperObj.transform.SetParent(transform, false);
            paperObj.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            paperObj.transform.localRotation = Quaternion.Euler(0f, 0f, -0.75f);
            paperObj.transform.localScale = Vector3.one;

            // 白紙ベース
            GameObject basePaperObj = new GameObject("BaseWhitePaper");
            basePaperObj.transform.SetParent(paperObj.transform, false);
            basePaperObj.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            baseWhitePaperRenderer = basePaperObj.AddComponent<SpriteRenderer>();
            baseWhitePaperRenderer.sprite = CreateRoundedRectSprite(128, 128, 6);
            baseWhitePaperRenderer.drawMode = SpriteDrawMode.Sliced;
            baseWhitePaperRenderer.size = new Vector2(1.24f, 1.24f);
            baseWhitePaperRenderer.color = new Color(0.98f, 0.98f, 0.95f, 1f);
            baseWhitePaperRenderer.sortingOrder = -40; // ステージ背景の前、ゲーム要素（床・プレイヤー）の後ろ

            paperRenderer = paperObj.AddComponent<SpriteRenderer>();

            Sprite paperSprite = Resources.Load<Sprite>("StageDecorations/CrayonSet/torn-notepad-paper");
            if (paperSprite != null)
            {
                paperRenderer.sprite = paperSprite;
                paperRenderer.drawMode = SpriteDrawMode.Sliced;
                // メモ帳の大きさを判定領域（1.0f）の隅々まで行き渡る1.32fへ拡大
                paperRenderer.size = new Vector2(1.32f, 1.32f);
                paperRenderer.color = new Color(1f, 1f, 1f, 1f);
                paperRenderer.sortingOrder = -39;
            }
            else
            {
                paperRenderer.sprite = CreateRoundedRectSprite(128, 128, 8);
                paperRenderer.color = new Color(0.98f, 0.98f, 0.95f, 1f);
                paperRenderer.sortingOrder = -39;
            }

            // 超淡い視認性優先の方眼
            GameObject gridObj = new GameObject("GridOverlay");
            gridObj.transform.SetParent(paperObj.transform, false);
            gridObj.transform.localPosition = new Vector3(0f, 0f, -0.005f);
            gridObj.transform.localScale = new Vector3(1.24f / 2.56f, 1.24f / 2.56f, 1f);
            gridOverlayRenderer = gridObj.AddComponent<SpriteRenderer>();
            gridOverlayRenderer.sprite = CreateFaintGridSprite(256, 256);
            gridOverlayRenderer.color = new Color(0.2f, 0.6f, 0.9f, 0.04f);
            gridOverlayRenderer.sortingOrder = -38;

            // 2. 超ゆったり漂う外周キラキラエフェクト（水色四角枠オーラはご要望により撤去）
            int sparkleCount = 10;
            perimeterSparkles = new GameObject[sparkleCount];
            sparklePhases = new float[sparkleCount];
            sparkleSpeeds = new float[sparkleCount];

            Color[] sparkleColors = new Color[]
            {
                new Color(1f, 0.85f, 0.2f, 0.9f),
                new Color(0.3f, 0.85f, 1f, 0.9f),
                new Color(1f, 0.4f, 0.75f, 0.9f),
                new Color(0.3f, 0.9f, 0.5f, 0.9f),
                new Color(0.9f, 0.6f, 1f, 0.9f)
            };

            for (int s = 0; s < sparkleCount; s++)
            {
                GameObject spObj = new GameObject("OKSparkle_" + s);
                spObj.transform.SetParent(transform, false);
                spObj.transform.localScale = new Vector3(0.22f, 0.22f, 1f);

                SpriteRenderer sr = spObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateRoundedRectSprite(32, 32, 8);
                sr.color = sparkleColors[s % sparkleColors.Length];
                sr.sortingOrder = -30; // 背景レイヤー配置（ゲーム要素の背面）

                perimeterSparkles[s] = spObj;
                sparklePhases[s] = s * (Mathf.PI * 2f / sparkleCount);
                sparkleSpeeds[s] = 0.8f + (s % 3) * 0.2f;
            }

            // 3. 本物クレヨン画像「D R A W !」（メモ帳の紙面上部に1.5倍の程よいサイズで配置）
            GameObject badgeObj = new GameObject("CrayonImageDrawTextGroup");
            badgeObj.transform.SetParent(transform, false);
            badgeObj.transform.localPosition = new Vector3(0f, 0.16f, -0.2f);
            badgeTransform = badgeObj.transform;

            string[] crayonResourcePaths = new string[]
            {
                "StageDecorations/CrayonSet/redraw-letter-d",
                "StageDecorations/CrayonSet/redraw-letter-r",
                "StageDecorations/CrayonSet/redraw-letter-a",
                "StageDecorations/CrayonSet/redraw-letter-w",
                "StageDecorations/CrayonSet/redraw-letter-excl"
            };

            int count = crayonResourcePaths.Length;
            charTransforms = new Transform[count];
            charBaseLocalPositions = new Vector3[count];

            float[] tilts = new float[] { -2.5f, 2.0f, -1.2f, 3.0f, -2.0f };
            // DRAW! の1.5倍拡大に合わせて文字間隔を広げる
            float[] customXOffsets = new float[] { -1.38f, -0.69f, 0.00f, 0.75f, 1.41f };

            Font fallbackFont = GetYomogiFont();
            char[] fallbackChars = new char[] { 'D', 'R', 'A', 'W', '!' };

            for (int i = 0; i < count; i++)
            {
                GameObject charGroup = new GameObject("CrayonChar_" + i);
                charGroup.transform.SetParent(badgeTransform, false);

                float baseX = customXOffsets[i];
                float baseY = (i % 2 == 0 ? 0.012f : -0.012f);
                Vector3 basePos = new Vector3(baseX, baseY, -0.06f);

                charGroup.transform.localPosition = basePos;
                charGroup.transform.localRotation = Quaternion.Euler(0f, 0f, tilts[i]);

                charTransforms[i] = charGroup.transform;
                charBaseLocalPositions[i] = basePos;

                Sprite letterSprite = Resources.Load<Sprite>(crayonResourcePaths[i]);
                if (letterSprite != null)
                {
                    GameObject spriteObj = new GameObject("CrayonSprite");
                    spriteObj.transform.SetParent(charGroup.transform, false);
                    // ご要望により直前の1.5倍サイズ（0.13f）に拡大
                    spriteObj.transform.localScale = new Vector3(0.13f, 0.13f, 1f);

                    SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
                    sr.sprite = letterSprite;
                    sr.sortingOrder = -20; // メモ用紙の上、ゲーム要素の背面
                }
                else
                {
                    GameObject textObj = new GameObject("FallbackText");
                    textObj.transform.SetParent(charGroup.transform, false);
                    TextMesh mainText = textObj.AddComponent<TextMesh>();
                    mainText.text = fallbackChars[i].ToString();
                    mainText.fontSize = 58;
                    mainText.characterSize = 0.16f;
                    mainText.alignment = TextAlignment.Center;
                    mainText.anchor = TextAnchor.MiddleCenter;
                    mainText.color = new Color(0.18f, 0.65f, 0.95f, 1f);
                    if (fallbackFont != null)
                    {
                        mainText.font = fallbackFont;
                        textObj.GetComponent<MeshRenderer>().sharedMaterial = fallbackFont.material;
                    }
                    textObj.GetComponent<MeshRenderer>().sortingOrder = -20;
                }
            }
        }

        private void UpdateLegacyVisuals()
        {
            float t = Time.time;

            // 【超ゆったり優雅に周回する漂うキラキラエフェクト (0.015fスピード)】
            if (perimeterSparkles != null)
            {
                int total = perimeterSparkles.Length;
                for (int s = 0; s < total; s++)
                {
                    if (perimeterSparkles[s] == null) continue;
                    float progress = (t * 0.015f * sparkleSpeeds[s] + (float)s / total) % 1f;
                    Vector3 perimeterPos = GetRectPerimeterPoint(progress, 0.62f, 0.62f);

                    perimeterSparkles[s].transform.localPosition = perimeterPos + new Vector3(0f, 0f, -0.05f);

                    float scalePulse = 0.20f + Mathf.Sin(t * 0.8f + s) * 0.04f;
                    perimeterSparkles[s].transform.localScale = new Vector3(scalePulse, scalePulse, 1f);
                    perimeterSparkles[s].transform.localRotation = Quaternion.Euler(0f, 0f, t * 6f + s * 45f);
                }
            }

            if (badgeTransform != null)
            {
                Vector3 parentScale = transform.localScale;
                float invX = parentScale.x > 0.001f ? 1f / parentScale.x : 1f;
                float invY = parentScale.y > 0.001f ? 1f / parentScale.y : 1f;

                // メモ帳の紙面上部中央（0.16f）に収まるように位置調整
                float badgeY = 0.16f - (0.03f * invY);
                badgeTransform.localPosition = new Vector3(0f, badgeY, -0.2f);
                badgeTransform.localScale = new Vector3(invX, invY, 1f);

                if (charTransforms != null)
                {
                    float cycleDuration = 2.0f;
                    float bounceDuration = 0.32f;
                    float charStagger = 0.18f;

                    float cycleTime = (t % cycleDuration);

                    for (int i = 0; i < charTransforms.Length; i++)
                    {
                        if (charTransforms[i] == null) continue;

                        float charStartTime = i * charStagger;
                        float localT = cycleTime - charStartTime;
                        if (localT < 0f) localT += cycleDuration;

                        float bounceY = 0f;
                        if (localT >= 0f && localT <= bounceDuration)
                        {
                            float norm = localT / bounceDuration;
                            bounceY = Mathf.Sin(norm * Mathf.PI) * 0.075f;
                        }

                        Vector3 basePos = charBaseLocalPositions[i];
                        charTransforms[i].localPosition = new Vector3(basePos.x, basePos.y + bounceY, basePos.z);
                    }
                }
            }
        }

        private static Vector3 GetRectPerimeterPoint(float progress, float halfW, float halfH)
        {
            float perimeter = (halfW * 2f + halfH * 2f) * 2f;
            float dist = progress * perimeter;

            float w = halfW * 2f;
            float h = halfH * 2f;

            if (dist < w)
                return new Vector3(-halfW + dist, halfH, 0f);
            dist -= w;

            if (dist < h)
                return new Vector3(halfW, halfH - dist, 0f);
            dist -= h;

            if (dist < w)
                return new Vector3(halfW - dist, -halfH, 0f);
            dist -= w;

            return new Vector3(-halfW, -halfH + dist, 0f);
        }

        private static Sprite CreateRoundedRectSprite(int width, int height, int cornerRadius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            float r = cornerRadius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = 0f, dy = 0f;
                    if (x < r) dx = r - x;
                    else if (x >= width - r) dx = x - (width - r - 1);

                    if (y < r) dy = r - y;
                    else if (y >= height - r) dy = y - (height - r - 1);

                    float distSq = dx * dx + dy * dy;
                    if (distSq > r * r)
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                    else if (distSq > (r - 1.5f) * (r - 1.5f))
                    {
                        float alpha = Mathf.Clamp01((r - Mathf.Sqrt(distSq)) / 1.5f);
                        pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * width + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateFaintGridSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            int gridStep = 16;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isGrid = (x % gridStep == 0 || y % gridStep == 0);
                    pixels[y * width + x] = isGrid ? Color.white : Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Font GetYomogiFont()
        {
#if UNITY_EDITOR
            Font editorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/Yomogi-Regular.ttf");
            if (editorFont != null) return editorFont;
#endif
            Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
            foreach (Font font in allFonts)
            {
                if (font != null && font.name.IndexOf("Yomogi", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
            }
            UnityEngine.UI.Text textObj = Object.FindFirstObjectByType<UnityEngine.UI.Text>();
            if (textObj != null && textObj.font != null) return textObj.font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
