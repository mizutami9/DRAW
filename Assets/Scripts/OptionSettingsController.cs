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

        private float nextTickTime;

        private void Awake()
        {
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
