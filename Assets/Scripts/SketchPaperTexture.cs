using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public sealed class SketchPaperTexture : MonoBehaviour
    {
        [SerializeField] private Color baseColor = new Color(0.98f, 0.955f, 0.865f, 1f);
        [SerializeField] private Color fiberColor = new Color(0.74f, 0.66f, 0.5f, 1f);
        [SerializeField] private int size = 192;
        [SerializeField] private float fiberStrength = 0.12f;
        [SerializeField] private int seed = 3197;

        private void Awake()
        {
            ApplyTexture();
        }

        private void OnEnable()
        {
            ApplyTexture();
        }

        private void OnValidate()
        {
            ApplyTexture();
        }

        private void ApplyTexture()
        {
            Image image = GetComponent<Image>();
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "GeneratedSketchPaper";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            System.Random random = new System.Random(seed);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = x / (float)size;
                    float ny = y / (float)size;
                    float grain = Mathf.PerlinNoise(nx * 22f + seed * 0.01f, ny * 22f);
                    float fiber = Mathf.PerlinNoise(nx * 4f, ny * 74f + seed * 0.03f);
                    float speck = random.NextDouble() > 0.986 ? Mathf.Lerp(0.18f, 0.4f, (float)random.NextDouble()) : 0f;
                    float amount = Mathf.Clamp01((grain * 0.55f + fiber * 0.45f) * fiberStrength + speck);
                    Color color = Color.Lerp(baseColor, fiberColor, amount);
                    color.a = baseColor.a;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(false, true);
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            image.type = Image.Type.Tiled;
            image.color = Color.white;
        }
    }
}
