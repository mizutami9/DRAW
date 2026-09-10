using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    internal sealed class SlimeBoxBreakFeedback : MonoBehaviour
    {
        private const float Duration = 0.72f;

        private sealed class Shard
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Spin;
            public Color Color;
        }

        private static Sprite squareSprite;
        private Shard[] shards;
        private LineRenderer[] burstLines;
        private float elapsed;
        private float effectRadius;

        internal static void Play(Transform box, Vector2 hitPoint, StageObjectType type)
        {
            if (box == null) return;
            GameObject root = new GameObject("Crayon Box Break Burst");
            root.transform.position = hitPoint;
            SlimeBoxBreakFeedback feedback = root.AddComponent<SlimeBoxBreakFeedback>();
            feedback.Initialize(box, type);
        }

        private void Initialize(Transform box, StageObjectType type)
        {
            Bounds bounds = new Bounds(box.position, Vector3.one);
            Renderer[] renderers = box.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            }
            effectRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y), 0.45f, 2.2f);
            Color baseColor = GetBoxColor(type);
            int shardCount = Mathf.Clamp(Mathf.RoundToInt(8f + effectRadius * 3f), 9, 15);
            shards = new Shard[shardCount];
            for (int i = 0; i < shards.Length; i++)
            {
                float angle = Mathf.Lerp(18f, 162f, i / Mathf.Max(1f, shards.Length - 1f))
                    + Random.Range(-14f, 14f);
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                GameObject piece = new GameObject("Crayon Box Shard " + (i + 1));
                piece.transform.SetParent(transform, false);
                piece.transform.localPosition = Random.insideUnitCircle * effectRadius * 0.18f;
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));
                float width = effectRadius * Random.Range(0.18f, 0.34f);
                float height = effectRadius * Random.Range(0.07f, 0.16f);
                piece.transform.localScale = new Vector3(width, height, 1f);
                SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = GetSquareSprite();
                Color pieceColor = Color.Lerp(baseColor, i % 3 == 0 ? Color.white : new Color(0.18f, 0.09f, 0.035f),
                    i % 3 == 0 ? 0.24f : 0.12f);
                renderer.color = pieceColor;
                renderer.sortingOrder = 245 + i % 3;
                shards[i] = new Shard
                {
                    Transform = piece.transform,
                    Renderer = renderer,
                    Velocity = direction * Random.Range(2.7f, 5.4f) * Mathf.Lerp(0.8f, 1.25f, effectRadius),
                    Spin = Random.Range(-540f, 540f),
                    Color = pieceColor
                };
            }

            burstLines = new LineRenderer[8];
            for (int i = 0; i < burstLines.Length; i++)
            {
                float angle = (i / (float)burstLines.Length * 360f + 12f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                GameObject rayObject = new GameObject("Break Pencil Slash " + (i + 1));
                rayObject.transform.SetParent(transform, false);
                LineRenderer line = rayObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.SetPosition(0, direction * effectRadius * 0.2f);
                line.SetPosition(1, direction * effectRadius * Random.Range(0.9f, 1.35f));
                line.startWidth = effectRadius * 0.055f;
                line.endWidth = effectRadius * 0.025f;
                line.numCapVertices = 4;
                line.sharedMaterial = DoodleRuntimeAssets.LineMaterial;
                line.startColor = line.endColor = Color.Lerp(baseColor, Color.white, 0.38f);
                line.sortingOrder = 244;
                burstLines[i] = line;
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float fade = 1f - Mathf.SmoothStep(0.48f, 1f, t);
            for (int i = 0; i < shards.Length; i++)
            {
                Shard shard = shards[i];
                shard.Velocity += Vector2.down * (8.5f * Time.deltaTime);
                shard.Transform.localPosition += (Vector3)(shard.Velocity * Time.deltaTime);
                shard.Transform.Rotate(0f, 0f, shard.Spin * Time.deltaTime);
                shard.Renderer.color = WithAlpha(shard.Color, fade);
            }
            for (int i = 0; i < burstLines.Length; i++)
            {
                LineRenderer line = burstLines[i];
                Color color = line.startColor;
                color.a = Mathf.Max(0f, 1f - t * 2.4f);
                line.startColor = line.endColor = color;
            }
            if (elapsed >= Duration) Destroy(gameObject);
        }

        private static Color GetBoxColor(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.IronBox: return new Color(0.52f, 0.6f, 0.66f, 1f);
                case StageObjectType.RubberBox: return new Color(0.95f, 0.38f, 0.2f, 1f);
                case StageObjectType.FloatingBox: return new Color(0.58f, 0.42f, 0.88f, 1f);
                case StageObjectType.TriangleBox: return new Color(0.9f, 0.7f, 0.18f, 1f);
                default: return new Color(0.68f, 0.38f, 0.14f, 1f);
            }
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null) return squareSprite;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Runtime Box Break Shard Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
            squareSprite.name = "Runtime Box Break Shard Sprite";
            return squareSprite;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
