using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class StageCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Color normalColor = new Color(0.98f, 0.94f, 0.82f, 0.95f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.42f, 0.98f);
        [SerializeField] private float lift = 4f;
        [SerializeField] private float scale = 1.04f;
        [SerializeField] private GameObject selectionScribble;
        [SerializeField] private float scribbleDrawDuration = 0.28f;

        private RectTransform rectTransform;
        private Vector2 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private RectTransform[] scribbleLines;
        private bool highlighted;
        private float scribbleTime;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            rectTransform.localRotation = Quaternion.identity;
            ApplyTitleNoteShape();
            basePosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
            baseRotation = rectTransform != null ? rectTransform.localRotation : Quaternion.identity;
            baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            EnsureTitleStickyNoteDecoration();
            Apply(false);
        }

        private void Update()
        {
            if (!highlighted || selectionScribble == null || scribbleLines == null || scribbleLines.Length == 0)
            {
                return;
            }

            scribbleTime += Time.unscaledDeltaTime;
            float progress = scribbleDrawDuration <= 0f ? 1f : Mathf.Clamp01(scribbleTime / scribbleDrawDuration);
            ApplyScribbleProgress(progress);
        }

        private void OnEnable()
        {
            ApplyTitleNoteShape();
            EnsureTitleStickyNoteDecoration();
            if (rectTransform != null)
            {
                rectTransform.localRotation = Quaternion.identity;
                basePosition = rectTransform.anchoredPosition;
                baseRotation = Quaternion.identity;
                baseScale = rectTransform.localScale;
            }

            Apply(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Apply(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Apply(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Apply(false);
        }

        private void Apply(bool value)
        {
            highlighted = value;
            if (targetImage != null)
            {
                targetImage.color = highlighted ? hoverColor : normalColor;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = basePosition + (highlighted ? new Vector2(0f, lift) : Vector2.zero);
                rectTransform.localScale = highlighted ? baseScale * scale : baseScale;
                rectTransform.localRotation = Quaternion.identity;
            }

            if (selectionScribble != null)
            {
                selectionScribble.SetActive(highlighted);
                scribbleTime = 0f;
                ApplyScribbleProgress(highlighted ? 0f : 1f);
            }
        }

        private void EnsureTitleStickyNoteDecoration()
        {
            if (rectTransform == null || !UsesStickyNoteStyle())
            {
                return;
            }

            Shadow shadow = GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.12f, 0.09f, 0.04f, 0.22f);
            shadow.effectDistance = new Vector2(5f, -6f);

            HideDecoration("CrayonFill");
            HideDecoration("MaskingTape");
            HideDecoration("StickyNoteBoldFrame");
            HideDecoration("SelectionScribble");
            selectionScribble = null;

            Outline outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.2f, 0.14f, 0.08f, 0.62f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            Transform label = transform.Find("Label");
            if (label != null)
            {
                label.SetAsLastSibling();
            }
        }

        private void HideDecoration(string childName)
        {
            Transform child = transform.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void ApplyTitleNoteShape()
        {
            if (rectTransform == null || !gameObject.name.StartsWith("Title", System.StringComparison.Ordinal))
            {
                return;
            }

            if (gameObject.name.Contains("Single"))
            {
                rectTransform.sizeDelta = new Vector2(146f, 61f);
            }
            else if (gameObject.name.Contains("Multi"))
            {
                rectTransform.sizeDelta = new Vector2(136f, 57f);
            }
            else if (gameObject.name.Contains("Draw"))
            {
                rectTransform.sizeDelta = new Vector2(142f, 63f);
            }
            else if (gameObject.name.Contains("Option"))
            {
                rectTransform.sizeDelta = new Vector2(152f, 58f);
            }
            else if (gameObject.name.Contains("Exit"))
            {
                rectTransform.sizeDelta = new Vector2(128f, 56f);
            }
        }

        private bool UsesStickyNoteStyle()
        {
            return gameObject.name.StartsWith("Title", System.StringComparison.Ordinal)
                || gameObject.name.StartsWith("Stage_", System.StringComparison.Ordinal)
                || gameObject.name.StartsWith("StageSelect", System.StringComparison.Ordinal);
        }

        private void CreateStickyNoteBoldFrame()
        {
            GameObject root = new GameObject("StickyNoteBoldFrame", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(4f, 4f);
            rootRect.offsetMax = new Vector2(-4f, -4f);

            float halfWidth = rectTransform.sizeDelta.x * 0.5f - 8f;
            float halfHeight = rectTransform.sizeDelta.y * 0.5f - 8f;
            Color outline = new Color(0.2f, 0.14f, 0.08f, 0.62f);
            CreateLine(root.transform, new Vector2(-halfWidth, halfHeight - 1f), new Vector2(halfWidth - 2f, halfHeight + 1f), 4.2f, outline, "StickyFrameLine");
            CreateLine(root.transform, new Vector2(halfWidth - 1f, halfHeight - 2f), new Vector2(halfWidth + 1f, -halfHeight + 2f), 4f, outline, "StickyFrameLine");
            CreateLine(root.transform, new Vector2(halfWidth - 3f, -halfHeight), new Vector2(-halfWidth + 2f, -halfHeight - 1f), 4.2f, outline, "StickyFrameLine");
            CreateLine(root.transform, new Vector2(-halfWidth + 1f, -halfHeight + 3f), new Vector2(-halfWidth - 1f, halfHeight - 3f), 4f, outline, "StickyFrameLine");
        }

        private void CreateCrayonFill()
        {
            GameObject root = new GameObject("CrayonFill", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(9f, 7f);
            rootRect.offsetMax = new Vector2(-9f, -7f);

            Color[] colors =
            {
                new Color(1f, 1f, 1f, 0.16f),
                new Color(0.95f, 0.78f, 0.32f, 0.15f),
                new Color(0.35f, 0.45f, 1f, 0.08f)
            };

            for (int i = 0; i < 7; i++)
            {
                float y = Mathf.Lerp(-18f, 18f, i / 6f) + Mathf.Sin(i * 1.7f) * 2f;
                float inset = 8f + (i % 3) * 3f;
                CreateLine(root.transform, new Vector2(-54f + inset, y), new Vector2(54f - inset, y + Mathf.Sin(i * 2.1f) * 3f), 5f + (i % 2), colors[i % colors.Length], "CrayonStroke");
            }
        }

        private void CreateMaskingTape()
        {
            GameObject tape = new GameObject("MaskingTape", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tape.transform.SetParent(transform, false);
            Image image = tape.GetComponent<Image>();
            image.color = new Color(1f, 0.9f, 0.56f, 0.48f);
            image.raycastTarget = false;

            RectTransform tapeRect = tape.GetComponent<RectTransform>();
            tapeRect.anchorMin = new Vector2(0.5f, 1f);
            tapeRect.anchorMax = new Vector2(0.5f, 1f);
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.anchoredPosition = new Vector2(GetTapeOffset(), -2f);
            tapeRect.sizeDelta = new Vector2(54f, 15f);
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, GetTapeRotation());

            Color edge = new Color(0.48f, 0.36f, 0.18f, 0.22f);
            CreateLine(tape.transform, new Vector2(-25f, 6f), new Vector2(25f, 5f), 1f, edge, "TapeEdge");
            CreateLine(tape.transform, new Vector2(-25f, -6f), new Vector2(25f, -5f), 1f, edge, "TapeEdge");
        }

        private float GetTapeOffset()
        {
            if (gameObject.name.Contains("Single"))
            {
                return -26f;
            }

            if (gameObject.name.Contains("Draw"))
            {
                return 22f;
            }

            if (gameObject.name.Contains("Exit"))
            {
                return -18f;
            }

            return 0f;
        }

        private float GetTapeRotation()
        {
            if (gameObject.name.Contains("Single"))
            {
                return -6f;
            }

            if (gameObject.name.Contains("Multi"))
            {
                return 4f;
            }

            if (gameObject.name.Contains("Draw"))
            {
                return -3f;
            }

            if (gameObject.name.Contains("Option"))
            {
                return 5f;
            }

            return -4f;
        }

        private GameObject CreateSelectionScribble()
        {
            GameObject root = new GameObject("SelectionScribble", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = rectTransform.sizeDelta + new Vector2(28f, 22f);
            rootRect.localRotation = Quaternion.Euler(0f, 0f, -2f);

            Color color = new Color(0.08f, 0.08f, 0.07f, 0.82f);
            const int segments = 56;
            float halfWidth = Mathf.Max(48f, rootRect.sizeDelta.x * 0.5f);
            float halfHeight = Mathf.Max(26f, rootRect.sizeDelta.y * 0.5f);
            Vector2 previous = EllipsePoint(0, segments, halfWidth, halfHeight);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = EllipsePoint(i, segments, halfWidth, halfHeight);
                CreateCrayonLine(root.transform, previous, next, 4.8f, color, i);
                previous = next;
            }

            CacheScribbleLines(root.transform);
            return root;
        }

        private static Vector2 EllipsePoint(int index, int segments, float halfWidth, float halfHeight)
        {
            float t = index / (float)segments;
            float angle = t * Mathf.PI * 2f;
            float wobble = 1f + Mathf.Sin(index * 1.73f) * 0.018f + Mathf.Cos(index * 0.91f) * 0.012f;
            return new Vector2(Mathf.Cos(angle) * halfWidth * wobble, Mathf.Sin(angle) * halfHeight * wobble);
        }

        private void CacheScribbleLines()
        {
            if (selectionScribble == null)
            {
                scribbleLines = null;
                return;
            }

            CacheScribbleLines(selectionScribble.transform);
        }

        private void CacheScribbleLines(Transform root)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name.StartsWith("ScribbleLine", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            scribbleLines = new RectTransform[count];
            int index = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (!child.name.StartsWith("ScribbleLine", System.StringComparison.Ordinal))
                {
                    continue;
                }

                scribbleLines[index++] = child.GetComponent<RectTransform>();
            }
        }

        private void ApplyScribbleProgress(float progress)
        {
            if (scribbleLines == null || scribbleLines.Length == 0)
            {
                return;
            }

            float total = scribbleLines.Length;
            for (int i = 0; i < scribbleLines.Length; i++)
            {
                RectTransform line = scribbleLines[i];
                if (line == null)
                {
                    continue;
                }

                float local = Mathf.Clamp01(progress * total - i);
                Vector3 scale = line.localScale;
                scale.x = Mathf.SmoothStep(0f, 1f, local);
                scale.y = local <= 0f ? 0f : 1f;
                line.localScale = scale;
            }
        }

        private static void CreateCrayonLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, int index)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Color soft = new Color(color.r, color.g, color.b, color.a * 0.46f);
            CreateLine(parent, from, to, width, color, "ScribbleLine");
            CreateLine(parent, from + normal * 2.1f, to + normal * 1.4f, width * 0.42f, soft, "ScribbleLine");
            CreateLine(parent, from - normal * 1.6f + direction * Mathf.Sin(index * 1.7f), to - normal * 1.2f, width * 0.34f, soft, "ScribbleLine");
        }

        private static void CreateLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, string name)
        {
            GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.anchoredPosition = from;
            lineRect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
