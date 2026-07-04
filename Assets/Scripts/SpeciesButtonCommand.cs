using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class SpeciesButtonCommand : MonoBehaviour
    {
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private DrawManager.Species species;
        [SerializeField] private Color selectedColor = new Color(0.98f, 0.9f, 0.55f, 0.95f);
        [SerializeField] private Color normalColor = new Color(0.98f, 0.96f, 0.9f, 0.82f);

        private Image image;
        private Outline outline;
        private RectTransform rectTransform;
        private Button button;
        private bool subscribed;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            image = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            outline = GetComponent<Outline>();
            if (outline == null)
            {
                outline = gameObject.AddComponent<Outline>();
            }

            outline.effectDistance = new Vector2(2f, -2f);
            button = GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(SelectSpecies);
        }

        private void OnEnable()
        {
            EnsureSubscribed();
            RefreshVisual(drawManager != null ? drawManager.CurrentSpecies : species);
        }

        private void OnDisable()
        {
            if (drawManager != null && subscribed)
            {
                drawManager.CurrentSpeciesChanged -= RefreshVisual;
                subscribed = false;
            }
        }

        private void SelectSpecies()
        {
            drawManager?.SetSpecies(species);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void EnsureSubscribed()
        {
            if (drawManager == null || subscribed)
            {
                return;
            }

            drawManager.CurrentSpeciesChanged += RefreshVisual;
            subscribed = true;
        }

        private void RefreshVisual(DrawManager.Species currentSpecies)
        {
            bool selected = currentSpecies == species;
            Color targetColor = selected ? selectedColor : normalColor;

            if (image != null)
            {
                image.color = targetColor;
            }

            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = targetColor;
                colors.highlightedColor = targetColor * 1.08f;
                colors.pressedColor = targetColor * 0.9f;
                button.colors = colors;
            }

            if (outline != null)
            {
                outline.effectColor = selected
                    ? new Color(0.25f, 0.45f, 1f, 0.9f)
                    : new Color(0.28f, 0.2f, 0.14f, 0.25f);
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = selected ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, selected ? -2.5f : 0f);
            }
        }
    }
}
