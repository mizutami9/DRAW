using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Shared runtime-only drawing assets used by stage and UI components.
    /// Centralizing these prevents every stage from allocating identical
    /// materials, textures, and sprites.
    /// </summary>
    internal static class DoodleRuntimeAssets
    {
        private const int CircleTextureSize = 48;

        private static Material lineMaterial;
        private static Sprite squareSprite;
        private static Sprite circleSprite;
        private static Font handwrittenFont;

        internal static Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    lineMaterial = new Material(shader)
                    {
                        name = "Shared Doodle Line Material",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                return lineMaterial;
            }
        }

        internal static Sprite SquareSprite
        {
            get
            {
                if (squareSprite == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    squareSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        texture.width);
                    squareSprite.name = "Shared Doodle Square";
                    squareSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return squareSprite;
            }
        }

        internal static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null) circleSprite = CreateCircleSprite();
                return circleSprite;
            }
        }

        internal static Font HandwrittenFont
        {
            get
            {
                if (handwrittenFont == null) handwrittenFont = FindHandwrittenFont();
                return handwrittenFont;
            }
        }

        private static Sprite CreateCircleSprite()
        {
            Texture2D texture = new Texture2D(
                CircleTextureSize,
                CircleTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "Shared Doodle Circle Texture",
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[CircleTextureSize * CircleTextureSize];
            Vector2 center = Vector2.one * (CircleTextureSize - 1) * 0.5f;
            float radius = CircleTextureSize * 0.46f;
            float radiusSquared = radius * radius;
            for (int y = 0; y < CircleTextureSize; y++)
            {
                for (int x = 0; x < CircleTextureSize; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    byte alpha = delta.sqrMagnitude <= radiusSquared ? (byte)255 : (byte)0;
                    pixels[y * CircleTextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
                new Vector2(0.5f, 0.5f),
                CircleTextureSize);
            sprite.name = "Shared Doodle Circle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Font FindHandwrittenFont()
        {
            Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < fonts.Length; i++)
            {
                Font font = fonts[i];
                if (font != null && font.name.IndexOf("Yomogi", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return font;
                }
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
