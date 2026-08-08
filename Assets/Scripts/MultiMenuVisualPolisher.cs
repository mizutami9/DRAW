using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class MultiMenuVisualPolisher : MonoBehaviour
    {
        private static readonly Color RandomColor = new Color(0.72f, 0.88f, 1f, 0.97f);
        private static readonly Color RandomHoverColor = new Color(0.83f, 0.94f, 1f, 1f);
        private static readonly Color RoomColor = new Color(0.78f, 0.95f, 0.76f, 0.97f);
        private static readonly Color RoomHoverColor = new Color(0.87f, 1f, 0.82f, 1f);
        private static readonly Color BackColor = new Color(0.88f, 0.84f, 0.74f, 0.94f);
        private static readonly Color InkColor = new Color(0.14f, 0.1f, 0.07f, 0.78f);

        private void OnEnable()
        {
            Polish();
        }

        public void Polish()
        {
            PolishChoiceScreen();
            PolishRandomScreen();
            PolishRoomScreen();
            PolishCreateRoomScreen();
            PolishJoinRoomScreen();
            PolishLobbyScreen();
            EnsureAllButtonHoverScribbles();
        }

        private void PolishRandomScreen()
        {
            Transform random = transform.Find("MultiRandomScreen/MultiRandomScreenNote");
            if (random == null)
            {
                return;
            }

            LayoutSheet(random);
            AddBackgroundDoodles(random);
            HideTitle(random, "MultiRandomScreenTitle");

            Transform statusTransform = FindDeep(random, "MultiRandomStatus");
            Text status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            if (status != null)
            {
                status.fontSize = 22;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 18;
                status.resizeTextMaxSize = 22;
                status.lineSpacing = 1.1f;
                status.alignment = TextAnchor.UpperCenter;
                status.color = new Color(0.05f, 0.045f, 0.04f, 1f);
                PlaceBottomRect(status.rectTransform, new Vector2(0f, 132f), new Vector2(540f, 276f));
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
            }

            EnsureRandomSearchHeader(random, status);
            PolishReadyButton(random, "MultiRandomReadyButton", new Vector2(-105f, 54f));
            PolishSmallBackButton(random, "MultiRandomCancelButton", new Vector2(105f, 54f));
            SetButtonSize(random, "MultiRandomReadyButton", new Vector2(190f, 48f));
            SetButtonSize(random, "MultiRandomCancelButton", new Vector2(190f, 48f));
        }

        private void PolishChoiceScreen()
        {
            Transform choice = transform.Find("MultiChoiceScreen/MultiChoiceScreenNote");
            if (choice == null)
            {
                return;
            }

            LayoutSheet(choice);
            AddBackgroundDoodles(choice);
            PolishLargeButton(choice, "MultiRandomButton", RandomColor, RandomHoverColor, new Vector2(0f, 292f), true);
            PolishLargeButton(choice, "MultiRoomButton", RoomColor, RoomHoverColor, new Vector2(0f, 172f), false);
            PolishSmallBackButton(choice, "MultiBackTitleButton", new Vector2(0f, 70f));
            SetButtonSize(choice, "MultiBackTitleButton", new Vector2(200f, 48f));
        }

        private void PolishRoomScreen()
        {
            Transform room = transform.Find("MultiRoomScreen/MultiRoomScreenNote");
            if (room == null)
            {
                return;
            }

            LayoutSheet(room);
            PolishLargeButton(room, "MultiCreateRoomNavButton", RoomColor, RoomHoverColor, new Vector2(0f, 292f), false);
            PolishLargeButton(room, "MultiJoinRoomNavButton", RandomColor, RandomHoverColor, new Vector2(0f, 172f), true);
            PolishSmallBackButton(room, "MultiRoomBackButton", new Vector2(0f, 70f));
            SetButtonSize(room, "MultiRoomBackButton", new Vector2(200f, 48f));
        }

        private void PolishCreateRoomScreen()
        {
            Transform create = transform.Find("MultiCreateRoomScreen/MultiCreateRoomScreenNote");
            if (create == null)
            {
                return;
            }

            LayoutSheet(create);
            Transform bodyTransform = FindDeep(create, "MultiCreateRoomBody");
            Text body = bodyTransform != null ? bodyTransform.GetComponent<Text>() : null;
            if (body != null)
            {
                body.gameObject.SetActive(false);
            }

            Font font = body != null ? body.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            EnsureCreateRoomText(create, "MultiCreateMaxPlayersLabel", LocalizationManager.T("multi_max_players"), font, new Vector2(0f, 396f), 24, Color.black, false);
            EnsureCreateRoomText(create, "MultiCreateMaxPlayersValue", "<color=#1F63D8><b>4</b></color>", font, new Vector2(0f, 358f), 26, Color.black, true);
            EnsureCreateRoomText(create, "MultiCreateVisibilityLabel", LocalizationManager.T("multi_visibility_short"), font, new Vector2(0f, 246f), 24, Color.black, false);
            EnsureCreateRoomText(create, "MultiCreateVisibilityValue", "<color=#0E7A2A><b>" + LocalizationManager.T("multi_public") + "</b></color>", font, new Vector2(0f, 208f), 25, Color.black, true);

            MultiMenuController controller = GetComponent<MultiMenuController>();
            EnsureRuntimeButton(create, "MultiCreatePlayersMinusButton", "multi_prev", new Vector2(-74f, 286f), new Color(0.98f, 0.96f, 0.9f, 0.92f), () => controller?.ChangeCreateRoomMaxPlayers(-1));
            EnsureRuntimeButton(create, "MultiCreatePlayersPlusButton", "multi_next", new Vector2(74f, 286f), new Color(0.98f, 0.96f, 0.9f, 0.92f), () => controller?.ChangeCreateRoomMaxPlayers(1));
            EnsureRuntimeButton(create, "MultiCreateVisibilityButton", "multi_toggle_visibility", new Vector2(0f, 132f), new Color(0.78f, 0.9f, 1f, 0.92f), () => controller?.ToggleCreateRoomVisibility());

            PolishSmallBackButton(create, "MultiCreatePlayersMinusButton", new Vector2(-72f, 292f));
            PolishSmallBackButton(create, "MultiCreatePlayersPlusButton", new Vector2(72f, 292f));
            PolishSmallBackButton(create, "MultiCreateVisibilityButton", new Vector2(0f, 142f));
            PolishReadyButton(create, "MultiCreateButton", new Vector2(-105f, 52f));
            PolishSmallBackButton(create, "MultiCreateBackButton", new Vector2(105f, 52f));
            SetButtonSize(create, "MultiCreatePlayersMinusButton", new Vector2(64f, 46f));
            SetButtonSize(create, "MultiCreatePlayersPlusButton", new Vector2(64f, 46f));
            SetButtonSize(create, "MultiCreateVisibilityButton", new Vector2(260f, 46f));
            SetButtonSize(create, "MultiCreateButton", new Vector2(190f, 48f));
            SetButtonSize(create, "MultiCreateBackButton", new Vector2(190f, 48f));
        }

        private static Text EnsureCreateRoomText(Transform parent, string name, string value, Font font, Vector2 bottomPosition, int fontSize, Color color, bool richText)
        {
            Transform existing = FindDeep(parent, name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(parent, false);
                text = textObject.GetComponent<Text>();
            }

            text.font = font;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, fontSize - 6);
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.supportRichText = richText;
            text.raycastTarget = false;
            text.text = value;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = bottomPosition;
            rect.sizeDelta = new Vector2(260f, 34f);
            return text;
        }

        private static void LayoutSheet(Transform sheetTransform)
        {
            RectTransform sheet = sheetTransform as RectTransform;
            if (sheet == null)
            {
                return;
            }

            sheet.anchorMin = new Vector2(0.5f, 0.5f);
            sheet.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.anchoredPosition = new Vector2(0f, 20f);
            sheet.sizeDelta = new Vector2(700f, 520f);

            for (int i = 0; i < sheet.childCount; i++)
            {
                Text title = sheet.GetChild(i).GetComponent<Text>();
                if (title == null || !title.name.EndsWith("ScreenTitle", System.StringComparison.Ordinal))
                {
                    continue;
                }

                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0f, 1f);
                titleRect.anchorMax = new Vector2(1f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -22f);
                titleRect.sizeDelta = new Vector2(-64f, 52f);
                title.alignment = TextAnchor.MiddleCenter;
                title.fontSize = 34;
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 24;
                title.resizeTextMaxSize = 34;
            }
        }

        private static void PlaceBottomRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetButtonSize(Transform parent, string name, Vector2 size)
        {
            Button button = FindButton(parent, name);
            RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect != null)
            {
                rect.sizeDelta = size;
            }
        }

        private void PolishJoinRoomScreen()
        {
            Transform join = transform.Find("MultiJoinRoomScreen/MultiJoinRoomScreenNote");
            if (join == null)
            {
                return;
            }

            LayoutSheet(join);
            Transform bodyTransform = FindDeep(join, "MultiJoinRoomBody");
            Text body = bodyTransform != null ? bodyTransform.GetComponent<Text>() : null;
            if (body != null)
            {
                body.gameObject.SetActive(false);
            }

            Font font = body != null ? body.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            EnsureCreateRoomText(join, "MultiJoinLobbyIdLabel", LocalizationManager.T("multi_room_code_label"), font, new Vector2(0f, 338f), 25, Color.black, false);

            Transform inputTransform = FindDeep(join, "MultiJoinAddressInput");
            RectTransform inputRect = inputTransform != null ? inputTransform.GetComponent<RectTransform>() : null;
            if (inputRect != null)
            {
                inputRect.anchoredPosition = new Vector2(0f, 266f);
                inputRect.sizeDelta = new Vector2(440f, 52f);
            }

            Transform refresh = FindDeep(join, "MultiRefreshButton");
            if (refresh != null)
            {
                refresh.gameObject.SetActive(false);
            }

            PolishReadyButton(join, "MultiJoinButton", new Vector2(-105f, 70f));
            PolishSmallBackButton(join, "MultiJoinBackButton", new Vector2(105f, 70f));
            SetButtonSize(join, "MultiJoinButton", new Vector2(190f, 48f));
            SetButtonSize(join, "MultiJoinBackButton", new Vector2(190f, 48f));
        }

        private void PolishLobbyScreen()
        {
            Transform lobby = transform.Find("MultiLobbyScreen/MultiLobbyScreenNote");
            if (lobby == null)
            {
                return;
            }

            LayoutSheet(lobby);
            Transform statusTransform = FindDeep(lobby, "MultiLobbyStatus");
            Text status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            if (status != null)
            {
                status.fontSize = 21;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 15;
                status.resizeTextMaxSize = 21;
                status.lineSpacing = 1.08f;
                status.alignment = TextAnchor.UpperCenter;
                status.color = new Color(0.05f, 0.045f, 0.04f, 1f);
                PlaceBottomRect(status.rectTransform, new Vector2(0f, 154f), new Vector2(550f, 244f));
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
            }

            Font noticeFont = status != null && status.font != null ? status.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text notice = EnsureCreateRoomText(lobby, "MultiLobbyNotice", string.Empty, noticeFont, new Vector2(0f, 418f), 18, new Color(0.18f, 0.13f, 0.08f, 1f), false);
            notice.rectTransform.sizeDelta = new Vector2(520f, 32f);
            if (string.IsNullOrEmpty(notice.text))
            {
                notice.gameObject.SetActive(false);
            }

            Transform drawButton = FindDeep(lobby, "MultiLobbyDrawButton");
            if (drawButton != null)
            {
                drawButton.gameObject.SetActive(false);
            }

            PolishReadyButton(lobby, "MultiLobbyReadyButton", new Vector2(-116f, 86f));
            PolishSmallBackButton(lobby, "MultiLobbyStageButton", new Vector2(116f, 86f));
            PolishSmallBackButton(lobby, "MultiLobbyCopyIdButton", new Vector2(-116f, 28f));
            PolishSmallBackButton(lobby, "MultiLobbyExitButton", new Vector2(116f, 28f));
            SetButtonSize(lobby, "MultiLobbyReadyButton", new Vector2(210f, 48f));
            SetButtonSize(lobby, "MultiLobbyStageButton", new Vector2(210f, 48f));
            SetButtonSize(lobby, "MultiLobbyCopyIdButton", new Vector2(210f, 48f));
            SetButtonSize(lobby, "MultiLobbyExitButton", new Vector2(210f, 48f));
            SetButtonText(lobby, "MultiLobbyStageButton", LocalizationManager.T("multi_stage_select"));
        }

        private static Button EnsureRuntimeButton(Transform parent, string name, string localizationKey, Vector2 position, Color color, UnityEngine.Events.UnityAction action)
        {
            Button existing = FindButton(parent, name);
            if (existing != null)
            {
                return existing;
            }

            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(132f, 46f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;

            Button button = buttonObject.GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = 18;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.black;
            label.text = LocalizationManager.T(localizationKey);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private void PolishLargeButton(Transform parent, string name, Color color, Color hoverColor, Vector2 position, bool globeIcon)
        {
            Button button = FindButton(parent, name);
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(410f, 100f);
            rect.localRotation = Quaternion.identity;

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = button.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.12f, 0.09f, 0.04f, 0.26f);
            shadow.effectDistance = new Vector2(6f, -7f);

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.15f, 0.1f, 0.06f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 24;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 24;
                label.lineSpacing = 1.08f;
                label.color = new Color(0.06f, 0.05f, 0.04f, 1f);
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.rectTransform.offsetMin = new Vector2(88f, 8f);
                label.rectTransform.offsetMax = new Vector2(-20f, -8f);
            }

            EnsureHover(button, color, hoverColor);
            EnsureFrame(button.transform, rect.sizeDelta, 3f, "MultiLargeFrame");
            if (globeIcon)
            {
                EnsureGlobeIcon(button.transform);
            }
            else
            {
                EnsureFriendsIcon(button.transform);
            }
        }

        private void PolishSmallBackButton(Transform parent, string name, Vector2 position)
        {
            Button button = FindButton(parent, name);
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(156f, 46f);

            Image image = button.GetComponent<Image>();
            Color normalColor = image != null ? image.color : BackColor;
            if (name.IndexOf("Back", System.StringComparison.Ordinal) >= 0)
            {
                normalColor = BackColor;
            }

            if (image != null)
            {
                image.color = normalColor;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                if (name.IndexOf("Back", System.StringComparison.Ordinal) >= 0)
                {
                    label.text = LocalizationManager.T("option_back");
                }

                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 20;
                label.color = new Color(0.05f, 0.04f, 0.03f, 1f);
            }

            EnsureHover(button, normalColor, Color.Lerp(normalColor, Color.white, 0.18f), 1.025f, 2f);
            EnsureFrame(button.transform, rect.sizeDelta, 2.1f, "MultiBackFrame");
        }

        private void PolishReadyButton(Transform parent, string name, Vector2 position)
        {
            Button button = FindButton(parent, name);
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(156f, 46f);

            Image image = button.GetComponent<Image>();
            Color normalColor = image != null ? image.color : new Color(0.82f, 0.82f, 0.76f, 0.94f);
            if (image != null)
            {
                image.color = normalColor;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 20;
                label.color = Color.black;
            }

            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = button.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.12f, 0.09f, 0.04f, 0.24f);
            shadow.effectDistance = new Vector2(4f, -5f);
            EnsureHover(button, normalColor, Color.Lerp(normalColor, Color.white, 0.18f), 1.04f, 4f);
            EnsureFrame(button.transform, rect.sizeDelta, 2.6f, "MultiReadyFrame");
        }

        private static Button FindButton(Transform parent, string name)
        {
            Transform found = FindDeep(parent, name);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private static void SetButtonText(Transform parent, string name, string text)
        {
            Button button = FindButton(parent, name);
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void HideTitle(Transform parent, string name)
        {
            Transform title = FindDeep(parent, name);
            if (title != null)
            {
                title.gameObject.SetActive(false);
            }
        }

        private static void EnsureRandomSearchHeader(Transform parent, Text template)
        {
            if (template == null)
            {
                return;
            }

            Text label = EnsureHeaderText(parent, "MultiRandomSearchingLabel", template, new Vector2(-24f, -58f), new Vector2(420f, 40f), TextAnchor.MiddleCenter);
            label.text = LocalizationManager.T("multi_searching_players");

            Text dots = EnsureHeaderText(parent, "MultiRandomSearchingDots", template, new Vector2(208f, -58f), new Vector2(64f, 40f), TextAnchor.MiddleLeft);
            dots.text = string.Empty;
        }

        private static Text EnsureHeaderText(Transform parent, string name, Text template, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            Transform existing = FindDeep(parent, name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(parent, false);
                text = textObject.GetComponent<Text>();
                text.font = template.font;
            }

            text.fontSize = 24;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = 24;
            text.color = new Color(0.05f, 0.045f, 0.04f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
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

        private static void EnsureHover(Button button, Color normalColor, Color hoverColor, float scale = 1.055f, float lift = 5f)
        {
            MultiMenuButtonHover hover = button.GetComponent<MultiMenuButtonHover>();
            if (hover == null)
            {
                hover = button.gameObject.AddComponent<MultiMenuButtonHover>();
            }

            hover.Configure(button.GetComponent<Image>(), normalColor, hoverColor, scale, lift);
        }

        private void EnsureAllButtonHoverScribbles()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.GetComponent<MultiMenuButtonHover>() != null)
                {
                    continue;
                }

                Image image = button.GetComponent<Image>();
                Color normal = image != null ? image.color : new Color(0.98f, 0.94f, 0.82f, 0.94f);
                Color hover = Color.Lerp(normal, Color.white, 0.18f);
                EnsureHover(button, normal, hover, 1.035f, 3f);
            }
        }

        private static void EnsureFrame(Transform parent, Vector2 size, float width, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }

            Outline outline = parent.GetComponent<Outline>();
            if (outline == null)
            {
                outline = parent.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private static void AddBackgroundDoodles(Transform parent)
        {
            Transform existing = parent.Find("MultiChoiceDoodles");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private static void EnsureGlobeIcon(Transform parent)
        {
            if (parent.Find("MultiGlobeIcon") != null)
            {
                return;
            }

            GameObject root = CreateIconRoot(parent, "MultiGlobeIcon");
            Color blue = new Color(0.1f, 0.32f, 0.86f, 0.86f);
            CreateCircle(root.transform, Vector2.zero, 26f, blue, 3.6f);
            CreateLine(root.transform, new Vector2(-22f, 0f), new Vector2(22f, 1f), 2.4f, blue);
            CreateLine(root.transform, new Vector2(0f, 25f), new Vector2(0f, -25f), 2.4f, blue);
            CreateLine(root.transform, new Vector2(-14f, 20f), new Vector2(10f, -21f), 2.1f, blue);
        }

        private static void EnsureFriendsIcon(Transform parent)
        {
            if (parent.Find("MultiFriendsIcon") != null)
            {
                return;
            }

            GameObject root = CreateIconRoot(parent, "MultiFriendsIcon");
            Color ink = new Color(0.08f, 0.08f, 0.07f, 0.9f);
            CreateCircle(root.transform, new Vector2(-13f, 12f), 10f, ink, 3.1f);
            CreateCircle(root.transform, new Vector2(14f, 12f), 10f, ink, 3.1f);
            CreateLine(root.transform, new Vector2(-24f, -16f), new Vector2(-14f, 2f), 3.1f, ink);
            CreateLine(root.transform, new Vector2(-14f, 2f), new Vector2(-2f, -16f), 3.1f, ink);
            CreateLine(root.transform, new Vector2(2f, -16f), new Vector2(14f, 2f), 3.1f, ink);
            CreateLine(root.transform, new Vector2(14f, 2f), new Vector2(26f, -16f), 3.1f, ink);
            CreateLine(root.transform, new Vector2(-2f, -6f), new Vector2(4f, -6f), 3f, ink);
        }

        private static GameObject CreateIconRoot(Transform parent, string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(52f, 0f);
            rect.sizeDelta = new Vector2(70f, 70f);
            return root;
        }

        private static void CreateCircle(Transform parent, Vector2 center, float radius, Color color, float width)
        {
            const int segments = 24;
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(i * 1.83f) * 0.045f;
                Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * wobble;
                CreateLine(parent, previous, next, width, color);
                previous = next;
            }
        }

        private static void CreateLine(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject line = new GameObject("MultiSketchLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }
    }

    public sealed class MultiMenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Image targetImage;
        private Color normalColor;
        private Color hoverColor;
        private RectTransform rectTransform;
        private GameObject selectionScribble;
        private RectTransform[] scribbleLines;
        private Vector2 basePosition;
        private Vector3 baseScale;
        private float hoverScale = 1.05f;
        private float lift = 5f;
        private float scribbleTime;
        private const float ScribbleDrawDuration = 0.24f;
        private bool hovering;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            CaptureBase();
            EnsureSelectionScribble();
        }

        private void Update()
        {
            if (!hovering || scribbleLines == null || scribbleLines.Length == 0)
            {
                return;
            }

            scribbleTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(scribbleTime / ScribbleDrawDuration);
            ApplyScribbleProgress(progress);
        }

        private void OnEnable()
        {
            CaptureBase();
            EnsureSelectionScribble();
            Apply(false);
        }

        public void Configure(Image image, Color normal, Color hover, float scale, float liftAmount)
        {
            targetImage = image;
            normalColor = normal;
            hoverColor = hover;
            hoverScale = scale;
            lift = liftAmount;
            rectTransform = GetComponent<RectTransform>();
            CaptureBase();
            EnsureSelectionScribble();
            Apply(hovering);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Apply(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Apply(false);
        }

        private void CaptureBase()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (rectTransform != null && !hovering)
            {
                basePosition = rectTransform.anchoredPosition;
                baseScale = rectTransform.localScale;
            }
        }

        private void Apply(bool value)
        {
            hovering = value;
            if (targetImage != null)
            {
                targetImage.color = hovering ? hoverColor : normalColor;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = basePosition + (hovering ? new Vector2(0f, lift) : Vector2.zero);
                rectTransform.localScale = hovering ? baseScale * hoverScale : baseScale;
            }

            if (selectionScribble != null)
            {
                selectionScribble.SetActive(hovering);
                scribbleTime = 0f;
                ApplyScribbleProgress(hovering ? 0f : 1f);
            }
        }

        private void EnsureSelectionScribble()
        {
            Transform existing = transform.Find("MultiHoverScribble");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
            selectionScribble = null;
        }

        private static Vector2 EllipsePoint(int index, int segments, float halfWidth, float halfHeight)
        {
            float t = index / (float)segments;
            float angle = t * Mathf.PI * 2f;
            float wobble = 1f + Mathf.Sin(index * 1.73f) * 0.018f + Mathf.Cos(index * 0.91f) * 0.012f;
            return new Vector2(Mathf.Cos(angle) * halfWidth * wobble, Mathf.Sin(angle) * halfHeight * wobble);
        }

        private void CacheScribbleLines()
        {
            if (selectionScribble == null)
            {
                scribbleLines = null;
                return;
            }

            int count = 0;
            for (int i = 0; i < selectionScribble.transform.childCount; i++)
            {
                if (selectionScribble.transform.GetChild(i).name.StartsWith("MultiHoverScribbleLine", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            scribbleLines = new RectTransform[count];
            int index = 0;
            for (int i = 0; i < selectionScribble.transform.childCount; i++)
            {
                Transform child = selectionScribble.transform.GetChild(i);
                if (child.name.StartsWith("MultiHoverScribbleLine", System.StringComparison.Ordinal))
                {
                    scribbleLines[index++] = child.GetComponent<RectTransform>();
                }
            }
        }

        private void ApplyScribbleProgress(float progress)
        {
            if (scribbleLines == null)
            {
                return;
            }

            float total = scribbleLines.Length;
            for (int i = 0; i < scribbleLines.Length; i++)
            {
                RectTransform line = scribbleLines[i];
                if (line == null)
                {
                    continue;
                }

                float local = Mathf.Clamp01(progress * total - i);
                Vector3 scale = line.localScale;
                scale.x = Mathf.SmoothStep(0f, 1f, local);
                scale.y = local <= 0f ? 0f : 1f;
                line.localScale = scale;
            }
        }

        private static void CreateCrayonScribbleLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, int index)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Color soft = new Color(color.r, color.g, color.b, color.a * 0.46f);
            CreateScribbleLine(parent, from, to, width, color);
            CreateScribbleLine(parent, from + normal * 2.1f, to + normal * 1.4f, width * 0.42f, soft);
            CreateScribbleLine(parent, from - normal * 1.6f + direction * Mathf.Sin(index * 1.7f), to - normal * 1.2f, width * 0.34f, soft);
        }

        private static void CreateScribbleLine(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject line = new GameObject("MultiHoverScribbleLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.anchoredPosition = from;
            lineRect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
