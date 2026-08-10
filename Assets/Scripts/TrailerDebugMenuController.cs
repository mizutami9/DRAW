using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class TrailerDebugMenuController : MonoBehaviour
    {
        private RectTransform overlay;
        private Text titleLabel;
        private Text scenarioLabel;
        private Text helpLabel;
        private Text startLabel;
        private Text backLabel;
        private Text editShapesLabel;

        public static void Ensure(RectTransform titlePanel, RectTransform titleMenu)
        {
            if (titlePanel == null || titleMenu == null)
            {
                return;
            }

            TrailerDebugMenuController controller = titlePanel.GetComponent<TrailerDebugMenuController>();
            if (controller == null)
            {
                controller = titlePanel.gameObject.AddComponent<TrailerDebugMenuController>();
            }
            controller.EnsureContents(titleMenu);
            controller.RefreshLabels();
        }

        public void Show()
        {
            if (overlay == null)
            {
                return;
            }
            RefreshLabels();
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
        }

        public void Hide()
        {
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
            }
        }

        private void EnsureContents(RectTransform titleMenu)
        {
            Font font = FindFont(titleMenu);
            Transform existingButton = titleMenu.Find("TitleDebugButton");
            if (existingButton == null)
            {
                GameObject buttonObject = CreateUiObject("TitleDebugButton", titleMenu);
                Image image = buttonObject.AddComponent<Image>();
                image.color = new Color(0.22f, 0.78f, 0.92f, 1f);
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                TitleButtonCommand command = buttonObject.AddComponent<TitleButtonCommand>();
                command.Configure(TitleButtonCommand.Command.Debug);
                CreateLabel("Label", buttonObject.transform as RectTransform, LocalizationManager.T("title_debug"), 22, font);
            }
            else
            {
                Text label = existingButton.GetComponentInChildren<Text>(true);
                if (label != null) label.text = LocalizationManager.T("title_debug");
            }

            Transform existingOverlay = transform.Find("TrailerDebugOverlay");
            if (existingOverlay != null)
            {
                overlay = existingOverlay as RectTransform;
                CacheLabels();
                return;
            }

            GameObject overlayObject = CreateUiObject("TrailerDebugOverlay", transform as RectTransform);
            overlay = overlayObject.transform as RectTransform;
            Stretch(overlay);
            Image veil = overlayObject.AddComponent<Image>();
            veil.color = new Color(0.04f, 0.055f, 0.075f, 0.82f);

            GameObject cardObject = CreateUiObject("CaptureCard", overlay);
            RectTransform card = cardObject.transform as RectTransform;
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(780f, 470f);
            card.anchoredPosition = Vector2.zero;
            Image cardImage = cardObject.AddComponent<Image>();
            cardImage.color = new Color(1f, 0.97f, 0.84f, 1f);
            Outline outline = cardObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.07f, 0.06f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);

            titleLabel = CreateLabel("Title", card, string.Empty, 38, font);
            SetRect(titleLabel.rectTransform, new Vector2(0f, 158f), new Vector2(700f, 65f));
            titleLabel.fontStyle = FontStyle.Bold;

            scenarioLabel = CreateLabel("Scenario", card, string.Empty, 31, font);
            SetRect(scenarioLabel.rectTransform, new Vector2(0f, 62f), new Vector2(670f, 60f));
            scenarioLabel.color = new Color(0.08f, 0.34f, 0.55f, 1f);

            helpLabel = CreateLabel("Help", card, string.Empty, 23, font);
            SetRect(helpLabel.rectTransform, new Vector2(0f, 5f), new Vector2(680f, 56f));

            Button editShapes = CreateButton("EditShapesButton", card, new Vector2(-255f, -120f), new Vector2(230f, 72f), new Color(0.35f, 0.76f, 0.96f, 1f), font, out editShapesLabel);
            editShapes.onClick.AddListener(EditShapes);
            Button start = CreateButton("StartButton", card, new Vector2(5f, -120f), new Vector2(260f, 72f), new Color(0.45f, 0.88f, 0.42f, 1f), font, out startLabel);
            start.onClick.AddListener(StartScenario);
            Button back = CreateButton("BackButton", card, new Vector2(270f, -120f), new Vector2(210f, 72f), new Color(1f, 0.68f, 0.34f, 1f), font, out backLabel);
            back.onClick.AddListener(Hide);

            overlay.gameObject.SetActive(false);
        }

        private void StartScenario()
        {
            Hide();
            FindFirstObjectByType<StageManager>()?.StartTrailerCoopDemo();
        }

        private void EditShapes()
        {
            Hide();
            FindFirstObjectByType<StageManager>()?.EnterDrawingMode();
        }

        private void CacheLabels()
        {
            titleLabel = FindText("CaptureCard/Title");
            scenarioLabel = FindText("CaptureCard/Scenario");
            helpLabel = FindText("CaptureCard/Help");
            startLabel = FindText("CaptureCard/StartButton/Label");
            backLabel = FindText("CaptureCard/BackButton/Label");
            editShapesLabel = FindText("CaptureCard/EditShapesButton/Label");
        }

        private Text FindText(string path)
        {
            Transform found = overlay != null ? overlay.Find(path) : null;
            return found != null ? found.GetComponent<Text>() : null;
        }

        private void RefreshLabels()
        {
            if (titleLabel != null) titleLabel.text = LocalizationManager.T("trailer_debug_title");
            if (scenarioLabel != null) scenarioLabel.text = LocalizationManager.T("trailer_debug_scenario_01");
            if (helpLabel != null) helpLabel.text = LocalizationManager.T("trailer_debug_scenario_01_help");
            if (startLabel != null) startLabel.text = LocalizationManager.T("trailer_debug_start");
            if (backLabel != null) backLabel.text = LocalizationManager.T("trailer_debug_back");
            if (editShapesLabel != null) editShapesLabel.text = LocalizationManager.T("trailer_debug_edit_shapes");
        }

        private static Button CreateButton(string name, RectTransform parent, Vector2 position, Vector2 size, Color color, Font font, out Text label)
        {
            GameObject target = CreateUiObject(name, parent);
            RectTransform rect = target.transform as RectTransform;
            SetRect(rect, position, size);
            Image image = target.AddComponent<Image>();
            image.color = color;
            Button button = target.AddComponent<Button>();
            button.targetGraphic = image;
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.07f, 0.06f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);
            label = CreateLabel("Label", rect, string.Empty, 25, font);
            return button;
        }

        private static Text CreateLabel(string name, RectTransform parent, string value, int size, Font font)
        {
            GameObject target = CreateUiObject(name, parent);
            RectTransform rect = target.transform as RectTransform;
            Stretch(rect);
            Text text = target.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.08f, 0.07f, 0.06f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUiObject(string name, RectTransform parent)
        {
            GameObject target = new GameObject(name, typeof(RectTransform));
            target.transform.SetParent(parent, false);
            return target;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Font FindFont(RectTransform menu)
        {
            Text source = menu.GetComponentInChildren<Text>(true);
            if (source != null && source.font != null)
            {
                return source.font;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
