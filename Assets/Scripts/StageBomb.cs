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
        private const float BlastRangeWarningSeconds = 0.65f;

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
        private ExplosionRangeIndicator rangeIndicator;

        public string ObjectId => marker != null && !string.IsNullOrEmpty(marker.objectId)
            ? marker.objectId
            : gameObject.name;
        public float BlastRadius => Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 2.8f;
        public bool HasExploded => exploded;
        public float RemainingFuseSeconds => exploded ? 0f : armed ? Mathf.Max(0f, detonateAt - Time.time) : fuseSeconds;

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
            UpdateRangeIndicator(remaining);
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
            EnsureRangeIndicator();
            if (countdownText != null)
            {
                countdownText.color = Color.white;
            }
            GameSfx.PlayAt(SfxId.BombFuseStart, transform.position, 1.05f);
        }

        private void EnsureRangeIndicator()
        {
            if (rangeIndicator == null)
            {
                rangeIndicator = ExplosionRangeIndicator.Create(transform.position, BlastRadius, false);
                rangeIndicator.gameObject.SetActive(false);
            }
        }

        private void UpdateRangeIndicator(float remaining)
        {
            if (rangeIndicator == null)
            {
                return;
            }

            bool showWarning = remaining <= BlastRangeWarningSeconds;
            rangeIndicator.gameObject.SetActive(showWarning);
            if (!showWarning)
            {
                return;
            }

            float urgency = 1f - Mathf.Clamp01(remaining / BlastRangeWarningSeconds);
            rangeIndicator.SetPosition(transform.position);
            rangeIndicator.SetWarningState(urgency, true);
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
            DestroyRangeIndicator();
            ReleaseFromCarriers();
            BreakBombWalls(position, radius);
            if (applyGameplay) StageValueCoinChallengeController.BreakCratesInRadius(position, radius);
            DefeatBlockBreakerEnemies(position, radius, applyGameplay);
            DamageTowerDefenseAlly(position, radius, applyGameplay);
            TriggerNearbyBombs(position, radius, applyGameplay);
            if (applyGameplay)
            {
                ApplyBlastToPlayersAndBodies(position, radius);
                Object.FindFirstObjectByType<StageMirrorFinalBossController>()?.ApplyAreaDamage(position, radius, 2);
            }

            CreateExplosionVisual(position, radius);
            GameSfx.PlayAt(SfxId.BombExplosion, position, 1.35f);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void DestroyRangeIndicator()
        {
            if (rangeIndicator != null)
            {
                Destroy(rangeIndicator.gameObject);
                rangeIndicator = null;
            }
        }

        private void OnDestroy()
        {
            DestroyRangeIndicator();
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
                if ((closest - position).sqrMagnitude <= radius * radius)
                {
                    StageTowerDefenseEnemyHealth defenseEnemy = enemy.GetComponent<StageTowerDefenseEnemyHealth>();
                    if (defenseEnemy != null) defenseEnemy.HitByBomb();
                    else enemy.HitByBomb();
                }
            }
        }

        private void DamageTowerDefenseAlly(Vector2 position, float radius, bool applyGameplay)
        {
            // Only bombs dropped by the invading 8-3 bombers can hurt the
            // protected friend. Player-triggered airstrikes remain friendly.
            if (!applyGameplay || ObjectId.IndexOf("_air_bomb_", System.StringComparison.Ordinal) < 0) return;
            StageTowerDefenseAlly ally = Object.FindFirstObjectByType<StageTowerDefenseAlly>();
            ally?.TryHitByEnemyBomb(position, radius);
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

                StageEscortFriend escortFriend = hit.GetComponentInParent<StageEscortFriend>();
                if (canSimulateWorldBodies && escortFriend != null)
                {
                    escortFriend.Defeat();
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
            Font handwritten = DoodleRuntimeAssets.HandwrittenFont;
            if (handwritten != null)
            {
                countdownText.font = handwritten;
                textRenderer.sharedMaterial = handwritten.material;
            }
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

    public sealed class ExplosionRangeIndicator : MonoBehaviour
    {
        private const int CircleSegments = 72;

        private LineRenderer outerRing;
        private LineRenderer innerRing;
        private SpriteRenderer areaFill;
        private float radius;
        private bool mega;
        private static Material sharedLineMaterial;
        private static Sprite circleSprite;

        public static ExplosionRangeIndicator Create(Vector2 position, float blastRadius, bool isMega)
        {
            GameObject root = new GameObject(isMega ? "Dynamite Blast Range" : "Bomb Blast Range");
            root.transform.position = position;
            ExplosionRangeIndicator indicator = root.AddComponent<ExplosionRangeIndicator>();
            indicator.Configure(blastRadius, isMega);
            return indicator;
        }

        public void SetPosition(Vector2 position)
        {
            transform.position = position;
        }

        public void SetWarningState(float urgency, bool active)
        {
            urgency = Mathf.Clamp01(urgency);
            float speed = Mathf.Lerp(2.8f, 13f, urgency);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * speed);
            Color warning = Color.Lerp(
                new Color(1f, 0.72f, 0.05f, 1f),
                new Color(1f, 0.08f, 0.02f, 1f),
                urgency);
            float idleAlpha = mega ? 0.32f : 0.18f;
            float ringAlpha = active ? Mathf.Lerp(0.48f, 0.92f, urgency * (0.7f + pulse * 0.3f)) : idleAlpha;
            warning.a = ringAlpha;
            outerRing.startColor = warning;
            outerRing.endColor = warning;
            outerRing.widthMultiplier = (mega ? 0.11f : 0.075f) * (1f + pulse * urgency * 0.35f);

            Color innerColor = warning;
            innerColor.a = ringAlpha * 0.48f;
            innerRing.startColor = innerColor;
            innerRing.endColor = innerColor;
            innerRing.transform.localScale = Vector3.one * Mathf.Lerp(0.93f, 0.985f, pulse);

            Color fillColor = warning;
            fillColor.a = active
                ? Mathf.Lerp(0.025f, mega ? 0.13f : 0.09f, urgency * (0.65f + pulse * 0.35f))
                : mega ? 0.035f : 0.018f;
            areaFill.color = fillColor;
        }

        private void Configure(float blastRadius, bool isMega)
        {
            radius = Mathf.Max(0.5f, blastRadius);
            mega = isMega;

            GameObject fillObject = new GameObject("Blast Area Fill");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localScale = Vector3.one * radius * 2f;
            areaFill = fillObject.AddComponent<SpriteRenderer>();
            areaFill.sprite = GetCircleSprite();
            areaFill.sortingOrder = 285;

            outerRing = CreateRing("Blast Range Boundary", radius, 290);
            innerRing = CreateRing("Blast Range Pulse", radius * 0.93f, 289);
            SetWarningState(0f, false);
        }

        private LineRenderer CreateRing(string objectName, float ringRadius, int sortingOrder)
        {
            GameObject ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(transform, false);
            LineRenderer line = ringObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.numCornerVertices = 2;
            line.sharedMaterial = GetLineMaterial();
            line.sortingOrder = sortingOrder;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * ringRadius);
            }
            return line;
        }

        private static Material GetLineMaterial()
        {
            if (sharedLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    sharedLineMaterial = new Material(shader);
                }
            }
            return sharedLineMaterial;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int textureSize = 64;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "Explosion Range Circle";
            Color32[] pixels = new Color32[textureSize * textureSize];
            Vector2 center = Vector2.one * (textureSize - 1) * 0.5f;
            float maxRadius = textureSize * 0.48f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float normalized = Mathf.Clamp01((maxRadius - Vector2.Distance(new Vector2(x, y), center)) / 2f);
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)(normalized * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            return circleSprite;
        }
    }

    public sealed class BombExplosionVisual : MonoBehaviour
    {
        private const int CircleSegments = 64;

        private readonly List<LineRenderer> rings = new List<LineRenderer>();
        private readonly List<LineRenderer> sparks = new List<LineRenderer>();
        private readonly List<Vector2> sparkDirections = new List<Vector2>();
        private readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();
        private Material material;
        private SpriteRenderer flash;
        private SpriteRenderer fireball;
        private float radius;
        private float duration;
        private bool mega;
        private float elapsed;
        private string originStageId;
        private StageManager stageManager;
        private static Sprite circleSprite;

        public void Configure(float blastRadius, bool isMega = false, string sourceStageId = null)
        {
            originStageId = sourceStageId;
            if (!IsOriginStageCurrent())
            {
                HideAndDestroy();
                return;
            }

            radius = Mathf.Max(0.5f, blastRadius);
            mega = isMega;
            duration = mega ? 1.25f : 0.82f;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Destroy(gameObject);
                return;
            }
            material = new Material(shader);

            flash = CreateDisc("Explosion White Flash", Color.white, 334);
            fireball = CreateDisc(
                "Explosion Fireball",
                new Color(1f, 0.34f, 0.025f, 0.9f),
                330);

            for (int ring = 0; ring < (mega ? 4 : 3); ring++)
            {
                GameObject ringObject = new GameObject("Explosion Ring " + ring);
                ringObject.transform.SetParent(transform, false);
                LineRenderer line = ringObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = CircleSegments;
                line.numCornerVertices = 3;
                line.widthMultiplier = (mega ? 0.2f : 0.13f) - ring * 0.018f;
                line.sharedMaterial = material;
                line.sortingOrder = 322 + ring;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                }
                rings.Add(line);
            }

            int cloudCount = mega ? 18 : 11;
            for (int cloud = 0; cloud < cloudCount; cloud++)
            {
                GameObject cloudObject = new GameObject("Explosion Cloud " + cloud);
                cloudObject.transform.SetParent(transform, false);
                SpriteRenderer renderer = cloudObject.AddComponent<SpriteRenderer>();
                renderer.sprite = GetCircleSprite();
                renderer.sortingOrder = 326 + cloud % 3;
                clouds.Add(renderer);
            }

            int sparkCount = mega ? 28 : 16;
            for (int spark = 0; spark < sparkCount; spark++)
            {
                GameObject sparkObject = new GameObject("Explosion Spark " + spark);
                sparkObject.transform.SetParent(transform, false);
                LineRenderer line = sparkObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.numCapVertices = 3;
                line.widthMultiplier = mega ? 0.11f : 0.07f;
                line.sharedMaterial = material;
                line.sortingOrder = 332;
                float angle = spark / (float)sparkCount * Mathf.PI * 2f
                    + Mathf.Sin(spark * 7.13f) * 0.16f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                sparkDirections.Add(direction);
                sparks.Add(line);
            }
        }

        private SpriteRenderer CreateDisc(string objectName, Color color, int sortingOrder)
        {
            GameObject discObject = new GameObject(objectName);
            discObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = discObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCircleSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void Update()
        {
            if (!IsOriginStageCurrent())
            {
                HideAndDestroy();
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float expansion = 1f - Mathf.Pow(1f - progress, 3f);

            float flashProgress = Mathf.Clamp01(progress * 4.5f);
            flash.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.08f, 0.72f, flashProgress);
            flash.color = new Color(1f, 0.98f, 0.78f, Mathf.Lerp(0.95f, 0f, flashProgress));

            float fireProgress = Mathf.Clamp01(progress * 1.8f);
            float fireFade = 1f - Mathf.Clamp01((progress - 0.28f) / 0.58f);
            fireball.transform.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.06f, mega ? 0.62f : 0.48f, fireProgress);
            fireball.color = new Color(
                1f,
                Mathf.Lerp(0.78f, 0.08f, progress),
                0.015f,
                fireFade * (mega ? 0.82f : 0.72f));

            for (int ring = 0; ring < rings.Count; ring++)
            {
                LineRenderer line = rings[ring];
                float ringProgress = Mathf.Clamp01(progress * (mega ? 1.55f : 1.75f) - ring * 0.09f);
                float ringRadius = Mathf.Lerp(radius * 0.04f, radius, 1f - Mathf.Pow(1f - ringProgress, 2.5f));
                line.transform.localScale = Vector3.one * ringRadius;
                float fade = 1f - Mathf.Clamp01((progress - 0.35f - ring * 0.025f) / 0.58f);
                Color color = Color.Lerp(
                    new Color(1f, 0.96f, 0.38f, fade),
                    new Color(1f, 0.12f, 0.015f, fade),
                    progress);
                line.startColor = color;
                line.endColor = color;
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                float angle = i / (float)clouds.Count * Mathf.PI * 2f + Mathf.Sin(i * 4.17f) * 0.28f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float spread = radius * Mathf.Lerp(0.08f, mega ? 0.48f : 0.38f, expansion)
                    * Mathf.Lerp(0.72f, 1.08f, Mathf.Abs(Mathf.Sin(i * 2.31f)));
                SpriteRenderer cloud = clouds[i];
                cloud.transform.localPosition = direction * spread;
                float cloudScale = radius * Mathf.Lerp(0.08f, mega ? 0.31f : 0.24f, expansion)
                    * Mathf.Lerp(0.72f, 1.2f, Mathf.Abs(Mathf.Cos(i * 1.71f)));
                cloud.transform.localScale = Vector3.one * cloudScale;
                float smoke = Mathf.Clamp01((progress - 0.32f) / 0.45f);
                float alpha = (1f - progress) * Mathf.Lerp(0.88f, mega ? 0.48f : 0.28f, smoke);
                cloud.color = Color.Lerp(
                    new Color(1f, 0.52f, 0.035f, alpha),
                    new Color(0.2f, 0.16f, 0.16f, alpha),
                    smoke);
            }

            for (int i = 0; i < sparks.Count; i++)
            {
                LineRenderer spark = sparks[i];
                Vector2 direction = sparkDirections[i];
                float variation = Mathf.Lerp(0.72f, 1.18f, Mathf.Abs(Mathf.Sin(i * 3.77f)));
                float distance = radius * expansion * variation;
                float tail = Mathf.Max(0.1f, radius * (mega ? 0.09f : 0.065f));
                spark.SetPosition(0, direction * Mathf.Max(0f, distance - tail));
                spark.SetPosition(1, direction * distance);
                float alpha = 1f - Mathf.Clamp01(progress * 1.15f);
                Color sparkColor = new Color(1f, Mathf.Lerp(0.95f, 0.28f, progress), 0.04f, alpha);
                spark.startColor = sparkColor;
                spark.endColor = new Color(sparkColor.r, sparkColor.g, sparkColor.b, 0f);
            }

            if (progress >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private bool IsOriginStageCurrent()
        {
            if (string.IsNullOrEmpty(originStageId))
            {
                return true;
            }

            if (stageManager == null)
            {
                stageManager = Object.FindFirstObjectByType<StageManager>();
            }

            return stageManager == null || stageManager.CurrentStageId == originStageId;
        }

        private void HideAndDestroy()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = false;
            }
            if (material != null)
            {
                Destroy(material);
            }
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int textureSize = 64;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.name = "Explosion Soft Circle";
            Color32[] pixels = new Color32[textureSize * textureSize];
            Vector2 center = Vector2.one * (textureSize - 1) * 0.5f;
            float maxRadius = textureSize * 0.48f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01((maxRadius - distance) / 3f);
                    pixels[y * textureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                textureSize);
            return circleSprite;
        }
    }
}
