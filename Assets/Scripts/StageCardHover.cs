using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class StageCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Color normalColor = new Color(0.98f, 0.94f, 0.82f, 0.95f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.9f, 0.42f, 0.98f);
        [SerializeField] private float lift = 4f;
        [SerializeField] private float scale = 1.04f;

        private RectTransform rectTransform;
        private Vector2 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private bool highlighted;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            basePosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
            baseRotation = rectTransform != null ? rectTransform.localRotation : Quaternion.identity;
            baseScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            Apply(false);
        }

        private void OnEnable()
        {
            if (rectTransform != null)
            {
                basePosition = rectTransform.anchoredPosition;
                baseRotation = rectTransform.localRotation;
                baseScale = rectTransform.localScale;
            }

            Apply(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Apply(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            Apply(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            Apply(false);
        }

        private void Apply(bool value)
        {
            highlighted = value;
            if (targetImage != null)
            {
                targetImage.color = highlighted ? hoverColor : normalColor;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = basePosition + (highlighted ? new Vector2(0f, lift) : Vector2.zero);
                rectTransform.localScale = highlighted ? baseScale * scale : baseScale;
                rectTransform.localRotation = highlighted ? baseRotation * Quaternion.Euler(0f, 0f, -1.5f) : baseRotation;
            }
        }
    }
}
