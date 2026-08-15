using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class StageEditorVisualPolisher : MonoBehaviour
    {
        private static readonly Color PaperBlue = new Color(0.93f, 0.975f, 1f, 0.98f);
        private static readonly Color PaperYellow = new Color(1f, 0.975f, 0.86f, 0.98f);
        private static readonly Color Ink = new Color(0.08f, 0.07f, 0.055f, 0.96f);

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Polish;
            Polish();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Polish;
        }

        public void Polish()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
            {
                return;
            }

            RectTransform tools = FindRect(root, "RuntimeStageEditorTools");
            RectTransform list = FindRect(root, "RuntimeStageEditorListPanel");
            LayoutSidePanels(root, tools, list);
            LayoutTopBar(root);
            LayoutToolPanel(root, tools);
            LayoutObjectList(list);
            LayoutHelpBar(root);

            RuntimeStageEditor editor = root.GetComponentInParent<RuntimeStageEditor>();
            if (editor == null)
            {
                editor = Object.FindObjectOfType<RuntimeStageEditor>();
            }
            EnsureBackgroundColorButton(root, editor);
            RefreshState(editor);
        }

        public void RefreshState(RuntimeStageEditor editor)
        {
            if (editor == null)
            {
                return;
            }

            StageEditorPaletteButton[] quickButtons = GetComponentsInChildren<StageEditorPaletteButton>(true);
            for (int i = 0; i < quickButtons.Length; i++)
            {
                quickButtons[i].SetSelected(quickButtons[i].Type == editor.CurrentAddType);
            }

            RectTransform snap = FindRect(transform, "RuntimeEditSnapButton");
            if (snap != null)
            {
                StyleButton(snap, editor.SnapEnabled ? new Color(0.62f, 0.94f, 0.58f, 1f) : new Color(0.9f, 0.9f, 0.86f, 1f), 14);
                Text label = snap.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = LocalizationManager.T("stage_editor_snap_attach") + (editor.SnapEnabled ? "  ON" : "  OFF");
                }
            }

            RectTransform copyDirection = FindRect(transform, "RuntimeEditDuplicateDirectionButton");
            if (copyDirection != null)
            {
                Text label = copyDirection.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = editor.CopyDirectionLabel;
                }
            }


            RectTransform freehand = FindRect(transform, "RuntimeEditFreehandButton");
            if (freehand != null)
            {
                StyleButton(freehand, editor.TerrainFreehandEnabled
                    ? new Color(0.62f, 0.94f, 0.58f, 1f)
                    : new Color(0.68f, 0.88f, 1f, 1f), 13);
                Text label = freehand.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = editor.TerrainFreehandEnabled
                        ? LocalizationManager.T("stage_editor_draw_mode") + "  ON"
                        : LocalizationManager.T("stage_editor_select_mode") + "  ON";
                }
            }

            RectTransform straight = FindRect(transform, "RuntimeEditStraightButton");
            if (straight != null)
            {
                bool straightAvailable = editor.TerrainFreehandEnabled;
                StyleButton(straight, straightAvailable && editor.TerrainStraightLineEnabled
                    ? new Color(0.68f, 0.88f, 1f, 1f)
                    : new Color(0.96f, 0.95f, 0.9f, 1f), 13);
                Button straightButton = straight.GetComponent<Button>();
                if (straightButton != null)
                {
                    straightButton.interactable = straightAvailable;
                }
                Text label = straight.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = (editor.TerrainStraightLineEnabled ? "✓  " : "□  ") + LocalizationManager.T("stage_editor_straight");
                }
            }

            RectTransform separate = FindRect(transform, "RuntimeEditSeparateButton");
            if (separate != null)
            {
                StyleButton(separate, editor.TerrainKeepSeparate
                    ? new Color(1f, 0.86f, 0.46f, 1f)
                    : new Color(0.96f, 0.95f, 0.9f, 1f), 12);
                Text label = separate.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = (editor.TerrainKeepSeparate ? "✓  " : "□  ") + LocalizationManager.T("stage_editor_separate");
                }
            }

            Text thicknessValue = FindText(transform, "RuntimeEditThicknessValue");
            if (thicknessValue != null)
            {
                thicknessValue.text = editor.TerrainPathThickness.ToString("0.00");
            }

            StageBackgroundColorButton backgroundColorButton = GetComponentInChildren<StageBackgroundColorButton>(true);
            backgroundColorButton?.RefreshColor(editor.StageBackgroundColor);
            Slider thicknessSlider = FindRect(transform, "RuntimeEditThicknessSlider")?.GetComponent<Slider>();
            thicknessSlider?.SetValueWithoutNotify(editor.TerrainPathThickness);

            RefreshButtonLabel(transform, "RuntimeStageRuleModeButton", editor.StageRuleModeLabel);
            RefreshButtonLabel(transform, "RuntimeStageRuleTargetButton", editor.StageCollectionTargetLabel);
            RefreshButtonLabel(transform, "RuntimeStageRuleTimeButton", editor.StageTimeLimitLabel);
            RefreshButtonLabel(transform, "RuntimeStageRuleCountButton", editor.StageRequiredCountLabel);
            bool timedRule = editor.IsTimedCollectionRule;
            SetActive(transform, "RuntimeStageRuleTargetButton", timedRule);
            SetActive(transform, "RuntimeStageRuleTimeButton", timedRule || editor.IsSurvivalRule || editor.IsBlockBreakerRule);
            SetActive(transform, "RuntimeStageRuleCountButton", timedRule);

            bool multipleSelection = editor.HasMultipleSelection;
            bool showActionStrength = !multipleSelection && editor.SelectedSupportsActionStrength;
            bool showWeightThreshold = !multipleSelection && editor.SelectedSupportsWeightThreshold;
            bool showBoxSize = !multipleSelection && editor.SelectedIsDropper;
            bool showSizeControls = !multipleSelection && !showWeightThreshold && !showBoxSize;
            SetActive(transform, "RuntimeStageEditorSizeLabel", !showActionStrength && showSizeControls);
            SetActive(transform, "RuntimeEditWidthMinus", showSizeControls);
            SetActive(transform, "RuntimeEditWidthPlus", showSizeControls);
            SetActive(transform, "RuntimeEditHeightMinus", showSizeControls);
            SetActive(transform, "RuntimeEditHeightPlus", showSizeControls);
            SetActive(transform, "RuntimeEditActionStrengthLabel", showActionStrength);
            SetActive(transform, "RuntimeEditActionStrengthSlider", showActionStrength);
            SetActive(transform, "RuntimeEditActionStrengthValue", showActionStrength);
            SetActive(transform, "RuntimeEditMovementSpeedLabel", !multipleSelection && editor.SelectedSupportsSecondarySlider);
            SetActive(transform, "RuntimeEditMovementSpeedSlider", !multipleSelection && editor.SelectedSupportsSecondarySlider);
            SetActive(transform, "RuntimeEditMovementSpeedValue", !multipleSelection && editor.SelectedSupportsSecondarySlider);
            SetActive(transform, "RuntimeEditWeightThresholdLabel", showWeightThreshold);
            SetActive(transform, "RuntimeEditWeightThresholdInput", showWeightThreshold);
            SetActive(transform, "RuntimeEditWeightThresholdVisibleValue", showWeightThreshold);
            SetActive(transform, "RuntimeEditDropperBoxSizeLabel", showBoxSize);
            SetActive(transform, "RuntimeEditDropperBoxSizeSlider", showBoxSize);
            SetActive(transform, "RuntimeEditDropperBoxSizeValue", showBoxSize);
            SetActive(transform, "RuntimeEditConveyorDirectionButton", !multipleSelection && editor.SelectedIsConveyor);
            SetActive(transform, "RuntimeEditBoxPatternButton", !multipleSelection && editor.SelectedUsesDropperPattern);

            RectTransform conveyorDirection = FindRect(transform, "RuntimeEditConveyorDirectionButton");
            if (conveyorDirection != null)
            {
                Text label = conveyorDirection.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = editor.SelectedConveyorDirectionLabel;
                }
            }
            RectTransform boxPattern = FindRect(transform, "RuntimeEditBoxPatternButton");
            if (boxPattern != null)
            {
                Text label = boxPattern.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = editor.SelectedDropperPatternLabel;
                }
            }

            Text actionLabel = FindText(transform, "RuntimeEditActionStrengthLabel");
            if (actionLabel != null)
            {
                actionLabel.text = editor.SelectedActionStrengthLabel;
            }
            Text actionValue = FindText(transform, "RuntimeEditActionStrengthValue");
            if (actionValue != null)
            {
                actionValue.text = editor.SelectedActionStrength.ToString("0.0");
            }
            Slider actionSlider = FindRect(transform, "RuntimeEditActionStrengthSlider")?.GetComponent<Slider>();
            if (actionSlider != null)
            {
                StageEditorActionStrengthSlider actionCommand = actionSlider.GetComponent<StageEditorActionStrengthSlider>();
                if (actionCommand != null)
                {
                    actionCommand.SetRangeAndValueWithoutNotify(
                        editor.SelectedActionStrengthMinimum,
                        editor.SelectedActionStrengthMaximum,
                        editor.SelectedActionStrength);
                }
                else
                {
                    actionSlider.minValue = editor.SelectedActionStrengthMinimum;
                    actionSlider.maxValue = editor.SelectedActionStrengthMaximum;
                    actionSlider.SetValueWithoutNotify(editor.SelectedActionStrength);
                }
            }
            Text movementSpeedLabel = FindText(transform, "RuntimeEditMovementSpeedLabel");
            if (movementSpeedLabel != null)
            {
                movementSpeedLabel.text = editor.SelectedSecondarySliderLabel;
            }
            Text movementSpeedValue = FindText(transform, "RuntimeEditMovementSpeedValue");
            if (movementSpeedValue != null)
            {
                movementSpeedValue.text = editor.SelectedMovementSpeed.ToString("0.0");
            }
            Slider movementSpeedSlider = FindRect(transform, "RuntimeEditMovementSpeedSlider")?.GetComponent<Slider>();
            if (movementSpeedSlider != null)
            {
                movementSpeedSlider.minValue = editor.SelectedSecondarySliderMinimum;
                movementSpeedSlider.maxValue = editor.SelectedSecondarySliderMaximum;
                movementSpeedSlider.SetValueWithoutNotify(editor.SelectedMovementSpeed);
            }
            Text boxSizeValue = FindText(transform, "RuntimeEditDropperBoxSizeValue");
            if (boxSizeValue != null)
            {
                boxSizeValue.text = editor.SelectedDropperBoxSize.ToString("0.0");
            }
            Text boxSizeLabel = FindText(transform, "RuntimeEditDropperBoxSizeLabel");
            if (boxSizeLabel != null)
            {
                boxSizeLabel.text = editor.SelectedDropperSizeLabel;
            }
            Slider boxSizeSlider = FindRect(transform, "RuntimeEditDropperBoxSizeSlider")?.GetComponent<Slider>();
            boxSizeSlider?.SetValueWithoutNotify(editor.SelectedDropperBoxSize);
            InputField thresholdInput = FindRect(transform, "RuntimeEditWeightThresholdInput")?.GetComponent<InputField>();
            string thresholdValue = editor.SelectedWeightThreshold.ToString("0");
            thresholdInput?.SetTextWithoutNotify(thresholdValue);
            thresholdInput?.GetComponent<StageEditorWeightThresholdInput>()?.RefreshVisibleText(thresholdValue);
            Text thresholdVisibleValue = FindText(transform, "RuntimeEditWeightThresholdVisibleValue");
            if (thresholdVisibleValue != null)
            {
                thresholdVisibleValue.text = thresholdValue;
            }

            RectTransform linkAction = FindRect(transform, "RuntimeEditLinkActionButton");
            if (linkAction != null)
            {
                Text label = linkAction.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = editor.SelectedLinkActionLabel;
                }
            }
        }

        private static void LayoutSidePanels(RectTransform root, RectTransform tools, RectTransform list)
        {
            float rootHeight = Mathf.Max(360f, root.rect.height);
            if (tools != null)
            {
                float scale = Mathf.Clamp((rootHeight - 98f) / 620f, 0.64f, 1f);
                PlaceTopLeft(tools, new Vector2(24f, -76f), new Vector2(330f, 620f));
                tools.localScale = Vector3.one * scale;
                SetPanelStyle(tools, PaperYellow, 2f);
            }

            if (list != null)
            {
                float scale = Mathf.Clamp((rootHeight - 98f) / 500f, 0.72f, 1f);
                PlaceTopRight(list, new Vector2(-24f, -76f), new Vector2(290f, 500f));
                list.localScale = Vector3.one * scale;
                SetPanelStyle(list, PaperBlue, 2f);
            }
        }

        private static void LayoutTopBar(RectTransform root)
        {
            Text title = FindText(root, "RuntimeStageEditorTitle");
            if (title != null)
            {
                PlaceTopLeft(title.rectTransform, new Vector2(24f, -14f), new Vector2(300f, 48f));
                title.alignment = TextAnchor.MiddleLeft;
                title.fontSize = 32;
                title.fontStyle = FontStyle.Bold;
                title.color = Ink;
            }

            RectTransform statusCard = EnsureCard(root, "RuntimeStageEditorStatusCard", new Color(1f, 0.99f, 0.94f, 0.96f));
            statusCard.anchorMin = new Vector2(0.5f, 1f);
            statusCard.anchorMax = new Vector2(0.5f, 1f);
            statusCard.pivot = new Vector2(0.5f, 1f);
            statusCard.anchoredPosition = new Vector2(-46f, -14f);
            statusCard.sizeDelta = new Vector2(420f, 48f);

            Text status = FindText(root, "RuntimeStageEditorStatus");
            if (status != null)
            {
                status.transform.SetParent(statusCard, false);
                Stretch(status.rectTransform, new Vector2(14f, 5f));
                status.alignment = TextAnchor.MiddleCenter;
                status.fontSize = 15;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 12;
                status.resizeTextMaxSize = 15;
                status.color = Ink;
            }

            LayoutTopAction(root, "RuntimeEditSaveButton", -302f, new Color(0.68f, 0.88f, 1f, 1f), "F5  " + LocalizationManager.T("stage_editor_save"));
            LayoutTopAction(root, "RuntimeEditTestButton", -166f, new Color(0.62f, 0.94f, 0.58f, 1f), "F6  " + LocalizationManager.T("stage_editor_test"));
            LayoutTopAction(root, "RuntimeEditCloseButton", -24f, new Color(1f, 0.72f, 0.62f, 1f), "ESC  " + LocalizationManager.T("stage_editor_back"));
        }

        private static void LayoutTopAction(RectTransform root, string name, float x, Color color, string labelValue)
        {
            RectTransform rect = FindRect(root, name);
            if (rect == null)
            {
                return;
            }

            rect.SetParent(root, false);
            PlaceTopRight(rect, new Vector2(x, -14f), new Vector2(name.Contains("Close") ? 132f : 124f, 46f));
            StyleButton(rect, color, 17);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = labelValue;
            }
        }

        private static void EnsureBackgroundColorButton(RectTransform root, RuntimeStageEditor editor)
        {
            const string name = "RuntimeStageBackgroundColorButton";
            RectTransform rect = FindRect(root, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(root, false);
                rect = obj.GetComponent<RectTransform>();

                GameObject swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                swatchObject.transform.SetParent(obj.transform, false);
                RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
                swatchRect.anchorMin = new Vector2(0f, 0.5f);
                swatchRect.anchorMax = new Vector2(0f, 0.5f);
                swatchRect.pivot = new Vector2(0f, 0.5f);
                swatchRect.anchoredPosition = new Vector2(8f, 0f);
                swatchRect.sizeDelta = new Vector2(22f, 22f);

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(root);
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Ink;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(32f, 3f);
                labelRect.offsetMax = new Vector2(-4f, -3f);

                obj.AddComponent<StageBackgroundColorButton>();
            }

            PlaceTopLeft(rect, new Vector2(190f, -14f), new Vector2(92f, 46f));
            StyleButton(rect, new Color(0.93f, 0.975f, 1f, 1f), 13);
            Text buttonLabel = rect.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = LocalizationManager.T("stage_editor_background_color");
            }

            StageBackgroundColorButton command = rect.GetComponent<StageBackgroundColorButton>();
            if (command == null)
            {
                command = rect.gameObject.AddComponent<StageBackgroundColorButton>();
            }
            command.Configure(editor, ResolveFont(root));
        }

        private static void LayoutToolPanel(RectTransform root, RectTransform tools)
        {
            if (tools == null)
            {
                return;
            }

            Text quickTitle = EnsureText(tools, "RuntimeStageEditorQuickTitle", LocalizationManager.T("stage_editor_quick"), 19, TextAnchor.MiddleLeft);
            quickTitle.gameObject.SetActive(false);

            RuntimeStageEditor editor = root.GetComponentInParent<RuntimeStageEditor>();
            if (editor == null)
            {
                editor = Object.FindObjectOfType<RuntimeStageEditor>();
            }

            RectTransform freehand = EnsureFreehandButton(tools, editor);
            PlaceTopLeft(freehand, new Vector2(18f, -10f), new Vector2(94f, 30f));
            RectTransform straight = EnsureStraightButton(tools, editor);
            PlaceTopLeft(straight, new Vector2(118f, -10f), new Vector2(62f, 30f));
            RectTransform separate = EnsureSeparateButton(tools, editor);
            PlaceTopLeft(separate, new Vector2(186f, -10f), new Vector2(62f, 30f));
            RectTransform duplicateDirection = EnsureDuplicateDirectionButton(tools, editor);
            RectTransform duplicate = EnsureDuplicateButton(tools, editor);
            RectTransform linkAction = EnsureLinkActionButton(tools, editor);
            RectTransform conveyorDirection = EnsureConveyorDirectionButton(tools, editor);
            RectTransform boxPattern = EnsureBoxPatternButton(tools, editor);
            RectTransform ruleMode = EnsureStageRuleButton(root, editor, "RuntimeStageRuleModeButton", 0);
            RectTransform ruleTarget = EnsureStageRuleButton(root, editor, "RuntimeStageRuleTargetButton", 1);
            RectTransform ruleTime = EnsureStageRuleButton(root, editor, "RuntimeStageRuleTimeButton", 2);
            RectTransform ruleCount = EnsureStageRuleButton(root, editor, "RuntimeStageRuleCountButton", 3);
            RectTransform snap = FindRect(tools, "RuntimeEditSnapButton");
            if (snap != null)
            {
                PlaceTopLeft(snap, new Vector2(254f, -10f), new Vector2(68f, 30f));
            }

            Text thicknessLabel = EnsureText(tools, "RuntimeEditThicknessLabel", LocalizationManager.T("stage_editor_thickness"), 14, TextAnchor.MiddleLeft);
            PlaceTopLeft(thicknessLabel.rectTransform, new Vector2(18f, -46f), new Vector2(82f, 30f));
            thicknessLabel.fontStyle = FontStyle.Bold;
            RectTransform oldMinus = FindRect(tools, "RuntimeEditThicknessMinus");
            if (oldMinus != null) oldMinus.gameObject.SetActive(false);
            RectTransform oldPlus = FindRect(tools, "RuntimeEditThicknessPlus");
            if (oldPlus != null) oldPlus.gameObject.SetActive(false);
            RectTransform thicknessSlider = EnsureThicknessSlider(tools, editor);
            PlaceTopLeft(thicknessSlider, new Vector2(104f, -48f), new Vector2(154f, 24f));
            Text thicknessValue = EnsureText(tools, "RuntimeEditThicknessValue", editor != null ? editor.TerrainPathThickness.ToString("0.00") : "0.50", 15, TextAnchor.MiddleCenter);
            PlaceTopLeft(thicknessValue.rectTransform, new Vector2(264f, -44f), new Vector2(48f, 30f));

            StageObjectType[] quickTypes =
            {
                StageObjectType.Platform,
                StageObjectType.Wall,
                StageObjectType.Spawn,
                StageObjectType.Goal,
                StageObjectType.StageBoundary,
                StageObjectType.WoodBox
            };
            Color[] quickColors =
            {
                new Color(0.84f, 0.92f, 1f, 1f),
                new Color(0.9f, 0.88f, 1f, 1f),
                new Color(0.72f, 0.94f, 0.72f, 1f),
                new Color(1f, 0.86f, 0.38f, 1f),
                new Color(0.94f, 0.82f, 0.66f, 1f),
                new Color(1f, 0.9f, 0.46f, 1f)
            };
            for (int i = 0; i < quickTypes.Length; i++)
            {
                int row = i / 3;
                int column = i % 3;
                RectTransform button = EnsureQuickButton(tools, editor, quickTypes[i], quickColors[i]);
                PlaceTopLeft(button, new Vector2(18f + column * 98f, -82f - row * 48f), new Vector2(90f, 40f));
            }
            PlaceTopLeft(ruleMode, new Vector2(372f, -78f), new Vector2(116f, 34f));
            PlaceTopLeft(ruleTarget, new Vector2(494f, -78f), new Vector2(84f, 34f));
            PlaceTopLeft(ruleTime, new Vector2(584f, -78f), new Vector2(70f, 34f));
            PlaceTopLeft(ruleCount, new Vector2(660f, -78f), new Vector2(56f, 34f));
            StyleButton(ruleMode, new Color(0.74f, 0.9f, 1f, 1f), 12);
            StyleButton(ruleTarget, new Color(0.72f, 0.94f, 0.78f, 1f), 11);
            StyleButton(ruleTime, new Color(1f, 0.88f, 0.54f, 1f), 11);
            StyleButton(ruleCount, new Color(1f, 0.78f, 0.68f, 1f), 11);
            RectTransform obsoleteDoorButton = FindRect(tools, "RuntimeStageQuickDoor");
            if (obsoleteDoorButton != null)
            {
                obsoleteDoorButton.gameObject.SetActive(false);
            }

            LayoutTopField(tools, "RuntimeStageEditorCategoryLabel", "RuntimeStageCategoryDropdown", -184f);
            LayoutTopField(tools, "RuntimeStageEditorSearchLabel", "RuntimeStageSearchInput", -252f);
            LayoutTopField(tools, "RuntimeStageEditorTypeLabel", "RuntimeStageObjectTypeDropdown", -320f);

            Text selectionTitle = EnsureText(tools, "RuntimeStageEditorSelectionTitle", LocalizationManager.T("stage_editor_selection"), 18, TextAnchor.MiddleLeft);
            PlaceTopLeft(selectionTitle.rectTransform, new Vector2(18f, -378f), new Vector2(294f, 26f));
            selectionTitle.fontStyle = FontStyle.Bold;
            PlaceTopLeft(conveyorDirection, new Vector2(190f, -376f), new Vector2(122f, 28f));
            StyleButton(conveyorDirection, new Color(0.68f, 0.88f, 1f, 1f), 12);
            PlaceTopLeft(boxPattern, new Vector2(190f, -376f), new Vector2(122f, 28f));
            StyleButton(boxPattern, new Color(1f, 0.86f, 0.46f, 1f), 12);

            Text movementSpeedLabel = EnsureText(tools, "RuntimeEditMovementSpeedLabel", editor != null ? editor.SelectedSecondarySliderLabel : LocalizationManager.T("stage_editor_move_speed"), 12, TextAnchor.MiddleLeft);
            PlaceTopLeft(movementSpeedLabel.rectTransform, new Vector2(158f, -376f), new Vector2(66f, 28f));
            movementSpeedLabel.fontStyle = FontStyle.Bold;
            RectTransform movementSpeedSlider = EnsureMovementSpeedSlider(tools, editor);
            PlaceTopLeft(movementSpeedSlider, new Vector2(222f, -379f), new Vector2(54f, 22f));
            Text movementSpeedValue = EnsureText(tools, "RuntimeEditMovementSpeedValue", editor != null ? editor.SelectedMovementSpeed.ToString("0.0") : "3.2", 13, TextAnchor.MiddleRight);
            PlaceTopLeft(movementSpeedValue.rectTransform, new Vector2(278f, -376f), new Vector2(34f, 28f));

            Text selected = FindText(tools, "RuntimeStageEditorSelected");
            if (selected != null)
            {
                PlaceTopLeft(selected.rectTransform, new Vector2(18f, -408f), new Vector2(294f, 46f));
                selected.alignment = TextAnchor.UpperLeft;
                selected.fontSize = 14;
                selected.resizeTextForBestFit = true;
                selected.resizeTextMinSize = 12;
                selected.resizeTextMaxSize = 14;
            }

            Text sizeLabel = FindText(tools, "RuntimeStageEditorSizeLabel");
            if (sizeLabel != null)
            {
                PlaceTopLeft(sizeLabel.rectTransform, new Vector2(18f, -458f), new Vector2(294f, 24f));
                sizeLabel.fontStyle = FontStyle.Bold;
            }

            LayoutToolButton(tools, "RuntimeEditWidthMinus", 18f, -486f, 68f, new Color(0.96f, 0.95f, 0.9f, 1f));
            LayoutToolButton(tools, "RuntimeEditWidthPlus", 92f, -486f, 68f, new Color(0.96f, 0.95f, 0.9f, 1f));
            LayoutToolButton(tools, "RuntimeEditHeightMinus", 166f, -486f, 68f, new Color(0.96f, 0.95f, 0.9f, 1f));
            LayoutToolButton(tools, "RuntimeEditHeightPlus", 240f, -486f, 68f, new Color(0.96f, 0.95f, 0.9f, 1f));

            Text actionStrengthLabel = EnsureText(tools, "RuntimeEditActionStrengthLabel", LocalizationManager.T("stage_editor_action_strength"), 14, TextAnchor.MiddleLeft);
            PlaceTopLeft(actionStrengthLabel.rectTransform, new Vector2(18f, -458f), new Vector2(82f, 30f));
            actionStrengthLabel.fontStyle = FontStyle.Bold;
            RectTransform actionStrengthSlider = EnsureActionStrengthSlider(tools, editor);
            PlaceTopLeft(actionStrengthSlider, new Vector2(104f, -460f), new Vector2(154f, 24f));
            Text actionStrengthValue = EnsureText(tools, "RuntimeEditActionStrengthValue", editor != null ? editor.SelectedActionStrength.ToString("0.0") : "27.0", 15, TextAnchor.MiddleCenter);
            PlaceTopLeft(actionStrengthValue.rectTransform, new Vector2(264f, -456f), new Vector2(48f, 30f));

            Text boxSizeLabel = EnsureText(tools, "RuntimeEditDropperBoxSizeLabel", LocalizationManager.T("stage_editor_box_size"), 14, TextAnchor.MiddleLeft);
            PlaceTopLeft(boxSizeLabel.rectTransform, new Vector2(18f, -486f), new Vector2(82f, 30f));
            boxSizeLabel.fontStyle = FontStyle.Bold;
            RectTransform boxSizeSlider = EnsureDropperBoxSizeSlider(tools, editor);
            PlaceTopLeft(boxSizeSlider, new Vector2(104f, -488f), new Vector2(154f, 24f));
            Text boxSizeValue = EnsureText(tools, "RuntimeEditDropperBoxSizeValue", editor != null ? editor.SelectedDropperBoxSize.ToString("0.0") : "0.9", 15, TextAnchor.MiddleCenter);
            PlaceTopLeft(boxSizeValue.rectTransform, new Vector2(264f, -484f), new Vector2(48f, 30f));

            Text weightThresholdLabel = EnsureText(tools, "RuntimeEditWeightThresholdLabel", LocalizationManager.T("stage_editor_weight_threshold"), 14, TextAnchor.MiddleLeft);
            PlaceTopLeft(weightThresholdLabel.rectTransform, new Vector2(18f, -458f), new Vector2(82f, 30f));
            weightThresholdLabel.fontStyle = FontStyle.Bold;
            RectTransform weightThresholdInput = EnsureWeightThresholdInput(tools, editor);
            PlaceTopLeft(weightThresholdInput, new Vector2(104f, -458f), new Vector2(208f, 32f));
            Text weightThresholdVisibleValue = EnsureText(
                tools,
                "RuntimeEditWeightThresholdVisibleValue",
                editor != null ? editor.SelectedWeightThreshold.ToString("0") : "300",
                18,
                TextAnchor.MiddleRight);
            PlaceTopLeft(weightThresholdVisibleValue.rectTransform, new Vector2(114f, -458f), new Vector2(188f, 32f));
            weightThresholdVisibleValue.fontStyle = FontStyle.Bold;
            weightThresholdVisibleValue.raycastTarget = false;
            weightThresholdVisibleValue.transform.SetAsLastSibling();
            weightThresholdInput.GetComponent<StageEditorWeightThresholdInput>()?.SetOverlay(weightThresholdVisibleValue);

            LayoutToolButton(tools, "RuntimeEditUndoButton", 18f, -528f, 56f, new Color(0.9f, 0.94f, 1f, 1f));
            LayoutToolButton(tools, "RuntimeEditRedoButton", 78f, -528f, 56f, new Color(0.9f, 0.94f, 1f, 1f));
            PlaceTopLeft(duplicateDirection, new Vector2(138f, -528f), new Vector2(54f, 36f));
            StyleButton(duplicateDirection, new Color(1f, 0.9f, 0.52f, 1f), 13);
            PlaceTopLeft(duplicate, new Vector2(196f, -528f), new Vector2(54f, 36f));
            StyleButton(duplicate, new Color(0.82f, 0.96f, 0.82f, 1f), 11);
            LayoutToolButton(tools, "RuntimeEditDeleteButton", 254f, -528f, 54f, new Color(1f, 0.72f, 0.64f, 1f));

            LayoutToolButton(tools, "RuntimeEditLinkSourceButton", 18f, -576f, 68f, new Color(0.84f, 0.92f, 1f, 1f));
            LayoutToolButton(tools, "RuntimeEditLinkTargetButton", 92f, -576f, 68f, new Color(0.82f, 0.96f, 0.82f, 1f));
            PlaceTopLeft(linkAction, new Vector2(166f, -576f), new Vector2(68f, 36f));
            StyleButton(linkAction, new Color(1f, 0.9f, 0.52f, 1f), 12);
            LayoutToolButton(tools, "RuntimeEditClearLinkButton", 240f, -576f, 68f, new Color(1f, 0.84f, 0.7f, 1f));
        }

        private static void LayoutTopField(RectTransform parent, string labelName, string fieldName, float y)
        {
            Text label = FindText(parent, labelName);
            if (label != null)
            {
                PlaceTopLeft(label.rectTransform, new Vector2(18f, y), new Vector2(72f, 38f));
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 14;
                label.fontStyle = FontStyle.Bold;
            }

            RectTransform field = FindRect(parent, fieldName);
            if (field != null)
            {
                PlaceTopLeft(field, new Vector2(92f, y), new Vector2(220f, 40f));
                SetPanelStyle(field, Color.white, 1.5f);
            }
        }

        private static void LayoutToolButton(RectTransform parent, string name, float x, float y, float width, Color color)
        {
            RectTransform button = FindRect(parent, name);
            if (button == null)
            {
                return;
            }

            PlaceTopLeft(button, new Vector2(x, y), new Vector2(width, 36f));
            StyleButton(button, color, 14);
        }

        private static void LayoutObjectList(RectTransform list)
        {
            if (list == null)
            {
                return;
            }

            Text title = FindText(list, "RuntimeStageEditorListTitle");
            if (title != null)
            {
                PlaceTopLeft(title.rectTransform, new Vector2(14f, -12f), new Vector2(262f, 30f));
                title.alignment = TextAnchor.MiddleLeft;
                title.fontSize = 20;
                title.fontStyle = FontStyle.Bold;
            }

            LayoutListButton(list, "RuntimeStageEditorObjectsTab", 14f, -50f, 126f, new Color(0.72f, 0.88f, 1f, 1f));
            LayoutListButton(list, "RuntimeStageEditorLinksTab", 150f, -50f, 126f, new Color(0.72f, 0.94f, 0.72f, 1f));

            for (int i = 0; i < 5; i++)
            {
                RectTransform item = FindRect(list, $"RuntimeStageEditorListItem{i}");
                if (item == null)
                {
                    continue;
                }

                PlaceTopLeft(item, new Vector2(14f, -98f - i * 58f), new Vector2(262f, 50f));
                StyleButton(item, i % 2 == 0 ? Color.white : new Color(0.95f, 0.98f, 1f, 1f), 15);
                Text label = item.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleLeft;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 11;
                    label.resizeTextMaxSize = 14;
                    label.rectTransform.offsetMin = new Vector2(12f, 4f);
                    label.rectTransform.offsetMax = new Vector2(-8f, -4f);
                }
            }

            LayoutListButton(list, "RuntimeStageEditorListPrev", 38f, -424f, 50f, new Color(1f, 0.93f, 0.68f, 1f));
            LayoutListButton(list, "RuntimeStageEditorListNext", 202f, -424f, 50f, new Color(1f, 0.93f, 0.68f, 1f));
            Text page = FindText(list, "RuntimeStageEditorListPage");
            if (page != null)
            {
                PlaceTopLeft(page.rectTransform, new Vector2(96f, -424f), new Vector2(98f, 36f));
                page.alignment = TextAnchor.MiddleCenter;
                page.fontStyle = FontStyle.Bold;
            }
        }

        private static void LayoutListButton(RectTransform parent, string name, float x, float y, float width, Color color)
        {
            RectTransform button = FindRect(parent, name);
            if (button == null)
            {
                return;
            }

            PlaceTopLeft(button, new Vector2(x, y), new Vector2(width, 38f));
            StyleButton(button, color, 15);
        }

        private static void LayoutHelpBar(RectTransform root)
        {
            Text help = FindText(root, "RuntimeStageEditorHelp");
            if (help == null)
            {
                return;
            }

            RectTransform bar = EnsureCard(root, "RuntimeStageEditorHelpBar", new Color(1f, 0.99f, 0.92f, 0.94f));
            bar.anchorMin = new Vector2(0.5f, 0f);
            bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 18f);
            bar.sizeDelta = new Vector2(570f, 62f);
            help.transform.SetParent(bar, false);
            Stretch(help.rectTransform, new Vector2(14f, 7f));
            help.alignment = TextAnchor.MiddleCenter;
            help.fontSize = 14;
            help.resizeTextForBestFit = true;
            help.resizeTextMinSize = 11;
            help.resizeTextMaxSize = 14;
            help.color = Ink;
        }

        private static RectTransform EnsureQuickButton(Transform parent, RuntimeStageEditor editor, StageObjectType type, Color color)
        {
            string name = "RuntimeStageQuick" + type;
            RectTransform rect = FindRect(parent, name);
            Button button;
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                button = obj.GetComponent<Button>();

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                Stretch(label.rectTransform, new Vector2(5f, 3f));

                StageEditorPaletteButton palette = obj.AddComponent<StageEditorPaletteButton>();
                palette.Configure(editor, type);
            }
            else
            {
                button = rect.GetComponent<Button>();
                StageEditorPaletteButton palette = rect.GetComponent<StageEditorPaletteButton>();
                if (palette != null)
                {
                    palette.Configure(editor, type);
                }
            }

            StyleButton(rect, color, 14);
            if (button != null)
            {
                button.targetGraphic = rect.GetComponent<Image>();
            }

            Text buttonLabel = rect.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = type == StageObjectType.StageBoundary
                    ? LocalizationManager.T("stage_editor_boundary_quick")
                    : StageObjectCatalog.Get(type).Label;
            }

            return rect;
        }

        private static RectTransform EnsureFreehandButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditFreehandButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                Stretch(label.rectTransform, new Vector2(5f, 3f));

                StageEditorFreehandButton command = obj.AddComponent<StageEditorFreehandButton>();
                command.Configure(editor);
            }
            else
            {
                StageEditorFreehandButton command = rect.GetComponent<StageEditorFreehandButton>();
                command?.Configure(editor);
            }

            return rect;
        }

        private static RectTransform EnsureStraightButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditStraightButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                Stretch(label.rectTransform, new Vector2(5f, 3f));

                StageEditorStraightButton command = obj.AddComponent<StageEditorStraightButton>();
                command.Configure(editor);
            }
            else
            {
                StageEditorStraightButton command = rect.GetComponent<StageEditorStraightButton>();
                command?.Configure(editor);
            }

            return rect;
        }

        private static RectTransform EnsureSeparateButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditSeparateButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                Stretch(label.rectTransform, new Vector2(4f, 2f));
                StageEditorSeparateButton command = obj.AddComponent<StageEditorSeparateButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorSeparateButton>()?.Configure(editor);
            }

            return rect;
        }

        private static RectTransform EnsureDuplicateDirectionButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditDuplicateDirectionButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.text = editor != null
                    ? editor.CopyDirectionLabel
                    : LocalizationManager.T("stage_editor_copy_right");
                Stretch(label.rectTransform, new Vector2(2f, 2f));
                StageEditorDuplicateDirectionButton command = obj.AddComponent<StageEditorDuplicateDirectionButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorDuplicateDirectionButton>()?.Configure(editor);
            }

            return rect;
        }

        private static RectTransform EnsureDuplicateButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditDuplicateButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.text = LocalizationManager.T("stage_editor_copy");
                Stretch(label.rectTransform, new Vector2(4f, 2f));
                StageEditorDuplicateButton command = obj.AddComponent<StageEditorDuplicateButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorDuplicateButton>()?.Configure(editor);
                Text label = rect.GetComponentInChildren<Text>(true);
                if (label != null) label.text = LocalizationManager.T("stage_editor_copy");
            }
            return rect;
        }

        private static RectTransform EnsureLinkActionButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditLinkActionButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.text = editor != null ? editor.SelectedLinkActionLabel : LocalizationManager.T("stage_editor_link_action");
                Stretch(label.rectTransform, new Vector2(4f, 2f));
                StageEditorLinkActionButton command = obj.AddComponent<StageEditorLinkActionButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorLinkActionButton>()?.Configure(editor);
            }

            return rect;
        }

        private static RectTransform EnsureConveyorDirectionButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditConveyorDirectionButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.alignment = TextAnchor.MiddleCenter;
                Stretch(label.rectTransform, new Vector2(4f, 2f));
                StageEditorConveyorDirectionButton command = obj.AddComponent<StageEditorConveyorDirectionButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorConveyorDirectionButton>()?.Configure(editor);
            }
            return rect;
        }

        private static RectTransform EnsureStageRuleButton(Transform parent, RuntimeStageEditor editor, string name, int commandId)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.alignment = TextAnchor.MiddleCenter;
                Stretch(label.rectTransform, new Vector2(3f, 2f));
                StageEditorRuleButton command = obj.AddComponent<StageEditorRuleButton>();
                command.Configure(editor, commandId);
            }
            else
            {
                rect.GetComponent<StageEditorRuleButton>()?.Configure(editor, commandId);
            }
            return rect;
        }

        private static void RefreshButtonLabel(Transform parent, string name, string value)
        {
            RectTransform rect = FindRect(parent, name);
            Text label = rect != null ? rect.GetComponentInChildren<Text>(true) : null;
            if (label != null)
            {
                label.text = value;
            }
        }

        private static RectTransform EnsureBoxPatternButton(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditBoxPatternButton";
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.alignment = TextAnchor.MiddleCenter;
                Stretch(label.rectTransform, new Vector2(4f, 2f));
                StageEditorBoxPatternButton command = obj.AddComponent<StageEditorBoxPatternButton>();
                command.Configure(editor);
            }
            else
            {
                rect.GetComponent<StageEditorBoxPatternButton>()?.Configure(editor);
            }
            return rect;
        }

        private static RectTransform EnsureThicknessButton(Transform parent, RuntimeStageEditor editor, string name, float delta, string labelValue)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();

                GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(obj.transform, false);
                Text label = labelObject.GetComponent<Text>();
                label.font = ResolveFont(parent);
                label.text = labelValue;
                Stretch(label.rectTransform, new Vector2(4f, 2f));

                StageEditorThicknessButton command = obj.AddComponent<StageEditorThicknessButton>();
                command.Configure(editor, delta);
            }
            else
            {
                StageEditorThicknessButton command = rect.GetComponent<StageEditorThicknessButton>();
                command?.Configure(editor, delta);
            }

            StyleButton(rect, new Color(0.9f, 0.94f, 1f, 1f), 18);
            return rect;
        }

        private static RectTransform EnsureThicknessSlider(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditThicknessSlider";
            RectTransform rect = FindRect(parent, name);
            Slider slider;
            if (rect == null)
            {
                GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
                root.transform.SetParent(parent, false);
                rect = root.GetComponent<RectTransform>();
                slider = root.GetComponent<Slider>();

                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(root.transform, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                Stretch(backgroundRect, new Vector2(0f, 7f));
                background.GetComponent<Image>().color = new Color(0.82f, 0.84f, 0.8f, 1f);

                GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(root.transform, false);
                RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
                Stretch(fillAreaRect, new Vector2(7f, 7f));
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                Stretch(fillRect, Vector2.zero);
                fill.GetComponent<Image>().color = new Color(0.24f, 0.67f, 0.92f, 1f);

                GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(root.transform, false);
                RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
                Stretch(handleAreaRect, new Vector2(8f, 1f));
                GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handle.transform.SetParent(handleArea.transform, false);
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(18f, 26f);
                Image handleImage = handle.GetComponent<Image>();
                handleImage.color = new Color(1f, 0.82f, 0.28f, 1f);
                Outline handleOutline = handle.AddComponent<Outline>();
                handleOutline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
                handleOutline.effectDistance = new Vector2(1.5f, -1.5f);

                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0.25f;
                slider.maxValue = 4f;
                slider.wholeNumbers = false;

                StageEditorThicknessSlider command = root.AddComponent<StageEditorThicknessSlider>();
                command.Configure(editor, slider);
            }
            else
            {
                slider = rect.GetComponent<Slider>();
                StageEditorThicknessSlider command = rect.GetComponent<StageEditorThicknessSlider>();
                command?.Configure(editor, slider);
            }

            slider?.SetValueWithoutNotify(editor != null ? editor.TerrainPathThickness : 0.5f);
            return rect;
        }

        private static RectTransform EnsureActionStrengthSlider(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditActionStrengthSlider";
            RectTransform rect = FindRect(parent, name);
            Slider slider;
            if (rect == null)
            {
                GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
                root.transform.SetParent(parent, false);
                rect = root.GetComponent<RectTransform>();
                slider = root.GetComponent<Slider>();

                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(root.transform, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                Stretch(backgroundRect, new Vector2(0f, 7f));
                background.GetComponent<Image>().color = new Color(0.82f, 0.84f, 0.8f, 1f);

                GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(root.transform, false);
                RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
                Stretch(fillAreaRect, new Vector2(7f, 7f));
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                Stretch(fillRect, Vector2.zero);
                fill.GetComponent<Image>().color = new Color(1f, 0.48f, 0.22f, 1f);

                GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(root.transform, false);
                RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
                Stretch(handleAreaRect, new Vector2(8f, 1f));
                GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handle.transform.SetParent(handleArea.transform, false);
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(18f, 26f);
                Image handleImage = handle.GetComponent<Image>();
                handleImage.color = new Color(1f, 0.82f, 0.28f, 1f);
                Outline handleOutline = handle.AddComponent<Outline>();
                handleOutline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
                handleOutline.effectDistance = new Vector2(1.5f, -1.5f);

                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 5f;
                slider.maxValue = 60f;
                slider.wholeNumbers = false;

                StageEditorActionStrengthSlider command = root.AddComponent<StageEditorActionStrengthSlider>();
                command.Configure(editor, slider);
            }
            else
            {
                slider = rect.GetComponent<Slider>();
                StageEditorActionStrengthSlider command = rect.GetComponent<StageEditorActionStrengthSlider>();
                command?.Configure(editor, slider);
            }

            slider?.SetValueWithoutNotify(editor != null ? editor.SelectedActionStrength : 27f);
            return rect;
        }

        private static RectTransform EnsureMovementSpeedSlider(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditMovementSpeedSlider";
            RectTransform rect = FindRect(parent, name);
            Slider slider;
            if (rect == null)
            {
                GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
                root.transform.SetParent(parent, false);
                rect = root.GetComponent<RectTransform>();
                slider = root.GetComponent<Slider>();

                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(root.transform, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                Stretch(backgroundRect, new Vector2(0f, 6f));
                background.GetComponent<Image>().color = new Color(0.78f, 0.84f, 0.86f, 1f);

                GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(root.transform, false);
                RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
                Stretch(fillAreaRect, new Vector2(5f, 6f));
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                Stretch(fillRect, Vector2.zero);
                fill.GetComponent<Image>().color = new Color(0.12f, 0.58f, 0.9f, 1f);

                GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(root.transform, false);
                RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
                Stretch(handleAreaRect, new Vector2(6f, 1f));
                GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handle.transform.SetParent(handleArea.transform, false);
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(14f, 24f);
                Image handleImage = handle.GetComponent<Image>();
                handleImage.color = new Color(0.38f, 0.8f, 1f, 1f);
                Outline handleOutline = handle.AddComponent<Outline>();
                handleOutline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
                handleOutline.effectDistance = new Vector2(1.2f, -1.2f);

                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0.5f;
                slider.maxValue = 10f;
                slider.wholeNumbers = false;

                StageEditorMovementSpeedSlider command = root.AddComponent<StageEditorMovementSpeedSlider>();
                command.Configure(editor, slider);
            }
            else
            {
                slider = rect.GetComponent<Slider>();
                StageEditorMovementSpeedSlider command = rect.GetComponent<StageEditorMovementSpeedSlider>();
                command?.Configure(editor, slider);
            }

            if (slider != null)
            {
                slider.minValue = editor != null ? editor.SelectedSecondarySliderMinimum : 0.5f;
                slider.maxValue = editor != null ? editor.SelectedSecondarySliderMaximum : 10f;
                slider.SetValueWithoutNotify(editor != null ? editor.SelectedMovementSpeed : 3.2f);
            }
            return rect;
        }

        private static RectTransform EnsureDropperBoxSizeSlider(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditDropperBoxSizeSlider";
            RectTransform rect = FindRect(parent, name);
            Slider slider;
            if (rect == null)
            {
                GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
                root.transform.SetParent(parent, false);
                rect = root.GetComponent<RectTransform>();
                slider = root.GetComponent<Slider>();

                GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                background.transform.SetParent(root.transform, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                Stretch(backgroundRect, new Vector2(0f, 7f));
                background.GetComponent<Image>().color = new Color(0.82f, 0.84f, 0.8f, 1f);

                GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
                fillArea.transform.SetParent(root.transform, false);
                RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
                Stretch(fillAreaRect, new Vector2(7f, 7f));
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fill.transform.SetParent(fillArea.transform, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                Stretch(fillRect, Vector2.zero);
                fill.GetComponent<Image>().color = new Color(0.38f, 0.72f, 0.28f, 1f);

                GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
                handleArea.transform.SetParent(root.transform, false);
                RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
                Stretch(handleAreaRect, new Vector2(8f, 1f));
                GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                handle.transform.SetParent(handleArea.transform, false);
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(18f, 26f);
                Image handleImage = handle.GetComponent<Image>();
                handleImage.color = new Color(1f, 0.82f, 0.28f, 1f);
                Outline handleOutline = handle.AddComponent<Outline>();
                handleOutline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
                handleOutline.effectDistance = new Vector2(1.5f, -1.5f);

                slider.fillRect = fillRect;
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = Slider.Direction.LeftToRight;
                slider.minValue = 0.5f;
                slider.maxValue = 2f;
                slider.wholeNumbers = false;

                StageEditorDropperBoxSizeSlider command = root.AddComponent<StageEditorDropperBoxSizeSlider>();
                command.Configure(editor, slider);
            }
            else
            {
                slider = rect.GetComponent<Slider>();
                rect.GetComponent<StageEditorDropperBoxSizeSlider>()?.Configure(editor, slider);
            }

            slider?.SetValueWithoutNotify(editor != null ? editor.SelectedDropperBoxSize : 0.9f);
            return rect;
        }

        private static RectTransform EnsureWeightThresholdInput(Transform parent, RuntimeStageEditor editor)
        {
            const string name = "RuntimeEditWeightThresholdInput";
            RectTransform rect = FindRect(parent, name);
            InputField input;
            if (rect == null)
            {
                GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
                root.transform.SetParent(parent, false);
                rect = root.GetComponent<RectTransform>();
                Image background = root.GetComponent<Image>();
                background.color = Color.white;

                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textObject.transform.SetParent(root.transform, false);
                RectTransform textRect = textObject.GetComponent<RectTransform>();
                Stretch(textRect, new Vector2(10f, 4f));
                Text text = textObject.GetComponent<Text>();
                text.font = ResolveFont(parent);
                text.fontSize = 17;
                text.alignment = TextAnchor.MiddleRight;
                text.color = Ink;
                text.supportRichText = false;

                GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                placeholderObject.transform.SetParent(root.transform, false);
                RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
                Stretch(placeholderRect, new Vector2(10f, 4f));
                Text placeholder = placeholderObject.GetComponent<Text>();
                placeholder.font = text.font;
                placeholder.fontSize = 14;
                placeholder.fontStyle = FontStyle.Italic;
                placeholder.alignment = TextAnchor.MiddleRight;
                placeholder.color = new Color(Ink.r, Ink.g, Ink.b, 0.4f);
                placeholder.text = "1 - 2000 INK";

                input = root.GetComponent<InputField>();
                input.textComponent = text;
                input.placeholder = placeholder;
                input.contentType = InputField.ContentType.IntegerNumber;
                input.lineType = InputField.LineType.SingleLine;
                input.characterLimit = 4;

                Outline outline = root.AddComponent<Outline>();
                outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.72f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                StageEditorWeightThresholdInput command = root.AddComponent<StageEditorWeightThresholdInput>();
                command.Configure(editor, input, text, placeholder);
            }
            else
            {
                input = rect.GetComponent<InputField>();
                Text text = FindRect(rect, "Text")?.GetComponent<Text>();
                Text placeholder = FindRect(rect, "Placeholder")?.GetComponent<Text>();
                rect.GetComponent<StageEditorWeightThresholdInput>()?.Configure(editor, input, text, placeholder);
            }

            string visibleValue = editor != null ? editor.SelectedWeightThreshold.ToString("0") : "300";
            input?.SetTextWithoutNotify(visibleValue);
            rect.GetComponent<StageEditorWeightThresholdInput>()?.RefreshVisibleText(visibleValue);
            return rect;
        }

        private static RectTransform EnsureCard(Transform parent, string name, Color color)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(parent, false);
                rect = obj.GetComponent<RectTransform>();
            }

            Image image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetPanelStyle(rect, color, 1.5f);
            return rect;
        }

        private static Text EnsureText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            RectTransform rect = FindRect(parent, name);
            Text text = rect != null ? rect.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                obj.transform.SetParent(parent, false);
                text = obj.GetComponent<Text>();
                text.font = ResolveFont(parent);
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Ink;
            text.raycastTarget = false;
            return text;
        }

        private static Font ResolveFont(Transform root)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font != null)
                {
                    return texts[i].font;
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void StyleButton(RectTransform rect, Color color, int fontSize)
        {
            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.68f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = fontSize;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.Max(10, fontSize - 4);
                label.resizeTextMaxSize = fontSize;
                label.alignment = TextAnchor.MiddleCenter;
                label.fontStyle = FontStyle.Bold;
                label.color = Ink;
            }
        }

        private static void SetPanelStyle(RectTransform rect, Color color, float outlineWidth)
        {
            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            outline.effectDistance = new Vector2(outlineWidth, -outlineWidth);
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
        }

        private static void PlaceTopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect, Vector2 inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = inset;
            rect.offsetMax = -inset;
            rect.localRotation = Quaternion.identity;
        }

        private static Text FindText(Transform root, string name)
        {
            RectTransform rect = FindRect(root, name);
            return rect != null ? rect.GetComponent<Text>() : null;
        }

        private static void SetActive(Transform root, string name, bool active)
        {
            RectTransform rect = FindRect(root, name);
            if (rect != null)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root as RectTransform;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform found = FindRect(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }

    public sealed class StageEditorPaletteButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private StageObjectType type;
        private Button button;

        public StageObjectType Type => type;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(SelectType);
        }

        public void Configure(RuntimeStageEditor targetEditor, StageObjectType targetType)
        {
            editor = targetEditor;
            type = targetType;
        }

        private void SelectType()
        {
            if (type == StageObjectType.StageBoundary)
            {
                editor?.CreateOrFitStageBoundary();
                return;
            }

            editor?.SetAddType(type);
        }

        public void SetSelected(bool selected)
        {
            Outline outline = GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected
                    ? new Color(0.08f, 0.35f, 0.92f, 0.98f)
                    : new Color(0.08f, 0.07f, 0.055f, 0.68f);
                outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
            }
        }
    }

    public sealed class StageBackgroundColorButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private Font font;
        private GameObject popup;
        private Image swatch;
        private Image preview;
        private RectTransform wheelPointer;
        private Slider redSlider;
        private Slider greenSlider;
        private Slider blueSlider;
        private Slider alphaSlider;
        private Text redValue;
        private Text greenValue;
        private Text blueValue;
        private Text alphaValue;
        private bool updating;
        private bool clickListenerAttached;

        private static readonly Color PanelColor = new Color(1f, 0.985f, 0.9f, 1f);
        private static readonly Color InkColor = new Color(0.08f, 0.07f, 0.055f, 0.96f);

        private void Awake()
        {
            EnsureClickListener();
            swatch = transform.Find("Swatch")?.GetComponent<Image>();
        }

        private void OnDisable()
        {
            if (popup != null)
            {
                popup.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (popup != null)
            {
                Destroy(popup);
            }
        }

        public void Configure(RuntimeStageEditor target, Font targetFont)
        {
            editor = target;
            font = targetFont;
            EnsureClickListener();
            RefreshColor(editor != null ? editor.StageBackgroundColor : StageBackgroundAppearance.DefaultColor);
        }

        private void EnsureClickListener()
        {
            Button button = GetComponent<Button>();
            if (button == null)
            {
                button = gameObject.AddComponent<Button>();
            }

            if (clickListenerAttached)
            {
                return;
            }

            button.onClick.AddListener(TogglePopup);
            clickListenerAttached = true;
        }

        public void RefreshColor(Color color)
        {
            if (swatch == null)
            {
                swatch = transform.Find("Swatch")?.GetComponent<Image>();
            }
            if (swatch != null)
            {
                swatch.color = color;
            }
            if (popup != null && popup.activeSelf)
            {
                SetControls(color);
            }
        }

        private void TogglePopup()
        {
            if (popup == null)
            {
                BuildPopup();
            }
            if (popup == null)
            {
                return;
            }

            bool show = !popup.activeSelf;
            popup.SetActive(show);
            if (show)
            {
                SetControls(editor != null
                    ? editor.StageBackgroundColor
                    : StageBackgroundAppearance.DefaultColor);
                popup.transform.SetAsLastSibling();
            }
        }

        private void BuildPopup()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform root = canvas != null
                ? canvas.transform as RectTransform
                : transform.parent as RectTransform;
            if (root == null)
            {
                return;
            }

            popup = new GameObject("RuntimeStageColorPickerPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            popup.transform.SetParent(root, false);
            RectTransform overlay = popup.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            popup.GetComponent<Image>().color = new Color(0.05f, 0.045f, 0.04f, 0.32f);

            GameObject panelObject = new GameObject("Color Picker Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(popup.transform, false);
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(440f, 558f);
            panel.anchoredPosition = Vector2.zero;
            panelObject.GetComponent<Image>().color = PanelColor;
            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = new Color(0.08f, 0.07f, 0.055f, 0.82f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel, "Title", LocalizationManager.T("stage_editor_color_title"), 24, TextAnchor.MiddleCenter, new Vector2(0f, -28f), new Vector2(350f, 38f));

            GameObject wheelObject = new GameObject("Color Wheel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ColorWheelInput));
            wheelObject.transform.SetParent(panel, false);
            RectTransform wheel = wheelObject.GetComponent<RectTransform>();
            PlaceCenteredTop(wheel, new Vector2(0f, -72f), new Vector2(230f, 230f));
            wheelObject.GetComponent<Image>().sprite = CreateColorWheelSprite(256);
            wheelObject.GetComponent<Image>().preserveAspect = true;
            wheelObject.GetComponent<ColorWheelInput>().Configure(OnWheelChanged);

            GameObject pointerObject = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            pointerObject.transform.SetParent(wheel, false);
            wheelPointer = pointerObject.GetComponent<RectTransform>();
            wheelPointer.anchorMin = wheelPointer.anchorMax = wheelPointer.pivot = new Vector2(0.5f, 0.5f);
            wheelPointer.sizeDelta = new Vector2(15f, 15f);
            pointerObject.GetComponent<Image>().color = Color.white;
            Outline pointerOutline = pointerObject.GetComponent<Outline>();
            pointerOutline.effectColor = Color.black;
            pointerOutline.effectDistance = new Vector2(2f, -2f);

            preview = CreateImage(panel, "Preview", Color.white, new Vector2(-170f, -322f), new Vector2(58f, 58f));
            redSlider = CreateRgbRow(panel, "R", new Vector2(10f, -322f), new Color(0.9f, 0.22f, 0.18f), out redValue);
            greenSlider = CreateRgbRow(panel, "G", new Vector2(10f, -362f), new Color(0.2f, 0.72f, 0.3f), out greenValue);
            blueSlider = CreateRgbRow(panel, "B", new Vector2(10f, -402f), new Color(0.2f, 0.48f, 0.92f), out blueValue);
            CreateText(panel, "Alpha Label", LocalizationManager.T("stage_editor_color_opacity"), 14, TextAnchor.MiddleCenter, new Vector2(-112f, -442f), new Vector2(72f, 28f));
            alphaSlider = CreateSlider(panel, "Alpha Slider", new Vector2(10f, -442f), new Vector2(190f, 24f), new Color(0.42f, 0.42f, 0.45f, 1f));
            alphaValue = CreateText(panel, "Alpha Value", "100%", 15, TextAnchor.MiddleRight, new Vector2(136f, -442f), new Vector2(58f, 28f));
            redSlider.onValueChanged.AddListener(_ => OnRgbChanged());
            greenSlider.onValueChanged.AddListener(_ => OnRgbChanged());
            blueSlider.onValueChanged.AddListener(_ => OnRgbChanged());
            alphaSlider.onValueChanged.AddListener(_ => OnRgbChanged());

            Button reset = CreateButton(panel, "Reset", LocalizationManager.T("stage_editor_color_reset"), new Vector2(-92f, -510f), new Vector2(150f, 40f), new Color(0.92f, 0.91f, 0.86f, 1f));
            reset.onClick.AddListener(() => ApplyColor(StageBackgroundAppearance.DefaultColor));
            Button close = CreateButton(panel, "Close", LocalizationManager.T("stage_editor_color_close"), new Vector2(92f, -510f), new Vector2(150f, 40f), new Color(0.62f, 0.88f, 1f, 1f));
            close.onClick.AddListener(() => popup.SetActive(false));
            popup.SetActive(false);
        }

        private void OnWheelChanged(float hue, float saturation)
        {
            if (updating)
            {
                return;
            }

            Color current = editor != null ? editor.StageBackgroundColor : Color.white;
            Color.RGBToHSV(current, out _, out _, out float value);
            Color selected = Color.HSVToRGB(hue, saturation, Mathf.Max(0.05f, value));
            selected.a = current.a;
            ApplyColor(selected);
        }

        private void OnRgbChanged()
        {
            if (!updating)
            {
                ApplyColor(new Color(redSlider.value, greenSlider.value, blueSlider.value, alphaSlider.value));
            }
        }

        private void ApplyColor(Color color)
        {
            editor?.SetStageBackgroundColor(color);
            SetControls(color);
        }

        private void SetControls(Color color)
        {
            updating = true;
            if (redSlider != null) redSlider.SetValueWithoutNotify(color.r);
            if (greenSlider != null) greenSlider.SetValueWithoutNotify(color.g);
            if (blueSlider != null) blueSlider.SetValueWithoutNotify(color.b);
            if (alphaSlider != null) alphaSlider.SetValueWithoutNotify(color.a);
            if (redValue != null) redValue.text = Mathf.RoundToInt(color.r * 255f).ToString();
            if (greenValue != null) greenValue.text = Mathf.RoundToInt(color.g * 255f).ToString();
            if (blueValue != null) blueValue.text = Mathf.RoundToInt(color.b * 255f).ToString();
            if (alphaValue != null) alphaValue.text = Mathf.RoundToInt(color.a * 100f) + "%";
            if (preview != null) preview.color = color;
            if (swatch != null) swatch.color = color;

            if (wheelPointer != null)
            {
                Color.RGBToHSV(color, out float hue, out float saturation, out _);
                float angle = hue * Mathf.PI * 2f;
                float radius = 107f * saturation;
                wheelPointer.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            updating = false;
        }

        private Slider CreateRgbRow(RectTransform parent, string channel, Vector2 position, Color fillColor, out Text valueText)
        {
            CreateText(parent, channel + " Label", channel, 17, TextAnchor.MiddleCenter, new Vector2(position.x - 122f, position.y), new Vector2(28f, 28f));
            Slider slider = CreateSlider(parent, channel + " Slider", new Vector2(position.x, position.y), new Vector2(190f, 24f), fillColor);
            valueText = CreateText(parent, channel + " Value", "255", 15, TextAnchor.MiddleRight, new Vector2(position.x + 126f, position.y), new Vector2(46f, 28f));
            return slider;
        }

        private Slider CreateSlider(RectTransform parent, string name, Vector2 position, Vector2 size, Color fillColor)
        {
            GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
            rootObject.transform.SetParent(parent, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            PlaceCenteredTop(rect, position, size);

            Image background = CreateImage(rect, "Background", new Color(0.82f, 0.81f, 0.76f, 1f), Vector2.zero, size);
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            background.rectTransform.offsetMin = new Vector2(0f, -4f);
            background.rectTransform.offsetMax = new Vector2(0f, 4f);

            Image fill = CreateImage(rect, "Fill", fillColor, Vector2.zero, size);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(size.x, 8f);

            Image handle = CreateImage(rect, "Handle", Color.white, Vector2.zero, new Vector2(18f, 26f));
            Outline outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Slider slider = rootObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            PlaceCenteredTop(rect, position, size);
            obj.GetComponent<Image>().color = color;
            Outline outline = obj.GetComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(2f, -2f);
            CreateText(rect, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, size);
            return obj.GetComponent<Button>();
        }

        private Text CreateText(RectTransform parent, string name, string value, int size, TextAnchor alignment, Vector2 position, Vector2 dimensions)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text text = obj.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = InkColor;
            PlaceCenteredTop(text.rectTransform, position, dimensions);
            return text;
        }

        private static Image CreateImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 dimensions)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            PlaceCenteredTop(image.rectTransform, position, dimensions);
            return image;
        }

        private static void PlaceCenteredTop(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Sprite CreateColorWheelSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime RGB Color Wheel",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float radius = center - 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float saturation = Mathf.Sqrt(dx * dx + dy * dy);
                    if (saturation > 1f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f), 1f);
                    pixels[y * size + x] = Color.HSVToRGB(hue, saturation, 1f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    public sealed class ColorWheelInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private System.Action<float, float> changed;

        public void Configure(System.Action<float, float> callback)
        {
            changed = callback;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateColor(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateColor(eventData);
        }

        private void UpdateColor(PointerEventData eventData)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                return;
            }

            Vector2 centered = local - rect.rect.center;
            float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
            float saturation = centered.magnitude / Mathf.Max(1f, radius);
            if (saturation > 1f)
            {
                return;
            }

            float hue = Mathf.Repeat(Mathf.Atan2(centered.y, centered.x) / (Mathf.PI * 2f), 1f);
            changed?.Invoke(hue, Mathf.Clamp01(saturation));
        }
    }

    public sealed class StageEditorFreehandButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.AddListener(Toggle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Toggle()
        {
            editor?.ToggleTerrainFreehand();
        }
    }

    public sealed class StageEditorStraightButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.AddListener(Toggle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Toggle()
        {
            editor?.ToggleTerrainStraightLine();
        }
    }

    public sealed class StageEditorSeparateButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Toggle()
        {
            editor?.ToggleTerrainKeepSeparate();
        }
    }

    public sealed class StageEditorDuplicateButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Duplicate);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Duplicate()
        {
            editor?.DuplicateSelected();
        }
    }

    public sealed class StageEditorDuplicateDirectionButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Cycle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Cycle()
        {
            editor?.CycleCopyDirection();
        }
    }

    public sealed class StageEditorLinkActionButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Toggle()
        {
            editor?.ToggleSelectedLinkAction();
        }
    }

    public sealed class StageEditorConveyorDirectionButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Toggle()
        {
            editor?.ToggleSelectedConveyorDirection();
        }
    }

    public sealed class StageEditorRuleButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private int commandId;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Execute);
        }

        public void Configure(RuntimeStageEditor targetEditor, int targetCommandId)
        {
            editor = targetEditor;
            commandId = targetCommandId;
        }

        private void Execute()
        {
            switch (commandId)
            {
                case 0: editor?.CycleStageRuleMode(); break;
                case 1: editor?.CycleStageCollectionTarget(); break;
                case 2: editor?.AdjustStageTimeLimit(); break;
                case 3: editor?.AdjustStageRequiredCount(); break;
            }
        }
    }

    public sealed class StageEditorBoxPatternButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Cycle);
        }

        public void Configure(RuntimeStageEditor targetEditor)
        {
            editor = targetEditor;
        }

        private void Cycle()
        {
            editor?.CycleSelectedDropperPattern();
        }
    }

    public sealed class StageEditorThicknessButton : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private float delta;

        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.AddListener(ChangeThickness);
        }

        public void Configure(RuntimeStageEditor targetEditor, float change)
        {
            editor = targetEditor;
            delta = change;
        }

        private void ChangeThickness()
        {
            editor?.AdjustTerrainThickness(delta);
        }
    }

    public sealed class StageEditorThicknessSlider : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(ChangeThickness);
        }

        public void Configure(RuntimeStageEditor targetEditor, Slider targetSlider)
        {
            editor = targetEditor;
            slider = targetSlider;
        }

        private void ChangeThickness(float value)
        {
            editor?.SetTerrainThickness(value);
        }
    }

    public sealed class StageEditorActionStrengthSlider : MonoBehaviour, IPointerDownHandler
    {
        private RuntimeStageEditor editor;
        private Slider slider;
        private bool suppressChanges;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(ChangeStrength);
        }

        public void Configure(RuntimeStageEditor targetEditor, Slider targetSlider)
        {
            editor = targetEditor;
            slider = targetSlider;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            editor?.BeginActionStrengthEdit();
        }

        public void SetRangeAndValueWithoutNotify(float minimum, float maximum, float value)
        {
            if (slider == null)
            {
                slider = GetComponent<Slider>();
            }

            if (slider == null)
            {
                return;
            }

            suppressChanges = true;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, minimum, maximum));
            suppressChanges = false;
        }

        private void ChangeStrength(float value)
        {
            if (!suppressChanges)
            {
                editor?.SetSelectedActionStrength(value);
            }
        }
    }

    public sealed class StageEditorMovementSpeedSlider : MonoBehaviour, IPointerDownHandler
    {
        private RuntimeStageEditor editor;
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(ChangeSpeed);
        }

        public void Configure(RuntimeStageEditor targetEditor, Slider targetSlider)
        {
            editor = targetEditor;
            slider = targetSlider;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            editor?.BeginMovementSpeedEdit();
        }

        private void ChangeSpeed(float value)
        {
            editor?.SetSelectedMovementSpeed(value);
        }
    }

    public sealed class StageEditorDropperBoxSizeSlider : MonoBehaviour, IPointerDownHandler
    {
        private RuntimeStageEditor editor;
        private Slider slider;

        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(ChangeSize);
        }

        public void Configure(RuntimeStageEditor targetEditor, Slider targetSlider)
        {
            editor = targetEditor;
            slider = targetSlider;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            editor?.BeginDropperBoxSizeEdit();
        }

        private void ChangeSize(float value)
        {
            editor?.SetSelectedDropperBoxSize(value);
        }
    }

    public sealed class StageEditorWeightThresholdInput : MonoBehaviour
    {
        private RuntimeStageEditor editor;
        private InputField input;
        private Text visibleText;
        private Text placeholderText;
        private Text overlayText;

        private void Awake()
        {
            input = GetComponent<InputField>();
            input.onEndEdit.AddListener(Submit);
            input.onValueChanged.AddListener(RefreshVisibleText);
        }

        public void Configure(RuntimeStageEditor targetEditor, InputField targetInput, Text targetText, Text targetPlaceholder)
        {
            editor = targetEditor;
            input = targetInput;
            visibleText = targetText;
            placeholderText = targetPlaceholder;
            RefreshVisibleText(input != null ? input.text : string.Empty);
        }

        public void RefreshVisibleText(string value)
        {
            if (visibleText != null)
            {
                visibleText.text = value;
                visibleText.color = new Color(0.08f, 0.07f, 0.055f, 1f);
                visibleText.enabled = true;
                visibleText.canvasRenderer.SetAlpha(1f);
            }
            if (placeholderText != null)
            {
                placeholderText.enabled = string.IsNullOrEmpty(value);
            }
            if (overlayText != null)
            {
                overlayText.text = value;
                overlayText.enabled = true;
                overlayText.canvasRenderer.SetAlpha(1f);
            }
        }

        public void SetOverlay(Text targetOverlay)
        {
            overlayText = targetOverlay;
            RefreshVisibleText(input != null ? input.text : string.Empty);
        }

        private void Submit(string value)
        {
            editor?.SetSelectedWeightThreshold(value);
        }
    }
}
