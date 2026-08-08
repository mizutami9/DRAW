using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class HudFoldoutToggle : MonoBehaviour
    {
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private Text label;
        [SerializeField] private bool open = true;
        [SerializeField] private Vector2 collapsedSize = new Vector2(44f, 44f);

        private RectTransform containerRect;
        private RectTransform toggleRect;
        private Vector2 openSize;
        private Vector2 openTogglePosition;
        private Vector2 openToggleSize;
        private bool cached;

        public void Configure(GameObject panel, Text buttonLabel)
        {
            targetPanel = panel;
            label = buttonLabel;
            Apply();
        }

        private void Awake()
        {
            EnsureCached();
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(Toggle);
            }

            Apply();
        }

        public void Toggle()
        {
            open = !open;
            Apply();
        }

        private void Apply()
        {
            EnsureCached();

            if (targetPanel != null)
            {
                targetPanel.SetActive(open);
            }

            if (containerRect != null)
            {
                containerRect.sizeDelta = open ? openSize : collapsedSize;
            }

            if (toggleRect != null)
            {
                toggleRect.anchoredPosition = open ? openTogglePosition : new Vector2(0f, 6f);
                toggleRect.sizeDelta = open ? openToggleSize : new Vector2(38f, 32f);
                toggleRect.SetAsLastSibling();
            }

            if (label != null)
            {
                label.text = GetArrowLabel();
            }
        }

        private void EnsureCached()
        {
            if (cached)
            {
                return;
            }

            toggleRect = GetComponent<RectTransform>();
            if (targetPanel != null && targetPanel.transform.parent != null)
            {
                containerRect = targetPanel.transform.parent.GetComponent<RectTransform>();
            }

            if (containerRect != null)
            {
                openSize = containerRect.sizeDelta;
            }

            if (toggleRect != null)
            {
                openTogglePosition = toggleRect.anchoredPosition;
                openToggleSize = toggleRect.sizeDelta;
            }

            cached = true;
        }

        private string GetArrowLabel()
        {
            if (open)
            {
                return "\u25be";
            }

            if (containerRect != null && containerRect.pivot.x > 0.5f)
            {
                return "\u25c0";
            }

            return "\u25b6";
        }
    }
}
