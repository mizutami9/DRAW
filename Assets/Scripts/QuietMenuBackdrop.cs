using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    internal enum QuietMenuBackdropPreset
    {
        Title,
        StageSelect,
        Option,
        Draw,
        Multi
    }

    /// <summary>
    /// Uses the original scrapbook collage at a softened opacity, then adds a small
    /// cast of moving crayon cut-outs and occasional paper-craft events. Decorations
    /// stay behind the controls and never receive raycasts.
    /// </summary>
    internal static class QuietMenuBackdrop
    {
        internal static void Apply(RectTransform panel, string rootName, QuietMenuBackdropPreset preset)
        {
            if (panel == null)
            {
                return;
            }

            Transform found = panel.Find(rootName);
            RectTransform root;
            if (found == null)
            {
                GameObject obj = new GameObject(rootName, typeof(RectTransform));
                obj.transform.SetParent(panel, false);
                root = obj.GetComponent<RectTransform>();
            }
            else
            {
                root = found as RectTransform;
            }

            if (root == null)
            {
                return;
            }

            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            root.gameObject.SetActive(true);
            root.SetAsFirstSibling();

            Image oldFrame = root.GetComponent<Image>();
            if (oldFrame == null)
            {
                oldFrame = root.gameObject.AddComponent<Image>();
            }
            ConfigureScrapbookFrame(oldFrame, preset);

            switch (preset)
            {
                case QuietMenuBackdropPreset.Title:
                    ConfigureMark(root, 0, new Vector2(0.145f, 0.265f), new Vector2(120f, 42f),
                        new Color(0.96f, 0.36f, 0.24f, 0.29f), 2, 0f);
                    SetMarkActive(root, 1, false);
                    ConfigureSprite(root, 0, "paper-airplane", new Vector2(0.18f, 0.3f), Vector2.zero,
                        new Vector2(88f, 65f), 0.84f, -7f, new Vector2(16f, 5f), 2.3f, 0.025f, 0.28f, 0.1f);
                    ConfigureSprite(root, 1, "hot-air-balloon", new Vector2(0.89f, 0.25f), Vector2.zero,
                        new Vector2(78f, 96f), 0.68f, 3f, new Vector2(7f, 18f), 3.5f, 0.035f, 0.105f, 1.6f);
                    ConfigureSprite(root, 2, "butterfly", new Vector2(0.105f, 0.77f), Vector2.zero,
                        new Vector2(64f, 52f), 0.7f, -8f, new Vector2(22f, 10f), 8f, 0.07f, 0.34f, 3.1f);
                    ConfigureSprite(root, 3, "star", new Vector2(0.91f, 0.76f), Vector2.zero,
                        new Vector2(48f, 48f), 0.62f, 7f, new Vector2(6f, 9f), 15f, 0.1f, 0.22f, 4.8f);
                    break;

                case QuietMenuBackdropPreset.StageSelect:
                    SetMarkActive(root, 0, false);
                    SetMarkActive(root, 1, false);
                    ConfigureSprite(root, 0, "hot-air-balloon", new Vector2(0.925f, 0.2f), Vector2.zero,
                        new Vector2(68f, 86f), 0.62f, 4f, new Vector2(6f, 15f), 3f, 0.03f, 0.1f, 0.7f);
                    ConfigureSprite(root, 1, "paper-airplane", new Vector2(0.075f, 0.15f), Vector2.zero,
                        new Vector2(72f, 54f), 0.66f, -8f, new Vector2(12f, 3f), 2f, 0.02f, 0.25f, 2f);
                    ConfigureSprite(root, 2, "butterfly", new Vector2(0.08f, 0.82f), Vector2.zero,
                        new Vector2(56f, 46f), 0.64f, -7f, new Vector2(18f, 9f), 8f, 0.065f, 0.31f, 3.7f);
                    ConfigureSprite(root, 3, "star", new Vector2(0.925f, 0.8f), Vector2.zero,
                        new Vector2(43f, 43f), 0.58f, 8f, new Vector2(5f, 8f), 14f, 0.09f, 0.2f, 5.2f);
                    break;

                case QuietMenuBackdropPreset.Option:
                    SetMarkActive(root, 0, false);
                    SetMarkActive(root, 1, false);
                    SetSpriteActive(root, 0, false);
                    SetSpriteActive(root, 1, false);
                    ConfigureSprite(root, 2, "star", new Vector2(0.93f, 0.15f), Vector2.zero,
                        new Vector2(42f, 42f), 0.64f, 5f, new Vector2(2f, 3f), 5f, 0.065f, 0.42f, 4.3f);
                    SetSpriteActive(root, 3, false);
                    break;

                case QuietMenuBackdropPreset.Draw:
                    SetMarkActive(root, 0, false);
                    SetMarkActive(root, 1, false);
                    SetSpriteActive(root, 0, false);
                    ConfigureSprite(root, 1, "star", new Vector2(0.045f, 0.76f), Vector2.zero,
                        new Vector2(44f, 44f), 0.62f, -4f, new Vector2(2f, 3f), 5f, 0.06f, 0.4f, 2.8f);
                    SetSpriteActive(root, 2, false);
                    SetSpriteActive(root, 3, false);
                    break;

                default:
                    SetMarkActive(root, 0, false);
                    SetMarkActive(root, 1, false);
                    ConfigureSprite(root, 0, "paper-airplane", new Vector2(0.06f, 0.76f), Vector2.zero,
                        new Vector2(70f, 52f), 0.64f, -8f, new Vector2(11f, 3f), 2f, 0.02f, 0.24f, 1.4f);
                    SetSpriteActive(root, 1, false);
                    SetSpriteActive(root, 2, false);
                    SetSpriteActive(root, 3, false);
                    break;
            }

            DoodleMenuEventAnimator eventAnimator = root.GetComponent<DoodleMenuEventAnimator>();
            bool useEvents = preset == QuietMenuBackdropPreset.Title
                || preset == QuietMenuBackdropPreset.StageSelect;
            if (useEvents)
            {
                if (eventAnimator == null) eventAnimator = root.gameObject.AddComponent<DoodleMenuEventAnimator>();
                eventAnimator.Configure(preset == QuietMenuBackdropPreset.StageSelect ? 6.5f : 0f);
                eventAnimator.enabled = true;
            }
            else if (eventAnimator != null)
            {
                eventAnimator.enabled = false;
            }
        }

        private static void ConfigureScrapbookFrame(Image image, QuietMenuBackdropPreset preset)
        {
            string resource;
            float alpha;
            switch (preset)
            {
                case QuietMenuBackdropPreset.Title:
                    resource = "title-scrapbook-frame-v2";
                    alpha = 0.84f;
                    break;
                case QuietMenuBackdropPreset.StageSelect:
                    resource = "stage-select-scrapbook-frame-v1";
                    alpha = 0.86f;
                    break;
                case QuietMenuBackdropPreset.Option:
                    resource = "option-scrapbook-frame-v1";
                    alpha = 0.82f;
                    break;
                case QuietMenuBackdropPreset.Draw:
                    resource = "draw-scrapbook-frame-v1";
                    alpha = 0.82f;
                    break;
                default:
                    resource = "multi-scrapbook-frame-v1";
                    alpha = 0.84f;
                    break;
            }

            Sprite sprite = Resources.Load<Sprite>("UI/" + resource);
            image.enabled = sprite != null;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
        }

        private static void ConfigureMark(
            RectTransform parent,
            int index,
            Vector2 anchor,
            Vector2 size,
            Color color,
            int style,
            float phase)
        {
            string name = "AmbientCrayonStroke" + index;
            Transform found = parent.Find(name);
            RectTransform rect;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
            }
            else
            {
                rect = found as RectTransform;
            }

            if (rect == null)
            {
                return;
            }

            rect.gameObject.SetActive(true);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            DoodleStrokeLoop motion = rect.GetComponent<DoodleStrokeLoop>();
            if (motion == null)
            {
                motion = rect.gameObject.AddComponent<DoodleStrokeLoop>();
            }
            motion.Configure(color, style, phase);
        }

        private static void SetMarkActive(RectTransform parent, int index, bool active)
        {
            Transform mark = parent.Find("AmbientCrayonStroke" + index);
            if (mark != null)
            {
                mark.gameObject.SetActive(active);
            }
        }

        private static void ConfigureSprite(
            RectTransform parent,
            int index,
            string resourceName,
            Vector2 anchor,
            Vector2 position,
            Vector2 size,
            float alpha,
            float rotation,
            Vector2 travel,
            float rotationAmplitude,
            float scaleAmplitude,
            float speed,
            float phase)
        {
            string name = "AmbientCrayonCutout" + index;
            Transform found = parent.Find(name);
            RectTransform rect;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
            }
            else rect = found as RectTransform;
            if (rect == null) return;

            Image image = rect.GetComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>("StageDecorations/CrayonSet/" + resourceName);
            rect.gameObject.SetActive(sprite != null);
            if (sprite == null) return;

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = Vector3.one;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;

            DoodleAmbientSpriteMotion motion = rect.GetComponent<DoodleAmbientSpriteMotion>();
            if (motion == null)
            {
                motion = rect.gameObject.AddComponent<DoodleAmbientSpriteMotion>();
            }
            motion.Configure(position, rotation, travel, rotationAmplitude, scaleAmplitude, speed, phase);
        }

        private static void SetSpriteActive(RectTransform parent, int index, bool active)
        {
            Transform sprite = parent.Find("AmbientCrayonCutout" + index);
            if (sprite != null)
            {
                sprite.gameObject.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Deterministic, allocation-free loop that draws a few short crayon segments,
    /// holds them briefly, then erases them.  It owns only dedicated background art.
    /// </summary>
    internal sealed class DoodleStrokeLoop : MonoBehaviour
    {
        private const int SegmentCount = 4;
        private const float CycleSeconds = 5.1f;
        private const float DrawSeconds = 1.2f;
        private const float HoldSeconds = 2.9f;
        private const float FadeSeconds = 0.75f;

        private readonly RectTransform[] segments = new RectTransform[SegmentCount];
        private CanvasGroup canvasGroup;
        private RectTransform root;
        private float phase;
        private float cycleStartedAt;
        private bool configured;

        internal void Configure(Color color, int style, float animationPhase)
        {
            root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.ignoreParentGroups = false;

            phase = animationPhase;
            Vector2[] starts = new Vector2[SegmentCount];
            Vector2[] ends = new Vector2[SegmentCount];
            BuildShape(style, root.sizeDelta, starts, ends);
            float thickness = Mathf.Clamp(Mathf.Min(root.sizeDelta.x, root.sizeDelta.y) * 0.065f, 2.2f, 4.2f);

            for (int i = 0; i < SegmentCount; i++)
            {
                RectTransform segment = EnsureSegment(i);
                Vector2 delta = ends[i] - starts[i];
                segment.anchorMin = new Vector2(0.5f, 0.5f);
                segment.anchorMax = new Vector2(0.5f, 0.5f);
                segment.pivot = new Vector2(0f, 0.5f);
                segment.anchoredPosition = starts[i];
                segment.sizeDelta = new Vector2(Mathf.Max(1f, delta.magnitude), thickness * (1f + i * 0.04f));
                segment.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

                Image image = segment.GetComponent<Image>();
                image.sprite = DoodleRuntimeAssets.SquareSprite;
                image.type = Image.Type.Simple;
                image.color = new Color(color.r, color.g, color.b,
                    Mathf.Clamp01(color.a * (0.88f + i * 0.04f)));
                image.raycastTarget = false;
            }

            cycleStartedAt = Time.unscaledTime;
            configured = true;
            Apply(0f);
        }

        private RectTransform EnsureSegment(int index)
        {
            string name = "AnimatedStrokeSegment" + index;
            Transform found = transform.Find(name);
            RectTransform rect;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(transform, false);
                rect = obj.GetComponent<RectTransform>();
            }
            else
            {
                rect = found as RectTransform;
            }
            segments[index] = rect;
            return rect;
        }

        private static void BuildShape(int style, Vector2 size, Vector2[] starts, Vector2[] ends)
        {
            float w = Mathf.Max(40f, size.x);
            float h = Mathf.Max(28f, size.y);
            if (style == 1)
            {
                starts[0] = new Vector2(-0.08f * w, 0.02f * h);
                ends[0] = new Vector2(-0.34f * w, 0.33f * h);
                starts[1] = new Vector2(0.02f * w, 0.1f * h);
                ends[1] = new Vector2(0.29f * w, 0.38f * h);
                starts[2] = new Vector2(-0.08f * w, -0.02f * h);
                ends[2] = new Vector2(-0.32f * w, -0.3f * h);
                starts[3] = new Vector2(0.03f * w, -0.09f * h);
                ends[3] = new Vector2(0.31f * w, -0.34f * h);
                return;
            }

            if (style == 2)
            {
                starts[0] = new Vector2(-0.47f * w, -0.18f * h);
                ends[0] = new Vector2(-0.28f * w, -0.02f * h);
                starts[1] = new Vector2(-0.18f * w, 0.07f * h);
                ends[1] = new Vector2(-0.01f * w, 0.18f * h);
                starts[2] = new Vector2(0.09f * w, 0.19f * h);
                ends[2] = new Vector2(0.25f * w, 0.08f * h);
                starts[3] = new Vector2(0.34f * w, 0.01f * h);
                ends[3] = new Vector2(0.47f * w, -0.17f * h);
                return;
            }

            starts[0] = new Vector2(-0.47f * w, -0.12f * h);
            ends[0] = new Vector2(-0.22f * w, 0.14f * h);
            starts[1] = ends[0];
            ends[1] = new Vector2(0.02f * w, -0.08f * h);
            starts[2] = ends[1];
            ends[2] = new Vector2(0.25f * w, 0.16f * h);
            starts[3] = ends[2];
            ends[3] = new Vector2(0.47f * w, -0.04f * h);
        }

        private void OnEnable()
        {
            cycleStartedAt = Time.unscaledTime;
            if (configured)
            {
                Apply(0f);
            }
        }

        private void OnDisable()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (configured)
            {
                Apply(Time.unscaledTime - cycleStartedAt);
            }
        }

        private void Apply(float time)
        {
            float local = Mathf.Repeat(time + phase, CycleSeconds);
            float fadeStart = DrawSeconds + HoldSeconds;
            float alpha = local < fadeStart
                ? 1f
                : local < fadeStart + FadeSeconds
                    ? 1f - Mathf.SmoothStep(0f, 1f, (local - fadeStart) / FadeSeconds)
                    : 0f;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                RectTransform segment = segments[i];
                if (segment == null)
                {
                    continue;
                }

                float segmentStart = i * 0.19f;
                float progress = Mathf.Clamp01((local - segmentStart) / 0.62f);
                progress = Mathf.SmoothStep(0f, 1f, progress);
                segment.localScale = new Vector3(progress, 1f, 1f);
            }
        }
    }

    /// <summary>
    /// Gentle but always-visible movement for decorative crayon cut-outs.  Rest
    /// coordinates are supplied explicitly so repeated theme passes cannot drift.
    /// </summary>
    internal sealed class DoodleAmbientSpriteMotion : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 restPosition;
        private float restRotation;
        private Vector2 travel;
        private float rotationAmplitude;
        private float scaleAmplitude;
        private float speed;
        private float phase;
        private bool configured;

        internal void Configure(
            Vector2 position,
            float rotation,
            Vector2 movement,
            float angleAmplitude,
            float sizeAmplitude,
            float cyclesPerSecond,
            float animationPhase)
        {
            rect = transform as RectTransform;
            if (rect == null) return;

            restPosition = position;
            restRotation = rotation;
            travel = movement;
            rotationAmplitude = angleAmplitude;
            scaleAmplitude = sizeAmplitude;
            speed = Mathf.Max(0.05f, cyclesPerSecond);
            phase = animationPhase;
            configured = true;
            Apply(Time.unscaledTime);
        }

        private void OnEnable()
        {
            if (configured) Apply(Time.unscaledTime);
        }

        private void Update()
        {
            if (configured) Apply(Time.unscaledTime);
        }

        private void Apply(float time)
        {
            float angle = (time * speed + phase) * Mathf.PI * 2f;
            float horizontal = Mathf.Sin(angle);
            float vertical = Mathf.Sin(angle * 0.73f + 1.17f);
            float turn = Mathf.Sin(angle * 0.87f + 0.48f);
            float pulse = Mathf.Sin(angle * 1.13f + 2.04f);

            rect.anchoredPosition = restPosition + new Vector2(
                travel.x * horizontal,
                travel.y * vertical);
            rect.localRotation = Quaternion.Euler(0f, 0f,
                restRotation + rotationAmplitude * turn);
            rect.localScale = Vector3.one * (1f + scaleAmplitude * pulse);
        }
    }

    /// <summary>
    /// Alternates two occasional menu events: a bumpy flock of paper airplanes
    /// and a shower of small origami-like flowers and colored paper diamonds.
    /// Everything is created once and animated with unscaled time.
    /// </summary>
    internal sealed class DoodleMenuEventAnimator : MonoBehaviour
    {
        private const int PlaneCount = 9;
        private const int ConfettiCount = 24;
        private const float CycleSeconds = 22f;
        private const float EventStart = 6f;
        private const float EventDuration = 7.2f;

        private readonly RectTransform[] planes = new RectTransform[PlaneCount];
        private readonly Image[] planeImages = new Image[PlaneCount];
        private readonly RectTransform[] confetti = new RectTransform[ConfettiCount];
        private readonly Image[] confettiImages = new Image[ConfettiCount];
        private RectTransform root;
        private RectTransform layer;
        private float startedAt;
        private float phaseOffset;
        private bool configured;

        internal void Configure(float offset)
        {
            root = transform as RectTransform;
            if (root == null) return;
            phaseOffset = offset;
            EnsureLayer();
            EnsurePlanes();
            EnsureConfetti();
            startedAt = Time.unscaledTime;
            configured = true;
            Apply(phaseOffset);
        }

        private void EnsureLayer()
        {
            Transform found = transform.Find("AmbientPaperCraftEvents");
            if (found == null)
            {
                GameObject obj = new GameObject("AmbientPaperCraftEvents", typeof(RectTransform));
                obj.transform.SetParent(transform, false);
                layer = obj.GetComponent<RectTransform>();
            }
            else layer = found as RectTransform;
            if (layer == null) return;
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.pivot = new Vector2(0.5f, 0.5f);
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            layer.localPosition = Vector3.zero;
            layer.localRotation = Quaternion.identity;
            layer.localScale = Vector3.one;
            layer.SetAsLastSibling();
        }

        private void EnsurePlanes()
        {
            if (layer == null) return;
            Sprite sprite = Resources.Load<Sprite>("StageDecorations/CrayonSet/paper-airplane");
            for (int i = 0; i < planes.Length; i++)
            {
                string name = "PaperAirplaneFlock" + i;
                Transform found = layer.Find(name);
                RectTransform rect;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(layer, false);
                    rect = obj.GetComponent<RectTransform>();
                }
                else rect = found as RectTransform;
                if (rect == null) continue;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                float size = 38f + i % 4 * 7f;
                rect.sizeDelta = new Vector2(size * 1.34f, size);
                Image image = rect.GetComponent<Image>();
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                rect.gameObject.SetActive(sprite != null);
                planes[i] = rect;
                planeImages[i] = image;
            }
        }

        private void EnsureConfetti()
        {
            if (layer == null) return;
            Sprite flower = Resources.Load<Sprite>("StageDecorations/CrayonSet/flower");
            Sprite star = Resources.Load<Sprite>("StageDecorations/CrayonSet/star");
            Color[] paperColors =
            {
                new Color(1f, 0.28f, 0.24f),
                new Color(0.12f, 0.62f, 0.92f),
                new Color(1f, 0.72f, 0.08f),
                new Color(0.58f, 0.34f, 0.9f),
                new Color(0.18f, 0.72f, 0.46f)
            };
            for (int i = 0; i < confetti.Length; i++)
            {
                string name = "OrigamiFlowerConfetti" + i;
                Transform found = layer.Find(name);
                RectTransform rect;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(layer, false);
                    rect = obj.GetComponent<RectTransform>();
                }
                else rect = found as RectTransform;
                if (rect == null) continue;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                bool isFlower = i % 5 == 0;
                bool isStar = !isFlower && i % 7 == 0;
                float size = isFlower ? 31f + i % 3 * 5f : isStar ? 25f : 12f + i % 4 * 4f;
                rect.sizeDelta = isFlower || isStar
                    ? Vector2.one * size
                    : new Vector2(size, size * 0.58f);
                Image image = rect.GetComponent<Image>();
                image.sprite = isFlower ? flower : isStar ? star : DoodleRuntimeAssets.SquareSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = isFlower || isStar;
                image.raycastTarget = false;
                Color color = isFlower || isStar ? Color.white : paperColors[i % paperColors.Length];
                color.a = 0f;
                image.color = color;
                rect.gameObject.SetActive(image.sprite != null);
                confetti[i] = rect;
                confettiImages[i] = image;
            }
        }

        private void OnEnable()
        {
            startedAt = Time.unscaledTime;
            if (configured) Apply(phaseOffset);
        }

        private void OnDisable()
        {
            HideAll();
        }

        private void Update()
        {
            if (configured) Apply(Time.unscaledTime - startedAt + phaseOffset);
        }

        private void Apply(float time)
        {
            if (root == null || layer == null) return;
            int cycle = Mathf.FloorToInt(Mathf.Max(0f, time) / CycleSeconds);
            float local = Mathf.Repeat(Mathf.Max(0f, time), CycleSeconds);
            float eventTime = local - EventStart;
            bool active = eventTime >= 0f && eventTime <= EventDuration;
            bool airplaneEvent = cycle % 2 == 0;
            AnimatePlanes(active && airplaneEvent, eventTime);
            AnimateConfetti(active && !airplaneEvent, eventTime);
        }

        private void AnimatePlanes(bool active, float eventTime)
        {
            float width = Mathf.Max(960f, root.rect.width);
            float height = Mathf.Max(540f, root.rect.height);
            for (int i = 0; i < planes.Length; i++)
            {
                RectTransform rect = planes[i];
                Image image = planeImages[i];
                if (rect == null || image == null) continue;
                float delay = i * 0.19f;
                float progress = active
                    ? Mathf.Clamp01((eventTime - delay) / (EventDuration - 1.25f))
                    : 0f;
                float visible = active && eventTime >= delay
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.1f))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.88f) / 0.12f)))
                    : 0f;
                float lane = ((i * 5) % PlaneCount) / (float)(PlaneCount - 1);
                float arc = Mathf.Sin(progress * Mathf.PI * (1.1f + i % 3 * 0.22f));
                float ripple = Mathf.Sin(progress * Mathf.PI * 6f + i * 0.83f);
                rect.anchoredPosition = new Vector2(
                    Mathf.Lerp(-width * 0.62f, width * 0.62f, progress),
                    Mathf.Lerp(-height * 0.31f, height * 0.32f, lane) + arc * (32f + i % 4 * 11f) + ripple * 7f);
                rect.localRotation = Quaternion.Euler(0f, 0f,
                    -10f + arc * 15f + ripple * 4f);
                float pulse = 0.92f + Mathf.Sin(progress * Mathf.PI * 4f + i) * 0.08f;
                rect.localScale = Vector3.one * pulse;
                image.color = new Color(1f, 1f, 1f, visible * (0.58f + i % 3 * 0.1f));
            }
        }

        private void AnimateConfetti(bool active, float eventTime)
        {
            float width = Mathf.Max(960f, root.rect.width);
            float height = Mathf.Max(540f, root.rect.height);
            for (int i = 0; i < confetti.Length; i++)
            {
                RectTransform rect = confetti[i];
                Image image = confettiImages[i];
                if (rect == null || image == null) continue;
                float delay = (i % 6) * 0.16f + (i / 6) * 0.08f;
                float progress = active
                    ? Mathf.Clamp01((eventTime - delay) / (EventDuration - 0.85f))
                    : 0f;
                float visible = active && eventTime >= delay
                    ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.08f))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.91f) / 0.09f)))
                    : 0f;
                float column = ((i * 37 + 11) % 101) / 100f;
                float sway = Mathf.Sin(progress * Mathf.PI * (5f + i % 4) + i * 1.37f);
                rect.anchoredPosition = new Vector2(
                    Mathf.Lerp(-width * 0.48f, width * 0.48f, column) + sway * (18f + i % 5 * 5f),
                    Mathf.Lerp(height * 0.58f, -height * 0.58f, progress));
                rect.localRotation = Quaternion.Euler(0f, 0f,
                    i * 29f + progress * (260f + i % 5 * 55f));
                float flutter = 0.68f + Mathf.Abs(Mathf.Sin(progress * Mathf.PI * 8f + i)) * 0.42f;
                rect.localScale = new Vector3(flutter, 1f, 1f);
                Color color = image.color;
                color.a = visible * (i % 5 == 0 ? 0.72f : 0.82f);
                image.color = color;
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < planeImages.Length; i++)
                if (planeImages[i] != null)
                {
                    Color color = planeImages[i].color;
                    color.a = 0f;
                    planeImages[i].color = color;
                }
            for (int i = 0; i < confettiImages.Length; i++)
                if (confettiImages[i] != null)
                {
                    Color color = confettiImages[i].color;
                    color.a = 0f;
                    confettiImages[i].color = color;
                }
        }
    }

    /// <summary>
    /// Keeps the title mark alive after its one-shot reveal without returning to a
    /// generic floating-logo animation. A pencil repeatedly redraws the underline,
    /// while two crayon stars react at the end of the stroke.
    /// </summary>
    internal sealed class TitleLogoAccentAnimator : MonoBehaviour
    {
        private const float CycleSeconds = 4.8f;
        private const float DrawSeconds = 1.45f;
        private readonly Image[] underline = new Image[3];
        private RectTransform root;
        private RectTransform pencil;
        private Image pencilImage;
        private RectTransform leftStar;
        private Image leftStarImage;
        private RectTransform rightStar;
        private Image rightStarImage;
        private float cycleStartedAt;
        private bool configured;

        internal void Configure()
        {
            root = transform as RectTransform;
            if (root == null) return;

            underline[0] = EnsureUnderline("LogoUnderlineBlue", new Vector2(-1f, -82f),
                new Vector2(420f, 6f), new Color(0.08f, 0.43f, 0.9f, 0.82f), -1.2f);
            underline[1] = EnsureUnderline("LogoUnderlineYellow", new Vector2(10f, -89f),
                new Vector2(382f, 4f), new Color(1f, 0.66f, 0.05f, 0.7f), 0.8f);
            underline[2] = EnsureUnderline("LogoUnderlineCoral", new Vector2(4f, -94f),
                new Vector2(306f, 3f), new Color(1f, 0.29f, 0.25f, 0.58f), -0.5f);

            pencil = EnsureCutout("LogoDrawingPencil", "pencil", new Vector2(48f, 48f), out pencilImage);
            leftStar = EnsureCutout("LogoSparkLeft", "star", new Vector2(31f, 31f), out leftStarImage);
            rightStar = EnsureCutout("LogoSparkRight", "star", new Vector2(37f, 37f), out rightStarImage);
            if (leftStar != null) leftStar.anchoredPosition = new Vector2(-292f, -72f);
            if (rightStar != null) rightStar.anchoredPosition = new Vector2(292f, 72f);

            cycleStartedAt = Time.unscaledTime;
            configured = true;
            Apply(0f);
        }

        private Image EnsureUnderline(string name, Vector2 position, Vector2 size, Color color, float rotation)
        {
            Transform found = transform.Find(name);
            RectTransform rect;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(transform, false);
                rect = obj.GetComponent<RectTransform>();
            }
            else rect = found as RectTransform;
            if (rect == null) return null;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position - new Vector2(size.x * 0.5f, 0f);
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.localScale = Vector3.one;
            Image image = rect.GetComponent<Image>();
            image.sprite = DoodleRuntimeAssets.SquareSprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform EnsureCutout(string name, string resourceName, Vector2 size, out Image image)
        {
            Transform found = transform.Find(name);
            RectTransform rect;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(transform, false);
                rect = obj.GetComponent<RectTransform>();
            }
            else rect = found as RectTransform;
            image = rect != null ? rect.GetComponent<Image>() : null;
            if (rect == null || image == null) return null;

            image.sprite = Resources.Load<Sprite>("StageDecorations/CrayonSet/" + resourceName);
            rect.gameObject.SetActive(image.sprite != null);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        private void OnEnable()
        {
            cycleStartedAt = Time.unscaledTime;
            if (configured) Apply(0f);
        }

        private void Update()
        {
            if (configured) Apply(Time.unscaledTime - cycleStartedAt);
        }

        private void Apply(float time)
        {
            float local = Mathf.Repeat(time, CycleSeconds);
            float draw = Mathf.Clamp01(local / DrawSeconds);
            draw = Mathf.SmoothStep(0f, 1f, draw);
            float fade = local < 4.15f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (local - 4.15f) / 0.65f);

            for (int i = 0; i < underline.Length; i++)
            {
                Image line = underline[i];
                if (line == null) continue;
                float delayed = Mathf.Clamp01((draw - i * 0.14f) / Mathf.Max(0.01f, 1f - i * 0.14f));
                line.fillAmount = delayed;
                Color color = line.color;
                color.a = (i == 0 ? 0.82f : i == 1 ? 0.7f : 0.58f) * fade;
                line.color = color;
            }

            if (pencil != null && pencilImage != null)
            {
                float pencilProgress = Mathf.Clamp01(local / DrawSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, pencilProgress);
                pencil.anchoredPosition = new Vector2(Mathf.Lerp(-218f, 220f, eased),
                    -74f + Mathf.Sin(eased * Mathf.PI * 3f) * 3f);
                pencil.localRotation = Quaternion.Euler(0f, 0f, -42f + Mathf.Sin(eased * Mathf.PI) * 5f);
                pencil.localScale = Vector3.one * (0.9f + Mathf.Sin(eased * Mathf.PI) * 0.08f);
                Color pencilColor = Color.white;
                pencilColor.a = local < DrawSeconds
                    ? Mathf.Clamp01(local / 0.15f)
                    : Mathf.Clamp01(1f - (local - DrawSeconds) / 0.3f);
                pencilImage.color = pencilColor;
            }

            AnimateStar(leftStar, leftStarImage, time, 0.15f, -7f, 0.76f);
            AnimateStar(rightStar, rightStarImage, time, 0.65f, 8f, 0.9f);
        }

        private static void AnimateStar(RectTransform star, Image image, float time,
            float phase, float baseRotation, float alpha)
        {
            if (star == null || image == null) return;
            float wave = 0.5f + 0.5f * Mathf.Sin((time * 0.72f + phase) * Mathf.PI * 2f);
            float pulse = Mathf.SmoothStep(0f, 1f, wave);
            star.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.17f, pulse);
            star.localRotation = Quaternion.Euler(0f, 0f,
                baseRotation + Mathf.Sin((time * 0.43f + phase) * Mathf.PI * 2f) * 9f);
            image.color = new Color(1f, 1f, 1f, Mathf.Lerp(alpha * 0.58f, alpha, pulse));
        }
    }
}
