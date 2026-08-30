using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StageGrainCarryObjectFactory
    {
        private static Sprite circleSprite;

        public static GameObject Create(StageObjectData data, Transform parent, int groundLayer)
        {
            GameObject root = new GameObject(data.objectId);
            root.name = data.type.ToString();
            root.transform.SetParent(parent, false);
            root.transform.position = data.position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);

            if (data.type == StageObjectType.GrainEmitter)
            {
                CreateEmitter(root, data);
            }
            else if (data.type == StageObjectType.GrainScale)
            {
                CreateScale(root, data, groundLayer);
            }
            else
            {
                CreateGate(root, data, groundLayer);
            }
            return root;
        }

        private static void CreateEmitter(GameObject root, StageObjectData data)
        {
            Vector2 size = new Vector2(Mathf.Max(1.2f, data.size.x), Mathf.Max(1.6f, data.size.y));
            bool hasPencilArt = AddResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/grain-emitter",
                "Colored Pencil Grain Emitter",
                new Vector2(size.x / 0.707f, size.y / 0.939f),
                24);
            if (!hasPencilArt)
            {
            Color outline = new Color(0.08f, 0.19f, 0.27f, 1f);
            Vector2 bodyCenter = new Vector2(0f, size.y * 0.25f);
            Vector2 bodySize = new Vector2(size.x, size.y * 0.48f);
            AddRect(root.transform, "Pencil Grain Hopper", bodyCenter,
                bodySize, new Color(0.48f, 0.77f, 0.93f), 16);
            AddHatching(root.transform, "Hopper Pencil Fill", bodyCenter, bodySize * 0.88f,
                new Color(0.08f, 0.39f, 0.65f, 0.42f), 17, 7);
            AddSketchRect(root.transform, "Hopper Loose Outline", bodyCenter, bodySize, outline, 0.055f, 20);

            Vector2 windowSize = new Vector2(size.x * 0.62f, size.y * 0.18f);
            AddRect(root.transform, "Grain Window", bodyCenter,
                windowSize, new Color(1f, 0.88f, 0.3f), 18);
            AddSketchRect(root.transform, "Grain Window Outline", bodyCenter, windowSize,
                new Color(0.42f, 0.27f, 0.04f), 0.038f, 21);
            for (int i = -2; i <= 2; i++)
            {
                AddDot(root.transform, "Stored Grain " + i,
                    bodyCenter + new Vector2(i * windowSize.x * 0.13f, Mathf.Sin(i * 2.1f) * windowSize.y * 0.16f),
                    Mathf.Min(size.x, size.y) * 0.055f,
                    i % 2 == 0 ? new Color(1f, 0.48f, 0.08f) : new Color(0.97f, 0.68f, 0.08f), 22);
            }

            Vector2 neckCenter = new Vector2(0f, -size.y * 0.015f);
            Vector2 neckSize = new Vector2(size.x * 0.38f, size.y * 0.22f);
            AddRect(root.transform, "Dispenser Neck", neckCenter, neckSize, new Color(0.22f, 0.36f, 0.43f), 17);
            AddSketchRect(root.transform, "Dispenser Neck Outline", neckCenter, neckSize, outline, 0.045f, 21);
            Vector2 nozzleCenter = new Vector2(0f, -size.y * 0.16f);
            Vector2 nozzleSize = new Vector2(size.x * 0.25f, size.y * 0.13f);
            AddRect(root.transform, "Dark Grain Outlet", nozzleCenter, nozzleSize, new Color(0.07f, 0.1f, 0.12f), 22);
            AddSketchRect(root.transform, "Outlet Lip", nozzleCenter, nozzleSize, Color.black, 0.042f, 23);
            }

            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(size.x * 1.2f, 2.8f);
            trigger.offset = new Vector2(0f, -1.15f);

            StageGrainEmitter emitter = root.AddComponent<StageGrainEmitter>();
            emitter.Configure((DrawManager.Species)Mathf.Clamp(data.spawnPattern, 0, 4), size);
        }

        private static void CreateScale(GameObject root, StageObjectData data, int groundLayer)
        {
            Vector2 size = new Vector2(Mathf.Max(1.8f, data.size.x), Mathf.Max(0.8f, data.size.y));
            root.layer = groundLayer;
            bool hasPencilArt = AddResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/grain-scale",
                "Colored Pencil 100g Scale",
                new Vector2(size.x / 0.848f, size.y / 0.576f),
                24);
            if (!hasPencilArt)
            {
            Color outline = new Color(0.18f, 0.12f, 0.035f, 1f);
            AddRect(root.transform, "Scale Body", new Vector2(0f, -size.y * 0.2f),
                new Vector2(size.x * 0.86f, size.y * 0.62f), new Color(0.96f, 0.72f, 0.18f), 17);
            AddHatching(root.transform, "Scale Body Pencil Fill", new Vector2(0f, -size.y * 0.2f),
                new Vector2(size.x * 0.76f, size.y * 0.5f), new Color(0.64f, 0.34f, 0.03f, 0.34f), 18, 7);
            AddSketchRect(root.transform, "Scale Body Loose Outline", new Vector2(0f, -size.y * 0.2f),
                new Vector2(size.x * 0.86f, size.y * 0.62f), outline, 0.052f, 23);
            AddRect(root.transform, "Scale Plate", new Vector2(0f, size.y * 0.31f),
                new Vector2(size.x, size.y * 0.16f), new Color(0.68f, 0.79f, 0.83f), 20);
            AddSketchRect(root.transform, "Scale Plate Outline", new Vector2(0f, size.y * 0.31f),
                new Vector2(size.x, size.y * 0.16f), new Color(0.12f, 0.22f, 0.25f), 0.048f, 24);
            AddRect(root.transform, "Scale Display", new Vector2(0f, -size.y * 0.18f),
                new Vector2(size.x * 0.56f, size.y * 0.28f), new Color(0.08f, 0.13f, 0.15f), 21);
            AddSketchRect(root.transform, "Scale Display Frame", new Vector2(0f, -size.y * 0.18f),
                new Vector2(size.x * 0.6f, size.y * 0.32f), outline, 0.04f, 25);
            AddBowl(root.transform, new Vector2(size.x * 0.28f, size.y * 0.56f), size.x * 0.34f);
            AddSketchLine(root.transform, "Scale Left Foot", new[]
            {
                new Vector2(-size.x * 0.34f, -size.y * 0.47f), new Vector2(-size.x * 0.41f, -size.y * 0.55f)
            }, outline, 0.055f, 22);
            AddSketchLine(root.transform, "Scale Right Foot", new[]
            {
                new Vector2(size.x * 0.34f, -size.y * 0.47f), new Vector2(size.x * 0.41f, -size.y * 0.55f)
            }, outline, 0.055f, 22);
            }
            else
            {
                // StageGrainScale uses this transform as the visual anchor for
                // grains already deposited on the pan. It intentionally has no
                // renderer because the pan itself is part of the PNG.
                GameObject bowlAnchor = new GameObject("Bowl Cup");
                bowlAnchor.transform.SetParent(root.transform, false);
                bowlAnchor.transform.localPosition = new Vector3(size.x * 0.28f, size.y * 0.42f, 0f);
            }

            BoxCollider2D platform = root.AddComponent<BoxCollider2D>();
            platform.size = new Vector2(size.x, size.y * 0.16f);
            platform.offset = new Vector2(0f, size.y * 0.31f);
            BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(size.x * 0.92f, 2.5f);
            trigger.offset = new Vector2(0f, 1.45f);

            StageGrainScale scale = root.AddComponent<StageGrainScale>();
            scale.Configure(
                (DrawManager.Species)Mathf.Clamp(data.spawnPattern, 0, 4),
                string.IsNullOrEmpty(data.linkTargetId) ? data.objectId + "_gate" : data.linkTargetId,
                data.actionStrength > 0f ? data.actionStrength : 80f);
        }

        private static void CreateGate(GameObject root, StageObjectData data, int groundLayer)
        {
            Vector2 size = new Vector2(Mathf.Max(0.65f, data.size.x), Mathf.Max(2f, data.size.y));
            root.layer = groundLayer;
            bool hasPencilArt = AddResourceSprite(
                root.transform,
                "StageObjects/NicoDraw/grain-gate",
                "Colored Pencil Weight Gate",
                new Vector2(size.x / 0.969f, size.y / 0.827f),
                24);
            if (!hasPencilArt)
            {
            Color frame = new Color(0.1f, 0.23f, 0.3f, 1f);
            AddRect(root.transform, "Gate Paper Fill", Vector2.zero, size, new Color(0.56f, 0.81f, 0.91f, 0.88f), 15);
            AddHatching(root.transform, "Gate Blue Pencil Fill", Vector2.zero, size * 0.92f,
                new Color(0.08f, 0.38f, 0.58f, 0.3f), 16, 12);
            AddSketchRect(root.transform, "Gate Outer Frame", Vector2.zero, size, frame, 0.065f, 22);
            float postWidth = Mathf.Max(0.09f, size.x * 0.17f);
            AddRect(root.transform, "Gate Left Post", new Vector2(-size.x * 0.38f, 0f),
                new Vector2(postWidth, size.y * 0.92f), frame, 19);
            AddRect(root.transform, "Gate Right Post", new Vector2(size.x * 0.38f, 0f),
                new Vector2(postWidth, size.y * 0.92f), frame, 19);
            for (int i = -2; i <= 2; i++)
            {
                AddSketchLine(root.transform, "Gate Warning Slash " + i, new[]
                {
                    new Vector2(-size.x * 0.3f, i * size.y * 0.17f - size.y * 0.08f),
                    new Vector2(size.x * 0.3f, i * size.y * 0.17f + size.y * 0.08f)
                }, new Color(0.96f, 0.58f, 0.08f, 0.82f), 0.052f, 20);
            }
            AddRect(root.transform, "Gate Header", new Vector2(0f, size.y * 0.46f),
                new Vector2(size.x * 1.18f, size.y * 0.12f), new Color(1f, 0.68f, 0.12f), 23);
            AddSketchRect(root.transform, "Gate Header Outline", new Vector2(0f, size.y * 0.46f),
                new Vector2(size.x * 1.18f, size.y * 0.12f), new Color(0.4f, 0.2f, 0.02f), 0.048f, 25);
            }
            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = size;
            StageGrainGate gate = root.AddComponent<StageGrainGate>();
            gate.Configure(data.objectId);
        }

        private static void AddBowl(Transform parent, Vector2 position, float width)
        {
            AddRect(parent, "Bowl Rim", position, new Vector2(width, 0.09f), new Color(0.9f, 0.32f, 0.24f), 24);
            AddRect(parent, "Bowl Cup", position + Vector2.down * 0.13f,
                new Vector2(width * 0.72f, 0.22f), new Color(1f, 0.55f, 0.28f), 23);
            AddSketchLine(parent, "Bowl Pencil Outline", new[]
            {
                position + new Vector2(-width * 0.5f, 0f),
                position + new Vector2(-width * 0.34f, -0.2f),
                position + new Vector2(width * 0.34f, -0.2f),
                position + new Vector2(width * 0.5f, 0f)
            }, new Color(0.45f, 0.12f, 0.04f), 0.04f, 26);
        }

        private static bool AddResourceSprite(
            Transform parent,
            string resourcePath,
            string objectName,
            Vector2 localSize,
            int sortingOrder)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f) return false;
            GameObject visual = new GameObject(objectName);
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            visual.transform.localScale = new Vector3(
                localSize.x / sprite.bounds.size.x,
                localSize.y / sprite.bounds.size.y,
                1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            return true;
        }

        private static void AddSketchRect(
            Transform parent, string name, Vector2 center, Vector2 size, Color color, float width, int order)
        {
            Vector2 half = size * 0.5f;
            AddSketchLine(parent, name, new[]
            {
                center + new Vector2(-half.x - 0.012f, -half.y + 0.01f),
                center + new Vector2(half.x, -half.y - 0.008f),
                center + new Vector2(half.x - 0.01f, half.y),
                center + new Vector2(-half.x + 0.008f, half.y - 0.012f),
                center + new Vector2(-half.x - 0.012f, -half.y + 0.01f)
            }, color, width, order);
        }

        private static void AddHatching(
            Transform parent, string name, Vector2 center, Vector2 size, Color color, int order, int count)
        {
            float halfX = size.x * 0.5f;
            float halfY = size.y * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-halfX, halfX, t);
                float run = Mathf.Min(size.x * 0.3f, size.y * 0.5f);
                AddSketchLine(parent, name + " " + i, new[]
                {
                    center + new Vector2(Mathf.Max(-halfX, x - run), -halfY + 0.03f),
                    center + new Vector2(Mathf.Min(halfX, x + run), halfY - 0.03f)
                }, new Color(color.r, color.g, color.b, color.a * (0.72f + (i % 3) * 0.12f)), 0.018f, order);
            }
        }

        private static void AddSketchLine(
            Transform parent, string name, Vector2[] points, Color color, float width, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.startWidth = width;
            line.endWidth = width * 0.9f;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        internal static SpriteRenderer AddRect(
            Transform parent, string name, Vector2 position, Vector2 size, Color color, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(position.x, position.y, -0.03f);
            child.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        internal static SpriteRenderer AddDot(
            Transform parent, string name, Vector2 position, float size, Color color, int order)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(position.x, position.y, -0.04f);
            child.transform.localScale = Vector3.one * size;
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite GetSquareSprite()
        {
            return DoodleRuntimeAssets.SquareSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int resolution = 24;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = "Grain Carry Dot";
            Vector2 center = Vector2.one * (resolution - 1) * 0.5f;
            float radius = resolution * 0.47f;
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                texture.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= radius ? Color.white : Color.clear);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), Vector2.one * 0.5f, resolution);
            return circleSprite;
        }
    }

    public sealed class StageGrainEmitter : MonoBehaviour
    {
        private DrawManager.Species requiredSpecies;
        private Vector2 size;
        private float nextSpawnAt;
        private float nextEligibilityScanAt;
        private float lastEligiblePlayerAt = float.NegativeInfinity;

        public void Configure(DrawManager.Species species, Vector2 visualSize)
        {
            requiredSpecies = species;
            size = visualSize;
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            enabled = editor == null || !editor.IsEditing;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextEligibilityScanAt)
            {
                nextEligibilityScanAt = Time.unscaledTime + 0.08f;
                RefreshHorizontalEligibility();
            }
            if (Time.time - lastEligiblePlayerAt > 0.22f || Time.time < nextSpawnAt) return;
            // Use fewer, larger grains so their physical pile is readable and
            // grain-to-grain contacts stay inexpensive.
            nextSpawnAt = Time.time + 0.21f;
            Vector3 origin = transform.TransformPoint(new Vector3(Random.Range(-0.13f, 0.13f), -0.42f, -0.05f));
            SpriteRenderer renderer = StageGrainCarryObjectFactory.AddDot(
                null, "Physical Grain", origin, Random.Range(0.16f, 0.20f),
                Random.value > 0.35f ? new Color(1f, 0.74f, 0.08f) : new Color(0.94f, 0.43f, 0.05f), 72);
            StageGrainParticle particle = renderer.gameObject.AddComponent<StageGrainParticle>();
            particle.Configure(requiredSpecies, new Vector2(Random.Range(-0.16f, 0.16f), -0.25f));
        }

        private void RefreshHorizontalEligibility()
        {
            // Short bird/turtle drawings can remain below the trigger volume.
            // Use the visible dispenser width with a little standing room so all
            // three rooms respond consistently while still requiring the right species.
            float halfWidth = Mathf.Max(1.8f, size.x * 1.05f);
            foreach (StageGrainCarrier carrier in StageGrainCarrier.All)
            {
                if (carrier == null) continue;
                PlayerAbilityController abilities = carrier.Abilities;
                if (abilities == null || !abilities.isActiveAndEnabled
                    || abilities.CurrentProfile.Species != requiredSpecies) continue;
                // Only horizontal alignment matters. Stages may place the
                // dispenser far above the playable route, so Y must not prevent
                // grains from falling when the matching character waits below.
                if (Mathf.Abs(abilities.transform.position.x - transform.position.x) > halfWidth) continue;
                lastEligiblePlayerAt = Time.time;
                return;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            PlayerAbilityController abilities = other.GetComponentInParent<PlayerAbilityController>();
            if (abilities != null && abilities.CurrentProfile.Species == requiredSpecies)
                lastEligiblePlayerAt = Time.time;
        }
    }

    public sealed class StageGrainScale : MonoBehaviour
    {
        private DrawManager.Species requiredSpecies;
        private string gateId;
        private float gramsPerPlayer;
        private float targetGrams;
        private float measuredGrams;
        private TextMesh display;
        private bool completed;
        private float nextConsumeAt;
        private float lastControlledOccupantAt = -1f;
        private bool hasIncompleteVisit;
        private float nextParticipantRefreshAt;
        private int appliedPlayerCount = 1;
        private StageManager stageManager;
        private readonly List<SpriteRenderer> measuredDots = new List<SpriteRenderer>();

        public string ObjectId => GetComponent<StageEditorObject>()?.objectId ?? gameObject.name;

        public void Configure(DrawManager.Species species, string targetGateId, float grams)
        {
            requiredSpecies = species;
            gateId = targetGateId;
            gramsPerPlayer = Mathf.Max(1f, grams);
            targetGrams = gramsPerPlayer;
            Transform bowl = transform.Find("Bowl Cup");
            Vector2 bowlCenter = bowl != null ? (Vector2)bowl.localPosition + Vector2.up * 0.12f : new Vector2(0.45f, 0.42f);
            for (int i = 0; i < 12; i++)
            {
                float x = ((i % 6) - 2.5f) * 0.075f;
                float y = (i / 6) * 0.07f;
                measuredDots.Add(StageGrainCarryObjectFactory.AddDot(
                    transform, "Measured Grain " + i, bowlCenter + new Vector2(x, y),
                    0.09f, i % 3 == 0 ? new Color(0.94f, 0.43f, 0.05f) : new Color(1f, 0.74f, 0.08f), 26 + i));
            }
            GameObject label = new GameObject("Grain Weight Display");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, -0.16f, -0.08f);
            display = label.AddComponent<TextMesh>();
            display.anchor = TextAnchor.MiddleCenter;
            display.alignment = TextAlignment.Center;
            display.fontSize = 40;
            display.characterSize = 0.085f;
            display.color = new Color(0.35f, 1f, 0.76f);
            TextMesh reference = null;
            TextMesh[] textMeshes = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (textMeshes[i] != display && textMeshes[i].font != null)
                {
                    reference = textMeshes[i];
                    break;
                }
            }
            if (reference != null)
            {
                display.font = reference.font;
                display.GetComponent<MeshRenderer>().sharedMaterial = reference.font.material;
            }
            display.GetComponent<MeshRenderer>().sortingOrder = 30;
            RefreshDisplay();
        }

        private void Start()
        {
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            enabled = editor == null || !editor.IsEditing;
            stageManager = Object.FindFirstObjectByType<StageManager>();
            RefreshParticipantTarget(true);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (completed) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            PlayerAbilityController abilities = player != null ? player.GetComponent<PlayerAbilityController>() : null;
            StageGrainCarrier carrier = player != null ? player.GetComponent<StageGrainCarrier>() : null;
            if (player == null || abilities == null || carrier == null
                || abilities.CurrentProfile.Species != requiredSpecies || !player.IsGrounded) return;

            StageGrainCarryController controller = GetComponentInParent<StageGrainCarryController>();
            if (controller != null && !controller.CanControlCarrier(player)) return;
            lastControlledOccupantAt = Time.time;
            if (measuredGrams > 0f) hasIncompleteVisit = true;
            if (Time.time < nextConsumeAt) return;
            if (!carrier.TryTakeContainedParticle(out StageGrainParticle particle)) return;
            nextConsumeAt = Time.time + 0.065f;
            float amount = Mathf.Min(particle.Grams, targetGrams - measuredGrams);
            particle.Consume();
            hasIncompleteVisit = true;
            if (controller != null) controller.Deposit(this, amount);
            else ApplyMeasuredGrams(measuredGrams + amount);
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextParticipantRefreshAt)
            {
                nextParticipantRefreshAt = Time.unscaledTime + 0.5f;
                RefreshParticipantTarget(false);
            }
            // The scale is a single weighing attempt, not a cumulative bank.
            // A short grace period avoids compound player colliders briefly
            // dropping OnTriggerStay while the character is still on the pan.
            if (completed || !hasIncompleteVisit || measuredGrams <= 0f
                || Time.time - lastControlledOccupantAt <= 0.28f) return;
            hasIncompleteVisit = false;
            StageGrainCarryController controller = GetComponentInParent<StageGrainCarryController>();
            if (controller != null) controller.ResetScale(this);
            else ApplyMeasuredGrams(0f);
        }

        private void RefreshParticipantTarget(bool force)
        {
            if (completed) return;
            if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
            int playerCount = stageManager != null ? stageManager.GetInkBudgetPlayerCount() : 1;
            playerCount = Mathf.Max(1, playerCount);
            if (!force && playerCount == appliedPlayerCount) return;
            appliedPlayerCount = playerCount;
            targetGrams = ObjectId == "7-2_scale_bird"
                ? 30f + 15f * (Mathf.Clamp(appliedPlayerCount, 1, 4) - 1)
                : gramsPerPlayer * appliedPlayerCount;
            ApplyMeasuredGrams(measuredGrams);
        }

        public void ApplyMeasuredGrams(float grams)
        {
            measuredGrams = Mathf.Clamp(grams, 0f, targetGrams);
            RefreshDisplay();
            if (!completed && measuredGrams >= targetGrams - 0.01f)
            {
                completed = true;
                StageGrainGate gate = FindGate();
                gate?.Open();
                GameSfx.PlayAt(SfxId.DrawConfirm, transform.position);
            }
        }

        public float MeasuredGrams => measuredGrams;
        public bool Completed => completed;

        private StageGrainGate FindGate()
        {
            StageGrainCarryController controller = GetComponentInParent<StageGrainCarryController>();
            StageGrainGate[] gates = controller != null
                ? controller.GetComponentsInChildren<StageGrainGate>(true)
                : Object.FindObjectsByType<StageGrainGate>(FindObjectsSortMode.None);
            for (int i = 0; i < gates.Length; i++) if (gates[i].ObjectId == gateId) return gates[i];
            return null;
        }

        private void RefreshDisplay()
        {
            if (display != null) display.text = Mathf.FloorToInt(measuredGrams) + " / " + Mathf.RoundToInt(targetGrams) + " g";
            int visible = Mathf.CeilToInt(measuredDots.Count * measuredGrams / Mathf.Max(1f, targetGrams));
            for (int i = 0; i < measuredDots.Count; i++) measuredDots[i].enabled = i < visible;
        }
    }

    public sealed class StageGrainGate : MonoBehaviour
    {
        private string objectId;
        private bool opened;
        private float openAmount;
        private float openTravel;
        private BoxCollider2D gateCollider;
        public string ObjectId => objectId;

        public void Configure(string id) { objectId = id; }

        private void Awake()
        {
            gateCollider = GetComponent<BoxCollider2D>();
            openTravel = gateCollider != null ? gateCollider.bounds.size.y + 0.8f : 6f;
        }

        public void Open()
        {
            opened = true;
            if (gateCollider != null) gateCollider.enabled = false;
        }

        private void Update()
        {
            if (!opened || openAmount >= 1f) return;
            float previous = openAmount;
            openAmount = Mathf.MoveTowards(openAmount, 1f, Time.deltaTime * 1.7f);
            transform.position += Vector3.up * ((openAmount - previous) * openTravel);
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = renderers[i].color;
                color.a = Mathf.Lerp(color.a, 0.08f, openAmount);
                renderers[i].color = color;
            }
        }
    }

    public sealed class StageGrainParticle : MonoBehaviour
    {
        private static readonly HashSet<StageGrainParticle> ActiveParticles = new HashSet<StageGrainParticle>();
        private static PhysicsMaterial2D grainMaterial;
        private static bool grainCollisionConfigured;
        private Rigidbody2D body;
        private float bornAt;
        private float lastInsideHeadAt;
        private bool consumed;
        private DrawManager.Species sourceSpecies;
        private float floorContactStartedAt = -1f;
        private float lastFloorContactAt = -1f;
        private float nextContainmentCheck;
        private bool insideCarrier;
        private StageGrainCarrier containingCarrier;
        private bool lockedToCarrier;
        private CircleCollider2D grainCollider;
        private Vector3 carrierLocalPosition;
        private float configuredGravityScale = 0.72f;
        private float grams = 10f;

        public float Grams => grams;
        public bool IsInsideCarrier => insideCarrier;
        public bool IsOnGround => floorContactStartedAt >= 0f
            && Time.time - lastFloorContactAt <= 0.15f;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public static IEnumerable<StageGrainParticle> All => ActiveParticles;

        public void Configure(DrawManager.Species species, Vector2 initialVelocity)
        {
            Configure(species, initialVelocity, 10f, 0.72f, false);
        }

        public void Configure(
            DrawManager.Species species,
            Vector2 initialVelocity,
            float weightGrams,
            float gravityScale,
            bool ignoreOneWayPlatforms)
        {
            sourceSpecies = species;
            grams = Mathf.Max(0.1f, weightGrams);
            gameObject.layer = 31;
            if (!grainCollisionConfigured)
            {
                grainCollisionConfigured = true;
                // Grain-to-grain collision is essential: the visible pile and
                // spill-over define how much a hand-drawn head can carry.
                Physics2D.IgnoreLayerCollision(31, 31, false);
            }
            bornAt = Time.time;
            lastInsideHeadAt = bornAt;
            nextContainmentCheck = Time.unscaledTime + Random.Range(0f, 0.08f);
            body = gameObject.AddComponent<Rigidbody2D>();
            // Ten grains make 100 g. Their mass stays deliberately light so the
            // pile cannot shove the player around or suppress a jump.
            body.mass = Mathf.Clamp(0.00045f + grams * 0.000035f, 0.0005f, 0.0022f);
            configuredGravityScale = Mathf.Max(0.1f, gravityScale);
            body.gravityScale = configuredGravityScale;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = initialVelocity;
            grainCollider = gameObject.AddComponent<CircleCollider2D>();
            grainCollider.radius = 0.46f;
            grainCollider.sharedMaterial = GetGrainMaterial();
            if (ignoreOneWayPlatforms)
            {
                PlatformEffector2D[] effectors = Object.FindObjectsByType<PlatformEffector2D>(FindObjectsSortMode.None);
                for (int i = 0; i < effectors.Length; i++)
                {
                    Collider2D platform = effectors[i] != null ? effectors[i].GetComponent<Collider2D>() : null;
                    if (platform != null) Physics2D.IgnoreCollision(grainCollider, platform, true);
                }
                Collider2D[] stageColliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
                for (int i = 0; i < stageColliders.Length; i++)
                {
                    Collider2D stageCollider = stageColliders[i];
                    if (stageCollider != null && stageCollider.gameObject.name == "Boundary Ceiling")
                        Physics2D.IgnoreCollision(grainCollider, stageCollider, true);
                }
            }
        }

        private void OnEnable() { ActiveParticles.Add(this); }
        private void OnDisable()
        {
            ActiveParticles.Remove(this);
            if (lockedToCarrier && containingCarrier != null)
                containingCarrier.SetParticleCollisionIgnored(grainCollider, false);
        }

        private void Update()
        {
            if (consumed) return;
            if (insideCarrier && containingCarrier != null) StabilizeOnCarrier(containingCarrier);
            if (Time.unscaledTime >= nextContainmentCheck)
            {
                nextContainmentCheck = Time.unscaledTime + 0.08f;
                StageGrainCarrier previousCarrier = containingCarrier;
                StageGrainCarrier foundCarrier = null;
                foreach (StageGrainCarrier carrier in StageGrainCarrier.All)
                {
                    if (!IsOnGround && carrier != null && carrier.ContainsWorldPoint(transform.position))
                    {
                        foundCarrier = carrier;
                        break;
                    }
                }
                insideCarrier = foundCarrier != null;
                if (foundCarrier != null)
                {
                    if (lockedToCarrier && previousCarrier != null && previousCarrier != foundCarrier)
                        ReleaseCarrierLock(previousCarrier);
                    containingCarrier = foundCarrier;
                    lastInsideHeadAt = Time.time;
                    StabilizeOnCarrier(foundCarrier);
                }
                else if (lockedToCarrier)
                {
                    ReleaseCarrierLock(previousCarrier);
                    containingCarrier = null;
                }
            }

            if (transform.position.y < -15f
                || (!insideCarrier
                    && floorContactStartedAt >= 0f
                    && Time.time - lastFloorContactAt <= 0.12f
                    && Time.time - floorContactStartedAt >= 3f))
            {
                Destroy(gameObject);
            }

            else if (Time.time - lastFloorContactAt > 0.12f)
            {
                floorContactStartedAt = -1f;
            }
        }

        private void StabilizeOnCarrier(StageGrainCarrier carrier)
        {
            if (body == null || carrier == null) return;
            if (!lockedToCarrier)
            {
                lockedToCarrier = true;
                carrierLocalPosition = carrier.transform.InverseTransformPoint(body.position);
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                carrier.SetParticleCollisionIgnored(grainCollider, true);
            }

            Vector2 anchoredPosition = carrier.transform.TransformPoint(carrierLocalPosition);
            body.position = anchoredPosition;
            // Thin hand-drawn line colliders can move a long way in one physics
            // step when their player climbs a ledge. Limit only the grain's
            // velocity relative to that carrier so a settled pile cannot tunnel
            // through the drawing and be launched away by the step impulse.
            Vector2 carrierVelocity = carrier.Velocity;
            body.linearVelocity = carrierVelocity;
            body.angularVelocity = 0f;
        }

        private void ReleaseCarrierLock(StageGrainCarrier previousCarrier)
        {
            if (body == null || !lockedToCarrier) return;
            lockedToCarrier = false;
            previousCarrier?.SetParticleCollisionIgnored(grainCollider, false);
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = configuredGravityScale;
            body.linearVelocity = previousCarrier != null ? previousCarrier.Velocity : Vector2.zero;
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // Grains resting on a character form a pile and naturally touch one
            // another. Only contact with actual terrain starts the cleanup timer.
            if (collision.collider.GetComponentInParent<PlayerController2D>() != null
                || collision.collider.GetComponentInParent<StageGrainParticle>() != null) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y <= 0.45f) continue;
                if (floorContactStartedAt < 0f) floorContactStartedAt = Time.time;
                lastFloorContactAt = Time.time;
                return;
            }
        }

        public void Consume()
        {
            if (consumed) return;
            consumed = true;
            ActiveParticles.Remove(this);
            Destroy(gameObject);
        }

        public void MirrorAcross(float worldPivotX)
        {
            if (consumed) return;
            Vector2 position = body != null ? body.position : (Vector2)transform.position;
            position.x = worldPivotX * 2f - position.x;
            if (body != null)
            {
                body.position = position;
                Vector2 velocity = body.linearVelocity;
                velocity.x = -velocity.x;
                body.linearVelocity = velocity;
                if (lockedToCarrier && containingCarrier != null)
                    carrierLocalPosition = containingCarrier.transform.InverseTransformPoint(position);
            }
            else
            {
                transform.position = new Vector3(position.x, position.y, transform.position.z);
            }
        }

        private static PhysicsMaterial2D GetGrainMaterial()
        {
            if (grainMaterial == null)
            {
                grainMaterial = new PhysicsMaterial2D("Physical Grain")
                {
                    friction = 0.62f,
                    bounciness = 0.04f,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return grainMaterial;
        }
    }

    public sealed class StageGrainCarrier : MonoBehaviour
    {
        private static readonly HashSet<StageGrainCarrier> ActiveCarriers = new HashSet<StageGrainCarrier>();
        private readonly List<Collider2D> bodyColliders = new List<Collider2D>();
        private float nextColliderRefresh;
        private float nextBoundsRefresh;
        private Bounds cachedCarrierBounds;
        private bool hasCachedCarrierBounds;
        private Rigidbody2D playerBody;
        private PlayerAbilityController abilities;

        public static IEnumerable<StageGrainCarrier> All => ActiveCarriers;
        public PlayerAbilityController Abilities => abilities;
        public Vector2 Velocity => playerBody != null ? playerBody.linearVelocity : Vector2.zero;

        private void OnEnable() { ActiveCarriers.Add(this); }
        private void OnDisable() { ActiveCarriers.Remove(this); }

        private void Awake()
        {
            playerBody = GetComponent<Rigidbody2D>();
            abilities = GetComponent<PlayerAbilityController>();
        }

        private void LateUpdate()
        {
            RefreshCarrierBounds();
        }

        public bool ContainsWorldPoint(Vector2 point)
        {
            if (!TryGetCarrierBounds(out Bounds bounds)) return false;
            // Only the upper/head region carries grains. Using the complete body
            // bounds made falling grains stick to legs, tails and the empty space
            // inside a large drawing, visually wrapping the whole character.
            const float sidePadding = 0.08f;
            float headRegionBottom = bounds.min.y + bounds.size.y * 0.62f;
            return point.x >= bounds.min.x - sidePadding
                && point.x <= bounds.max.x + sidePadding
                && point.y >= headRegionBottom
                && point.y <= bounds.max.y + 0.08f;
        }

        public void SetParticleCollisionIgnored(Collider2D particleCollider, bool ignored)
        {
            if (particleCollider == null) return;
            RefreshCarrierBounds();
            for (int i = 0; i < bodyColliders.Count; i++)
            {
                Collider2D bodyCollider = bodyColliders[i];
                if (bodyCollider != null && bodyCollider.enabled)
                    Physics2D.IgnoreCollision(particleCollider, bodyCollider, ignored);
            }
        }

        public bool TryTakeContainedParticle(out StageGrainParticle result)
        {
            result = null;
            float bestY = float.PositiveInfinity;
            foreach (StageGrainParticle particle in StageGrainParticle.All)
            {
                if (particle == null || particle.IsOnGround
                    || !ContainsWorldPoint(particle.transform.position)) continue;
                Vector2 relativeVelocity = particle.Velocity - (playerBody != null ? playerBody.linearVelocity : Vector2.zero);
                if (relativeVelocity.sqrMagnitude > 4f) continue;
                if (particle.transform.position.y < bestY)
                {
                    bestY = particle.transform.position.y;
                    result = particle;
                }
            }
            return result != null;
        }

        public void MirrorContainedParticles(float worldPivotX)
        {
            List<StageGrainParticle> contained = new List<StageGrainParticle>();
            foreach (StageGrainParticle particle in StageGrainParticle.All)
            {
                if (particle != null && !particle.IsOnGround && ContainsWorldPoint(particle.transform.position))
                    contained.Add(particle);
            }
            for (int i = 0; i < contained.Count; i++)
                contained[i].MirrorAcross(worldPivotX);
            // The cached carrier bounds describe the pre-flip drawing.
            hasCachedCarrierBounds = false;
        }

        public void SetGrams(float value)
        {
            if (value > 0f) return;
            List<StageGrainParticle> remove = new List<StageGrainParticle>();
            foreach (StageGrainParticle particle in StageGrainParticle.All)
                if (particle != null && ContainsWorldPoint(particle.transform.position)) remove.Add(particle);
            for (int i = 0; i < remove.Count; i++) remove[i].Consume();
        }

        private bool TryGetCarrierBounds(out Bounds bounds)
        {
            if (!hasCachedCarrierBounds || Time.unscaledTime >= nextBoundsRefresh)
                RefreshCarrierBounds();
            bounds = cachedCarrierBounds;
            return hasCachedCarrierBounds;
        }

        private void RefreshCarrierBounds()
        {
            nextBoundsRefresh = Time.unscaledTime + 0.025f;
            if (Time.unscaledTime >= nextColliderRefresh
                || bodyColliders.Count == 0)
            {
                nextColliderRefresh = Time.unscaledTime + 0.35f;
                bodyColliders.Clear();
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D collider = colliders[i];
                    if (collider == null || collider.isTrigger) continue;
                    if (collider.gameObject.name.EndsWith("Segment", System.StringComparison.Ordinal))
                    {
                        bodyColliders.Add(collider);
                    }
                }
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = bodyColliders.Count - 1; i >= 0; i--)
            {
                Collider2D collider = bodyColliders[i];
                if (collider == null || !collider.enabled)
                {
                    if (collider == null) bodyColliders.RemoveAt(i);
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }
            cachedCarrierBounds = bounds;
            hasCachedCarrierBounds = hasBounds;
        }
    }

    public sealed class StageGrainCarryController : MonoBehaviour
    {
        private const string DepositKind = "grain_carry_deposit";
        private const string ResetKind = "grain_carry_reset";
        private const string ScaleStateKind = "grain_carry_scale_state";
        private float nextPlayerScan;
        private readonly Dictionary<string, StageGrainScale> scales = new Dictionary<string, StageGrainScale>();
        private readonly Dictionary<string, float> pendingDeposits = new Dictionary<string, float>();
        private readonly Dictionary<string, float> nextScaleBroadcastAt = new Dictionary<string, float>();
        private float nextDepositSendAt;
        private OnlineManager onlineManager;
        private StageManager stageManager;
        private StageGimmickSyncManager syncManager;

        [System.Serializable]
        private sealed class GrainDepositMessage
        {
            public string ScaleId;
            public float Amount;
        }

        [System.Serializable]
        private sealed class GrainScaleState
        {
            public string ScaleId;
            public float Grams;
        }

        [System.Serializable]
        private sealed class GrainScaleReset
        {
            public string ScaleId;
        }

        private void Start()
        {
            onlineManager = Object.FindFirstObjectByType<OnlineManager>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            syncManager = GetComponent<StageGimmickSyncManager>();
            if (onlineManager != null) onlineManager.GimmickDataReceived += HandleNetworkData;
            StageGrainScale[] found = GetComponentsInChildren<StageGrainScale>(true);
            for (int i = 0; i < found.Length; i++) scales[found[i].ObjectId] = found[i];
            EnsurePlayerCarriers(true);
        }

        private void OnDestroy()
        {
            if (onlineManager != null) onlineManager.GimmickDataReceived -= HandleNetworkData;
            StageGrainCarrier[] carriers = Object.FindObjectsByType<StageGrainCarrier>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                if (carriers[i] == null) continue;
                carriers[i].SetGrams(0f);
                Destroy(carriers[i]);
            }
        }

        private void Update()
        {
            FlushPendingDeposits();
            if (Time.unscaledTime < nextPlayerScan) return;
            nextPlayerScan = Time.unscaledTime + 1f;
            EnsurePlayerCarriers(false);
        }

        private void EnsurePlayerCarriers(bool reset)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                StageGrainCarrier carrier = GetOrAddCarrier(players[i]);
                if (reset) carrier?.SetGrams(0f);
            }
        }

        public static StageGrainCarrier GetOrAddCarrier(PlayerController2D player)
        {
            if (player == null) return null;
            StageGrainCarrier carrier = player.GetComponent<StageGrainCarrier>();
            return carrier != null ? carrier : player.gameObject.AddComponent<StageGrainCarrier>();
        }

        public void Deposit(StageGrainScale scale, float amount)
        {
            if (scale == null || amount <= 0f) return;
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                pendingDeposits.TryGetValue(scale.ObjectId, out float pending);
                pendingDeposits[scale.ObjectId] = pending + amount;
                return;
            }
            scale.ApplyMeasuredGrams(scale.MeasuredGrams + amount);
            BroadcastScaleState(scale);
        }

        public void ResetScale(StageGrainScale scale)
        {
            if (scale == null || scale.Completed) return;
            pendingDeposits.Remove(scale.ObjectId);
            scale.ApplyMeasuredGrams(0f);
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                onlineManager?.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = "7-2",
                    Kind = ResetKind,
                    Json = JsonUtility.ToJson(new GrainScaleReset { ScaleId = scale.ObjectId })
                });
                return;
            }
            BroadcastScaleState(scale, true);
        }

        public bool CanControlCarrier(PlayerController2D player)
        {
            if (syncManager == null || !syncManager.IsOnlineActive) return true;
            return stageManager != null
                && onlineManager != null
                && stageManager.GetOnlinePlayerId(player) == onlineManager.LocalPlayerId;
        }

        private void HandleNetworkData(OnlineGimmickData data)
        {
            if (data == null || data.ObjectId != "7-2") return;
            if (data.Kind == DepositKind && syncManager != null && syncManager.IsHost)
            {
                GrainDepositMessage deposit = JsonUtility.FromJson<GrainDepositMessage>(data.Json);
                if (deposit != null && scales.TryGetValue(deposit.ScaleId, out StageGrainScale scale))
                {
                    PlayerController2D sourcePlayer = stageManager != null
                        ? stageManager.GetOnlinePlayerController(data.PlayerId)
                        : null;
                    StageGrainCarrier sourceCarrier = sourcePlayer != null
                        ? sourcePlayer.GetComponent<StageGrainCarrier>()
                        : null;
                    if (sourceCarrier != null
                        && sourceCarrier.TryTakeContainedParticle(out StageGrainParticle hostParticle))
                    {
                        hostParticle.Consume();
                    }
                    scale.ApplyMeasuredGrams(scale.MeasuredGrams + Mathf.Clamp(deposit.Amount, 0f, 12f));
                    BroadcastScaleState(scale);
                }
            }
            else if (data.Kind == ResetKind && syncManager != null && syncManager.IsHost)
            {
                GrainScaleReset reset = JsonUtility.FromJson<GrainScaleReset>(data.Json);
                if (reset != null && scales.TryGetValue(reset.ScaleId, out StageGrainScale scale)
                    && !scale.Completed)
                {
                    scale.ApplyMeasuredGrams(0f);
                    BroadcastScaleState(scale, true);
                }
            }
            else if (data.Kind == ScaleStateKind && syncManager != null && !syncManager.IsHost)
            {
                GrainScaleState state = JsonUtility.FromJson<GrainScaleState>(data.Json);
                if (state != null && scales.TryGetValue(state.ScaleId, out StageGrainScale scale))
                    scale.ApplyMeasuredGrams(state.Grams);
            }
        }

        private void BroadcastScaleState(StageGrainScale scale, bool force = false)
        {
            if (scale == null || syncManager == null || !syncManager.IsOnlineActive || !syncManager.IsHost) return;
            nextScaleBroadcastAt.TryGetValue(scale.ObjectId, out float nextSend);
            if (!force && !scale.Completed && Time.unscaledTime < nextSend) return;
            nextScaleBroadcastAt[scale.ObjectId] = Time.unscaledTime + 0.15f;
            onlineManager?.SendGimmickData(new OnlineGimmickData
            {
                ObjectId = "7-2",
                Kind = ScaleStateKind,
                Json = JsonUtility.ToJson(new GrainScaleState { ScaleId = scale.ObjectId, Grams = scale.MeasuredGrams })
            });
        }

        private void FlushPendingDeposits()
        {
            if (pendingDeposits.Count == 0 || Time.unscaledTime < nextDepositSendAt) return;
            nextDepositSendAt = Time.unscaledTime + 0.15f;
            foreach (KeyValuePair<string, float> pair in pendingDeposits)
            {
                if (pair.Value <= 0f) continue;
                onlineManager?.SendGimmickData(new OnlineGimmickData
                {
                    ObjectId = "7-2",
                    Kind = DepositKind,
                    Json = JsonUtility.ToJson(new GrainDepositMessage
                    {
                        ScaleId = pair.Key,
                        Amount = pair.Value
                    })
                });
            }
            pendingDeposits.Clear();
        }
    }
}
