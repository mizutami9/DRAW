using System;
using System.Globalization;
using UnityEngine;

namespace DrawBody.Prototype
{
    [Serializable]
    public sealed class OnlineCrumblingFloorState
    {
        public bool Triggered;
        public bool Broken;
        public float Progress;
    }

    [DisallowMultipleComponent]
    public sealed class StageCrumblingFloor : MonoBehaviour
    {
        private const float FallDuration = 0.7f;
        private static Sprite countdownBadgeSprite;

        private Collider2D[] floorColliders;
        private LineRenderer[] cracks;
        private TextMesh countdownText;
        private TextMesh countdownShadow;
        private Transform countdownBadge;
        private SpriteRenderer countdownBadgeBorder;
        private StageEditorObject marker;
        private StageGimmickSyncManager syncManager;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private float crackDuration = 0.4f;
        private float elapsed;
        private bool triggered;
        private bool broken;
        private bool requestSent;

        public string ObjectId => marker != null ? marker.objectId : string.Empty;
        public bool HasTriggered => triggered;

        private void Awake()
        {
            marker = GetComponent<StageEditorObject>();
            floorColliders = GetComponents<Collider2D>();
            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            crackDuration = marker != null && marker.actionStrength > 0f
                ? Mathf.Clamp(marker.actionStrength, 0.1f, 5f)
                : 0.4f;
            Vector2 floorSize = box != null ? box.size : new Vector2(3f, 0.5f);
            cracks = CreateCracks(floorSize);
            CreateCountdownDisplay(floorSize);
            SetCrackAlpha(0.2f);
            RefreshCountdownText();
        }

        private void Start()
        {
            syncManager = GetComponentInParent<StageGimmickSyncManager>();
        }

        private void Update()
        {
            if (!triggered)
            {
                return;
            }

            elapsed = Mathf.Min(elapsed + Time.deltaTime, TotalDuration);
            ApplyProgress();

            if (!broken && elapsed >= crackDuration)
            {
                broken = true;
                SetCollidersEnabled(false);
                syncManager?.NotifyCrumblingFloorChanged(this);
            }

            if (elapsed >= TotalDuration)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (triggered || requestSent || collision == null)
            {
                return;
            }

            PlayerController2D player = collision.gameObject.GetComponentInParent<PlayerController2D>();
            Collider2D playerCollider = collision.collider;
            Collider2D floorCollider = floorColliders != null && floorColliders.Length > 0
                ? floorColliders[0]
                : null;
            if (player == null || playerCollider == null || floorCollider == null)
            {
                return;
            }

            Vector2 relative = playerCollider.bounds.center - floorCollider.bounds.center;
            if (Vector2.Dot(relative, transform.up) <= 0.02f)
            {
                return;
            }

            requestSent = true;
            if (syncManager != null && syncManager.IsOnlineActive)
            {
                syncManager.RequestCrumblingFloor(ObjectId);
            }
            else
            {
                TriggerAuthoritatively();
            }
        }

        public void TriggerAuthoritatively()
        {
            if (triggered)
            {
                return;
            }

            triggered = true;
            requestSent = true;
            elapsed = 0f;
            ApplyProgress();
        }

        public OnlineCrumblingFloorState CreateNetworkState()
        {
            return new OnlineCrumblingFloorState
            {
                Triggered = triggered,
                Broken = broken,
                Progress = Mathf.Clamp01(elapsed / TotalDuration)
            };
        }

        public void ApplyNetworkState(OnlineCrumblingFloorState state)
        {
            if (state == null || !state.Triggered)
            {
                return;
            }

            if (!triggered)
            {
                TriggerAuthoritatively();
            }

            elapsed = Mathf.Max(elapsed, Mathf.Clamp01(state.Progress) * TotalDuration);
            broken |= state.Broken || elapsed >= crackDuration;
            if (broken)
            {
                SetCollidersEnabled(false);
            }
            ApplyProgress();

            if (elapsed >= TotalDuration)
            {
                gameObject.SetActive(false);
            }
        }

        private float TotalDuration => crackDuration + FallDuration;

        private void ApplyProgress()
        {
            RefreshCountdownText();
            float crackProgress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, crackDuration));
            SetCrackAlpha(Mathf.Lerp(0.2f, 1f, crackProgress));

            if (elapsed < crackDuration)
            {
                float shake = Mathf.Sin(elapsed * 75f) * 0.025f * crackProgress;
                transform.localPosition = originalLocalPosition + Vector3.right * shake;
                transform.localRotation = originalLocalRotation;
                return;
            }

