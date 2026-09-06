using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class OptionSettingsController : MonoBehaviour
    {
        private const string BgmKey = GameBgm.VolumePlayerPrefsKey;
        private const string SeKey = GameSfx.VolumePlayerPrefsKey;

        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Text bgmValueText;
        [SerializeField] private Text seValueText;
        [SerializeField] private Text languageValueText;
        [SerializeField] private Button japaneseButton;
        [SerializeField] private Button englishButton;
        private Text languageSelectorValueText;
        private GameObject languagePopup;
        private Text languagePopupTitle;
        private ScrollRect languageScroll;
        private readonly Dictionary<string, Button> languageButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, int> languageButtonIndices = new Dictionary<string, int>();
        private InputField playerNameInput;
        private Text playerNameError;
        private Button registerButton;
        private Button backButton;

        private float nextTickTime;

        private void Awake()
        {
            EnsurePlayerNameControls();
            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value = PlayerPrefs.GetFloat(BgmKey, GameBgm.DefaultMasterVolume);
                bgmSlider.onValueChanged.AddListener(SetBgm);
            }

            if (seSlider != null)
            {
                seSlider.minValue = 0f;
                seSlider.maxValue = 1f;
                seSlider.value = PlayerPrefs.GetFloat(SeKey, GameSfx.DefaultMasterVolume);
                seSlider.onValueChanged.AddListener(SetSe);
            }

            ConfigureLanguageControls();

            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void EnsurePlayerNameControls()
        {
            RectTransform panel = transform as RectTransform;
            if (panel == null) return;
            Transform existing = panel.Find("OptionPlayerNameInput");
            if (existing != null) playerNameInput = existing.GetComponent<InputField>();
            if (playerNameInput == null)
            {
                Font font = GetComponentInChildren<Text>(true)?.font
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject labelObject = new GameObject("OptionPlayerNameLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(panel, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = font; label.fontSize = 20; label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleLeft; label.color = new Color(0.12f, 0.1f, 0.08f);
                LocalizedText localized = labelObject.AddComponent<LocalizedText>(); localized.SetKey("option_player_name_guide");

                GameObject inputObject = new GameObject("OptionPlayerNameInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
                inputObject.transform.SetParent(panel, false);
                Image background = inputObject.GetComponent<Image>();
                background.color = new Color(1f, 0.97f, 0.72f, 1f);
                Outline outline = inputObject.AddComponent<Outline>(); outline.effectColor = new Color(0.04f, 0.56f, 0.82f, 0.95f); outline.effectDistance = new Vector2(4f, -4f);
                playerNameInput = inputObject.GetComponent<InputField>();
                playerNameInput.characterLimit = PlayerNameSettings.MaximumLength;
                playerNameInput.lineType = InputField.LineType.SingleLine;

                Text value = CreateInputText(inputObject.transform, "Text", font, new Color(0.08f, 0.08f, 0.07f), TextAnchor.MiddleLeft);
                Text placeholder = CreateInputText(inputObject.transform, "Placeholder", font, new Color(0.32f, 0.3f, 0.26f, 0.55f), TextAnchor.MiddleLeft);
                LocalizedText placeholderLocalized = placeholder.gameObject.AddComponent<LocalizedText>(); placeholderLocalized.SetKey("option_player_name_placeholder");
                playerNameInput.textComponent = value;
                playerNameInput.placeholder = placeholder;

                GameObject errorObject = new GameObject("OptionPlayerNameError", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                errorObject.transform.SetParent(panel, false);
                playerNameError = errorObject.GetComponent<Text>(); playerNameError.font = font; playerNameError.fontSize = 14;
                playerNameError.alignment = TextAnchor.MiddleCenter; playerNameError.color = new Color(0.82f, 0.12f, 0.1f);
                LocalizedText errorLocalized = errorObject.AddComponent<LocalizedText>(); errorLocalized.SetKey("option_player_name_required");
                playerNameError.gameObject.SetActive(false);
            }
            else playerNameError = panel.Find("OptionPlayerNameError")?.GetComponent<Text>();

            backButton = panel.Find("TitleOptionBackButton")?.GetComponent<Button>();
            Transform register = panel.Find("OptionPlayerNameRegisterButton");
            if (register == null)
            {
                Font font = GetComponentInChildren<Text>(true)?.font
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject buttonObject = new GameObject("OptionPlayerNameRegisterButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
                buttonObject.transform.SetParent(panel, false);
                buttonObject.GetComponent<Image>().color = new Color(0.28f, 0.84f, 0.38f, 1f);
                Outline outline = buttonObject.GetComponent<Outline>();
                outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.9f);
                outline.effectDistance = new Vector2(3f, -3f);
                Text label = CreateInputText(buttonObject.transform, "Label", font, new Color(0.06f, 0.1f, 0.06f), TextAnchor.MiddleCenter);
                label.fontSize = 22;
                label.fontStyle = FontStyle.Bold;
                LocalizedText localized = label.gameObject.AddComponent<LocalizedText>();
                localized.SetKey("option_register");
                register = buttonObject.transform;
            }
            registerButton = register.GetComponent<Button>();
            registerButton.onClick.RemoveListener(RegisterPlayerName);
            registerButton.onClick.AddListener(RegisterPlayerName);

            playerNameInput.SetTextWithoutNotify(PlayerNameSettings.IsConfigured ? PlayerNameSettings.CurrentName : string.Empty);
            playerNameInput.onEndEdit.RemoveListener(SavePlayerName);
            LayoutPlayerNameControls(panel);
            RefreshEntryButtons();
        }

        private static Text CreateInputText(Transform parent, string name, Font font, Color color, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 3f); rect.offsetMax = new Vector2(-12f, -3f);
            Text text = obj.GetComponent<Text>(); text.font = font; text.fontSize = 19; text.alignment = alignment; text.color = color;
            return text;
        }

        private void LayoutPlayerNameControls(RectTransform panel)
        {
            panel.sizeDelta = new Vector2(720f, 480f);
            Place(panel.Find("OptionPlayerNameLabel") as RectTransform, new Vector2(-185f, 142f), new Vector2(190f, 40f));
            Place(playerNameInput != null ? playerNameInput.transform as RectTransform : null, new Vector2(72f, 142f), new Vector2(350f, 46f));
            Place(playerNameError != null ? playerNameError.transform as RectTransform : null, new Vector2(72f, 108f), new Vector2(350f, 22f));
            Place(registerButton != null ? registerButton.transform as RectTransform : null, new Vector2(0f, 48f), new Vector2(280f, 62f));
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f); rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private void SavePlayerName(string value)
        {
            bool accepted = PlayerNameSettings.TrySet(value);
            if (playerNameError != null) playerNameError.gameObject.SetActive(!accepted);
            if (accepted) playerNameInput.SetTextWithoutNotify(PlayerNameSettings.CurrentName);
        }

        private void RegisterPlayerName()
        {
            if (!CommitPlayerName())
            {
                playerNameInput?.ActivateInputField();
                return;
            }
            RefreshEntryButtons();
            Object.FindFirstObjectByType<UIManager>()?.CloseOption();
        }

        public bool CommitPlayerName()
        {
            SavePlayerName(playerNameInput != null ? playerNameInput.text : string.Empty);
            return PlayerNameSettings.IsConfigured;
        }

        private void RefreshEntryButtons()
        {
            bool configured = PlayerNameSettings.IsConfigured;
            if (registerButton != null) registerButton.gameObject.SetActive(!configured);
            if (backButton != null) backButton.gameObject.SetActive(configured);
            if (!configured) playerNameInput?.ActivateInputField();
        }

        public bool ValidatePlayerName()
        {
            bool valid = CommitPlayerName();
            if (playerNameError != null) playerNameError.gameObject.SetActive(!valid);
            if (!valid) playerNameInput?.ActivateInputField();
            return valid;
        }

        private void OnDestroy()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        private void SetBgm(float value)
        {
            PlayerPrefs.SetFloat(BgmKey, value);
            GameBgm.SetMasterVolume(value);
            Refresh();
        }

        private void SetSe(float value)
        {
            PlayerPrefs.SetFloat(SeKey, value);
            GameSfx.SetMasterVolume(value);
            if (Time.unscaledTime >= nextTickTime)
            {
                nextTickTime = Time.unscaledTime + 0.12f;
                PlayTick(value);
            }

            Refresh();
        }

        private void ConfigureLanguageControls()
        {
            ConfigureLanguageButton(japaneseButton, "ja");
            ConfigureLanguageButton(englishButton, "en");
            if (LocalizationManager.SupportedLanguages.Count <= 2) return;

            japaneseButton?.onClick.RemoveAllListeners();
            englishButton?.onClick.RemoveAllListeners();
            japaneseButton?.onClick.AddListener(OpenLanguagePopup);
            if (englishButton != null) englishButton.gameObject.SetActive(false);

            Transform oldValue = transform.Find("OptionLanguageCurrentValue");
            if (oldValue != null) oldValue.gameObject.SetActive(false);
            EnsureLanguagePopup();
        }

        private void ConfigureLanguageButton(Button button, string languageCode)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SetLanguageAndPlayTick(languageCode));
            LocalizationManager.LanguageDefinition definition = LocalizationManager.GetLanguageDefinition(languageCode);
            SetLanguageButtonLabel(button, definition != null ? definition.nativeName : languageCode.ToUpperInvariant());
        }

        private static void SetLanguageButtonLabel(Button button, string value)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null) return;
            LocalizedText localized = label.GetComponent<LocalizedText>();
            if (localized != null) localized.enabled = false;
            label.text = value;
        }

        private void EnsureLanguagePopup()
        {
            if (languagePopup != null) return;
            Font fallback = GetComponentInChildren<Text>(true)?.font
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            languagePopup = new GameObject("OptionLanguagePopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            languagePopup.transform.SetParent(transform, false);
            RectTransform overlay = languagePopup.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            Image overlayImage = languagePopup.GetComponent<Image>();
            overlayImage.color = new Color(0.08f, 0.07f, 0.06f, 0.48f);
            Button overlayButton = languagePopup.GetComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;
            overlayButton.onClick.AddListener(CloseLanguagePopup);

            GameObject cardObject = new GameObject("LanguageCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            cardObject.transform.SetParent(overlay, false);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(650f, 424f);
            cardObject.GetComponent<Image>().color = new Color(1f, 0.975f, 0.88f, 1f);
            Outline cardOutline = cardObject.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.92f);
            cardOutline.effectDistance = new Vector2(4f, -4f);

            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleObject.transform.SetParent(card, false);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -15f);
            titleRect.sizeDelta = new Vector2(500f, 45f);
            languagePopupTitle = titleObject.GetComponent<Text>();
            languagePopupTitle.font = fallback;
            languagePopupTitle.fontSize = 27;
            languagePopupTitle.fontStyle = FontStyle.Bold;
            languagePopupTitle.alignment = TextAnchor.MiddleCenter;
            languagePopupTitle.color = new Color(0.12f, 0.1f, 0.08f);
            titleObject.AddComponent<LocalizedText>().SetKey("language_settings");

            Button closeButton = CreateLanguageButton(card, "Close", new Vector2(286f, -36f), new Vector2(42f, 38f), fallback, "×", 24);
            closeButton.onClick.AddListener(CloseLanguagePopup);

            GameObject scrollObject = new GameObject("LanguageScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(card, false);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = scrollRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRectTransform.sizeDelta = new Vector2(604f, 342f);
            scrollRectTransform.anchoredPosition = new Vector2(0f, -27f);

            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(scrollRectTransform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-22f, 0f);
            viewportObject.GetComponent<Image>().color = new Color(0.91f, 0.96f, 0.97f, 0.62f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = true;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.cellSize = new Vector2(267f, 43f);
            grid.spacing = new Vector2(10f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scrollbar scrollbar = CreateLanguageScrollbar(scrollRectTransform);
            languageScroll = scrollObject.GetComponent<ScrollRect>();
            languageScroll.viewport = viewport;
            languageScroll.content = content;
            languageScroll.horizontal = false;
            languageScroll.vertical = true;
            languageScroll.movementType = ScrollRect.MovementType.Clamped;
            languageScroll.scrollSensitivity = 34f;
            languageScroll.verticalScrollbar = scrollbar;
            languageScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            languageScroll.verticalScrollbarSpacing = 5f;

            IReadOnlyList<LocalizationManager.LanguageDefinition> languages = LocalizationManager.SupportedLanguages;
            for (int i = 0; i < languages.Count; i++)
            {
                LocalizationManager.LanguageDefinition definition = languages[i];
                Font itemFont = LocalizationManager.LoadFontForLanguage(definition.code, fallback);
                Button button = CreateLanguageButton(content, "Language_" + definition.code, Vector2.zero, grid.cellSize, itemFont, definition.nativeName, 18);
                string code = definition.code;
                button.onClick.AddListener(() => SelectLanguageFromPopup(code));
                languageButtons[code] = button;
                languageButtonIndices[code] = i;
            }

            languagePopup.SetActive(false);
        }

        private static Button CreateLanguageButton(Transform parent, string name, Vector2 position, Vector2 size, Font font, string labelValue, int fontSize)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            buttonObject.GetComponent<Image>().color = new Color(1f, 0.985f, 0.925f, 1f);
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(rect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.08f, 0.08f, 0.07f);
            label.text = labelValue;
            return buttonObject.GetComponent<Button>();
        }

        private static Scrollbar CreateLanguageScrollbar(Transform parent)
        {
            GameObject scrollbarObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(parent, false);
            RectTransform rect = scrollbarObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(-17f, 2f);
            rect.offsetMax = new Vector2(-1f, -2f);
            scrollbarObject.GetComponent<Image>().color = new Color(0.18f, 0.16f, 0.13f, 0.14f);

            GameObject slidingObject = new GameObject("Sliding Area", typeof(RectTransform));
            slidingObject.transform.SetParent(rect, false);
            RectTransform sliding = slidingObject.GetComponent<RectTransform>();
            sliding.anchorMin = Vector2.zero;
            sliding.anchorMax = Vector2.one;
            sliding.offsetMin = new Vector2(2f, 2f);
            sliding.offsetMax = new Vector2(-2f, -2f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleObject.transform.SetParent(sliding, false);
            RectTransform handle = handleObject.GetComponent<RectTransform>();
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            handleObject.GetComponent<Image>().color = new Color(0.16f, 0.64f, 0.82f, 0.92f);

            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private void OpenLanguagePopup()
        {
            EnsureLanguagePopup();
            languagePopup.transform.SetAsLastSibling();
            languagePopup.SetActive(true);
            RefreshLanguagePopupSelection();
            ScrollCurrentLanguageIntoView();
            EventSystem.current?.SetSelectedGameObject(languageButtons.TryGetValue(LocalizationManager.CurrentLanguageCode, out Button selected)
                ? selected.gameObject
                : null);
            PlayTick(PlayerPrefs.GetFloat(SeKey, GameSfx.DefaultMasterVolume));
        }

        private void CloseLanguagePopup()
        {
            if (languagePopup == null || !languagePopup.activeSelf) return;
            languagePopup.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(japaneseButton != null ? japaneseButton.gameObject : null);
            PlayTick(PlayerPrefs.GetFloat(SeKey, GameSfx.DefaultMasterVolume));
        }

        public bool TryCloseLanguagePopup()
        {
            if (languagePopup == null || !languagePopup.activeSelf) return false;
            CloseLanguagePopup();
            return true;
        }

        private void SelectLanguageFromPopup(string languageCode)
        {
            SetLanguageAndPlayTick(languageCode);
            if (languagePopup != null) languagePopup.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(japaneseButton != null ? japaneseButton.gameObject : null);
        }

        private void RefreshLanguagePopupSelection()
        {
            foreach (KeyValuePair<string, Button> entry in languageButtons)
            {
                bool selected = LocalizationManager.IsCurrentLanguage(entry.Key);
                SetButtonStateColor(entry.Value, selected
                    ? new Color(1f, 0.82f, 0.22f, 1f)
                    : new Color(1f, 0.985f, 0.925f, 1f));
            }
        }

        private void ScrollCurrentLanguageIntoView()
        {
            if (languageScroll == null
                || !languageButtonIndices.TryGetValue(LocalizationManager.CurrentLanguageCode, out int index)) return;
            Canvas.ForceUpdateCanvases();
            int totalRows = Mathf.CeilToInt(languageButtons.Count / 2f);
            const int visibleRows = 6;
            int maximumTopRow = Mathf.Max(0, totalRows - visibleRows);
            int selectedRow = index / 2;
            int topRow = Mathf.Clamp(selectedRow - 2, 0, maximumTopRow);
            languageScroll.verticalNormalizedPosition = maximumTopRow > 0
                ? 1f - topRow / (float)maximumTopRow
                : 1f;
        }

        private void SetLanguageAndPlayTick(string languageCode)
        {
            LocalizationManager.SetLanguage(languageCode);
            PlayTick(PlayerPrefs.GetFloat(SeKey, GameSfx.DefaultMasterVolume));
            Refresh();
        }

        private void Refresh()
        {
            float bgm = bgmSlider != null ? bgmSlider.value : PlayerPrefs.GetFloat(BgmKey, GameBgm.DefaultMasterVolume);
            float se = seSlider != null ? seSlider.value : PlayerPrefs.GetFloat(SeKey, GameSfx.DefaultMasterVolume);

            if (bgmValueText != null)
            {
                bgmValueText.text = Mathf.RoundToInt(bgm * 100f) + "%";
            }

            if (seValueText != null)
            {
                seValueText.text = Mathf.RoundToInt(se * 100f) + "%";
            }

            if (languageValueText != null)
            {
                languageValueText.text = LocalizationManager.CurrentLanguageDefinition.nativeName;
            }

            if (languageSelectorValueText != null)
            {
                languageSelectorValueText.text = LocalizationManager.CurrentLanguageDefinition.nativeName;
            }

            bool selectorMode = LocalizationManager.SupportedLanguages.Count > 2;
            if (selectorMode && japaneseButton != null)
            {
                Text triggerLabel = japaneseButton.GetComponentInChildren<Text>(true);
                if (triggerLabel != null)
                {
                    triggerLabel.font = LocalizationManager.LoadCurrentFont(triggerLabel.font);
                    triggerLabel.text = LocalizationManager.CurrentLanguageDefinition.nativeName + "  ▼";
                }
            }
            RefreshLanguagePopupSelection();
            SetButtonStateColor(japaneseButton, selectorMode || LocalizationManager.IsCurrentLanguage("ja")
                ? new Color(0.22f, 0.78f, 0.92f, 1f)
                : new Color(1f, 0.985f, 0.925f, 1f));
            SetButtonStateColor(englishButton, selectorMode || LocalizationManager.IsCurrentLanguage("en")
                ? new Color(0.22f, 0.78f, 0.92f, 1f)
                : new Color(1f, 0.985f, 0.925f, 1f));
        }

        private static void SetButtonStateColor(Button button, Color color)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        private void PlayTick(float volume)
        {
            GameSfx.SetMasterVolume(volume);
            GameSfx.Play(SfxId.UiSliderTick);
        }
    }
}
