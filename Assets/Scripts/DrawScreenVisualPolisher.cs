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

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshLabels;
            Polish();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLabels;
        }

        public void Polish()
        {
            if (polished)
            {
                RefreshLabels();
                return;
            }

            polished = true;
            RebuildToolLayout();
            StraightenDrawUi();
            GameplayHudDrawer.RedrawTurtleIcon(FindRect(transform, "TurtleDrawSpeciesButton"));
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

            Hide(FindRect(transform, "PreviewTitle"));

            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(-145f, 14f);
            panel.sizeDelta = new Vector2(930f, 118f);

            RectTransform abilityCard = EnsureCard(transform as RectTransform, "SpeciesAbilityCard");
            SetCenterRect(abilityCard, new Vector2(85f, 0f), new Vector2(280f, 220f));
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

            Text abilityEffect = EnsureLabel(abilityCard, "AbilityEffectText", string.Empty, 18, TextAnchor.MiddleCenter);
            SetTopRect(abilityEffect.rectTransform, new Vector2(12f, -94f), new Vector2(256f, 28f), new Vector2(0f, 1f));
            abilityEffect.fontStyle = FontStyle.Bold;

            RectTransform abilityGauge = EnsureGauge(abilityCard, "AbilityGaugeBack", "AbilityGaugeFill");
            SetTopRect(abilityGauge, new Vector2(18f, -128f), new Vector2(244f, 18f), new Vector2(0f, 1f));
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

            Text abilityLow = EnsureLabel(abilityCard, "AbilityLowText", string.Empty, 12, TextAnchor.MiddleLeft);
            SetTopRect(abilityLow.rectTransform, new Vector2(18f, -148f), new Vector2(100f, 18f), new Vector2(0f, 1f));
            Text abilityHigh = EnsureLabel(abilityCard, "AbilityHighText", string.Empty, 12, TextAnchor.MiddleRight);
            SetTopRect(abilityHigh.rectTransform, new Vector2(162f, -148f), new Vector2(100f, 18f), new Vector2(0f, 1f));

            Text abilityInk = EnsureLabel(abilityCard, "AbilityInkText", string.Empty, 15, TextAnchor.MiddleCenter);
            SetTopRect(abilityInk.rectTransform, new Vector2(10f, -169f), new Vector2(260f, 22f), new Vector2(0f, 1f));
            Text abilityHint = EnsureLabel(abilityCard, "AbilityHintText", string.Empty, 12, TextAnchor.MiddleCenter);
            SetTopRect(abilityHint.rectTransform, new Vector2(10f, -194f), new Vector2(260f, 18f), new Vector2(0f, 1f));
            abilityHint.color = new Color(0.28f, 0.23f, 0.16f, 0.82f);

            Text header = EnsureLabel(panel, "ToolPanelHeader", "TOOLS", 19, TextAnchor.MiddleLeft);
            header.gameObject.SetActive(false);

            RectTransform pen = FindRect(panel, "PenToolButton");
            RectTransform eraser = FindRect(panel, "EraserToolButton");
            SetDockRect(pen, new Vector2(12f, 12f), new Vector2(108f, 94f));
            SetDockRect(eraser, new Vector2(128f, 12f), new Vector2(108f, 94f));
            EnsureSelectionBadge(pen, new Color(0.08f, 0.64f, 0.78f, 1f));
            EnsureSelectionBadge(eraser, new Color(0.95f, 0.42f, 0.25f, 1f));

            RectTransform brush = FindRect(panel, "BrushSizeChip");
            SetDockRect(brush, new Vector2(244f, 10f), new Vector2(240f, 98f));
            RestyleCard(brush, new Color(1f, 0.975f, 0.9f, 0.96f));
            Text brushHeader = EnsureLabel(brush, "BrushSectionHeader", "BRUSH", 13, TextAnchor.MiddleLeft);
            SetTopRect(brushHeader.rectTransform, new Vector2(14f, -8f), new Vector2(150f, 24f), new Vector2(0f, 1f));
            brushHeader.fontStyle = FontStyle.Bold;
            Hide(FindRect(brush, "BrushSizeTitle"));

            RectTransform slider = FindRect(brush, "BrushSizeSlider");
            Hide(slider);

            RectTransform brushValue = FindRect(brush, "BrushSizeValueText");
            Hide(brushValue);
            Hide(FindRect(brush, "BrushValueBadge"));
            CreateBrushPresetButtons(brush);

            RectTransform inkCard = EnsureCard(panel, "InkStatusCard");
            SetDockRect(inkCard, new Vector2(492f, 10f), new Vector2(260f, 98f));
            RestyleCard(inkCard, new Color(0.92f, 0.975f, 1f, 0.97f));
            MoveInto(FindRect(panel, "InkUsageTitle"), inkCard);
            MoveInto(FindRect(panel, "InkGaugeBack"), inkCard);
            MoveInto(FindRect(panel, "InkText"), inkCard);

            RectTransform inkTitle = FindRect(inkCard, "InkUsageTitle");
            SetTopRect(inkTitle, new Vector2(12f, -5f), new Vector2(236f, 20f), new Vector2(0f, 1f));
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
            }

            RectTransform gauge = FindRect(inkCard, "InkGaugeBack");
            SetTopRect(gauge, new Vector2(12f, -47f), new Vector2(236f, 11f), new Vector2(0f, 1f));
            ConfigureStraightGauge(gauge, "InkGaugeFill");
            RectTransform inkText = FindRect(inkCard, "InkText");
            Hide(inkText);

            Text personalLabel = EnsureLabel(inkCard, "PersonalInkLabel", "YOU", 13, TextAnchor.MiddleLeft);
            SetTopRect(personalLabel.rectTransform, new Vector2(12f, -25f), new Vector2(130f, 20f), new Vector2(0f, 1f));
            Text personalValue = EnsureLabel(inkCard, "PersonalInkValue", "0 / 500", 14, TextAnchor.MiddleRight);
            SetTopRect(personalValue.rectTransform, new Vector2(142f, -25f), new Vector2(106f, 20f), new Vector2(0f, 1f));

            Text teamLabel = EnsureLabel(inkCard, "TeamInkLabel", "TEAM", 13, TextAnchor.MiddleLeft);
            SetTopRect(teamLabel.rectTransform, new Vector2(12f, -62f), new Vector2(130f, 20f), new Vector2(0f, 1f));
            Text teamValue = EnsureLabel(inkCard, "TeamInkValue", "0 / 350", 14, TextAnchor.MiddleRight);
            SetTopRect(teamValue.rectTransform, new Vector2(142f, -62f), new Vector2(106f, 20f), new Vector2(0f, 1f));
            RectTransform teamGauge = EnsureGauge(inkCard, "TeamInkGaugeBack", "TeamInkGaugeFill");
            SetTopRect(teamGauge, new Vector2(12f, -84f), new Vector2(236f, 11f), new Vector2(0f, 1f));
            ConfigureStraightGauge(teamGauge, "TeamInkGaugeFill");

            if (personalValue != null)
            {
                personalValue.fontStyle = FontStyle.Bold;
            }

            teamValue.fontStyle = FontStyle.Bold;

            RectTransform history = EnsureCard(panel, "HistoryCard");
            SetDockRect(history, new Vector2(760f, 10f), new Vector2(158f, 98f));
            RestyleCard(history, new Color(1f, 0.96f, 0.9f, 0.97f));
            MoveInto(FindRect(panel, "ToolClearButton"), history);
            MoveInto(FindRect(panel, "ToolUndoButton"), history);
            RectTransform clear = FindRect(history, "ToolClearButton");
            RectTransform undo = FindRect(history, "ToolUndoButton");
            RectTransform fullReset = EnsureFullResetButton(history);
            SetDockRect(clear, new Vector2(6f, 67f), new Vector2(146f, 27f));
            SetDockRect(undo, new Vector2(6f, 36f), new Vector2(146f, 27f));
            SetDockRect(fullReset, new Vector2(6f, 5f), new Vector2(146f, 27f));
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
            SetBottomRect(decide, new Vector2(420f, 14f), new Vector2(168f, 104f));
            SetBottomRect(cancel, new Vector2(570f, 14f), new Vector2(96f, 104f));

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

        private void RefreshLabels()
        {
            bool japanese = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese;
            SetPlainText("BrushSectionHeader", japanese ? "\u30da\u30f3\u306e\u592a\u3055" : "PEN SIZE");
            SetPlainText("InkUsageTitle", japanese ? "\u30a4\u30f3\u30af" : "INK");
            SetPlainText("PersonalInkLabel", LocalizationManager.T("ink_personal_cap"));
            SetPlainText("TeamInkLabel", LocalizationManager.Format("ink_team_formula", 1, DrawManager.InkAllowancePerPlayer));
            SetButtonLabel("PenToolButton", "\u270e  " + LocalizationManager.T("pen"), 18);
            SetButtonLabel("EraserToolButton", "\u25b1  " + LocalizationManager.T("eraser"), 17);
            SetButtonLabel("ToolClearButton", japanese ? "\u2715  \u30d1\u30fc\u30c4\u6d88\u53bb" : "\u2715  CLEAR PART", 14);
            SetButtonLabel("ToolUndoButton", japanese ? "\u21b6  1\u3064\u623b\u3059" : "\u21b6  UNDO", 14);
            SetButtonLabel("FullResetButton", "\u25a0  " + LocalizationManager.T("draw_reset_all"), 13);
            SetPlainText("FullResetConfirmTitle", LocalizationManager.T("draw_reset_confirm_title"));
            SetPlainText("FullResetConfirmMessage", LocalizationManager.T("draw_reset_confirm_message"));
            SetButtonLabel("FullResetConfirmButton", LocalizationManager.T("draw_reset_confirm_yes"), 18);
            SetButtonLabel("FullResetCancelButton", LocalizationManager.T("draw_reset_confirm_no"), 18);
            SetButtonLabel("DecideButton", "\u2713  " + LocalizationManager.T("draw_finish") + "\nENTER", 19);
            SetButtonLabel("CancelDrawButton", "\u2190  " + LocalizationManager.T("draw_redo") + "\nESC", 16);
            ApplyTypography();
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
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateBrushPresetButtons(RectTransform brush)
        {
            if (brush == null)
            {
                return;
            }

            DrawManager drawManager = FindObjectOfType<DrawManager>();
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

                SetDockRect(rect, new Vector2(10f + i * 45f, 8f), new Vector2(40f, 54f));
                Outline outline = rect.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = rect.gameObject.AddComponent<Outline>();
                }

                Text number = EnsureLabel(rect, "Value", preset.ToString("0"), 12, TextAnchor.MiddleCenter);
                number.rectTransform.anchorMin = new Vector2(0f, 0f);
                number.rectTransform.anchorMax = new Vector2(1f, 0f);
                number.rectTransform.pivot = new Vector2(0.5f, 0f);
                number.rectTransform.anchoredPosition = new Vector2(0f, 3f);
                number.rectTransform.sizeDelta = new Vector2(0f, 18f);
                number.fontStyle = FontStyle.Bold;

                RectTransform sample = EnsureImage(rect, "StrokeSample");
                sample.anchorMin = new Vector2(0.5f, 1f);
                sample.anchorMax = new Vector2(0.5f, 1f);
                sample.pivot = new Vector2(0.5f, 0.5f);
                sample.anchoredPosition = new Vector2(0f, -17f);
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
            image.color = new Color(1f, 0.72f, 0.62f, 1f);
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
            image.color = color;
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
            FindObjectOfType<DrawManager>()?.ResetAllToDefault();
            CloseFullResetConfirmDialog();
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
                image.color = buttonColor;
                Button button = rect.GetComponent<Button>();
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
                colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
                Outline outline = rect.GetComponent<Outline>();
                outline.effectColor = selected
                    ? new Color(0.04f, 0.18f, 0.24f, 1f)
                    : new Color(0.2f, 0.18f, 0.14f, 0.45f);
                outline.effectDistance = selected ? new Vector2(3f, -3f) : new Vector2(1f, -1f);
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
            if (rect == null || rect.GetComponent<Image>() == null)
            {
                return;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null)
            {
                outline = rect.gameObject.AddComponent<Outline>();
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
            Font font = reference != null && reference.font != null
                ? reference.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
                else if (text.name != "ConnectionMessageText")
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
                image.color = color;
            }

            Outline outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.12f, 0.09f, 0.06f, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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
