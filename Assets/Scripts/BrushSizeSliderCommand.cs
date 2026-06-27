using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Slider))]
    public sealed class BrushSizeSliderCommand : MonoBehaviour
    {
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private Text valueText;

        private Slider slider;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(Apply);
            Apply(slider.value);
        }

        private void Apply(float value)
        {
            drawManager?.SetBrushSizePixels(value);

            if (valueText != null)
            {
                valueText.text = $"{value:0.#}px";
            }
        }
    }
}
