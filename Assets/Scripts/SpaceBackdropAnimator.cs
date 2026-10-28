using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Lightweight, code-drawn motion layered over the static crayon space sky.
    /// It is decorative only and never participates in physics or networking.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceBackdropAnimator : MonoBehaviour
    {
        private sealed class Twinkle
        {
            public Transform Root;
            public SpriteRenderer Core;
            public SpriteRenderer Glow;
            public LineRenderer[] Rays;
            public Color Color;
            public float Phase;
            public float Speed;
            public float BaseScale;
        }

        private sealed class ShootingStar
        {
            public Transform Root;
            public SpriteRenderer Head;
            public LineRenderer Trail;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public float SpawnAt;
            public bool Active;
        }

        private readonly List<Twinkle> twinkles = new List<Twinkle>();
        private readonly List<ShootingStar> shootingStars = new List<ShootingStar>();
        private Rect bounds;
        private System.Random random;
        private Camera cachedCamera;
        private bool configured;

        public void Configure(Rect backdropBounds, int seed)
        {
            bounds = backdropBounds;
            random = new System.Random(seed);
            BuildTwinkles();
            BuildShootingStars();
            configured = true;
        }

        private void BuildTwinkles()
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(bounds.width / 3.2f + bounds.height * 0.8f), 28, 92);
            Color[] palette =
            {
                new Color(1f, 0.88f, 0.3f),
                new Color(0.72f, 0.9f, 1f),
                new Color(1f, 0.62f, 0.88f),
                new Color(0.65f, 1f, 0.84f)
            };
            for (int i = 0; i < count; i++)
            {
                GameObject rootObject = new GameObject("Animated Crayon Star " + (i + 1));
                rootObject.transform.SetParent(transform, false);
                rootObject.transform.localPosition = new Vector3(
                    Range(bounds.xMin + 0.4f, bounds.xMax - 0.4f),
                    Range(bounds.yMin + 0.45f, bounds.yMax - 0.4f),
                    0f);

                Color color = palette[i % palette.Length];
                float scale = Range(0.055f, 0.14f);
                SpriteRenderer glow = CreateDisc(rootObject.transform, "Soft Glow", color, -87);
                glow.transform.localScale = Vector3.one * 3.2f;
                SpriteRenderer core = CreateDisc(rootObject.transform, "Bright Star", color, -85);

                LineRenderer[] rays = System.Array.Empty<LineRenderer>();
                if (i % 3 == 0)
                {
                    CreateRay(rootObject.transform, "Horizontal Crayon Ray", Vector2.left, Vector2.right, color);
                    CreateRay(rootObject.transform, "Vertical Crayon Ray", Vector2.down, Vector2.up, color);
                    rays = rootObject.GetComponentsInChildren<LineRenderer>();
                }

                twinkles.Add(new Twinkle
                {
                    Root = rootObject.transform,
                    Core = core,
                    Glow = glow,
                    Rays = rays,
                    Color = color,
                    Phase = Range(0f, Mathf.PI * 2f),
                    Speed = Range(1.15f, 3.1f),
                    BaseScale = scale
                });
            }
        }

        private void BuildShootingStars()
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject rootObject = new GameObject("Animated Shooting Star " + (i + 1));
                rootObject.transform.SetParent(transform, false);
                SpriteRenderer head = CreateDisc(rootObject.transform, "Shooting Star Head", Color.white, -82);
                head.transform.localScale = Vector3.one * 0.15f;

                GameObject trailObject = new GameObject("Colored Pencil Trail");
                trailObject.transform.SetParent(rootObject.transform, false);
                LineRenderer trail = trailObject.AddComponent<LineRenderer>();
                trail.useWorldSpace = false;
                trail.positionCount = 3;
                trail.startWidth = 0.085f;
                trail.endWidth = 0.018f;
                trail.numCapVertices = 5;
                trail.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                trail.sortingOrder = -83;

                ShootingStar star = new ShootingStar
                {
                    Root = rootObject.transform,
                    Head = head,
                    Trail = trail,
                    SpawnAt = Time.time + 0.8f + i * 1.7f,
                    Active = false
                };
                rootObject.SetActive(false);
                shootingStars.Add(star);
            }
        }

        private void Update()
        {
            if (!configured) return;
            AnimateTwinkles();
            AnimateShootingStars();
        }

        private void AnimateTwinkles()
        {
            float now = Time.time;
            for (int i = 0; i < twinkles.Count; i++)
            {
                Twinkle star = twinkles[i];
                if (star.Root == null) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(now * star.Speed + star.Phase);
                float sparkle = Mathf.SmoothStep(0f, 1f, wave);
                float scale = star.BaseScale * Mathf.Lerp(0.72f, 1.5f, sparkle);
                star.Root.localScale = new Vector3(scale, scale, 1f);
                star.Core.color = WithAlpha(star.Color, Mathf.Lerp(0.38f, 1f, sparkle));
                star.Glow.color = WithAlpha(star.Color, Mathf.Lerp(0.025f, 0.2f, sparkle));
                for (int ray = 0; ray < star.Rays.Length; ray++)
                {
                    Color rayColor = WithAlpha(star.Color, Mathf.Lerp(0.08f, 0.72f, sparkle));
                    star.Rays[ray].startColor = rayColor;
                    star.Rays[ray].endColor = WithAlpha(rayColor, rayColor.a * 0.25f);
                }
            }
        }

        private void AnimateShootingStars()
        {
            float now = Time.time;
            for (int i = 0; i < shootingStars.Count; i++)
            {
                ShootingStar star = shootingStars[i];
                if (!star.Active)
                {
                    if (now >= star.SpawnAt) SpawnShootingStar(star, i);
                    continue;
                }

                star.Age += Time.deltaTime;
                star.Root.localPosition += (Vector3)(star.Velocity * Time.deltaTime);
                float life = Mathf.Clamp01(star.Age / Mathf.Max(0.01f, star.Lifetime));
                float fade = Mathf.Sin(life * Mathf.PI);
                Color headColor = Color.Lerp(new Color(0.35f, 0.88f, 1f), new Color(1f, 0.62f, 0.88f), i / 2f);
                star.Head.color = WithAlpha(headColor, fade);
                Vector2 tail = -star.Velocity.normalized * Mathf.Lerp(1.8f, 3.4f, fade);
                Vector2 bend = tail * 0.52f + Vector2.up * 0.12f;
                star.Trail.SetPosition(0, Vector3.zero);
                star.Trail.SetPosition(1, bend);
                star.Trail.SetPosition(2, tail);
                star.Trail.startColor = WithAlpha(Color.white, fade * 0.95f);
                star.Trail.endColor = WithAlpha(headColor, 0f);
                if (star.Age >= star.Lifetime) RetireShootingStar(star);
            }
        }

        private void SpawnShootingStar(ShootingStar star, int index)
        {
            if (cachedCamera == null) cachedCamera = Camera.main;
            float visibleLeft = bounds.xMin + 1f;
            float visibleRight = bounds.xMax - 1f;
            float visibleBottom = bounds.yMin + 1f;
            float visibleTop = bounds.yMax - 1f;
            if (cachedCamera != null && cachedCamera.orthographic)
            {
                float halfHeight = cachedCamera.orthographicSize;
                float halfWidth = halfHeight * cachedCamera.aspect;
                visibleLeft = Mathf.Max(visibleLeft, cachedCamera.transform.position.x - halfWidth);
                visibleRight = Mathf.Min(visibleRight, cachedCamera.transform.position.x + halfWidth);
                visibleBottom = Mathf.Max(visibleBottom, cachedCamera.transform.position.y - halfHeight);
                visibleTop = Mathf.Min(visibleTop, cachedCamera.transform.position.y + halfHeight);
            }
            if (visibleRight <= visibleLeft) { visibleLeft = bounds.xMin; visibleRight = bounds.xMax; }
            if (visibleTop <= visibleBottom) { visibleBottom = bounds.yMin; visibleTop = bounds.yMax; }

            bool travelRight = index % 2 == 0;
            float x = travelRight ? visibleLeft - 0.4f : visibleRight + 0.4f;
            float y = Range(visibleBottom + (visibleTop - visibleBottom) * 0.45f, visibleTop);
            float speed = Range(5.5f, 8.5f);
            star.Root.localPosition = new Vector3(x, y, 0f);
            star.Velocity = new Vector2(travelRight ? speed : -speed, -Range(1.6f, 3.2f));
            star.Age = 0f;
            star.Lifetime = Range(1.25f, 2.1f);
            star.Active = true;
            star.Root.gameObject.SetActive(true);
        }

        private void RetireShootingStar(ShootingStar star)
        {
            star.Active = false;
            star.Root.gameObject.SetActive(false);
            star.SpawnAt = Time.time + Range(2.4f, 5.8f);
        }

        private static SpriteRenderer CreateDisc(Transform parent, string name, Color color, int sortingOrder)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void CreateRay(Transform parent, string name, Vector2 from, Vector2 to, Color color)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            LineRenderer line = item.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = line.endWidth = 0.16f;
            line.numCapVertices = 4;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = line.endColor = color;
            line.sortingOrder = -86;
        }

        private float Range(float minimum, float maximum)
        {
            if (maximum <= minimum) return minimum;
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
