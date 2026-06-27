using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class PartButtonCommand : MonoBehaviour
    {
        [SerializeField] private DrawManager drawManager;
        [SerializeField] private DrawManager.BodyPart bodyPart;
        [SerializeField] private Color selectedColor = new Color(0.1f, 0.38f, 0.95f);
        [SerializeField] private Color normalColor = new Color(0.28f, 0.28f, 0.32f);

        private Image image;
        private Button button;
        private Text label;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private bool subscribed;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            image = GetComponent<Image>();
            button = GetComponent<Button>();
            label = GetComponentInChildren<Text>();
            DisableNavigation(button);
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            button.onClick.AddListener(SelectPart);
        }

        private void Start()
        {
            EnsureSubscribed();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLabel;
            EnsureSubscribed();
            RefreshLabel();
        }

        private void EnsureSubscribed()
        {
            if (drawManager != null)
            {
                if (!subscribed)
                {
                    drawManager.CurrentPartChanged += RefreshVisual;
                    drawManager.CurrentSpeciesChanged += RefreshSpecies;
                    subscribed = true;
                }

                RefreshSpecies(drawManager.CurrentSpecies);
                RefreshVisual(drawManager.CurrentPart);
            }
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLabel;

            if (drawManager != null && subscribed)
            {
                drawManager.CurrentPartChanged -= RefreshVisual;
                drawManager.CurrentSpeciesChanged -= RefreshSpecies;
                subscribed = false;
            }
        }

        private void SelectPart()
        {
            drawManager?.SetCurrentPart(bodyPart);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private static void DisableNavigation(Button target)
        {
            if (target == null)
            {
                return;
            }

            Navigation navigation = target.navigation;
            navigation.mode = Navigation.Mode.None;
            target.navigation = navigation;
        }

        private void RefreshVisual(DrawManager.BodyPart currentPart)
        {
            if (drawManager != null && !drawManager.IsPartActive(bodyPart))
            {
                SetAvailable(false);
                return;
            }

            SetAvailable(true);
            RefreshLabel();
            Color targetColor = currentPart == bodyPart ? selectedColor : normalColor;

            if (image != null)
            {
                image.color = targetColor;
            }

            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = targetColor;
                colors.highlightedColor = targetColor * 1.15f;
                colors.pressedColor = targetColor * 0.82f;
                button.colors = colors;
            }
        }

        private void RefreshSpecies(DrawManager.Species species)
        {
            if (drawManager == null)
            {
                return;
            }

            SetAvailable(drawManager.IsPartActive(bodyPart));
            RefreshLabel();
            RepositionForSpecies();
        }

        private void RefreshLabel()
        {
            if (label != null)
            {
                label.text = DrawManager.GetPartLabel(bodyPart);
            }
        }

        private void RepositionForSpecies()
        {
            if (drawManager == null || rectTransform == null || !drawManager.IsPartActive(bodyPart))
            {
                return;
            }

            DrawManager.BodyPart[] parts = drawManager.GetCurrentParts();
            int index = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == bodyPart)
                {
                    index = i;
                    break;
                }
            }

            int columns = Mathf.Min(7, parts.Length);
            int row = index / columns;
            int column = index % columns;
            float spacingX = 122f;
            float spacingY = 42f;
            float startX = -spacingX * (columns - 1) * 0.5f;
            rectTransform.anchoredPosition = new Vector2(startX + spacingX * column, -10f - spacingY * row);
        }

        private void SetAvailable(bool available)
        {
            if (button != null)
            {
                button.interactable = available;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = available ? 1f : 0f;
                canvasGroup.blocksRaycasts = available;
                canvasGroup.interactable = available;
            }
        }
    }
}
