using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageMissileLauncher : MonoBehaviour, IStageLinkActivatable
    {
        private Transform projectileParent;
        private Transform muzzle;
        private float interval;
        private float missileSpeed;
        private float nextLaunchTime;
        private bool linkedMode;
        private int launchSequence;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private float linkedCooldownSeconds;
        private float nextLinkedLaunchTime;

        public void Configure(Transform parent, Transform launchPoint, float seconds, float speed)
        {
            projectileParent = parent;
            muzzle = launchPoint;
            interval = Mathf.Clamp(seconds > 0f ? seconds : 2f, 0.5f, 10f);
            missileSpeed = Mathf.Clamp(speed > 0f ? speed : 8f, 3f, 15f);
        }

        private void Start()
        {
            marker = GetComponent<StageEditorObject>();
            syncManager = Object.FindFirstObjectByType<StageGimmickSyncManager>();
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                enabled = false;
                return;
            }
            nextLaunchTime = Time.time + interval;
        }

        private void Update()
        {
            if (linkedMode || Time.time < nextLaunchTime
                || syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }
            nextLaunchTime = Time.time + interval;
            FireMissile();
        }

        public void PrepareForLink()
        {
            linkedMode = true;
        }

        public void ActivateFromLink()
        {
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost
                || Time.time < nextLinkedLaunchTime) return;
            nextLinkedLaunchTime = Time.time + linkedCooldownSeconds;
            FireMissile();
        }

        public void SetLinkCooldown(float seconds)
        {
            linkedCooldownSeconds = Mathf.Max(0f, seconds);
        }

        private void FireMissile()
        {
            if (muzzle == null)
            {
                return;
            }
            string launcherId = marker != null && !string.IsNullOrEmpty(marker.objectId)
                ? marker.objectId
                : gameObject.name;
            string launchId = launcherId + "_missile_" + launchSequence.ToString("D6");
            launchSequence++;
            if (syncManager != null)
            {
                syncManager.SpawnMissile(
                    launchId,
                    launcherId,
                    transform,
                    muzzle.position,
                    transform.right.normalized,
                    missileSpeed);
            }
            else
            {
                StageMissileProjectile.Create(
                    projectileParent,
                    transform,
                    muzzle.position,
                    transform.right.normalized,
                    missileSpeed);
            }
            GameSfx.PlayAt(SfxId.CannonFire, muzzle.position, 1.05f);
        }
    }

    [DisallowMultipleComponent]
    public sealed class StageMissileProjectile : MonoBehaviour
    {
        private const float LifeSeconds = 14f;
        private const float ExplosionRadius = 1.9f;

        private Transform owner;
        private Rigidbody2D body;
        private LineRenderer flame;
        private float destroyAt;
        private bool exploded;
        private bool terrainPiercing;
        private float explosionRadius = ExplosionRadius;
        private Transform homingTarget;
        private float homingTurnDegreesPerSecond;
        private bool isHoming;
        private static Sprite circleSprite;
        private static Material lineMaterial;

        public static StageMissileProjectile Create(
            Transform parent,
            Transform launcher,
            Vector2 position,
            Vector2 direction,
            float speed,
            bool passThroughTerrain = false,
            float scale = 1f,
            Transform trackingTarget = null,
            float trackingTurnDegreesPerSecond = 0f)
        {
            GameObject root = new GameObject("Launched Missile");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            float clampedScale = Mathf.Clamp(scale, 0.55f, 2.8f);
            root.transform.localScale = Vector3.one * clampedScale;

            Rigidbody2D missileBody = root.AddComponent<Rigidbody2D>();
            missileBody.bodyType = RigidbodyType2D.Kinematic;
            missileBody.gravityScale = 0f;
            missileBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            missileBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            missileBody.linearVelocity = direction.normalized * Mathf.Clamp(speed, 3f, 15f);

            CapsuleCollider2D missileCollider = root.AddComponent<CapsuleCollider2D>();
            missileCollider.direction = CapsuleDirection2D.Horizontal;
            missileCollider.size = new Vector2(0.9f, 0.34f);
            missileCollider.isTrigger = true;

            StageMissileProjectile projectile = root.AddComponent<StageMissileProjectile>();
            projectile.owner = launcher;
            projectile.body = missileBody;
            projectile.terrainPiercing = passThroughTerrain;
            projectile.explosionRadius = ExplosionRadius * clampedScale;
            projectile.homingTarget = trackingTarget;
            projectile.homingTurnDegreesPerSecond = Mathf.Max(0f, trackingTurnDegreesPerSecond);
            projectile.isHoming = trackingTarget != null && projectile.homingTurnDegreesPerSecond > 0f;
            projectile.BuildVisual();
            return projectile;
        }

        private void Start()
        {
            destroyAt = Time.time + LifeSeconds;
        }

        private void Update()
        {
            if (exploded)
            {
                return;
            }
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
                return;
            }
            if (flame != null)
            {
                float flicker = 0.72f + Mathf.Abs(Mathf.Sin(Time.time * 24f)) * 0.38f;
                flame.SetPosition(1, new Vector3(-0.72f * flicker, 0f, 0f));
            }
        }

        private void FixedUpdate()
        {
            if (exploded || !isHoming || homingTarget == null || body == null) return;
            Vector2 current = body.linearVelocity;
            float speed = current.magnitude;
            if (speed < 0.01f) return;
            Vector2 desired = ((Vector2)homingTarget.position - body.position).normalized;
            float maxRadians = homingTurnDegreesPerSecond * Mathf.Deg2Rad * Time.fixedDeltaTime;
            Vector2 steered = Vector3.RotateTowards(current.normalized, desired, maxRadians, 0f);
            body.linearVelocity = steered.normalized * speed;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(steered.y, steered.x) * Mathf.Rad2Deg);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (exploded || other == null || other.isTrigger
                || owner != null && (other.transform == owner || other.transform.IsChildOf(owner)))
            {
                return;
            }

            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player != null && player.IsTurtleShelled)
            {
                Explode(false);
                return;
            }

            if (terrainPiercing && other.gameObject.layer == 6)
            {
                return;
            }

            if (player != null || other.gameObject.layer == 6 || other.attachedRigidbody != null)
            {
                Explode(true);
            }
        }

        private void Explode(bool applyDamage)
        {
            if (exploded)
            {
                return;
            }
            exploded = true;
            if (body != null) body.simulated = false;
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null) ownCollider.enabled = false;

            if (applyDamage)
            {
                ApplyExplosionDamage();
            }

            GameObject effectRoot = new GameObject("Missile Explosion");
            effectRoot.transform.position = transform.position;
            BombExplosionVisual visual = effectRoot.AddComponent<BombExplosionVisual>();
            visual.Configure(explosionRadius, false);
            GameSfx.PlayAt(SfxId.BombExplosion, transform.position, 1.05f);
            Destroy(gameObject);
        }

        private void ApplyExplosionDamage()
        {
            StageValueCoinChallengeController.BreakCratesInRadius(transform.position, explosionRadius);
            Object.FindFirstObjectByType<StageMirrorFinalBossController>()?.ApplyAreaDamage(transform.position, explosionRadius, 1);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            HashSet<PlayerController2D> players = new HashSet<PlayerController2D>();
            HashSet<Rigidbody2D> bodies = new HashSet<Rigidbody2D>();
            HashSet<StageEnemyCharacter> enemies = new HashSet<StageEnemyCharacter>();
            StageManager manager = Object.FindFirstObjectByType<StageManager>();
            bool online = manager != null && manager.IsOnlineStageActive;
            Transform localPlayer = manager != null ? manager.ActivePlayerTransform : null;
            StageEditorObject ownerMarker = owner != null ? owner.GetComponent<StageEditorObject>() : null;
            bool friendlyDefenseMissile = ownerMarker != null
                && !string.IsNullOrEmpty(ownerMarker.objectId)
                && ownerMarker.objectId.StartsWith("13-1_", System.StringComparison.Ordinal);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                StageEnemyCharacter enemy = hit.GetComponentInParent<StageEnemyCharacter>();
                if (enemy != null && enemies.Add(enemy))
                {
                    StageTowerDefenseEnemyHealth defenseEnemy = enemy.GetComponent<StageTowerDefenseEnemyHealth>();
                    if (defenseEnemy != null) defenseEnemy.HitByBomb();
                    else enemy.HitByBomb();
                }
                PlayerController2D player = hit.GetComponentInParent<PlayerController2D>();
                if (!friendlyDefenseMissile && player != null && players.Add(player) && !player.IsInvulnerable
                    && (!online || player.transform == localPlayer))
                {
                    manager?.RespawnFromHazard(player);
                }
                if (friendlyDefenseMissile && player != null)
                {
                    continue;
                }

                Rigidbody2D targetBody = hit.attachedRigidbody;
                if (targetBody == null || targetBody.bodyType != RigidbodyType2D.Dynamic || !bodies.Add(targetBody))
                {
                    continue;
                }
                Vector2 away = targetBody.worldCenterOfMass - (Vector2)transform.position;
                if (away.sqrMagnitude < 0.01f) away = Vector2.up;
                targetBody.AddForce(away.normalized * 12f, ForceMode2D.Impulse);
            }
        }

        private void BuildVisual()
        {
            GameObject bodyObject = new GameObject("Missile Body");
            bodyObject.transform.SetParent(transform, false);
            bodyObject.transform.localScale = new Vector3(0.82f, 0.28f, 1f);
            SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = GetSquareSprite();
            bodyRenderer.color = isHoming
                ? new Color(0.72f, 0.18f, 0.9f, 1f)
                : new Color(0.92f, 0.22f, 0.12f, 1f);
            bodyRenderer.sortingOrder = 250;

            GameObject noseObject = new GameObject("Missile Nose");
            noseObject.transform.SetParent(transform, false);
            noseObject.transform.localPosition = new Vector3(0.45f, 0f, -0.02f);
            noseObject.transform.localScale = new Vector3(0.34f, 0.3f, 1f);
            SpriteRenderer nose = noseObject.AddComponent<SpriteRenderer>();
            nose.sprite = GetCircleSprite();
            nose.color = isHoming
                ? new Color(0.2f, 0.95f, 1f, 1f)
                : new Color(1f, 0.62f, 0.08f, 1f);
            nose.sortingOrder = 251;

            GameObject flameObject = new GameObject("Missile Flame");
            flameObject.transform.SetParent(transform, false);
            flame = flameObject.AddComponent<LineRenderer>();
            flame.useWorldSpace = false;
            flame.positionCount = 2;
            flame.startWidth = 0.22f;
            flame.endWidth = 0.03f;
            flame.numCapVertices = 3;
            flame.sharedMaterial = GetLineMaterial();
            flame.startColor = new Color(1f, 0.95f, 0.22f, 1f);
            flame.endColor = new Color(1f, 0.08f, 0.01f, 0f);
            flame.sortingOrder = 249;
            flame.SetPosition(0, new Vector3(-0.42f, 0f, 0f));
            flame.SetPosition(1, new Vector3(-0.72f, 0f, 0f));
        }

        private static Material GetLineMaterial()
        {
            if (lineMaterial == null) lineMaterial = DoodleRuntimeAssets.LineMaterial;
            return lineMaterial;
        }

        private static Sprite GetSquareSprite()
        {
            return DoodleRuntimeAssets.SquareSprite;
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
}
