using UnityEngine;

namespace DrawBody.Prototype
{
    internal static class NicoDrawBossArt
    {
        private const string ResourceRoot = "StageObjects/NicoDraw/";

        internal static SpriteRenderer Apply(
            Transform visual,
            string resourceName,
            Vector2 targetSize,
            int sortingOrder,
            bool keepFaceOverlay = false)
        {
            if (visual == null || string.IsNullOrEmpty(resourceName)) return null;
            Sprite sprite = Resources.Load<Sprite>(ResourceRoot + resourceName);
            if (sprite == null) return null;

            SpriteRenderer[] sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                SpriteRenderer current = sprites[i];
                bool keep = keepFaceOverlay && current != null && current.name.Contains("Eye");
                if (current != null && !keep) current.enabled = false;
            }

            LineRenderer[] lines = visual.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer current = lines[i];
                bool keep = keepFaceOverlay && current != null
                    && (current.name.Contains("Mouth")
                        || current.name.Contains("Brow")
                        || current.name.Contains("Defeat Eye"));
                if (current != null && !keep) current.enabled = false;
            }

            GameObject drawing = new GameObject("Messy Child Doodle");
            drawing.transform.SetParent(visual, false);
            drawing.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            SpriteRenderer renderer = drawing.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            Vector2 bounds = sprite.bounds.size;
            drawing.transform.localScale = new Vector3(
                targetSize.x / Mathf.Max(0.01f, bounds.x),
                targetSize.y / Mathf.Max(0.01f, bounds.y),
                1f);
            return renderer;
        }
    }
}
