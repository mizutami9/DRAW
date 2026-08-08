using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class EscapeMenuVisualPolisher : MonoBehaviour
    {
        private bool polished;

        private void OnEnable()
        {
            Polish();
        }

        public void Polish()
        {
            if (polished)
            {
                return;
            }

            polished = true;
            StrengthenPanelOutline();
            StrengthenText();
            StrengthenSketchLines();
        }

        private void StrengthenPanelOutline()
        {
            Outline outline = GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.08f, 0.07f, 0.06f, 0.86f);
                outline.effectDistance = new Vector2(2.8f, -2.8f);
            }
        }

        private void StrengthenText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                bool isTitle = text.name == "MenuTitle";
                text.color = new Color(0.05f, 0.045f, 0.04f, 1f);
                text.fontStyle = FontStyle.Bold;
                text.fontSize = isTitle ? Mathf.Max(text.fontSize, 24) : Mathf.Max(text.fontSize, 24);
                text.resizeTextMinSize = Mathf.Max(text.resizeTextMinSize, text.fontSize - 4);
                text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize);

                Outline textOutline = text.GetComponent<Outline>();
                if (textOutline == null)
                {
                    textOutline = text.gameObject.AddComponent<Outline>();
                }

                textOutline.effectColor = new Color(1f, 0.98f, 0.88f, 0.45f);
                textOutline.effectDistance = new Vector2(0.8f, -0.8f);
            }
        }

        private void StrengthenSketchLines()
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.gameObject.name != "IconLine")
                {
                    continue;
                }

                RectTransform rect = image.rectTransform;
                Vector2 size = rect.sizeDelta;
                size.y = Mathf.Min(size.y * 1.28f + 0.2f, 4.2f);
                rect.sizeDelta = size;

                Color color = image.color;
                image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a + 0.18f));
            }
        }
    }
}
