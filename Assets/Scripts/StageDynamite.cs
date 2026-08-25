using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageDynamite : MonoBehaviour
    {
        private const float BlastRangeWarningSeconds = 0.8f;

        private float fuseSeconds = 5f;
        private Vector2 visualSize = new Vector2(1.4f, 1.25f);
        private float detonateAt;
        private bool armed;
        private bool exploded;
        private int lastTick = -1;
        private TextMesh countdownText;
        private SpriteRenderer warningGlow;
        private SpriteRenderer fuseSpark;
        private ExplosionRangeIndicator rangeIndicator;
        private StageManager stageManager;
        private StageGimmickSyncManager syncManager;
        private static Sprite circleSprite;
        private static Material lineMaterial;

        public void Configure(float seconds, Vector2 size)
        {
            fuseSeconds = Mathf.Clamp(seconds, 1f, 15f);
            visualSize = new Vector2(Mathf.Max(0.65f, size.x), Mathf.Max(0.65f, size.y));
            BuildVisual();
        }

        private void Start()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
            RuntimeStageEditor editor = Object.FindFirstObjectByType<RuntimeStageEditor>();
            if (editor != null && editor.IsEditing)
            {
                SetCountdown(fuseSeconds);
                enabled = false;
                return;
            }
            SetCountdown(fuseSeconds);
            if (fuseSpark != null) fuseSpark.enabled = false;
            rangeIndicator = ExplosionRangeIndicator.Create(transform.position, CalculateBlastRadius(), true);
            rangeIndicator.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!armed || exploded) return;
            float remaining = Mathf.Max(0f, detonateAt - Time.time);
            SetCountdown(remaining);
            int tick = Mathf.CeilToInt(remaining);
            if (tick != lastTick)
            {
                lastTick = tick;
                GameSfx.PlayAt(SfxId.DynamiteTick, transform.position, remaining <= 1f ? 1.2f : 1f);
            }
            float urgency = 1f - Mathf.Clamp01(remaining / fuseSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(5f, 18f, urgency));
            if (rangeIndicator != null)
            {
                bool showWarning = remaining <= BlastRangeWarningSeconds;
                rangeIndicator.gameObject.SetActive(showWarning);
                if (showWarning)
                {
                    float warningUrgency = 1f - Mathf.Clamp01(remaining / BlastRangeWarningSeconds);
                    rangeIndicator.SetPosition(transform.position);
                    rangeIndicator.SetWarningState(warningUrgency, true);
                }
            }
            if (warningGlow != null)
            {
                warningGlow.color = new Color(1f, 0.1f, 0.025f, Mathf.Lerp(0.08f, 0.55f, urgency * pulse));
            }
            if (fuseSpark != null)
            {
                fuseSpark.transform.localScale = Vector3.one * Mathf.Lerp(0.13f, 0.29f, pulse);
                fuseSpark.color = Color.Lerp(
                    new Color(1f, 0.82f, 0.12f, 0.8f),
                    new Color(1f, 0.16f, 0.02f, 1f),
                    urgency);
            }
            if (remaining <= 0f) Explode();
        }

        public void ActivateFromLink()
        {
            if (armed || exploded) return;
            armed = true;
            detonateAt = Time.time + fuseSeconds;
            lastTick = -1;
            if (fuseSpark != null) fuseSpark.enabled = true;
            GameSfx.PlayAt(SfxId.DynamiteFuseStart, transform.position);
        }

        public void TriggerFromExplosion()
        {
            if (exploded) return;
            if (!armed) armed = true;
            if (fuseSpark != null) fuseSpark.enabled = true;
            detonateAt = Mathf.Min(detonateAt > 0f ? detonateAt : float.MaxValue, Time.time + 0.18f);
        }

        private void Explode()
        {
            if (exploded) return;
            exploded = true;
            armed = false;
            float radius = CalculateBlastRadius();
            DestroyRangeIndicator();
            ApplyBlast(radius);
            DamageBombWalls(radius);
            DefeatEnemies(radius);
            TriggerExplosives(radius);
            CreateExplosionVisual(radius);
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (warningGlow != null) warningGlow.gameObject.SetActive(false);
            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null) ownCollider.enabled = false;
            SetBundleVisible(false);
            GameSfx.PlayAt(SfxId.DynamiteExplosion, transform.position);
            Destroy(gameObject, 1.1f);
        }

        private float CalculateBlastRadius()
        {
            return Mathf.Clamp(Mathf.Max(visualSize.x, visualSize.y) * 5.7f, 6.5f, 12f);
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

        private void ApplyBlast(float radius)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            HashSet<Rigidbody2D> affectedBodies = new HashSet<Rigidbody2D>();
            HashSet<PlayerController2D> affectedPlayers = new HashSet<PlayerController2D>();
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            bool host = !online || stageManager.IsOnlineStageHost;
            Transform localPlayer = stageManager != null ? stageManager.ActivePlayerTransform : null;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                PlayerController2D player = hit.GetComponentInParent<PlayerController2D>();
                if (player != null && affectedPlayers.Add(player) && (!online || player.transform == localPlayer))
                {
                    Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
                    Vector2 offset = (Vector2)player.transform.position - (Vector2)transform.position;
                    float distance = Mathf.Max(0.2f, offset.magnitude);
                    Vector2 launchDirection = (offset.normalized + Vector2.up * 0.9f).normalized;
                    float launch = Mathf.Lerp(32f, 12f, Mathf.Clamp01(distance / radius));
                    if (playerBody != null)
                    {
                        playerBody.linearVelocity = Vector2.zero;
                        playerBody.AddForce(launchDirection * launch, ForceMode2D.Impulse);
                    }
                    if (!player.IsInvulnerable)
                    {
                        stageManager?.RespawnFromHazard(player);
                    }
                }

                Rigidbody2D body = hit.attachedRigidbody;
                if (!host || body == null || body.bodyType != RigidbodyType2D.Dynamic
                    || player != null || !affectedBodies.Add(body))
                {
                    continue;
                }
                Vector2 direction = body.worldCenterOfMass - (Vector2)transform.position;
                float distanceToBody = Mathf.Max(0.25f, direction.magnitude);
                direction = (direction.normalized + Vector2.up * 0.5f).normalized;
                body.AddForce(direction * Mathf.Lerp(42f, 10f, Mathf.Clamp01(distanceToBody / radius)), ForceMode2D.Impulse);
            }
        }

        private void DamageBombWalls(float radius)
        {
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost) return;
            StageBombBreakableWall[] walls = Object.FindObjectsByType<StageBombBreakableWall>(FindObjectsSortMode.None);
            for (int i = 0; i < walls.Length; i++)
            {
                StageBombBreakableWall wall = walls[i];
                if (wall == null || wall.IsBroken) continue;
                Collider2D wallCollider = wall.GetComponentInChildren<Collider2D>();
                Vector2 closest = wallCollider != null
                    ? wallCollider.ClosestPoint(transform.position)
                    : (Vector2)wall.transform.position;
                if ((closest - (Vector2)transform.position).sqrMagnitude > radius * radius) continue;
                wall.HitByBomb(transform.position);
                syncManager?.RegisterBombWallDamage(wall.ObjectId, wall.CurrentHits, transform.position);
            }
        }

        private void TriggerExplosives(float radius)
        {
            StageBomb[] bombs = Object.FindObjectsByType<StageBomb>(FindObjectsSortMode.None);
            for (int i = 0; i < bombs.Length; i++)
            {
                if (bombs[i] != null && ((Vector2)bombs[i].transform.position - (Vector2)transform.position).sqrMagnitude <= radius * radius)
                    bombs[i].TriggerFromExplosion();
            }
            StageDynamite[] dynamites = Object.FindObjectsByType<StageDynamite>(FindObjectsSortMode.None);
            for (int i = 0; i < dynamites.Length; i++)
            {
                if (dynamites[i] != null && dynamites[i] != this
                    && ((Vector2)dynamites[i].transform.position - (Vector2)transform.position).sqrMagnitude <= radius * radius)
                    dynamites[i].TriggerFromExplosion();
            }
        }

        private void DefeatEnemies(float radius)
        {
            if (syncManager != null && syncManager.IsOnlineActive && !syncManager.IsHost) return;
            StageEnemyCharacter[] enemies = Object.FindObjectsByType<StageEnemyCharacter>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                StageEnemyCharacter enemy = enemies[i];
                if (enemy == null || enemy.IsDefeated) continue;
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 closest = enemyCollider != null
                    ? enemyCollider.ClosestPoint(transform.position)
                    : (Vector2)enemy.transform.position;
                if ((closest - (Vector2)transform.position).sqrMagnitude <= radius * radius) enemy.HitByBomb();
            }
        }

        private void BuildVisual()
        {
            if (transform.Find("Dynamite Visual") != null) return;
            GameObject visual = new GameObject("Dynamite Visual");
            visual.transform.SetParent(transform, false);
            bool hasArtwork = StageGun.TryCreateResourceSprite(
                visual.transform,
                "StageObjects/NicoDraw/dynamite",
                "Colored Pencil Dynamite",
                new Vector2(visualSize.x * 1.65f, visualSize.y * 1.05f),
                44);
            Font handwrittenFont = DoodleRuntimeAssets.HandwrittenFont;
            if (!hasArtwork)
            {
            Color[] reds =
            {
                new Color(0.92f, 0.09f, 0.07f, 1f),
                new Color(1f, 0.18f, 0.08f, 1f),
                new Color(0.78f, 0.04f, 0.05f, 1f)
            };
            for (int i = 0; i < 3; i++)
            {
                GameObject stick = new GameObject("Red Stick " + (i + 1));
                stick.transform.SetParent(visual.transform, false);
                stick.transform.localPosition = new Vector3((i - 1) * visualSize.x * 0.22f, 0f, -0.02f);
                stick.transform.localScale = new Vector3(visualSize.x * 0.27f, visualSize.y * 0.82f, 1f);
                SpriteRenderer renderer = stick.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSquareSprite();
                renderer.color = reds[i];
                renderer.sortingOrder = 35 + i;
                AddBoxOutline(stick.transform, new Color(0.24f, 0.025f, 0.02f, 1f), 39);
            }

            GameObject band = new GameObject("Bundle Band");
            band.transform.SetParent(visual.transform, false);
            band.transform.localScale = new Vector3(visualSize.x * 0.78f, visualSize.y * 0.2f, 1f);
            SpriteRenderer bandRenderer = band.AddComponent<SpriteRenderer>();
            bandRenderer.sprite = GetSquareSprite();
            bandRenderer.color = new Color(0.15f, 0.11f, 0.08f, 1f);
            bandRenderer.sortingOrder = 41;

            GameObject badge = new GameObject("Countdown Badge");
            badge.transform.SetParent(visual.transform, false);
            badge.transform.localPosition = new Vector3(0f, visualSize.y * 0.08f, -0.06f);
            badge.transform.localScale = new Vector3(visualSize.x * 0.9f, visualSize.y * 0.68f, 1f);
            SpriteRenderer badgeRenderer = badge.AddComponent<SpriteRenderer>();
            badgeRenderer.sprite = GetCircleSprite();
            badgeRenderer.color = new Color(1f, 0.91f, 0.42f, 0.98f);
            badgeRenderer.sortingOrder = 45;

            GameObject tntObject = new GameObject("TNT Label");
            tntObject.transform.SetParent(visual.transform, false);
            tntObject.transform.localPosition = new Vector3(0f, -visualSize.y * 0.34f, -0.08f);
            TextMesh tntText = tntObject.AddComponent<TextMesh>();
            tntText.text = "TNT";
            tntText.anchor = TextAnchor.MiddleCenter;
            tntText.alignment = TextAlignment.Center;
            tntText.fontSize = 42;
            tntText.characterSize = Mathf.Clamp(Mathf.Min(visualSize.x, visualSize.y) * 0.068f, 0.055f, 0.1f);
            tntText.fontStyle = FontStyle.Bold;
            tntText.color = new Color(1f, 0.88f, 0.22f, 1f);
            if (handwrittenFont != null)
            {
                tntText.font = handwrittenFont;
                tntObject.GetComponent<MeshRenderer>().sharedMaterial = handwrittenFont.material;
            }
            tntObject.GetComponent<MeshRenderer>().sortingOrder = 46;

            GameObject fuse = new GameObject("Curved Fuse");
            fuse.transform.SetParent(visual.transform, false);
            LineRenderer fuseLine = fuse.AddComponent<LineRenderer>();
            fuseLine.useWorldSpace = false;
            fuseLine.positionCount = 4;
            fuseLine.startWidth = 0.065f;
            fuseLine.endWidth = 0.035f;
            fuseLine.numCapVertices = 5;
            fuseLine.sharedMaterial = GetLineMaterial();
            fuseLine.startColor = new Color(0.12f, 0.1f, 0.07f, 1f);
            fuseLine.endColor = new Color(1f, 0.42f, 0.04f, 1f);
            fuseLine.sortingOrder = 42;
            fuseLine.SetPosition(0, new Vector3(0f, visualSize.y * 0.42f, 0f));
            fuseLine.SetPosition(1, new Vector3(0.08f, visualSize.y * 0.62f, 0f));
            fuseLine.SetPosition(2, new Vector3(0.28f, visualSize.y * 0.68f, 0f));
            fuseLine.SetPosition(3, new Vector3(0.36f, visualSize.y * 0.82f, 0f));
            }

            GameObject sparkObject = new GameObject("Burning Fuse Spark");
            sparkObject.transform.SetParent(visual.transform, false);
            sparkObject.transform.localPosition = hasArtwork
                ? new Vector3(-visualSize.x * 0.78f, visualSize.y * 0.27f, -0.07f)
                : new Vector3(0.36f, visualSize.y * 0.82f, -0.07f);
            sparkObject.transform.localScale = Vector3.one * Mathf.Clamp(visualSize.y * 0.16f, 0.08f, 0.14f);
            fuseSpark = sparkObject.AddComponent<SpriteRenderer>();
            fuseSpark.sprite = GetCircleSprite();
            fuseSpark.color = new Color(1f, 0.72f, 0.08f, 0.75f);
            fuseSpark.sortingOrder = 48;

            GameObject glow = new GameObject("Warning Glow");
            glow.transform.SetParent(visual.transform, false);
            glow.transform.localScale = new Vector3(visualSize.x * 1.35f, visualSize.y * 1.35f, 1f);
            warningGlow = glow.AddComponent<SpriteRenderer>();
            warningGlow.sprite = GetCircleSprite();
            warningGlow.color = new Color(1f, 0.1f, 0.02f, 0.08f);
            warningGlow.sortingOrder = 30;

            GameObject textObject = new GameObject("Dynamite Countdown");
            textObject.transform.SetParent(visual.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.09f);
            countdownText = textObject.AddComponent<TextMesh>();
            countdownText.anchor = TextAnchor.MiddleCenter;
            countdownText.alignment = TextAlignment.Center;
            countdownText.fontSize = 56;
            countdownText.characterSize = Mathf.Clamp(Mathf.Min(visualSize.x, visualSize.y) * 0.105f, 0.075f, 0.14f);
            countdownText.fontStyle = FontStyle.Bold;
            countdownText.color = new Color(0.19f, 0.035f, 0.025f, 1f);
            if (handwrittenFont != null)
            {
                countdownText.font = handwrittenFont;
                textObject.GetComponent<MeshRenderer>().sharedMaterial = handwrittenFont.material;
            }
            textObject.GetComponent<MeshRenderer>().sortingOrder = 49;
        }

        private void SetCountdown(float seconds)
        {
            if (countdownText != null) countdownText.text = seconds.ToString("0.0");
        }

        private void SetBundleVisible(bool visible)
        {
            Transform visual = transform.Find("Dynamite Visual");
            if (visual == null) return;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }

        private void CreateExplosionVisual(float radius)
        {
            GameObject root = new GameObject("Dynamite Mega Explosion");
            root.transform.position = transform.position;
            BombExplosionVisual visual = root.AddComponent<BombExplosionVisual>();
            visual.Configure(radius, true);
        }

        private static void AddBoxOutline(Transform parent, Color color, int order)
        {
            GameObject lineObject = new GameObject("Pencil Outline");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.startWidth = 0.045f;
            line.endWidth = 0.045f;
            line.numCapVertices = 3;
            line.sharedMaterial = GetLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            line.SetPosition(0, new Vector2(-0.5f, -0.5f));
            line.SetPosition(1, new Vector2(0.5f, -0.5f));
            line.SetPosition(2, new Vector2(0.5f, 0.5f));
            line.SetPosition(3, new Vector2(-0.5f, 0.5f));
            line.SetPosition(4, new Vector2(-0.5f, -0.5f));
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
