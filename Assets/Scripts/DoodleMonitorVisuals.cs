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
        internal static void Build(Transform parent, Vector2 size, int backOrder)
        {
            if (parent == null) return;

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
                labels = GetComponentsInChildren<TextMesh>(true);
                knownChildCount = transform.childCount;
            }

            float preferredMinimum = Mathf.Clamp(monitorSize.y * 0.045f, 0.065f, 0.13f);
            for (int i = 0; i < labels.Length; i++)
            {
                TextMesh label = labels[i];
                if (label == null || string.IsNullOrEmpty(label.text)) continue;
                float fit = monitorSize.x * 0.82f / Mathf.Max(2.7f, label.text.Length * 2.7f);
                float readableSize = Mathf.Min(preferredMinimum, fit);
                if (label.characterSize < readableSize) label.characterSize = readableSize;
            }
        }
    }
}
