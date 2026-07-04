using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Image))]
    public sealed class TitleLogoReveal : MonoBehaviour
    {
        [SerializeField] private float duration = 1.1f;
        [SerializeField] private float delay = 0.2f;
        [SerializeField] private float wobbleDegrees = 0.7f;
        [SerializeField] private float idleBob = 5f;
        [SerializeField] private float idleScale = 0.018f;

        private Image image;
        private RectTransform rectTransform;
        private float timer;
        private Vector2 basePosition;
        private Vector3 baseScale;
        private Quaternion baseRotation;

        private void Awake()
        {
            image = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            basePosition = rectTransform.anchoredPosition;
            baseScale = rectTransform.localScale;
            baseRotation = rectTransform.localRotation;
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

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = basePosition;
                rectTransform.localScale = baseScale;
                rectTransform.localRotation = baseRotation;
            }
        }

        private void Update()
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01((timer - delay) / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            image.fillAmount = eased;
            if (rectTransform != null)
            {
                float revealWobble = Mathf.Sin(Time.unscaledTime * 18f) * wobbleDegrees * (1f - eased);
                float idleWave = Mathf.Sin(Time.unscaledTime * 2.2f);
                float idleTilt = Mathf.Sin(Time.unscaledTime * 1.4f) * 0.65f * eased;
                rectTransform.anchoredPosition = basePosition + new Vector2(0f, idleWave * idleBob * eased);
                rectTransform.localScale = baseScale * (1f + idleWave * idleScale * eased);
                rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, revealWobble + idleTilt);
            }
        }
    }
}
