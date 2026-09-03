using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private Text uiText;
        private TextMesh textMesh;
        private Font defaultFont;
        private TextAnchor defaultAlignment;

        private void Awake()
        {
            uiText = GetComponent<Text>();
            textMesh = GetComponent<TextMesh>();
            if (uiText != null)
            {
                defaultFont = uiText.font;
                defaultAlignment = uiText.alignment;
            }
            else if (textMesh != null)
            {
                defaultFont = textMesh.font;
                defaultAlignment = textMesh.anchor;
            }
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
                uiText.font = LocalizationManager.LoadCurrentFont(defaultFont);
                uiText.alignment = ResolveAlignment(defaultAlignment, LocalizationManager.CurrentLanguageIsRightToLeft);
            }

            if (textMesh != null)
            {
                textMesh.text = value;
                textMesh.font = LocalizationManager.LoadCurrentFont(defaultFont);
                textMesh.anchor = ResolveAlignment(defaultAlignment, LocalizationManager.CurrentLanguageIsRightToLeft);
                MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
                if (renderer != null && textMesh.font != null) renderer.sharedMaterial = textMesh.font.material;
            }
        }

        private static TextAnchor ResolveAlignment(TextAnchor alignment, bool rightToLeft)
        {
            if (!rightToLeft) return alignment;
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.UpperRight: return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight: return TextAnchor.MiddleLeft;
                case TextAnchor.LowerLeft: return TextAnchor.LowerRight;
                case TextAnchor.LowerRight: return TextAnchor.LowerLeft;
                default: return alignment;
            }
        }
    }
}
