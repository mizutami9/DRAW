using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class DrawScreenVisualPolisher : MonoBehaviour
    {
        private bool polished;
        private readonly float[] brushPresets = { 3f, 5f, 6f, 8f, 10f };
        private float selectedBrushPreset = 6f;
        private GameObject fullResetConfirmDialog;
        private GameObject drawingPresetConfirmDialog;
        private GameObject drawingPresetPopup;
        private DrawManager drawManager;
        private int pendingPresetSlot;
        private DrawManager.Species pendingPresetSpecies;
        private PresetAction pendingPresetAction;

        private enum PresetAction
        {
            Save,
            Load
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLabels;
            ResolveDrawManager();
            Polish();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLabels;
            if (drawManager != null)
            {
                drawManager.CurrentSpeciesChanged -= HandlePresetSpeciesChanged;
            }
        }

        public void Polish()
        {
            if (!polished)
            {
                polished = true;
                RebuildToolLayout();
                StraightenDrawUi();
                EnsureDrawTitlePaintStroke();
                GameplayHudDrawer.RedrawTurtleIcon(FindRect(transform, "TurtleDrawSpeciesButton"));
            }

            ApplyScrapbookVisuals();
            RefreshLabels();
            ApplyTypography();
        }

        private void RebuildToolLayout()
        {
            RectTransform panel = FindRect(transform, "DrawToolPanel");
            if (panel == null)
            {
                return;
            }

            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            // Keep a real paper margin around every control.  The previous 134px dock
            // packed the Yomogi descenders against the bottom edge (and outside it for
            // some languages), while its narrow cards left almost no horizontal inset.
            panel.anchoredPosition = new Vector2(-108f, 10f);
            panel.sizeDelta = new Vector2(1044f, 146f);

            RectTransform abilityCard = EnsureCard(transform as RectTransform, "SpeciesAbilityCard");
            SetCenterRect(abilityCard, new Vector2(92f, 10f), new Vector2(280f, 250f));
            RestyleCard(abilityCard, new Color(1f, 0.965f, 0.78f, 0.97f));

            RectTransform abilityHeader = EnsureImage(abilityCard, "AbilityHeaderBand");
            SetTopRect(abilityHeader, Vector2.zero, new Vector2(280f, 42f), new Vector2(0f, 1f));
            abilityHeader.GetComponent<Image>().color = new Color(0.28f, 0.66f, 0.9f, 1f);
            Text abilityTitle = EnsureLabel(abilityHeader, "AbilityTitleText", string.Empty, 20, TextAnchor.MiddleCenter);
            Stretch(abilityTitle.rectTransform);
            abilityTitle.color = Color.white;
            abilityTitle.fontStyle = FontStyle.Bold;

            RectTransform abilityValue = FindRect(transform, "AbilityPreviewText");
            MoveInto(abilityValue, abilityCard);
            SetTopRect(abilityValue, new Vector2(12f, -48f), new Vector2(256f, 48f), new Vector2(0f, 1f));
            Text abilityValueText = abilityValue != null ? abilityValue.GetComponent<Text>() : null;
            if (abilityValueText != null)
            {
                abilityValueText.fontSize = 31;
                abilityValueText.fontStyle = FontStyle.Bold;
                abilityValueText.alignment = TextAnchor.MiddleCenter;
                abilityValueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                abilityValueText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            Text abilityEffect = EnsureLabel(abilityCard, "AbilityEffectText", string.Empty, 13, TextAnchor.MiddleCenter);
            SetTopRect(abilityEffect.rectTransform, new Vector2(10f, -118f), new Vector2(260f, 58f), new Vector2(0f, 1f));
            abilityEffect.fontStyle = FontStyle.Bold;

            RectTransform abilityGauge = EnsureGauge(abilityCard, "AbilityGaugeBack", "AbilityGaugeFill");
            SetTopRect(abilityGauge, new Vector2(18f, -136f), new Vector2(244f, 18f), new Vector2(0f, 1f));
            ConfigureStraightGauge(abilityGauge, "AbilityGaugeFill");
            for (int i = 1; i < 4; i++)
            {
                RectTransform tick = EnsureImage(abilityGauge, "AbilityTick" + i);
                tick.anchorMin = new Vector2(i / 4f, 0f);
                tick.anchorMax = new Vector2(i / 4f, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = Vector2.zero;
                tick.sizeDelta = new Vector2(2f, 0f);
                tick.GetComponent<Image>().color = new Color(0.16f, 0.13f, 0.1f, 0.48f);
            }

            Text humanJumpLabel = EnsureLabel(
                abilityCard, "HumanJumpGaugeLabel", string.Empty, 12, TextAnchor.MiddleLeft);
            SetTopRect(humanJumpLabel.rectTransform, new Vector2(12f, -131f), new Vector2(54f, 18f), new Vector2(0f, 1f));
            humanJumpLabel.fontStyle = FontStyle.Bold;

            RectTransform humanArmGauge = EnsureGauge(
                abilityCard, "HumanArmGaugeBack", "HumanArmGaugeFill");
            SetTopRect(humanArmGauge, new Vector2(68f, -155f), new Vector2(194f, 14f), new Vector2(0f, 1f));
            ConfigureStraightGauge(humanArmGauge, "HumanArmGaugeFill");
            Text humanArmLabel = EnsureLabel(
                abilityCard, "HumanArmGaugeLabel", string.Empty, 12, TextAnchor.MiddleLeft);
            SetTopRect(humanArmLabel.rectTransform, new Vector2(12f, -153f), new Vector2(54f, 18f), new Vector2(0f, 1f));
            humanArmLabel.fontStyle = FontStyle.Bold;

            Text abilityLow = EnsureLabel(abilityCard, "AbilityLowText", string.Empty, 12, TextAnchor.MiddleLeft);
            SetTopRect(abilityLow.rectTransform, new Vector2(18f, -156f), new Vector2(120f, 18f), new Vector2(0f, 1f));
            Text abilityHigh = EnsureLabel(abilityCard, "AbilityHighText", string.Empty, 12, TextAnchor.MiddleRight);
            SetTopRect(abilityHigh.rectTransform, new Vector2(142f, -156f), new Vector2(120f, 18f), new Vector2(0f, 1f));

            Text abilityInk = EnsureLabel(abilityCard, "AbilityInkText", string.Empty, 15, TextAnchor.MiddleCenter);
            SetTopRect(abilityInk.rectTransform, new Vector2(10f, -177f), new Vector2(260f, 22f), new Vector2(0f, 1f));
            Text abilityHint = EnsureLabel(abilityCard, "AbilityHintText", string.Empty, 13, TextAnchor.MiddleCenter);
            SetTopRect(abilityHint.rectTransform, new Vector2(10f, -184f), new Vector2(260f, 26f), new Vector2(0f, 1f));
            abilityHint.color = new Color(0.28f, 0.23f, 0.16f, 0.82f);

            EnsureDrawingPresetPanel();
            EnsureDrawingPresetConfirmDialog();

            Text header = EnsureLabel(panel, "ToolPanelHeader", "TOOLS", 19, TextAnchor.MiddleLeft);
            header.gameObject.SetActive(false);

            RectTransform pen = FindRect(panel, "PenToolButton");
            RectTransform eraser = FindRect(panel, "EraserToolButton");
            SetDockRect(pen, new Vector2(10f, 8f), new Vector2(134f, 130f));
            SetDockRect(eraser, new Vector2(152f, 8f), new Vector2(134f, 130f));
            EnsureSelectionBadge(pen, new Color(0.08f, 0.64f, 0.78f, 1f));
            EnsureSelectionBadge(eraser, new Color(0.95f, 0.42f, 0.25f, 1f));

            RectTransform brush = FindRect(panel, "BrushSizeChip");
            SetDockRect(brush, new Vector2(294f, 8f), new Vector2(270f, 130f));
            RestyleCard(brush, new Color(1f, 0.975f, 0.9f, 0.96f));
            Text brushHeader = EnsureLabel(brush, "BrushSectionHeader", "BRUSH", 13, TextAnchor.MiddleLeft);
            SetTopRect(brushHeader.rectTransform, new Vector2(16f, -8f), new Vector2(190f, 25f), new Vector2(0f, 1f));
            brushHeader.fontStyle = FontStyle.Bold;
            ConfigureContainedText(brushHeader, 10, 14, TextAnchor.MiddleLeft);
            Hide(FindRect(brush, "BrushSizeTitle"));

            RectTransform slider = FindRect(brush, "BrushSizeSlider");
            Hide(slider);

            RectTransform brushValue = FindRect(brush, "BrushSizeValueText");
            Hide(brushValue);
            Hide(FindRect(brush, "BrushValueBadge"));
            CreateBrushPresetButtons(brush);

            RectTransform inkCard = EnsureCard(panel, "InkStatusCard");
            SetDockRect(inkCard, new Vector2(572f, 8f), new Vector2(292f, 130f));
            RestyleCard(inkCard, new Color(0.92f, 0.975f, 1f, 0.97f));
            MoveInto(FindRect(panel, "InkUsageTitle"), inkCard);
            MoveInto(FindRect(panel, "InkGaugeBack"), inkCard);
            MoveInto(FindRect(panel, "InkText"), inkCard);

            RectTransform inkTitle = FindRect(inkCard, "InkUsageTitle");
            SetTopRect(inkTitle, new Vector2(43f, -7f), new Vector2(237f, 24f), new Vector2(0f, 1f));
            Text inkTitleText = inkTitle != null ? inkTitle.GetComponent<Text>() : null;
            if (inkTitleText != null)
            {
                LocalizedText localized = inkTitle.GetComponent<LocalizedText>();
                if (localized != null)
                {
                    localized.enabled = false;
                }

                inkTitleText.fontSize = 15;
                inkTitleText.fontStyle = FontStyle.Bold;
                inkTitleText.alignment = TextAnchor.MiddleLeft;
                ConfigureContainedText(inkTitleText, 10, 15, TextAnchor.MiddleLeft);
            }

            RectTransform gauge = FindRect(inkCard, "InkGaugeBack");
            SetTopRect(gauge, new Vector2(12f, -57f), new Vector2(268f, 12f), new Vector2(0f, 1f));
            ConfigureStraightGauge(gauge, "InkGaugeFill");
            RectTransform inkText = FindRect(inkCard, "InkText");
            Hide(inkText);

            Text personalLabel = EnsureLabel(inkCard, "PersonalInkLabel", "YOU", 13, TextAnchor.MiddleLeft);
            SetTopRect(personalLabel.rectTransform, new Vector2(12f, -32f), new Vector2(150f, 22f), new Vector2(0f, 1f));
            Text personalValue = EnsureLabel(inkCard, "PersonalInkValue", "0 / 500", 14, TextAnchor.MiddleRight);
            SetTopRect(personalValue.rectTransform, new Vector2(162f, -32f), new Vector2(118f, 22f), new Vector2(0f, 1f));

            Text teamLabel = EnsureLabel(inkCard, "TeamInkLabel", "TEAM", 13, TextAnchor.MiddleLeft);
            SetTopRect(teamLabel.rectTransform, new Vector2(12f, -79f), new Vector2(150f, 22f), new Vector2(0f, 1f));
            Text teamValue = EnsureLabel(inkCard, "TeamInkValue", "0 / 350", 14, TextAnchor.MiddleRight);
            SetTopRect(teamValue.rectTransform, new Vector2(162f, -79f), new Vector2(118f, 22f), new Vector2(0f, 1f));
            RectTransform teamGauge = EnsureGauge(inkCard, "TeamInkGaugeBack", "TeamInkGaugeFill");
            SetTopRect(teamGauge, new Vector2(12f, -107f), new Vector2(268f, 12f), new Vector2(0f, 1f));
            ConfigureStraightGauge(teamGauge, "TeamInkGaugeFill");

            ConfigureContainedText(personalLabel, 9, 13, TextAnchor.MiddleLeft);
            ConfigureContainedText(personalValue, 10, 14, TextAnchor.MiddleRight);
            ConfigureContainedText(teamLabel, 9, 13, TextAnchor.MiddleLeft);
            ConfigureContainedText(teamValue, 10, 14, TextAnchor.MiddleRight);

            if (personalValue != null)
            {
                personalValue.fontStyle = FontStyle.Bold;
            }

            teamValue.fontStyle = FontStyle.Bold;

            RectTransform history = EnsureCard(panel, "HistoryCard");
            SetDockRect(history, new Vector2(872f, 8f), new Vector2(172f, 130f));
            RestyleCard(history, new Color(1f, 0.96f, 0.9f, 0.97f));
            MoveInto(FindRect(panel, "ToolClearButton"), history);
            MoveInto(FindRect(panel, "ToolUndoButton"), history);
            RectTransform clear = FindRect(history, "ToolClearButton");
            RectTransform undo = FindRect(history, "ToolUndoButton");
            RectTransform fullReset = EnsureFullResetButton(history);
            SetDockRect(clear, new Vector2(7f, 89f), new Vector2(158f, 34f));
            SetDockRect(undo, new Vector2(7f, 48f), new Vector2(158f, 34f));
            SetDockRect(fullReset, new Vector2(7f, 7f), new Vector2(158f, 34f));
            EnsureFullResetConfirmDialog();

            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                if (child.name == "IconLine" || child.name == "IconDot")
                {
                    child.gameObject.SetActive(false);
                }
            }

            RectTransform decide = FindRect(transform, "DecideButton");
            RectTransform cancel = FindRect(transform, "CancelDrawButton");
            SetBottomRect(decide, new Vector2(535f, 86f), new Vector2(194f, 72f));
            SetBottomRect(cancel, new Vector2(535f, 16f), new Vector2(194f, 62f));

            HideLegacyDecoration(brush);
            HideLegacyDecoration(gauge);
            HideLegacyDecoration(pen);
            HideLegacyDecoration(eraser);
            HideLegacyDecoration(clear);
            HideLegacyDecoration(undo);
            HideLegacyDecoration(fullReset);
            HideLegacyDecoration(decide);
            HideLegacyDecoration(cancel);
        }

        private void ApplyScrapbookVisuals()
        {
            RectTransform root = FindRect(transform, "DrawPanel");
            if (root == null)
            {
                return;
            }

            EnsureScrapbookFrame(root);

            RectTransform title = FindRect(root, "DrawTitle");
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(0f, 1f);
                title.pivot = new Vector2(0f, 1f);
                title.anchoredPosition = new Vector2(36f, -20f);
                title.sizeDelta = new Vector2(230f, 82f);
                Text titleText = title.GetComponent<Text>();
                if (titleText != null)
                {
                    titleText.fontSize = 47;
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.alignment = TextAnchor.MiddleLeft;
                    titleText.resizeTextForBestFit = true;
                    titleText.resizeTextMinSize = 30;
                    titleText.resizeTextMaxSize = 47;
                }
            }
            EnsureDrawTitlePaintStroke();
            ConfigureDrawTitleGraphic(root, title);

            RectTransform partBar = FindRect(root, "PartButtonBar");
            if (partBar != null)
            {
                Image background = partBar.GetComponent<Image>();
                if (background != null)
                {
                    background.color = Color.clear;
                    background.raycastTarget = false;
                }
                SetEffectEnabled<Outline>(partBar, false);
                SetEffectEnabled<Shadow>(partBar, false);
                ConfigurePartTabs(partBar);
            }

            ConfigureSpeciesRail(root);
            ConfigureNotebookWorkspace(FindRect(root, "DrawArea"), true);
            ConfigureNotebookWorkspace(FindRect(root, "PreviewArea"), false);
            ConfigurePreviewLabel(root);

            RectTransform abilityCard = FindRect(root, "SpeciesAbilityCard");
            RectTransform previewWorkspace = FindRect(root, "PreviewArea");
            float workspaceY = previewWorkspace != null ? previewWorkspace.anchoredPosition.y : 10f;
            SetCenterRect(abilityCard, new Vector2(92f, workspaceY), new Vector2(280f, 250f));
            RestyleCard(abilityCard, new Color(1f, 0.94f, 0.67f, 0.98f));
            EnsurePaperTape(abilityCard, "AbilityPaperTapeLeft", new Vector2(44f, -3f), 54f, -7f,
                new Color(1f, 0.78f, 0.24f, 0.72f));
            EnsurePaperTape(abilityCard, "AbilityPaperTapeRight", new Vector2(230f, -3f), 54f, 6f,
                new Color(0.54f, 0.7f, 1f, 0.68f));
            RectTransform abilityHeader = FindRect(abilityCard, "AbilityHeaderBand");
            if (abilityHeader != null)
            {
                Image headerImage = abilityHeader.GetComponent<Image>();
                if (headerImage != null)
                {
                    DoodlePaperUi.Apply(headerImage, new Color(1f, 0.91f, 0.55f, 0.68f));
                }
            }

            RectTransform toolPanel = FindRect(root, "DrawToolPanel");
            if (toolPanel != null)
            {
                Image toolBackground = toolPanel.GetComponent<Image>();
                if (toolBackground != null)
                {
                    toolBackground.color = Color.clear;
                    toolBackground.raycastTarget = false;
                }
                SetEffectEnabled<Outline>(toolPanel, false);
                SetEffectEnabled<Shadow>(toolPanel, false);
            }

            RectTransform pen = FindRect(root, "PenToolButton");
            RectTransform eraser = FindRect(root, "EraserToolButton");
            ConfigureToolTile(pen, 2, new Color(1f, 0.84f, 0.25f, 1f));
            ConfigureToolTile(eraser, 13, new Color(0.93f, 0.96f, 0.98f, 1f));

            RectTransform brush = FindRect(root, "BrushSizeChip");
            RectTransform ink = FindRect(root, "InkStatusCard");
            RectTransform history = FindRect(root, "HistoryCard");
            RestyleCard(brush, new Color(1f, 0.975f, 0.9f, 0.98f));
            RestyleCard(ink, new Color(0.9f, 0.97f, 1f, 0.98f));
            RestyleCard(history, new Color(1f, 0.95f, 0.88f, 0.98f));
            EnsurePaperTape(brush, "BrushPaperTape", new Vector2(218f, -3f), 42f, 5f,
                new Color(0.39f, 0.76f, 1f, 0.6f));
            EnsurePaperTape(ink, "InkPaperTape", new Vector2(236f, -3f), 42f, -5f,
                new Color(0.37f, 0.8f, 0.58f, 0.62f));

            RectTransform inkIcon = EnsureImage(ink, "InkBottleIcon");
            if (inkIcon != null)
            {
                SetTopRect(inkIcon, new Vector2(12f, -4f), new Vector2(27f, 27f), new Vector2(0f, 1f));
                Image iconImage = inkIcon.GetComponent<Image>();
                iconImage.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(20);
                iconImage.color = new Color(0.08f, 0.16f, 0.3f, 0.9f);
                iconImage.raycastTarget = false;
                RectTransform inkTitle = FindRect(ink, "InkUsageTitle");
                SetTopRect(inkTitle, new Vector2(43f, -7f), new Vector2(237f, 24f), new Vector2(0f, 1f));
            }

            ConfigureCompactAction(FindRect(root, "ToolClearButton"), 14,
                new Color(1f, 0.96f, 0.82f, 1f));
            ConfigureCompactAction(FindRect(root, "ToolUndoButton"), 11,
                new Color(1f, 0.96f, 0.82f, 1f));
            ConfigureCompactAction(FindRect(root, "FullResetButton"), 14,
                new Color(1f, 0.69f, 0.69f, 1f));

            ConfigureLargeAction(FindRect(root, "DecideButton"), 12,
                new Color(0.35f, 0.88f, 0.47f, 1f));
            ConfigureLargeAction(FindRect(root, "CancelDrawButton"), 15,
                new Color(1f, 0.43f, 0.36f, 1f));

            RectTransform presetCard = FindRect(root, "DrawingPresetCard");
            if (presetCard != null)
            {
                LayoutDrawingPresetCard(root, presetCard, abilityCard);
                RestyleCard(presetCard, new Color(0.91f, 0.97f, 1f, 0.98f));
                RectTransform open = FindRect(presetCard, "DrawingPresetOpenButton");
                ConfigureCompactAction(open, 6, new Color(0.5f, 0.82f, 0.96f, 1f));
            }

            if (drawingPresetPopup != null && drawingPresetPopup.activeSelf)
                drawingPresetPopup.transform.SetAsLastSibling();
            if (fullResetConfirmDialog != null && fullResetConfirmDialog.activeSelf)
                fullResetConfirmDialog.transform.SetAsLastSibling();
            if (drawingPresetConfirmDialog != null && drawingPresetConfirmDialog.activeSelf)
                drawingPresetConfirmDialog.transform.SetAsLastSibling();
        }

        private static void EnsureScrapbookFrame(RectTransform root)
        {
            QuietMenuBackdrop.Apply(root, "DrawScrapbookFrame",
                QuietMenuBackdropPreset.Draw);
        }

        private static void ConfigureDrawTitleGraphic(RectTransform root, RectTransform fallbackTitle)
        {
            if (root == null)
            {
                return;
            }

            Sprite titleSprite = Resources.Load<Sprite>("UI/draw-title-crayon-v1");
            RectTransform graphic = EnsureImage(root, "DrawTitleGraphic");
            graphic.anchorMin = new Vector2(0f, 1f);
            graphic.anchorMax = new Vector2(0f, 1f);
            graphic.pivot = new Vector2(0f, 1f);
            graphic.anchoredPosition = new Vector2(30f, -18f);
            graphic.sizeDelta = new Vector2(238f, 84f);
            graphic.localRotation = Quaternion.identity;

            Image image = graphic.GetComponent<Image>();
            image.sprite = titleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            graphic.gameObject.SetActive(titleSprite != null);

            Text fallbackText = fallbackTitle != null ? fallbackTitle.GetComponent<Text>() : null;
            if (fallbackText != null)
            {
                fallbackText.enabled = titleSprite == null;
            }

            RectTransform stroke = FindRect(root, "DrawTitlePaintStroke");
            if (stroke != null)
            {
                stroke.gameObject.SetActive(titleSprite == null);
            }

            if (titleSprite != null && fallbackTitle != null)
            {
                graphic.SetSiblingIndex(Mathf.Max(1, fallbackTitle.GetSiblingIndex()));
            }
        }

        private void ConfigurePartTabs(RectTransform partBar)
        {
            Button[] buttons = partBar.GetComponentsInChildren<Button>(true);
            Color selected = new Color(0.31f, 0.79f, 0.96f, 1f);
            Color normal = new Color(1f, 0.965f, 0.84f, 1f);
            Color[] tapeColors =
            {
                new Color(0.2f, 0.76f, 0.95f, 0.76f),
                new Color(1f, 0.76f, 0.24f, 0.76f),
                new Color(0.96f, 0.47f, 0.52f, 0.72f),
                new Color(0.5f, 0.82f, 0.49f, 0.72f),
                new Color(0.67f, 0.5f, 0.94f, 0.72f)
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(112f, 48f);
                SetPaperButton(rect, normal);
                PartButtonCommand command = rect.GetComponent<PartButtonCommand>();
                command?.ApplyScrapbookPalette(selected, normal);
                EnsurePaperTape(rect, "PartTabTape", new Vector2(56f, -1f), 34f, (i % 3 - 1) * 2f,
                    tapeColors[i % tapeColors.Length]);
                EnsureButtonIcon(rect, "PartSketchIcon", GetPartIconIndex(rect.name),
                    new Vector2(17f, 0f), new Vector2(25f, 25f), new Color(0.11f, 0.1f, 0.08f, 0.92f), false);
                Text label = rect.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.rectTransform.anchorMin = new Vector2(0.29f, 0f);
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(2f, 3f);
                    label.rectTransform.offsetMax = new Vector2(-5f, -3f);
                    label.fontSize = 14;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 9;
                    label.resizeTextMaxSize = 14;
                    label.transform.SetAsLastSibling();
                }
            }
        }

        private void ConfigureSpeciesRail(RectTransform root)
        {
            RectTransform panel = FindRect(root, "DrawSpeciesPanel");
            if (panel == null)
            {
                return;
            }

            RestyleCard(panel, new Color(1f, 0.97f, 0.84f, 0.92f));
            RectTransform title = FindRect(panel, "DrawSpeciesTitle");
            if (title != null)
            {
                title.gameObject.SetActive(true);
                SetTopRect(title, new Vector2(4f, -5f), new Vector2(64f, 27f), new Vector2(0f, 1f));
                Text label = title.GetComponent<Text>();
                if (label != null)
                {
                    label.fontSize = 12;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 8;
                    label.resizeTextMaxSize = 12;
                }
            }

            string[] names =
            {
                "HumanDrawSpeciesButton", "CatDrawSpeciesButton", "BirdDrawSpeciesButton",
                "TurtleDrawSpeciesButton", "SlimeDrawSpeciesButton"
            };
            Color[] colors =
            {
                new Color(1f, 0.84f, 0.3f, 1f), new Color(1f, 0.9f, 0.72f, 1f),
                new Color(0.86f, 0.95f, 1f, 1f), new Color(0.85f, 0.96f, 0.84f, 1f),
                new Color(0.94f, 0.86f, 1f, 1f)
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform button = FindRect(panel, names[i]);
                if (button == null) continue;
                SetPaperButton(button, colors[i]);
                button.GetComponent<SpeciesButtonCommand>()?.ApplyScrapbookPalette(
                    new Color(1f, 0.78f, 0.25f, 1f), colors[i]);
                EnsurePaperTape(button, "SpeciesPaperTape", new Vector2(25f, -1f), 23f,
                    (i % 2 == 0 ? -4f : 4f), new Color(1f, 0.84f, 0.36f, 0.58f));
            }
        }

        private static void ConfigureNotebookWorkspace(RectTransform workspace, bool drawingArea)
        {
            if (workspace == null)
            {
                return;
            }

            Image paper = workspace.GetComponent<Image>();
            if (paper != null)
            {
                DoodlePaperUi.Apply(paper, drawingArea
                    ? new Color(1f, 0.995f, 0.95f, 1f)
                    : new Color(0.94f, 0.985f, 1f, 1f));
            }
            ApplyStraightOutline(workspace, 2f);
            EnsurePaperShadow(workspace, new Vector2(6f, -7f), 0.2f);

            RectTransform grid = EnsureImage(workspace, drawingArea ? "DrawNotebookGrid" : "PreviewNotebookGrid");
            grid.anchorMin = Vector2.zero;
            grid.anchorMax = Vector2.one;
            grid.offsetMin = new Vector2(14f, 14f);
            grid.offsetMax = new Vector2(-12f, -12f);
            Image gridImage = grid.GetComponent<Image>();
            gridImage.sprite = DoodleRuntimeAssets.DotGridSprite;
            gridImage.type = Image.Type.Tiled;
            gridImage.color = drawingArea
                ? new Color(1f, 1f, 1f, 0.7f)
                : new Color(1f, 1f, 1f, 0.36f);
            gridImage.raycastTarget = false;
            grid.SetAsFirstSibling();

            RectTransform binding = EnsureImage(workspace, "NotebookBindingMargin");
            binding.anchorMin = new Vector2(0f, 0f);
            binding.anchorMax = new Vector2(0f, 1f);
            binding.pivot = new Vector2(0f, 0.5f);
            binding.anchoredPosition = new Vector2(12f, 0f);
            binding.sizeDelta = new Vector2(2f, -24f);
            Image bindingLine = binding.GetComponent<Image>();
            bindingLine.sprite = DoodleRuntimeAssets.SquareSprite;
            bindingLine.color = new Color(0.22f, 0.17f, 0.12f, 0.3f);
            bindingLine.raycastTarget = false;
            binding.SetSiblingIndex(Mathf.Min(1, workspace.childCount - 1));
            for (int i = 0; i < 11; i++)
            {
                RectTransform ring = EnsureImage(workspace, "NotebookRing" + i);
                ring.anchorMin = new Vector2(0f, 0f);
                ring.anchorMax = new Vector2(0f, 0f);
                ring.pivot = new Vector2(0.5f, 0.5f);
                ring.anchoredPosition = new Vector2(12f, 24f + i * 25f);
                ring.sizeDelta = new Vector2(16f, 4f);
                Image ringImage = ring.GetComponent<Image>();
                ringImage.sprite = DoodleRuntimeAssets.CircleSprite;
                ringImage.color = new Color(0.16f, 0.13f, 0.1f, 0.62f);
                ringImage.raycastTarget = false;
                ring.SetSiblingIndex(Mathf.Min(2 + i, workspace.childCount - 1));
            }
        }

        private void ConfigurePreviewLabel(RectTransform root)
        {
            RectTransform preview = FindRect(root, "PreviewArea");
            RectTransform title = FindRect(root, "PreviewTitle");
            if (preview == null || title == null)
            {
                return;
            }

            title.gameObject.SetActive(true);
            title.SetParent(root, false);
            title.anchorMin = new Vector2(0.5f, 0.5f);
            title.anchorMax = new Vector2(0.5f, 0.5f);
            title.pivot = new Vector2(0.5f, 0.5f);
            title.anchoredPosition = preview.anchoredPosition + new Vector2(0f, 166f);
            title.sizeDelta = new Vector2(136f, 38f);
            Text label = title.GetComponent<Text>();
            if (label != null)
            {
                label.fontSize = 20;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
            }

            RectTransform paper = EnsureImage(root, "PreviewTitlePaper");
            paper.anchorMin = title.anchorMin;
            paper.anchorMax = title.anchorMax;
            paper.pivot = title.pivot;
            paper.anchoredPosition = title.anchoredPosition;
            paper.sizeDelta = title.sizeDelta;
            Image image = paper.GetComponent<Image>();
            DoodlePaperUi.Apply(image, new Color(1f, 0.92f, 0.61f, 0.98f));
            image.raycastTarget = false;
            paper.SetSiblingIndex(Mathf.Max(0, title.GetSiblingIndex()));
            title.SetAsLastSibling();
        }

        private static void ConfigureToolTile(RectTransform rect, int iconIndex, Color color)
        {
            if (rect == null) return;
            SetPaperButton(rect, color);
            EnsureButtonIcon(rect, "ScrapbookToolIcon", iconIndex, new Vector2(0f, 21f),
                new Vector2(43f, 43f), new Color(0.1f, 0.09f, 0.07f, 0.94f), true);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                // Leave enough room below the glyph baseline for the handwritten
                // font's long descenders.  Overflow is clipped to the button instead
                // of painting over the scrapbook frame.
                label.rectTransform.anchorMin = new Vector2(0f, 0.1f);
                label.rectTransform.anchorMax = new Vector2(1f, 0.39f);
                label.rectTransform.offsetMin = new Vector2(10f, 2f);
                label.rectTransform.offsetMax = new Vector2(-10f, -2f);
                ConfigureContainedText(label, 11, 18, TextAnchor.MiddleCenter);
                label.transform.SetAsLastSibling();
            }
        }

        private static void ConfigureCompactAction(RectTransform rect, int iconIndex, Color color)
        {
            if (rect == null) return;
            SetPaperButton(rect, color);
            EnsureButtonIcon(rect, "ScrapbookActionIcon", iconIndex, new Vector2(15f, 0f),
                new Vector2(18f, 18f), new Color(0.12f, 0.1f, 0.08f, 0.92f), false);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.rectTransform.anchorMin = new Vector2(0.24f, 0f);
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(4f, 3f);
                label.rectTransform.offsetMax = new Vector2(-9f, -3f);
                ConfigureContainedText(label, 9, 14, TextAnchor.MiddleCenter);
                label.transform.SetAsLastSibling();
            }
        }

        private static void ConfigureLargeAction(RectTransform rect, int iconIndex, Color color)
        {
            if (rect == null) return;
            SetPaperButton(rect, color);
            EnsureButtonIcon(rect, "ScrapbookActionIcon", iconIndex, new Vector2(27f, 0f),
                new Vector2(34f, 34f), new Color(0.08f, 0.09f, 0.07f, 0.94f), false);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.rectTransform.anchorMin = new Vector2(0.28f, 0f);
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(5f, 6f);
                label.rectTransform.offsetMax = new Vector2(-12f, -6f);
                ConfigureContainedText(label, 10, 19, TextAnchor.MiddleCenter);
                label.transform.SetAsLastSibling();
            }
        }

        private static void ConfigureContainedText(Text text, int minimumSize, int maximumSize,
            TextAnchor alignment)
        {
            if (text == null)
            {
                return;
            }

            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minimumSize;
            text.resizeTextMaxSize = maximumSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void SetPaperButton(RectTransform rect, Color color)
        {
            if (rect == null) return;
            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                DoodlePaperUi.Apply(image, color);
            }
            ApplyStraightOutline(rect, 1.5f);
            EnsurePaperShadow(rect, new Vector2(4f, -4f), 0.18f);
        }

        private static void EnsureButtonIcon(RectTransform parent, string name, int iconIndex,
            Vector2 position, Vector2 size, Color color, bool centered)
        {
            if (parent == null) return;
            RectTransform icon = EnsureImage(parent, name);
            icon.anchorMin = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            icon.anchorMax = icon.anchorMin;
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = position;
            icon.sizeDelta = size;
            Image image = icon.GetComponent<Image>();
            image.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(iconIndex);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
            icon.SetSiblingIndex(0);
        }

        private static int GetPartIconIndex(string name)
        {
            if (name.Contains("Head")) return 16;
            if (name.Contains("Torso") || name.Contains("Body")) return 17;
            if (name.Contains("Arm") || name.Contains("Wing") || name.Contains("Tail")) return 18;
            return 19;
        }

        private static void EnsurePaperTape(RectTransform parent, string name, Vector2 position,
            float width, float rotation, Color color)
        {
            if (parent == null) return;
            RectTransform tape = EnsureImage(parent, name);
            tape.anchorMin = new Vector2(0f, 1f);
            tape.anchorMax = new Vector2(0f, 1f);
            tape.pivot = new Vector2(0.5f, 0.5f);
            tape.anchoredPosition = position;
            tape.sizeDelta = new Vector2(width, 8f);
            tape.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = tape.GetComponent<Image>();
            DoodlePaperUi.Apply(image, color);
            image.raycastTarget = false;
            tape.SetSiblingIndex(0);
        }

        private static void EnsurePaperShadow(RectTransform rect, Vector2 distance, float alpha)
        {
            if (rect == null) return;
            if (DoodlePaperUi.IsApplied(rect.GetComponent<Image>()))
            {
                DoodlePaperUi.DisableLegacyEffects(rect.gameObject);
                return;
            }

            Shadow shadow = null;
            Shadow[] shadows = rect.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i] != null && shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }
            if (shadow == null) shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.enabled = true;
            shadow.effectColor = new Color(0.12f, 0.09f, 0.05f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static void SetEffectEnabled<T>(RectTransform rect, bool enabled) where T : Behaviour
        {
            if (rect == null) return;
            T[] effects = rect.GetComponents<T>();
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null) effects[i].enabled = enabled;
            }
        }

        private void RefreshLabels()
        {
            SetPlainText("DrawTitle", LocalizationManager.T("title_draw"));
            SetPlainText("PreviewTitle", LocalizationManager.T("preview"));
            SetPlainText("DrawSpeciesTitle", LocalizationManager.T("draw_preset_button"));
            SetPlainText("BrushSectionHeader", LocalizationManager.T("draw_pen_size"));
            SetPlainText("InkUsageTitle", LocalizationManager.T("draw_ink"));
            SetPlainText("PersonalInkLabel", LocalizationManager.T("ink_personal_cap"));
            SetPlainText("HumanJumpGaugeLabel", LocalizationManager.T("ability_human_jump_gauge"));
            SetPlainText("HumanArmGaugeLabel", LocalizationManager.T("ability_human_arm_gauge"));
            SetButtonLabel("PenToolButton", LocalizationManager.T("pen"), 18);
            SetButtonLabel("EraserToolButton", LocalizationManager.T("eraser"), 17);
            SetButtonLabel("ToolClearButton", LocalizationManager.T("draw_clear_part"), 14);
            SetButtonLabel("ToolUndoButton", LocalizationManager.T("draw_undo_once"), 14);
            SetButtonLabel("FullResetButton", LocalizationManager.T("draw_reset_all"), 13);
            SetPlainText("FullResetConfirmTitle", LocalizationManager.T("draw_reset_confirm_title"));
            SetPlainText("FullResetConfirmMessage", LocalizationManager.T("draw_reset_confirm_message"));
            SetButtonLabel("FullResetConfirmButton", LocalizationManager.T("draw_reset_confirm_yes"), 18);
            SetButtonLabel("FullResetCancelButton", LocalizationManager.T("draw_reset_confirm_no"), 18);
            RefreshPresetSlotVisuals();
            SetButtonLabel("DecideButton", LocalizationManager.T("draw_finish") + "\nENTER", 19);
            SetButtonLabel("CancelDrawButton", LocalizationManager.T("ui_back_esc"), 16);
            Text backLabel = FindRect(transform, "CancelDrawButton")?.GetComponentInChildren<Text>(true);
            if (backLabel != null)
            {
                backLabel.resizeTextForBestFit = true;
                backLabel.resizeTextMinSize = 10;
                backLabel.resizeTextMaxSize = 16;
            }
            ApplyTypography();
        }

        private void EnsureDrawTitlePaintStroke()
        {
            RectTransform panel = FindRect(transform, "DrawPanel");
            RectTransform title = FindRect(transform, "DrawTitle");
            if (panel == null || title == null) return;

            Transform existing = panel.Find("DrawTitlePaintStroke");
            GameObject stroke = existing != null ? existing.gameObject : null;
            if (stroke == null)
            {
                stroke = new GameObject("DrawTitlePaintStroke", typeof(RectTransform));
                stroke.transform.SetParent(panel, false);
                Color ink = new Color(1f, 0.82f, 0.22f, 1f);
                for (int i = 0; i < 4; i++)
                {
                    GameObject stripe = new GameObject("PaintStripe" + i,
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    stripe.transform.SetParent(stroke.transform, false);
                    RectTransform stripeRect = stripe.GetComponent<RectTransform>();
                    stripeRect.anchorMin = stripeRect.anchorMax = new Vector2(0.5f, 0.5f);
                    stripeRect.pivot = new Vector2(0.5f, 0.5f);
                    stripeRect.anchoredPosition = new Vector2((i - 1.5f) * 3f, (i - 1.5f) * 2.5f);
                    stripeRect.sizeDelta = new Vector2(180f - i * 10f, 13f + i * 2f);
                    stripeRect.localRotation = Quaternion.Euler(0f, 0f, -1.8f + i * 1.05f);
                    Image image = stripe.GetComponent<Image>();
                    image.color = new Color(ink.r, ink.g, ink.b, 0.2f + i * 0.07f);
                    image.raycastTarget = false;
                }
            }

            RectTransform rect = stroke.GetComponent<RectTransform>();
            rect.anchorMin = title.anchorMin;
            rect.anchorMax = title.anchorMax;
            rect.pivot = title.pivot;
            rect.anchoredPosition = title.anchoredPosition + new Vector2(0f, -8f);
            rect.sizeDelta = new Vector2(190f, 48f);
            rect.localRotation = Quaternion.identity;
            stroke.SetActive(true);
            stroke.transform.SetSiblingIndex(Mathf.Max(0, title.GetSiblingIndex()));
            title.SetAsLastSibling();
        }

        private void SetPlainText(string name, string value)
        {
            RectTransform rect = FindRect(transform, name);
            Text text = rect != null ? rect.GetComponent<Text>() : null;
            if (text != null)
            {
                text.text = value;
            }
        }

        private void SetButtonLabel(string name, string value, int size)
        {
            RectTransform rect = FindRect(transform, name);
            Text text = rect != null ? rect.GetComponentInChildren<Text>(true) : null;
            if (text == null)
            {
                return;
            }

            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            ConfigureContainedText(text, Mathf.Min(9, size), size, TextAnchor.MiddleCenter);
        }

        private void CreateBrushPresetButtons(RectTransform brush)
        {
            if (brush == null)
            {
                return;
            }

            DrawManager drawManager = FindFirstObjectByType<DrawManager>();
            for (int i = 0; i < brushPresets.Length; i++)
            {
                float preset = brushPresets[i];
                string name = "BrushPreset" + preset.ToString("0");
                Transform existing = brush.Find(name);
                RectTransform rect;
                Button button;
                if (existing == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    obj.transform.SetParent(brush, false);
                    rect = obj.GetComponent<RectTransform>();
                    button = obj.GetComponent<Button>();
                }
                else
                {
                    rect = existing as RectTransform;
                    button = existing.GetComponent<Button>();
                }

                SetDockRect(rect, new Vector2(12f + i * 49f, 10f), new Vector2(45f, 70f));
                DoodlePaperUi.Apply(rect.GetComponent<Image>(),
                    new Color(1f, 0.985f, 0.925f, 1f));
                Outline outline = rect.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = rect.gameObject.AddComponent<Outline>();
                }
                outline.enabled = false;

                Text number = EnsureLabel(rect, "Value", preset.ToString("0"), 12, TextAnchor.MiddleCenter);
                number.rectTransform.anchorMin = new Vector2(0f, 0f);
                number.rectTransform.anchorMax = new Vector2(1f, 0f);
                number.rectTransform.pivot = new Vector2(0.5f, 0f);
                number.rectTransform.anchoredPosition = new Vector2(0f, 6f);
                number.rectTransform.sizeDelta = new Vector2(0f, 20f);
                number.fontStyle = FontStyle.Bold;
                ConfigureContainedText(number, 10, 12, TextAnchor.MiddleCenter);

                RectTransform sample = EnsureImage(rect, "StrokeSample");
                sample.anchorMin = new Vector2(0.5f, 1f);
                sample.anchorMax = new Vector2(0.5f, 1f);
                sample.pivot = new Vector2(0.5f, 0.5f);
                sample.anchoredPosition = new Vector2(0f, -19f);
                sample.sizeDelta = new Vector2(24f, Mathf.Lerp(2f, 9f, i / 4f));
                sample.GetComponent<Image>().color = new Color(0.1f, 0.09f, 0.08f, 1f);

                Text check = EnsureLabel(rect, "SelectedCheck", "\u2713", 12, TextAnchor.UpperRight);
                check.rectTransform.anchorMin = new Vector2(0f, 0f);
                check.rectTransform.anchorMax = new Vector2(1f, 1f);
                check.rectTransform.offsetMin = new Vector2(2f, 1f);
                check.rectTransform.offsetMax = new Vector2(-3f, -1f);
                check.fontStyle = FontStyle.Bold;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    selectedBrushPreset = preset;
                    drawManager?.SetBrushSizePixels(preset);
                    ApplyBrushPresetSelection();
                });
            }

            ApplyBrushPresetSelection();
        }

        private RectTransform EnsureFullResetButton(RectTransform parent)
        {
            if (parent == null)
            {
                return null;
            }

            RectTransform rect = FindRect(parent, "FullResetButton");
            Button button;
            if (rect == null)
            {
                GameObject obj = new GameObject("FullResetButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                button = obj.GetComponent<Button>();
            }
            else
            {
                button = rect.GetComponent<Button>();
            }

            Image image = rect.GetComponent<Image>();
            DoodlePaperUi.Apply(image, new Color(1f, 0.72f, 0.62f, 1f));
            Text label = EnsureLabel(rect, "Label", string.Empty, 13, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyle.Bold;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OpenFullResetConfirmDialog);
            ApplyStraightOutline(rect, 1.5f);
            return rect;
        }

        private void EnsureFullResetConfirmDialog()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            RectTransform overlay = FindRect(root, "FullResetConfirmDialog");
            if (overlay == null)
            {
                GameObject overlayObject = new GameObject(
                    "FullResetConfirmDialog",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlayObject.transform.SetParent(root, false);
                overlay = overlayObject.GetComponent<RectTransform>();
            }

            Stretch(overlay);
            Image blocker = overlay.GetComponent<Image>();
            blocker.color = new Color(0.05f, 0.045f, 0.035f, 0.62f);
            blocker.raycastTarget = true;

            RectTransform card = EnsureCard(overlay, "ConfirmCard");
            SetCenterRect(card, Vector2.zero, new Vector2(520f, 260f));
            RestyleCard(card, new Color(1f, 0.975f, 0.88f, 1f));

            Text title = EnsureLabel(card, "FullResetConfirmTitle", string.Empty, 28, TextAnchor.MiddleCenter);
            SetTopRect(title.rectTransform, new Vector2(30f, -24f), new Vector2(460f, 48f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            Text message = EnsureLabel(card, "FullResetConfirmMessage", string.Empty, 19, TextAnchor.MiddleCenter);
            SetTopRect(message.rectTransform, new Vector2(42f, -82f), new Vector2(436f, 76f), new Vector2(0f, 1f));
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;

            Button confirm = EnsureDialogButton(card, "FullResetConfirmButton", new Color(1f, 0.42f, 0.32f, 1f));
            SetDockRect(confirm.GetComponent<RectTransform>(), new Vector2(270f, 24f), new Vector2(202f, 58f));
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(ConfirmFullReset);

            Button cancel = EnsureDialogButton(card, "FullResetCancelButton", new Color(0.82f, 0.82f, 0.75f, 1f));
            SetDockRect(cancel.GetComponent<RectTransform>(), new Vector2(48f, 24f), new Vector2(202f, 58f));
            cancel.onClick.RemoveAllListeners();
            cancel.onClick.AddListener(CloseFullResetConfirmDialog);

            fullResetConfirmDialog = overlay.gameObject;
            fullResetConfirmDialog.SetActive(false);
        }

        private Button EnsureDialogButton(RectTransform parent, string name, Color color)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
            }

            Image image = rect.GetComponent<Image>();
            DoodlePaperUi.Apply(image, color);
            Button button = rect.GetComponent<Button>();
            Text label = EnsureLabel(rect, "Label", string.Empty, 18, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyle.Bold;
            ApplyStraightOutline(rect, 2f);
            return button;
        }

        private void OpenFullResetConfirmDialog()
        {
            if (fullResetConfirmDialog == null)
            {
                EnsureFullResetConfirmDialog();
            }

            if (fullResetConfirmDialog != null)
            {
                RefreshLabels();
                fullResetConfirmDialog.SetActive(true);
                fullResetConfirmDialog.transform.SetAsLastSibling();
            }
        }

        private void CloseFullResetConfirmDialog()
        {
            if (fullResetConfirmDialog != null)
            {
                fullResetConfirmDialog.SetActive(false);
            }
        }

        private void ConfirmFullReset()
        {
            FindFirstObjectByType<DrawManager>()?.ResetAllToDefault();
            CloseFullResetConfirmDialog();
        }

        private void EnsureDrawingPresetPanel()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            RectTransform panel = EnsureCard(root, "DrawingPresetCard");
            LayoutDrawingPresetCard(root, panel, FindRect(root, "SpeciesAbilityCard"));
            RestyleCard(panel, new Color(0.9f, 0.97f, 1f, 0.98f));
            Button open = EnsureDialogButton(panel, "DrawingPresetOpenButton", new Color(0.3f, 0.7f, 0.94f, 1f));
            Stretch(open.GetComponent<RectTransform>());
            open.onClick.RemoveAllListeners();
            open.onClick.AddListener(OpenDrawingPresetPopup);

            RectTransform overlay = FindRect(root, "DrawingPresetPopup");
            if (overlay == null)
            {
                GameObject obj = new GameObject("DrawingPresetPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(root, false);
                overlay = obj.GetComponent<RectTransform>();
            }
            Stretch(overlay);
            Image blocker = overlay.GetComponent<Image>();
            blocker.color = new Color(0.04f, 0.04f, 0.035f, 0.64f);
            blocker.raycastTarget = true;

            RectTransform popup = EnsureCard(overlay, "DrawingPresetPopupCard");
            SetCenterRect(popup, Vector2.zero, new Vector2(980f, 650f));
            RestyleCard(popup, new Color(1f, 0.975f, 0.86f, 1f));
            Text title = EnsureLabel(popup, "DrawingPresetTitle", string.Empty, 28, TextAnchor.MiddleCenter);
            SetTopRect(title.rectTransform, new Vector2(35f, -16f), new Vector2(910f, 45f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            DrawManager.Species[] presetSpecies =
            {
                DrawManager.Species.Human,
                DrawManager.Species.Cat,
                DrawManager.Species.Bird,
                DrawManager.Species.Turtle,
                DrawManager.Species.Slime
            };

            for (int i = 0; i < CharacterDrawingPresetStore.SlotCount; i++)
            {
                int slot = i;
                RectTransform slotCard = EnsureCard(popup, "DrawingPresetSlot" + (i + 1));
                SetTopRect(slotCard, new Vector2(28f + i * 310f, -72f), new Vector2(294f, 510f), new Vector2(0f, 1f));
                RestyleCard(slotCard, i % 2 == 0
                    ? new Color(1f, 0.94f, 0.72f, 1f)
                    : new Color(0.9f, 0.96f, 1f, 1f));
                Text slotLabel = EnsureLabel(slotCard, "SlotLabel", string.Empty, 19, TextAnchor.MiddleCenter);
                SetTopRect(slotLabel.rectTransform, new Vector2(12f, -7f), new Vector2(270f, 30f), new Vector2(0f, 1f));
                slotLabel.fontStyle = FontStyle.Bold;
                for (int speciesIndex = 0; speciesIndex < presetSpecies.Length; speciesIndex++)
                {
                    DrawManager.Species species = presetSpecies[speciesIndex];
                    RectTransform row = EnsureCard(slotCard, "PresetSpecies_" + species);
                    SetTopRect(row, new Vector2(10f, -43f - speciesIndex * 90f), new Vector2(274f, 82f), new Vector2(0f, 1f));
                    RestyleCard(row, new Color(1f, 0.99f, 0.93f, 0.96f));
                    Text speciesLabel = EnsureLabel(row, "SpeciesLabel", string.Empty, 15, TextAnchor.MiddleLeft);
                    SetTopRect(speciesLabel.rectTransform, new Vector2(72f, -4f), new Vector2(98f, 24f), new Vector2(0f, 1f));
                    speciesLabel.fontStyle = FontStyle.Bold;
                    Text status = EnsureLabel(row, "StatusLabel", string.Empty, 11, TextAnchor.MiddleRight);
                    SetTopRect(status.rectTransform, new Vector2(172f, -4f), new Vector2(92f, 24f), new Vector2(0f, 1f));
                    RectTransform previewRect = FindRect(row, "PresetDrawingPreview");
                    if (previewRect == null)
                    {
                        GameObject previewObject = new GameObject(
                            "PresetDrawingPreview",
                            typeof(RectTransform),
                            typeof(CanvasRenderer),
                            typeof(DrawingPresetPreviewGraphic));
                        previewObject.transform.SetParent(row, false);
                        previewRect = previewObject.GetComponent<RectTransform>();
                    }
                    SetTopRect(previewRect, new Vector2(8f, -7f), new Vector2(56f, 68f), new Vector2(0f, 1f));
                    DrawingPresetPreviewGraphic preview = previewRect.GetComponent<DrawingPresetPreviewGraphic>();
                    preview.raycastTarget = false;
                    preview.color = new Color(0.08f, 0.34f, 0.52f, 0.95f);
                    Button save = EnsureDialogButton(row, "PresetSaveButton", new Color(1f, 0.65f, 0.16f, 1f));
                    SetDockRect(save.GetComponent<RectTransform>(), new Vector2(70f, 7f), new Vector2(94f, 42f));
                    save.onClick.RemoveAllListeners();
                    save.onClick.AddListener(() => OpenDrawingPresetConfirm(slot, species, PresetAction.Save));
                    Button load = EnsureDialogButton(row, "PresetLoadButton", new Color(0.22f, 0.68f, 0.9f, 1f));
                    SetDockRect(load.GetComponent<RectTransform>(), new Vector2(172f, 7f), new Vector2(94f, 42f));
                    load.onClick.RemoveAllListeners();
                    load.onClick.AddListener(() => OpenDrawingPresetConfirm(slot, species, PresetAction.Load));
                }
            }

            Button close = EnsureDialogButton(popup, "DrawingPresetCloseButton", new Color(0.9f, 0.42f, 0.32f, 1f));
            SetDockRect(close.GetComponent<RectTransform>(), new Vector2(820f, 18f), new Vector2(130f, 48f));
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(CloseDrawingPresetPopup);
            FitDrawingPresetPopup(root, popup);
            drawingPresetPopup = overlay.gameObject;
            drawingPresetPopup.SetActive(false);
        }

        private static void LayoutDrawingPresetCard(
            RectTransform root,
            RectTransform presetCard,
            RectTransform abilityCard)
        {
            if (presetCard == null)
            {
                return;
            }

            // Keep presets with the character information they affect. The old fixed
            // top-left position collided with the species rail on narrower/aspect-ratio
            // constrained layouts. Anchoring above the ability card keeps the rail clear
            // and remains stable as the canvas scaler changes the root dimensions.
            const float width = 156f;
            const float height = 46f;
            const float cardGap = 10f;
            Vector2 position = abilityCard != null
                ? abilityCard.anchoredPosition + new Vector2(
                    0f,
                    abilityCard.rect.height * 0.5f + height * 0.5f + cardGap)
                : new Vector2(92f, 158f);

            RectTransform partBar = FindRect(root, "PartButtonBar");
            if (root != null && partBar != null && root.rect.height > 0f)
            {
                float partBarBottom = root.rect.height * 0.5f
                    + partBar.anchoredPosition.y
                    - partBar.rect.height;
                position.y = Mathf.Min(position.y, partBarBottom - height * 0.5f - 8f);
            }

            SetCenterRect(presetCard, position, new Vector2(width, height));
        }

        private static void FitDrawingPresetPopup(RectTransform root, RectTransform popup)
        {
            if (root == null || popup == null)
            {
                return;
            }

            const float horizontalMargin = 20f;
            const float verticalMargin = 12f;
            const float popupWidth = 980f;
            const float popupHeight = 650f;
            float widthScale = (root.rect.width - horizontalMargin * 2f) / popupWidth;
            float heightScale = (root.rect.height - verticalMargin * 2f) / popupHeight;
            float scale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.72f, 1f);
            popup.localScale = Vector3.one * scale;
        }

        private void OpenDrawingPresetPopup()
        {
            if (drawingPresetPopup == null) EnsureDrawingPresetPanel();
            RefreshPresetSlotVisuals();
            if (drawingPresetPopup != null)
            {
                FitDrawingPresetPopup(
                    transform as RectTransform,
                    FindRect(drawingPresetPopup.transform, "DrawingPresetPopupCard"));
                drawingPresetPopup.SetActive(true);
                drawingPresetPopup.transform.SetAsLastSibling();
            }
        }

        private void CloseDrawingPresetPopup()
        {
            if (drawingPresetPopup != null) drawingPresetPopup.SetActive(false);
        }

        private void EnsureDrawingPresetConfirmDialog()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            RectTransform overlay = FindRect(root, "DrawingPresetConfirmDialog");
            if (overlay == null)
            {
                GameObject obj = new GameObject(
                    "DrawingPresetConfirmDialog",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                obj.transform.SetParent(root, false);
                overlay = obj.GetComponent<RectTransform>();
            }

            Stretch(overlay);
            Image blocker = overlay.GetComponent<Image>();
            blocker.color = new Color(0.05f, 0.045f, 0.035f, 0.62f);
            blocker.raycastTarget = true;

            RectTransform card = EnsureCard(overlay, "ConfirmCard");
            SetCenterRect(card, Vector2.zero, new Vector2(540f, 270f));
            RestyleCard(card, new Color(1f, 0.975f, 0.88f, 1f));
            Text title = EnsureLabel(card, "DrawingPresetConfirmTitle", string.Empty, 27, TextAnchor.MiddleCenter);
            SetTopRect(title.rectTransform, new Vector2(28f, -24f), new Vector2(484f, 48f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;
            Text message = EnsureLabel(card, "DrawingPresetConfirmMessage", string.Empty, 18, TextAnchor.MiddleCenter);
            SetTopRect(message.rectTransform, new Vector2(44f, -83f), new Vector2(452f, 82f), new Vector2(0f, 1f));
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;

            Button confirm = EnsureDialogButton(card, "DrawingPresetConfirmButton", new Color(0.24f, 0.72f, 0.48f, 1f));
            SetDockRect(confirm.GetComponent<RectTransform>(), new Vector2(282f, 24f), new Vector2(210f, 58f));
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(ConfirmDrawingPresetAction);
            Button cancel = EnsureDialogButton(card, "DrawingPresetCancelButton", new Color(0.82f, 0.82f, 0.75f, 1f));
            SetDockRect(cancel.GetComponent<RectTransform>(), new Vector2(48f, 24f), new Vector2(210f, 58f));
            cancel.onClick.RemoveAllListeners();
            cancel.onClick.AddListener(CloseDrawingPresetConfirm);

            drawingPresetConfirmDialog = overlay.gameObject;
            drawingPresetConfirmDialog.SetActive(false);
        }

        private void ResolveDrawManager()
        {
            DrawManager resolved = drawManager != null ? drawManager : FindFirstObjectByType<DrawManager>();
            if (drawManager != null && drawManager != resolved)
            {
                drawManager.CurrentSpeciesChanged -= HandlePresetSpeciesChanged;
            }

            drawManager = resolved;
            if (drawManager != null)
            {
                drawManager.CurrentSpeciesChanged -= HandlePresetSpeciesChanged;
                drawManager.CurrentSpeciesChanged += HandlePresetSpeciesChanged;
            }
        }

        private void HandlePresetSpeciesChanged(DrawManager.Species species)
        {
            RefreshPresetSlotVisuals();
        }

        private void RefreshPresetSlotVisuals()
        {
            ResolveDrawManager();
            if (drawManager == null)
            {
                return;
            }

            SetPlainText("DrawingPresetTitle", LocalizationManager.T("draw_preset_sets_title"));
            SetButtonText(FindRect(transform, "DrawingPresetOpenButton")?.GetComponent<Button>(),
                LocalizationManager.T("draw_preset_button"), 17);
            SetButtonText(FindRect(transform, "DrawingPresetCloseButton")?.GetComponent<Button>(),
                LocalizationManager.T("draw_preset_back"), 16);
            for (int i = 0; i < CharacterDrawingPresetStore.SlotCount; i++)
            {
                RectTransform slot = FindRect(transform, "DrawingPresetSlot" + (i + 1));
                if (slot == null)
                {
                    continue;
                }

                Text slotLabel = slot.Find("SlotLabel")?.GetComponent<Text>();
                if (slotLabel != null)
                {
                    slotLabel.text = LocalizationManager.Format("draw_preset_slot", i + 1);
                }
                foreach (DrawManager.Species species in System.Enum.GetValues(typeof(DrawManager.Species)))
                {
                    Transform row = slot.Find("PresetSpecies_" + species);
                    if (row == null) continue;
                    bool exists = CharacterDrawingPresetStore.Exists(species, i);
                    Text speciesLabel = row.Find("SpeciesLabel")?.GetComponent<Text>();
                    Text status = row.Find("StatusLabel")?.GetComponent<Text>();
                    if (speciesLabel != null)
                        speciesLabel.text = LocalizationManager.T(StageSpeciesRules.GetSpeciesLocalizationKey(species));
                    if (status != null)
                    {
                        status.text = LocalizationManager.T(exists ? "draw_preset_saved" : "draw_preset_empty");
                        status.color = exists ? new Color(0.08f, 0.52f, 0.3f, 1f) : new Color(0.45f, 0.42f, 0.36f, 1f);
                    }
                    Button save = row.Find("PresetSaveButton")?.GetComponent<Button>();
                    Button load = row.Find("PresetLoadButton")?.GetComponent<Button>();
                    DrawingPresetPreviewGraphic preview = row.Find("PresetDrawingPreview")
                        ?.GetComponent<DrawingPresetPreviewGraphic>();
                    preview?.SetDrawing(
                        exists ? CharacterDrawingPresetStore.Load(species, i) : null,
                        species);
                    SetButtonText(save, LocalizationManager.T("draw_preset_register"), 12);
                    SetButtonText(load, LocalizationManager.T("draw_preset_apply"), 12);
                    if (load != null) load.interactable = exists;
                }
            }
        }

        private static void SetButtonText(Button button, string value, int size)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label != null)
            {
                label.text = value;
                label.fontSize = size;
                label.fontStyle = FontStyle.Bold;
            }
        }

        private void OpenDrawingPresetConfirm(int slot, DrawManager.Species species, PresetAction action)
        {
            ResolveDrawManager();
            if (drawManager == null
                || action == PresetAction.Load
                && !CharacterDrawingPresetStore.Exists(species, slot))
            {
                return;
            }

            pendingPresetSlot = slot;
            pendingPresetSpecies = species;
            pendingPresetAction = action;
            if (drawingPresetConfirmDialog == null)
            {
                EnsureDrawingPresetConfirmDialog();
            }

            string speciesName = LocalizationManager.T(StageSpeciesRules.GetSpeciesLocalizationKey(species));
            string titleKey = action == PresetAction.Save
                ? "draw_preset_save_confirm_title"
                : "draw_preset_load_confirm_title";
            bool overwrite = action == PresetAction.Save
                && CharacterDrawingPresetStore.Exists(species, slot);
            string messageKey = action == PresetAction.Load
                ? "draw_preset_load_confirm_message"
                : overwrite
                    ? "draw_preset_overwrite_confirm_message"
                    : "draw_preset_save_confirm_message";
            SetPlainText("DrawingPresetConfirmTitle", LocalizationManager.Format(titleKey, slot + 1));
            SetPlainText(
                "DrawingPresetConfirmMessage",
                LocalizationManager.Format(messageKey, speciesName, slot + 1));
            RectTransform confirmRect = FindRect(transform, "DrawingPresetConfirmButton");
            SetButtonText(
                confirmRect != null ? confirmRect.GetComponent<Button>() : null,
                LocalizationManager.T(action == PresetAction.Save
                    ? "draw_preset_register"
                    : "draw_preset_apply"),
                18);
            RectTransform cancelRect = FindRect(transform, "DrawingPresetCancelButton");
            SetButtonText(
                cancelRect != null ? cancelRect.GetComponent<Button>() : null,
                LocalizationManager.T("draw_reset_confirm_no"),
                18);
            ApplyTypography();
            drawingPresetConfirmDialog.SetActive(true);
            drawingPresetConfirmDialog.transform.SetAsLastSibling();
        }

        private void ConfirmDrawingPresetAction()
        {
            ResolveDrawManager();
            if (drawManager == null)
            {
                CloseDrawingPresetConfirm();
                return;
            }

            bool successful;
            if (pendingPresetAction == PresetAction.Save)
            {
                successful = CharacterDrawingPresetStore.Save(
                    pendingPresetSpecies, pendingPresetSlot, drawManager.CreateState());
            }
            else
            {
                DrawManager.DrawingState preset = CharacterDrawingPresetStore.Load(
                    pendingPresetSpecies, pendingPresetSlot);
                if (preset != null)
                {
                    drawManager.LoadState(preset, false);
                }

                successful = preset != null;
            }

            if (successful)
            {
                GameSfx.Play(SfxId.DrawConfirm);
            }
            CloseDrawingPresetConfirm();
            RefreshPresetSlotVisuals();
        }

        private void CloseDrawingPresetConfirm()
        {
            if (drawingPresetConfirmDialog != null)
            {
                drawingPresetConfirmDialog.SetActive(false);
            }
        }

        private void ApplyBrushPresetSelection()
        {
            for (int i = 0; i < brushPresets.Length; i++)
            {
                RectTransform rect = FindRect(transform, "BrushPreset" + brushPresets[i].ToString("0"));
                if (rect == null)
                {
                    continue;
                }

                bool selected = Mathf.Approximately(selectedBrushPreset, brushPresets[i]);
                Image image = rect.GetComponent<Image>();
                Color buttonColor = selected
                    ? new Color(0.32f, 0.82f, 0.94f, 1f)
                    : new Color(1f, 0.985f, 0.925f, 1f);
                DoodlePaperUi.Apply(image, buttonColor);
                Button button = rect.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
                colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                Outline outline = rect.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                Transform check = rect.Find("SelectedCheck");
                if (check != null)
                {
                    check.gameObject.SetActive(selected);
                }
            }
        }

        private Text EnsureSelectionBadge(RectTransform button, Color color)
        {
            if (button == null)
            {
                return null;
            }

            RectTransform badge = EnsureImage(button, "SelectionBadge");
            badge.anchorMin = new Vector2(1f, 1f);
            badge.anchorMax = new Vector2(1f, 1f);
            badge.pivot = new Vector2(1f, 1f);
            badge.anchoredPosition = new Vector2(-6f, -6f);
            badge.sizeDelta = new Vector2(27f, 27f);
            badge.GetComponent<Image>().color = color;
            Text check = EnsureLabel(badge, "Check", "\u2713", 19, TextAnchor.MiddleCenter);
            Stretch(check.rectTransform);
            check.color = Color.white;
            check.fontStyle = FontStyle.Bold;
            badge.gameObject.SetActive(false);
            return check;
        }

        private static RectTransform EnsureImage(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            return obj.GetComponent<RectTransform>();
        }

        private void StraightenDrawUi()
        {
            RectTransform drawRoot = FindRect(transform, "DrawPanel");
            HideLegacyDecoration(drawRoot);
            Hide(FindRect(transform, "DrawBackdrop"));
            Hide(FindRect(transform, "DrawCornerDoodles"));

            string[] panels =
            {
                "PartButtonBar", "DrawArea", "PreviewArea", "DrawToolPanel",
                "BrushSizeChip", "InkStatusCard", "HistoryCard", "InkGaugeBack", "TeamInkGaugeBack"
            };
            for (int i = 0; i < panels.Length; i++)
            {
                RectTransform rect = FindRect(transform, panels[i]);
                HideLegacyDecoration(rect);
                HideDirectTapes(rect);
                ApplyStraightOutline(rect, panels[i].Contains("Gauge") ? 1f : 2f);
            }

            RectTransform partBar = FindRect(transform, "PartButtonBar");
            if (partBar != null)
            {
                Button[] partButtons = partBar.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < partButtons.Length; i++)
                {
                    RectTransform rect = partButtons[i].GetComponent<RectTransform>();
                    HideLegacyDecoration(rect);
                    ApplyStraightOutline(rect, 1.5f);
                }
            }

            string[] buttons =
            {
                "PenToolButton", "EraserToolButton", "ToolClearButton", "ToolUndoButton",
                "FullResetButton", "DecideButton", "CancelDrawButton"
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = FindRect(transform, buttons[i]);
                HideLegacyDecoration(rect);
                ApplyStraightOutline(rect, 2f);
            }
        }

        private static void HideDirectTapes(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            for (int i = 0; i < rect.childCount; i++)
            {
                Transform child = rect.GetChild(i);
                if (child.name == "MaskingTape" || child.name == "ModernMaskingTape")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void ApplyStraightOutline(RectTransform rect, float width)
        {
            Image surface = rect != null ? rect.GetComponent<Image>() : null;
            if (surface == null)
            {
                return;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }

            if (DoodlePaperUi.IsApplied(surface))
            {
                outline.enabled = false;
                return;
            }

            outline.enabled = true;
            outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.78f);
            outline.effectDistance = new Vector2(width, -width);
        }

        private Text EnsureLabel(RectTransform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                obj.transform.SetParent(parent, false);
                text = obj.GetComponent<Text>();
                Text template = GetComponentInChildren<Text>(true);
                text.font = template != null ? template.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform EnsureCard(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static RectTransform EnsureGauge(RectTransform parent, string backName, string fillName)
        {
            RectTransform back = FindRect(parent, backName);
            if (back == null)
            {
                GameObject backObject = new GameObject(backName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backObject.transform.SetParent(parent, false);
                back = backObject.GetComponent<RectTransform>();
            }

            RectTransform fill = FindRect(back, fillName);
            if (fill == null)
            {
                GameObject fillObject = new GameObject(fillName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObject.transform.SetParent(back, false);
                fill = fillObject.GetComponent<RectTransform>();
            }

            return back;
        }

        private static void ConfigureStraightGauge(RectTransform gauge, string fillName)
        {
            if (gauge == null)
            {
                return;
            }

            Image background = gauge.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.86f, 0.84f, 0.77f, 1f);
            }

            HideLegacyDecoration(gauge);
            Outline outline = gauge.GetComponent<Outline>();
            if (outline == null)
            {
                outline = gauge.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.12f, 0.1f, 0.08f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            RectTransform fill = FindRect(gauge, fillName);
            if (fill == null)
            {
                return;
            }

            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Simple;
            fillImage.color = new Color(0.12f, 0.72f, 0.48f, 1f);
            fillImage.raycastTarget = false;
        }

        private void ApplyTypography()
        {
            Text reference = FindRect(transform, "DrawTitle")?.GetComponent<Text>();
            Font fallback = reference != null && reference.font != null
                ? reference.font
                : DoodleRuntimeAssets.HandwrittenFont;
            Font font = LocalizationManager.LoadCurrentFont(fallback);
            bool rightToLeft = LocalizationManager.CurrentLanguageIsRightToLeft;
            Text[] texts = GetComponentsInChildren<Text>(true);
            Color ink = new Color(0.12f, 0.1f, 0.08f, 1f);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.font = font;
                bool selectionBadge = text.transform.parent != null && text.transform.parent.name == "SelectionBadge";
                bool abilityHeader = text.transform.parent != null && text.transform.parent.name == "AbilityHeaderBand";
                if (selectionBadge || abilityHeader)
                {
                    text.color = Color.white;
                }
                else if (text.name == "AbilityHintText")
                {
                    text.color = new Color(0.28f, 0.23f, 0.16f, 0.82f);
                }
                else if (text.name != "ConnectionMessageText"
                    && text.name != "StatusLabel")
                {
                    text.color = ink;
                }

                bool buttonLabel = text.GetComponentInParent<Button>() != null;
                bool emphasized = text.name == "DrawTitle"
                    || text.name == "InkUsageTitle"
                    || text.name == "BrushSectionHeader"
                    || text.name == "BrushSizeValueText"
                    || text.name == "PersonalInkLabel"
                    || text.name == "TeamInkLabel"
                    || text.name == "PersonalInkValue"
                    || text.name == "TeamInkValue"
                    || text.name == "AbilityPreviewText"
                    || text.name == "AbilityTitleText"
                    || text.name == "AbilityEffectText"
                    || text.name == "AbilityInkText";
                text.fontStyle = buttonLabel || emphasized ? FontStyle.Bold : FontStyle.Normal;
                if (rightToLeft)
                {
                    if (text.alignment == TextAnchor.MiddleLeft) text.alignment = TextAnchor.MiddleRight;
                    else if (text.alignment == TextAnchor.UpperLeft) text.alignment = TextAnchor.UpperRight;
                    else if (text.alignment == TextAnchor.LowerLeft) text.alignment = TextAnchor.LowerRight;
                }
            }
        }

        private static void RestyleCard(RectTransform card, Color color)
        {
            if (card == null)
            {
                return;
            }

            Image image = card.GetComponent<Image>();
            if (image != null)
            {
                DoodlePaperUi.Apply(image, color);
            }

            Outline outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.gameObject.AddComponent<Outline>();
            }

            outline.enabled = false;
        }

        private static void ModernizeSlider(RectTransform slider)
        {
            Slider sliderControl = slider.GetComponent<Slider>();
            if (sliderControl != null)
            {
                sliderControl.wholeNumbers = true;
            }

            for (int i = 0; i < slider.childCount; i++)
            {
                Transform child = slider.GetChild(i);
                if (child.name == "IconLine" || child.name == "Background")
                {
                    child.gameObject.SetActive(false);
                }
            }

            Transform existing = slider.Find("ModernBrushTrack");
            RectTransform track;
            if (existing == null)
            {
                GameObject obj = new GameObject("ModernBrushTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(slider, false);
                track = obj.GetComponent<RectTransform>();
            }
            else
            {
                track = existing as RectTransform;
            }

            if (track == null)
            {
                return;
            }

            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(-16f, 5f);
            track.SetAsFirstSibling();
            Image image = track.GetComponent<Image>();
            image.color = new Color(0.15f, 0.13f, 0.1f, 0.7f);
            image.raycastTarget = false;

            for (int i = 0; i < 5; i++)
            {
                string name = "BrushTick" + i;
                Transform found = slider.Find(name);
                RectTransform tick;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(slider, false);
                    tick = obj.GetComponent<RectTransform>();
                }
                else
                {
                    tick = found as RectTransform;
                }

                tick.anchorMin = new Vector2(0f, 0.5f);
                tick.anchorMax = new Vector2(0f, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = new Vector2(8f + i * 36f, 0f);
                float diameter = 6f + i * 1.5f;
                tick.sizeDelta = new Vector2(diameter, diameter);
                Image tickImage = tick.GetComponent<Image>();
                tickImage.color = new Color(1f, 0.96f, 0.84f, 1f);
                tickImage.raycastTarget = false;
                tick.SetSiblingIndex(Mathf.Min(i + 1, tick.parent.childCount - 1));
            }
        }

        private static void MoveInto(RectTransform child, RectTransform parent)
        {
            if (child != null && parent != null && child.parent != parent)
            {
                child.SetParent(parent, false);
            }
        }

        private static void SetTopRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetDockRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetBottomRect(RectTransform rect, Vector2 position, Vector2 size)
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

        private static void Hide(RectTransform rect)
        {
            if (rect != null)
            {
                rect.gameObject.SetActive(false);
            }
        }

        private static void HideLegacyDecoration(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            for (int i = 0; i < rect.childCount; i++)
            {
                Transform child = rect.GetChild(i);
                if (child.name == "IconLine"
                    || child.name == "IconDot"
                    || child.name == "SoftFrame"
                    || child.name == "StickyNoteBoldFrame"
                    || child.name == "ButtonBoldFrame")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void EnsureMarker(RectTransform textRect, Color color)
        {
            if (textRect == null || textRect.parent == null || textRect.parent.Find(textRect.name + "Marker") != null)
            {
                return;
            }

            GameObject obj = new GameObject(textRect.name + "Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(textRect.parent, false);
            RectTransform marker = obj.GetComponent<RectTransform>();
            marker.anchorMin = textRect.anchorMin;
            marker.anchorMax = textRect.anchorMax;
            marker.pivot = textRect.pivot;
            marker.anchoredPosition = textRect.anchoredPosition + new Vector2(0f, -8f);
            marker.sizeDelta = new Vector2(82f, 9f);
            marker.localRotation = Quaternion.identity;
            marker.SetSiblingIndex(textRect.GetSiblingIndex());
            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void AddSoftFrame(string targetName, float width, float alpha)
        {
            RectTransform target = FindRect(transform, targetName);
            if (target == null || target.Find("SoftFrame") != null)
            {
                return;
            }

            GameObject root = new GameObject("SoftFrame", typeof(RectTransform));
            root.transform.SetParent(target, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            rect.SetAsLastSibling();

            float halfWidth = target.sizeDelta.x * 0.5f - 5f;
            float halfHeight = target.sizeDelta.y * 0.5f - 5f;
            Color color = new Color(0.18f, 0.12f, 0.07f, alpha);
            CreateLine(root.transform, new Vector2(-halfWidth, halfHeight), new Vector2(halfWidth - 2f, halfHeight + 1f), width, color);
            CreateLine(root.transform, new Vector2(halfWidth, halfHeight - 2f), new Vector2(halfWidth + 1f, -halfHeight + 2f), width, color);
            CreateLine(root.transform, new Vector2(halfWidth - 3f, -halfHeight), new Vector2(-halfWidth + 2f, -halfHeight - 1f), width, color);
            CreateLine(root.transform, new Vector2(-halfWidth, -halfHeight + 3f), new Vector2(-halfWidth - 1f, halfHeight - 3f), width, color);
        }

        private static RectTransform FindRect(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root as RectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform found = FindRect(root.GetChild(i), targetName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void CreateLine(Transform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            GameObject line = new GameObject("SoftFrameLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            Image image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
