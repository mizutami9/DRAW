using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Small, low-contrast ambient accents shared by non-space stage themes.
    /// Motion stays deliberately slow and local so gameplay remains dominant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThemedBackdropAmbientAnimator : MonoBehaviour
    {
        public enum AmbientTheme
        {
            Nature,
            Cave,
            Underwater,
            NightCity,
            Factory,
            Snowfield,
            AmusementPark,
            CompetitionArena
        }

        private sealed class Accent
        {
            public Transform Root;
            public SpriteRenderer Core;
            public SpriteRenderer Glow;
            public Vector2 Origin;
            public Vector2 Aspect;
            public Color Color;
            public float Phase;
            public float Speed;
            public float Scale;
            public float Travel;
            public bool Alternate;
        }

        private readonly List<Accent> accents = new List<Accent>();
        private AmbientTheme theme;
        private Rect bounds;
        private System.Random random;
        private bool configured;

        public void Configure(AmbientTheme ambientTheme, Rect backdropBounds, int seed)
        {
            theme = ambientTheme;
            bounds = backdropBounds;
            random = new System.Random(seed);
            BuildAccents();
            configured = true;
        }

        private void BuildAccents()
        {
            int count = Mathf.Clamp(
                Mathf.RoundToInt(bounds.width / 4.2f + bounds.height * 0.45f),
                18,
                72);
            if (theme == AmbientTheme.NightCity || theme == AmbientTheme.Factory)
                count = Mathf.Max(22, count);

            Color[] palette = GetPalette(theme);
            for (int i = 0; i < count; i++)
            {
                GameObject rootObject = new GameObject(GetAccentName(theme) + " " + (i + 1));
                rootObject.transform.SetParent(transform, false);
                Vector2 origin = new Vector2(
                    Range(bounds.xMin + 0.35f, bounds.xMax - 0.35f),
                    Range(bounds.yMin + 0.4f, bounds.yMax - 0.4f));
                rootObject.transform.localPosition = origin;

                Color color = palette[i % palette.Length];
                SpriteRenderer core = CreateDisc(rootObject.transform, "Crayon Accent", color, -76);
                SpriteRenderer glow = null;
                if (i % 5 == 0 && theme != AmbientTheme.Snowfield)
                {
                    glow = CreateDisc(rootObject.transform, "Faint Ambient Glow", color, -77);
                    glow.transform.localScale = Vector3.one * 2.7f;
                }

                accents.Add(new Accent
                {
                    Root = rootObject.transform,
                    Core = core,
                    Glow = glow,
                    Origin = origin,
                    Aspect = GetAspect(theme, i),
                    Color = color,
                    Phase = Range(0f, Mathf.PI * 2f),
                    Speed = GetSpeed(theme),
                    Scale = GetScale(theme),
                    Travel = GetTravel(theme),
                    Alternate = (i & 1) != 0
                });
            }
        }

        private void Update()
        {
            if (!configured) return;
            float now = Time.time;
            for (int i = 0; i < accents.Count; i++) AnimateAccent(accents[i], now);
        }

        private void AnimateAccent(Accent accent, float now)
        {
            float cycle = now * accent.Speed + accent.Phase;
            float wave = 0.5f + 0.5f * Mathf.Sin(cycle);
            float pulse = Mathf.SmoothStep(0f, 1f, wave);
            Vector2 offset = Vector2.zero;
            float alpha;
            float sizePulse = 1f;
            float rotation = 0f;

            switch (theme)
            {
                case AmbientTheme.Nature:
                    offset = new Vector2(Mathf.Sin(cycle * 0.7f) * 0.24f, Mathf.Cos(cycle * 0.53f) * 0.12f);
                    alpha = Mathf.Lerp(0.18f, 0.62f, pulse);
                    sizePulse = Mathf.Lerp(0.86f, 1.16f, pulse);
                    rotation = Mathf.Sin(cycle * 0.6f) * 5f;
                    break;
                case AmbientTheme.Cave:
                    offset = new Vector2(Mathf.Sin(cycle * 0.45f) * 0.1f, Mathf.Cos(cycle * 0.38f) * 0.14f);
                    alpha = Mathf.Lerp(0.08f, 0.3f, pulse);
                    sizePulse = Mathf.Lerp(0.9f, 1.08f, pulse);
                    break;
                case AmbientTheme.Underwater:
                    offset = new Vector2(
                        Mathf.Sin(cycle * 0.85f) * 0.22f,
                        Mathf.Repeat(cycle / (Mathf.PI * 2f), 1f) * accent.Travel - accent.Travel * 0.5f);
                    alpha = Mathf.Sin(Mathf.Repeat(cycle / (Mathf.PI * 2f), 1f) * Mathf.PI) * 0.42f;
                    sizePulse = Mathf.Lerp(0.86f, 1.12f, pulse);
                    break;
                case AmbientTheme.NightCity:
                    alpha = accent.Alternate
                        ? Mathf.Lerp(0.12f, 0.58f, pulse)
                        : Mathf.Lerp(0.28f, 0.76f, pulse);
                    sizePulse = Mathf.Lerp(0.92f, 1.1f, pulse);
                    break;
                case AmbientTheme.Factory:
                    if (accent.Alternate)
                    {
                        float rise = Mathf.Repeat(cycle / (Mathf.PI * 2f), 1f);
                        offset = new Vector2(Mathf.Sin(cycle) * 0.1f, rise * accent.Travel);
                        alpha = (1f - rise) * 0.24f;
                        sizePulse = Mathf.Lerp(0.8f, 1.25f, rise);
                    }
                    else
                    {
                        alpha = Mathf.Lerp(0.16f, 0.66f, pulse);
                        sizePulse = Mathf.Lerp(0.92f, 1.08f, pulse);
                    }
                    break;
                case AmbientTheme.Snowfield:
                    offset = new Vector2(
                        Mathf.Sin(cycle * 0.7f) * 0.28f,
                        accent.Travel * 0.5f - Mathf.Repeat(cycle / (Mathf.PI * 2f), 1f) * accent.Travel);
                    alpha = 0.46f;
                    rotation = cycle * 12f;
                    break;
                case AmbientTheme.AmusementPark:
                    offset = new Vector2(Mathf.Sin(cycle * 0.45f) * 0.12f, Mathf.Cos(cycle * 0.7f) * 0.15f);
                    alpha = Mathf.Lerp(0.28f, 0.7f, pulse);
                    sizePulse = Mathf.Lerp(0.9f, 1.12f, pulse);
                    rotation = Mathf.Sin(cycle * 0.5f) * 9f;
                    break;
                default:
                    offset = new Vector2(
                        Mathf.Repeat(cycle / (Mathf.PI * 2f), 1f) * accent.Travel - accent.Travel * 0.5f,
                        Mathf.Sin(cycle * 0.65f) * 0.08f);
                    alpha = Mathf.Lerp(0.1f, 0.34f, pulse);
                    break;
            }

            accent.Root.localPosition = accent.Origin + offset;
            accent.Root.localRotation = Quaternion.Euler(0f, 0f, rotation);
            accent.Root.localScale = new Vector3(
                accent.Scale * accent.Aspect.x * sizePulse,
                accent.Scale * accent.Aspect.y * sizePulse,
                1f);
            accent.Core.color = WithAlpha(accent.Color, alpha);
            if (accent.Glow != null)
                accent.Glow.color = WithAlpha(accent.Color, alpha * 0.18f);
        }

        private static Color[] GetPalette(AmbientTheme value)
        {
            switch (value)
            {
                case AmbientTheme.Nature:
                    return new[] { new Color(0.95f, 0.76f, 0.22f), new Color(0.56f, 0.82f, 0.35f), new Color(1f, 0.68f, 0.76f) };
                case AmbientTheme.Cave:
                    return new[] { new Color(0.64f, 0.72f, 0.78f), new Color(0.78f, 0.68f, 0.52f), new Color(0.5f, 0.62f, 0.72f) };
                case AmbientTheme.Underwater:
                    return new[] { new Color(0.62f, 0.92f, 1f), new Color(0.46f, 0.8f, 0.94f), new Color(0.78f, 1f, 0.94f) };
                case AmbientTheme.NightCity:
                    return new[] { new Color(1f, 0.82f, 0.28f), new Color(0.34f, 0.82f, 1f), new Color(1f, 0.48f, 0.72f) };
                case AmbientTheme.Factory:
                    return new[] { new Color(1f, 0.58f, 0.16f), new Color(0.58f, 0.78f, 0.84f), new Color(0.94f, 0.82f, 0.42f) };
                case AmbientTheme.Snowfield:
                    return new[] { new Color(0.92f, 0.99f, 1f), new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 1f) };
                case AmbientTheme.AmusementPark:
                    return new[] { new Color(1f, 0.48f, 0.58f), new Color(0.3f, 0.78f, 0.94f), new Color(1f, 0.76f, 0.22f), new Color(0.55f, 0.86f, 0.4f) };
                default:
                    return new[] { new Color(0.86f, 0.7f, 0.42f), new Color(0.66f, 0.76f, 0.78f), new Color(0.92f, 0.82f, 0.58f) };
            }
        }

        private float GetSpeed(AmbientTheme value)
        {
            switch (value)
            {
                case AmbientTheme.NightCity: return Range(0.45f, 1.15f);
                case AmbientTheme.Cave: return Range(0.28f, 0.62f);
                case AmbientTheme.Snowfield: return Range(0.34f, 0.72f);
                default: return Range(0.42f, 0.95f);
            }
        }

        private float GetScale(AmbientTheme value)
        {
            switch (value)
            {
                case AmbientTheme.Underwater: return Range(0.055f, 0.14f);
                case AmbientTheme.Snowfield: return Range(0.035f, 0.09f);
                case AmbientTheme.NightCity: return Range(0.04f, 0.085f);
                default: return Range(0.045f, 0.11f);
            }
        }

        private float GetTravel(AmbientTheme value)
        {
            switch (value)
            {
                case AmbientTheme.Underwater: return Range(0.8f, 1.8f);
                case AmbientTheme.Snowfield: return Range(1f, 2f);
                case AmbientTheme.Factory: return Range(0.5f, 1.1f);
                case AmbientTheme.CompetitionArena: return Range(0.5f, 1.2f);
                default: return Range(0.2f, 0.5f);
            }
        }

        private static Vector2 GetAspect(AmbientTheme value, int index)
        {
            if (value == AmbientTheme.Snowfield) return new Vector2(0.65f, 1.25f);
            if (value == AmbientTheme.AmusementPark) return index % 2 == 0
                ? new Vector2(1.35f, 0.65f)
                : new Vector2(0.7f, 1.3f);
            if (value == AmbientTheme.NightCity || value == AmbientTheme.Factory)
                return new Vector2(1.15f, 0.75f);
            return Vector2.one;
        }

        private static string GetAccentName(AmbientTheme value)
        {
            switch (value)
            {
                case AmbientTheme.Nature: return "Floating Pollen";
                case AmbientTheme.Cave: return "Cave Dust Glint";
                case AmbientTheme.Underwater: return "Tiny Bubble";
                case AmbientTheme.NightCity: return "Distant Window Light";
                case AmbientTheme.Factory: return "Factory Steam And Lamp";
                case AmbientTheme.Snowfield: return "Gentle Snowflake";
                case AmbientTheme.AmusementPark: return "Floating Confetti";
                default: return "Arena Dust Sparkle";
            }
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

        private float Range(float minimum, float maximum)
        {
            if (maximum <= minimum) return minimum;
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
