using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class SketchPreviewWiggle : MonoBehaviour
    {
        [SerializeField] private float bobAmount = 3f;
        [SerializeField] private float squashAmount = 0.025f;
        [SerializeField] private float speed = 2.2f;

        private RectTransform rectTransform;
        private Vector2 basePosition;
        private Vector3 baseScale;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                basePosition = rectTransform.anchoredPosition;
                baseScale = rectTransform.localScale;
            }
        }

        private void OnEnable()
        {
            if (rectTransform != null)
            {
                basePosition = rectTransform.anchoredPosition;
                baseScale = rectTransform.localScale;
            }
        }

        private void Update()
        {
            if (rectTransform == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * speed);
            rectTransform.anchoredPosition = basePosition + new Vector2(0f, wave * bobAmount);
            rectTransform.localScale = new Vector3(
                baseScale.x * (1f + wave * squashAmount),
                baseScale.y * (1f - wave * squashAmount),
                baseScale.z);
        }
    }
}
