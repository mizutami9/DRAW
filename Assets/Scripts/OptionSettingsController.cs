using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class OptionSettingsController : MonoBehaviour
    {
        private const string BgmKey = "option_bgm_volume";
        private const string SeKey = "option_se_volume";
        private const string VibrationKey = "option_vibration";

        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Text bgmValueText;
        [SerializeField] private Text seValueText;
        [SerializeField] private Text vibrationValueText;
        [SerializeField] private Text languageValueText;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private Button japaneseButton;
        [SerializeField] private Button englishButton;

        private AudioSource feedbackSource;
        private AudioClip tickClip;
        private bool vibrationEnabled;
        private float nextTickTime;

        private void Awake()
        {
            feedbackSource = gameObject.AddComponent<AudioSource>();
            feedbackSource.playOnAwake = false;
            tickClip = CreateTickClip();

            if (bgmSlider != null)
            {
                bgmSlider.minValue = 0f;
                bgmSlider.maxValue = 1f;
                bgmSlider.value = PlayerPrefs.GetFloat(BgmKey, 0.8f);
                bgmSlider.onValueChanged.AddListener(SetBgm);
            }

            if (seSlider != null)
            {
                seSlider.minValue = 0f;
                seSlider.maxValue = 1f;
                seSlider.value = PlayerPrefs.GetFloat(SeKey, 0.8f);
                seSlider.onValueChanged.AddListener(SetSe);
            }

            vibrationEnabled = PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            if (vibrationButton != null)
            {
                vibrationButton.onClick.AddListener(ToggleVibration);
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
            AudioListener.volume = Mathf.Clamp01(value);
            Refresh();
        }

        private void SetSe(float value)
        {
            PlayerPrefs.SetFloat(SeKey, value);
            if (Time.unscaledTime >= nextTickTime)
            {
                nextTickTime = Time.unscaledTime + 0.12f;
                PlayTick(value);
            }

            Refresh();
        }

        private void ToggleVibration()
        {
            vibrationEnabled = !vibrationEnabled;
            PlayerPrefs.SetInt(VibrationKey, vibrationEnabled ? 1 : 0);
            PlayTick(PlayerPrefs.GetFloat(SeKey, 0.8f));
            Refresh();
        }

        private void SetLanguage(LocalizationManager.Language language)
        {
            LocalizationManager.SetLanguage(language);
            PlayTick(PlayerPrefs.GetFloat(SeKey, 0.8f));
            Refresh();
        }

        private void Refresh()
        {
            float bgm = bgmSlider != null ? bgmSlider.value : PlayerPrefs.GetFloat(BgmKey, 0.8f);
            float se = seSlider != null ? seSlider.value : PlayerPrefs.GetFloat(SeKey, 0.8f);

            if (bgmValueText != null)
            {
                bgmValueText.text = Mathf.RoundToInt(bgm * 100f) + "%";
            }

            if (seValueText != null)
            {
                seValueText.text = Mathf.RoundToInt(se * 100f) + "%";
            }

            if (vibrationValueText != null)
            {
                vibrationValueText.text = vibrationEnabled ? "◉ ON" : "○ OFF";
            }

            if (languageValueText != null)
            {
                languageValueText.text = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese ? "日本語" : "English";
            }
        }

        private void PlayTick(float volume)
        {
            if (feedbackSource != null && tickClip != null)
            {
                feedbackSource.PlayOneShot(tickClip, Mathf.Clamp01(volume) * 0.45f);
            }
        }

        private static AudioClip CreateTickClip()
        {
            const int sampleRate = 22050;
            int samples = Mathf.RoundToInt(sampleRate * 0.055f);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (i / (float)samples);
                data[i] = Mathf.Sin(t * 940f * Mathf.PI * 2f) * envelope * 0.28f;
            }

            AudioClip clip = AudioClip.Create("OptionTick", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
