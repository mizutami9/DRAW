using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static GameObject CreateTitlePanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = new GameObject("TitlePanel");
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            Stretch(rect);

            Sprite logoSprite = LoadTitleLogoSprite();
            if (logoSprite != null)
            {
                GameObject logoObject = new GameObject("TitleNicoDrowLogo");
                logoObject.transform.SetParent(panel.transform, false);
                RectTransform logoRect = logoObject.AddComponent<RectTransform>();
                logoRect.anchorMin = new Vector2(0.5f, 1f);
                logoRect.anchorMax = new Vector2(0.5f, 1f);
                logoRect.pivot = new Vector2(0.5f, 1f);
                logoRect.anchoredPosition = new Vector2(0f, -14f);
                logoRect.sizeDelta = new Vector2(1120f, 365f);
                logoRect.localRotation = Quaternion.identity;

                Image logoImage = logoObject.AddComponent<Image>();
                logoImage.sprite = logoSprite;
                logoImage.color = Color.white;
                logoImage.preserveAspect = true;
                logoImage.raycastTarget = false;
                logoObject.AddComponent<TitleLogoReveal>();
            }

            GameObject bar = CreatePanel("TitleMenuBar", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddPaperTexture(bar, new Color(0.97f, 0.94f, 0.84f, 1f), new Color(0.62f, 0.5f, 0.34f, 1f), 0.08f, 9301);
            AddSketchFrame(bar.transform, new Vector2(1280f, 88f), new Color(0.23f, 0.17f, 0.12f, 0.35f), 1.8f);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 0f);
            barRect.sizeDelta = new Vector2(0f, 88f);

            AddTitleMenuButton("TitleSingleButton", bar.transform, font, "title_single", new Vector2(-336f, 16f), stageManager, TitleButtonCommand.Command.Single, new Color(1f, 0.94f, 0.62f, 0.96f), -2.5f);
            AddTitleMenuButton("TitleMultiButton", bar.transform, font, "title_multi", new Vector2(-168f, 18f), stageManager, TitleButtonCommand.Command.Multi, new Color(0.78f, 0.92f, 1f, 0.96f), 1.5f);
            AddTitleMenuButton("TitleDrawButton", bar.transform, font, "title_draw", new Vector2(0f, 15f), stageManager, TitleButtonCommand.Command.Draw, new Color(0.82f, 0.96f, 0.72f, 0.96f), -1.2f);
            AddTitleMenuButton("TitleOptionButton", bar.transform, font, "title_option", new Vector2(168f, 18f), stageManager, TitleButtonCommand.Command.Option, new Color(0.98f, 0.84f, 0.72f, 0.96f), 2f);
            AddTitleMenuButton("TitleExitButton", bar.transform, font, "title_exit", new Vector2(336f, 16f), stageManager, TitleButtonCommand.Command.Exit, new Color(0.94f, 0.9f, 0.98f, 0.96f), -1.8f);

            return panel;
        }

        private static GameObject CreateTitleMultiPanel(Transform parent, Font font, StageManager stageManager, OnlineManager onlineManager)
        {
            GameObject panel = CreatePanel("TitleMultiPanel", parent, new Color(0.965f, 0.945f, 0.88f, 0.08f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            Stretch(rect);
            panel.GetComponent<Image>().raycastTarget = false;

            GameObject choice = CreateMultiScreen("MultiChoiceScreen", panel.transform, font, "multi_play");
            AddMultiLargeButton("MultiRandomButton", choice.transform, font, "multi_random_button", new Vector2(0f, 304f), MultiMenuButtonCommand.Command.Random, new Color(0.72f, 0.88f, 1f, 0.97f));
            AddMultiLargeButton("MultiRoomButton", choice.transform, font, "multi_room_button", new Vector2(0f, 184f), MultiMenuButtonCommand.Command.Room, new Color(0.78f, 0.95f, 0.76f, 0.97f));
            AddMultiSmallButton("MultiBackTitleButton", choice.transform, font, "option_back", new Vector2(0f, 72f), MultiMenuButtonCommand.Command.BackToTitle, new Color(0.88f, 0.84f, 0.74f, 0.94f));

            GameObject random = CreateMultiScreen("MultiRandomScreen", panel.transform, font, string.Empty);
            Text randomStatus = CreateMultiBodyText("MultiRandomStatus", random.transform, font, "multi_random_status_default");
            AddMultiSmallButton("MultiRandomReadyButton", random.transform, font, "multi_ready", new Vector2(-92f, 96f), MultiMenuButtonCommand.Command.Ready, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiRandomCancelButton", random.transform, font, "cancel", new Vector2(92f, 96f), MultiMenuButtonCommand.Command.Choice, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            GameObject room = CreateMultiScreen("MultiRoomScreen", panel.transform, font, "multi_room_title");
            AddMultiLargeButton("MultiCreateRoomNavButton", room.transform, font, "multi_create_room", new Vector2(0f, 300f), MultiMenuButtonCommand.Command.CreateRoom, new Color(0.78f, 0.95f, 0.76f, 0.97f));
            AddMultiLargeButton("MultiJoinRoomNavButton", room.transform, font, "multi_join_room", new Vector2(0f, 188f), MultiMenuButtonCommand.Command.JoinRoom, new Color(0.72f, 0.88f, 1f, 0.97f));
            AddMultiSmallButton("MultiRoomBackButton", room.transform, font, "option_back", new Vector2(0f, 76f), MultiMenuButtonCommand.Command.Choice, new Color(0.88f, 0.84f, 0.74f, 0.94f));

            GameObject create = CreateMultiScreen("MultiCreateRoomScreen", panel.transform, font, "multi_create_room");
            Text createRoomBody = CreateMultiBodyText("MultiCreateRoomBody", create.transform, font, "multi_create_room_body");
            createRoomBody.gameObject.SetActive(false);
            AddMultiSmallButton("MultiCreatePlayersMinusButton", create.transform, font, "multi_prev", new Vector2(-74f, 286f), MultiMenuButtonCommand.Command.DecreaseMaxPlayers, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiCreatePlayersPlusButton", create.transform, font, "multi_next", new Vector2(74f, 286f), MultiMenuButtonCommand.Command.IncreaseMaxPlayers, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiCreateVisibilityButton", create.transform, font, "multi_toggle_visibility", new Vector2(0f, 132f), MultiMenuButtonCommand.Command.TogglePrivateRoom, new Color(0.78f, 0.9f, 1f, 0.92f));
            AddMultiSmallButton("MultiCreateButton", create.transform, font, "multi_create", new Vector2(-86f, 42f), MultiMenuButtonCommand.Command.CreateRoomAction, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiCreateBackButton", create.transform, font, "option_back", new Vector2(86f, 42f), MultiMenuButtonCommand.Command.Room, new Color(0.88f, 0.84f, 0.74f, 0.94f));

            GameObject join = CreateMultiScreen("MultiJoinRoomScreen", panel.transform, font, "multi_join_room");
            Text joinHelp = CreateMultiBodyText("MultiJoinRoomBody", join.transform, font, "multi_join_room_help");
            joinHelp.gameObject.SetActive(false);
            InputField joinAddressInput = CreateMultiInputField("MultiJoinAddressInput", join.transform, font, "multi_lobby_id_placeholder", new Vector2(0f, 230f), new Vector2(360f, 48f));
            AddMultiSmallButton("MultiJoinButton", join.transform, font, "multi_join", new Vector2(-86f, 82f), MultiMenuButtonCommand.Command.JoinRoomAction, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            AddMultiSmallButton("MultiJoinBackButton", join.transform, font, "option_back", new Vector2(86f, 82f), MultiMenuButtonCommand.Command.Room, new Color(0.88f, 0.84f, 0.74f, 0.94f));

            GameObject lobby = CreateMultiScreen("MultiLobbyScreen", panel.transform, font, "multi_room_lobby");
            Text lobbyStatus = CreateMultiBodyText("MultiLobbyStatus", lobby.transform, font, "multi_lobby_status_default");
            lobbyStatus.rectTransform.anchoredPosition = new Vector2(0f, 42f);
            lobbyStatus.rectTransform.sizeDelta = new Vector2(-96f, -270f);
            Text lobbyNotice = CreateText("MultiLobbyNotice", lobby.transform, font, 18, TextAnchor.MiddleCenter);
            lobbyNotice.text = string.Empty;
            lobbyNotice.color = new Color(0.18f, 0.13f, 0.08f, 1f);
            lobbyNotice.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            lobbyNotice.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            lobbyNotice.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lobbyNotice.rectTransform.anchoredPosition = new Vector2(0f, 178f);
            lobbyNotice.rectTransform.sizeDelta = new Vector2(420f, 28f);
            lobbyNotice.gameObject.SetActive(false);
            AddMultiSmallButton("MultiLobbyReadyButton", lobby.transform, font, "multi_ready", new Vector2(-86f, 98f), MultiMenuButtonCommand.Command.Ready, new Color(0.75f, 0.95f, 0.75f, 0.92f));
            Button startStageButton = AddMultiSmallButton("MultiLobbyStageButton", lobby.transform, font, "multi_stage_select", new Vector2(86f, 98f), MultiMenuButtonCommand.Command.StartStage, new Color(0.78f, 0.9f, 1f, 0.92f));
            Button copyLobbyIdButton = AddMultiSmallButton("MultiLobbyCopyIdButton", lobby.transform, font, "multi_copy_id", new Vector2(-86f, 36f), MultiMenuButtonCommand.Command.CopyLobbyId, new Color(0.98f, 0.96f, 0.9f, 0.92f));
            AddMultiSmallButton("MultiLobbyExitButton", lobby.transform, font, "multi_leave", new Vector2(86f, 36f), MultiMenuButtonCommand.Command.LeaveLobby, new Color(0.98f, 0.78f, 0.72f, 0.92f));

            MultiMenuController controller = panel.AddComponent<MultiMenuController>();
            panel.AddComponent<MultiMenuVisualPolisher>();
            AssignObject(controller, "onlineManager", onlineManager);
            AssignObject(controller, "stageManager", stageManager);
            AssignObject(controller, "choiceScreen", choice);
            AssignObject(controller, "randomScreen", random);
            AssignObject(controller, "roomScreen", room);
            AssignObject(controller, "createRoomScreen", create);
            AssignObject(controller, "joinRoomScreen", join);
            AssignObject(controller, "lobbyScreen", lobby);
            AssignObject(controller, "randomStatusText", randomStatus);
            AssignObject(controller, "lobbyStatusText", lobbyStatus);
            AssignObject(controller, "lobbyNoticeText", lobbyNotice);
            AssignObject(controller, "createRoomStatusText", createRoomBody);
            AssignObject(controller, "joinAddressInput", joinAddressInput);
            AssignObject(controller, "startStageButton", startStageButton);
            AssignObject(controller, "copyLobbyIdButton", copyLobbyIdButton);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject CreateTitleOptionPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("TitleOptionPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.94f));
            AddPaperTexture(panel, new Color(0.965f, 0.94f, 0.84f, 1f), new Color(0.62f, 0.5f, 0.34f, 1f), 0.09f, 9182);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            rect.sizeDelta = new Vector2(720f, 420f);
            AddSketchFrame(panel.transform, new Vector2(720f, 420f), new Color(0.2f, 0.14f, 0.1f, 0.72f), 2f);
            AddMaskingTape(panel.transform, new Vector2(-248f, 192f), -5f);
            AddMaskingTape(panel.transform, new Vector2(248f, 192f), 4f);

            Text title = CreateText("TitleOptionTitle", panel.transform, font, 36, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("title_option");
            AddLocalizedText(title.gameObject, "title_option");
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            title.rectTransform.sizeDelta = new Vector2(0f, 42f);

            Text subtitle = CreateText("TitleOptionSubtitle", panel.transform, font, 18, TextAnchor.UpperCenter);
            subtitle.text = LocalizationManager.T("option_subtitle");
            subtitle.color = new Color(0.22f, 0.18f, 0.12f, 0.78f);
            AddLocalizedText(subtitle.gameObject, "option_subtitle");
            subtitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            subtitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            subtitle.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            subtitle.rectTransform.sizeDelta = new Vector2(0f, 24f);

            CreateOptionLabel("OptionBgmLabel", panel.transform, font, "\u266a " + LocalizationManager.T("option_bgm"), "option_bgm", new Vector2(-190f, -140f));
            Slider bgmSlider = CreateOptionSlider("OptionBgmSlider", panel.transform, new Vector2(35f, -140f), new Vector2(250f, 36f));
            Text bgmValue = CreateOptionValueText("OptionBgmValue", panel.transform, font, new Vector2(254f, -140f));

            CreateOptionLabel("OptionSeLabel", panel.transform, font, "\u25b7 " + LocalizationManager.T("option_se"), "option_se", new Vector2(-190f, -200f));
            Slider seSlider = CreateOptionSlider("OptionSeSlider", panel.transform, new Vector2(35f, -200f), new Vector2(250f, 36f));
            Text seValue = CreateOptionValueText("OptionSeValue", panel.transform, font, new Vector2(254f, -200f));

            Text keysLabel = CreateOptionLabel("OptionKeysLabel", panel.transform, font, "\u2328 " + LocalizationManager.T("option_keys"), "option_keys", new Vector2(-178f, -280f));
            Text keysValue = CreateText("OptionKeysValue", panel.transform, font, 21, TextAnchor.MiddleLeft);
            keysValue.text = LocalizationManager.T("option_not_implemented");
            AddLocalizedText(keysValue.gameObject, "option_not_implemented");
            keysValue.color = new Color(0.24f, 0.2f, 0.14f, 0.78f);
            keysValue.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            keysValue.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            keysValue.rectTransform.pivot = new Vector2(0f, 0.5f);
            keysValue.rectTransform.anchoredPosition = new Vector2(-22f, -280f);
            keysValue.rectTransform.sizeDelta = new Vector2(230f, 34f);
            keysLabel.gameObject.SetActive(false);
            keysValue.gameObject.SetActive(false);

            CreateOptionLabel("OptionLanguageLabel", panel.transform, font, "\u25ce " + LocalizationManager.T("option_language"), "option_language", new Vector2(-190f, -260f));
            Button japanese = CreateButton("OptionJapaneseButton", panel.transform, font, LocalizationManager.T("lang_ja"), new Vector2(-12f, 139f), new Vector2(130f, 42f), new Color(0.78f, 0.9f, 1f, 0.94f), "lang_ja");
            Button english = CreateButton("OptionEnglishButton", panel.transform, font, LocalizationManager.T("lang_en"), new Vector2(138f, 139f), new Vector2(130f, 42f), new Color(0.98f, 0.96f, 0.9f, 0.94f), "lang_en");
            SetButtonLabelColor(japanese, Color.black);
            SetButtonLabelColor(english, Color.black);
            SetButtonLabelFontSize(japanese, 17);
            SetButtonLabelFontSize(english, 17);
            Text languageValue = CreateText("OptionLanguageValue", panel.transform, font, 17, TextAnchor.MiddleLeft);
            languageValue.text = LocalizationManager.T("lang_ja");
            languageValue.color = new Color(0.24f, 0.2f, 0.14f, 0.78f);
            languageValue.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            languageValue.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            languageValue.rectTransform.pivot = new Vector2(0f, 0.5f);
            languageValue.rectTransform.anchoredPosition = new Vector2(244f, -334f);
            languageValue.rectTransform.sizeDelta = new Vector2(96f, 30f);
            languageValue.gameObject.SetActive(false);

            Button back = CreateButton("TitleOptionBackButton", panel.transform, font, "\u2190 " + LocalizationManager.T("option_back"), new Vector2(0f, 19f), new Vector2(260f, 58f), new Color(0.98f, 0.78f, 0.72f, 0.92f));
            SetButtonLabelColor(back, Color.black);
            SetButtonLabelFontSize(back, 22);
            AddSketchFrame(back.transform, new Vector2(260f, 58f), new Color(0.25f, 0.18f, 0.12f, 0.5f), 1.5f);
            AddTitleCommand(back.gameObject, stageManager, TitleButtonCommand.Command.Back);

            OptionSettingsController controller = panel.AddComponent<OptionSettingsController>();
            AssignObject(controller, "bgmSlider", bgmSlider);
            AssignObject(controller, "seSlider", seSlider);
            AssignObject(controller, "bgmValueText", bgmValue);
            AssignObject(controller, "seValueText", seValue);
            AssignObject(controller, "languageValueText", languageValue);
            AssignObject(controller, "japaneseButton", japanese);
            AssignObject(controller, "englishButton", english);

            panel.SetActive(false);
            return panel;
        }

        private static void AddTitleMenuButton(string name, Transform parent, Font font, string localizationKey, Vector2 position, StageManager stageManager, TitleButtonCommand.Command command, Color color, float rotation)
        {
            Vector2 size = new Vector2(132f, 56f);
            Button button = CreateButton(name, parent, font, LocalizationManager.T(localizationKey), position, size, color, localizationKey);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.localRotation = Quaternion.identity;
            AddSketchFrame(button.transform, size, new Color(0.25f, 0.18f, 0.12f, 0.5f), 1.5f);
            SetButtonLabelFontSize(button, 21);
            SetButtonLabelColor(button, Color.black);
            StageCardHover hover = button.gameObject.AddComponent<StageCardHover>();
            AssignObject(hover, "targetImage", button.GetComponent<Image>());
            AssignColor(hover, "normalColor", color);
            AssignColor(hover, "hoverColor", new Color(1f, 0.97f, 0.72f, 1f));
            AddTitleCommand(button.gameObject, stageManager, command);
        }

        private static Text CreateOptionLabel(string name, Transform parent, Font font, string label, string localizationKey, Vector2 anchoredPosition)
        {
            Text text = CreateText(name, parent, font, 21, TextAnchor.MiddleLeft);
            text.text = label;
            text.color = Color.black;
            text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.anchoredPosition = anchoredPosition;
            text.rectTransform.sizeDelta = new Vector2(180f, 34f);
            int separator = label.LastIndexOf(LocalizationManager.T(localizationKey), System.StringComparison.Ordinal);
            PrefixedLocalizedText localized = text.gameObject.AddComponent<PrefixedLocalizedText>();
            AssignString(localized, "key", localizationKey);
            AssignString(localized, "prefix", separator > 0 ? label.Substring(0, separator) : string.Empty);
            return text;
        }

        private static Text CreateOptionValueText(string name, Transform parent, Font font, Vector2 anchoredPosition)
        {
            Text text = CreateText(name, parent, font, 19, TextAnchor.MiddleRight);
            text.text = "80%";
            text.color = Color.black;
            text.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(1f, 0.5f);
            text.rectTransform.anchoredPosition = anchoredPosition;
            text.rectTransform.sizeDelta = new Vector2(78f, 32f);
            return text;
        }

        private static Slider CreateOptionSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            GameObject background = CreatePanel("Background", sliderObject.transform, new Color(0.92f, 0.87f, 0.74f, 0.95f));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, 10f);
            AddSketchFrame(background.transform, new Vector2(size.x, 10f), new Color(0.25f, 0.18f, 0.12f, 0.45f), 1.1f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);

            GameObject fill = CreatePanel("Fill", fillArea.transform, new Color(0.42f, 0.84f, 0.36f, 0.95f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(10f, 10f);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = CreatePanel("Handle", handleArea.transform, new Color(0.98f, 0.72f, 0.28f, 0.98f));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(24f, 24f);
            CreateIconDot(handle.transform, Vector2.zero, 20f, new Color(0.98f, 0.72f, 0.28f, 1f));
            CreateIconDot(handle.transform, new Vector2(-3f, 3f), 6f, new Color(1f, 0.95f, 0.72f, 0.72f));

            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            return slider;
        }

        private static GameObject CreateMultiScreen(string name, Transform parent, Font font, string titleKey)
        {
            GameObject screen = new GameObject(name);
            screen.transform.SetParent(parent, false);
            RectTransform screenRect = screen.AddComponent<RectTransform>();
            Stretch(screenRect);

            GameObject note = CreatePanel(name + "Note", screen.transform, new Color(0.96f, 0.93f, 0.86f, 0.96f));
            AddUiOutline(note, new Color(0.12f, 0.11f, 0.1f, 0.8f), new Vector2(2f, -2f));
            RectTransform noteRect = note.GetComponent<RectTransform>();
            noteRect.anchorMin = new Vector2(0.5f, 0.5f);
            noteRect.anchorMax = new Vector2(0.5f, 0.5f);
            noteRect.pivot = new Vector2(0.5f, 0.5f);
            noteRect.anchoredPosition = new Vector2(0f, 20f);
            noteRect.sizeDelta = new Vector2(700f, 520f);

            Text title = CreateText(name + "Title", note.transform, font, 34, TextAnchor.UpperCenter);
            title.text = string.IsNullOrEmpty(titleKey) ? string.Empty : LocalizationManager.T(titleKey);
            if (!string.IsNullOrEmpty(titleKey))
            {
                AddLocalizedText(title.gameObject, titleKey);
            }
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            title.rectTransform.sizeDelta = new Vector2(0f, 48f);
            title.gameObject.SetActive(!string.IsNullOrEmpty(titleKey));

            return note;
        }

        private static Text CreateMultiBodyText(string name, Transform parent, Font font, string bodyKey)
        {
            Text body = CreateText(name, parent, font, 21, TextAnchor.MiddleCenter);
            body.text = LocalizationManager.T(bodyKey);
            AddLocalizedText(body.gameObject, bodyKey);
            body.color = Color.black;
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            body.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            body.rectTransform.sizeDelta = new Vector2(-92f, -150f);
            return body;
        }

        private static void AddMultiLargeButton(string name, Transform parent, Font font, string localizationKey, Vector2 position, MultiMenuButtonCommand.Command command, Color color)
        {
            Button button = CreateButton(name, parent, font, LocalizationManager.T(localizationKey), position, new Vector2(410f, 100f), color, localizationKey);
            SetButtonLabelColor(button, Color.black);
            SetButtonLabelFontSize(button, 24);
            AddSketchFrame(button.transform, new Vector2(410f, 100f), new Color(0.14f, 0.1f, 0.07f, 0.62f), 3f);
            AddMultiCommand(button.gameObject, command);
        }

        private static Button AddMultiSmallButton(string name, Transform parent, Font font, string localizationKey, Vector2 position, MultiMenuButtonCommand.Command command, Color color)
        {
            Button button = CreateButton(name, parent, font, LocalizationManager.T(localizationKey), position, new Vector2(132f, 46f), color, localizationKey);
            SetButtonLabelColor(button, Color.black);
            SetButtonLabelFontSize(button, name.Contains("Back") ? 20 : 18);
            AddMultiCommand(button.gameObject, command);
            return button;
        }

        private static InputField CreateMultiInputField(string name, Transform parent, Font font, string placeholderKey, Vector2 position, Vector2 size)
        {
            GameObject fieldObject = CreatePanel(name, parent, new Color(0.98f, 0.96f, 0.9f, 0.96f));
            AddUiOutline(fieldObject, new Color(0.12f, 0.11f, 0.1f, 0.65f), new Vector2(1.5f, -1.5f));
            RectTransform rect = fieldObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            InputField input = fieldObject.AddComponent<InputField>();
            input.lineType = InputField.LineType.SingleLine;

            Text text = CreateText(name + "Text", fieldObject.transform, font, 20, TextAnchor.MiddleLeft);
            text.color = Color.black;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 2f);
            text.rectTransform.offsetMax = new Vector2(-12f, -2f);

            Text placeholder = CreateText(name + "Placeholder", fieldObject.transform, font, 18, TextAnchor.MiddleLeft);
            placeholder.text = LocalizationManager.T(placeholderKey);
            AddLocalizedText(placeholder.gameObject, placeholderKey);
            placeholder.color = new Color(0.25f, 0.25f, 0.25f, 0.55f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(12f, 2f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, -2f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }
    }
}
