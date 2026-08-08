using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class GameplayHudDrawer : MonoBehaviour
    {
        [SerializeField] private RectTransform drawer;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private Button tabButton;
        [SerializeField] private Button escButton;
        [SerializeField] private Vector2 openPosition = new Vector2(12f, 86f);
        [SerializeField] private Vector2 closedPosition = new Vector2(-272f, 86f);
        [SerializeField] private float slideSpeed = 18f;

        private bool open;
        private Image drawerImage;
        private Outline drawerOutline;

        private void Awake()
        {
            if (drawer == null)
            {
                drawer = GetComponent<RectTransform>();
            }

            if (drawer != null)
            {
                NormalizeLayout();
                drawerImage = drawer.GetComponent<Image>();
                drawerOutline = drawer.GetComponent<Outline>();
                drawer.anchoredPosition = closedPosition;
            }

            if (tabButton != null)
            {
                tabButton.onClick.AddListener(Toggle);
            }

            EnsureEscapeButton();

            ApplyImmediate();
        }

        private void EnsureEscapeButton()
        {
            if (escButton == null)
            {
                Transform esc = FindDeep(transform, "GameplayEscHintButton");
                if (esc != null)
                {
                    escButton = esc.GetComponent<Button>();
                    if (escButton == null)
                    {
                        escButton = esc.gameObject.AddComponent<Button>();
                    }
                }
            }

            if (escButton == null)
            {
                return;
            }

            Navigation navigation = escButton.navigation;
            navigation.mode = Navigation.Mode.None;
            escButton.navigation = navigation;
            ColorBlock colors = escButton.colors;
            colors.normalColor = new Color(0.18f, 0.17f, 0.15f, 0.96f);
            colors.highlightedColor = new Color(0.22f, 0.55f, 0.72f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.22f, 0.31f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            escButton.colors = colors;
            escButton.onClick.AddListener(OpenMenu);
        }

        private void OpenMenu()
        {
            UIManager uiManager = FindObjectOfType<UIManager>();
            uiManager?.ToggleMenu();
        }

        private void NormalizeLayout()
        {
            drawer.sizeDelta = new Vector2(250f, 440f);

            RectTransform title = FindRect("GameplaySpeciesTitle");
            if (title != null)
            {
                title.gameObject.SetActive(false);
            }

            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Turtle,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                string prefix = species[i].ToString();
                RectTransform button = FindRect(prefix + "GameplaySpeciesButton");
                if (button != null)
                {
                    int column = i % 2;
                    int row = i / 2;
                    button.anchoredPosition = new Vector2(-50f + column * 100f, 356f - row * 66f);
                    button.sizeDelta = new Vector2(82f, 62f);
                    RestyleSpeciesButton(button);
                    if (species[i] == DrawManager.Species.Turtle)
                    {
                        RedrawTurtleIcon(button);
                    }
                }

                RectTransform label = FindRect(prefix + "GameplaySpeciesLabel");
                if (label != null)
                {
                    label.gameObject.SetActive(false);
                }
            }

            SetButtonRect("GameplayAddCharacterButton", new Vector2(0f, 164f), new Vector2(190f, 40f));
            SetButtonRect("GameplaySwitchCharacterButton", new Vector2(0f, 116f), new Vector2(190f, 40f));
            SetButtonRect("GameplayDeleteCharacterButton", new Vector2(0f, 68f), new Vector2(190f, 40f));
            SetButtonRect("GameplayRedrawButton", new Vector2(0f, 14f), new Vector2(190f, 46f));
            RestyleActionButton("GameplayAddCharacterButton");
            RestyleActionButton("GameplaySwitchCharacterButton");
            RestyleActionButton("GameplayDeleteCharacterButton");
            RestyleRedrawButton();
        }

        private void SetButtonRect(string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = FindRect(name);
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void RestyleRedrawButton()
        {
            RectTransform rect = FindRect("GameplayRedrawButton");
            if (rect == null)
            {
                return;
            }

            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.98f, 0.91f, 0.66f, 0.96f);
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.42f, 0.28f, 0.12f, 0.65f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = rect.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 20;
                label.color = new Color(0.14f, 0.1f, 0.07f, 1f);
            }

            for (int i = rect.childCount - 1; i >= 0; i--)
            {
                Transform child = rect.GetChild(i);
                if (child.name == "IconLine")
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void RestyleActionButton(string name)
        {
            RectTransform rect = FindRect(name);
            if (rect == null)
            {
                return;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.22f, 0.15f, 0.08f, 0.62f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = rect.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 18;
                label.color = new Color(0.12f, 0.08f, 0.05f, 1f);
            }
        }

        private void RestyleSpeciesButton(RectTransform rect)
        {
            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.98f, 0.95f, 0.82f, 0.96f);
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.2f, 0.14f, 0.08f, 0.58f);
            outline.effectDistance = new Vector2(2f, -2f);

            for (int i = 0; i < rect.childCount; i++)
            {
                Transform child = rect.GetChild(i);
                if (child.name == "IconLine" || child.name == "IconDot")
                {
                    child.localScale = Vector3.one * 1.18f;
                }
            }
        }

        public static void RedrawTurtleIcon(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name != "IconLine" && child.name != "IconDot")
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            Color ink = new Color(0.08f, 0.08f, 0.08f, 1f);
            Color shell = new Color(0.2f, 0.55f, 0.25f, 1f);
            CreateTurtleLine(parent, new Vector2(-13f, -7f), new Vector2(-9f, 9f), 2.6f, ink);
            CreateTurtleLine(parent, new Vector2(-9f, 9f), new Vector2(9f, 9f), 2.6f, ink);
            CreateTurtleLine(parent, new Vector2(9f, 9f), new Vector2(13f, -7f), 2.6f, ink);
            CreateTurtleLine(parent, new Vector2(13f, -7f), new Vector2(-13f, -7f), 2.6f, ink);
            CreateTurtleLine(parent, new Vector2(-9f, 9f), new Vector2(13f, -7f), 1.6f, shell);
            CreateTurtleLine(parent, new Vector2(9f, 9f), new Vector2(-13f, -7f), 1.6f, shell);
            CreateTurtleLine(parent, new Vector2(13f, -2f), new Vector2(20f, 2f), 2.6f, ink);
            CreateTurtleLine(parent, new Vector2(20f, 2f), new Vector2(16f, 6f), 2.6f, ink);
        }

        private static void CreateTurtleLine(RectTransform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject line = new GameObject("IconLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private RectTransform FindRect(string name)
        {
            Transform found = FindDeep(drawer, name);
            return found != null ? found as RectTransform : null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void Update()
        {
            if (drawer == null)
            {
                return;
            }

            Vector2 target = open ? openPosition : closedPosition;
            drawer.anchoredPosition = Vector2.Lerp(drawer.anchoredPosition, target, Time.unscaledDeltaTime * slideSpeed);

            bool settled = Vector2.Distance(drawer.anchoredPosition, target) < 0.5f;
            if (settled)
            {
                drawer.anchoredPosition = target;
            }

            RefreshVisibility(settled);
        }

        public void Toggle()
        {
            SetOpen(!open);
        }

        public void Close()
        {
            SetOpen(false);
        }

        public void SetOpen(bool value)
        {
            open = value;
            RefreshVisibility(false);
        }

        private void ApplyImmediate()
        {
            if (drawer != null)
            {
                drawer.anchoredPosition = open ? openPosition : closedPosition;
            }

            RefreshVisibility(true);
        }

        private void RefreshVisibility(bool settled)
        {
            bool showPanel = open || !settled;
            if (contentRoot != null)
            {
                contentRoot.SetActive(open || !settled);
            }

            if (drawerImage != null)
            {
                drawerImage.enabled = showPanel;
            }

            if (drawerOutline != null)
            {
                drawerOutline.enabled = showPanel;
            }
        }
    }
}
