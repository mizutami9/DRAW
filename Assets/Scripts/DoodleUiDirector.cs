using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Applies one coherent, lively sketchbook theme to both the authored scene and
    /// UI rebuilt with Phase0SceneBuilder. The pass is intentionally idempotent.
    /// </summary>
    public sealed class DoodleUiDirector : MonoBehaviour
    {
        private static readonly Color Paper = new Color(0.975f, 0.955f, 0.885f, 0.985f);
        private static readonly Color PaperRaised = new Color(1f, 0.985f, 0.925f, 0.98f);
        private static readonly Color Ink = new Color(0.075f, 0.065f, 0.055f, 0.96f);
        private static readonly Color Cyan = new Color(0.22f, 0.78f, 0.92f, 1f);
        private static readonly Color Yellow = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color Green = new Color(0.45f, 0.88f, 0.42f, 1f);
        private static readonly Color Coral = new Color(1f, 0.45f, 0.34f, 1f);
        private static readonly Color Violet = new Color(0.66f, 0.52f, 0.96f, 1f);

        private Font fallbackFont;
        private bool themeApplied;

        private void Awake()
        {
            fallbackFont = FindFont();
            LegacyPencilStrokeBatcher.BatchScene();
            ApplyTheme();
        }

        private void Start()
        {
            // UIManager may create these small overlays later in Awake. Refresh only
            // those targets instead of walking the complete UI tree again.
            ThemeGameplayHud();
            ThemeMenuAndResults();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        }

        public void ApplyTheme()
        {
            ApplyTheme(false);
        }

        public void RefreshDynamicTheme()
        {
            ThemeGameplayHud();
            ThemeMenuAndResults();
        }

        private void HandleLanguageChanged()
        {
            ApplyTheme(true);
        }

        private void ApplyTheme(bool force)
        {
            if (themeApplied && !force)
            {
                return;
            }

            themeApplied = true;
            fallbackFont = fallbackFont != null ? fallbackFont : FindFont();
            ThemeStageBackgroundDoodles();
            ThemeTitle();
            ThemeStageSelect();
            ThemeDrawScreen();
            ThemeMultiAndOptions();
            ThemeGameplayHud();
            ThemeStageEditor();
            ThemeMenuAndResults();
            ThemeAllButtons();
            ThemeAllText();
            StraightenAllScreens();
        }

        private void ThemeStageBackgroundDoodles()
        {
            // Remove runtime UI decorations created by older versions of this pass.
            string[] panelNames =
            {
                "TitlePanel", "StageSelectPanel", "DrawPanel",
                "TitleMultiPanel", "TitleOptionPanel", "RuntimeStageEditorPanel"
            };
            for (int i = 0; i < panelNames.Length; i++)
            {
                RectTransform panel = FindRect(panelNames[i]);
                Transform oldMarks = panel != null ? panel.Find("RandomNotebookMarks") : null;
                if (oldMarks != null)
                {
                    oldMarks.gameObject.SetActive(false);
                }
            }

            GameObject paper = GameObject.Find("Notebook Paper");
            if (paper != null && paper.transform.parent != null)
            {
                ExpandWorldNotebook(paper);

                SpriteRenderer paperRenderer = paper.GetComponent<SpriteRenderer>();
                if (paperRenderer != null)
                {
                    CrayonStageBackground texture = paper.GetComponent<CrayonStageBackground>();
                    if (texture == null)
                    {
                        texture = paper.AddComponent<CrayonStageBackground>();
                    }
                    texture.Configure(paperRenderer.color, paperRenderer);
                }
            }
            NotebookBackgroundDoodles.RemoveWorld();
        }

        private static void ExpandWorldNotebook(GameObject paper)
        {
            const float minX = -40f;
            const float maxX = 200f;
            const float minY = -60f;
            const float maxY = 80f;
            const float ruleSpacing = 0.6f;

            Vector3 paperPosition = paper.transform.localPosition;
            paper.transform.localPosition = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, paperPosition.z);
            paper.transform.localScale = new Vector3(maxX - minX, maxY - minY, paper.transform.localScale.z);

            Transform ruleParent = paper.transform.parent;
            Transform[] notebookParts = ruleParent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < notebookParts.Length; i++)
            {
                string partName = notebookParts[i].name;
                if (partName.StartsWith("Notebook Margin", System.StringComparison.Ordinal)
                    || partName.StartsWith("Notebook Hole", System.StringComparison.Ordinal))
                {
                    notebookParts[i].gameObject.SetActive(false);
                }
            }

            LineRenderer template = null;
            LineRenderer[] existingLines = ruleParent.GetComponentsInChildren<LineRenderer>(true);
            for (int i = 0; i < existingLines.Length; i++)
            {
                LineRenderer line = existingLines[i];
                if (line != null && line.name.StartsWith("Notebook Rule", System.StringComparison.Ordinal))
                {
                    template = template != null ? template : line;
                    float y = line.GetPosition(0).y;
                    line.positionCount = 2;
                    line.SetPosition(0, new Vector3(minX, y, 1.7f));
                    line.SetPosition(1, new Vector3(maxX, y, 1.7f));
                }
            }

            if (template == null)
            {
                return;
            }

            int ruleIndex = 0;
            for (float y = minY + 0.2f; y <= maxY; y += ruleSpacing)
            {
                bool exists = false;
                for (int i = 0; i < existingLines.Length; i++)
                {
                    LineRenderer line = existingLines[i];
                    if (line != null
                        && line.name.StartsWith("Notebook Rule", System.StringComparison.Ordinal)
                        && Mathf.Abs(line.GetPosition(0).y - y) < 0.08f)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }

                GameObject lineObject = new GameObject($"Notebook Rule Expanded {ruleIndex++}");
                lineObject.transform.SetParent(ruleParent, false);
                LineRenderer expanded = lineObject.AddComponent<LineRenderer>();
                expanded.useWorldSpace = false;
                expanded.positionCount = 2;
                expanded.SetPosition(0, new Vector3(minX, y, 1.7f));
                expanded.SetPosition(1, new Vector3(maxX, y, 1.7f));
                expanded.startWidth = template.startWidth;
                expanded.endWidth = template.endWidth;
                expanded.material = template.sharedMaterial;
                expanded.startColor = template.startColor;
                expanded.endColor = template.endColor;
                expanded.sortingOrder = template.sortingOrder;
            }
        }

        private void ThemeTitle()
        {
            RectTransform panel = FindRect("TitlePanel");
            if (panel == null)
            {
                return;
            }

            EnsureBackdrop(panel, "TitleBackdrop", new Color(0.96f, 0.93f, 0.83f, 0.18f));
            EnsureDoodleCluster(panel, "TitleDoodles", new Vector2(-390f, -120f), 1.35f);

            RectTransform logo = FindRect(panel, "TitleNicoDrowLogo");
            if (logo != null)
            {
                logo.anchorMin = new Vector2(0.5f, 1f);
                logo.anchorMax = new Vector2(0.5f, 1f);
                logo.pivot = new Vector2(0.5f, 1f);
                logo.anchoredPosition = new Vector2(0f, -28f);
                logo.sizeDelta = new Vector2(1040f, 330f);
                logo.localRotation = Quaternion.identity;
            }

            RectTransform menu = FindRect(panel, "TitleMenuBar");
            if (menu != null)
            {
                TrailerDebugMenuController.Ensure(panel, menu);
                menu.anchorMin = new Vector2(0f, 0f);
                menu.anchorMax = new Vector2(1f, 0f);
                menu.pivot = new Vector2(0.5f, 0f);
                menu.anchoredPosition = new Vector2(0f, 12f);
                menu.sizeDelta = new Vector2(-56f, 94f);
                SetImage(menu, new Color(0.995f, 0.975f, 0.9f, 0.96f));
                EnsureOutline(menu.gameObject, 3.2f, 0.84f);
                EnsureShadow(menu.gameObject, new Vector2(6f, -7f), 0.2f);

                SetButtonLayout(menu, "TitleSingleButton", new Vector2(-344f, 18f), new Vector2(148f, 58f), Green, -1.2f);
                SetButtonLayout(menu, "TitleMultiButton", new Vector2(-172f, 18f), new Vector2(148f, 58f), Cyan, 0.8f);
                SetButtonLayout(menu, "TitleDrawButton", new Vector2(0f, 18f), new Vector2(148f, 58f), Yellow, -0.7f);
                SetButtonLayout(menu, "TitleOptionButton", new Vector2(172f, 18f), new Vector2(148f, 58f), Violet, 0.6f);
                SetButtonLayout(menu, "TitleExitButton", new Vector2(344f, 18f), new Vector2(148f, 58f), Coral, -0.5f);
                HideIfExists(menu, "TitleDebugButton");
            }

            HideIfExists(panel, "TitleTagline");
            HideIfExists(panel, "TitleTaglineMarkerHighlight");
        }

        private void ThemeStageSelect()
        {
            RectTransform panel = FindRect("StageSelectPanel");
            if (panel == null)
            {
                return;
            }

            SetImage(panel, Paper);
            EnsureBackdrop(panel, "StageMapBackdrop", new Color(0.35f, 0.7f, 0.9f, 0.055f));
            Text heading = EnsureText(panel, "ModernStageSelectTitle", LocalizationManager.T("stage_select"), 38, TextAnchor.MiddleLeft);
            heading.fontStyle = FontStyle.Bold;
            heading.color = Ink;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(0f, 1f);
            headingRect.pivot = new Vector2(0f, 1f);
            headingRect.anchoredPosition = new Vector2(42f, -24f);
            headingRect.sizeDelta = new Vector2(430f, 58f);
            EnsureHighlight(headingRect, Yellow);

            for (int i = 1; i <= 15; i++)
            {
                RectTransform card = FindRect(panel, $"World{i}Card");
                if (card == null)
                {
                    continue;
                }

                Color tint = i % 3 == 0
                    ? new Color(0.91f, 0.97f, 1f, 0.98f)
                    : i % 3 == 1
                        ? new Color(1f, 0.97f, 0.83f, 0.98f)
                        : new Color(0.92f, 1f, 0.89f, 0.98f);
                SetImage(card, tint);
                EnsureOutline(card.gameObject, 2.8f, 0.68f);
                EnsureShadow(card.gameObject, new Vector2(6f, -7f), 0.18f);
                EnsureTape(card, new Vector2(0f, 164f), (i % 5 - 2) * 2f, i % 2 == 0 ? Cyan : Yellow);
            }

            SetFloatingButton(panel, "StageSelectPreviousPageButton", new Vector2(-92f, 78f), new Vector2(64f, 52f), Cyan);
            SetFloatingButton(panel, "StageSelectNextPageButton", new Vector2(92f, 78f), new Vector2(64f, 52f), Cyan);
            SetFloatingButton(panel, "StageSelectBackButton", new Vector2(528f, 38f), new Vector2(172f, 56f), Coral);
            SetFloatingButton(panel, "StageSelectEditModeButton", new Vector2(-528f, 38f), new Vector2(188f, 56f), Violet);
        }

        private void ThemeDrawScreen()
        {
            RectTransform panel = FindRect("DrawPanel");
            if (panel == null)
            {
                return;
            }

            SetImage(panel, Paper);
            HideIfExists(panel, "DrawBackdrop");
            HideIfExists(panel, "DrawCornerDoodles");

            RectTransform title = FindRect(panel, "DrawTitle");
            if (title != null)
            {
                title.anchoredPosition = new Vector2(34f, -18f);
                title.sizeDelta = new Vector2(260f, 62f);
                Text titleText = title.GetComponent<Text>();
                if (titleText != null)
                {
                    titleText.fontSize = 44;
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.color = Ink;
                }
            }

            RectTransform partBar = FindRect(panel, "PartButtonBar");
            if (partBar != null)
            {
                SetImage(partBar, new Color(1f, 0.97f, 0.86f, 0.96f));
                EnsureOutline(partBar.gameObject, 2.4f, 0.62f);
                EnsureShadow(partBar.gameObject, new Vector2(5f, -5f), 0.14f);
            }

            RectTransform drawArea = FindRect(panel, "DrawArea");
            RectTransform preview = FindRect(panel, "PreviewArea");
            RectTransform tools = FindRect(panel, "DrawToolPanel");
            ThemeWorkspaceCard(drawArea, Color.white, Cyan);
            ThemeWorkspaceCard(preview, new Color(0.96f, 0.985f, 1f, 1f), Violet);
            ThemeWorkspaceCard(tools, new Color(1f, 0.95f, 0.79f, 0.98f), Yellow);
            HideIfExists(drawArea, "ModernMaskingTape");
            HideIfExists(preview, "ModernMaskingTape");
            HideIfExists(tools, "ModernMaskingTape");

            SetNamedButtonColor(panel, "DecideButton", Green);
            SetNamedButtonColor(panel, "CancelDrawButton", Coral);
        }

        private void ThemeMultiAndOptions()
        {
            RectTransform multi = FindRect("TitleMultiPanel");
            if (multi != null)
            {
                Image multiBackground = multi.GetComponent<Image>();
                if (multiBackground != null)
                {
                    multiBackground.color = new Color(Paper.r, Paper.g, Paper.b, 0.08f);
                    multiBackground.raycastTarget = false;
                }

                string[] sheets =
                {
                    "MultiChoiceScreenNote",
                    "MultiRandomScreenNote",
                    "MultiRoomScreenNote",
                    "MultiCreateRoomScreenNote",
                    "MultiJoinRoomScreenNote",
                    "MultiLobbyScreenNote"
                };
                for (int i = 0; i < sheets.Length; i++)
                {
                    RectTransform sheet = FindRect(multi, sheets[i]);
                    if (sheet == null)
                    {
                        continue;
                    }
                    SetImage(sheet, PaperRaised);
                    EnsureOutline(sheet.gameObject, 3f, 0.78f);
                    EnsureShadow(sheet.gameObject, new Vector2(7f, -8f), 0.2f);
                }

                // MultiMenuVisualPolisher owns the final bottom-bar geometry.
                // Re-apply it after the shared theme so the theme pass cannot
                // restore the old centered 700 x 520 popup layout.
                MultiMenuVisualPolisher polisher = multi.GetComponent<MultiMenuVisualPolisher>();
                polisher?.Polish();
            }

            RectTransform option = FindRect("TitleOptionPanel");
            if (option != null)
            {
                option.anchorMin = new Vector2(0.5f, 0f);
                option.anchorMax = new Vector2(0.5f, 0f);
                option.pivot = new Vector2(0.5f, 0f);
                option.anchoredPosition = new Vector2(0f, 16f);
                option.sizeDelta = new Vector2(720f, 480f);
                SetImage(option, Paper);
                EnsureOutline(option.gameObject, 3f, 0.76f);
                EnsureShadow(option.gameObject, new Vector2(7f, -8f), 0.2f);
                LayoutOptionPanel(option);
            }

            if (multi != null)
            {
                HideIfExists(multi, "MultiDoodles");
            }
        }

        private void LayoutOptionPanel(RectTransform panel)
        {
            RectTransform title = FindRect(panel, "TitleOptionTitle");
            PlaceOptionText(title, new Vector2(0f, 434f), new Vector2(640f, 42f), TextAnchor.MiddleCenter, 34, true);
            RectTransform subtitle = FindRect(panel, "TitleOptionSubtitle");
            if (subtitle != null) subtitle.gameObject.SetActive(false);

            float[] rowY = { 340f, 280f, 220f };
            Color[] rowColors =
            {
                new Color(1f, 0.97f, 0.84f, 0.76f),
                new Color(0.92f, 0.975f, 1f, 0.7f),
                new Color(0.98f, 0.93f, 1f, 0.7f)
            };
            for (int i = 0; i < rowY.Length; i++)
            {
                EnsureOptionRow(panel, "OptionRow" + i, rowY[i], rowColors[i]);
            }

            PlaceOptionText(FindRect(panel, "OptionBgmLabel"), new Vector2(-190f, rowY[0]), new Vector2(160f, 40f), TextAnchor.MiddleLeft, 20, true);
            RectTransform bgmSlider = FindRect(panel, "OptionBgmSlider");
            PlaceOptionRect(bgmSlider, new Vector2(35f, rowY[0]), new Vector2(250f, 36f));
            ThemeOptionSlider(bgmSlider, Cyan);
            PlaceOptionText(FindRect(panel, "OptionBgmValue"), new Vector2(218f, rowY[0]), new Vector2(72f, 36f), TextAnchor.MiddleRight, 18, true);

            PlaceOptionText(FindRect(panel, "OptionSeLabel"), new Vector2(-190f, rowY[1]), new Vector2(160f, 40f), TextAnchor.MiddleLeft, 20, true);
            RectTransform seSlider = FindRect(panel, "OptionSeSlider");
            PlaceOptionRect(seSlider, new Vector2(35f, rowY[1]), new Vector2(250f, 36f));
            ThemeOptionSlider(seSlider, Coral);
            PlaceOptionText(FindRect(panel, "OptionSeValue"), new Vector2(218f, rowY[1]), new Vector2(72f, 36f), TextAnchor.MiddleRight, 18, true);

            RectTransform languageLabel = FindRect(panel, "OptionLanguageLabel");
            PlaceOptionText(languageLabel, new Vector2(-190f, rowY[2]), new Vector2(160f, 40f), TextAnchor.MiddleLeft, 20, true);
            Text languageText = languageLabel != null ? languageLabel.GetComponent<Text>() : null;
            if (languageText != null)
            {
                LocalizedText localized = languageText.GetComponent<LocalizedText>();
                if (localized == null) localized = languageText.gameObject.AddComponent<LocalizedText>();
                localized.SetKey("option_language");
            }
            RectTransform japanese = FindRect(panel, "OptionJapaneseButton");
            RectTransform english = FindRect(panel, "OptionEnglishButton");
            PlaceOptionRect(japanese, new Vector2(-12f, rowY[2]), new Vector2(130f, 42f));
            PlaceOptionRect(english, new Vector2(138f, rowY[2]), new Vector2(130f, 42f));
            bool japaneseSelected = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese;
            ThemeOptionButton(japanese, japaneseSelected ? Cyan : PaperRaised, 18);
            ThemeOptionButton(english, japaneseSelected ? PaperRaised : Cyan, 18);

            HideIfExists(panel, "OptionKeysLabel");
            HideIfExists(panel, "OptionKeysValue");
            HideIfExists(panel, "OptionVibrationLabel");
            HideIfExists(panel, "OptionVibrationButton");
            HideIfExists(panel, "OptionLanguageValue");

            RectTransform back = FindRect(panel, "TitleOptionBackButton");
            PlaceOptionRect(back, new Vector2(0f, 48f), new Vector2(260f, 58f));
            ThemeOptionButton(back, Coral, 21);
            Text backLabel = back != null ? back.GetComponentInChildren<Text>(true) : null;
            if (backLabel != null)
            {
                LocalizedText localized = backLabel.GetComponent<LocalizedText>();
                if (localized == null) localized = backLabel.gameObject.AddComponent<LocalizedText>();
                localized.SetKey("option_back_esc");
            }

            RectTransform register = FindRect(panel, "OptionPlayerNameRegisterButton");
            PlaceOptionRect(register, new Vector2(0f, 48f), new Vector2(280f, 62f));
            ThemeOptionButton(register, Green, 22);

            BringOptionControlsForward(panel);
        }

        private void EnsureOptionRow(RectTransform panel, string name, float y, Color color)
        {
            Transform existing = panel.Find(name);
            RectTransform row;
            if (existing == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                row = obj.GetComponent<RectTransform>();
            }
            else
            {
                row = existing as RectTransform;
            }

            if (row == null)
            {
                return;
            }

            PlaceOptionRect(row, new Vector2(0f, y), new Vector2(580f, 48f));
            Image image = row.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            EnsureOutline(row.gameObject, 1f, 0.22f);
            row.SetAsFirstSibling();
        }

        private void PlaceOptionText(RectTransform rect, Vector2 position, Vector2 size, TextAnchor alignment, int fontSize, bool bold)
        {
            PlaceOptionRect(rect, position, size);
            Text text = rect != null ? rect.GetComponent<Text>() : null;
            if (text == null)
            {
                return;
            }

            text.font = fallbackFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = bold ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.72f);
        }

        private static void PlaceOptionRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
        }

        private void ThemeOptionButton(RectTransform rect, Color color, int fontSize)
        {
            if (rect == null)
            {
                return;
            }

            SetImage(rect, color);
            EnsureOutline(rect.gameObject, 2f, 0.7f);
            EnsureShadow(rect.gameObject, new Vector2(3f, -3f), 0.16f);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.font = fallbackFont;
                label.fontSize = fontSize;
                label.fontStyle = FontStyle.Bold;
                label.color = Ink;
            }

            for (int i = 0; i < rect.childCount; i++)
            {
                Transform child = rect.GetChild(i);
                if (child.name == "IconLine" || child.name == "IconDot" || child.name == "SoftFrame")
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void ThemeOptionSlider(RectTransform rect, Color accent)
        {
            if (rect == null) return;
            Slider slider = rect.GetComponent<Slider>();
            if (slider == null) return;

            // Keep the hand-drawn knob unchanged while it is hovered, pressed, or dragged.
            // Unity's Selectable transition otherwise tints/replaces the target graphic on pointer down.
            slider.transition = Selectable.Transition.None;

            RectTransform background = rect.Find("Background") as RectTransform;
            if (background != null)
            {
                background.anchorMin = new Vector2(0f, 0.5f);
                background.anchorMax = new Vector2(1f, 0.5f);
                background.pivot = new Vector2(0.5f, 0.5f);
                background.anchoredPosition = Vector2.zero;
                background.sizeDelta = new Vector2(-12f, 11f);
                Image backgroundImage = background.GetComponent<Image>();
                if (backgroundImage != null)
                {
                    backgroundImage.color = new Color(0.96f, 0.925f, 0.79f, 1f);
                    backgroundImage.raycastTarget = false;
                    backgroundImage.type = Image.Type.Simple;
                }
                EnsureOutline(background.gameObject, 1.2f, 0.58f);
                EnsureSliderTrackScribbles(background);
                background.SetAsFirstSibling();
            }

            RectTransform fill = slider.fillRect;
            RectTransform fillArea = fill != null ? fill.parent as RectTransform : null;
            if (fillArea != null)
            {
                fillArea.anchorMin = new Vector2(0f, 0.5f);
                fillArea.anchorMax = new Vector2(1f, 0.5f);
                fillArea.pivot = new Vector2(0.5f, 0.5f);
                fillArea.anchoredPosition = Vector2.zero;
                fillArea.sizeDelta = new Vector2(-12f, 7f);
                fillArea.SetSiblingIndex(Mathf.Min(1, rect.childCount - 1));
            }
            if (fill != null)
            {
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = new Color(accent.r, accent.g, accent.b, 0.9f);
                    fillImage.raycastTarget = false;
                    fillImage.type = Image.Type.Simple;
                }
                EnsureSliderFillScribbles(fill, accent);
            }

            RectTransform handle = slider.handleRect;
            RectTransform handleArea = handle != null ? handle.parent as RectTransform : null;
            if (handleArea != null)
            {
                handleArea.anchorMin = new Vector2(0f, 0.5f);
                handleArea.anchorMax = new Vector2(1f, 0.5f);
                handleArea.pivot = new Vector2(0.5f, 0.5f);
                handleArea.anchoredPosition = Vector2.zero;
                handleArea.sizeDelta = new Vector2(-12f, 28f);
                handleArea.SetAsLastSibling();
            }
            if (handle != null)
            {
                handle.anchorMin = new Vector2(handle.anchorMin.x, 0.5f);
                handle.anchorMax = new Vector2(handle.anchorMax.x, 0.5f);
                handle.pivot = new Vector2(0.5f, 0.5f);
                handle.anchoredPosition = new Vector2(handle.anchoredPosition.x, 0f);
                handle.sizeDelta = new Vector2(24f, 26f);
                handle.localRotation = Quaternion.Euler(0f, 0f, accent == Cyan ? -3.5f : 3.2f);
                Image handleImage = handle.GetComponent<Image>();
                if (handleImage != null)
                {
                    handleImage.sprite = DoodleRuntimeAssets.CircleSprite;
                    handleImage.color = accent;
                    handleImage.type = Image.Type.Simple;
                }
                EnsureOutline(handle.gameObject, 1.5f, 0.72f);
                EnsureShadow(handle.gameObject, new Vector2(2f, -2f), 0.16f);
                EnsureSliderKnobScribbles(handle, accent);
                EnsureSliderKnobDot(handle);
            }

            RectTransform ticks = EnsureOptionSliderTicks(rect);
            if (ticks != null)
            {
                int index = handleArea != null ? handleArea.GetSiblingIndex() : rect.childCount - 1;
                ticks.SetSiblingIndex(Mathf.Max(0, index));
            }

        }

        private static RectTransform EnsureOptionSliderTicks(RectTransform slider)
        {
            Transform existing = slider.Find("Crayon Volume Ticks");
            RectTransform root;
            if (existing == null)
            {
                GameObject obj = new GameObject("Crayon Volume Ticks", typeof(RectTransform));
                obj.transform.SetParent(slider, false);
                root = obj.GetComponent<RectTransform>();
            }
            else root = existing as RectTransform;
            if (root == null) return null;

            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(-12f, 18f);
            for (int i = 0; i <= 10; i++)
            {
                string name = "Tick " + i;
                Transform found = root.Find(name);
                RectTransform tick;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(root, false);
                    tick = obj.GetComponent<RectTransform>();
                }
                else tick = found as RectTransform;
                if (tick == null) continue;
                float anchor = i / 10f;
                tick.anchorMin = new Vector2(anchor, 0.5f);
                tick.anchorMax = new Vector2(anchor, 0.5f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.anchoredPosition = new Vector2(0f, (i % 3 - 1) * 0.8f);
                tick.sizeDelta = new Vector2(i % 5 == 0 ? 2.2f : 1.4f,
                    (i % 5 == 0 ? 15f : 10f) + i % 2 * 1.8f);
                tick.localRotation = Quaternion.Euler(0f, 0f, (i % 4 - 1.5f) * 2.2f);
                Image image = tick.GetComponent<Image>();
                image.color = new Color(Ink.r, Ink.g, Ink.b, i % 5 == 0 ? 0.42f : 0.22f);
                image.raycastTarget = false;
            }
            return root;
        }

        private static void EnsureSliderTrackScribbles(RectTransform background)
        {
            for (int i = 0; i < 2; i++)
            {
                string name = "Pencil Track " + i;
                Transform found = background.Find(name);
                RectTransform stroke;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(background, false);
                    stroke = obj.GetComponent<RectTransform>();
                }
                else stroke = found as RectTransform;
                if (stroke == null) continue;
                stroke.anchorMin = new Vector2(0f, 0.5f);
                stroke.anchorMax = new Vector2(1f, 0.5f);
                stroke.pivot = new Vector2(0.5f, 0.5f);
                stroke.anchoredPosition = new Vector2(0f, i == 0 ? -3.1f : 3.2f);
                stroke.sizeDelta = new Vector2(-3f, 1.2f);
                stroke.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -0.45f : 0.35f);
                Image image = stroke.GetComponent<Image>();
                image.color = new Color(Ink.r, Ink.g, Ink.b, 0.34f);
                image.raycastTarget = false;
            }
        }

        private static void EnsureSliderFillScribbles(RectTransform fill, Color accent)
        {
            Image baseImage = fill.GetComponent<Image>();
            if (baseImage != null) baseImage.color = new Color(accent.r, accent.g, accent.b, 0.48f);
            for (int i = 0; i < 3; i++)
            {
                string name = "Crayon Stroke " + i;
                Transform found = fill.Find(name);
                RectTransform stroke;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(fill, false);
                    stroke = obj.GetComponent<RectTransform>();
                }
                else stroke = found as RectTransform;
                if (stroke == null) continue;
                stroke.anchorMin = new Vector2(0f, 0.5f);
                stroke.anchorMax = new Vector2(1f, 0.5f);
                stroke.pivot = new Vector2(0.5f, 0.5f);
                stroke.anchoredPosition = new Vector2(i - 1f, (i - 1f) * 1.7f);
                stroke.sizeDelta = new Vector2(-1f - i, 2.7f);
                stroke.localRotation = Quaternion.Euler(0f, 0f, (i - 1f) * 0.65f);
                Image image = stroke.GetComponent<Image>();
                image.color = new Color(accent.r, accent.g, accent.b, 0.55f);
                image.raycastTarget = false;
            }
        }

        private static void EnsureSliderKnobScribbles(RectTransform handle, Color accent)
        {
            for (int i = 0; i < 2; i++)
            {
                string name = "Knob Crayon " + i;
                Transform found = handle.Find(name);
                RectTransform scribble;
                if (found == null)
                {
                    GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(handle, false);
                    scribble = obj.GetComponent<RectTransform>();
                }
                else scribble = found as RectTransform;
                if (scribble == null) continue;
                scribble.anchorMin = scribble.anchorMax = new Vector2(0.5f, 0.5f);
                scribble.pivot = new Vector2(0.5f, 0.5f);
                scribble.anchoredPosition = i == 0 ? new Vector2(-1.6f, 1.1f) : new Vector2(1.3f, -1.4f);
                scribble.sizeDelta = i == 0 ? new Vector2(19f, 22f) : new Vector2(21f, 18f);
                Image image = scribble.GetComponent<Image>();
                image.sprite = DoodleRuntimeAssets.CircleSprite;
                image.color = new Color(accent.r, accent.g, accent.b, 0.46f);
                image.raycastTarget = false;
            }
        }

        private static void EnsureSliderKnobDot(RectTransform handle)
        {
            Transform existing = handle.Find("Knob Highlight");
            RectTransform dot;
            if (existing == null)
            {
                GameObject obj = new GameObject("Knob Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(handle, false);
                dot = obj.GetComponent<RectTransform>();
            }
            else dot = existing as RectTransform;
            if (dot == null) return;
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = new Vector2(-2f, 2f);
            dot.sizeDelta = new Vector2(7f, 7f);
            Image image = dot.GetComponent<Image>();
            image.sprite = DoodleRuntimeAssets.CircleSprite;
            image.color = new Color(1f, 0.98f, 0.88f, 0.88f);
            image.raycastTarget = false;
        }

        private static void BringOptionControlsForward(RectTransform panel)
        {
            string[] names =
            {
                "TitleOptionTitle", "TitleOptionSubtitle",
                "OptionBgmLabel", "OptionBgmSlider", "OptionBgmValue",
                "OptionSeLabel", "OptionSeSlider", "OptionSeValue",
                "OptionLanguageLabel", "OptionJapaneseButton", "OptionEnglishButton",
                "OptionPlayerNameLabel", "OptionPlayerNameInput", "OptionPlayerNameError",
                "OptionPlayerNameRegisterButton", "TitleOptionBackButton"
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rect = FindRect(panel, names[i]);
                if (rect != null)
                {
                    rect.SetAsLastSibling();
                }
            }
        }

        private void ThemeGameplayHud()
        {
            RectTransform hud = FindRect("GameplayHud");
            if (hud == null)
            {
                return;
            }

            RectTransform hints = FindRect(hud, "GameplayKeyHints");
            if (hints != null)
            {
                hints.anchoredPosition = new Vector2(18f, 16f);
            }

            RectTransform drawer = FindRect(hud, "GameplayActionDrawer");
            if (drawer != null)
            {
                SetImage(drawer, new Color(1f, 0.975f, 0.89f, 0.97f));
                EnsureOutline(drawer.gameObject, 3f, 0.8f);
                EnsureShadow(drawer.gameObject, new Vector2(8f, -8f), 0.2f);
                EnsureTape(drawer, new Vector2(0f, 218f), -2f, Yellow);
            }

            RectTransform tab = FindRect(hud, "GameplayDrawerTabButton");
            RectTransform esc = FindRect(hud, "GameplayEscHintButton");
            ThemeKeyChip(tab, Yellow);
            ThemeKeyChip(esc, Cyan);
        }

        private void ThemeStageEditor()
        {
            RectTransform panel = FindRect("RuntimeStageEditorPanel");
            if (panel == null)
            {
                return;
            }

            StageEditorVisualPolisher polisher = panel.GetComponent<StageEditorVisualPolisher>();
            if (polisher == null)
            {
                polisher = panel.gameObject.AddComponent<StageEditorVisualPolisher>();
            }
            polisher.Polish();

            EnsureBackdrop(panel, "StageEditorBackdrop", new Color(0.4f, 0.72f, 0.92f, 0.035f));
            RectTransform list = FindRect(panel, "RuntimeStageEditorListPanel");
            RectTransform tools = FindRect(panel, "RuntimeStageEditorTools");
            ThemeWorkspaceCard(list, new Color(0.96f, 0.985f, 1f, 0.96f), Cyan);
            ThemeWorkspaceCard(tools, new Color(1f, 0.97f, 0.86f, 0.97f), Yellow);

            RectTransform title = FindRect(panel, "RuntimeStageEditorTitle");
            if (title != null)
            {
                Text titleText = title.GetComponent<Text>();
                if (titleText != null)
                {
                    titleText.fontSize = Mathf.Max(30, titleText.fontSize);
                    titleText.fontStyle = FontStyle.Bold;
                    titleText.color = Ink;
                }

                EnsureHighlight(title, Violet);
            }
        }

        private void ThemeMenuAndResults()
        {
            string[] panels = { "MenuPanel", "StageClearResult", "StageSelectLockedPanel" };
            for (int i = 0; i < panels.Length; i++)
            {
                RectTransform panel = FindRect(panels[i]);
                if (panel == null)
                {
                    continue;
                }

                SetImage(panel, PaperRaised);
                EnsureOutline(panel.gameObject, 3.2f, 0.82f);
                EnsureShadow(panel.gameObject, new Vector2(9f, -10f), 0.22f);
                if (panels[i] != "StageClearResult")
                {
                    EnsureTape(panel, new Vector2(0f, panel.rect.height * 0.5f), -3f, Yellow);
                }
            }
        }

        private void ThemeAllButtons()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                if (rect == null || rect.sizeDelta.x < 20f || rect.sizeDelta.y < 18f)
                {
                    continue;
                }

                EnsureOutline(button.gameObject, 2.1f, 0.72f);
                EnsureShadow(button.gameObject, new Vector2(4f, -4f), 0.18f);
                Color baseColor = button.targetGraphic != null ? button.targetGraphic.color : PaperRaised;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Brighten(baseColor, 0.14f);
                colors.pressedColor = Darken(baseColor, 0.12f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.72f, 0.7f, 0.65f, 0.56f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.09f;
                button.colors = colors;

                DoodleButtonMotion motion = button.GetComponent<DoodleButtonMotion>();
                if (motion == null)
                {
                    motion = button.gameObject.AddComponent<DoodleButtonMotion>();
                }

                rect.localRotation = Quaternion.identity;
                motion.Configure(0f);

                StageCardHover legacyHover = button.GetComponent<StageCardHover>();
                if (legacyHover != null)
                {
                    legacyHover.enabled = false;
                }
            }
        }

        private void ThemeAllText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text.name.Contains("Placeholder"))
                {
                    continue;
                }

                if (text.color.a > 0.65f && Luminance(text.color) < 0.32f)
                {
                    text.color = new Color(Ink.r, Ink.g, Ink.b, text.color.a);
                }

                if (text.fontSize >= 21)
                {
                    text.fontStyle = FontStyle.Bold;
                }
            }
        }

        private void StraightenAllScreens()
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null)
                {
                    continue;
                }

                string objectName = rect.name;
                if (IsLooseDecoration(objectName))
                {
                    rect.gameObject.SetActive(false);
                    continue;
                }

                if (objectName.EndsWith("MarkerHighlight", System.StringComparison.Ordinal))
                {
                    rect.localRotation = Quaternion.identity;
                }

                Image surface = rect.GetComponent<Image>();
                bool isUiSurface = surface != null && rect.rect.width >= 80f && rect.rect.height >= 28f;
                if (isUiSurface)
                {
                    rect.localRotation = Quaternion.identity;
                    HideDirectSketchFrame(rect);
                }

                Button button = rect.GetComponent<Button>();
                if (button != null)
                {
                    rect.localRotation = Quaternion.identity;
                    DoodleButtonMotion motion = button.GetComponent<DoodleButtonMotion>();
                    if (motion != null)
                    {
                        motion.Configure(0f);
                    }
                }

                if (objectName == "NotebookRule")
                {
                    rect.localRotation = Quaternion.identity;
                }
                else if (objectName == "MarginRule")
                {
                    rect.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
        }

        private static bool IsLooseDecoration(string objectName)
        {
            return objectName.IndexOf("MaskingTape", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Doodles", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("SoftFrame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("BoldFrame", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("FrameLine", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("CrayonFill", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("SelectionScribble", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("HoverScribble", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void HideDirectSketchFrame(RectTransform surface)
        {
            bool removedFrame = false;
            float horizontalThreshold = Mathf.Max(64f, surface.rect.width * 0.45f);
            float verticalThreshold = Mathf.Max(36f, surface.rect.height * 0.45f);
            for (int i = 0; i < surface.childCount; i++)
            {
                RectTransform line = surface.GetChild(i) as RectTransform;
                if (line == null || line.name != "IconLine")
                {
                    continue;
                }

                float angle = Mathf.Abs(Mathf.DeltaAngle(0f, line.localEulerAngles.z));
                bool vertical = angle > 45f && angle < 135f;
                float threshold = vertical ? verticalThreshold : horizontalThreshold;
                if (line.rect.width < threshold)
                {
                    continue;
                }

                line.gameObject.SetActive(false);
                removedFrame = true;
            }

            if (removedFrame)
            {
                EnsureOutline(surface.gameObject, 2f, 0.72f);
            }
        }

        private void ThemeWorkspaceCard(RectTransform rect, Color background, Color accent)
        {
            if (rect == null)
            {
                return;
            }

            SetImage(rect, background);
            EnsureOutline(rect.gameObject, 2.8f, 0.76f);
            EnsureShadow(rect.gameObject, new Vector2(7f, -7f), 0.18f);
            EnsureTape(rect, new Vector2(0f, rect.rect.height * 0.5f), -3f, accent);
        }

        private void ThemeKeyChip(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            SetImage(rect, color);
            EnsureOutline(rect.gameObject, 2.2f, 0.86f);
            EnsureShadow(rect.gameObject, new Vector2(3f, -3f), 0.2f);
        }

        private void SetButtonLayout(RectTransform parent, string name, Vector2 position, Vector2 size, Color color, float rotation)
        {
            RectTransform rect = FindRect(parent, name);
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
            SetImage(rect, color);
            EnsureOutline(rect.gameObject, 2.5f, 0.84f);
            EnsureShadow(rect.gameObject, new Vector2(5f, -5f), 0.21f);

            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = name == "TitleExitButton" ? 20 : 23;
                label.fontStyle = FontStyle.Bold;
                label.color = Ink;
            }
        }

        private void SetFloatingButton(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            SetImage(rect, color);
        }

        private void SetNamedButtonColor(RectTransform parent, string name, Color color)
        {
            RectTransform button = FindRect(parent, name);
            if (button != null)
            {
                SetImage(button, color);
            }
        }

        private static void LayoutBottomSheet(RectTransform sheet, Vector2 size)
        {
            if (sheet == null)
            {
                return;
            }

            sheet.anchorMin = new Vector2(0.5f, 0.5f);
            sheet.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.anchoredPosition = new Vector2(0f, 20f);
            sheet.sizeDelta = size;
            SetImage(sheet, PaperRaised);
            EnsureOutline(sheet.gameObject, 3f, 0.78f);
            EnsureShadow(sheet.gameObject, new Vector2(7f, -8f), 0.2f);
        }

        private static void HideIfExists(Transform parent, string name)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect != null)
            {
                rect.gameObject.SetActive(false);
            }
        }

        private void EnsureBackdrop(RectTransform parent, string name, Color tint)
        {
            Transform existing = parent.Find(name);
            RectTransform root;
            if (existing == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform));
                obj.transform.SetParent(parent, false);
                root = obj.GetComponent<RectTransform>();
                Stretch(root);
                root.SetAsFirstSibling();
            }
            else
            {
                root = existing as RectTransform;
            }

            if (root == null || root.childCount > 0)
            {
                return;
            }

            for (int i = 0; i < 9; i++)
            {
                float y = -280f + i * 70f;
                CreateLine(root, new Vector2(-620f, y), new Vector2(620f, y), 1.2f, tint, "NotebookRule");
            }

            CreateLine(root, new Vector2(-548f, -350f), new Vector2(-548f, 350f), 2f, new Color(Coral.r, Coral.g, Coral.b, tint.a * 1.45f), "MarginRule");
        }

        private void EnsureDoodleCluster(RectTransform parent, string name, Vector2 position, float scale)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private Text EnsureText(RectTransform parent, string name, string value, int size, TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                obj.transform.SetParent(parent, false);
                text = obj.GetComponent<Text>();
            }

            text.font = fallbackFont;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private void EnsureHighlight(RectTransform textRect, Color color)
        {
            if (textRect == null || textRect.parent == null)
            {
                return;
            }

            string markerName = textRect.name + "MarkerHighlight";
            Transform existing = textRect.parent.Find(markerName);
            RectTransform marker;
            if (existing == null)
            {
                GameObject obj = new GameObject(markerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(textRect.parent, false);
                marker = obj.GetComponent<RectTransform>();
            }
            else
            {
                marker = existing as RectTransform;
            }

            if (marker == null)
            {
                return;
            }

            marker.anchorMin = textRect.anchorMin;
            marker.anchorMax = textRect.anchorMax;
            marker.pivot = textRect.pivot;
            marker.anchoredPosition = textRect.anchoredPosition + new Vector2(0f, -textRect.sizeDelta.y * 0.2f);
            marker.sizeDelta = new Vector2(textRect.sizeDelta.x * 0.84f, Mathf.Max(10f, textRect.sizeDelta.y * 0.28f));
            marker.localRotation = Quaternion.identity;
            marker.SetSiblingIndex(Mathf.Max(0, textRect.GetSiblingIndex()));
            Image image = marker.GetComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.52f);
            image.raycastTarget = false;
        }

        private void EnsureTape(RectTransform parent, Vector2 position, float rotation, Color color)
        {
            Transform existing = parent.Find("ModernMaskingTape");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private static void CreateCircle(RectTransform parent, Vector2 center, float diameter, float width, Color color)
        {
            const int segments = 28;
            float radius = diameter * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                Vector2 from = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                Vector2 to = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                CreateLine(parent, from, to, width, new Color(color.r, color.g, color.b, 0.5f), "DoodleCircle");
            }
        }

        private static void CreateLine(RectTransform parent, Vector2 from, Vector2 to, float width, Color color, string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }

        private static void SetImage(RectTransform rect, Color color)
        {
            Image image = rect != null ? rect.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void EnsureOutline(GameObject target, float distance, float alpha)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private static void EnsureShadow(GameObject target, Vector2 distance, float alpha)
        {
            Shadow[] effects = target.GetComponents<Shadow>();
            Shadow shadow = null;
            for (int i = 0; i < effects.Length; i++)
            {
                if (!(effects[i] is Outline))
                {
                    shadow = effects[i];
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private Font FindFont()
        {
            Text text = GetComponentInChildren<Text>(true);
            if (text != null && text.font != null)
            {
                return text.font;
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private RectTransform FindRect(string name)
        {
            return FindRect(transform, name);
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color Brighten(Color color, float amount)
        {
            return new Color(
                Mathf.Lerp(color.r, 1f, amount),
                Mathf.Lerp(color.g, 1f, amount),
                Mathf.Lerp(color.b, 1f, amount),
                color.a);
        }

        private static Color Darken(Color color, float amount)
        {
            return new Color(color.r * (1f - amount), color.g * (1f - amount), color.b * (1f - amount), color.a);
        }

        private static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }
    }

    internal static class LegacyPencilStrokeBatcher
    {
        private sealed class Batch
        {
            public Material Material;
            public int SortingLayerId;
            public int SortingOrder;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
        }

        private static bool completed;

        public static void BatchScene()
        {
            if (completed || !Application.isPlaying)
            {
                return;
            }

            completed = true;
            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Dictionary<long, Batch> batches = new Dictionary<long, Batch>();
            List<GameObject> obsolete = new List<GameObject>();
            for (int i = 0; i < lines.Length; i++)
            {
                LineRenderer line = lines[i];
                if (line == null || !IsLegacyPencilStroke(line.name) || line.positionCount < 2)
                {
                    continue;
                }

                Material material = line.sharedMaterial;
                int materialId = material != null ? material.GetInstanceID() : 0;
                long key = ((long)line.sortingLayerID << 32)
                    ^ ((long)(line.sortingOrder & 0xffff) << 16)
                    ^ (uint)materialId;
                if (!batches.TryGetValue(key, out Batch batch))
                {
                    batch = new Batch
                    {
                        Material = material,
                        SortingLayerId = line.sortingLayerID,
                        SortingOrder = line.sortingOrder
                    };
                    batches.Add(key, batch);
                }

                float widthScale = line.useWorldSpace
                    ? 1f
                    : Mathf.Max(Mathf.Abs(line.transform.lossyScale.x), Mathf.Abs(line.transform.lossyScale.y));
                for (int pointIndex = 1; pointIndex < line.positionCount; pointIndex++)
                {
                    Vector3 from = line.GetPosition(pointIndex - 1);
                    Vector3 to = line.GetPosition(pointIndex);
                    if (!line.useWorldSpace)
                    {
                        from = line.transform.TransformPoint(from);
                        to = line.transform.TransformPoint(to);
                    }

                    float amount = pointIndex / (float)(line.positionCount - 1);
                    Color fromColor = Color.Lerp(line.startColor, line.endColor, (pointIndex - 1f) / (line.positionCount - 1f));
                    Color toColor = Color.Lerp(line.startColor, line.endColor, amount);
                    float width = Mathf.Lerp(line.startWidth, line.endWidth, amount) * widthScale;
                    AppendQuad(batch, from, to, width, fromColor, toColor);
                }

                line.enabled = false;
                line.gameObject.SetActive(false);
                obsolete.Add(line.gameObject);
            }

            if (batches.Count == 0)
            {
                return;
            }

            GameObject root = new GameObject("Batched Legacy Pencil Fills");
            int batchIndex = 0;
            foreach (Batch batch in batches.Values)
            {
                GameObject visual = new GameObject("Pencil Fill Batch " + batchIndex++, typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(root.transform, false);
                Mesh mesh = new Mesh { name = visual.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(batch.Vertices);
                mesh.SetColors(batch.Colors);
                mesh.SetTriangles(batch.Triangles, 0);
                mesh.RecalculateBounds();
                visual.GetComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = visual.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.Material != null
                    ? batch.Material
                    : new Material(Shader.Find("Sprites/Default"));
                renderer.sortingLayerID = batch.SortingLayerId;
                renderer.sortingOrder = batch.SortingOrder;
            }

            for (int i = 0; i < obsolete.Count; i++)
            {
                Object.Destroy(obsolete[i]);
            }
        }

        private static bool IsLegacyPencilStroke(string objectName)
        {
            return objectName != null
                && objectName.Contains("Pencil Fill", System.StringComparison.Ordinal)
                && (objectName.Contains("Pencil Stroke", System.StringComparison.Ordinal)
                    || objectName.Contains("Soft Horizontal Grain", System.StringComparison.Ordinal));
        }

        private static void AppendQuad(Batch batch, Vector3 from, Vector3 to, float width, Color fromColor, Color toColor)
        {
            Vector2 delta = (Vector2)(to - from);
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int first = batch.Vertices.Count;
            batch.Vertices.Add(from + (Vector3)normal);
            batch.Vertices.Add(from - (Vector3)normal);
            batch.Vertices.Add(to + (Vector3)normal);
            batch.Vertices.Add(to - (Vector3)normal);
            batch.Colors.Add(fromColor);
            batch.Colors.Add(fromColor);
            batch.Colors.Add(toColor);
            batch.Colors.Add(toColor);
            batch.Triangles.Add(first);
            batch.Triangles.Add(first + 2);
            batch.Triangles.Add(first + 1);
            batch.Triangles.Add(first + 2);
            batch.Triangles.Add(first + 3);
            batch.Triangles.Add(first + 1);
        }
    }

    public sealed class DoodleButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        private RectTransform rect;
        private Vector3 restingScale = Vector3.one;
        private float restingRotation;
        private float targetScale = 1f;
        private float targetRotation;

        public void Configure(float rotation)
        {
            rect = rect != null ? rect : transform as RectTransform;
            restingScale = Vector3.one;
            restingRotation = NormalizeAngle(rotation);
            targetRotation = restingRotation;
        }

        private void Awake()
        {
            rect = transform as RectTransform;
            restingScale = rect != null ? rect.localScale : Vector3.one;
            restingRotation = rect != null ? NormalizeAngle(rect.localRotation.eulerAngles.z) : 0f;
            targetRotation = restingRotation;
        }

        private void OnEnable()
        {
            targetScale = 1f;
            targetRotation = restingRotation;
        }

        private void Update()
        {
            if (rect == null)
            {
                return;
            }

            float t = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            rect.localScale = Vector3.Lerp(rect.localScale, restingScale * targetScale, t);
            float z = Mathf.LerpAngle(rect.localRotation.eulerAngles.z, targetRotation, t);
            rect.localRotation = Quaternion.Euler(0f, 0f, z);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
            GameSfx.Play(SfxId.UiButtonHover);
        }
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = 0.96f;
            targetRotation = restingRotation;
            GameSfx.Play(SfxId.UiButtonPress);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetHovered(true);
        }

        private void SetHovered(bool hovered)
        {
            targetScale = hovered ? 1.055f : 1f;
            targetRotation = restingRotation;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
