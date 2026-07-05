using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class HudFoldoutToggle : MonoBehaviour
    {
        [SerializeField] private GameObject targetPanel;
        [SerializeField] private Text label;
        [SerializeField] private bool open = true;

        public void Configure(GameObject panel, Text buttonLabel)
        {
            targetPanel = panel;
            label = buttonLabel;
            Apply();
        }

        private void Awake()
        {
            Button button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(Toggle);
            }
        }

        public void Toggle()
        {
            open = !open;
            Apply();
        }

        private void Apply()
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(open);
            }

            if (label != null)
            {
                label.text = open ? "▼" : "▲";
            }
        }
    }
}
