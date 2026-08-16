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

        public bool IsOpen => open;

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
