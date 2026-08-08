using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static GameObject CreateDrawPanel(
            Transform parent,
            Font font,
            StageManager stageManager,
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
            out Button[] partButtons)
        {
            GameObject panel = CreatePanel("DrawPanel", parent, new Color(0.98f, 0.955f, 0.865f, 0.98f));
            AddPaperTexture(panel, new Color(0.98f, 0.955f, 0.865f, 1f), new Color(0.76f, 0.67f, 0.49f, 1f), 0.1f, 3113);
            AddSketchbookPaper(panel.transform);
            panel.AddComponent<DrawScreenVisualPolisher>();

            Text title = CreateText("DrawTitle", panel.transform, font, 48, TextAnchor.UpperLeft);
            title.text = "DROW";
            title.color = new Color(0.12f, 0.1f, 0.08f);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(28f, -10f);
            title.rectTransform.sizeDelta = new Vector2(230f, 58f);

            Text help = CreateText("DrawHelp", panel.transform, font, 18, TextAnchor.UpperCenter);
            help.text = LocalizationManager.T("draw_help");
            help.color = Color.black;
            AddLocalizedText(help.gameObject, "draw_help");
            help.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            help.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(-60f, -606f);
            help.rectTransform.sizeDelta = new Vector2(420f, 78f);

            GameObject partBar = CreatePanel("PartButtonBar", panel.transform, new Color(0.91f, 0.86f, 0.74f, 0.72f));
            RectTransform partBarRect = partBar.GetComponent<RectTransform>();
            partBarRect.anchorMin = new Vector2(0.5f, 1f);
            partBarRect.anchorMax = new Vector2(0.5f, 1f);
            partBarRect.pivot = new Vector2(0.5f, 1f);
            partBarRect.anchoredPosition = new Vector2(96f, -58f);
            partBarRect.sizeDelta = new Vector2(930f, 78f);
            partButtons = CreatePartButtons(partBar.transform, font);

            GameObject areaObject = CreatePanel("DrawArea", panel.transform, new Color(0.985f, 0.982f, 0.94f, 1f));
            AddPaperTexture(areaObject, new Color(0.985f, 0.982f, 0.94f, 1f), new Color(0.72f, 0.66f, 0.52f, 1f), 0.085f, 4127);
            drawArea = areaObject.GetComponent<RectTransform>();
            drawArea.anchorMin = new Vector2(0.5f, 0.5f);
            drawArea.anchorMax = new Vector2(0.5f, 0.5f);
            drawArea.pivot = new Vector2(0.5f, 0.5f);
            drawArea.anchoredPosition = new Vector2(-225f, 0f);
            drawArea.sizeDelta = new Vector2(300f, 300f);
            areaObject.AddComponent<RectMask2D>();

            GameObject lineRootObject = new GameObject("LineRoot");
            lineRootObject.transform.SetParent(areaObject.transform, false);
            lineRoot = lineRootObject.AddComponent<RectTransform>();
            Stretch(lineRoot);

            GameObject previewObject = CreatePanel("PreviewArea", panel.transform, new Color(0.965f, 0.958f, 0.91f, 0.98f));
            AddPaperTexture(previewObject, new Color(0.965f, 0.958f, 0.91f, 1f), new Color(0.68f, 0.62f, 0.5f, 1f), 0.08f, 5519);
            RectTransform previewArea = previewObject.GetComponent<RectTransform>();
            previewArea.anchorMin = new Vector2(0.5f, 0.5f);
            previewArea.anchorMax = new Vector2(0.5f, 0.5f);
            previewArea.pivot = new Vector2(0.5f, 0.5f);
            previewArea.anchoredPosition = new Vector2(390f, 0f);
            previewArea.sizeDelta = new Vector2(290f, 290f);
            previewObject.AddComponent<RectMask2D>();

            Text previewTitle = CreateText("PreviewTitle", panel.transform, font, 18, TextAnchor.UpperCenter);
            previewTitle.text = LocalizationManager.T("preview");
            previewTitle.color = Color.black;
            AddLocalizedText(previewTitle.gameObject, "preview");
            previewTitle.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            previewTitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            previewTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            previewTitle.rectTransform.anchoredPosition = previewArea.anchoredPosition + new Vector2(0f, 161f);
            previewTitle.rectTransform.sizeDelta = new Vector2(300f, 18f);

            GameObject previewRootObject = new GameObject("PreviewRoot");
            previewRootObject.transform.SetParent(previewObject.transform, false);
            previewRoot = previewRootObject.AddComponent<RectTransform>();
            previewRoot.anchorMin = new Vector2(0.5f, 0.5f);
            previewRoot.anchorMax = new Vector2(0.5f, 0.5f);
            previewRoot.pivot = new Vector2(0.5f, 0.5f);
            previewRoot.anchoredPosition = new Vector2(0f, -8f);
            previewRoot.sizeDelta = new Vector2(256f, 256f);
            previewRoot.localScale = Vector3.one * 0.6f;
            previewRootObject.AddComponent<SketchPreviewWiggle>();

            GameObject toolPanel = CreateDrawToolPanel(
                panel.transform,
                font,
                out inkGaugeFill,
                out penButton,
                out eraserButton,
                out Button undoButton,
                out Slider brushSizeSlider,
                out Text brushSizeValueText);
            RectTransform toolRect = toolPanel.GetComponent<RectTransform>();
            toolRect.anchorMin = new Vector2(0.5f, 0f);
            toolRect.anchorMax = new Vector2(0.5f, 0f);
            toolRect.pivot = new Vector2(0.5f, 0f);
            toolRect.anchoredPosition = new Vector2(-145f, 14f);

            inkText = CreateText("InkText", toolPanel.transform, font, 16, TextAnchor.MiddleCenter);
            inkText.color = Color.black;
            inkText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            inkText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            inkText.rectTransform.pivot = new Vector2(0.5f, 1f);
            inkText.rectTransform.anchoredPosition = new Vector2(0f, -218f);
            inkText.rectTransform.sizeDelta = new Vector2(190f, 52f);

            partText = CreateText("PartText", panel.transform, font, 20, TextAnchor.MiddleLeft);
            partText.color = Color.black;
            partText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            partText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            partText.rectTransform.pivot = new Vector2(0.5f, 0f);
            partText.rectTransform.anchoredPosition = new Vector2(-500f, 642f);
            partText.rectTransform.sizeDelta = new Vector2(220f, 42f);
            partText.gameObject.SetActive(false);

            messageText = CreateText("ConnectionMessageText", panel.transform, font, 18, TextAnchor.MiddleCenter);
            messageText.text = LocalizationManager.T("msg_torso_first");
            messageText.color = Color.black;
            messageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            messageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            messageText.rectTransform.pivot = new Vector2(0.5f, 0f);
            messageText.rectTransform.anchoredPosition = new Vector2(90f, 536f);
            messageText.rectTransform.sizeDelta = new Vector2(780f, 34f);
            messageText.gameObject.SetActive(false);

            abilityText = CreateText("AbilityPreviewText", panel.transform, font, 16, TextAnchor.MiddleCenter);
            abilityText.text = PlayerAbilityController.GetProfileSummary(new PlayerAbilityController.AbilityProfile());
            abilityText.color = Color.black;
            abilityText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            abilityText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            abilityText.rectTransform.pivot = new Vector2(0.5f, 0f);
            abilityText.rectTransform.anchoredPosition = new Vector2(430f, 112f);
            abilityText.rectTransform.sizeDelta = new Vector2(260f, 48f);
            abilityText.gameObject.SetActive(false);

            clearButton = toolPanel.transform.Find("ToolClearButton").GetComponent<Button>();
            decideButton = CreateButton("DecideButton", panel.transform, font, "✓ " + LocalizationManager.T("draw_finish") + "\nENTER", new Vector2(420f, 14f), new Vector2(168f, 104f), new Color(0.76f, 0.95f, 0.7f, 0.92f));
            Button cancelButton = CreateButton("CancelDrawButton", panel.transform, font, "← " + LocalizationManager.T("draw_redo") + "\nESC", new Vector2(570f, 14f), new Vector2(96f, 104f), new Color(0.98f, 0.74f, 0.64f, 0.92f));
            SetButtonLabelColor(clearButton, Color.black);
            SetButtonLabelColor(decideButton, Color.black);
            SetButtonLabelColor(cancelButton, Color.black);
            AddGameplayCommand(cancelButton.gameObject, stageManager, GameplayButtonCommand.Command.CloseDrawing);
            AddDrawCommand(penButton.gameObject, null, DrawButtonCommand.Command.ToolMode, 0);
            AddDrawCommand(eraserButton.gameObject, null, DrawButtonCommand.Command.ToolMode, 1);
            AddDrawCommand(undoButton.gameObject, null, DrawButtonCommand.Command.Undo);
            AddBrushSizeSliderCommand(brushSizeSlider.gameObject, null, brushSizeValueText);

            for (int i = 0; i < partButtons.Length; i++)
            {
                partButtons[i].transform.SetAsLastSibling();
            }

            return panel;
        }

        private static Button[] CreatePartButtons(Transform parent, Font font)
        {
            DrawManager.BodyPart[] parts =
            {
                DrawManager.BodyPart.Head,
                DrawManager.BodyPart.Torso,
                DrawManager.BodyPart.LeftArm,
                DrawManager.BodyPart.RightArm,
                DrawManager.BodyPart.LeftLeg,
                DrawManager.BodyPart.RightLeg,
                DrawManager.BodyPart.LeftFrontLeg,
                DrawManager.BodyPart.RightFrontLeg,
                DrawManager.BodyPart.LeftBackLeg,
                DrawManager.BodyPart.RightBackLeg,
                DrawManager.BodyPart.Tail,
                DrawManager.BodyPart.LeftWing,
                DrawManager.BodyPart.RightWing,
                DrawManager.BodyPart.TailFeather,
                DrawManager.BodyPart.SlimeBody
            };

            Button[] buttons = new Button[parts.Length];
            int columns = 5;
            float spacingX = 118f;
            float spacingY = 42f;
            float startX = -spacingX * (columns - 1) * 0.5f;

            for (int i = 0; i < parts.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                string label = DrawManager.GetPartLabel(parts[i]);
                buttons[i] = CreateButton(
                    $"{parts[i]}Button",
                    parent,
                    font,
                    label,
                    new Vector2(startX + spacingX * column, -10f - spacingY * row),
                    new Vector2(108f, 36f),
                    parts[i] == DrawManager.BodyPart.Torso ? new Color(0.52f, 0.76f, 1f, 0.95f) : new Color(0.98f, 0.94f, 0.82f, 0.95f),
                    null,
                    true);
                SetButtonLabelColor(buttons[i], Color.black);
            }

            return buttons;
        }

        private static GameObject CreateDrawToolPanel(
            Transform parent,
            Font font,
            out Image inkGaugeFill,
            out Button penButton,
            out Button eraserButton,
            out Button undoButton,
            out Slider brushSizeSlider,
            out Text brushSizeValueText)
        {
            GameObject panel = CreatePanel("DrawToolPanel", parent, new Color(0.91f, 0.86f, 0.74f, 0.94f));
            AddPaperTexture(panel, new Color(0.91f, 0.86f, 0.74f, 1f), new Color(0.62f, 0.5f, 0.34f, 1f), 0.12f, 7211);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(930f, 118f);

            penButton = CreateButton("PenToolButton", panel.transform, font, "✏\n" + LocalizationManager.T("pen"), new Vector2(-48f, 248f), new Vector2(82f, 64f), new Color(0.73f, 0.94f, 0.67f, 0.92f));
            eraserButton = CreateButton("EraserToolButton", panel.transform, font, "▱\n" + LocalizationManager.T("eraser"), new Vector2(48f, 248f), new Vector2(86f, 64f), new Color(0.98f, 0.89f, 0.8f, 0.92f));
            SetButtonLabelColor(penButton, Color.black);
            SetButtonLabelColor(eraserButton, Color.black);
            SetButtonLabelFontSize(penButton, 17);
            SetButtonLabelFontSize(eraserButton, 15);

            GameObject sizeChip = CreatePanel("BrushSizeChip", panel.transform, new Color(0.98f, 0.94f, 0.82f, 0.92f));
            RectTransform sizeChipRect = sizeChip.GetComponent<RectTransform>();
            sizeChipRect.anchorMin = new Vector2(0.5f, 1f);
            sizeChipRect.anchorMax = new Vector2(0.5f, 1f);
            sizeChipRect.pivot = new Vector2(0.5f, 1f);
            sizeChipRect.anchoredPosition = new Vector2(0f, -106f);
            sizeChipRect.sizeDelta = new Vector2(188f, 66f);

            Text sizeTitle = CreateText("BrushSizeTitle", sizeChip.transform, font, 13, TextAnchor.MiddleLeft);
            sizeTitle.text = LocalizationManager.T("brush_size");
            sizeTitle.color = Color.black;
            AddLocalizedText(sizeTitle.gameObject, "brush_size");
            sizeTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            sizeTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            sizeTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            sizeTitle.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            sizeTitle.rectTransform.sizeDelta = new Vector2(44f, 44f);

            brushSizeSlider = CreateBrushSlider("BrushSizeSlider", sizeChip.transform, new Vector2(12f, 32f), new Vector2(94f, 24f));
            brushSizeSlider.minValue = 3f;
            brushSizeSlider.maxValue = 10f;
            brushSizeSlider.value = 6f;
            brushSizeSlider.wholeNumbers = true;

            brushSizeValueText = CreateText("BrushSizeValueText", sizeChip.transform, font, 17, TextAnchor.MiddleCenter);
            brushSizeValueText.text = "6 px";
            brushSizeValueText.color = new Color(0.05f, 0.58f, 0.72f, 1f);
            brushSizeValueText.fontStyle = FontStyle.Bold;
            brushSizeValueText.rectTransform.anchorMin = new Vector2(1f, 0f);
            brushSizeValueText.rectTransform.anchorMax = new Vector2(1f, 0f);
            brushSizeValueText.rectTransform.pivot = new Vector2(1f, 0f);
            brushSizeValueText.rectTransform.anchoredPosition = new Vector2(-12f, 6f);
            brushSizeValueText.rectTransform.sizeDelta = new Vector2(58f, 24f);

            Text inkTitle = CreateText("InkUsageTitle", panel.transform, font, 16, TextAnchor.MiddleLeft);
            inkTitle.text = LocalizationManager.T("ink_usage");
            inkTitle.color = Color.black;
            AddLocalizedText(inkTitle.gameObject, "ink_usage");
            inkTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            inkTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            inkTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            inkTitle.rectTransform.anchoredPosition = new Vector2(18f, -166f);
            inkTitle.rectTransform.sizeDelta = new Vector2(-24f, 24f);

            GameObject gaugeBack = CreatePanel("InkGaugeBack", panel.transform, new Color(0.98f, 0.94f, 0.82f, 0.88f));
            RectTransform gaugeBackRect = gaugeBack.GetComponent<RectTransform>();
            gaugeBackRect.anchorMin = new Vector2(0.5f, 1f);
            gaugeBackRect.anchorMax = new Vector2(0.5f, 1f);
            gaugeBackRect.pivot = new Vector2(0.5f, 1f);
            gaugeBackRect.anchoredPosition = new Vector2(10f, -200f);
            gaugeBackRect.sizeDelta = new Vector2(118f, 16f);

            GameObject gaugeFill = CreatePanel("InkGaugeFill", gaugeBack.transform, new Color(0.3f, 0.78f, 0.22f, 0.92f));
            inkGaugeFill = gaugeFill.GetComponent<Image>();
            inkGaugeFill.type = Image.Type.Filled;
            inkGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            inkGaugeFill.fillOrigin = 0;
            inkGaugeFill.fillAmount = 0f;

            Button clearButton = CreateButton("ToolClearButton", panel.transform, font, "♲\n" + LocalizationManager.T("clear"), new Vector2(-50f, 6f), new Vector2(88f, 50f), new Color(0.98f, 0.94f, 0.82f, 0.92f));
            undoButton = CreateButton("ToolUndoButton", panel.transform, font, "↶\n" + LocalizationManager.T("undo"), new Vector2(50f, 6f), new Vector2(92f, 50f), new Color(0.98f, 0.94f, 0.82f, 0.92f));
            SetButtonLabelColor(clearButton, Color.black);
            SetButtonLabelColor(undoButton, Color.black);
            SetButtonLabelFontSize(clearButton, 15);
            SetButtonLabelFontSize(undoButton, 13);

            return panel;
        }

        private static Slider CreateBrushSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject sliderObject = new GameObject(name);
            sliderObject.transform.SetParent(parent, false);
            RectTransform rect = sliderObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            GameObject background = CreatePanel("Background", sliderObject.transform, new Color(1f, 1f, 1f, 0.01f));
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, 18f);

            AddPencilSliderTrack(sliderObject.transform, size.x);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObject.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);

            GameObject fill = CreatePanel("Fill", fillArea.transform, new Color(0.05f, 0.68f, 0.82f, 0.95f));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(10f, 3.5f);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0f, 0f);
            handleAreaRect.anchorMax = new Vector2(1f, 1f);
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            GameObject handle = CreatePanel("Handle", handleArea.transform, new Color(0.05f, 0.58f, 0.72f, 0.02f));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(18f, 18f);
            CreateIconDot(handle.transform, Vector2.zero, 15f, new Color(0.05f, 0.68f, 0.82f, 1f));
            CreateIconDot(handle.transform, new Vector2(-2f, 2f), 5f, new Color(0.7f, 0.95f, 1f, 0.65f));

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }
    }
}
