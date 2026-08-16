using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageEnemyCharacter : MonoBehaviour
    {
        private StageObjectType enemyType;
        private Vector2 enemySize;
        private float direction = 1f;
        private float baseSpeed;
        private float nextAbilityAt;
        private float chargeUntil;
        private float reverseLockUntil;
        private float flightPhase;
        private Vector2 flightOrigin;
        private float flightStartedAt;
        private bool defeated;
        private bool spawnedByDevice;
        private Rigidbody2D body;
        private Collider2D enemyCollider;
        private Transform visualRoot;
        private StageManager stageManager;
        private StageGimmickSyncManager syncManager;
        private static Sprite circleSprite;
        private static Sprite squareSprite;
        private static Material lineMaterial;

        public string ObjectId
        {
            get
            {
                StageEditorObject marker = GetComponent<StageEditorObject>();
                return marker != null ? marker.objectId : gameObject.name;
            }
        }

        public bool IsDefeated => defeated;

        public void SetSpawnedByDevice()
        {
            spawnedByDevice = true;
        }

        public void Configure(StageObjectType type, Vector2 size, float speedOverride, float initialFacing)
        {
            enemyType = type;
            enemySize = size;
            baseSpeed = speedOverride > 0f ? Mathf.Clamp(speedOverride, 0.5f, 8f) : DefaultSpeed(type);
            direction = Mathf.Abs(initialFacing) < 0.15f ? 1f : Mathf.Sign(initialFacing);
            BuildVisual();
        }

        private void Start()
        {
            body = GetComponent<Rigidbody2D>();
            enemyCollider = GetComponent<Collider2D>();
            stageManager = Object.FindFirstObjectByType<StageManager>();
            syncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            flightPhase = Mathf.Abs(ObjectId.GetHashCode() % 1000) * 0.01f;
            flightOrigin = transform.position;
            flightStartedAt = Time.time;
            nextAbilityAt = Time.time + InitialAbilityDelay();

            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.linearVelocity = Vector2.zero;
                }
                enabled = false;
                return;
            }

            if (!spawnedByDevice && syncManager != null && syncManager.ShouldAskHost && body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
            }
        }

        private void Update()
        {
            if (defeated) return;
            if (body != null && Mathf.Abs(body.linearVelocity.x) > 0.15f)
                direction = Mathf.Sign(body.linearVelocity.x);
            SetFacingVisual();
            if (enemyType == StageObjectType.EnemyShooter && Time.time >= nextAbilityAt)
            {
                nextAbilityAt = Time.time + 2.8f;
                FireInkShot();
            }
        }

        private void FixedUpdate()
        {
            if (defeated || body == null || syncManager != null && syncManager.ShouldAskHost) return;

            if (enemyType == StageObjectType.EnemyFlyer
                || enemyType == StageObjectType.EnemyFlyerZigzag
                || enemyType == StageObjectType.EnemyFlyerOrbit)
            {
                MoveFlyer();
                return;
            }

            bool grounded = IsGrounded();
            if (IsWallAhead() || grounded && !HasGroundAhead()) Reverse();

            float speed = baseSpeed;
            if (enemyType == StageObjectType.EnemyCharger)
            {
                UpdateCharge();
                if (Time.time < chargeUntil) speed *= 3.25f;
            }
            else if (enemyType == StageObjectType.EnemyJumper && grounded && Time.time >= nextAbilityAt)
            {
                nextAbilityAt = Time.time + 1.35f;
                body.linearVelocity = new Vector2(direction * baseSpeed * 1.35f, 8.8f);
                GameSfx.PlayAt(SfxId.EnemyJump, transform.position);
                return;
            }

            float horizontal = enemyType == StageObjectType.EnemyShooter ? direction * 0.35f : direction * speed;
            body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);
        }

        private void MoveFlyer()
        {
            if (enemyType == StageObjectType.EnemyFlyerOrbit)
            {
                float time = (Time.time - flightStartedAt) * Mathf.Max(0.5f, baseSpeed * 0.48f);
                Vector2 target = flightOrigin + new Vector2(Mathf.Sin(time) * 2.6f, Mathf.Sin(time * 2f) * 1.55f);
                body.linearVelocity = (target - body.position) / Mathf.Max(Time.fixedDeltaTime, 0.001f);
                return;
            }
            if (IsWallAhead()) Reverse();
            float vertical = enemyType == StageObjectType.EnemyFlyerZigzag
                ? (Mathf.Repeat(Time.time * 1.3f + flightPhase, 2f) < 1f ? 2.7f : -2.7f)
                : Mathf.Sin(Time.time * 2.25f + flightPhase) * 1.35f;
            body.linearVelocity = new Vector2(direction * baseSpeed, vertical);
        }

        private void UpdateCharge()
        {
            if (Time.time < chargeUntil || Time.time < nextAbilityAt) return;
            PlayerController2D target = FindNearestPlayer(7.5f, true);
            if (target == null) return;
            float delta = target.transform.position.x - transform.position.x;
            if (Mathf.Abs(delta) < 0.5f || Mathf.Sign(delta) != direction) return;
            chargeUntil = Time.time + 1.15f;
            nextAbilityAt = Time.time + 3.1f;
            GameSfx.PlayAt(SfxId.EnemyCharge, transform.position);
        }

        private void FireInkShot()
        {
            PlayerController2D target = FindNearestPlayer(11f, false);
            if (target == null) return;
            Vector2 origin = (Vector2)transform.position + new Vector2(direction * enemySize.x * 0.55f, 0.12f);
            Vector2 aim = ((Vector2)target.transform.position - origin).normalized;
            if (Mathf.Abs(aim.x) > 0.08f) direction = Mathf.Sign(aim.x);
            StageEnemyProjectile.Create(transform.parent, this, origin, aim, Mathf.Max(0.16f, enemySize.x * 0.16f));
            GameSfx.PlayAt(SfxId.EnemyShoot, origin);
        }

        private PlayerController2D FindNearestPlayer(float range, bool requireSimilarHeight)
        {
            PlayerController2D[] players = Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
            PlayerController2D nearest = null;
            float best = range * range;
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            Transform local = stageManager != null ? stageManager.ActivePlayerTransform : null;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerController2D player = players[i];
                if (player == null || online && player.transform != local) continue;
                Vector2 delta = player.transform.position - transform.position;
                if (requireSimilarHeight && Mathf.Abs(delta.y) > 1.7f) continue;
                float sqr = delta.sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = player;
                }
            }
            return nearest;
        }

        private bool IsGrounded()
        {
            Bounds bounds = enemyCollider != null ? enemyCollider.bounds : new Bounds(transform.position, enemySize);
            Vector2 start = new Vector2(bounds.center.x, bounds.min.y + 0.08f);
            return Physics2D.Raycast(start, Vector2.down, 0.18f, 1 << 6).collider != null;
        }

        private bool HasGroundAhead()
        {
            Bounds bounds = enemyCollider != null ? enemyCollider.bounds : new Bounds(transform.position, enemySize);
            Vector2 start = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.12f), bounds.min.y + 0.14f);
            return Physics2D.Raycast(start, Vector2.down, 0.55f, 1 << 6).collider != null;
        }

        private bool IsWallAhead()
        {
            Bounds bounds = enemyCollider != null ? enemyCollider.bounds : new Bounds(transform.position, enemySize);
            Vector2 start = new Vector2(bounds.center.x, bounds.center.y);
            return Physics2D.Raycast(start, Vector2.right * direction, bounds.extents.x + 0.14f, 1 << 6).collider != null;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (defeated || collision == null) return;
            PlayerController2D player = collision.collider != null
                ? collision.collider.GetComponentInParent<PlayerController2D>()
                : null;
            if (player == null && collision.otherCollider != null)
                player = collision.otherCollider.GetComponentInParent<PlayerController2D>();

            if (player != null)
            {
                if (player.IsTurtleShelled)
                {
                    GameSfx.PlayAt(SfxId.EnemyShellBounce, collision.GetContact(0).point);
                    ReverseAwayFrom(player.transform.position.x);
                    return;
                }
                if (stageManager == null) stageManager = Object.FindFirstObjectByType<StageManager>();
                if (stageManager == null || !stageManager.IsOnlineStageActive
                    || player.transform == stageManager.ActivePlayerTransform)
                    stageManager?.RespawnFromHazard(player);
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Mathf.Abs(collision.GetContact(i).normal.x) > 0.55f)
                {
                    Reverse();
                    break;
                }
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            PlayerController2D player = collision != null && collision.collider != null
                ? collision.collider.GetComponentInParent<PlayerController2D>()
                : null;
            if (player != null && player.IsTurtleShelled) ReverseAwayFrom(player.transform.position.x);
        }

        private void ReverseAwayFrom(float obstacleX)
        {
            float away = Mathf.Sign(transform.position.x - obstacleX);
            if (Mathf.Abs(transform.position.x - obstacleX) < 0.04f) away = -direction;
            direction = away;
            reverseLockUntil = Time.time + 0.2f;
            if (body != null)
                body.linearVelocity = new Vector2(direction * Mathf.Max(2f, baseSpeed), Mathf.Max(body.linearVelocity.y, 0.8f));
            SetFacingVisual();
        }

        private void Reverse()
        {
            if (Time.time < reverseLockUntil) return;
            direction = -direction;
            reverseLockUntil = Time.time + 0.18f;
            SetFacingVisual();
        }

        public void HitByBomb()
        {
            RequestDefeat();
        }

        public void HitByCatScratch()
        {
            RequestDefeat();
        }

        public void RequestDefeat()
        {
            if (defeated) return;
            if (syncManager == null) syncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            if (syncManager != null && syncManager.IsOnlineActive)
                syncManager.RequestPlacedEnemyDefeat(ObjectId);
            else
                ApplyDefeated();
        }

        public void ApplyDefeated()
        {
            if (defeated) return;
            defeated = true;
            if (enemyCollider != null) enemyCollider.enabled = false;
            if (body != null) body.simulated = false;
            GameSfx.PlayAt(SfxId.EnemyDefeat, transform.position);
            StartCoroutine(DefeatAnimation());
        }

        private IEnumerator DefeatAnimation()
        {
            Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            float elapsed = 0f;
            while (elapsed < 0.28f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.28f);
                if (visualRoot != null)
                    visualRoot.localScale = new Vector3(startScale.x * (1f + t * 0.45f), startScale.y * (1f - t), 1f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void BuildVisual()
        {
            if (transform.Find("Enemy Visual") != null) return;
            visualRoot = new GameObject("Enemy Visual").transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localScale = new Vector3(enemySize.x / 1.25f, enemySize.y / 1.3f, 1f);

            Color bodyColor = EnemyColor(enemyType);
            GameObject bodyObject = CreateSprite("Crayon Body", visualRoot, Vector2.zero, new Vector2(1.05f, 0.9f), bodyColor, 34, true);
            AddOutline(bodyObject.transform, new Color(bodyColor.r * 0.48f, bodyColor.g * 0.48f, bodyColor.b * 0.48f, 1f));
            AddEyes(visualRoot, enemyType == StageObjectType.EnemyShooter ? 1 : 2);
            AddAbilityMark(visualRoot);
            SetFacingVisual();
        }

        private void AddAbilityMark(Transform parent)
        {
            Color ink = new Color(0.12f, 0.08f, 0.16f, 1f);
            if (enemyType == StageObjectType.EnemyJumper)
            {
                AddLine(parent, "Spring Legs", new[] { new Vector2(-0.3f, -0.34f), new Vector2(-0.48f, -0.65f), new Vector2(-0.18f, -0.65f), new Vector2(0f, -0.4f), new Vector2(0.22f, -0.68f), new Vector2(0.5f, -0.68f) }, 0.09f, ink, 36);
            }
            else if (enemyType == StageObjectType.EnemyCharger)
            {
                AddLine(parent, "Charge Horns", new[] { new Vector2(-0.45f, 0.27f), new Vector2(-0.78f, 0.58f), new Vector2(-0.58f, 0.08f) }, 0.1f, ink, 36);
                AddLine(parent, "Charge Horns Right", new[] { new Vector2(0.45f, 0.27f), new Vector2(0.78f, 0.58f), new Vector2(0.58f, 0.08f) }, 0.1f, ink, 36);
            }
            else if (enemyType == StageObjectType.EnemyFlyer
                || enemyType == StageObjectType.EnemyFlyerZigzag
                || enemyType == StageObjectType.EnemyFlyerOrbit)
            {
                AddLine(parent, "Left Wing", new[] { new Vector2(-0.42f, 0.08f), new Vector2(-0.9f, 0.42f), new Vector2(-0.72f, -0.05f), new Vector2(-0.98f, -0.2f) }, 0.105f, ink, 33);
                AddLine(parent, "Right Wing", new[] { new Vector2(0.42f, 0.08f), new Vector2(0.9f, 0.42f), new Vector2(0.72f, -0.05f), new Vector2(0.98f, -0.2f) }, 0.105f, ink, 33);
                if (enemyType == StageObjectType.EnemyFlyerZigzag)
                    AddLine(parent, "Zigzag Mark", new[] { new Vector2(-0.3f, 0.55f), new Vector2(0f, 0.82f), new Vector2(0.3f, 0.55f) }, 0.075f, ink, 37);
                else if (enemyType == StageObjectType.EnemyFlyerOrbit)
                    AddLine(parent, "Orbit Mark", new[] { new Vector2(-0.42f, 0.62f), new Vector2(0f, 0.78f), new Vector2(0.42f, 0.62f), new Vector2(0f, 0.48f), new Vector2(-0.42f, 0.62f) }, 0.065f, ink, 37);
            }
            else if (enemyType == StageObjectType.EnemyShooter)
            {
                CreateSprite("Ink Cannon", parent, new Vector2(0.55f, -0.08f), new Vector2(0.55f, 0.28f), new Color(0.16f, 0.25f, 0.12f, 1f), 36, false);
            }
            else
            {
                AddLine(parent, "Walking Feet", new[] { new Vector2(-0.28f, -0.35f), new Vector2(-0.46f, -0.66f), new Vector2(-0.7f, -0.66f), new Vector2(0.25f, -0.35f), new Vector2(0.48f, -0.66f), new Vector2(0.72f, -0.66f) }, 0.09f, ink, 35);
            }
        }

        private static void AddEyes(Transform parent, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = count == 1 ? 0f : (i == 0 ? -0.23f : 0.23f);
                GameObject eye = CreateSprite("Enemy Eye", parent, new Vector2(x, 0.13f), new Vector2(0.23f, 0.28f), new Color(1f, 0.97f, 0.8f, 1f), 37, true);
                CreateSprite("Pupil", eye.transform, new Vector2(0.12f, -0.03f), Vector2.one * 0.35f, new Color(0.08f, 0.04f, 0.1f, 1f), 38, true);
            }
        }

        private void SetFacingVisual()
        {
            if (visualRoot == null) return;
            float x = Mathf.Abs(enemySize.x / 1.25f) * (direction >= 0f ? 1f : -1f);
            visualRoot.localScale = new Vector3(x, enemySize.y / 1.3f, 1f);
        }

        private static GameObject CreateSprite(string name, Transform parent, Vector2 position, Vector2 scale, Color color, int order, bool circle)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(position.x, position.y, -0.03f);
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = circle ? GetCircleSprite() : GetSquareSprite();
            renderer.color = color;
            renderer.sortingOrder = order;
            return obj;
        }

        private static void AddOutline(Transform parent, Color color)
        {
            Vector2[] points = new Vector2[13];
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                float wobble = i % 2 == 0 ? 0.5f : 0.46f;
                points[i] = new Vector2(Mathf.Cos(angle) * wobble, Mathf.Sin(angle) * wobble);
            }
            points[12] = points[0];
            AddLine(parent, "Crayon Outline", points, 0.075f, color, 35);
        }

        private static void AddLine(Transform parent, string name, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width * 0.92f;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.sharedMaterial = GetLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }

        private static float DefaultSpeed(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.EnemyJumper: return 2.1f;
                case StageObjectType.EnemyCharger: return 1.7f;
                case StageObjectType.EnemyFlyer: return 2.65f;
                case StageObjectType.EnemyFlyerZigzag: return 2.45f;
                case StageObjectType.EnemyFlyerOrbit: return 1.9f;
                case StageObjectType.EnemyShooter: return 0.35f;
                default: return 2.4f;
            }
        }

        private float InitialAbilityDelay()
        {
            float offset = Mathf.Abs(ObjectId.GetHashCode() % 100) * 0.007f;
            return enemyType == StageObjectType.EnemyJumper ? 0.75f + offset : 1.4f + offset;
        }

        private static Color EnemyColor(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.EnemyJumper: return new Color(1f, 0.57f, 0.12f, 0.94f);
                case StageObjectType.EnemyCharger: return new Color(0.93f, 0.18f, 0.15f, 0.94f);
                case StageObjectType.EnemyFlyer: return new Color(0.18f, 0.62f, 0.95f, 0.94f);
                case StageObjectType.EnemyFlyerZigzag: return new Color(0.95f, 0.38f, 0.72f, 0.94f);
                case StageObjectType.EnemyFlyerOrbit: return new Color(0.55f, 0.25f, 0.92f, 0.94f);
                case StageObjectType.EnemyShooter: return new Color(0.42f, 0.76f, 0.24f, 0.94f);
                default: return new Color(0.62f, 0.35f, 0.82f, 0.94f);
            }
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            return lineMaterial;
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite == null)
            {
                Texture2D texture = Texture2D.whiteTexture;
                squareSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
            }
            return squareSprite;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 48;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        Vector2.Distance(new Vector2(x, y), center) <= size * 0.46f ? (byte)255 : (byte)0);
            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return circleSprite;
        }
    }

    public sealed class StageEnemyProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private float expiresAt;
        private StageEnemyCharacter source;

        public static void Create(Transform parent, StageEnemyCharacter source, Vector2 position, Vector2 direction, float size)
        {
            GameObject root = new GameObject("Enemy Ink Shot");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * size;
            root.layer = 9;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = GetProjectileSprite();
            renderer.color = new Color(0.24f, 0.62f, 0.13f, 0.95f);
            renderer.sortingOrder = 42;
            StageEnemyProjectile projectile = root.AddComponent<StageEnemyProjectile>();
            projectile.source = source;
            projectile.velocity = direction.normalized * 6.8f;
            projectile.expiresAt = Time.time + 4f;
            Collider2D sourceCollider = source != null ? source.GetComponent<Collider2D>() : null;
            if (sourceCollider != null) Physics2D.IgnoreCollision(collider, sourceCollider, true);
        }

        private void FixedUpdate()
        {
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
            if (Time.time >= expiresAt) Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || source != null && other.transform.IsChildOf(source.transform)) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null)
            {
                if (!player.IsTurtleShelled)
                {
                    StageManager manager = Object.FindFirstObjectByType<StageManager>();
                    if (manager == null || !manager.IsOnlineStageActive || player.transform == manager.ActivePlayerTransform)
                        manager?.RespawnFromHazard(player);
                }
                Destroy(gameObject);
                return;
            }
            if (other.gameObject.layer == 6) Destroy(gameObject);
        }

        private static Sprite projectileSprite;
        private static Sprite GetProjectileSprite()
        {
            if (projectileSprite != null) return projectileSprite;
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        Vector2.Distance(new Vector2(x, y), center) < size * 0.45f ? (byte)255 : (byte)0);
            texture.SetPixels32(pixels);
            texture.Apply();
            projectileSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return projectileSprite;
        }
    }
}
