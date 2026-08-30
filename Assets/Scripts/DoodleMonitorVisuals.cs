using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Shared child-drawn casing for in-world status monitors. Live text, bars and
    /// lamps stay controller-owned so this visual can be reused without changing
    /// challenge behaviour.
    /// </summary>
    internal static class DoodleMonitorVisuals
    {
        // Character body strokes use sorting order 10.  A monitor may use much
        // larger child orders to arrange its case, screen and text internally,
        // so keep the whole assembly in a group behind characters.
        private const int WorldMonitorSortingOrder = 5;

        internal static void Build(Transform parent, Vector2 size, int backOrder)
        {
            if (parent == null) return;

            KeepBehindPlayers(parent);

            DoodleMonitorTextReadability readability = parent.GetComponent<DoodleMonitorTextReadability>();
            if (readability == null) readability = parent.gameObject.AddComponent<DoodleMonitorTextReadability>();
            readability.Configure(size);

            float scale = Mathf.Max(0.35f, Mathf.Min(size.x / 3.2f, size.y / 1.25f));
            Color graphite = new Color(0.16f, 0.2f, 0.26f, 0.94f);
            Color blue = new Color(0.34f, 0.66f, 0.88f, 0.76f);
            AddRect(parent, "Crayon Monitor Paper Case", size, new Color(0.98f, 0.95f, 0.82f, 0.98f), backOrder);
            AddRect(parent, "Blue Pencil Case Wash", size - Vector2.one * (0.12f * scale), blue, backOrder + 1);

            int strokes = Mathf.Clamp(Mathf.RoundToInt((size.x + size.y) * 0.5f), 5, 17);
            for (int i = 0; i < strokes; i++)
            {
                float t = (i + 0.5f) / strokes;
                float y = Mathf.Lerp(-size.y * 0.44f, size.y * 0.44f, t);
                float wobble = Mathf.Sin(i * 2.31f) * 0.035f;
                AddLine(parent, "Blue Case Pencil Stroke", new[]
                {
                    new Vector2(-size.x * 0.46f, y - 0.12f + wobble),
                    new Vector2(size.x * 0.46f, y + 0.12f - wobble)
                }, Mathf.Max(0.012f, Mathf.Min(size.x, size.y) * 0.008f),
                    new Color(0.18f, 0.45f, 0.72f, 0.2f), backOrder + 2);
            }

            Vector2 screenSize = new Vector2(
                Mathf.Max(0.5f, size.x - 0.34f * scale),
                Mathf.Max(0.3f, size.y * 0.7f));
            Vector2 screenAt = new Vector2(0f, -size.y * 0.045f);
            AddRect(parent, "Pale Paper Screen", screenSize, new Color(0.91f, 0.97f, 0.91f, 0.98f), backOrder + 3, screenAt);
            AddRect(parent, "Screen Phosphor Wash", screenSize * 0.96f,
                new Color(0.18f, 0.78f, 0.88f, 0.055f), backOrder + 4, screenAt);
            int scanLineCount = Mathf.Clamp(Mathf.RoundToInt(screenSize.y * 2.4f), 5, 10);
            for (int i = 0; i < scanLineCount; i++)
            {
                float t = (i + 0.5f) / scanLineCount;
                float y = Mathf.Lerp(-screenSize.y * 0.43f, screenSize.y * 0.43f, t);
                AddRect(parent, "Screen Scanline", new Vector2(screenSize.x * 0.91f, 0.012f * scale),
                    new Color(0.04f, 0.42f, 0.58f, 0.065f), backOrder + 5,
                    screenAt + new Vector2(0f, y));
            }
            AddLine(parent, "Screen Glass Gleam", new[]
            {
                screenAt + new Vector2(-screenSize.x * 0.43f, screenSize.y * 0.31f),
                screenAt + new Vector2(screenSize.x * 0.05f, screenSize.y * 0.42f)
            }, 0.018f * scale, new Color(0.75f, 1f, 1f, 0.16f), backOrder + 5);
            AddCrookedBox(parent, "Crooked Screen Outline", screenSize, screenAt, graphite, 0.028f * scale, backOrder + 4);
            AddCrookedBox(parent, "Loose Monitor Outline", size, Vector2.zero, graphite, 0.045f * scale, backOrder + 4);

            AddLine(parent, "Left Crayon Antenna", new[]
            {
                new Vector2(-0.1f * scale, size.y * 0.49f),
                new Vector2(-0.34f * scale, size.y * 0.5f + 0.24f * scale)
            }, 0.035f * scale, graphite, backOrder + 4);
            AddLine(parent, "Right Crayon Antenna", new[]
            {
                new Vector2(0.08f * scale, size.y * 0.49f),
                new Vector2(0.39f * scale, size.y * 0.5f + 0.19f * scale)
            }, 0.035f * scale, graphite, backOrder + 4);
            AddLine(parent, "Left Crooked Foot", new[]
            {
                new Vector2(-size.x * 0.28f, -size.y * 0.48f),
                new Vector2(-size.x * 0.32f, -size.y * 0.5f - 0.12f * scale),
                new Vector2(-size.x * 0.18f, -size.y * 0.5f - 0.12f * scale)
            }, 0.04f * scale, graphite, backOrder + 4);
            AddLine(parent, "Right Crooked Foot", new[]
            {
                new Vector2(size.x * 0.27f, -size.y * 0.48f),
                new Vector2(size.x * 0.31f, -size.y * 0.5f - 0.11f * scale),
                new Vector2(size.x * 0.17f, -size.y * 0.5f - 0.11f * scale)
            }, 0.04f * scale, graphite, backOrder + 4);
        }

        internal static void KeepBehindPlayers(Transform parent)
        {
            if (parent == null) return;
            UnityEngine.Rendering.SortingGroup group = parent.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group == null) group = parent.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
            group.sortingOrder = WorldMonitorSortingOrder;
            group.sortAtRoot = true;
        }

        private static void AddRect(Transform parent, string name, Vector2 size, Color color, int order, Vector2 offset = default)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(offset.x, offset.y, -0.02f);
            obj.transform.localScale = new Vector3(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y), 1f);
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
        }

        private static void AddCrookedBox(Transform parent, string name, Vector2 size, Vector2 offset, Color color, float width, int order)
        {
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            AddLine(parent, name, new[]
            {
                offset + new Vector2(-x - width * 0.35f, -y + width * 0.2f),
                offset + new Vector2(x, -y - width * 0.15f),
                offset + new Vector2(x + width * 0.25f, y - width * 0.3f),
                offset + new Vector2(-x + width * 0.15f, y + width * 0.2f),
                offset + new Vector2(-x - width * 0.35f, -y + width * 0.2f)
            }, width, color, order);
        }

        private static void AddLine(Transform parent, string name, Vector2[] points, float width, Color color, int order)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            LineRenderer line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 5;
            line.numCornerVertices = 3;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class DoodleMonitorTextReadability : MonoBehaviour
    {
        private Vector2 monitorSize;
        private TextMesh[] labels = System.Array.Empty<TextMesh>();
        private readonly Dictionary<int, float> preferredSizes = new Dictionary<int, float>();
        private readonly Dictionary<int, TextMesh> glowLabels = new Dictionary<int, TextMesh>();
        private readonly Dictionary<int, string> rawTexts = new Dictionary<int, string>();
        private readonly Dictionary<int, string> wrappedTexts = new Dictionary<int, string>();
        private int knownChildCount = -1;

        internal void Configure(Vector2 size)
        {
            monitorSize = size;
            knownChildCount = -1;
        }

        private void LateUpdate()
        {
            if (knownChildCount != transform.childCount)
            {
                TextMesh[] allLabels = GetComponentsInChildren<TextMesh>(true);
                List<TextMesh> sourceLabels = new List<TextMesh>();
                for (int i = 0; i < allLabels.Length; i++)
                {
                    TextMesh candidate = allLabels[i];
                    if (candidate == null || candidate.name.EndsWith(" Screen Glow")) continue;
                    sourceLabels.Add(candidate);
                    int id = candidate.GetInstanceID();
                    if (!preferredSizes.ContainsKey(id)) preferredSizes[id] = candidate.characterSize;
                    EnsureGlow(candidate);
                }
                labels = sourceLabels.ToArray();
                knownChildCount = transform.childCount;
            }

            for (int i = 0; i < labels.Length; i++)
            {
                TextMesh label = labels[i];
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                int id = label.GetInstanceID();
                string current = label.text;
                if (!wrappedTexts.TryGetValue(id, out string previousWrapped) || current != previousWrapped)
                    rawTexts[id] = current;

                string raw = rawTexts.TryGetValue(id, out string savedRaw) ? savedRaw : current;
                float preferredSize = preferredSizes.TryGetValue(id, out float savedSize)
                    ? savedSize
                    : label.characterSize;
                float safeWidth = monitorSize.x * 0.68f;
                float maximumLineUnits = safeWidth / Mathf.Max(0.025f, preferredSize * 2.7f);
                string wrapped = WrapText(raw, maximumLineUnits, 2);
                label.text = wrapped;
                wrappedTexts[id] = wrapped;

                GetTextDimensions(wrapped, out float longestLineUnits, out int lineCount);
                float widthFit = safeWidth / Mathf.Max(2.7f, longestLineUnits * 2.7f);
                float safeHeight = monitorSize.y * 0.52f;
                float heightFit = safeHeight / Mathf.Max(3.1f, lineCount * 3.1f);
                label.characterSize = Mathf.Min(preferredSize, widthFit, heightFit);

                if (glowLabels.TryGetValue(id, out TextMesh glow) && glow != null)
                {
                    glow.text = label.text;
                    glow.font = label.font;
                    glow.fontSize = label.fontSize;
                    glow.anchor = label.anchor;
                    glow.alignment = label.alignment;
                    glow.characterSize = label.characterSize * 1.035f;
                    glow.lineSpacing = label.lineSpacing;
                    glow.tabSize = label.tabSize;
                    glow.transform.localPosition = label.transform.localPosition + new Vector3(0.012f, -0.012f, 0.006f);
                    glow.transform.localRotation = label.transform.localRotation;
                    glow.transform.localScale = label.transform.localScale;
                }
            }
        }

        private void EnsureGlow(TextMesh label)
        {
            int id = label.GetInstanceID();
            if (glowLabels.TryGetValue(id, out TextMesh existing) && existing != null) return;

            GameObject glowObject = new GameObject(label.name + " Screen Glow");
            glowObject.transform.SetParent(label.transform.parent, false);
            TextMesh glow = glowObject.AddComponent<TextMesh>();
            glow.color = new Color(0.08f, 0.68f, 0.88f, 0.18f);
            MeshRenderer sourceRenderer = label.GetComponent<MeshRenderer>();
            MeshRenderer glowRenderer = glow.GetComponent<MeshRenderer>();
            if (sourceRenderer != null && glowRenderer != null)
            {
                glowRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                glowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                glowRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            }
            glowLabels[id] = glow;
        }

        private static string WrapText(string value, float maximumLineUnits, int maximumLines)
        {
            if (string.IsNullOrEmpty(value) || maximumLines <= 1 || value.IndexOf('\n') >= 0) return value;
            float totalUnits = MeasureTextUnits(value);
            if (totalUnits <= maximumLineUnits) return value;

            int bestBreak = -1;
            int whitespaceBreak = -1;
            float units = 0f;
            float target = Mathf.Min(maximumLineUnits, totalUnits * 0.5f);
            for (int i = 0; i < value.Length; i++)
            {
                units += MeasureCharacter(value[i]);
                if (char.IsWhiteSpace(value[i])) whitespaceBreak = i;
                if (units < target) continue;
                bestBreak = whitespaceBreak > 0 && units - MeasureTextUnits(value.Substring(0, whitespaceBreak)) < 6f
                    ? whitespaceBreak
                    : i + 1;
                break;
            }
            if (bestBreak <= 0 || bestBreak >= value.Length) return value;
            string left = value.Substring(0, bestBreak).TrimEnd();
            string right = value.Substring(bestBreak).TrimStart();
            return string.IsNullOrEmpty(right) ? value : left + "\n" + right;
        }

        private static void GetTextDimensions(string value, out float longestLineUnits, out int lineCount)
        {
            longestLineUnits = 0f;
            lineCount = 1;
            float current = 0f;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    longestLineUnits = Mathf.Max(longestLineUnits, current);
                    current = 0f;
                    lineCount++;
                    continue;
                }
                current += MeasureCharacter(value[i]);
            }
            longestLineUnits = Mathf.Max(longestLineUnits, current);
        }

        private static float MeasureTextUnits(string value)
        {
            float units = 0f;
            for (int i = 0; i < value.Length; i++) units += MeasureCharacter(value[i]);
            return units;
        }

        private static float MeasureCharacter(char character)
        {
            if (char.IsWhiteSpace(character)) return 0.35f;
            if (character <= 0x007f)
                return char.IsPunctuation(character) ? 0.5f : 0.68f;
            return 1f;
        }
    }
}
