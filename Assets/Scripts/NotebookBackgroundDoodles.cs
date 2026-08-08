using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Adds quiet, non-interactive notebook doodles behind gameplay and UI.
    /// The glyphs are deliberately typographic so their strokes stay clean at every resolution.
    /// </summary>
    public sealed class NotebookBackgroundDoodles : MonoBehaviour
    {
        private static NotebookBackgroundDoodles worldInstance;
        private static Material worldLineMaterial;

        private static readonly string[] PictureDoodles =
        {
            // Faces and familiar notebook marks.
            "=^.^=", "U o.o U", "O\n/|\\\n/ \\", ":-)", "<3", "*",
            // Sky and weather.
            "C", "\\ | /\n--O--\n/ | \\", "(____)", "(____)\n////", "_/\\_", "((( )))",
            // Landscape and plants.
            "/\\  /\\", "/\\\n||", "\\|/\n-*- \n/|\\", "o-o\n \\|/\n  |", " __\n/  \\\n ||", " ,-.\n(   )\n '-'",
            // Fruit and snacks.
            " ,-.\n(   )\n '-'", "(___)", "(////)", "(@)", "(o)\n\\_/", "[_]c", "/\\\n..", "[____]",
            // Things that fly.
            ">==>", "--o--", " /\\\n ||\n/**\\", "._=_.", "( O )\n  |\n [_]",
            // Buildings and treasure.
            " /\\\n/__\\\n|[]|", "|_|_|_|\n| [] |", "[=_=]", "o--", "--|>", "\\|/\\|/", "/\\_/\\", "<==>", "<>", "(o)",
            // Science, tools and toys.
            "o==o", " ( )\n  V\n _|_", "-(o)-", "/\\/\\/\\", "U", "[::]",
            // Communication and marks.
            "( ... )", "-->", "[OK]", "?", "!"
        };

        private static readonly string[] WordDoodles =
        {
            "Hello!", "OK!", "GO!", "NICE!", "WOW!", "Oops!", "Zzz...",
            "100!", "TEST", "GAME OVER", "START", "GOAL",
            "<-  ->  ^  v", "1+1=2", "x^2", "E=mc^2", "2026", "A+", "O  X  /\\"
        };

        private static readonly Color[] PencilColors =
        {
            new Color(0.12f, 0.34f, 0.75f, 1f),
            new Color(0.82f, 0.24f, 0.22f, 1f),
            new Color(0.14f, 0.55f, 0.28f, 1f),
            new Color(0.48f, 0.28f, 0.66f, 1f),
            new Color(0.25f, 0.22f, 0.18f, 1f)
        };

        private RectTransform uiRoot;
        private Font doodleFont;
        private int baseSeed;
        private int itemCount;
        private int generation;
        private bool configured;
        private bool worldMode;

        public static void EnsureUi(RectTransform parent, Font font, int seed, int count)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("RandomNotebookMarks");
            NotebookBackgroundDoodles doodles;
            if (existing == null)
            {
                GameObject root = new GameObject("RandomNotebookMarks", typeof(RectTransform));
                root.transform.SetParent(parent, false);
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
                doodles = root.AddComponent<NotebookBackgroundDoodles>();
            }
            else
            {
                doodles = existing.GetComponent<NotebookBackgroundDoodles>();
                if (doodles == null)
                {
                    doodles = existing.gameObject.AddComponent<NotebookBackgroundDoodles>();
                }
                existing.SetAsFirstSibling();
                existing.gameObject.SetActive(true);
            }

            doodles.ConfigureUi(font, seed, count);
        }

        public static void EnsureWorld(Transform parent, Font font)
        {
            if (parent == null)
            {
                return;
            }

            NotebookBackgroundDoodles existing = FindWorldInstance();
            if (existing != null)
            {
                // The fallback/debug stage is disabled when a JSON stage is loaded.
                // Keep the background independent so it survives that switch.
                existing.transform.SetParent(null, true);
                return;
            }

            GameObject root = new GameObject("WorldNotebookDoodles");
            root.transform.SetParent(null, false);
            NotebookBackgroundDoodles doodles = root.AddComponent<NotebookBackgroundDoodles>();
            doodles.ConfigureWorld(font, 20260725, 56);
        }

        public static void SetWorldVisible(bool visible)
        {
            NotebookBackgroundDoodles world = FindWorldInstance();
            if (world != null)
            {
                if (visible)
                {
                    world.RefreshWorldLayout();
                }
                else
                {
                    // Do not leave old TextMesh-based marks visible behind the title.
                    world.ClearChildren();
                }
                world.gameObject.SetActive(visible);
            }
        }

        public static void RemoveWorld()
        {
            NotebookBackgroundDoodles world = FindWorldInstance();
            if (world == null)
            {
                return;
            }

            worldInstance = null;
            world.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(world.gameObject);
            }
            else
            {
                DestroyImmediate(world.gameObject);
            }
        }

        private void RefreshWorldLayout()
        {
            Font font = doodleFont;
            if (font == null)
            {
                TextMesh existingText = GetComponentInChildren<TextMesh>(true);
                font = existingText != null ? existingText.font : null;
            }
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            ConfigureWorld(font, 20260725, 56);
        }

        private static NotebookBackgroundDoodles FindWorldInstance()
        {
            if (worldInstance != null)
            {
                return worldInstance;
            }

            NotebookBackgroundDoodles[] all = Resources.FindObjectsOfTypeAll<NotebookBackgroundDoodles>();
            for (int i = 0; i < all.Length; i++)
            {
                NotebookBackgroundDoodles candidate = all[i];
                if (candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.name == "WorldNotebookDoodles")
                {
                    worldInstance = candidate;
                    return candidate;
                }
            }

            return null;
        }

        private void ConfigureUi(Font font, int seed, int count)
        {
            uiRoot = transform as RectTransform;
            doodleFont = font;
            baseSeed = seed;
            itemCount = Mathf.Clamp(count, 4, 30);
            worldMode = false;
            configured = true;
            RebuildUi();
        }

        private void ConfigureWorld(Font font, int seed, int count)
        {
            worldInstance = this;
            doodleFont = font;
            baseSeed = seed;
            itemCount = Mathf.Clamp(count, 8, 64);
            worldMode = true;
            configured = true;
            RebuildWorld();
        }

        private void OnDestroy()
        {
            if (worldInstance == this)
            {
                worldInstance = null;
            }
        }

        private void OnEnable()
        {
            if (!configured || worldMode)
            {
                return;
            }

            generation++;
            RebuildUi();
        }

        private void RebuildUi()
        {
            ClearChildren();
            if (uiRoot == null || doodleFont == null)
            {
                return;
            }

            System.Random random = new System.Random(baseSeed + generation * 7919);
            int wordCount = Mathf.RoundToInt(itemCount * 0.2f);
            for (int i = 0; i < itemCount; i++)
            {
                bool word = i >= itemCount - wordCount;
                string value = Pick(random, word ? WordDoodles : PictureDoodles);
                GameObject obj = new GameObject($"Notebook Doodle {i:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                obj.transform.SetParent(uiRoot, false);

                Text text = obj.GetComponent<Text>();
                text.font = doodleFont;
                text.text = value;
                text.fontSize = word ? random.Next(85, 145) : random.Next(105, 195);
                text.fontStyle = random.NextDouble() < 0.24 ? FontStyle.Bold : FontStyle.Normal;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                Color pencil = PencilColors[random.Next(PencilColors.Length)];
                float alpha = word ? RandomRange(random, 0.20f, 0.29f) : RandomRange(random, 0.22f, 0.34f);
                text.color = new Color(pencil.r, pencil.g, pencil.b, alpha);

                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = FindUiPosition(random, i);
                rect.sizeDelta = word ? new Vector2(760f, 230f) : new Vector2(540f, 460f);
                rect.localRotation = Quaternion.Euler(0f, 0f, RandomRange(random, -7f, 7f));
                float scale = RandomRange(random, 0.82f, 1.18f);
                rect.localScale = Vector3.one * scale;
            }
        }

        private void RebuildWorld()
        {
            ClearChildren();
            System.Random random = new System.Random(baseSeed);
            int wordCount = Mathf.RoundToInt(itemCount * 0.1f);
            List<Rect> occupied = new List<Rect>();
            for (int i = 0; i < itemCount; i++)
            {
                bool word = i >= itemCount - wordCount;
                float scale = word ? RandomRange(random, 1.3f, 1.65f) : RandomRange(random, 1.45f, 2.15f);
                int pictureKind = random.Next(42);
                string wordValue = Pick(random, new[] { "OK", "GO", "WOW", "NICE" });
                Vector2 estimatedSize = word
                    ? new Vector2(wordValue.Length * 1.05f * scale, 1.8f * scale)
                    : new Vector2(2.35f * scale, 2.35f * scale);
                if (!TryFindWorldPosition(random, estimatedSize, occupied, out Vector2 position))
                {
                    continue;
                }

                GameObject obj = new GameObject($"World Doodle {i:00}");
                obj.transform.SetParent(transform, false);
                obj.transform.localPosition = new Vector3(position.x, position.y, 1.65f);
                obj.transform.localRotation = Quaternion.Euler(0f, 0f, RandomRange(random, -8f, 8f));
                Color pencil = PencilColors[random.Next(PencilColors.Length)];
                Color color = new Color(pencil.r, pencil.g, pencil.b, RandomRange(random, 0.25f, 0.38f));
                if (word)
                {
                    DrawVectorWord(obj.transform, wordValue, scale, color);
                }
                else
                {
                    DrawCrayonFill(obj.transform, pictureKind, scale, pencil, random);
                    DrawPicture(obj.transform, pictureKind, scale, color);
                }
            }
        }

        private static void DrawCrayonFill(Transform root, int kind, float scale, Color pencil, System.Random random)
        {
            Color fill = new Color(pencil.r, pencil.g, pencil.b, RandomRange(random, 0.065f, 0.11f));
            if (kind == 10 || kind == 34 || kind == 38 || kind == 39)
            {
                // Narrow symbols look better with a broad crayon pass following their direction.
                Vector2 from = kind == 34 ? new Vector2(-0.82f, 0.62f) : new Vector2(-0.78f, -0.25f);
                Vector2 to = kind == 34 ? new Vector2(0.82f, -0.62f) : new Vector2(0.78f, 0.3f);
                CrayonStroke(root, scale, fill, 0.24f * scale, from, to);
                return;
            }

            float verticalScale = kind == 2 || kind == 13 || kind == 19 || kind == 21 || kind == 23 || kind == 32
                ? 1.1f
                : 0.82f;
            for (int row = -4; row <= 4; row++)
            {
                float normalizedY = row / 4.7f;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedY * normalizedY)) * 0.72f;
                float jitter = RandomRange(random, -0.055f, 0.055f);
                float y = normalizedY * verticalScale + jitter;
                CrayonStroke(
                    root,
                    scale,
                    fill,
                    (0.13f + RandomRange(random, 0f, 0.055f)) * scale,
                    new Vector2(-halfWidth, y),
                    new Vector2(halfWidth, y + RandomRange(random, -0.035f, 0.035f)));
            }
        }

        private static void CrayonStroke(Transform root, float scale, Color color, float width, params Vector2[] points)
        {
            GameObject lineObject = new GameObject("Crayon Fill");
            lineObject.transform.SetParent(root, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i] * scale);
            }
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.material = GetWorldLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = -88;
        }

        private static void DrawPicture(Transform root, int kind, float scale, Color color)
        {
            float w = 0.055f * scale;
            switch (kind)
            {
                case 0: // cat
                    Circle(root, Vector2.zero, 0.72f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.62f, 0.48f), new Vector2(-0.82f, 0.92f), new Vector2(-0.25f, 0.7f));
                    Stroke(root, scale, color, w, new Vector2(0.25f, 0.7f), new Vector2(0.82f, 0.92f), new Vector2(0.62f, 0.48f));
                    Face(root, scale, color, w, true);
                    break;
                case 1: // dog
                    Circle(root, Vector2.zero, 0.7f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.55f, 0.5f), new Vector2(-0.9f, 0.15f), new Vector2(-0.72f, -0.35f));
                    Stroke(root, scale, color, w, new Vector2(0.55f, 0.5f), new Vector2(0.9f, 0.15f), new Vector2(0.72f, -0.35f));
                    Face(root, scale, color, w, false);
                    break;
                case 2: // stick figure
                    Circle(root, new Vector2(0f, 0.55f), 0.28f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0f, 0.27f), new Vector2(0f, -0.45f));
                    Stroke(root, scale, color, w, new Vector2(-0.55f, 0f), new Vector2(0f, 0.12f), new Vector2(0.55f, -0.04f));
                    Stroke(root, scale, color, w, new Vector2(-0.46f, -0.92f), new Vector2(0f, -0.45f), new Vector2(0.5f, -0.92f));
                    break;
                case 3: // smile
                    Circle(root, Vector2.zero, 0.82f, scale, color, w);
                    Circle(root, new Vector2(-0.3f, 0.22f), 0.07f, scale, color, w);
                    Circle(root, new Vector2(0.3f, 0.22f), 0.07f, scale, color, w);
                    Arc(root, Vector2.zero, 0.48f, 205f, 335f, scale, color, w);
                    break;
                case 4: // heart
                    Stroke(root, scale, color, w,
                        new Vector2(0f, -0.85f), new Vector2(-0.72f, -0.12f), new Vector2(-0.62f, 0.55f),
                        new Vector2(-0.18f, 0.72f), new Vector2(0f, 0.38f), new Vector2(0.18f, 0.72f),
                        new Vector2(0.62f, 0.55f), new Vector2(0.72f, -0.12f), new Vector2(0f, -0.85f));
                    break;
                case 5: // star
                    Star(root, scale, color, w);
                    break;
                case 6: // moon
                    Arc(root, Vector2.zero, 0.82f, 65f, 295f, scale, color, w);
                    Arc(root, new Vector2(0.3f, 0f), 0.62f, 105f, 255f, scale, color, w);
                    break;
                case 7: // sun
                    Circle(root, Vector2.zero, 0.42f, scale, color, w);
                    Rays(root, scale, color, w, 8, 0.64f, 0.94f);
                    break;
                case 8: // cloud
                    Cloud(root, scale, color, w);
                    break;
                case 9: // rain
                    Cloud(root, scale, color, w);
                    for (int i = -1; i <= 1; i++) Stroke(root, scale, color, w, new Vector2(i * 0.38f, -0.42f), new Vector2(i * 0.38f - 0.13f, -0.85f));
                    break;
                case 10: // lightning
                    Stroke(root, scale, color, w, new Vector2(0.25f, 0.92f), new Vector2(-0.35f, 0.05f), new Vector2(0.12f, 0.05f), new Vector2(-0.25f, -0.92f), new Vector2(0.62f, -0.02f), new Vector2(0.2f, -0.02f));
                    break;
                case 11: // rainbow
                    for (int i = 0; i < 3; i++) Arc(root, Vector2.zero, 0.52f + i * 0.2f, 15f, 165f, scale, color, w * 0.75f);
                    break;
                case 12: // mountains
                    Stroke(root, scale, color, w, new Vector2(-0.95f, -0.7f), new Vector2(-0.35f, 0.65f), new Vector2(0f, -0.05f), new Vector2(0.42f, 0.85f), new Vector2(0.98f, -0.7f));
                    break;
                case 13: // tree
                    Stroke(root, scale, color, w, new Vector2(-0.16f, -0.9f), new Vector2(-0.1f, 0.05f), new Vector2(0.16f, 0.05f), new Vector2(0.2f, -0.9f));
                    Circle(root, new Vector2(0f, 0.42f), 0.62f, scale, color, w);
                    break;
                case 14: // flower
                    for (int i = 0; i < 6; i++) Circle(root, Direction(i, 6) * 0.43f, 0.24f, scale, color, w);
                    Circle(root, Vector2.zero, 0.18f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0f, -0.18f), new Vector2(0f, -0.92f));
                    break;
                case 15: // clover
                    Circle(root, new Vector2(-0.25f, 0.2f), 0.3f, scale, color, w);
                    Circle(root, new Vector2(0.25f, 0.2f), 0.3f, scale, color, w);
                    Circle(root, new Vector2(0f, 0.58f), 0.3f, scale, color, w);
                    Circle(root, new Vector2(0f, -0.18f), 0.3f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0f, -0.45f), new Vector2(0.25f, -0.95f));
                    break;
                case 16: // mushroom
                    Arc(root, new Vector2(0f, 0.25f), 0.78f, 0f, 180f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.78f, 0.25f), new Vector2(0.78f, 0.25f));
                    Stroke(root, scale, color, w, new Vector2(-0.25f, 0.22f), new Vector2(-0.3f, -0.82f), new Vector2(0.3f, -0.82f), new Vector2(0.25f, 0.22f));
                    break;
                case 17: // apple
                    Circle(root, new Vector2(0f, -0.05f), 0.7f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0f, 0.62f), new Vector2(0.12f, 0.98f));
                    Arc(root, new Vector2(0.38f, 0.72f), 0.28f, 20f, 190f, scale, color, w);
                    break;
                case 18: // donut
                    Circle(root, Vector2.zero, 0.82f, scale, color, w);
                    Circle(root, Vector2.zero, 0.28f, scale, color, w);
                    break;
                case 19: // ice cream
                    Circle(root, new Vector2(0f, 0.42f), 0.52f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.4f, 0.05f), new Vector2(0f, -0.92f), new Vector2(0.4f, 0.05f));
                    break;
                case 20: // paper plane
                    Stroke(root, scale, color, w, new Vector2(-0.95f, 0.42f), new Vector2(0.95f, 0f), new Vector2(-0.72f, -0.55f), new Vector2(-0.42f, -0.05f), new Vector2(0.95f, 0f), new Vector2(-0.95f, 0.42f));
                    break;
                case 21: // rocket
                    Stroke(root, scale, color, w, new Vector2(0f, 0.95f), new Vector2(-0.42f, 0.28f), new Vector2(-0.3f, -0.5f), new Vector2(0.3f, -0.5f), new Vector2(0.42f, 0.28f), new Vector2(0f, 0.95f));
                    Circle(root, new Vector2(0f, 0.2f), 0.16f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.18f, -0.5f), new Vector2(0f, -0.95f), new Vector2(0.18f, -0.5f));
                    break;
                case 22: // UFO
                    Arc(root, new Vector2(0f, 0.22f), 0.42f, 0f, 180f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.9f, 0f), new Vector2(-0.48f, -0.35f), new Vector2(0.48f, -0.35f), new Vector2(0.9f, 0f), new Vector2(-0.9f, 0f));
                    break;
                case 23: // balloon
                    Circle(root, new Vector2(0f, 0.3f), 0.58f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0f, -0.28f), new Vector2(-0.1f, -0.42f), new Vector2(0.1f, -0.42f), new Vector2(0f, -0.28f), new Vector2(0f, -0.92f));
                    break;
                case 24: // house
                    Stroke(root, scale, color, w, new Vector2(-0.85f, 0.1f), new Vector2(0f, 0.88f), new Vector2(0.85f, 0.1f));
                    Stroke(root, scale, color, w, new Vector2(-0.68f, 0.08f), new Vector2(-0.68f, -0.82f), new Vector2(0.68f, -0.82f), new Vector2(0.68f, 0.08f));
                    Stroke(root, scale, color, w, new Vector2(-0.2f, -0.82f), new Vector2(-0.2f, -0.2f), new Vector2(0.2f, -0.2f), new Vector2(0.2f, -0.82f));
                    break;
                case 25: // castle
                    Stroke(root, scale, color, w, new Vector2(-0.85f, -0.85f), new Vector2(-0.85f, 0.65f), new Vector2(-0.5f, 0.65f), new Vector2(-0.5f, 0.25f), new Vector2(-0.15f, 0.25f), new Vector2(-0.15f, 0.65f), new Vector2(0.2f, 0.65f), new Vector2(0.2f, 0.25f), new Vector2(0.55f, 0.25f), new Vector2(0.55f, 0.65f), new Vector2(0.85f, 0.65f), new Vector2(0.85f, -0.85f), new Vector2(-0.85f, -0.85f));
                    break;
                case 26: // treasure chest
                    Arc(root, new Vector2(0f, 0.25f), 0.72f, 0f, 180f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.72f, 0.25f), new Vector2(-0.72f, -0.65f), new Vector2(0.72f, -0.65f), new Vector2(0.72f, 0.25f), new Vector2(-0.72f, 0.25f));
                    Circle(root, new Vector2(0f, -0.18f), 0.1f, scale, color, w);
                    break;
                case 27: // key
                    Circle(root, new Vector2(-0.48f, 0.25f), 0.32f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.18f, 0.05f), new Vector2(0.72f, -0.62f), new Vector2(0.9f, -0.38f), new Vector2(0.68f, -0.2f));
                    break;
                case 28: // sword
                    Stroke(root, scale, color, w, new Vector2(-0.65f, -0.72f), new Vector2(0.62f, 0.72f), new Vector2(0.82f, 0.92f), new Vector2(0.72f, 0.62f), new Vector2(-0.65f, -0.72f));
                    Stroke(root, scale, color, w, new Vector2(-0.85f, -0.45f), new Vector2(-0.4f, -0.88f));
                    break;
                case 29: // crown
                    Stroke(root, scale, color, w, new Vector2(-0.85f, 0.55f), new Vector2(-0.55f, -0.55f), new Vector2(0.55f, -0.55f), new Vector2(0.85f, 0.55f), new Vector2(0.25f, 0f), new Vector2(0f, 0.72f), new Vector2(-0.25f, 0f), new Vector2(-0.85f, 0.55f));
                    break;
                case 30: // shield
                    Stroke(root, scale, color, w, new Vector2(-0.72f, 0.72f), new Vector2(0.72f, 0.72f), new Vector2(0.58f, -0.35f), new Vector2(0f, -0.9f), new Vector2(-0.58f, -0.35f), new Vector2(-0.72f, 0.72f));
                    break;
                case 31: // gem
                    Stroke(root, scale, color, w, new Vector2(-0.75f, 0.35f), new Vector2(-0.38f, 0.78f), new Vector2(0.38f, 0.78f), new Vector2(0.75f, 0.35f), new Vector2(0f, -0.85f), new Vector2(-0.75f, 0.35f), new Vector2(0.75f, 0.35f));
                    break;
                case 32: // light bulb
                    Circle(root, new Vector2(0f, 0.3f), 0.5f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(-0.25f, -0.1f), new Vector2(-0.18f, -0.58f), new Vector2(0.18f, -0.58f), new Vector2(0.25f, -0.1f));
                    Stroke(root, scale, color, w, new Vector2(-0.18f, -0.72f), new Vector2(0.18f, -0.72f));
                    Rays(root, scale, color, w, 6, 0.72f, 0.94f);
                    break;
                case 33: // gear
                    Circle(root, Vector2.zero, 0.58f, scale, color, w);
                    Circle(root, Vector2.zero, 0.2f, scale, color, w);
                    Rays(root, scale, color, w * 1.1f, 8, 0.55f, 0.92f);
                    break;
                case 34: // spring
                    Stroke(root, scale, color, w, new Vector2(-0.8f, 0.65f), new Vector2(0.55f, 0.45f), new Vector2(-0.55f, 0.15f), new Vector2(0.55f, -0.15f), new Vector2(-0.55f, -0.45f), new Vector2(0.8f, -0.65f));
                    break;
                case 35: // magnet
                    Arc(root, Vector2.zero, 0.72f, 180f, 360f, scale, color, w * 2f);
                    Stroke(root, scale, color, w, new Vector2(-0.72f, 0f), new Vector2(-0.72f, 0.68f));
                    Stroke(root, scale, color, w, new Vector2(0.72f, 0f), new Vector2(0.72f, 0.68f));
                    break;
                case 36: // dice
                    Stroke(root, scale, color, w, new Vector2(-0.72f, -0.72f), new Vector2(0.72f, -0.72f), new Vector2(0.72f, 0.72f), new Vector2(-0.72f, 0.72f), new Vector2(-0.72f, -0.72f));
                    Circle(root, new Vector2(-0.35f, 0.35f), 0.08f, scale, color, w);
                    Circle(root, Vector2.zero, 0.08f, scale, color, w);
                    Circle(root, new Vector2(0.35f, -0.35f), 0.08f, scale, color, w);
                    break;
                case 37: // speech bubble
                    Stroke(root, scale, color, w, new Vector2(-0.85f, -0.45f), new Vector2(-0.85f, 0.58f), new Vector2(0.85f, 0.58f), new Vector2(0.85f, -0.45f), new Vector2(0.1f, -0.45f), new Vector2(-0.35f, -0.85f), new Vector2(-0.2f, -0.45f), new Vector2(-0.85f, -0.45f));
                    break;
                case 38: // arrow
                    Stroke(root, scale, color, w, new Vector2(-0.9f, 0f), new Vector2(0.82f, 0f), new Vector2(0.35f, 0.5f), new Vector2(0.82f, 0f), new Vector2(0.35f, -0.5f));
                    break;
                case 39: // check
                    Stroke(root, scale, color, w * 1.25f, new Vector2(-0.75f, 0f), new Vector2(-0.2f, -0.55f), new Vector2(0.8f, 0.65f));
                    break;
                case 40: // question mark
                    Arc(root, new Vector2(0f, 0.32f), 0.48f, -25f, 210f, scale, color, w);
                    Stroke(root, scale, color, w, new Vector2(0.35f, 0.05f), new Vector2(0f, -0.3f), new Vector2(0f, -0.52f));
                    Circle(root, new Vector2(0f, -0.82f), 0.07f, scale, color, w);
                    break;
                default: // exclamation
                    Stroke(root, scale, color, w * 1.2f, new Vector2(0f, 0.85f), new Vector2(0f, -0.42f));
                    Circle(root, new Vector2(0f, -0.78f), 0.08f, scale, color, w);
                    break;
            }
        }

        private static void DrawVectorWord(Transform root, string word, float scale, Color color)
        {
            float advance = 1.05f;
            float start = -(word.Length - 1) * advance * 0.5f;
            for (int i = 0; i < word.Length; i++)
            {
                DrawVectorLetter(root, word[i], new Vector2(start + i * advance, 0f), scale, color);
            }
        }

        private static void DrawVectorLetter(Transform root, char letter, Vector2 offset, float scale, Color color)
        {
            float w = 0.055f * scale;
            Vector2 P(float x, float y) => offset + new Vector2(x, y);
            switch (letter)
            {
                case 'O': Circle(root, offset, 0.48f, scale, color, w); break;
                case 'K':
                    Stroke(root, scale, color, w, P(-0.4f, -0.7f), P(-0.4f, 0.7f));
                    Stroke(root, scale, color, w, P(0.42f, 0.7f), P(-0.38f, 0f), P(0.45f, -0.7f));
                    break;
                case 'G':
                    Arc(root, offset, 0.52f, 35f, 330f, scale, color, w);
                    Stroke(root, scale, color, w, P(0.05f, 0f), P(0.5f, 0f), P(0.5f, -0.42f));
                    break;
                case 'W':
                    Stroke(root, scale, color, w, P(-0.5f, 0.7f), P(-0.28f, -0.7f), P(0f, 0.08f), P(0.28f, -0.7f), P(0.5f, 0.7f));
                    break;
                case 'N':
                    Stroke(root, scale, color, w, P(-0.45f, -0.7f), P(-0.45f, 0.7f), P(0.45f, -0.7f), P(0.45f, 0.7f));
                    break;
                case 'I': Stroke(root, scale, color, w, P(0f, -0.7f), P(0f, 0.7f)); break;
                case 'C': Arc(root, offset, 0.52f, 40f, 320f, scale, color, w); break;
                case 'E':
                    Stroke(root, scale, color, w, P(0.45f, 0.7f), P(-0.45f, 0.7f), P(-0.45f, -0.7f), P(0.45f, -0.7f));
                    Stroke(root, scale, color, w, P(-0.4f, 0f), P(0.3f, 0f));
                    break;
            }
        }

        private static void Face(Transform root, float scale, Color color, float width, bool whiskers)
        {
            Circle(root, new Vector2(-0.27f, 0.18f), 0.07f, scale, color, width);
            Circle(root, new Vector2(0.27f, 0.18f), 0.07f, scale, color, width);
            Stroke(root, scale, color, width, new Vector2(-0.12f, -0.08f), new Vector2(0f, -0.2f), new Vector2(0.12f, -0.08f));
            Arc(root, new Vector2(0f, -0.12f), 0.3f, 205f, 335f, scale, color, width);
            if (whiskers)
            {
                Stroke(root, scale, color, width * 0.7f, new Vector2(-0.25f, -0.15f), new Vector2(-0.9f, -0.02f));
                Stroke(root, scale, color, width * 0.7f, new Vector2(0.25f, -0.15f), new Vector2(0.9f, -0.02f));
            }
        }

        private static void Cloud(Transform root, float scale, Color color, float width)
        {
            Arc(root, new Vector2(-0.42f, 0f), 0.42f, 70f, 260f, scale, color, width);
            Arc(root, new Vector2(0f, 0.28f), 0.52f, 15f, 170f, scale, color, width);
            Arc(root, new Vector2(0.5f, 0f), 0.38f, -75f, 110f, scale, color, width);
            Stroke(root, scale, color, width, new Vector2(0.55f, -0.35f), new Vector2(-0.55f, -0.35f));
        }

        private static void Star(Transform root, float scale, Color color, float width)
        {
            Vector2[] points = new Vector2[11];
            for (int i = 0; i < 10; i++)
            {
                float radius = i % 2 == 0 ? 0.88f : 0.38f;
                float angle = Mathf.Deg2Rad * (90f + i * 36f);
                points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            points[10] = points[0];
            Stroke(root, scale, color, width, points);
        }

        private static void Rays(Transform root, float scale, Color color, float width, int count, float inner, float outer)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Direction(i, count);
                Stroke(root, scale, color, width, direction * inner, direction * outer);
            }
        }

        private static Vector2 Direction(int index, int count)
        {
            float angle = Mathf.PI * 2f * index / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static void Circle(Transform root, Vector2 center, float radius, float scale, Color color, float width)
        {
            Arc(root, center, radius, 0f, 360f, scale, color, width);
        }

        private static void Arc(Transform root, Vector2 center, float radius, float fromDegrees, float toDegrees, float scale, Color color, float width)
        {
            const int segments = 24;
            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Stroke(root, scale, color, width, points);
        }

        private static void Stroke(Transform root, float scale, Color color, float width, params Vector2[] points)
        {
            GameObject lineObject = new GameObject("Pencil Stroke");
            lineObject.transform.SetParent(root, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                line.SetPosition(i, points[i] * scale);
            }
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.material = GetWorldLineMaterial();
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = -86;
        }

        private static Material GetWorldLineMaterial()
        {
            if (worldLineMaterial == null)
            {
                worldLineMaterial = new Material(Shader.Find("Sprites/Default"));
                worldLineMaterial.name = "Notebook Background Pencil";
            }
            return worldLineMaterial;
        }

        private static Vector2 EstimateWorldTextSize(string value, float characterSize)
        {
            string[] lines = value.Split('\n');
            int longest = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                longest = Mathf.Max(longest, lines[i].Length);
            }

            return new Vector2(
                Mathf.Max(0.9f, longest * characterSize * 0.58f),
                Mathf.Max(0.9f, lines.Length * characterSize * 1.15f));
        }

        private static bool TryFindWorldPosition(
            System.Random random,
            Vector2 size,
            List<Rect> occupied,
            out Vector2 position)
        {
            const float minWorldX = -37f;
            const float maxWorldX = 197f;
            const float minWorldY = -7.5f;
            const float maxWorldY = 23.5f;
            const float spacing = 0.42f;
            Vector2 paddedSize = size + Vector2.one * spacing * 2f;
            float minX = minWorldX + paddedSize.x * 0.5f;
            float maxX = maxWorldX - paddedSize.x * 0.5f;
            float minY = minWorldY + paddedSize.y * 0.5f;
            float maxY = maxWorldY - paddedSize.y * 0.5f;
            if (minX >= maxX || minY >= maxY)
            {
                position = Vector2.zero;
                return false;
            }

            for (int attempt = 0; attempt < 100; attempt++)
            {
                Vector2 candidate = new Vector2(
                    RandomRange(random, minX, maxX),
                    RandomRange(random, minY, maxY));
                Rect bounds = new Rect(candidate - paddedSize * 0.5f, paddedSize);
                bool overlaps = false;
                for (int i = 0; i < occupied.Count; i++)
                {
                    if (bounds.Overlaps(occupied[i]))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    occupied.Add(bounds);
                    position = candidate;
                    return true;
                }
            }

            position = Vector2.zero;
            return false;
        }

        private static Vector2 FindUiPosition(System.Random random, int index)
        {
            // Alternate between outer and inner bands. This keeps the center readable
            // while still allowing a few marks to peek through open workspace areas.
            int band = index % 4;
            switch (band)
            {
                case 0:
                    return new Vector2(RandomRange(random, -600f, 600f), RandomRange(random, 245f, 330f));
                case 1:
                    return new Vector2(RandomRange(random, -600f, 600f), RandomRange(random, -330f, -245f));
                case 2:
                    return new Vector2(RandomRange(random, -610f, -430f), RandomRange(random, -235f, 235f));
                default:
                    return new Vector2(RandomRange(random, 430f, 610f), RandomRange(random, -235f, 235f));
            }
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static string Pick(System.Random random, string[] values)
        {
            return values[random.Next(values.Length)];
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
