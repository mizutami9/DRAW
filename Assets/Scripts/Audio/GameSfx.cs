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

        public static AudioClip CreatePreviewClip(SfxId id)
        {
            if (IsMuted(id)) return null;
            if (UsesProceduralOverride(id)) return CreateProceduralClip(id);
            SfxDefinition definition = SfxCatalog.Get(id);
            AudioClip clip = Resources.Load<AudioClip>(definition.ResourcePath);
            return clip != null ? clip : CreateProceduralClip(id);
        }

        public static bool IsMuted(SfxId id)
        {
            return id == SfxId.PlayerPush || id == SfxId.CrumblingFloorWarning;
        }

        public static bool UsesGeneratedClip(SfxId id)
        {
            return UsesProceduralOverride(id);
        }

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
            if (IsMuted(id)) return;
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

            clip = UsesProceduralOverride(id) ? CreateProceduralClip(id) : Resources.Load<AudioClip>(path);
            if (clip == null && !UsesProceduralOverride(id))
            {
                clip = CreateProceduralClip(id);
            }
            clips[id] = clip;
            if (clip == null)
            {
                Debug.LogWarning("SE asset not found: Resources/" + path);
            }
            return clip;
        }

        private static AudioClip CreateProceduralClip(SfxId id)
        {
            switch (id)
            {
                case SfxId.DrawPenStart:
                case SfxId.DrawPenLoop:
                case SfxId.DrawPenEnd:
                case SfxId.DrawEraserLoop:
                case SfxId.DrawEraseComplete:
                case SfxId.DrawPartChange:
                case SfxId.DrawSpeciesChange:
                case SfxId.DrawInkWarning:
                case SfxId.DrawInkOver:
                case SfxId.DrawConfirm:
                    return CreateDrawFeedbackClip(id);
                case SfxId.PlayerDeath:
                    return CreatePaperDeathClip();
                case SfxId.TurtleShellEnter:
                    return CreateShellTuckClip();
                case SfxId.BombExplosion:
                    return CreateCrayonExplosionClip(false);
                case SfxId.DynamiteExplosion:
                    return CreateCrayonExplosionClip(true);
                case SfxId.EnemyDefeat:
                    return CreateEnemyEraseClip();
                case SfxId.GoalReached:
                    return CreateGoalClip();
                case SfxId.StageClear:
                    return CreateStageClearClip();
                case SfxId.ClearStampImpact:
                    return CreateStampImpactClip();
                case SfxId.ClearCelebrationChime:
                    return CreateCelebrationChimeClip();
                default:
                    return null;
            }
        }

        private static bool UsesProceduralOverride(SfxId id)
        {
            switch (id)
            {
                case SfxId.DrawPenStart:
                case SfxId.DrawPenLoop:
                case SfxId.DrawPenEnd:
                case SfxId.DrawEraserLoop:
                case SfxId.DrawEraseComplete:
                case SfxId.DrawPartChange:
                case SfxId.DrawSpeciesChange:
                case SfxId.DrawInkWarning:
                case SfxId.DrawInkOver:
                case SfxId.DrawConfirm:
                case SfxId.PlayerDeath:
                case SfxId.TurtleShellEnter:
                case SfxId.BombExplosion:
                case SfxId.DynamiteExplosion:
                case SfxId.EnemyDefeat:
                case SfxId.GoalReached:
                case SfxId.StageClear:
                case SfxId.ClearStampImpact:
                case SfxId.ClearCelebrationChime:
                    return true;
                default:
                    return false;
            }
        }

        private static AudioClip CreateDrawFeedbackClip(SfxId id)
        {
            float duration = id switch
            {
                SfxId.DrawPenStart => 0.11f,
                SfxId.DrawPenLoop => 0.12f,
                SfxId.DrawPenEnd => 0.14f,
                SfxId.DrawEraserLoop => 0.18f,
                SfxId.DrawEraseComplete => 0.24f,
                SfxId.DrawPartChange => 0.2f,
                SfxId.DrawSpeciesChange => 0.38f,
                SfxId.DrawInkWarning => 0.4f,
                SfxId.DrawInkOver => 0.42f,
                _ => 0.55f
            };
            float[] samples = CreateSampleBuffer(duration);
            switch (id)
            {
                case SfxId.DrawPenStart:
                    AddNoise(samples, 0f, 0.075f, 0.22f, 24f, 0.48f, 0x101u);
                    AddTone(samples, 0f, 0.09f, 920f, 610f, 0.15f, 19f);
                    break;
                case SfxId.DrawPenLoop:
                    AddNoise(samples, 0f, 0.12f, 0.2f, 5f, 0.25f, 0x202u, 34f);
                    AddTone(samples, 0f, 0.11f, 185f, 205f, 0.055f, 8f);
                    break;
                case SfxId.DrawPenEnd:
                    AddNoise(samples, 0f, 0.07f, 0.16f, 29f, 0.52f, 0x303u);
                    AddTone(samples, 0.025f, 0.1f, 620f, 1180f, 0.12f, 16f);
                    break;
                case SfxId.DrawEraserLoop:
                    AddNoise(samples, 0f, 0.18f, 0.3f, 6f, 0.9f, 0x404u, 18f);
                    AddTone(samples, 0f, 0.16f, 116f, 132f, 0.07f, 7f);
                    break;
                case SfxId.DrawEraseComplete:
                    AddNoise(samples, 0f, 0.15f, 0.3f, 9f, 0.88f, 0x505u, 12f);
                    AddNoise(samples, 0.12f, 0.1f, 0.16f, 20f, 0.5f, 0x506u, 28f);
                    AddTone(samples, 0.1f, 0.12f, 360f, 540f, 0.1f, 13f);
                    break;
                case SfxId.DrawPartChange:
                    AddNoise(samples, 0f, 0.055f, 0.18f, 28f, 0.66f, 0x606u);
                    AddTone(samples, 0f, 0.14f, 430f, 520f, 0.17f, 13f);
                    AddTone(samples, 0.075f, 0.11f, 610f, 690f, 0.12f, 15f);
                    break;
                case SfxId.DrawSpeciesChange:
                    AddNoise(samples, 0f, 0.3f, 0.16f, 5f, 0.72f, 0x707u, 22f);
                    AddTone(samples, 0f, 0.34f, 245f, 760f, 0.2f, 5f);
                    AddTone(samples, 0.12f, 0.2f, 520f, 980f, 0.09f, 7f);
                    break;
                case SfxId.DrawInkWarning:
                    AddTone(samples, 0f, 0.16f, 540f, 510f, 0.19f, 10f);
                    AddTone(samples, 0.19f, 0.17f, 540f, 480f, 0.17f, 10f);
                    AddNoise(samples, 0f, 0.045f, 0.12f, 30f, 0.55f, 0x808u);
                    break;
                case SfxId.DrawInkOver:
                    AddNoise(samples, 0f, 0.095f, 0.3f, 22f, 0.42f, 0x909u);
                    AddTone(samples, 0f, 0.36f, 310f, 92f, 0.28f, 5.8f);
                    AddTone(samples, 0.06f, 0.21f, 116f, 74f, 0.16f, 7f);
                    break;
                default:
                    AddTone(samples, 0f, 0.28f, 659.25f, 659.25f, 0.18f, 7f);
                    AddTone(samples, 0.1f, 0.3f, 830.61f, 830.61f, 0.18f, 7f);
                    AddTone(samples, 0.2f, 0.3f, 1046.5f, 1046.5f, 0.17f, 7f);
                    AddNoise(samples, 0f, 0.07f, 0.1f, 27f, 0.55f, 0xA10u);
                    break;
            }
            return FinishClip("Sketch " + id, samples);
        }

        private static AudioClip CreatePaperDeathClip()
        {
            float[] samples = CreateSampleBuffer(0.62f);
            AddNoise(samples, 0f, 0.32f, 0.42f, 7f, 0.84f, 0xD01u, 13f);
            AddNoise(samples, 0.2f, 0.36f, 0.22f, 5f, 0.94f, 0xD02u, 8f);
            AddTone(samples, 0.04f, 0.48f, 245f, 74f, 0.24f, 5f);
            AddTone(samples, 0.14f, 0.28f, 510f, 180f, 0.08f, 8f);
            return FinishClip("Paper Crumple Defeat", samples);
        }

        private static AudioClip CreateShellTuckClip()
        {
            float[] samples = CreateSampleBuffer(0.3f);
            AddNoise(samples, 0f, 0.06f, 0.22f, 32f, 0.72f, 0x710u);
            AddTone(samples, 0f, 0.25f, 310f, 145f, 0.3f, 11f);
            AddTone(samples, 0.025f, 0.2f, 620f, 290f, 0.13f, 13f);
            return FinishClip("Turtle Wooden Tuck", samples);
        }

        private static AudioClip CreateCrayonExplosionClip(bool dynamite)
        {
            float duration = dynamite ? 0.92f : 0.58f;
            float[] samples = CreateSampleBuffer(duration);
            AddNoise(samples, 0f, dynamite ? 0.72f : 0.44f, dynamite ? 0.55f : 0.43f,
                dynamite ? 3.8f : 5.8f, 0.9f, dynamite ? 0xD17u : 0xB07u, dynamite ? 7f : 10f);
            AddTone(samples, 0f, duration * 0.9f, dynamite ? 94f : 132f, dynamite ? 42f : 64f,
                dynamite ? 0.52f : 0.4f, dynamite ? 3.5f : 5f);
            AddNoise(samples, 0f, 0.075f, dynamite ? 0.42f : 0.31f, 30f, 0.35f,
                dynamite ? 0xD18u : 0xB08u);
            if (dynamite)
            {
                AddTone(samples, 0.12f, 0.55f, 170f, 58f, 0.24f, 5f);
                AddNoise(samples, 0.3f, 0.38f, 0.18f, 7f, 0.86f, 0xD19u, 9f);
            }
            return FinishClip(dynamite ? "Crayon Dynamite Blast" : "Crayon Bomb Poof", samples);
        }

        private static AudioClip CreateEnemyEraseClip()
        {
            float[] samples = CreateSampleBuffer(0.38f);
            AddNoise(samples, 0f, 0.24f, 0.28f, 8f, 0.58f, 0xE01u, 25f);
            AddTone(samples, 0.02f, 0.24f, 380f, 980f, 0.18f, 8f);
            AddTone(samples, 0.14f, 0.18f, 1040f, 1320f, 0.12f, 12f);
            return FinishClip("Enemy Scribble Erase", samples);
        }

        private static AudioClip CreateGoalClip()
        {
            float[] samples = CreateSampleBuffer(1.02f);
            AddNoise(samples, 0f, 0.075f, 0.3f, 24f, 0.48f, 0x601u);
            AddNoise(samples, 0.2f, 0.07f, 0.24f, 24f, 0.5f, 0x602u);
            AddNoise(samples, 0.41f, 0.09f, 0.3f, 22f, 0.46f, 0x603u);
            AddTone(samples, 0f, 0.34f, 523.25f, 523.25f, 0.23f, 5.8f);
            AddTone(samples, 0.1f, 0.36f, 659.25f, 659.25f, 0.23f, 5.6f);
            AddTone(samples, 0.2f, 0.4f, 783.99f, 783.99f, 0.24f, 5.2f);
            AddTone(samples, 0.31f, 0.46f, 1046.5f, 1046.5f, 0.25f, 4.8f);
            AddTone(samples, 0.43f, 0.52f, 1318.51f, 1318.51f, 0.22f, 4.2f);
            AddTone(samples, 0.43f, 0.5f, 659.25f, 659.25f, 0.12f, 4.4f);
            AddTone(samples, 0.52f, 0.42f, 2093f, 2093f, 0.08f, 6f);
            return FinishClip("Goal Crayon Rise", samples);
        }

        private static AudioClip CreateStageClearClip()
        {
            float[] samples = CreateSampleBuffer(1.48f);
            AddNoise(samples, 0f, 0.08f, 0.34f, 25f, 0.48f, 0xC01u);
            AddNoise(samples, 0.25f, 0.08f, 0.3f, 24f, 0.5f, 0xC02u);
            AddNoise(samples, 0.5f, 0.08f, 0.34f, 24f, 0.48f, 0xC03u);
            AddNoise(samples, 0.76f, 0.12f, 0.42f, 20f, 0.42f, 0xC04u);

            AddTone(samples, 0f, 0.38f, 392f, 392f, 0.18f, 5.4f);
            AddTone(samples, 0.04f, 0.36f, 493.88f, 493.88f, 0.13f, 5.5f);
            AddTone(samples, 0.08f, 0.38f, 587.33f, 587.33f, 0.16f, 5.2f);

            AddTone(samples, 0.25f, 0.42f, 523.25f, 523.25f, 0.18f, 5f);
            AddTone(samples, 0.29f, 0.4f, 659.25f, 659.25f, 0.15f, 5f);
            AddTone(samples, 0.33f, 0.42f, 783.99f, 783.99f, 0.17f, 4.8f);

            AddTone(samples, 0.5f, 0.46f, 587.33f, 587.33f, 0.19f, 4.7f);
            AddTone(samples, 0.54f, 0.44f, 739.99f, 739.99f, 0.16f, 4.7f);
            AddTone(samples, 0.58f, 0.46f, 880f, 880f, 0.18f, 4.5f);

            AddTone(samples, 0.76f, 0.66f, 523.25f, 523.25f, 0.19f, 3.5f);
            AddTone(samples, 0.76f, 0.66f, 659.25f, 659.25f, 0.18f, 3.5f);
            AddTone(samples, 0.76f, 0.66f, 783.99f, 783.99f, 0.19f, 3.4f);
            AddTone(samples, 0.76f, 0.68f, 1046.5f, 1046.5f, 0.2f, 3.2f);
            AddTone(samples, 0.86f, 0.52f, 1567.98f, 1567.98f, 0.1f, 4.5f);
            AddTone(samples, 0.96f, 0.42f, 2093f, 2093f, 0.07f, 5.5f);
            return FinishClip("Stage Clear Victory Scribble", samples);
        }

        private static float[] CreateSampleBuffer(float duration)
        {
            return new float[Mathf.CeilToInt(44100f * duration)];
        }

        private static void AddTone(float[] samples, float start, float duration, float startHz, float endHz, float volume, float decay)
        {
            const int sampleRate = 44100;
            int first = Mathf.Clamp(Mathf.RoundToInt(start * sampleRate), 0, samples.Length);
            int count = Mathf.Min(Mathf.RoundToInt(duration * sampleRate), samples.Length - first);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = duration > 0f ? t / duration : 0f;
                float phase = 2f * Mathf.PI * (startHz * t + (endHz - startHz) * t * normalized * 0.5f);
                float attack = Mathf.Clamp01(t * 100f);
                float release = Mathf.Clamp01((duration - t) * 35f);
                float envelope = attack * release * Mathf.Exp(-decay * t);
                samples[first + i] += Mathf.Sin(phase) * volume * envelope;
            }
        }

        private static void AddNoise(
            float[] samples, float start, float duration, float volume, float decay,
            float smoothing, uint seed, float grainFrequency = 0f)
        {
            const int sampleRate = 44100;
            int first = Mathf.Clamp(Mathf.RoundToInt(start * sampleRate), 0, samples.Length);
            int count = Mathf.Min(Mathf.RoundToInt(duration * sampleRate), samples.Length - first);
            float filtered = 0f;
            uint state = seed;
            float retain = Mathf.Clamp01(smoothing);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                state = state * 1664525u + 1013904223u;
                float white = (state >> 8) / 16777215f * 2f - 1f;
                filtered = Mathf.Lerp(white, filtered, retain);
                float grain = grainFrequency > 0f ? 0.58f + Mathf.Sin(t * grainFrequency * Mathf.PI * 2f) * 0.42f : 1f;
                float release = Mathf.Clamp01((duration - t) * 30f);
                samples[first + i] += filtered * volume * Mathf.Exp(-decay * t) * release * grain;
            }
        }

        private static AudioClip FinishClip(string name, float[] samples)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i], -0.92f, 0.92f);
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, 44100, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateStampImpactClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.22f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            uint noiseState = 0x5A17u;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t * 19f);
                float body = Mathf.Sin(2f * Mathf.PI * (112f - t * 105f) * t) * 0.62f;
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 16777215f * 2f - 1f) * Mathf.Exp(-t * 42f) * 0.42f;
                float paperTap = Mathf.Sin(2f * Mathf.PI * 620f * t) * Mathf.Exp(-t * 55f) * 0.18f;
                samples[i] = Mathf.Clamp((body + noise + paperTap) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Clear Stamp Impact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateCelebrationChimeClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.68f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float[] starts = { 0f, 0.1f, 0.2f, 0.31f };
            float[] notes = { 659.25f, 783.99f, 987.77f, 1318.51f };
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float value = 0f;
                for (int note = 0; note < notes.Length; note++)
                {
                    float local = t - starts[note];
                    if (local < 0f) continue;
                    float envelope = Mathf.Exp(-local * 7.5f) * Mathf.Clamp01(local * 80f);
                    value += (Mathf.Sin(2f * Mathf.PI * notes[note] * local)
                        + Mathf.Sin(2f * Mathf.PI * notes[note] * 2f * local) * 0.22f) * envelope * 0.22f;
                }
                samples[i] = Mathf.Clamp(value, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Clear Celebration Chime", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
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
