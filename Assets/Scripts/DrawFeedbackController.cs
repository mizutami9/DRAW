using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class DrawFeedbackController : MonoBehaviour
    {
        [SerializeField] private RectTransform drawArea;
        [SerializeField] private RectTransform dustRoot;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool soundEnabled = true;
        [SerializeField] private int dustPoolSize = 48;
        [SerializeField] private float dustLifetime = 0.34f;

        private readonly List<Dust> dustPool = new List<Dust>();
        private AudioClip pencilLoop;
        private AudioClip pencilTap;
        private AudioClip eraser;
        private AudioClip stamp;
        private bool active;
        private bool stroking;
        private float soundTimer;
        private float stampTimer;
        private float buttonTimer;

        private sealed class Dust
        {
            public RectTransform Rect;
            public Image Image;
            public float Age;
            public float Lifetime;
            public Vector2 Velocity;
            public float Spin;
        }

        private void Awake()
        {
            if (dustRoot == null)
            {
                dustRoot = drawArea;
            }

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = 0.35f;

            pencilLoop = CreateNoiseClip("PencilScratch", 0.09f, 0.22f, 0.55f);
            pencilTap = CreateToneClip("PencilTap", 0.045f, 940f, 0.22f, 0.16f);
            eraser = CreateNoiseClip("EraserRub", 0.08f, 0.42f, 0.28f);
            stamp = CreateToneClip("PaperStamp", 0.08f, 220f, 0.34f, 0.3f);
            BuildDustPool();
        }

        private void Update()
        {
            for (int i = 0; i < dustPool.Count; i++)
            {
                Dust dust = dustPool[i];
                if (dust.Image == null || !dust.Image.gameObject.activeSelf)
                {
                    continue;
                }

                dust.Age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(dust.Age / dust.Lifetime);
                dust.Rect.anchoredPosition += dust.Velocity * Time.unscaledDeltaTime;
                dust.Rect.localRotation *= Quaternion.Euler(0f, 0f, dust.Spin * Time.unscaledDeltaTime);
                Color color = dust.Image.color;
                color.a = Mathf.Lerp(0.34f, 0f, t);
                dust.Image.color = color;
                if (t >= 1f)
                {
                    dust.Image.gameObject.SetActive(false);
                }
            }

            if (soundTimer > 0f)
            {
                soundTimer -= Time.unscaledDeltaTime;
            }

            if (stampTimer > 0f)
            {
                stampTimer -= Time.unscaledDeltaTime;
            }

            if (buttonTimer > 0f)
            {
                buttonTimer -= Time.unscaledDeltaTime;
            }
        }

        public void SetActive(bool value)
        {
            active = value;
            if (!active)
            {
                EndStroke();
            }
        }

        public void BeginStroke(Vector2 point, Color color)
        {
            if (!active)
            {
                return;
            }

            stroking = true;
            Play(pencilTap, 0.45f, Random.Range(0.92f, 1.08f));
            SpawnDust(point, color, 4);
        }

        public void DrawSegment(Vector2 start, Vector2 end, Color color)
        {
            if (!active || !stroking)
            {
                return;
            }

            Vector2 mid = (start + end) * 0.5f;
            float length = Vector2.Distance(start, end);
            int count = Mathf.Clamp(Mathf.RoundToInt(length / 18f), 1, 4);
            SpawnDust(mid, color, count);

            if (soundTimer <= 0f)
            {
                soundTimer = 0.055f;
                Play(pencilLoop, 0.22f, Random.Range(0.88f, 1.18f));
            }
        }

        public void Erase(Vector2 point)
        {
            if (!active)
            {
                return;
            }

            SpawnDust(point, new Color(0.95f, 0.92f, 0.82f, 1f), 6);
            if (soundTimer <= 0f)
            {
                soundTimer = 0.07f;
                Play(eraser, 0.26f, Random.Range(0.88f, 1.08f));
            }
        }

        public void EndStroke()
        {
            if (!stroking)
            {
                return;
            }

            stroking = false;
            if (active && stampTimer <= 0f)
            {
                stampTimer = 0.18f;
                Play(stamp, 0.16f, Random.Range(0.96f, 1.04f));
            }
        }

        public void ButtonPress()
        {
            if (!active)
            {
                return;
            }

            if (buttonTimer <= 0f)
            {
                buttonTimer = 0.08f;
                Play(stamp, 0.2f, Random.Range(0.94f, 1.08f));
            }
        }

        private void BuildDustPool()
        {
            if (dustRoot == null)
            {
                return;
            }

            for (int i = 0; i < dustPoolSize; i++)
            {
                GameObject dot = new GameObject("PencilDust");
                dot.transform.SetParent(dustRoot, false);
                Image image = dot.AddComponent<Image>();
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(3f, 3f);
                dot.SetActive(false);
                dustPool.Add(new Dust { Rect = rect, Image = image, Lifetime = dustLifetime });
            }
        }

        private void SpawnDust(Vector2 point, Color baseColor, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Dust dust = GetDust();
                if (dust == null)
                {
                    return;
                }

                float size = Random.Range(1.6f, 4.2f);
                dust.Rect.anchoredPosition = point + Random.insideUnitCircle * 7f;
                dust.Rect.sizeDelta = new Vector2(size, size);
                dust.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
                dust.Velocity = Random.insideUnitCircle * Random.Range(8f, 24f);
                dust.Spin = Random.Range(-55f, 55f);
                dust.Age = 0f;
                dust.Lifetime = dustLifetime * Random.Range(0.75f, 1.3f);
                Color color = Color.Lerp(baseColor, new Color(0.2f, 0.14f, 0.08f, 1f), 0.42f);
                color.a = Random.Range(0.22f, 0.38f);
                dust.Image.color = color;
                dust.Image.gameObject.SetActive(true);
            }
        }

        private Dust GetDust()
        {
            for (int i = 0; i < dustPool.Count; i++)
            {
                if (!dustPool[i].Image.gameObject.activeSelf)
                {
                    return dustPool[i];
                }
            }

            return dustPool.Count > 0 ? dustPool[0] : null;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (!soundEnabled || audioSource == null || clip == null)
            {
                return;
            }

            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip, volume);
        }

        private static AudioClip CreateNoiseClip(string name, float duration, float volume, float damping)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float envelope = Mathf.Pow(1f - t, damping);
                data[i] = Random.Range(-1f, 1f) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateToneClip(string name, float duration, float frequency, float volume, float damping)
        {
            const int sampleRate = 22050;
            int samples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = i / (float)samples;
                float envelope = Mathf.Pow(1f - normalized, damping);
                data[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * volume * envelope;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
