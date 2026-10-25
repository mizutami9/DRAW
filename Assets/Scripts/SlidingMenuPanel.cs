using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class SlidingMenuPanel : MonoBehaviour
    {
        [SerializeField] private Vector2 openPosition = new Vector2(12f, 86f);
        [SerializeField] private Vector2 closedPosition = new Vector2(-342f, 86f);
        [SerializeField] private float slideSpeed = 18f;

        private RectTransform rectTransform;
        private EscapeMenuVisualPolisher visualPolisher;
        private bool open;
        private bool localSplitLayout;

        public bool IsOpen => open;

        public void SetLocalSplitLayout(bool enabled)
        {
            if (localSplitLayout == enabled) return;
            localSplitLayout = enabled;
            NormalizeLayout();
            if (rectTransform != null)
                rectTransform.anchoredPosition = open ? openPosition : closedPosition;
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            NormalizeLayout();
            if (!open)
            {
                rectTransform.anchoredPosition = closedPosition;
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            NormalizeLayout();
        }

        private void Update()
        {
            if (rectTransform == null)
            {
                return;
            }

            Vector2 target = open ? openPosition : closedPosition;
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, target, Time.unscaledDeltaTime * slideSpeed);

            if (Vector2.Distance(rectTransform.anchoredPosition, target) < 0.5f)
            {
                rectTransform.anchoredPosition = target;
                if (!open)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        public void Open()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            NormalizeLayout();
            gameObject.SetActive(true);
            if (!open)
            {
                rectTransform.anchoredPosition = closedPosition;
            }

            open = true;
        }

        public void OpenImmediate()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            NormalizeLayout();
            open = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = openPosition;
            }
        }

        public void Close()
        {
            open = false;
        }

        public void CloseImmediate()
        {
            open = false;
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = closedPosition;
            }

            gameObject.SetActive(false);
        }

        private void NormalizeLayout()
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(0f, 0f);
            rectTransform.pivot = new Vector2(0f, 0f);
            rectTransform.sizeDelta = new Vector2(320f, 420f);
            rectTransform.localScale = Vector3.one;

            // A shared pause menu belongs to P1. In a two-player horizontal
            // split, keep the whole paper inside P1's upper viewport instead
            // of straddling the divider and covering P2's screen.
            float normalY = 86f;
            float splitY = normalY;
            RectTransform parentRect = rectTransform.parent as RectTransform;
            if (localSplitLayout && parentRect != null)
            {
                float parentHeight = parentRect.rect.height;
                float upperViewportHeight = parentHeight * 0.5f;
                float splitScale = Mathf.Clamp(
                    (upperViewportHeight - 24f) / rectTransform.sizeDelta.y,
                    0.72f,
                    1f);
                rectTransform.localScale = Vector3.one * splitScale;
                float visualHeight = rectTransform.sizeDelta.y * splitScale;
                splitY = upperViewportHeight
                    + Mathf.Max(12f, (upperViewportHeight - visualHeight) * 0.5f);
            }
            openPosition = new Vector2(localSplitLayout ? 18f : 12f, splitY);
            closedPosition = new Vector2(-342f, splitY);

            if (visualPolisher == null)
            {
                visualPolisher = GetComponent<EscapeMenuVisualPolisher>();
                if (visualPolisher == null)
                {
                    visualPolisher = gameObject.AddComponent<EscapeMenuVisualPolisher>();
                }
            }

            visualPolisher.Polish();
        }
    }
}
