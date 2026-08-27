using UnityEngine;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class StartFlagWave : MonoBehaviour
    {
        private const int HorizontalSegments = 14;
        private const int VerticalSegments = 8;
        private const float WaveSpeed = 2.8f;

        private SpriteRenderer sourceRenderer;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material material;
        private Vector3[] restingVertices;
        private Vector3[] animatedVertices;
        private Vector2[] normalizedCoordinates;
        private float phaseOffset;

        private void Awake()
        {
            BuildMesh();
        }

        private void Update()
        {
            if (mesh == null || restingVertices == null)
            {
                return;
            }

            float phase = Time.time * WaveSpeed + phaseOffset;
            Bounds bounds = sourceRenderer.sprite.bounds;
            float verticalAmplitude = bounds.size.y * 0.024f;
            float horizontalAmplitude = bounds.size.x * 0.006f;

            for (int i = 0; i < restingVertices.Length; i++)
            {
                Vector2 normalized = normalizedCoordinates[i];
                // The pole and pedestal occupy the left and lower portions of
                // the artwork. Fade the deformation in only across the cloth.
                float clothX = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 0.48f, normalized.x));
                float clothY = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.50f, normalized.y));
                float weight = clothX * clothY;
                float wave = Mathf.Sin(phase + normalized.x * 7.2f);
                float ripple = Mathf.Sin(phase * 1.7f + normalized.x * 12.5f) * 0.28f;

                Vector3 vertex = restingVertices[i];
                vertex.x += horizontalAmplitude * weight * ripple;
                vertex.y += verticalAmplitude * weight * (wave + ripple);
                animatedVertices[i] = vertex;
            }

            mesh.vertices = animatedVertices;
            mesh.RecalculateBounds();
        }

        private void BuildMesh()
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
            Sprite sprite = sourceRenderer != null ? sourceRenderer.sprite : null;
            Shader shader = Shader.Find("Sprites/Default");
            if (sprite == null || shader == null)
            {
                enabled = false;
                return;
            }

            int columns = HorizontalSegments + 1;
            int rows = VerticalSegments + 1;
            int vertexCount = columns * rows;
            restingVertices = new Vector3[vertexCount];
            animatedVertices = new Vector3[vertexCount];
            normalizedCoordinates = new Vector2[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[HorizontalSegments * VerticalSegments * 6];

            Bounds bounds = sprite.bounds;
            Rect textureRect = sprite.textureRect;
            float textureWidth = sprite.texture.width;
            float textureHeight = sprite.texture.height;
            for (int y = 0; y < rows; y++)
            {
                float v = y / (float)VerticalSegments;
                for (int x = 0; x < columns; x++)
                {
                    float u = x / (float)HorizontalSegments;
                    int index = y * columns + x;
                    restingVertices[index] = new Vector3(
                        Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                        Mathf.Lerp(bounds.min.y, bounds.max.y, v),
                        0f);
                    animatedVertices[index] = restingVertices[index];
                    normalizedCoordinates[index] = new Vector2(u, v);
                    uv[index] = new Vector2(
                        (textureRect.xMin + textureRect.width * u) / textureWidth,
                        (textureRect.yMin + textureRect.height * v) / textureHeight);
                }
            }

            int triangleIndex = 0;
            for (int y = 0; y < VerticalSegments; y++)
            {
                for (int x = 0; x < HorizontalSegments; x++)
                {
                    int bottomLeft = y * columns + x;
                    int topLeft = bottomLeft + columns;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = bottomLeft + 1;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topLeft + 1;
                }
            }

            mesh = new Mesh { name = "Start Flag Wave Mesh" };
            mesh.MarkDynamic();
            mesh.vertices = restingVertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            Color[] colors = new Color[vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.RecalculateBounds();

            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshFilter == null)
            {
                enabled = false;
                return;
            }
            meshFilter.sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            if (meshRenderer == null)
            {
                enabled = false;
                return;
            }
            material = new Material(shader)
            {
                name = "Start Flag Wave Material",
                mainTexture = sprite.texture,
                color = sourceRenderer.color
            };
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            meshRenderer.sortingOrder = sourceRenderer.sortingOrder;
            sourceRenderer.enabled = false;
            phaseOffset = transform.position.x * 0.37f;
        }

        private void OnDestroy()
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
