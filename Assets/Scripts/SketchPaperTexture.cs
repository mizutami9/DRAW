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
            // Keep legacy serialized values readable so existing scenes/prefabs retain
            // backward-compatible data even though the texture is now shared.
            _ = fiberColor;
            _ = size;
            _ = fiberStrength;
            _ = seed;
            Image image = GetComponent<Image>();
            DoodlePaperUi.Apply(image, baseColor);
        }
    }
}
