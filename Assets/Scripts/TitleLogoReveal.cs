using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleLogoReveal : MonoBehaviour
    {
        [SerializeField] private float duration = 0.85f;
        [SerializeField] private float delay = 0.12f;

        private Image image;
        private float timer;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
        }

        private void OnEnable()
        {
            timer = 0f;
            if (image != null)
            {
                image.fillAmount = 0f;
            }
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((timer - delay) / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            image.fillAmount = eased;
        }
    }
}
