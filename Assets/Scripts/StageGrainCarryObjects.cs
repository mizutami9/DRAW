using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StageGrainCarryObjectFactory
    {
        private static Sprite squareSprite;
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
            AddRect(root.transform, "Dispenser Body", new Vector2(0f, size.y * 0.25f),
                new Vector2(size.x, size.y * 0.48f), new Color(0.34f, 0.68f, 0.9f), 16);
            AddRect(root.transform, "Dispenser Label", new Vector2(0f, size.y * 0.25f),
                new Vector2(size.x * 0.62f, size.y * 0.18f), new Color(1f, 0.91f, 0.38f), 18);
            AddRect(root.transform, "Dispenser Nozzle", new Vector2(0f, -size.y * 0.08f),
                new Vector2(size.x * 0.28f, size.y * 0.25f), new Color(0.18f, 0.3f, 0.38f), 17);

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
            AddRect(root.transform, "Scale Body", new Vector2(0f, -size.y * 0.2f),
                new Vector2(size.x * 0.86f, size.y * 0.62f), new Color(0.96f, 0.76f, 0.24f), 17);
            AddRect(root.transform, "Scale Plate", new Vector2(0f, size.y * 0.31f),
                new Vector2(size.x, size.y * 0.16f), new Color(0.74f, 0.83f, 0.86f), 20);
            AddRect(root.transform, "Scale Display", new Vector2(0f, -size.y * 0.18f),
                new Vector2(size.x * 0.56f, size.y * 0.28f), new Color(0.08f, 0.13f, 0.15f), 21);
            AddBowl(root.transform, new Vector2(size.x * 0.28f, size.y * 0.56f), size.x * 0.34f);

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
            AddRect(root.transform, "Gate Fill", Vector2.zero, size, new Color(0.28f, 0.72f, 0.92f, 0.82f), 15);
            for (int i = -2; i <= 2; i++)
            {
                AddRect(root.transform, "Gate Bar " + i, new Vector2(i * size.x * 0.17f, 0f),
                    new Vector2(size.x * 0.055f, size.y * 0.94f), new Color(0.08f, 0.24f, 0.36f), 18);
            }
            AddRect(root.transform, "Gate Header", new Vector2(0f, size.y * 0.46f),
                new Vector2(size.x * 1.18f, size.y * 0.12f), new Color(1f, 0.72f, 0.18f), 20);
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
            if (squareSprite != null) return squareSprite;
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "Grain Carry Square";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            return squareSprite;
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
            float halfWidth = Mathf.Max(0.8f, size.x * 0.6f);
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
            targetGrams = gramsPerPlayer * appliedPlayerCount;
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

        public float Grams => 10f;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public static IEnumerable<StageGrainParticle> All => ActiveParticles;

        public void Configure(DrawManager.Species species, Vector2 initialVelocity)
        {
            sourceSpecies = species;
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
            body.mass = 0.0008f;
            body.gravityScale = 0.72f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = initialVelocity;
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.46f;
            collider.sharedMaterial = GetGrainMaterial();
        }

        private void OnEnable() { ActiveParticles.Add(this); }
        private void OnDisable() { ActiveParticles.Remove(this); }

        private void Update()
        {
            if (consumed) return;
            if (Time.unscaledTime >= nextContainmentCheck)
            {
                nextContainmentCheck = Time.unscaledTime + 0.08f;
                insideCarrier = false;
                foreach (StageGrainCarrier carrier in StageGrainCarrier.All)
                {
                    if (carrier != null && carrier.ContainsWorldPoint(transform.position))
                    {
                        insideCarrier = true;
                        lastInsideHeadAt = Time.time;
                        break;
                    }
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

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision.collider.GetComponentInParent<PlayerController2D>() != null) return;
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
        private readonly List<Collider2D> headColliders = new List<Collider2D>();
        private float nextColliderRefresh;
        private float nextBoundsRefresh;
        private Bounds cachedHeadBounds;
        private bool hasCachedHeadBounds;
        private Rigidbody2D playerBody;
        private PlayerAbilityController abilities;

        public static IEnumerable<StageGrainCarrier> All => ActiveCarriers;
        public PlayerAbilityController Abilities => abilities;

        private void OnEnable() { ActiveCarriers.Add(this); }
        private void OnDisable() { ActiveCarriers.Remove(this); }

        private void Awake()
        {
            playerBody = GetComponent<Rigidbody2D>();
            abilities = GetComponent<PlayerAbilityController>();
        }

        private void LateUpdate()
        {
            RefreshHeadBounds();
        }

        public bool ContainsWorldPoint(Vector2 point)
        {
            if (!TryGetHeadBounds(out Bounds bounds)) return false;
            // A particle resting on top of a closed head sits above max.y and is
            // not counted. An open U/bowl drawing lets it fall below this line.
            const float sidePadding = 0.08f;
            return point.x >= bounds.min.x - sidePadding
                && point.x <= bounds.max.x + sidePadding
                && point.y >= bounds.min.y - 0.08f
                && point.y <= bounds.max.y + 0.025f;
        }

        public bool TryTakeContainedParticle(out StageGrainParticle result)
        {
            result = null;
            float bestY = float.PositiveInfinity;
            foreach (StageGrainParticle particle in StageGrainParticle.All)
            {
                if (particle == null || !ContainsWorldPoint(particle.transform.position)) continue;
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
                if (particle != null && ContainsWorldPoint(particle.transform.position))
                    contained.Add(particle);
            }
            for (int i = 0; i < contained.Count; i++)
                contained[i].MirrorAcross(worldPivotX);
            // The cached head bounds describe the pre-flip drawing.
            hasCachedHeadBounds = false;
        }

        public void SetGrams(float value)
        {
            if (value > 0f) return;
            List<StageGrainParticle> remove = new List<StageGrainParticle>();
            foreach (StageGrainParticle particle in StageGrainParticle.All)
                if (particle != null && ContainsWorldPoint(particle.transform.position)) remove.Add(particle);
            for (int i = 0; i < remove.Count; i++) remove[i].Consume();
        }

        private bool TryGetHeadBounds(out Bounds bounds)
        {
            if (!hasCachedHeadBounds || Time.unscaledTime >= nextBoundsRefresh)
                RefreshHeadBounds();
            bounds = cachedHeadBounds;
            return hasCachedHeadBounds;
        }

        private void RefreshHeadBounds()
        {
            nextBoundsRefresh = Time.unscaledTime + 0.025f;
            if (Time.unscaledTime >= nextColliderRefresh || headColliders.Count == 0)
            {
                nextColliderRefresh = Time.unscaledTime + 0.35f;
                headColliders.Clear();
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Collider2D collider = colliders[i];
                    if (collider != null && !collider.isTrigger
                        && collider.gameObject.name.StartsWith("HeadSegment", System.StringComparison.Ordinal))
                    {
                        headColliders.Add(collider);
                    }
                }
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = headColliders.Count - 1; i >= 0; i--)
            {
                Collider2D collider = headColliders[i];
                if (collider == null || !collider.enabled)
                {
                    if (collider == null) headColliders.RemoveAt(i);
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }
            cachedHeadBounds = bounds;
            hasCachedHeadBounds = hasBounds;
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
