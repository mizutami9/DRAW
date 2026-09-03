using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class MultiMenuVisualPolisher : MonoBehaviour
    {
        // Match the title screen palette exactly.
        private static readonly Color RandomColor = new Color(0.22f, 0.78f, 0.92f, 1f);
        private static readonly Color RandomHoverColor = new Color(0.34f, 0.86f, 0.98f, 1f);
        private static readonly Color RoomColor = new Color(0.45f, 0.88f, 0.42f, 1f);
        private static readonly Color RoomHoverColor = new Color(0.58f, 0.95f, 0.52f, 1f);
        private static readonly Color BackColor = new Color(1f, 0.45f, 0.34f, 1f);
        private static readonly Color YellowColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color VioletColor = new Color(0.66f, 0.52f, 0.96f, 1f);
        private static readonly Color InkColor = new Color(0.14f, 0.1f, 0.07f, 0.78f);

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged -= HandleLanguageChanged;
            LocalizationManager.LanguageChanged += HandleLanguageChanged;
            Polish();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
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

            LayoutBottomSheet(random, 278f);
            PlaceScreenHeading(
                random,
                "MultiRandomScreenTitle",
                "multi_random_match",
                new Vector2(0f, 226f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(random, "MultiRandomHeadingStroke", new Vector2(0f, 222f), new Vector2(520f, 48f), RandomColor);
            MakeScreenOverlayTransparent(random);
            AddBackgroundDoodles(random);

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
                PlaceBottomRect(status.rectTransform, new Vector2(-265f, 16f), new Vector2(470f, 166f));
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
                EnsureTextBackdrop(random, status.rectTransform, "MultiRandomStatusCard", Color.Lerp(RandomColor, Color.white, 0.76f));
            }

            EnsureRandomSearchHeader(random, status);
            PlaceBottomText(random, "MultiRandomSearchingLabel", new Vector2(-285f, 188f), new Vector2(360f, 28f), TextAnchor.MiddleCenter);
            PlaceBottomText(random, "MultiRandomSearchingDots", new Vector2(-83f, 188f), new Vector2(44f, 28f), TextAnchor.MiddleLeft);
            PolishReadyButton(random, "MultiRandomReadyButton", Vector2.zero);
            PolishSmallBackButton(random, "MultiRandomCancelButton", Vector2.zero);
            PlaceButtonBottom(random, "MultiRandomReadyButton", new Vector2(350f, 105f), new Vector2(320f, 62f));
            PlaceButtonBottom(random, "MultiRandomCancelButton", new Vector2(350f, 32f), new Vector2(320f, 62f));
            SetButtonPalette(random, "MultiRandomReadyButton", RoomColor, RoomHoverColor);
            SetButtonPalette(random, "MultiRandomCancelButton", BackColor, Color.Lerp(BackColor, Color.white, 0.18f));
        }

        private void PolishChoiceScreen()
        {
            Transform choice = transform.Find("MultiChoiceScreen/MultiChoiceScreenNote");
            if (choice == null)
            {
                return;
            }

            LayoutBottomSheet(choice, 178f);
            PlaceScreenHeading(
                choice,
                "MultiChoiceScreenTitle",
                "multi_play",
                new Vector2(0f, 132f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(choice, "MultiChoiceHeadingStroke", new Vector2(0f, 128f), new Vector2(520f, 48f), RandomColor);
            AddBackgroundDoodles(choice);
            PolishLargeButton(choice, "MultiRandomButton", RandomColor, RandomHoverColor, Vector2.zero, true);
            PolishLargeButton(choice, "MultiRoomButton", RoomColor, RoomHoverColor, Vector2.zero, false);
            PolishSmallBackButton(choice, "MultiBackTitleButton", Vector2.zero);
            PlaceButtonBottom(choice, "MultiRandomButton", new Vector2(-235f, 20f), new Vector2(330f, 76f));
            PlaceButtonBottom(choice, "MultiRoomButton", new Vector2(130f, 20f), new Vector2(330f, 76f));
            PlaceButtonBottom(choice, "MultiBackTitleButton", new Vector2(475f, 30f), new Vector2(180f, 54f));
        }

        private void PolishRoomScreen()
        {
            Transform room = transform.Find("MultiRoomScreen/MultiRoomScreenNote");
            if (room == null)
            {
                return;
            }

            LayoutBottomSheet(room, 178f);
            PlaceScreenHeading(
                room,
                "MultiRoomScreenTitle",
                "multi_room_title",
                new Vector2(0f, 132f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(room, "MultiRoomHeadingStroke", new Vector2(0f, 128f), new Vector2(520f, 48f), RoomColor);
            PolishLargeButton(room, "MultiCreateRoomNavButton", RoomColor, RoomHoverColor, Vector2.zero, false);
            PolishLargeButton(room, "MultiJoinRoomNavButton", RandomColor, RandomHoverColor, Vector2.zero, true);
            PolishSmallBackButton(room, "MultiRoomBackButton", Vector2.zero);
            PlaceButtonBottom(room, "MultiCreateRoomNavButton", new Vector2(-235f, 20f), new Vector2(330f, 76f));
            PlaceButtonBottom(room, "MultiJoinRoomNavButton", new Vector2(130f, 20f), new Vector2(330f, 76f));
            PlaceButtonBottom(room, "MultiRoomBackButton", new Vector2(475f, 30f), new Vector2(180f, 54f));
        }

        private void PolishCreateRoomScreen()
        {
            Transform create = transform.Find("MultiCreateRoomScreen/MultiCreateRoomScreenNote");
            if (create == null)
            {
                return;
            }

            LayoutBottomSheet(create, 270f);
            PlaceScreenHeading(
                create,
                "MultiCreateRoomScreenTitle",
                "multi_room_create_title",
                new Vector2(0f, 218f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(create, "MultiCreateHeadingStroke", new Vector2(0f, 214f), new Vector2(520f, 48f), YellowColor);
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

            Text maxLabel = EnsureCreateRoomText(create, "MultiCreateMaxPlayersLabel", string.Empty, font, Vector2.zero, 20, Color.black, false);
            Text visibilityLabel = EnsureCreateRoomText(create, "MultiCreateVisibilityLabel", string.Empty, font, Vector2.zero, 20, Color.black, false);
            maxLabel.gameObject.SetActive(false);
            visibilityLabel.gameObject.SetActive(false);
            Text maxValue = EnsureCreateRoomText(
                create,
                "MultiCreateMaxPlayersValue",
                LocalizationManager.T("multi_max_players") + ":  <color=#1F63D8><b>4</b></color>",
                font,
                new Vector2(-350f, 157f),
                22,
                Color.black,
                true);
            maxValue.rectTransform.sizeDelta = new Vector2(230f, 44f);
            Text visibilityValue = EnsureCreateRoomText(
                create,
                "MultiCreateVisibilityValue",
                LocalizationManager.T("multi_visibility") + ":  <color=#0E7A2A><b>" + LocalizationManager.T("multi_public") + "</b></color>",
                font,
                new Vector2(-280f, 112f),
                21,
                Color.black,
                true);
            visibilityValue.rectTransform.sizeDelta = new Vector2(280f, 38f);

            MultiMenuController controller = GetComponent<MultiMenuController>();
            EnsureRuntimeButton(create, "MultiCreatePlayersMinusButton", "multi_prev", Vector2.zero, new Color(0.98f, 0.96f, 0.9f, 0.92f), () => controller?.ChangeCreateRoomMaxPlayers(-1));
            EnsureRuntimeButton(create, "MultiCreatePlayersPlusButton", "multi_next", Vector2.zero, new Color(0.98f, 0.96f, 0.9f, 0.92f), () => controller?.ChangeCreateRoomMaxPlayers(1));
            EnsureRuntimeButton(create, "MultiCreateVisibilityButton", "multi_toggle_visibility", Vector2.zero, new Color(0.78f, 0.9f, 1f, 0.92f), () => controller?.ToggleCreateRoomVisibility());

            PolishSmallBackButton(create, "MultiCreatePlayersMinusButton", Vector2.zero);
            PolishSmallBackButton(create, "MultiCreatePlayersPlusButton", Vector2.zero);
            PolishSmallBackButton(create, "MultiCreateVisibilityButton", Vector2.zero);
            PolishReadyButton(create, "MultiCreateButton", Vector2.zero);
            PolishSmallBackButton(create, "MultiCreateBackButton", Vector2.zero);
            PlaceButtonBottom(create, "MultiCreatePlayersMinusButton", new Vector2(-198f, 145f), new Vector2(66f, 50f));
            PlaceButtonBottom(create, "MultiCreatePlayersPlusButton", new Vector2(-115f, 145f), new Vector2(66f, 50f));
            PlaceButtonBottom(create, "MultiCreateVisibilityButton", new Vector2(-280f, 35f), new Vector2(280f, 52f));
            PlaceButtonBottom(create, "MultiCreateButton", new Vector2(285f, 130f), new Vector2(300f, 72f));
            PlaceButtonBottom(create, "MultiCreateBackButton", new Vector2(285f, 42f), new Vector2(300f, 72f));
            SetButtonPalette(create, "MultiCreatePlayersMinusButton", YellowColor, Color.Lerp(YellowColor, Color.white, 0.2f));
            SetButtonPalette(create, "MultiCreatePlayersPlusButton", YellowColor, Color.Lerp(YellowColor, Color.white, 0.2f));
            SetButtonPalette(create, "MultiCreateVisibilityButton", VioletColor, Color.Lerp(VioletColor, Color.white, 0.18f));
            SetButtonPalette(create, "MultiCreateButton", RoomColor, RoomHoverColor);
            SetButtonPalette(create, "MultiCreateBackButton", BackColor, Color.Lerp(BackColor, Color.white, 0.18f));
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

        private static void LayoutBottomSheet(Transform sheetTransform, float height)
        {
            RectTransform sheet = sheetTransform as RectTransform;
            if (sheet == null)
            {
                return;
            }

            sheet.anchorMin = new Vector2(0.5f, 0f);
            sheet.anchorMax = new Vector2(0.5f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.anchoredPosition = new Vector2(0f, 12f);
            sheet.sizeDelta = new Vector2(1240f, height);
            sheet.localScale = Vector3.one;

            for (int i = 0; i < sheet.childCount; i++)
            {
                Text title = sheet.GetChild(i).GetComponent<Text>();
                if (title == null || !title.name.EndsWith("ScreenTitle", System.StringComparison.Ordinal))
                {
                    continue;
                }

                RectTransform titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(-535f, 0f);
                titleRect.sizeDelta = new Vector2(150f, 68f);
                title.alignment = TextAnchor.MiddleCenter;
                title.fontSize = 30;
                title.resizeTextForBestFit = true;
                title.resizeTextMinSize = 22;
                title.resizeTextMaxSize = 30;
            }
        }

        private static void PlaceScreenHeading(
            Transform parent,
            string name,
            string localizationKey,
            Vector2 position,
            Vector2 size)
        {
            Transform found = FindDeep(parent, name);
            Text title = found != null ? found.GetComponent<Text>() : null;
            if (title == null)
            {
                return;
            }

            title.gameObject.SetActive(true);
            LocalizedText localized = title.GetComponent<LocalizedText>();
            localized?.SetKey(localizationKey);
            title.text = LocalizationManager.T(localizationKey);
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 22;
            title.resizeTextMaxSize = 30;
            title.alignment = TextAnchor.MiddleCenter;
            RectTransform rect = title.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureHeadingPaintStroke(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            Transform existing = parent.Find(name);
            GameObject stroke = existing != null ? existing.gameObject : null;
            if (stroke == null)
            {
                stroke = new GameObject(name, typeof(RectTransform));
                stroke.transform.SetParent(parent, false);
                for (int i = 0; i < 4; i++)
                {
                    GameObject stripe = new GameObject("PaintStripe" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    stripe.transform.SetParent(stroke.transform, false);
                    Image image = stripe.GetComponent<Image>();
                    image.raycastTarget = false;
                    image.color = new Color(color.r, color.g, color.b, 0.2f + i * 0.07f);
                    RectTransform stripeRect = stripe.GetComponent<RectTransform>();
                    stripeRect.anchorMin = new Vector2(0.5f, 0.5f);
                    stripeRect.anchorMax = new Vector2(0.5f, 0.5f);
                    stripeRect.pivot = new Vector2(0.5f, 0.5f);
                    stripeRect.anchoredPosition = new Vector2((i - 1.5f) * 4f, (i - 1.5f) * 3f);
                    stripeRect.sizeDelta = new Vector2(size.x - i * 13f, 13f + i * 2f);
                    stripeRect.localRotation = Quaternion.Euler(0f, 0f, -1.6f + i * 1.1f);
                }
            }

            RectTransform rect = stroke.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Transform title = FindDeep(parent, name == "MultiCreateHeadingStroke" ? "MultiCreateRoomScreenTitle" : "MultiLobbyScreenTitle");
            if (name == "MultiJoinHeadingStroke")
            {
                title = FindDeep(parent, "MultiJoinRoomScreenTitle");
            }
            else if (name == "MultiRandomHeadingStroke")
            {
                title = FindDeep(parent, "MultiRandomScreenTitle");
            }
            else if (name == "MultiChoiceHeadingStroke")
            {
                title = FindDeep(parent, "MultiChoiceScreenTitle");
            }
            else if (name == "MultiRoomHeadingStroke")
            {
                title = FindDeep(parent, "MultiRoomScreenTitle");
            }
            if (title != null)
            {
                stroke.transform.SetAsFirstSibling();
                title.SetAsLastSibling();
            }
        }

        private static void LayoutWaitingSheet(Transform sheetTransform)
        {
            RectTransform sheet = sheetTransform as RectTransform;
            if (sheet == null)
            {
                return;
            }

            sheet.anchorMin = new Vector2(0.5f, 0f);
            sheet.anchorMax = new Vector2(0.5f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.anchoredPosition = new Vector2(0f, 14f);
            sheet.sizeDelta = new Vector2(1240f, 156f);
            sheet.localScale = Vector3.one;
        }

        private static void MakeScreenOverlayTransparent(Transform sheet)
        {
            Transform screen = sheet != null ? sheet.parent : null;
            Image overlay = screen != null ? screen.GetComponent<Image>() : null;
            if (overlay == null)
            {
                return;
            }

            Color color = overlay.color;
            color.a = 0f;
            overlay.color = color;
            overlay.raycastTarget = false;
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

        private static void PlaceBottomText(Transform parent, string name, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            Transform found = FindDeep(parent, name);
            Text label = found != null ? found.GetComponent<Text>() : null;
            if (label == null)
            {
                return;
            }

            PlaceBottomRect(label.rectTransform, position, size);
            label.alignment = alignment;
        }

        private static void EnsureTextBackdrop(Transform parent, RectTransform target, string name, Color color)
        {
            if (target == null)
            {
                return;
            }
            Transform existing = parent.Find(name);
            GameObject card = existing != null ? existing.gameObject : null;
            if (card == null)
            {
                card = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                card.transform.SetParent(parent, false);
            }

            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.anchoredPosition = target.anchoredPosition + new Vector2(0f, -2f);
            rect.sizeDelta = target.sizeDelta + new Vector2(18f, 10f);
            Image image = card.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Outline outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.AddComponent<Outline>();
            }
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2f, -2f);
            card.transform.SetSiblingIndex(target.GetSiblingIndex());
            target.SetAsLastSibling();
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

        private static void PlaceButtonBottom(Transform parent, string name, Vector2 position, Vector2 size)
        {
            Button button = FindButton(parent, name);
            RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;

            MultiMenuButtonHover hover = button.GetComponent<MultiMenuButtonHover>();
            if (hover != null)
            {
                hover.CaptureCurrentLayout();
            }
        }

        private static void SetButtonPalette(Transform parent, string name, Color normal, Color hoverColor)
        {
            Button button = FindButton(parent, name);
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = normal;
            }

            MultiMenuButtonHover hover = button.GetComponent<MultiMenuButtonHover>();
            if (hover != null)
            {
                hover.Configure(image, normal, hoverColor, 1.025f, 2f);
                hover.CaptureCurrentLayout();
            }
        }

        private void PolishJoinRoomScreen()
        {
            Transform join = transform.Find("MultiJoinRoomScreen/MultiJoinRoomScreenNote");
            if (join == null)
            {
                return;
            }

            LayoutBottomSheet(join, 230f);
            PlaceScreenHeading(
                join,
                "MultiJoinRoomScreenTitle",
                "multi_room_join_title",
                new Vector2(0f, 180f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(join, "MultiJoinHeadingStroke", new Vector2(0f, 176f), new Vector2(520f, 48f), RandomColor);
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

            EnsureCreateRoomText(join, "MultiJoinLobbyIdLabel", LocalizationManager.T("multi_room_id"), font, new Vector2(-280f, 138f), 21, Color.black, false);

            Transform inputTransform = FindDeep(join, "MultiJoinAddressInput");
            RectTransform inputRect = inputTransform != null ? inputTransform.GetComponent<RectTransform>() : null;
            if (inputRect != null)
            {
                inputRect.anchorMin = new Vector2(0.5f, 0f);
                inputRect.anchorMax = new Vector2(0.5f, 0f);
                inputRect.pivot = new Vector2(0.5f, 0f);
                inputRect.anchoredPosition = new Vector2(-280f, 63f);
                inputRect.sizeDelta = new Vector2(500f, 54f);
            }

            Transform refresh = FindDeep(join, "MultiRefreshButton");
            if (refresh != null)
            {
                refresh.gameObject.SetActive(false);
            }

            PolishReadyButton(join, "MultiJoinButton", Vector2.zero);
            PolishSmallBackButton(join, "MultiJoinBackButton", Vector2.zero);
            PlaceButtonBottom(join, "MultiJoinButton", new Vector2(350f, 105f), new Vector2(320f, 62f));
            PlaceButtonBottom(join, "MultiJoinBackButton", new Vector2(350f, 32f), new Vector2(320f, 62f));
            SetButtonPalette(join, "MultiJoinButton", RoomColor, RoomHoverColor);
            SetButtonPalette(join, "MultiJoinBackButton", BackColor, Color.Lerp(BackColor, Color.white, 0.18f));
        }

        private void PolishLobbyScreen()
        {
            Transform lobby = transform.Find("MultiLobbyScreen/MultiLobbyScreenNote");
            if (lobby == null)
            {
                return;
            }

            LayoutBottomSheet(lobby, 280f);
            PlaceScreenHeading(
                lobby,
                "MultiLobbyScreenTitle",
                "multi_room_title",
                new Vector2(0f, 230f),
                new Vector2(500f, 42f));
            EnsureHeadingPaintStroke(lobby, "MultiLobbyHeadingStroke", new Vector2(0f, 226f), new Vector2(520f, 48f), RandomColor);
            MakeScreenOverlayTransparent(lobby);
            Transform statusTransform = FindDeep(lobby, "MultiLobbyStatus");
            Text status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            if (status != null)
            {
                status.gameObject.SetActive(false);
            }

            Font infoFont = status != null && status.font != null
                ? status.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SetDeepActive(lobby, "MultiLobbySummaryCard", false);
            SetDeepActive(lobby, "MultiLobbyPlayersCard", false);
            EnsureLobbyInfoCard(
                lobby,
                "MultiLobbyRoomIdCard",
                "MultiLobbyRoomIdText",
                infoFont,
                new Vector2(-355f, 184f),
                new Vector2(340f, 42f),
                Color.Lerp(YellowColor, Color.white, 0.58f),
                16);
            EnsureLobbyRoster(lobby, infoFont);

            Font noticeFont = status != null && status.font != null ? status.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text notice = EnsureCreateRoomText(lobby, "MultiLobbyNotice", string.Empty, noticeFont, new Vector2(320f, 218f), 14, new Color(0.18f, 0.13f, 0.08f, 1f), false);
            notice.rectTransform.sizeDelta = new Vector2(400f, 22f);
            if (string.IsNullOrEmpty(notice.text))
            {
                notice.gameObject.SetActive(false);
            }

            Transform drawButton = FindDeep(lobby, "MultiLobbyDrawButton");
            if (drawButton != null)
            {
                drawButton.gameObject.SetActive(false);
            }

            PolishReadyButton(lobby, "MultiLobbyReadyButton", Vector2.zero);
            PolishSmallBackButton(lobby, "MultiLobbyStageButton", Vector2.zero);
            PolishSmallBackButton(lobby, "MultiLobbyCopyIdButton", Vector2.zero);
            PolishSmallBackButton(lobby, "MultiLobbyExitButton", Vector2.zero);
            PlaceButtonBottom(lobby, "MultiLobbyCopyIdButton", new Vector2(-85f, 184f), new Vector2(150f, 42f));
            PlaceButtonBottom(lobby, "MultiLobbyReadyButton", new Vector2(320f, 150f), new Vector2(360f, 58f));
            PlaceButtonBottom(lobby, "MultiLobbyStageButton", new Vector2(320f, 82f), new Vector2(360f, 58f));
            PlaceButtonBottom(lobby, "MultiLobbyExitButton", new Vector2(320f, 14f), new Vector2(360f, 58f));
            SetButtonPalette(lobby, "MultiLobbyReadyButton", RoomColor, RoomHoverColor);
            SetButtonPalette(lobby, "MultiLobbyStageButton", RandomColor, RandomHoverColor);
            SetButtonPalette(lobby, "MultiLobbyCopyIdButton", YellowColor, Color.Lerp(YellowColor, Color.white, 0.2f));
            SetButtonPalette(lobby, "MultiLobbyExitButton", BackColor, Color.Lerp(BackColor, Color.white, 0.18f));
            SetButtonText(lobby, "MultiLobbyStageButton", LocalizationManager.T("multi_stage_select"));
        }

        private static void SetDeepActive(Transform parent, string name, bool active)
        {
            Transform found = FindDeep(parent, name);
            if (found != null)
            {
                found.gameObject.SetActive(active);
            }
        }

        private static void EnsureLobbyRoster(Transform parent, Font font)
        {
            Transform existing = FindDeep(parent, "MultiLobbyRoster");
            GameObject roster = existing != null ? existing.gameObject : null;
            if (roster == null)
            {
                roster = new GameObject("MultiLobbyRoster", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                roster.transform.SetParent(parent, false);
            }

            roster.SetActive(true);
            RectTransform rosterRect = roster.GetComponent<RectTransform>();
            rosterRect.anchorMin = new Vector2(0.5f, 0f);
            rosterRect.anchorMax = new Vector2(0.5f, 0f);
            rosterRect.pivot = new Vector2(0.5f, 0f);
            rosterRect.anchoredPosition = new Vector2(-300f, 24f);
            rosterRect.sizeDelta = new Vector2(500f, 138f);
            rosterRect.localScale = Vector3.one;

            Image rosterImage = roster.GetComponent<Image>();
            rosterImage.color = new Color(1f, 0.985f, 0.925f, 0.98f);
            rosterImage.raycastTarget = false;
            Outline outline = roster.GetComponent<Outline>();
            if (outline == null)
            {
                outline = roster.AddComponent<Outline>();
            }
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2.4f, -2.4f);

            EnsureRosterLine(
                roster.transform,
                "MultiLobbyRosterHeader",
                font,
                new Vector2(0f, -6f),
                new Vector2(-14f, 26f),
                Color.Lerp(YellowColor, Color.white, 0.5f),
                16,
                true);

            Color[] rowColors =
            {
                Color.Lerp(BackColor, Color.white, 0.68f),
                Color.Lerp(RandomColor, Color.white, 0.68f),
                Color.Lerp(YellowColor, Color.white, 0.62f),
                Color.Lerp(VioletColor, Color.white, 0.68f)
            };
            for (int i = 0; i < 4; i++)
            {
                EnsureRosterLine(
                    roster.transform,
                    "MultiLobbyPlayerRow" + i,
                    font,
                    new Vector2(0f, -36f - i * 23f),
                    new Vector2(-14f, 20f),
                    rowColors[i],
                    14,
                    false);
            }
        }

        private static Text EnsureRosterLine(
            Transform parent,
            string name,
            Font font,
            Vector2 topPosition,
            Vector2 size,
            Color background,
            int fontSize,
            bool bold)
        {
            Transform existing = parent.Find(name);
            GameObject line = existing != null ? existing.gameObject : null;
            if (line == null)
            {
                line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                line.transform.SetParent(parent, false);
            }

            RectTransform lineRect = line.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 1f);
            lineRect.anchorMax = new Vector2(1f, 1f);
            lineRect.pivot = new Vector2(0.5f, 1f);
            lineRect.anchoredPosition = topPosition;
            lineRect.sizeDelta = size;
            Image image = line.GetComponent<Image>();
            image.color = background;
            image.raycastTarget = false;

            Transform textTransform = line.transform.Find("Text");
            Text text = textTransform != null ? textTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(line.transform, false);
                text = textObject.GetComponent<Text>();
            }

            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.06f, 0.05f, 0.04f, 1f);
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 1f);
            textRect.offsetMax = new Vector2(-8f, -1f);
            if (!bold)
            {
                text.gameObject.SetActive(false);
                EnsureRosterTextCell(line.transform, "Slot", font, 8f, 42f, 13, FontStyle.Bold, TextAnchor.MiddleCenter);
                EnsureRosterTextCell(line.transform, "Name", font, 54f, 212f, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
                EnsureRosterBadgeCell(line.transform, "You", font, 272f, 48f, Color.Lerp(YellowColor, Color.white, 0.18f));
                EnsureRosterBadgeCell(line.transform, "Host", font, 326f, 60f, Color.Lerp(VioletColor, Color.white, 0.24f));
                EnsureRosterBadgeCell(line.transform, "Status", font, 392f, 84f, Color.Lerp(BackColor, Color.white, 0.5f));
            }
            else
            {
                text.gameObject.SetActive(true);
            }
            return text;
        }

        private static Text EnsureRosterTextCell(
            Transform parent,
            string name,
            Font font,
            float x,
            float width,
            int fontSize,
            FontStyle style,
            TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject cell = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                cell.transform.SetParent(parent, false);
                text = cell.GetComponent<Text>();
            }

            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.045f, 0.04f, 0.035f, 1f);
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            return text;
        }

        private static Text EnsureRosterBadgeCell(
            Transform parent,
            string name,
            Font font,
            float x,
            float width,
            Color color)
        {
            Transform existing = parent.Find(name);
            GameObject badge = existing != null ? existing.gameObject : null;
            if (badge == null)
            {
                badge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badge.transform.SetParent(parent, false);
            }

            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 18f);
            Image image = badge.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            Transform textTransform = badge.transform.Find("Text");
            Text text = textTransform != null ? textTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(badge.transform, false);
                text = textObject.GetComponent<Text>();
            }
            text.font = font;
            text.fontSize = 12;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.045f, 0.04f, 0.035f, 1f);
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2f, 0f);
            textRect.offsetMax = new Vector2(-2f, 0f);
            return text;
        }

        private static Text EnsureLobbyInfoCard(
            Transform parent,
            string cardName,
            string textName,
            Font font,
            Vector2 position,
            Vector2 size,
            Color color,
            int fontSize)
        {
            Transform existing = FindDeep(parent, cardName);
            GameObject card = existing != null ? existing.gameObject : null;
            if (card == null)
            {
                card = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                card.transform.SetParent(parent, false);
            }

            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0f);
            cardRect.anchorMax = new Vector2(0.5f, 0f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.anchoredPosition = position;
            cardRect.sizeDelta = size;
            cardRect.localScale = Vector3.one;

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = color;
            cardImage.raycastTarget = false;
            Outline outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.AddComponent<Outline>();
            }
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Transform textTransform = card.transform.Find(textName);
            Text text = textTransform != null ? textTransform.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject(textName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(card.transform, false);
                text = textObject.GetComponent<Text>();
            }

            text.font = font;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = fontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.06f, 0.05f, 0.04f, 1f);
            text.lineSpacing = 0.92f;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            return text;
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
                    LocalizedText localized = label.GetComponent<LocalizedText>();
                    if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
                    localized.SetKey("ui_back_esc");
                }

                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 20;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
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
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 20;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
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

        public void CaptureCurrentLayout()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }
            if (rectTransform == null)
            {
                return;
            }

            basePosition = rectTransform.anchoredPosition;
            baseScale = Vector3.one;
            rectTransform.localScale = Vector3.one;
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
