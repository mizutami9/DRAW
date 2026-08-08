using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class DrawFeedbackController : MonoBehaviour
    {
        [SerializeField] private RectTransform drawArea;
        [SerializeField] private RectTransform dustRoot;
        [SerializeField] private bool soundEnabled = true;
        [SerializeField] private bool strokeSoundEnabled = true;
        [SerializeField] private int dustPoolSize = 48;
        [SerializeField] private float dustLifetime = 0.34f;

        private readonly List<Dust> dustPool = new List<Dust>();
        private bool active;
        private bool stroking;
        private bool erasing;
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

            BuildDustPool();
        }

        private void Update()
        {
            for (int i = 0; i < dustPool.Count; i++)
            {
                Dust dust = dustPool[i];
                if (!IsDustValid(dust) || !dust.Image.gameObject.activeSelf)
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
            erasing = false;
            if (strokeSoundEnabled)
            {
                Play(SfxId.DrawPenStart);
            }
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

            if (strokeSoundEnabled && soundTimer <= 0f)
            {
                soundTimer = 0.055f;
                Play(SfxId.DrawPenLoop);
            }
        }

        public void Erase(Vector2 point)
        {
            if (!active)
            {
                return;
            }

            SpawnDust(point, new Color(0.95f, 0.92f, 0.82f, 1f), 6);
            stroking = true;
            erasing = true;
            if (strokeSoundEnabled && soundTimer <= 0f)
            {
                soundTimer = 0.07f;
                Play(SfxId.DrawEraserLoop);
            }
        }

        public void EndStroke()
        {
            if (!stroking)
            {
                return;
            }

            stroking = false;
            if (strokeSoundEnabled && active && stampTimer <= 0f)
            {
                stampTimer = 0.18f;
                Play(erasing ? SfxId.DrawEraseComplete : SfxId.DrawPenEnd);
            }
            erasing = false;
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
                Play(SfxId.UiButtonPress);
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
                if (!IsDustValid(dust))
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
            RemoveDestroyedDust();
            if (dustPool.Count == 0)
            {
                BuildDustPool();
            }

            for (int i = 0; i < dustPool.Count; i++)
            {
                if (IsDustValid(dustPool[i]) && !dustPool[i].Image.gameObject.activeSelf)
                {
                    return dustPool[i];
                }
            }

            return dustPool.Count > 0 && IsDustValid(dustPool[0]) ? dustPool[0] : null;
        }

        private void RemoveDestroyedDust()
        {
            for (int i = dustPool.Count - 1; i >= 0; i--)
            {
                if (!IsDustValid(dustPool[i]))
                {
                    dustPool.RemoveAt(i);
                }
            }
        }

        private static bool IsDustValid(Dust dust)
        {
            return dust != null && dust.Image != null && dust.Rect != null;
        }

        private void Play(SfxId id)
        {
            if (!soundEnabled)
            {
                return;
            }
            GameSfx.Play(id);
        }
    }
}
