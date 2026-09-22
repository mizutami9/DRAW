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
        private static Sprite dotGridSprite;
        private static Sprite paperCardSprite;
        private static Sprite[] titleMenuIconSprites;
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

        internal static Sprite PaperCardSprite
        {
            get
            {
                if (paperCardSprite == null)
                {
                    // Generated at runtime so every screen uses the same deterministic,
                    // nine-sliced hand-cut paper instead of a hard digital rectangle.
                    paperCardSprite = CreatePaperCardSprite();
                }
                return paperCardSprite;
            }
        }

        // Kept as an alias while older UI polishers migrate to the shared name.
        internal static Sprite TitlePaperCardSprite => PaperCardSprite;

        internal static Sprite DotGridSprite
        {
            get
            {
                if (dotGridSprite == null) dotGridSprite = CreateDotGridSprite();
                return dotGridSprite;
            }
        }

        internal static Sprite GetTitleMenuIconSprite(int iconIndex)
        {
            const int iconCount = 21;
            if (titleMenuIconSprites == null) titleMenuIconSprites = new Sprite[iconCount];
            int safeIndex = Mathf.Clamp(iconIndex, 0, iconCount - 1);
            if (titleMenuIconSprites[safeIndex] == null)
                titleMenuIconSprites[safeIndex] = CreateTitleMenuIconSprite(safeIndex);
            return titleMenuIconSprites[safeIndex];
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

        private static Sprite CreateDotGridSprite()
        {
            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Shared Notebook Dot Grid Texture",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            Color32 dot = new Color32(92, 150, 184, 74);
            int center = size / 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = x - center;
                    int dy = y - center;
                    pixels[y * size + x] = dx * dx + dy * dy <= 2
                        ? dot
                        : new Color32(255, 255, 255, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = "Shared Notebook Dot Grid";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite CreatePaperCardSprite()
        {
            const int width = 192;
            const int height = 192;
            // Keep the transparent fringe narrow: many in-game buttons are only
            // 42 px tall, so a large inset would make their coloured paper look much
            // smaller even though the RectTransform itself had not changed.
            const int paperInset = 5;
            const int shadowSpread = 4;
            const int shadowOffsetX = 1;
            const int shadowOffsetY = -1;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Shared Hand Cut Paper Texture",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            int[] lower = new int[width];
            int[] upper = new int[width];
            int[] left = new int[height];
            int[] right = new int[height];
            for (int x = 0; x < width; x++)
            {
                lower[x] = paperInset + Mathf.RoundToInt(GetPaperEdgeOffset(x, 17));
                upper[x] = height - 1 - paperInset
                    + Mathf.RoundToInt(GetPaperEdgeOffset(x, 43));
            }
            for (int y = 0; y < height; y++)
            {
                left[y] = paperInset + Mathf.RoundToInt(GetPaperEdgeOffset(y, 71));
                right[y] = width - 1 - paperInset
                    + Mathf.RoundToInt(GetPaperEdgeOffset(y, 101));
            }

            bool[] paperMask = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    paperMask[y * width + x] = y >= lower[x] && y <= upper[x]
                        && x >= left[y] && x <= right[y];
                }
            }

            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = paperMask[y * width + x];
                    if (!inside)
                    {
                        byte shadowAlpha = GetPaperShadowAlpha(
                            paperMask,
                            width,
                            height,
                            x - shadowOffsetX,
                            y - shadowOffsetY,
                            shadowSpread);
                        pixels[y * width + x] = shadowAlpha > 0
                            ? new Color32(72, 66, 55, shadowAlpha)
                            : new Color32(255, 255, 255, 0);
                        continue;
                    }

                    int edgeDistance = Math.Min(Math.Min(y - lower[x], upper[x] - y),
                        Math.Min(x - left[y], right[y] - x));
                    if (edgeDistance <= 0)
                    {
                        // A single incomplete pencil pass: readable, but never a thick
                        // black UI frame. Varying alpha lets the paper edge show through.
                        int pencil = 112 + PaperHash(x, y, 13) % 31;
                        int alpha = 178 + PaperHash(x, y, 29) % 55;
                        pixels[y * width + x] = new Color32(
                            (byte)pencil, (byte)pencil, (byte)pencil, (byte)alpha);
                    }
                    else if (edgeDistance == 1)
                    {
                        // Slightly darker exposed paper fibres suggest a cut/torn edge.
                        int cutEdge = 184 + PaperHash(x, y, 47) % 25;
                        pixels[y * width + x] = new Color32(
                            (byte)cutEdge, (byte)cutEdge, (byte)cutEdge, 248);
                    }
                    else
                    {
                        float broadMottle = Mathf.PerlinNoise(
                            x * 0.026f + 5.7f, y * 0.031f + 2.1f) - 0.5f;
                        float fineMottle = Mathf.PerlinNoise(
                            x * 0.137f + 19.3f, y * 0.119f + 7.4f) - 0.5f;
                        int shade = Mathf.RoundToInt(246f + broadMottle * 9f + fineMottle * 3f);

                        // Short, sparse marks read as pulp/fibres without making labels noisy.
                        int fibreCell = PaperHash(x / 6, y, 83);
                        bool horizontalFibre = fibreCell % 263 == 0 && x % 6 < 5;
                        int speck = PaperHash(x, y, 131);
                        if (horizontalFibre) shade -= 8;
                        else if (speck % 487 == 0) shade -= 14;
                        else if (speck % 431 == 0) shade += 7;

                        if (edgeDistance == 2) shade -= 8;
                        byte paperShade = (byte)Mathf.Clamp(shade, 218, 253);
                        pixels[y * width + x] = new Color32(
                            paperShade, paperShade, paperShade, 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
                new Vector4(18f, 18f, 18f, 18f));
            sprite.name = "Shared Hand Cut Paper";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float GetPaperEdgeOffset(int position, int seed)
        {
            // Interpolated, low-frequency cuts create a handmade silhouette. A small
            // secondary wave supplies paper fibres without the old furry/saw-tooth edge.
            const int segmentLength = 13;
            int segment = position / segmentLength;
            float t = (position % segmentLength) / (float)segmentLength;
            t = t * t * (3f - 2f * t);
            float first = PaperSignedHash(segment, seed);
            float second = PaperSignedHash(segment + 1, seed);
            float cut = Mathf.Lerp(first, second, t) * 1.15f;
            float longBend = Mathf.Sin(position * 0.027f + seed * 0.13f) * 0.52f;
            float fibre = Mathf.Sin(position * 0.19f + seed * 0.31f) * 0.16f;
            return Mathf.Clamp(cut + longBend + fibre, -1.8f, 1.8f);
        }

        private static float PaperSignedHash(int value, int seed)
        {
            int hash = PaperHash(value, seed, 193);
            return (hash & 0xffff) / 32767.5f - 1f;
        }

        private static int PaperHash(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 69069);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (int)(hash & 0x7fffffff);
            }
        }

        private static byte GetPaperShadowAlpha(
            bool[] mask,
            int width,
            int height,
            int sourceX,
            int sourceY,
            int spread)
        {
            int closestSquared = int.MaxValue;
            for (int offsetY = -spread; offsetY <= spread; offsetY++)
            {
                int sampleY = sourceY + offsetY;
                if (sampleY < 0 || sampleY >= height) continue;
                for (int offsetX = -spread; offsetX <= spread; offsetX++)
                {
                    int squared = offsetX * offsetX + offsetY * offsetY;
                    if (squared > spread * spread || squared >= closestSquared) continue;
                    int sampleX = sourceX + offsetX;
                    if (sampleX < 0 || sampleX >= width) continue;
                    if (mask[sampleY * width + sampleX]) closestSquared = squared;
                }
            }

            if (closestSquared == int.MaxValue) return 0;
            float distance = Mathf.Sqrt(closestSquared);
            float softness = Mathf.Clamp01(1f - distance / (spread + 0.65f));
            // At most ~18% opacity, with a broad quadratic falloff: paper hovering
            // a millimetre above the notebook rather than a hard UI drop shadow.
            return (byte)Mathf.RoundToInt(46f * softness * softness);
        }

        private static Sprite CreateTitleMenuIconSprite(int iconIndex)
        {
            const int size = 48;
            Color32[] pixels = new Color32[size * size];
            switch (iconIndex)
            {
                case 0: // Filled play triangle.
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(11, 7), new Vector2Int(40, 24), new Vector2Int(11, 41)
                    });
                    break;
                case 1: // Three-player group silhouette.
                    FillCircle(pixels, size, 24, 34, 7, true);
                    FillCircle(pixels, size, 11, 31, 5, true);
                    FillCircle(pixels, size, 37, 31, 5, true);
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(12, 7), new Vector2Int(14, 22),
                        new Vector2Int(20, 27), new Vector2Int(28, 27),
                        new Vector2Int(34, 22), new Vector2Int(36, 7)
                    });
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(3, 8), new Vector2Int(5, 21),
                        new Vector2Int(11, 25), new Vector2Int(16, 21), new Vector2Int(17, 8)
                    });
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(31, 8), new Vector2Int(32, 21),
                        new Vector2Int(37, 25), new Vector2Int(43, 21), new Vector2Int(45, 8)
                    });
                    break;
                case 2: // Outlined pencil with a solid graphite tip.
                    DrawThickLine(pixels, size, new Vector2(12f, 11f), new Vector2(36f, 35f), 11f, true);
                    DrawThickLine(pixels, size, new Vector2(14f, 13f), new Vector2(34f, 33f), 5f, false);
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(5, 5), new Vector2Int(16, 9), new Vector2Int(9, 16)
                    });
                    DrawThickLine(pixels, size, new Vector2(32f, 38f), new Vector2(40f, 30f), 4f, true);
                    break;
                case 3: // Solid gear with a cut-out hub.
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = Mathf.PI * 2f * i / 8f;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        DrawThickLine(pixels, size, new Vector2(24f, 24f) + direction * 10f,
                            new Vector2(24f, 24f) + direction * 19f, 8f, true);
                    }
                    FillCircle(pixels, size, 24, 24, 14, true);
                    FillCircle(pixels, size, 24, 24, 5, false);
                    break;
                case 4: // Door and outward arrow.
                    DrawThickLine(pixels, size, new Vector2(8f, 6f), new Vector2(8f, 42f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(8f, 42f), new Vector2(29f, 42f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(29f, 42f), new Vector2(29f, 6f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(29f, 6f), new Vector2(8f, 6f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(18f, 24f), new Vector2(43f, 24f), 5f, true);
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(43, 24), new Vector2Int(33, 34), new Vector2Int(33, 14)
                    });
                    FillCircle(pixels, size, 14, 23, 2, true);
                    break;
                case 5: // Globe.
                    DrawCircleRing(pixels, size, 24, 24, 18, 3);
                    DrawThickLine(pixels, size, new Vector2(24f, 6f), new Vector2(24f, 42f), 2f, true);
                    DrawThickLine(pixels, size, new Vector2(6f, 24f), new Vector2(42f, 24f), 2.5f, true);
                    DrawThickLine(pixels, size, new Vector2(9f, 16f), new Vector2(39f, 16f), 2f, true);
                    DrawThickLine(pixels, size, new Vector2(9f, 32f), new Vector2(39f, 32f), 2f, true);
                    break;
                case 6: // Checklist sheet.
                    DrawThickLine(pixels, size, new Vector2(8f, 5f), new Vector2(8f, 43f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(8f, 43f), new Vector2(40f, 43f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(40f, 43f), new Vector2(40f, 5f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(40f, 5f), new Vector2(8f, 5f), 3f, true);
                    for (int i = 0; i < 3; i++)
                    {
                        int y = 33 - i * 10;
                        DrawRectOutline(pixels, size, 13, y - 3, 19, y + 3, 2);
                        DrawThickLine(pixels, size, new Vector2(24f, y), new Vector2(35f, y), 2.5f, true);
                    }
                    break;
                case 7: // Pair of music notes.
                    DrawThickLine(pixels, size, new Vector2(18f, 14f), new Vector2(18f, 36f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(18f, 36f), new Vector2(35f, 40f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(35f, 17f), new Vector2(35f, 40f), 4f, true);
                    FillCircle(pixels, size, 13, 13, 7, true);
                    FillCircle(pixels, size, 30, 16, 7, true);
                    break;
                case 8: // Speaker with sound waves.
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(7, 18), new Vector2Int(15, 18), new Vector2Int(27, 8),
                        new Vector2Int(27, 40), new Vector2Int(15, 30), new Vector2Int(7, 30)
                    });
                    DrawThickLine(pixels, size, new Vector2(32f, 17f), new Vector2(37f, 21f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(37f, 21f), new Vector2(37f, 27f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(37f, 27f), new Vector2(32f, 31f), 3f, true);
                    DrawThickLine(pixels, size, new Vector2(38f, 12f), new Vector2(43f, 18f), 2.5f, true);
                    DrawThickLine(pixels, size, new Vector2(43f, 18f), new Vector2(43f, 30f), 2.5f, true);
                    DrawThickLine(pixels, size, new Vector2(43f, 30f), new Vector2(38f, 36f), 2.5f, true);
                    break;
                case 9: // Desktop monitor.
                    DrawRectOutline(pixels, size, 5, 13, 43, 39, 3);
                    DrawThickLine(pixels, size, new Vector2(24f, 8f), new Vector2(24f, 14f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(15f, 7f), new Vector2(33f, 7f), 4f, true);
                    break;
                case 10: // Cat-face doodle.
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(8, 33), new Vector2Int(10, 44), new Vector2Int(19, 37),
                        new Vector2Int(29, 37), new Vector2Int(38, 44), new Vector2Int(40, 33)
                    });
                    DrawCircleRing(pixels, size, 24, 24, 17, 3);
                    FillCircle(pixels, size, 18, 27, 2, true);
                    FillCircle(pixels, size, 30, 27, 2, true);
                    DrawThickLine(pixels, size, new Vector2(21f, 19f), new Vector2(24f, 17f), 2f, true);
                    DrawThickLine(pixels, size, new Vector2(24f, 17f), new Vector2(27f, 19f), 2f, true);
                    DrawThickLine(pixels, size, new Vector2(12f, 20f), new Vector2(3f, 17f), 1.8f, true);
                    DrawThickLine(pixels, size, new Vector2(12f, 24f), new Vector2(2f, 24f), 1.8f, true);
                    DrawThickLine(pixels, size, new Vector2(36f, 20f), new Vector2(45f, 17f), 1.8f, true);
                    DrawThickLine(pixels, size, new Vector2(36f, 24f), new Vector2(46f, 24f), 1.8f, true);
                    break;
                case 11: // Reset / circular arrow.
                    DrawCircleRing(pixels, size, 24, 23, 15, 4);
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(7, 32), new Vector2Int(7, 17), new Vector2Int(19, 28)
                    });
                    DrawThickLine(pixels, size, new Vector2(24f, 23f), new Vector2(31f, 31f), 3f, true);
                    break;
                case 12: // Confirmation check mark.
                    DrawThickLine(pixels, size, new Vector2(7f, 24f), new Vector2(19f, 11f), 7f, true);
                    DrawThickLine(pixels, size, new Vector2(19f, 11f), new Vector2(42f, 38f), 7f, true);
                    break;
                case 13: // Eraser.
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(7, 15), new Vector2Int(17, 6), new Vector2Int(41, 29),
                        new Vector2Int(31, 40)
                    });
                    DrawThickLine(pixels, size, new Vector2(13f, 12f), new Vector2(35f, 34f), 3f, false);
                    DrawThickLine(pixels, size, new Vector2(8f, 8f), new Vector2(25f, 8f), 3f, true);
                    break;
                case 14: // Waste basket.
                    DrawThickLine(pixels, size, new Vector2(12f, 35f), new Vector2(36f, 35f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(15f, 32f), new Vector2(18f, 8f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(33f, 32f), new Vector2(30f, 8f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(18f, 8f), new Vector2(30f, 8f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(18f, 41f), new Vector2(30f, 41f), 4f, true);
                    DrawThickLine(pixels, size, new Vector2(24f, 41f), new Vector2(24f, 45f), 4f, true);
                    break;
                case 15: // Back arrow.
                    DrawThickLine(pixels, size, new Vector2(8f, 25f), new Vector2(41f, 25f), 6f, true);
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(7, 25), new Vector2Int(22, 39), new Vector2Int(22, 11)
                    });
                    break;
                case 16: // Head / face.
                    DrawCircleRing(pixels, size, 24, 24, 17, 3);
                    FillCircle(pixels, size, 18, 29, 2, true);
                    FillCircle(pixels, size, 30, 29, 2, true);
                    DrawThickLine(pixels, size, new Vector2(18f, 18f), new Vector2(24f, 15f), 2f, true);
                    DrawThickLine(pixels, size, new Vector2(24f, 15f), new Vector2(30f, 18f), 2f, true);
                    break;
                case 17: // Torso / shirt.
                    FillPolygon(pixels, size, new[]
                    {
                        new Vector2Int(8, 34), new Vector2Int(17, 42), new Vector2Int(22, 36),
                        new Vector2Int(26, 36), new Vector2Int(31, 42), new Vector2Int(40, 34),
                        new Vector2Int(34, 25), new Vector2Int(34, 7), new Vector2Int(14, 7),
                        new Vector2Int(14, 25)
                    });
                    DrawThickLine(pixels, size, new Vector2(20f, 39f), new Vector2(24f, 35f), 2f, false);
                    DrawThickLine(pixels, size, new Vector2(24f, 35f), new Vector2(28f, 39f), 2f, false);
                    break;
                case 18: // Arm / bone.
                    DrawThickLine(pixels, size, new Vector2(13f, 12f), new Vector2(35f, 36f), 7f, true);
                    FillCircle(pixels, size, 11, 10, 5, true);
                    FillCircle(pixels, size, 37, 38, 5, true);
                    FillCircle(pixels, size, 16, 8, 5, true);
                    FillCircle(pixels, size, 32, 40, 5, true);
                    break;
                case 19: // Leg / boot.
                    DrawThickLine(pixels, size, new Vector2(22f, 41f), new Vector2(22f, 15f), 7f, true);
                    DrawThickLine(pixels, size, new Vector2(22f, 15f), new Vector2(39f, 10f), 8f, true);
                    DrawThickLine(pixels, size, new Vector2(14f, 42f), new Vector2(27f, 42f), 5f, true);
                    break;
                default: // Ink bottle.
                    DrawRectOutline(pixels, size, 10, 8, 38, 33, 3);
                    DrawRectOutline(pixels, size, 17, 34, 31, 43, 3);
                    DrawThickLine(pixels, size, new Vector2(15f, 22f), new Vector2(33f, 22f), 3f, true);
                    break;
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Shared Title Icon " + iconIndex,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = "Shared Title Icon " + iconIndex;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void FillCircle(Color32[] pixels, int size, int centerX, int centerY,
            int radius, bool filled)
        {
            int radiusSquared = radius * radius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy <= radiusSquared) SetIconPixel(pixels, size, x, y, filled);
                }
            }
        }

        private static void DrawCircleRing(Color32[] pixels, int size, int centerX, int centerY,
            int radius, int thickness)
        {
            int outer = radius * radius;
            int innerRadius = Mathf.Max(0, radius - thickness);
            int inner = innerRadius * innerRadius;
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    int distance = dx * dx + dy * dy;
                    if (distance <= outer && distance >= inner) SetIconPixel(pixels, size, x, y, true);
                }
            }
        }

        private static void DrawRectOutline(Color32[] pixels, int size, int minX, int minY,
            int maxX, int maxY, int thickness)
        {
            for (int i = 0; i < thickness; i++)
            {
                DrawThickLine(pixels, size, new Vector2(minX + i, minY + i), new Vector2(maxX - i, minY + i), 1f, true);
                DrawThickLine(pixels, size, new Vector2(maxX - i, minY + i), new Vector2(maxX - i, maxY - i), 1f, true);
                DrawThickLine(pixels, size, new Vector2(maxX - i, maxY - i), new Vector2(minX + i, maxY - i), 1f, true);
                DrawThickLine(pixels, size, new Vector2(minX + i, maxY - i), new Vector2(minX + i, minY + i), 1f, true);
            }
        }

        private static void DrawThickLine(Color32[] pixels, int size, Vector2 from, Vector2 to,
            float width, bool filled)
        {
            Vector2 segment = to - from;
            float lengthSquared = Mathf.Max(0.001f, segment.sqrMagnitude);
            float radius = width * 0.5f;
            int minX = Mathf.FloorToInt(Mathf.Min(from.x, to.x) - radius);
            int maxX = Mathf.CeilToInt(Mathf.Max(from.x, to.x) + radius);
            int minY = Mathf.FloorToInt(Mathf.Min(from.y, to.y) - radius);
            int maxY = Mathf.CeilToInt(Mathf.Max(from.y, to.y) + radius);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared);
                    if (Vector2.Distance(point, from + segment * t) <= radius)
                        SetIconPixel(pixels, size, x, y, filled);
                }
            }
        }

        private static void FillPolygon(Color32[] pixels, int size, Vector2Int[] vertices)
        {
            int minX = size;
            int minY = size;
            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                minX = Math.Min(minX, vertices[i].x);
                minY = Math.Min(minY, vertices[i].y);
                maxX = Math.Max(maxX, vertices[i].x);
                maxY = Math.Max(maxY, vertices[i].y);
            }
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    bool inside = false;
                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        Vector2Int vi = vertices[i];
                        Vector2Int vj = vertices[j];
                        bool crosses = (vi.y > y) != (vj.y > y)
                            && x < (vj.x - vi.x) * (y - vi.y) / (float)(vj.y - vi.y) + vi.x;
                        if (crosses) inside = !inside;
                    }
                    if (inside) SetIconPixel(pixels, size, x, y, true);
                }
            }
        }

        private static void SetIconPixel(Color32[] pixels, int size, int x, int y, bool filled)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            pixels[y * size + x] = filled
                ? new Color32(255, 255, 255, 255)
                : new Color32(255, 255, 255, 0);
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
