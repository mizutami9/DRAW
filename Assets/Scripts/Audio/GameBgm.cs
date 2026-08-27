using System.Collections;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class GameBgm : MonoBehaviour
    {
        public const string VolumePlayerPrefsKey = "option_bgm_volume";
        public const float DefaultMasterVolume = 0.05f;
        private const float FadeOutSeconds = 0.7f;
        private const float TransitionPauseSeconds = 0.15f;
        private const float FadeInSeconds = 0.9f;
        private const float InitialStartDelaySeconds = 0.45f;

        private static GameBgm instance;
        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private string currentTrack;
        private string requestedTrack;
        private int requestVersion;
        private float masterVolume = DefaultMasterVolume;
        private float sourceAGain;
        private float sourceBGain;
        private Coroutine transitionRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void PlayTitle()
        {
            PlayTrack("title");
        }

        public static void PlayForStage(string stageId)
        {
            PlayTrack(GetTrackName(stageId));
        }

        public static void SetMasterVolume(float value)
        {
            GameBgm service = EnsureInstance();
            if (service == null)
            {
                return;
            }

            service.masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePlayerPrefsKey, service.masterVolume);
            service.ApplySourceVolumes();

            // BGM and SE have independent sliders. Global listener volume would
            // multiply both and make the BGM slider mute sound effects as well.
            AudioListener.volume = 1f;
        }

        public static string GetTrackName(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId)
                || string.Equals(stageId, "title", System.StringComparison.OrdinalIgnoreCase))
            {
                return "title";
            }

            int separator = stageId.IndexOf('-');
            string worldPart = separator >= 0 ? stageId.Substring(0, separator) : stageId;
            return int.TryParse(worldPart, out int world) && world >= 1 && world <= 15
                ? world.ToString()
                : "1";
        }

        private static void PlayTrack(string trackName)
        {
            GameBgm service = EnsureInstance();
            if (service == null)
            {
                return;
            }

            EnsureAudioListener();
            service.RequestTrack(trackName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureAudioListener()
        {
            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i] != null && listeners[i].enabled
                    && listeners[i].gameObject.activeInHierarchy)
                {
                    return;
                }
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            AudioListener cameraListener = mainCamera.GetComponent<AudioListener>();
            if (cameraListener == null) cameraListener = mainCamera.gameObject.AddComponent<AudioListener>();
            cameraListener.enabled = true;
        }

        private static GameBgm EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject serviceObject = new GameObject("GameBgm");
            instance = serviceObject.AddComponent<GameBgm>();
            DontDestroyOnLoad(serviceObject);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            masterVolume = PlayerPrefs.GetFloat(VolumePlayerPrefsKey, DefaultMasterVolume);
            sourceA = CreateSource("BgmSourceA");
            sourceB = CreateSource("BgmSourceB");
            AudioListener.volume = 1f;
        }

        private AudioSource CreateSource(string objectName)
        {
            GameObject sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        private void RequestTrack(string trackName)
        {
            if (string.Equals(trackName, currentTrack, System.StringComparison.Ordinal)
                && string.IsNullOrEmpty(requestedTrack)
                && activeSource != null
                && activeSource.isPlaying)
            {
                return;
            }
            if (string.Equals(trackName, requestedTrack, System.StringComparison.Ordinal))
            {
                return;
            }

            requestedTrack = trackName;
            int version = ++requestVersion;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(LoadAndPlay(trackName, version));
        }

        private IEnumerator LoadAndPlay(string trackName, int version)
        {
            ResourceRequest load = Resources.LoadAsync<AudioClip>("Bgm/" + trackName);
            yield return load;
            if (version != requestVersion)
            {
                yield break;
            }

            AudioClip clip = load.asset as AudioClip;
            if (clip == null)
            {
                requestedTrack = null;
                transitionRoutine = null;
                Debug.LogWarning("BGM asset not found: Resources/Bgm/" + trackName);
                yield break;
            }

            bool hadPlayingTrack = (sourceA != null && sourceA.isPlaying)
                || (sourceB != null && sourceB.isPlaying);
            if (hadPlayingTrack)
            {
                float startGainA = sourceAGain;
                float startGainB = sourceBGain;
                float elapsed = 0f;
                while (elapsed < FadeOutSeconds && version == requestVersion)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float amount = Mathf.Clamp01(elapsed / FadeOutSeconds);
                    SetSourceGain(sourceA, Mathf.Lerp(startGainA, 0f, amount));
                    SetSourceGain(sourceB, Mathf.Lerp(startGainB, 0f, amount));
                    yield return null;
                }

                if (version != requestVersion)
                {
                    yield break;
                }

                StopAndClear(sourceA);
                StopAndClear(sourceB);
            }

            float pauseSeconds = hadPlayingTrack
                ? TransitionPauseSeconds
                : InitialStartDelaySeconds;
            if (pauseSeconds > 0f)
            {
                float pauseElapsed = 0f;
                while (pauseElapsed < pauseSeconds && version == requestVersion)
                {
                    pauseElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (version != requestVersion)
            {
                yield break;
            }

            AudioSource next = activeSource == sourceA ? sourceB : sourceA;
            next.Stop();
            next.clip = clip;
            SetSourceGain(next, 0f);
            next.Play();
            activeSource = next;
            currentTrack = trackName;
            requestedTrack = null;

            float fadeInElapsed = 0f;
            while (fadeInElapsed < FadeInSeconds && version == requestVersion)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                float amount = Mathf.Clamp01(fadeInElapsed / FadeInSeconds);
                SetSourceGain(next, amount);
                yield return null;
            }

            if (version != requestVersion)
            {
                yield break;
            }

            SetSourceGain(next, 1f);
            transitionRoutine = null;
        }

        private void StopAndClear(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
            SetSourceGain(source, 0f);
        }

        private void SetSourceGain(AudioSource source, float gain)
        {
            float clampedGain = Mathf.Clamp01(gain);
            if (source == sourceA)
            {
                sourceAGain = clampedGain;
            }
            else if (source == sourceB)
            {
                sourceBGain = clampedGain;
            }

            if (source != null)
            {
                source.volume = masterVolume * clampedGain;
            }
        }

        private void ApplySourceVolumes()
        {
            if (sourceA != null)
            {
                sourceA.volume = masterVolume * sourceAGain;
            }
            if (sourceB != null)
            {
                sourceB.volume = masterVolume * sourceBGain;
            }
        }
    }
}
