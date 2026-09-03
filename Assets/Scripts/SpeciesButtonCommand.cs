using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class SpeciesButtonCommand : MonoBehaviour
    {
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private StageManager stageManager;
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
                drawManager = FindFirstObjectByType<DrawManager>();
            }
            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
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
                drawManager.SpeciesAvailabilityChanged -= RefreshAvailability;
                subscribed = false;
            }
        }

        private void SelectSpecies()
        {
            if (stageManager != null && !stageManager.CanUseGameplayCharacterControls)
            {
                stageManager.ShowReadyRoomOnlyCharacterChangeNotice();
                EventSystem.current?.SetSelectedGameObject(null);
                return;
            }
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
            drawManager.SpeciesAvailabilityChanged += RefreshAvailability;
            subscribed = true;
        }

        private void RefreshAvailability()
        {
            RefreshVisual(drawManager != null ? drawManager.CurrentSpecies : species);
        }

        private void Update()
        {
            // The ready room is removed without changing species availability,
            // so refresh when gameplay starts as well as on drawing events.
            RefreshVisual(drawManager != null ? drawManager.CurrentSpecies : species);
        }

        private void RefreshVisual(DrawManager.Species currentSpecies)
        {
            bool selected = currentSpecies == species;
            bool allowed = (drawManager == null || drawManager.IsSpeciesAllowed(species))
                && (stageManager == null || stageManager.CanUseGameplayCharacterControls);
            Color targetColor = allowed
                ? selected ? selectedColor : normalColor
                : new Color(0.55f, 0.55f, 0.55f, 0.42f);

            if (image != null)
            {
                image.color = targetColor;
            }

            if (button != null)
            {
                button.interactable = allowed;
                ColorBlock colors = button.colors;
                colors.normalColor = targetColor;
                colors.highlightedColor = targetColor * 1.08f;
                colors.pressedColor = targetColor * 0.9f;
                colors.disabledColor = targetColor;
                button.colors = colors;
            }

            if (outline != null)
            {
                outline.effectColor = !allowed
                    ? new Color(0.25f, 0.22f, 0.2f, 0.35f)
                    : selected
                    ? new Color(0.25f, 0.45f, 1f, 0.9f)
                    : new Color(0.28f, 0.2f, 0.14f, 0.25f);
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = selected && allowed ? new Vector3(1.1f, 1.1f, 1f) : Vector3.one;
                rectTransform.localRotation = Quaternion.identity;
            }
        }
    }
}
