using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class GameSfx : MonoBehaviour
    {
        public const string VolumePlayerPrefsKey = "option_se_volume";
        public const float DefaultMasterVolume = 0.3f;
        private const int SourcePoolSize = 16;

        private static GameSfx instance;
        private readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        private readonly Dictionary<SfxId, float> nextAllowedTimes = new Dictionary<SfxId, float>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int nextSourceIndex;
        private float masterVolume = DefaultMasterVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void Play(SfxId id, float volumeScale = 1f)
        {
            EnsureInstance()?.PlayInternal(id, volumeScale, false, Vector3.zero);
        }

        public static void PlayAt(SfxId id, Vector3 position, float volumeScale = 1f)
        {
            EnsureInstance()?.PlayInternal(id, volumeScale, true, position);
        }

        public static void SetMasterVolume(float value)
        {
            GameSfx service = EnsureInstance();
            if (service == null)
            {
                return;
            }

            service.masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePlayerPrefsKey, service.masterVolume);
        }

        public static float MasterVolume => instance != null
            ? instance.masterVolume
            : PlayerPrefs.GetFloat(VolumePlayerPrefsKey, DefaultMasterVolume);

        private static GameSfx EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject serviceObject = new GameObject("GameSfx");
            instance = serviceObject.AddComponent<GameSfx>();
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
            for (int i = 0; i < SourcePoolSize; i++)
            {
                GameObject sourceObject = new GameObject("SfxSource_" + i);
                sourceObject.transform.SetParent(transform, false);
                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources.Add(source);
            }
        }

        private void PlayInternal(SfxId id, float volumeScale, bool spatial, Vector3 position)
        {
            SfxDefinition definition = SfxCatalog.Get(id);
            if (nextAllowedTimes.TryGetValue(id, out float nextTime) && Time.unscaledTime < nextTime)
            {
                return;
            }

            AudioClip clip = ResolveClip(id, definition.ResourcePath);
            if (clip == null || sources.Count == 0 || masterVolume <= 0f)
            {
                return;
            }

            nextAllowedTimes[id] = Time.unscaledTime + definition.Cooldown;
            AudioSource source = GetAvailableSource();
            source.transform.position = position;
            source.spatialBlend = spatial ? 0.7f : 0f;
            source.pitch = Random.Range(definition.PitchMin, definition.PitchMax);
            source.volume = Mathf.Clamp01(definition.Volume * masterVolume * volumeScale);
            source.clip = clip;
            source.Play();
        }

        private AudioClip ResolveClip(SfxId id, string path)
        {
            if (clips.TryGetValue(id, out AudioClip clip))
            {
                return clip;
            }

            clip = Resources.Load<AudioClip>(path);
            clips[id] = clip;
            if (clip == null)
            {
                Debug.LogWarning("SE asset not found: Resources/" + path);
            }
            return clip;
        }

        private AudioSource GetAvailableSource()
        {
            for (int i = 0; i < sources.Count; i++)
            {
                int index = (nextSourceIndex + i) % sources.Count;
                if (!sources[index].isPlaying)
                {
                    nextSourceIndex = (index + 1) % sources.Count;
                    return sources[index];
                }
            }

            AudioSource source = sources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sources.Count;
            return source;
        }
    }
}
