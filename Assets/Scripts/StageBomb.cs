using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class StageBomb : MonoBehaviour
    {
        private const float MinimumFuseSeconds = 0.2f;

        private bool startsOnPickup;
        private float fuseSeconds = 5f;
        private float detonateAt;
        private bool armed;
        private bool exploded;
        private int lastTickSecond = -1;
        private TextMesh countdownText;
        private SpriteRenderer pulseRenderer;
        private Color pulseBaseColor = Color.white;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;

        public string ObjectId => marker != null && !string.IsNullOrEmpty(marker.objectId)
            ? marker.objectId
            : gameObject.name;
        public float BlastRadius => Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 2.8f;
        public bool HasExploded => exploded;

        public void Configure(bool armOnPickup, float seconds)
        {
            startsOnPickup = armOnPickup;
            fuseSeconds = Mathf.Max(MinimumFuseSeconds, seconds);
        }

        private void Start()
        {
            marker = GetComponent<StageEditorObject>();
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            CreateCountdownDisplay();

            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                countdownText.text = startsOnPickup ? "F" : fuseSeconds.ToString("0.0");
                enabled = false;
                return;
            }

            if (startsOnPickup)
            {
                countdownText.text = "F";
                countdownText.color = new Color(0.3f, 0.82f, 1f, 1f);
            }
            else
            {
                Arm(fuseSeconds);
            }
        }

        private void Update()
        {
            if (!armed || exploded)
            {
                return;
            }

            float remaining = Mathf.Max(0f, detonateAt - Time.time);
            UpdateCountdownVisual(remaining);
            if (remaining > 0f)
            {
                return;
            }

            if (syncManager != null && syncManager.IsOnlineActive)
            {
                if (syncManager.IsHost)
                {
                    syncManager.DetonateBomb(ObjectId, transform.position, BlastRadius);
                }
                return;
            }

            ApplyExplosion(transform.position, BlastRadius, true);
        }

        public void NotifyPickedUp()
        {
            if (startsOnPickup && !armed && !exploded)
            {
                if (syncManager != null && syncManager.IsOnlineActive)
                {
                    syncManager.RequestArmBomb(ObjectId);
                }
                Arm(fuseSeconds);
            }
        }

        public void ArmFromNetwork()
        {
            if (!armed && !exploded)
            {
                Arm(fuseSeconds);
            }
        }

        public void TriggerFromExplosion()
        {
            if (!exploded)
            {
                Arm(Mathf.Min(0.16f, fuseSeconds));
            }
        }

        public void ApplyNetworkExplosion(Vector2 position, float radius, bool applyGameplay)
        {
            ApplyExplosion(position, radius, applyGameplay);
        }

        private void Arm(float delay)
        {
            armed = true;
            detonateAt = Time.time + Mathf.Max(MinimumFuseSeconds, delay);
            lastTickSecond = -1;
            if (countdownText != null)
            {
                countdownText.color = Color.white;
            }
            GameSfx.PlayAt(SfxId.BombFuseStart, transform.position, 1.05f);
        }

        private void UpdateCountdownVisual(float remaining)
        {
            if (countdownText == null)
            {
                return;
            }

            countdownText.text = remaining.ToString("0.0");
            int wholeSecond = Mathf.CeilToInt(remaining);
            if (wholeSecond != lastTickSecond)
            {
                lastTickSecond = wholeSecond;
                GameSfx.PlayAt(SfxId.BombTick, transform.position, remaining <= 1f ? 1.25f : 0.9f);
            }

            float urgency = 1f - Mathf.Clamp01(remaining / fuseSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 16f, urgency));
            countdownText.color = Color.Lerp(Color.white, new Color(1f, 0.16f, 0.08f, 1f), urgency * (0.62f + pulse * 0.38f));
            countdownText.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.14f, pulse * urgency);
            if (pulseRenderer != null)
            {
                pulseRenderer.color = Color.Lerp(pulseBaseColor, new Color(1f, 0.08f, 0.04f, 1f), urgency * pulse * 0.75f);
            }
        }

        private void ApplyExplosion(Vector2 position, float radius, bool applyGameplay)
        {
            if (exploded)
            {
                return;
            }

            exploded = true;
            armed = false;
            ReleaseFromCarriers();
            BreakBombWalls(position, radius);
            DefeatBlockBreakerEnemies(position, radius, applyGameplay);
            TriggerNearbyBombs(position, radius, applyGameplay);
            if (applyGameplay)
            {
                ApplyBlastToPlayersAndBodies(position, radius);
            }

            CreateExplosionVisual(position, radius);
            GameSfx.PlayAt(SfxId.BombExplosion, position, 1.35f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void BreakBombWalls(Vector2 position, float radius)
        {
            StageBombBreakableWall[] walls = Object.FindObjectsByType<StageBombBreakableWall>(FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                StageBombBreakableWall wall = walls[i];
                if (wall == null || wall.IsBroken)
                {
                    continue;
                }

                Collider2D wallCollider = wall.GetComponentInChildren<Collider2D>();
                Vector2 closest = wallCollider != null
                    ? wallCollider.ClosestPoint(position)
                    : (Vector2)wall.transform.position;
                if ((closest - position).sqrMagnitude > radius * radius)
                {
                    continue;
                }

                bool destroyed = wall.HitByBomb(position);
                syncManager?.RegisterBombWallDamage(wall.ObjectId, wall.CurrentHits, position);
                GameSfx.PlayAt(SfxId.BombWallBreak, closest, destroyed ? 1.1f : 0.62f);
            }
        }

        private void TriggerNearbyBombs(Vector2 position, float radius, bool applyGameplay)
        {
            if (!applyGameplay)
            {
                return;
            }

            StageBomb[] bombs = Object.FindObjectsByType<StageBomb>(FindObjectsSortMode.None);
            for (int i = 0; i < bombs.Length; i++)
            {
                StageBomb bomb = bombs[i];
                if (bomb != null && bomb != this && !bomb.exploded
                    && ((Vector2)bomb.transform.position - position).sqrMagnitude <= radius * radius)
                {
                    bomb.TriggerFromExplosion();
                }
            }
        }

        private void DefeatBlockBreakerEnemies(Vector2 position, float radius, bool applyGameplay)
        {
            if (!applyGameplay || syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost)
            {
                return;
            }
            StageBlockBreakerEnemy[] enemies = Object.FindObjectsByType<StageBlockBreakerEnemy>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                StageBlockBreakerEnemy enemy = enemies[i];
                if (enemy == null)
                {
                    continue;
                }
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 closest = enemyCollider != null ? enemyCollider.ClosestPoint(position) : (Vector2)enemy.transform.position;
                if ((closest - position).sqrMagnitude <= radius * radius)
                {
                    enemy.HitByBomb();
                }
            }

            StageEnemyCharacter[] placedEnemies = Object.FindObjectsByType<StageEnemyCharacter>(FindObjectsSortMode.None);
            for (int i = 0; i < placedEnemies.Length; i++)
            {
                StageEnemyCharacter enemy = placedEnemies[i];
                if (enemy == null || enemy.IsDefeated) continue;
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 closest = enemyCollider != null ? enemyCollider.ClosestPoint(position) : (Vector2)enemy.transform.position;
                if ((closest - position).sqrMagnitude <= radius * radius) enemy.HitByBomb();
            }
        }

        private void ApplyBlastToPlayersAndBodies(Vector2 position, float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius);
            HashSet<Rigidbody2D> affectedBodies = new HashSet<Rigidbody2D>();
            HashSet<PlayerController2D> affectedPlayers = new HashSet<PlayerController2D>();
            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            bool online = syncManager != null && syncManager.IsOnlineActive;
            Transform localPlayer = stageManager != null ? stageManager.ActivePlayerTransform : null;
            bool canSimulateWorldBodies = !online || syncManager.IsHost;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                PlayerController2D player = hit.GetComponentInParent<PlayerController2D>();
                if (player != null
                    && affectedPlayers.Add(player)
                    && !player.IsInvulnerable
                    && (!online || player.transform == localPlayer))
                {
                    stageManager?.RespawnFromHazard(player);
                }

                Rigidbody2D body = hit.attachedRigidbody;
                if (!canSimulateWorldBodies
                    || body == null
                    || body.bodyType != RigidbodyType2D.Dynamic
                    || !affectedBodies.Add(body))
                {
                    continue;
                }
                Vector2 direction = body.worldCenterOfMass - position;
                float distance = Mathf.Max(0.25f, direction.magnitude);
                body.AddForce(direction.normalized * Mathf.Lerp(24f, 7f, distance / radius), ForceMode2D.Impulse);
            }
        }

        private void ReleaseFromCarriers()
        {
            PlayerCarryController[] carriers = Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None);
            for (int i = 0; i < carriers.Length; i++)
            {
                carriers[i]?.ReleaseIfHolding(transform);
            }
        }

        private void CreateCountdownDisplay()
        {
            GameObject textObject = new GameObject("Bomb Countdown");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = new Vector3(0f, -0.05f, -0.12f);
            countdownText = textObject.AddComponent<TextMesh>();
            countdownText.anchor = TextAnchor.MiddleCenter;
            countdownText.alignment = TextAlignment.Center;
            countdownText.fontSize = 52;
            countdownText.characterSize = 0.105f;
            countdownText.fontStyle = FontStyle.Bold;
            countdownText.color = Color.white;
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = 230;

            pulseRenderer = GetComponentInChildren<SpriteRenderer>();
            if (pulseRenderer != null)
            {
                pulseBaseColor = pulseRenderer.color;
            }
        }

        private static void CreateExplosionVisual(Vector2 position, float radius)
        {
            GameObject root = new GameObject("Bomb Explosion");
            root.transform.position = position;
            BombExplosionVisual visual = root.AddComponent<BombExplosionVisual>();
            visual.Configure(radius);
        }
    }

    public sealed class BombExplosionVisual : MonoBehaviour
    {
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private Material material;
        private float radius;
        private float elapsed;

        public void Configure(float blastRadius)
        {
            radius = Mathf.Max(0.5f, blastRadius);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Destroy(gameObject);
                return;
            }
            material = new Material(shader);

            for (int ring = 0; ring < 3; ring++)
            {
                GameObject ringObject = new GameObject("Explosion Ring " + ring);
                ringObject.transform.SetParent(transform, false);
                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 40;
                line.widthMultiplier = 0.12f - ring * 0.02f;
                line.sharedMaterial = material;
                line.sortingOrder = 300 + ring;
                lines.Add(line);
            }

            for (int ray = 0; ray < 12; ray++)
            {
                GameObject rayObject = new GameObject("Explosion Ray " + ray);
                rayObject.transform.SetParent(transform, false);
                LineRenderer line = rayObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.widthMultiplier = 0.1f;
                line.sharedMaterial = material;
                line.sortingOrder = 304;
                float angle = ray / 12f * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                line.SetPosition(0, direction * 0.2f);
                line.SetPosition(1, direction * radius);
                lines.Add(line);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / 0.48f);
            for (int ring = 0; ring < Mathf.Min(3, lines.Count); ring++)
            {
                LineRenderer line = lines[ring];
                float ringProgress = Mathf.Clamp01(progress * 1.45f - ring * 0.15f);
                float ringRadius = Mathf.Lerp(0.12f, radius, ringProgress);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i / (float)line.positionCount * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius);
                }
                Color color = Color.Lerp(new Color(1f, 0.95f, 0.35f, 1f), new Color(1f, 0.08f, 0.02f, 0f), progress);
                line.startColor = color;
                line.endColor = color;
            }
            for (int i = 3; i < lines.Count; i++)
            {
                Color color = new Color(1f, Mathf.Lerp(0.8f, 0.1f, progress), 0.02f, 1f - progress);
                lines[i].startColor = color;
                lines[i].endColor = color;
                lines[i].transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, progress);
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
