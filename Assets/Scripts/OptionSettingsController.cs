using UnityEngine;
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
            japaneseButton?.onClick.AddListener(() => CycleLanguage(-1));
            englishButton?.onClick.AddListener(() => CycleLanguage(1));
            SetLanguageButtonLabel(japaneseButton, "<");
            SetLanguageButtonLabel(englishButton, ">");

            RectTransform panel = transform as RectTransform;
            Transform existing = panel != null ? panel.Find("OptionLanguageCurrentValue") : null;
            if (existing == null && panel != null)
            {
                Font font = GetComponentInChildren<Text>(true)?.font
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                GameObject valueObject = new GameObject("OptionLanguageCurrentValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                valueObject.transform.SetParent(panel, false);
                languageSelectorValueText = valueObject.GetComponent<Text>();
                languageSelectorValueText.font = font;
                languageSelectorValueText.fontSize = 17;
                languageSelectorValueText.fontStyle = FontStyle.Bold;
                languageSelectorValueText.alignment = TextAnchor.MiddleCenter;
                languageSelectorValueText.color = new Color(0.08f, 0.08f, 0.07f);
                Place(valueObject.transform as RectTransform, new Vector2(63f, 220f), new Vector2(118f, 42f));
            }
            else
            {
                languageSelectorValueText = existing.GetComponent<Text>();
            }
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

        private void CycleLanguage(int direction)
        {
            System.Collections.Generic.IReadOnlyList<LocalizationManager.LanguageDefinition> languages = LocalizationManager.SupportedLanguages;
            if (languages.Count == 0) return;
            int currentIndex = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (LocalizationManager.IsCurrentLanguage(languages[i].code))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + direction + languages.Count) % languages.Count;
            SetLanguageAndPlayTick(languages[nextIndex].code);
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
