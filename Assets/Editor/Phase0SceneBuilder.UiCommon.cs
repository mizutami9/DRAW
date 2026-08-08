using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static UIManager CreateUi(Transform parent, Font font, StageManager stageManager, OnlineManager onlineManager, out DrawManager drawManager, out RuntimeStageEditor runtimeStageEditor)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(parent);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(parent);
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            GameObject drawPanel = CreateDrawPanel(
                canvasObject.transform,
                font,
                stageManager,
                out RectTransform drawArea,
                out RectTransform lineRoot,
                out RectTransform previewRoot,
                out Text inkText,
                out Image inkGaugeFill,
                out Text partText,
                out Text messageText,
                out Text abilityText,
                out Button clearButton,
                out Button decideButton,
                out Button penButton,
                out Button eraserButton,
                out Button[] partButtons);

            GameObject clearPanel = CreatePanel("ClearPanel", canvasObject.transform, new Color(0.05f, 0.2f, 0.08f, 0.68f));
            Text clearText = CreateText("ClearText", clearPanel.transform, font, 42, TextAnchor.MiddleCenter);
            clearText.text = LocalizationManager.T("status_clear");
            clearText.color = Color.white;
            AddLocalizedText(clearText.gameObject, "status_clear");
            Stretch(clearText.rectTransform);

            drawPanel.SetActive(false);
            clearPanel.SetActive(false);

            UIManager ui = canvasObject.AddComponent<UIManager>();
            AssignObject(ui, "drawingHintPanel", drawPanel);
            AssignObject(ui, "clearPanel", clearPanel);

            drawManager = canvasObject.AddComponent<DrawManager>();
            DrawFeedbackController drawFeedback = drawPanel.AddComponent<DrawFeedbackController>();
            AssignObject(drawFeedback, "drawArea", drawArea);
            AssignObject(drawFeedback, "dustRoot", lineRoot);
            AssignObject(drawManager, "drawPanel", drawPanel);
            AssignObject(drawManager, "drawArea", drawArea);
            AssignObject(drawManager, "lineRoot", lineRoot);
            AssignObject(drawManager, "previewRoot", previewRoot);
            AssignObject(drawManager, "inkText", inkText);
            AssignObject(drawManager, "inkGaugeFill", inkGaugeFill);
            AssignObject(drawManager, "partText", partText);
            AssignObject(drawManager, "messageText", messageText);
            AssignObject(drawManager, "abilityText", abilityText);
            AssignObject(drawManager, "feedback", drawFeedback);
            AssignObject(drawManager, "penToolButton", penButton);
            AssignObject(drawManager, "eraserToolButton", eraserButton);
            AssignFloat(drawManager, "maxInk", DrawManager.IndividualInkLimit);

            GameObject gameplayHud = CreateGameplayHud(canvasObject.transform, font, drawManager, stageManager);
            AssignObject(ui, "gameplayHudPanel", gameplayHud);
            AssignObject(ui, "gameplayHudDrawer", gameplayHud.GetComponentInChildren<GameplayHudDrawer>(true));
            GameObject titlePanel = CreateTitlePanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "titlePanel", titlePanel);
            GameObject multiPanel = CreateTitleMultiPanel(canvasObject.transform, font, stageManager, onlineManager);
            AssignObject(ui, "multiPanel", multiPanel);
            GameObject optionPanel = CreateTitleOptionPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "optionPanel", optionPanel);
            GameObject menuPanel = CreateMenuPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "menuPanel", menuPanel);
            GameObject stageSelectPanel = CreateStageSelectPanel(canvasObject.transform, font, stageManager);
            AssignObject(ui, "stageSelectPanel", stageSelectPanel);
            GameObject stageEditorPanel = CreateRuntimeStageEditorPanel(canvasObject.transform, font, stageManager, out RectTransform editorUiBlocker, out Text editorStageText, out Text editorSelectedText, out Text editorStatusText, out Dropdown editorCategoryDropdown, out Dropdown editorTypeDropdown, out InputField editorSearchInput);
            AssignObject(ui, "stageEditorPanel", stageEditorPanel);
            CreateDrawSpeciesPanel(drawPanel.transform, font, drawManager);

            runtimeStageEditor = canvasObject.AddComponent<RuntimeStageEditor>();
            AssignObject(runtimeStageEditor, "editorPanel", stageEditorPanel);
            AssignObject(runtimeStageEditor, "uiBlocker", editorUiBlocker);
            AssignObject(runtimeStageEditor, "stageManager", stageManager);
            AssignObject(runtimeStageEditor, "stageText", editorStageText);
            AssignObject(runtimeStageEditor, "selectedText", editorSelectedText);
            AssignObject(runtimeStageEditor, "statusText", editorStatusText);
            AssignObject(runtimeStageEditor, "categoryDropdown", editorCategoryDropdown);
            AssignObject(runtimeStageEditor, "objectTypeDropdown", editorTypeDropdown);
            AssignObject(runtimeStageEditor, "searchInput", editorSearchInput);
            RuntimeStageEditorButtonCommand[] editorCommands = stageEditorPanel.GetComponentsInChildren<RuntimeStageEditorButtonCommand>(true);
            for (int i = 0; i < editorCommands.Length; i++)
            {
                AssignObject(editorCommands[i], "editor", runtimeStageEditor);
            }

            AddDrawCommand(clearButton.gameObject, drawManager, DrawButtonCommand.Command.Clear);
            AddDrawCommand(decideButton.gameObject, drawManager, DrawButtonCommand.Command.Confirm);
            AddPartCommands(partButtons, drawManager);

            if (canvasObject.GetComponent<DoodleUiDirector>() == null)
            {
                canvasObject.AddComponent<DoodleUiDirector>();
            }

            return ui;
        }

        private static void CreateDrawSpeciesPanel(Transform parent, Font font, DrawManager drawManager)
        {
            GameObject speciesPanel = CreatePanel("DrawSpeciesPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.86f));
            AddUiOutline(speciesPanel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform rect = speciesPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f, 142f);
            rect.sizeDelta = new Vector2(68f, 300f);

            Text title = CreateText("DrawSpeciesTitle", speciesPanel.transform, font, 14, TextAnchor.UpperCenter);
            title.text = string.Empty;
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -5f);
            title.rectTransform.sizeDelta = new Vector2(0f, 22f);
            title.gameObject.SetActive(false);

            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                Button button = CreateSpeciesIconButton(
                    $"{species[i]}DrawSpeciesButton",
                    speciesPanel.transform,
                    font,
                    species[i],
                    new Vector2(0f, -10f - i * 64f),
                    new Vector2(56f, 56f),
                    new Color(0.98f, 0.96f, 0.9f, 0.82f));
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.5f, 1f);
                buttonRect.anchorMax = new Vector2(0.5f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 1f);

                Text label = CreateText($"{species[i]}DrawSpeciesLabel", speciesPanel.transform, font, 12, TextAnchor.MiddleCenter);
                label.text = string.Empty;
                label.color = Color.black;
                label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(62f, 20f);
                label.gameObject.SetActive(false);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }
        }

        private static GameObject CreateMenuPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("MenuPanel", parent, new Color(0.96f, 0.93f, 0.86f, 0.94f));
            AddUiOutline(panel, new Color(0.12f, 0.11f, 0.1f, 0.75f), new Vector2(2f, -2f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(-342f, 86f);
            rect.sizeDelta = new Vector2(320f, 420f);

            Text title = CreateText("MenuTitle", panel.transform, font, 22, TextAnchor.UpperCenter);
            title.text = LocalizationManager.T("menu");
            AddLocalizedText(title.gameObject, "menu");
            title.color = Color.black;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            title.rectTransform.sizeDelta = new Vector2(0f, 42f);

            Button continueButton = CreateButton("MenuContinueButton", panel.transform, font, LocalizationManager.T("menu_continue"), new Vector2(0f, 298f), new Vector2(250f, 48f), new Color(0.78f, 0.95f, 0.78f, 0.92f), "menu_continue");
            Button retryButton = CreateButton("MenuRetryButton", panel.transform, font, LocalizationManager.T("retry"), new Vector2(0f, 238f), new Vector2(250f, 48f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "retry");
            Button optionButton = CreateButton("MenuOptionButton", panel.transform, font, LocalizationManager.T("title_option"), new Vector2(0f, 178f), new Vector2(250f, 48f), new Color(0.98f, 0.84f, 0.72f, 0.92f), "title_option");
            Button titleButton = CreateButton("MenuTitleButton", panel.transform, font, LocalizationManager.T("menu_stage_select"), new Vector2(0f, 118f), new Vector2(250f, 48f), new Color(0.82f, 0.9f, 1f, 0.92f), "menu_stage_select");

            Button[] buttons = { continueButton, retryButton, optionButton, titleButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                SetButtonLabelColor(buttons[i], Color.black);
                SetButtonLabelFontSize(buttons[i], 22);
                AddSketchFrame(buttons[i].transform, new Vector2(250f, 48f), new Color(0.25f, 0.18f, 0.12f, 0.45f), 1.5f);
            }

            AddGameplayCommand(continueButton.gameObject, stageManager, GameplayButtonCommand.Command.Continue);
            AddGameplayCommand(retryButton.gameObject, stageManager, GameplayButtonCommand.Command.Retry);
            AddGameplayCommand(optionButton.gameObject, stageManager, GameplayButtonCommand.Command.Option);
            AddGameplayCommand(titleButton.gameObject, stageManager, GameplayButtonCommand.Command.StageSelect);

            panel.AddComponent<SlidingMenuPanel>();
            panel.SetActive(false);
            return panel;
        }

        private static Button CreateSpeciesIconButton(string name, Transform parent, Font font, DrawManager.Species species, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            Button button = CreateButton(name, parent, font, string.Empty, anchoredPosition, size, color);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
                label.raycastTarget = false;
            }

            CreateSpeciesSketchIcon(button.transform, species);
            return button;
        }

        private static void CreateLanguageButtons(Transform parent, Font font)
        {
            Button japaneseButton = CreateButton("JapaneseButton", parent, font, LocalizationManager.T("lang_ja"), new Vector2(500f, -12f), new Vector2(96f, 36f), new Color(0.18f, 0.42f, 0.78f), "lang_ja", true);
            Button englishButton = CreateButton("EnglishButton", parent, font, LocalizationManager.T("lang_en"), new Vector2(608f, -12f), new Vector2(70f, 36f), new Color(0.28f, 0.28f, 0.32f), "lang_en", true);

            LanguageButtonCommand japaneseCommand = japaneseButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(japaneseCommand, "language", (int)LocalizationManager.Language.Japanese);

            LanguageButtonCommand englishCommand = englishButton.gameObject.AddComponent<LanguageButtonCommand>();
            AssignEnum(englishCommand, "language", (int)LocalizationManager.Language.English);
        }

        private static void CreateSpeciesButtons(Transform parent, Font font, DrawManager drawManager)
        {
            DrawManager.Species[] species =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Snake,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < species.Length; i++)
            {
                Button button = CreateButton(
                    $"{species[i]}SpeciesButton",
                    parent,
                    font,
                    GetSpeciesLabel(species[i]),
                    new Vector2(-210f + i * 86f, -12f),
                    new Vector2(78f, 32f),
                    new Color(0.2f, 0.32f, 0.42f),
                    null,
                    true);

                SpeciesButtonCommand command = button.gameObject.AddComponent<SpeciesButtonCommand>();
                AssignObject(command, "drawManager", drawManager);
                AssignEnum(command, "species", (int)species[i]);
            }
        }

        private static string GetSpeciesLabel(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat:
                    return LocalizationManager.T("cat");
                case DrawManager.Species.Bird:
                    return LocalizationManager.T("bird");
                case DrawManager.Species.Snake:
                    return LocalizationManager.T("snake");
                case DrawManager.Species.Slime:
                    return LocalizationManager.T("slime");
                default:
                    return LocalizationManager.T("human");
            }
        }

        private static void AddLocalizedText(GameObject gameObject, string localizationKey)
        {
            LocalizedText localizedText = gameObject.AddComponent<LocalizedText>();
            AssignString(localizedText, "key", localizationKey);
        }

        private static string GetPartKey(DrawManager.BodyPart part)
        {
            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    return "head";
                case DrawManager.BodyPart.Torso:
                    return "torso";
                case DrawManager.BodyPart.LeftArm:
                    return "left_arm";
                case DrawManager.BodyPart.RightArm:
                    return "right_arm";
                case DrawManager.BodyPart.LeftLeg:
                    return "left_leg";
                case DrawManager.BodyPart.RightLeg:
                    return "right_leg";
                case DrawManager.BodyPart.LeftFrontLeg:
                    return "left_front_leg";
                case DrawManager.BodyPart.RightFrontLeg:
                    return "right_front_leg";
                case DrawManager.BodyPart.LeftBackLeg:
                    return "left_back_leg";
                case DrawManager.BodyPart.RightBackLeg:
                    return "right_back_leg";
                case DrawManager.BodyPart.Tail:
                    return "tail";
                case DrawManager.BodyPart.LeftWing:
                    return "left_wing";
                case DrawManager.BodyPart.RightWing:
                    return "right_wing";
                case DrawManager.BodyPart.TailFeather:
                    return "tail_feather";
                case DrawManager.BodyPart.SlimeBody:
                    return "slime_body";
                default:
                    return string.Empty;
            }
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent);
            Image image = panel.AddComponent<Image>();
            image.color = color;
            Stretch(image.rectTransform);
            return panel;
        }

        private static void AddUiOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        private static void SetButtonLabelColor(Button button, Color color)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = color;
            }
        }

        private static void SetButtonLabelFontSize(Button button, int size)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = size;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.Max(8, size - 4);
                label.resizeTextMaxSize = size;
            }
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent);
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        private static Button CreateButton(string name, Transform parent, Font font, string label, Vector2 anchoredPosition, Vector2 size, Color color, string localizationKey = null, bool anchorToTop = false)
        {
            GameObject buttonObject = CreatePanel(name, parent, color);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchorMax = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.pivot = anchorToTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Button button = buttonObject.AddComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color * 1.15f;
            colors.pressedColor = color * 0.8f;
            button.colors = colors;

            Text text = CreateText("Label", buttonObject.transform, font, 20, TextAnchor.MiddleCenter);
            text.text = string.IsNullOrEmpty(localizationKey) ? label : LocalizationManager.T(localizationKey);
            text.color = Color.white;
            Stretch(text.rectTransform);
            if (!string.IsNullOrEmpty(localizationKey))
            {
                AddLocalizedText(text.gameObject, localizationKey);
            }
            return button;
        }

    }
}
