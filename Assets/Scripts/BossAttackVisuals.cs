using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Runtime-built colored-pencil attack art. These shapes deliberately use
    /// uneven overlapping strokes instead of the generic square sprite so a
    /// fast projectile is still readable as a missile, ink bolt, or laser.
    /// </summary>
    internal static class BossAttackVisuals
    {
        internal static void AddMissile(
            Transform root,
            float length,
            float width,
            Color bodyColor,
            Color accentColor,
            int sortingOrder,
            bool addFlame = true)
        {
            if (root == null) return;

            Color ink = new Color(0.12f, 0.035f, 0.07f, 1f);
            AddEllipse(root, "Missile Pencil Fill", new Vector2(-length * 0.04f, 0f),
                new Vector2(length * 0.72f, width * 0.78f), bodyColor, sortingOrder);
            AddEllipse(root, "Missile Nose", new Vector2(length * 0.34f, 0f),
                new Vector2(width * 0.72f, width * 0.66f), accentColor, sortingOrder + 1);
            AddEllipse(root, "Missile Window", new Vector2(length * 0.02f, width * 0.02f),
                Vector2.one * width * 0.32f, new Color(0.76f, 0.96f, 1f, 0.96f), sortingOrder + 2);

            Vector2 nose = new Vector2(length * 0.55f, 0f);
            Vector2 tailTop = new Vector2(-length * 0.43f, width * 0.36f);
            Vector2 tailBottom = new Vector2(-length * 0.43f, -width * 0.36f);
            StageGun.AddLine(root, "Missile Hand Outline", new[]
            {
                nose,
                new Vector2(length * 0.22f, width * 0.43f),
                tailTop,
                tailBottom,
                new Vector2(length * 0.22f, -width * 0.43f),
                nose
            }, Mathf.Max(0.035f, width * 0.1f), ink, sortingOrder + 3);

            StageGun.AddLine(root, "Upper Missile Fin", new[]
            {
                new Vector2(-length * 0.22f, width * 0.3f),
                new Vector2(-length * 0.42f, width * 0.78f),
                new Vector2(length * 0.02f, width * 0.31f)
            }, Mathf.Max(0.035f, width * 0.09f), ink, sortingOrder + 2);
            StageGun.AddLine(root, "Lower Missile Fin", new[]
            {
                new Vector2(-length * 0.22f, -width * 0.3f),
                new Vector2(-length * 0.42f, -width * 0.78f),
                new Vector2(length * 0.02f, -width * 0.31f)
            }, Mathf.Max(0.035f, width * 0.09f), ink, sortingOrder + 2);

            if (!addFlame) return;
            Color flame = new Color(1f, 0.55f, 0.06f, 0.95f);
            float tailX = -length * 0.43f;
            StageGun.AddLine(root, "Missile Flame Center", new[]
            {
                new Vector2(tailX, 0f),
                new Vector2(-length * 0.72f, width * 0.08f),
                new Vector2(-length * 0.98f, -width * 0.04f)
            }, Mathf.Max(0.045f, width * 0.16f), flame, sortingOrder - 1);
            StageGun.AddLine(root, "Missile Flame Edge", new[]
            {
                new Vector2(tailX, width * 0.18f),
                new Vector2(-length * 0.76f, width * 0.34f),
                new Vector2(-length * 0.9f, width * 0.13f)
            }, Mathf.Max(0.025f, width * 0.08f), new Color(1f, 0.9f, 0.18f, 0.9f), sortingOrder);
        }

        internal static void AddInkBolt(Transform root, bool fast, int sortingOrder)
        {
            if (root == null) return;
            Color fill = fast ? new Color(1f, 0.18f, 0.1f, 1f) : new Color(0.72f, 0.16f, 0.94f, 1f);
            Color light = fast ? new Color(1f, 0.72f, 0.12f, 0.96f) : new Color(1f, 0.42f, 0.92f, 0.92f);
            Color ink = fast ? new Color(0.35f, 0.025f, 0.02f, 1f) : new Color(0.16f, 0.015f, 0.24f, 1f);
            float length = fast ? 1.35f : 0.78f;
            float width = fast ? 0.46f : 0.7f;

            AddEllipse(root, "Ink Bolt Core", new Vector2(length * 0.08f, 0f),
                new Vector2(length * 0.72f, width * 0.72f), fill, sortingOrder);
            AddEllipse(root, "Ink Bolt Highlight", new Vector2(length * 0.2f, width * 0.1f),
                new Vector2(length * 0.28f, width * 0.2f), light, sortingOrder + 1);

            StageGun.AddLine(root, "Ink Bolt Jagged Edge", new[]
            {
                new Vector2(length * 0.52f, 0f),
                new Vector2(length * 0.18f, width * 0.43f),
                new Vector2(-length * 0.12f, width * 0.32f),
                new Vector2(-length * 0.4f, width * 0.12f),
                new Vector2(-length * 0.18f, 0f),
                new Vector2(-length * 0.42f, -width * 0.18f),
                new Vector2(-length * 0.08f, -width * 0.38f),
                new Vector2(length * 0.22f, -width * 0.36f),
                new Vector2(length * 0.52f, 0f)
            }, fast ? 0.07f : 0.065f, ink, sortingOrder + 2);

            StageGun.AddLine(root, "Ink Speed Stroke A", new[]
            {
                new Vector2(-length * 0.25f, width * 0.2f),
                new Vector2(-length * 0.82f, width * 0.34f)
            }, 0.055f, light, sortingOrder - 1);
            StageGun.AddLine(root, "Ink Speed Stroke B", new[]
            {
                new Vector2(-length * 0.3f, -width * 0.18f),
                new Vector2(-length * (fast ? 1.08f : 0.76f), -width * 0.28f)
            }, 0.04f, fill, sortingOrder - 1);
        }

        internal static GameObject CreateLaser(
            Transform parent,
            Vector2 start,
            Vector2 end,
            float width,
            int sortingOrder)
        {
            GameObject root = new GameObject("Colored Pencil Boss Laser");
            root.transform.SetParent(parent, false);
            AddWorldLine(root, "Laser Glow", start, end, width * 1.55f,
                new Color(1f, 0.08f, 0.12f, 0.25f), sortingOrder);
            AddWorldLine(root, "Laser Ink", start, end, width,
                new Color(0.95f, 0.05f, 0.12f, 0.96f), sortingOrder + 1);
            AddWorldLine(root, "Laser Hot Core", start, end, width * 0.3f,
                new Color(1f, 0.9f, 0.42f, 1f), sortingOrder + 2);
            AddWorldLine(root, "Laser Pencil Wobble", start + Vector2.up * width * 0.31f,
                end + Vector2.down * width * 0.18f, width * 0.08f,
                new Color(0.28f, 0.01f, 0.08f, 0.9f), sortingOrder + 3);
            return root;
        }

        private static void AddWorldLine(
            GameObject root,
            string name,
            Vector2 start,
            Vector2 end,
            float width,
            Color color,
            int order)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 5;
            line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static SpriteRenderer AddEllipse(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color,
            int order)
        {
            GameObject ellipse = new GameObject(name);
            ellipse.transform.SetParent(parent, false);
            ellipse.transform.localPosition = new Vector3(position.x, position.y, 0f);
            ellipse.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = ellipse.AddComponent<SpriteRenderer>();
            renderer.sprite = DoodleRuntimeAssets.CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
