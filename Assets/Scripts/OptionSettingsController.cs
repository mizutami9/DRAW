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

            if (japaneseButton != null)
            {
                japaneseButton.onClick.AddListener(() => SetLanguage(LocalizationManager.Language.Japanese));
            }

            if (englishButton != null)
            {
                englishButton.onClick.AddListener(() => SetLanguage(LocalizationManager.Language.English));
            }

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

        private void SetLanguage(LocalizationManager.Language language)
        {
            LocalizationManager.SetLanguage(language);
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
                languageValueText.text = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese
                    ? LocalizationManager.T("lang_ja")
                    : LocalizationManager.T("lang_en");
            }

            bool japanese = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese;
            SetButtonStateColor(japaneseButton, japanese
                ? new Color(0.22f, 0.78f, 0.92f, 1f)
                : new Color(1f, 0.985f, 0.925f, 1f));
            SetButtonStateColor(englishButton, japanese
                ? new Color(1f, 0.985f, 0.925f, 1f)
                : new Color(0.22f, 0.78f, 0.92f, 1f));
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
