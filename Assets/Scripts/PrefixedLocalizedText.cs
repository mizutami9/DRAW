using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class PrefixedLocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string prefix;

        private Text uiText;

        private void Awake()
        {
            uiText = GetComponent<Text>();
            Refresh();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (uiText != null && !string.IsNullOrEmpty(key))
            {
                uiText.text = prefix + LocalizationManager.T(key);
            }
        }
    }
}