            float fall = Mathf.Clamp01((elapsed - crackDuration) / FallDuration);
            transform.localPosition = originalLocalPosition + Vector3.down * (fall * fall * 2.8f);
            transform.localRotation = originalLocalRotation * Quaternion.Euler(0f, 0f, fall * 9f);
        }

        private void RefreshCountdownText()
        {
            if (countdownText == null || countdownShadow == null)
            {
                return;
            }

            float remaining = triggered
                ? Mathf.Max(0f, crackDuration - elapsed)
                : crackDuration;
            float displayed = Mathf.Ceil(remaining * 10f - 0.001f) / 10f;
            string value = displayed.ToString("0.0", CultureInfo.InvariantCulture);
            countdownText.text = value;
            countdownShadow.text = value;

            float ratio = Mathf.Clamp01(remaining / Mathf.Max(0.1f, crackDuration));
            Color accent = !triggered
                ? new Color(0.08f, 0.46f, 0.88f, 1f)
                : ratio > 0.5f
                    ? new Color(0.98f, 0.55f, 0.08f, 1f)
                    : new Color(0.9f, 0.12f, 0.08f, 1f);
            countdownText.color = accent;
            if (countdownBadgeBorder != null)
            {
                countdownBadgeBorder.color = accent;
            }
            if (countdownBadge != null)
            {
                float pulse = triggered && remaining > 0f && remaining <= 1f
                    ? 1f + Mathf.Sin(Time.time * 18f) * 0.045f
                    : 1f;
                countdownBadge.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        private void CreateCountdownDisplay(Vector2 size)
        {
            float badgeHeight = Mathf.Clamp(size.y * 0.82f, 0.36f, 0.58f);
            float badgeWidth = Mathf.Clamp(badgeHeight * 2.35f, 0.86f, Mathf.Max(0.86f, size.x * 0.72f));

            GameObject badgeObject = new GameObject("Crumbling Timer Badge");
            badgeObject.transform.SetParent(transform, false);
            badgeObject.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            countdownBadge = badgeObject.transform;

            countdownBadgeBorder = CreateBadgeLayer(
                countdownBadge,
                "Timer Border",
                new Color(0.08f, 0.46f, 0.88f, 1f),
                new Vector2(badgeWidth, badgeHeight),
                21,
                new Vector3(0.018f, -0.018f, 0f));
            CreateBadgeLayer(
                countdownBadge,
                "Timer Paper",
                new Color(1f, 0.98f, 0.84f, 0.98f),
                new Vector2(badgeWidth * 0.91f, badgeHeight * 0.82f),
                22,
                Vector3.zero);

            Font font = FindHandwrittenFont();
            float characterSize = Mathf.Clamp(badgeHeight * 0.135f, 0.05f, 0.078f);
            float numberY = badgeHeight * 0.055f;
            countdownShadow = CreateCountdownText(
                countdownBadge,
                "Timer Number Shadow",
                font,
                characterSize,
                new Color(0.16f, 0.11f, 0.08f, 0.48f),
                23,
                new Vector3(0.014f, numberY - 0.014f, 0f));
            countdownText = CreateCountdownText(
                countdownBadge,
                "Timer Number",
                font,
                characterSize,
                new Color(0.08f, 0.46f, 0.88f, 1f),
                24,
                new Vector3(0f, numberY, 0f));
        }

        private static TextMesh CreateCountdownText(
            Transform parent,
            string objectName,
            Font font,
            float characterSize,
            Color color,
            int sortingOrder,
            Vector3 localPosition)
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

            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (font != null)
            {
                renderer.sharedMaterial = font.material;
            }

            return text;
        }

        private static SpriteRenderer CreateBadgeLayer(
            Transform parent,
            string objectName,
            Color color,
            Vector2 size,
            int sortingOrder,
            Vector3 localPosition)
        {
            GameObject layer = new GameObject(objectName);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = localPosition;
            layer.transform.localScale = new Vector3(size.x * 0.5f, size.y, 1f);
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCountdownBadgeSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite GetCountdownBadgeSprite()
        {
            if (countdownBadgeSprite != null)
            {
                return countdownBadgeSprite;
            }

            const int width = 128;
            const int height = 64;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Crumbling Timer Capsule",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[width * height];
            float centerX = (width - 1) * 0.5f;
            float centerY = (height - 1) * 0.5f;
            float radius = centerY - 1f;
            float straightHalfWidth = centerX - radius;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - centerX) - straightHalfWidth);
                    float dy = y - centerY;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.75f - distance) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            countdownBadgeSprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                height);
            countdownBadgeSprite.name = "Crumbling Timer Capsule";
            return countdownBadgeSprite;
        }

        private static Font FindHandwrittenFont()
        {
            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                Font font = loadedFonts[i];
                if (font != null && font.name.IndexOf("Yomogi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }

            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback != null ? fallback : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void SetCollidersEnabled(bool value)
        {
            if (floorColliders == null)
            {
                return;
            }

            for (int i = 0; i < floorColliders.Length; i++)
            {
                if (floorColliders[i] != null)
                {
                    floorColliders[i].enabled = value;
                }
            }
        }

        private LineRenderer[] CreateCracks(Vector2 size)
        {
            LineRenderer existing = GetComponentInChildren<LineRenderer>();
            Material material = existing != null
                ? existing.sharedMaterial
                : new Material(Shader.Find("Sprites/Default"));
            LineRenderer[] result = new LineRenderer[3];
            for (int i = 0; i < result.Length; i++)
            {
                float x = Mathf.Lerp(-size.x * 0.3f, size.x * 0.3f, i / 2f);
                GameObject crackObject = new GameObject("Crumbling Crack " + (i + 1));
                crackObject.transform.SetParent(transform, false);
                LineRenderer line = crackObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 4;
                line.SetPosition(0, new Vector3(x - 0.08f, size.y * 0.48f, -0.08f));
                line.SetPosition(1, new Vector3(x + 0.06f, size.y * 0.14f, -0.08f));
                line.SetPosition(2, new Vector3(x - 0.04f, -size.y * 0.12f, -0.08f));
                line.SetPosition(3, new Vector3(x + 0.08f, -size.y * 0.48f, -0.08f));
                line.startWidth = 0.035f;
                line.endWidth = 0.035f;
                line.numCapVertices = 4;
                line.material = material;
                line.sortingOrder = 20;
                result[i] = line;
            }
            return result;
        }

        private void SetCrackAlpha(float alpha)
        {
            if (cracks == null)
            {
                return;
            }

            Color color = new Color(0.36f, 0.12f, 0.08f, Mathf.Clamp01(alpha));
            for (int i = 0; i < cracks.Length; i++)
            {
                if (cracks[i] != null)
                {
                    cracks[i].startColor = color;
                    cracks[i].endColor = color;
                }
            }
        }
    }
}
