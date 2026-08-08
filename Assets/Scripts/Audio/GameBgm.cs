using System.Collections;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class GameBgm : MonoBehaviour
    {
        public const string VolumePlayerPrefsKey = "option_bgm_volume";
        private const float CrossFadeSeconds = 0.65f;

        private static GameBgm instance;
        private AudioSource sourceA;
        private AudioSource sourceB;
        private AudioSource activeSource;
        private string currentTrack;
        private string requestedTrack;
        private int requestVersion;
        private float masterVolume = 0.8f;

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
            if (service.activeSource != null)
            {
                service.activeSource.volume = service.masterVolume;
            }

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

            service.RequestTrack(trackName);
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
            masterVolume = PlayerPrefs.GetFloat(VolumePlayerPrefsKey, 0.8f);
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
            StartCoroutine(LoadAndPlay(trackName, version));
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
                Debug.LogWarning("BGM asset not found: Resources/Bgm/" + trackName);
                yield break;
            }

            AudioSource previous = activeSource;
            AudioSource next = previous == sourceA ? sourceB : sourceA;
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            activeSource = next;
            currentTrack = trackName;
            requestedTrack = null;

            float elapsed = 0f;
            while (elapsed < CrossFadeSeconds && version == requestVersion)
            {
                elapsed += Time.unscaledDeltaTime;
                float amount = Mathf.Clamp01(elapsed / CrossFadeSeconds);
                next.volume = masterVolume * amount;
                if (previous != null)
                {
                    previous.volume = masterVolume * (1f - amount);
                }
                yield return null;
            }

            if (version != requestVersion)
            {
                yield break;
            }

            next.volume = masterVolume;
            if (previous != null)
            {
                previous.Stop();
                previous.clip = null;
                previous.volume = 0f;
            }
        }
    }
}
