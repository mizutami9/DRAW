using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class PlaytestFeedbackController : MonoBehaviour
    {
        private const string ConfigFileName = "feedback_config.json";
        private const float OpenCooldownSeconds = 1.5f;
        private const uint FullGameAppId = 5090120;
        private const string FullGameStoreUrl = "https://store.steampowered.com/app/5090120/";

        [Serializable]
        private sealed class FeedbackConfig
        {
            public bool enabled = true;
            public string feedbackUrl = string.Empty;
        }

        private GameObject titlePanel;
        private GameObject clearPanel;
        private Button titleButton;
        private Button clearButton;
        private Button titleWishlistButton;
        private Button clearWishlistButton;
        private FeedbackConfig config;
        private float nextAllowedOpenAt;
        private bool showClearFeedback;

        private bool IsPlaytestAvailable => DemoAccessPolicy.IsDemoBuild
            || Debug.isDebugBuild || Application.isEditor;
        private bool IsFeedbackAvailable => IsPlaytestAvailable && config != null && config.enabled;

        public void Configure(GameObject nextTitlePanel, GameObject nextClearPanel)
        {
            titlePanel = nextTitlePanel;
            clearPanel = nextClearPanel;
            LoadConfig();
            EnsureButtons();
            RefreshLabels();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLabels;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLabels;
        }

        private void LoadConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[Feedback] Config file is missing: " + path);
                config = new FeedbackConfig { enabled = false };
                return;
            }

            try
            {
                config = JsonUtility.FromJson<FeedbackConfig>(File.ReadAllText(path));
                if (config == null) config = new FeedbackConfig { enabled = false };
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Feedback] Could not read config: " + exception.Message);
                config = new FeedbackConfig { enabled = false };
            }
        }

        private void EnsureButtons()
        {
            if (titleButton == null && titlePanel != null)
            {
                Transform bar = titlePanel.transform.Find("TitleMenuBar");
                if (bar != null)
                {
                    titleButton = CreateButton("TitleFeedbackButton", bar, new Vector2(455f, 20f),
                        new Vector2(132f, 28f), new Color(1f, 0.86f, 0.32f, 0.98f), 10,
                        OpenFeedback);
                    AddTitleButtonIcon(titleButton, false);
                    titleWishlistButton = CreateButton("TitleWishlistButton", bar, new Vector2(455f, -18f),
                        new Vector2(132f, 28f), new Color(1f, 0.86f, 0.32f, 0.98f), 10,
                        OpenWishlist);
                    AddTitleButtonIcon(titleWishlistButton, true);
                }
            }

            if (clearButton == null && clearPanel != null)
            {
                Transform result = clearPanel.transform.Find("StageClearResult");
                if (result != null)
                {
                    clearButton = CreateButton("ClearFeedbackButton", result, new Vector2(-155f, -120f),
                        new Vector2(270f, 38f), new Color(0.84f, 0.75f, 1f, 0.98f), 14,
                        OpenFeedback);
                    clearWishlistButton = CreateButton("ClearWishlistButton", result, new Vector2(-155f, -168f),
                        new Vector2(270f, 42f), new Color(1f, 0.82f, 0.28f, 0.99f), 17,
                        OpenWishlist);
                }
            }
        }

        private Button CreateButton(string name, Transform parent, Vector2 position,
            Vector2 size, Color color, int fontSize, Action pressed)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(Outline), typeof(Shadow));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = root.GetComponent<Image>();
            DoodlePaperUi.Apply(image, color);
            Outline outline = root.GetComponent<Outline>();
            outline.enabled = false;
            Shadow shadow = root.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.17f);
            shadow.effectDistance = new Vector2(4f, -5f);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(root.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            Text label = labelObject.GetComponent<Text>();
            label.font = GetComponentInChildren<Text>(true)?.font;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.12f, 0.08f, 0.05f, 1f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;

            Button button = root.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => pressed?.Invoke());
            return button;
        }

        private static void AddTitleButtonIcon(Button button, bool wishlist)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.rectTransform.offsetMin = new Vector2(31f, 2f);
                label.rectTransform.offsetMax = new Vector2(-5f, -2f);
            }

            Transform existing = button.transform.Find("Icon");
            RectTransform root;
            if (existing == null)
            {
                GameObject iconObject = new GameObject("Icon", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(button.transform, false);
                root = iconObject.GetComponent<RectTransform>();
            }
            else
            {
                root = existing as RectTransform;
            }
            if (root == null) return;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(17f, 0f);
            root.sizeDelta = new Vector2(22f, 22f);
            root.localRotation = Quaternion.identity;
            root.SetAsLastSibling();
            for (int i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);

            Color ink = new Color(0.12f, 0.08f, 0.05f, 0.94f);
            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(wishlist ? 6 : 5);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = ink;
            image.raycastTarget = false;
        }

        private void RefreshLabels()
        {
            SetLabel(titleButton, GetCompactFeedbackLabel());
            SetLabel(clearButton, LocalizationManager.T("feedback_clear_button"));
            SetLabel(titleWishlistButton, LocalizationManager.T("wishlist_button"));
            SetLabel(clearWishlistButton, LocalizationManager.T("wishlist_button"));
        }

        private static string GetCompactFeedbackLabel()
        {
            string value = LocalizationManager.T("feedback_button");
            if (string.IsNullOrWhiteSpace(value)) return value;
            int separator = value.IndexOfAny(new[] { '/', '／', '|', '\n' });
            return separator > 0 ? value.Substring(0, separator).Trim() : value.Trim();
        }

        private static void SetLabel(Button button, string value)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null) return;
            label.text = value;
            label.font = LocalizationManager.LoadCurrentFont(label.font);
        }

        private void RefreshVisibility()
        {
            if (titleButton != null) titleButton.gameObject.SetActive(IsFeedbackAvailable);
            if (clearButton != null) clearButton.gameObject.SetActive(IsFeedbackAvailable && showClearFeedback);
            if (titleWishlistButton != null) titleWishlistButton.gameObject.SetActive(IsPlaytestAvailable);
            if (clearWishlistButton != null)
                clearWishlistButton.gameObject.SetActive(IsPlaytestAvailable && showClearFeedback);
        }

        public void SetClearContext(bool isCleared, string nextStageId)
        {
            showClearFeedback = isCleared && string.IsNullOrEmpty(nextStageId);
            RefreshVisibility();
        }

        public void BringClearButtonForward()
        {
            if (clearButton != null && clearButton.gameObject.activeSelf)
            {
                clearButton.transform.SetAsLastSibling();
            }
            if (clearWishlistButton != null && clearWishlistButton.gameObject.activeSelf)
            {
                clearWishlistButton.transform.SetAsLastSibling();
            }
        }

        private void OpenFeedback()
        {
            if (Time.unscaledTime < nextAllowedOpenAt) return;
            if (!TryGetValidUrl(out string url)) return;
            nextAllowedOpenAt = Time.unscaledTime + OpenCooldownSeconds;
            Debug.Log("[Feedback] Opening feedback form");
            Application.OpenURL(url);
        }

        private void OpenWishlist()
        {
            if (Time.unscaledTime < nextAllowedOpenAt) return;
            nextAllowedOpenAt = Time.unscaledTime + OpenCooldownSeconds;

#if NICO_DRAW_STEAM && !DISABLESTEAMWORKS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            try
            {
                if (SteamPlatformAuth.IsAvailable && Steamworks.SteamUtils.IsOverlayEnabled())
                {
                    Debug.Log("[Wishlist] Opening full game store page in Steam Overlay (App ID "
                        + FullGameAppId + ")");
                    Steamworks.SteamFriends.ActivateGameOverlayToStore(
                        new Steamworks.AppId_t(FullGameAppId),
                        Steamworks.EOverlayToStoreFlag.k_EOverlayToStoreFlag_None);
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Wishlist] Steam Overlay failed; opening browser instead: "
                    + exception.Message);
            }
#endif

            Debug.Log("[Wishlist] Steam Overlay unavailable; opening full game store page in browser");
            Application.OpenURL(FullGameStoreUrl);
        }

        private bool TryGetValidUrl(out string url)
        {
            url = config != null ? config.feedbackUrl?.Trim() : string.Empty;
            if (string.IsNullOrEmpty(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Debug.LogWarning("[Feedback] feedbackUrl is empty or invalid.");
                return false;
            }
            return true;
        }
    }
}
