using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private Text uiText;
        private TextMesh textMesh;

        private void Awake()
        {
            uiText = GetComponent<Text>();
            textMesh = GetComponent<TextMesh>();
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

        public void SetKey(string localizationKey)
        {
            key = localizationKey;
            Refresh();
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            string value = LocalizationManager.T(key);
            if (uiText != null)
            {
                uiText.text = value;
            }

            if (textMesh != null)
            {
                textMesh.text = value;
            }
        }
    }
}
