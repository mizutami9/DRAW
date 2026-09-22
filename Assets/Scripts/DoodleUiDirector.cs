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
        private Font defaultFont;
        private bool themeApplied;

        private void Awake()
        {
            defaultFont = FindFont();
            fallbackFont = LocalizationManager.LoadCurrentFont(defaultFont);
            LegacyPencilStrokeBatcher.BatchScene();
            ApplyTheme();
        }

        private void Start()
        {
            // Other components create some controls (for example the OPTION player-name
            // controls) during Awake. Apply the same focused layout pass after every
            // Awake has completed so the initial screen matches a language refresh.
            ThemeMultiAndOptions();
            ThemeGameplayHud();
            ThemeMenuAndResults();
            ApplyAllButtonLabelSpacing();
            NormalizePaperSurfaceEffects();
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
            ThemeTitle();
            ThemeGameplayHud();
            ThemeMenuAndResults();
            ThemeAllButtons();
            NormalizeBackButtonLabels();
            RectTransform draw = FindRect("DrawPanel");
            draw?.GetComponent<DrawScreenVisualPolisher>()?.Polish();
            RectTransform multi = FindRect("TitleMultiPanel");
            multi?.GetComponent<MultiMenuVisualPolisher>()?.Polish();
            ApplyAllButtonLabelSpacing();
            NormalizePaperSurfaceEffects();
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
            defaultFont = defaultFont != null ? defaultFont : FindFont();
            fallbackFont = LocalizationManager.LoadCurrentFont(defaultFont);
            ThemeStageBackgroundDoodles();
            ThemeTitle();
            ThemeStageSelect();
            ThemeDrawScreen();
            ThemeMultiAndOptions();
            ThemeGameplayHud();
            ThemeStageEditor();
            ThemeMenuAndResults();
            ThemeAllButtons();
            NormalizeBackButtonLabels();
            ThemeAllText();
            ConfigureLocalizedTextSafety();
            StraightenAllScreens();

            // The generic button/text passes above intentionally cover every
            // screen, but stage cards have a stricter shared layout. Reapply
            // that layout last so the initially visible WORLD 1-5 and pages
            // shown later use exactly the same type size and frame weight.
            RectTransform stageSelect = FindRect("StageSelectPanel");
            StageSelectVisualPolisher stagePolisher = stageSelect != null
                ? stageSelect.GetComponent<StageSelectVisualPolisher>()
                : null;
            stagePolisher?.Polish();

            // DRAW has interactive child graphics and a denser bespoke layout.
            // Reapply it after the generic passes so common button styling cannot
            // cover the notebook tabs, tool icons, or localized type settings.
            RectTransform draw = FindRect("DrawPanel");
            draw?.GetComponent<DrawScreenVisualPolisher>()?.Polish();

            // Multiplayer screens swap several full-screen children at runtime.
            // Re-apply their shared geometry after the generic passes so the
            // image headings remain above the sheets and the fallback labels
            // do not get re-enabled by the localization/text pass.
            RectTransform multi = FindRect("TitleMultiPanel");
            multi?.GetComponent<MultiMenuVisualPolisher>()?.Polish();

            // Dedicated screen polishers run after the generic theme and may
            // rebuild their labels. Finish with one bounded fit pass: captions
            // receive a wider inset, and only cramped buttons grow slightly.
            ApplyAllButtonLabelSpacing();
            NormalizePaperSurfaceEffects();
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

            // The live title stage already draws notebook rules.  A second UI rule
            // layer and the former decorated border made the scene unnecessarily busy.
            HideIfExists(panel, "TitleBackdrop");
            EnsureTitleScrapbookDecorations(panel);

            RectTransform logo = FindRect(panel, "TitleNicoDrowLogo");
            if (logo != null)
            {
                // The alpha art occupies only the centre of this source image.  This
                // rect places the visible logo in the band between the title stage
                // and the bottom controls. A small intentional overlap makes the mark
                // read as the main title instead of another compact UI label.
                float panelHeight = panel.rect.height > 1f ? panel.rect.height : 720f;
                const float menuTop = 128f;
                float stageLowerEdge = panelHeight * 0.4703f;
                float availableHeight = Mathf.Max(28f, stageLowerEdge - menuTop);
                float signScale = Mathf.Clamp((availableHeight + 10f) / 230f, 0.24f, 1f);
                float signCenterY = (stageLowerEdge + menuTop) * 0.5f
                    + Mathf.Min(4f, availableHeight * 0.025f);

                logo.anchorMin = new Vector2(0.5f, 0f);
                logo.anchorMax = new Vector2(0.5f, 0f);
                logo.pivot = new Vector2(0.5f, 0.5f);
                logo.anchoredPosition = new Vector2(0f, signCenterY);
                logo.sizeDelta = new Vector2(1320f, 420f);
                logo.localRotation = Quaternion.identity;
                logo.localScale = Vector3.one * signScale;
                EnsureTitleLogoMount(panel, logo);
            }

            RectTransform menu = FindRect(panel, "TitleMenuBar");
            if (menu != null)
            {
                TrailerDebugMenuController.Ensure(panel, menu);
                menu.anchorMin = new Vector2(0f, 0f);
                menu.anchorMax = new Vector2(1f, 0f);
                menu.pivot = new Vector2(0.5f, 0f);
                menu.anchoredPosition = new Vector2(0f, 10f);
                menu.sizeDelta = new Vector2(-38f, 118f);
                // A single quiet paper shelf replaces the former full-screen collage.
                // It keeps the controls grounded without competing with the stage.
                SetPaperSurface(menu, new Color(1f, 0.985f, 0.925f, 0.9f));
                HideIfExists(menu, "TornPaperEdges");

                SetTitleMenuButton(menu, "TitleSingleButton", new Vector2(-400f, 24f), Green, TitleMenuIcon.Play);
                SetTitleMenuButton(menu, "TitleMultiButton", new Vector2(-230f, 24f), Cyan, TitleMenuIcon.Group);
                SetTitleMenuButton(menu, "TitleDrawButton", new Vector2(-60f, 22f), Yellow, TitleMenuIcon.Pencil);
                SetTitleMenuButton(menu, "TitleOptionButton", new Vector2(110f, 24f), Violet, TitleMenuIcon.Gear);
                SetTitleMenuButton(menu, "TitleExitButton", new Vector2(280f, 24f), Coral, TitleMenuIcon.Exit);
                HideIfExists(menu, "TitleDebugButton");
                menu.SetAsLastSibling();
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
            EnsureStageSelectScrapbookDecorations(panel);
            Text heading = EnsureText(panel, "ModernStageSelectTitle", LocalizationManager.T("stage_select"), 38, TextAnchor.MiddleLeft);
            LocalizedText localizedHeading = heading.GetComponent<LocalizedText>();
            if (localizedHeading == null)
            {
                localizedHeading = heading.gameObject.AddComponent<LocalizedText>();
            }
            localizedHeading.SetKey("stage_select");
            heading.fontStyle = FontStyle.Bold;
            heading.color = Ink;
            RectTransform headingRect = heading.rectTransform;
            headingRect.anchorMin = new Vector2(0f, 1f);
            headingRect.anchorMax = new Vector2(0f, 1f);
            headingRect.pivot = new Vector2(0f, 1f);
            headingRect.anchoredPosition = new Vector2(42f, -24f);
            headingRect.sizeDelta = new Vector2(430f, 58f);
            EnsurePaintStrokeHighlight(headingRect, Cyan);
            ConfigureStageSelectTitleGraphic(panel, headingRect);

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
                SetPaperSurface(card, tint);
                EnsureOutline(card.gameObject, 2.8f, 0.68f);
                EnsureShadow(card.gameObject, new Vector2(6f, -7f), 0.18f);
                EnsureTape(card, new Vector2(0f, 164f), (i % 5 - 2) * 2f, i % 2 == 0 ? Cyan : Yellow);
            }

            SetFloatingButton(panel, "StageSelectPreviousPageButton", new Vector2(-92f, 78f), new Vector2(64f, 52f), Cyan);
            SetFloatingButton(panel, "StageSelectNextPageButton", new Vector2(92f, 78f), new Vector2(64f, 52f), Cyan);
            SetFloatingButton(panel, "StageSelectBackButton", new Vector2(528f, 38f), new Vector2(172f, 56f), Coral);
            SetFloatingButton(panel, "StageSelectEditModeButton", new Vector2(-528f, 38f), new Vector2(188f, 56f), Violet);
        }

        private static void EnsureStageSelectScrapbookDecorations(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            QuietMenuBackdrop.Apply(panel, "StageSelectScrapbookDecorations",
                QuietMenuBackdropPreset.StageSelect);
        }

        private static void ConfigureStageSelectTitleGraphic(RectTransform panel, RectTransform fallbackTitle)
        {
            if (panel == null)
            {
                return;
            }

            Sprite titleSprite = Resources.Load<Sprite>("UI/stage-title-crayon-v1");
            Transform existing = panel.Find("StageSelectTitleGraphic");
            RectTransform graphic;
            if (existing == null)
            {
                GameObject obj = new GameObject("StageSelectTitleGraphic", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                graphic = obj.GetComponent<RectTransform>();
            }
            else
            {
                graphic = existing as RectTransform;
            }
            if (graphic == null)
            {
                return;
            }

            graphic.anchorMin = new Vector2(0.5f, 1f);
            graphic.anchorMax = new Vector2(0.5f, 1f);
            graphic.pivot = new Vector2(0.5f, 1f);
            // Keep the title inside the dedicated header band.  The stage cards are
            // lowered by StageSelectVisualPolisher, leaving clear space below this art.
            graphic.anchoredPosition = new Vector2(0f, -36f);
            graphic.sizeDelta = new Vector2(330f, 116f);
            graphic.localRotation = Quaternion.identity;

            Image image = graphic.GetComponent<Image>();
            if (image == null)
            {
                image = graphic.gameObject.AddComponent<Image>();
            }
            image.sprite = titleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            graphic.gameObject.SetActive(titleSprite != null);
            graphic.SetAsLastSibling();

            Text fallbackText = fallbackTitle != null ? fallbackTitle.GetComponent<Text>() : null;
            if (fallbackText != null)
            {
                fallbackText.enabled = titleSprite == null;
            }

            RectTransform fallbackStroke = panel.Find("ModernStageSelectTitleMarkerHighlight") as RectTransform;
            if (fallbackStroke != null)
            {
                fallbackStroke.gameObject.SetActive(titleSprite == null);
            }
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
                SetPaperSurface(partBar, new Color(1f, 0.97f, 0.86f, 0.96f));
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
                    // Multiplayer menus are an overlay on the live title-stage.
                    // Keep the root transparent so the playable background remains
                    // fully visible; only the bottom control sheet is opaque paper.
                    multiBackground.color = Color.clear;
                    multiBackground.raycastTarget = false;
                }

                EnsureMultiScrapbookDecorations(multi);
                HideIfExists(multi, "MultiNotebookBackdrop");

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
                    SetPaperSurface(sheet, PaperRaised);
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
                option.anchorMin = Vector2.zero;
                option.anchorMax = Vector2.one;
                option.pivot = new Vector2(0.5f, 0.5f);
                option.anchoredPosition = Vector2.zero;
                option.offsetMin = Vector2.zero;
                option.offsetMax = Vector2.zero;
                SetImage(option, Paper);
                DisableGraphicEffects(option.gameObject);
                LayoutOptionPanel(option);
            }

            if (multi != null)
            {
                HideIfExists(multi, "MultiDoodles");
            }
        }

        private static void EnsureMultiScrapbookDecorations(RectTransform panel)
        {
            if (panel == null)
            {
                return;
            }

            QuietMenuBackdrop.Apply(panel, "MultiScrapbookDecorations",
                QuietMenuBackdropPreset.Multi);
        }

        private void LayoutOptionPanel(RectTransform panel)
        {
            EnsureOptionScrapbookDecorations(panel);

            RectTransform title = FindRect(panel, "TitleOptionTitle");
            PlaceOptionText(title, new Vector2(0f, 625f), new Vector2(700f, 72f),
                TextAnchor.MiddleCenter, 52, true);
            Text titleText = title != null ? title.GetComponent<Text>() : null;
            if (titleText != null)
            {
                LocalizedText localized = titleText.GetComponent<LocalizedText>();
                if (localized != null) localized.enabled = false;
                titleText.supportRichText = true;
                titleText.text = BuildRainbowOptionTitle(LocalizationManager.T("title_option"));
                titleText.resizeTextForBestFit = true;
                titleText.resizeTextMinSize = 34;
                titleText.resizeTextMaxSize = 52;
            }
            EnsureOptionHeadingPaintStroke(panel, title, new Vector2(0f, 580f),
                new Vector2(430f, 20f), Violet);
            ConfigureOptionTitleGraphic(panel, title);
            RectTransform subtitle = FindRect(panel, "TitleOptionSubtitle");
            if (subtitle != null) subtitle.gameObject.SetActive(false);

            float[] rowY = { 500f, 425f, 350f, 275f, 200f };
            Color[] rowColors =
            {
                new Color(0.84f, 0.97f, 1f, 0.84f),
                new Color(1f, 0.86f, 0.82f, 0.84f),
                new Color(0.84f, 0.92f, 1f, 0.84f),
                new Color(0.88f, 0.98f, 0.92f, 0.84f),
                new Color(1f, 0.97f, 0.79f, 0.84f)
            };
            for (int i = 0; i < rowY.Length; i++)
            {
                EnsureOptionRow(panel, "OptionRow" + i, rowY[i], rowColors[i]);
            }

            EnsureOptionIcon(panel, "OptionBgmIcon", 7, new Vector2(-365f, rowY[0]), 42f);
            RectTransform bgmLabel = FindRect(panel, "OptionBgmLabel");
            UsePlainLocalizedOptionLabel(bgmLabel, "option_bgm");
            PlaceOptionText(bgmLabel, new Vector2(-286f, rowY[0]),
                new Vector2(130f, 42f), TextAnchor.MiddleLeft, 22, true);
            RectTransform bgmSlider = FindRect(panel, "OptionBgmSlider");
            PlaceOptionRect(bgmSlider, new Vector2(55f, rowY[0]), new Vector2(390f, 40f));
            ThemeOptionSlider(bgmSlider, Cyan);
            EnsureOptionBadge(panel, "OptionBgmValueBadge", new Vector2(310f, rowY[0]),
                new Vector2(96f, 44f));
            PlaceOptionText(FindRect(panel, "OptionBgmValue"), new Vector2(310f, rowY[0]),
                new Vector2(84f, 40f), TextAnchor.MiddleCenter, 19, true);

            EnsureOptionIcon(panel, "OptionSeIcon", 8, new Vector2(-365f, rowY[1]), 42f);
            RectTransform seLabel = FindRect(panel, "OptionSeLabel");
            UsePlainLocalizedOptionLabel(seLabel, "option_se");
            PlaceOptionText(seLabel, new Vector2(-286f, rowY[1]),
                new Vector2(130f, 42f), TextAnchor.MiddleLeft, 22, true);
            RectTransform seSlider = FindRect(panel, "OptionSeSlider");
            PlaceOptionRect(seSlider, new Vector2(55f, rowY[1]), new Vector2(390f, 40f));
            ThemeOptionSlider(seSlider, Coral);
            EnsureOptionBadge(panel, "OptionSeValueBadge", new Vector2(310f, rowY[1]),
                new Vector2(96f, 44f));
            PlaceOptionText(FindRect(panel, "OptionSeValue"), new Vector2(310f, rowY[1]),
                new Vector2(84f, 40f), TextAnchor.MiddleCenter, 19, true);

            EnsureOptionIcon(panel, "OptionLanguageIcon", 5, new Vector2(-365f, rowY[2]), 42f);
            RectTransform languageLabel = FindRect(panel, "OptionLanguageLabel");
            UsePlainLocalizedOptionLabel(languageLabel, "option_language");
            PlaceOptionText(languageLabel, new Vector2(-272f, rowY[2]), new Vector2(165f, 42f),
                TextAnchor.MiddleLeft, 22, true);
            RectTransform japanese = FindRect(panel, "OptionJapaneseButton");
            RectTransform english = FindRect(panel, "OptionEnglishButton");
            bool selectorMode = LocalizationManager.SupportedLanguages.Count > 2;
            if (selectorMode)
            {
                PlaceOptionRect(japanese, new Vector2(102f, rowY[2]), new Vector2(500f, 46f));
                HideIfExists(panel, "OptionLanguageCurrentValue");
                HideIfExists(panel, "OptionEnglishButton");
                ThemeOptionButton(japanese, new Color(0.66f, 0.88f, 1f, 1f), 19);
                EnsureOptionButtonIcon(japanese, "DropdownArrow", 0, true, -90f, 20f);
            }
            else
            {
                PlaceOptionRect(japanese, new Vector2(-25f, rowY[2]), new Vector2(230f, 44f));
                PlaceOptionRect(english, new Vector2(225f, rowY[2]), new Vector2(230f, 44f));
                ThemeOptionButton(japanese, LocalizationManager.IsCurrentLanguage("ja") ? Cyan : PaperRaised, 18);
                ThemeOptionButton(english, LocalizationManager.IsCurrentLanguage("en") ? Cyan : PaperRaised, 18);
            }

            HideIfExists(panel, "OptionKeysLabel");
            HideIfExists(panel, "OptionKeysValue");
            HideIfExists(panel, "OptionVibrationLabel");
            HideIfExists(panel, "OptionVibrationButton");
            HideIfExists(panel, "OptionLanguageValue");

            EnsureOptionIcon(panel, "OptionDisplayIcon", 9, new Vector2(-365f, rowY[3]), 42f);
            RectTransform screenMode = FindRect(panel, "OptionScreenModeButton");
            RectTransform resolution = FindRect(panel, "OptionResolutionButton");
            PlaceOptionRect(screenMode, new Vector2(-110f, rowY[3]), new Vector2(310f, 46f));
            PlaceOptionRect(resolution, new Vector2(220f, rowY[3]), new Vector2(310f, 46f));
            ThemeOptionButton(screenMode, new Color(0.91f, 0.98f, 0.96f, 1f), 17);
            ThemeOptionButton(resolution, new Color(0.91f, 0.98f, 0.96f, 1f), 17);

            EnsureOptionIcon(panel, "OptionPlayerNameIcon", 10, new Vector2(-365f, rowY[4]), 42f);
            PlaceOptionText(FindRect(panel, "OptionPlayerNameLabel"), new Vector2(-270f, rowY[4]),
                new Vector2(175f, 42f), TextAnchor.MiddleLeft, 20, true);
            RectTransform playerNameInput = FindRect(panel, "OptionPlayerNameInput");
            PlaceOptionRect(playerNameInput, new Vector2(100f, rowY[4]), new Vector2(485f, 46f));
            ThemeOptionInput(playerNameInput);
            PlaceOptionText(FindRect(panel, "OptionPlayerNameError"), new Vector2(100f, 165f),
                new Vector2(485f, 24f), TextAnchor.MiddleCenter, 14, true);

            RectTransform back = FindRect(panel, "TitleOptionBackButton");
            PlaceOptionRect(back, new Vector2(185f, 85f), new Vector2(300f, 62f));
            ThemeOptionButton(back, new Color(1f, 0.48f, 0.64f, 1f), 22);
            EnsureOptionButtonIcon(back, "DoneIcon", 12, false, 0f, 30f);
            Text backLabel = back != null ? back.GetComponentInChildren<Text>(true) : null;
            if (backLabel != null)
            {
                LocalizedText localized = backLabel.GetComponent<LocalizedText>();
                if (localized == null) localized = backLabel.gameObject.AddComponent<LocalizedText>();
                localized.SetKey("option_complete");
            }

            RectTransform register = FindRect(panel, "OptionPlayerNameRegisterButton");
            PlaceOptionRect(register, new Vector2(185f, 85f), new Vector2(300f, 62f));
            ThemeOptionButton(register, Green, 22);
            EnsureOptionButtonIcon(register, "DoneIcon", 12, false, 0f, 30f);

            RectTransform reset = FindRect(panel, "OptionDataResetButton");
            PlaceOptionRect(reset, new Vector2(-185f, 85f), new Vector2(270f, 62f));
            ThemeOptionButton(reset, Coral, 17);
            EnsureOptionButtonIcon(reset, "ResetIcon", 11, false, 0f, 30f);

            BringOptionControlsForward(panel);
        }

        private static string BuildRainbowOptionTitle(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (LocalizationManager.CurrentLanguageIsRightToLeft)
            {
                string escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                return "<color=#" + ColorUtility.ToHtmlStringRGB(Violet) + ">" + escaped + "</color>";
            }
            Color[] palette = { Violet, Coral, Yellow, Green, Cyan, Violet };
            string result = string.Empty;
            int colorIndex = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsWhiteSpace(character))
                {
                    result += character;
                    continue;
                }

                string glyph = character == '<' ? "&lt;"
                    : character == '>' ? "&gt;"
                    : character == '&' ? "&amp;"
                    : character.ToString();
                result += "<color=#" + ColorUtility.ToHtmlStringRGB(palette[colorIndex % palette.Length])
                    + ">" + glyph + "</color>";
                colorIndex++;
            }
            return result;
        }

        private static void ConfigureOptionTitleGraphic(RectTransform panel, RectTransform fallbackTitle)
        {
            if (panel == null) return;

            Sprite titleSprite = Resources.Load<Sprite>("UI/option-title-crayon-v1");
            Transform existing = panel.Find("OptionTitleGraphic");
            RectTransform graphic;
            if (existing == null)
            {
                GameObject obj = new GameObject("OptionTitleGraphic", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                graphic = obj.GetComponent<RectTransform>();
            }
            else graphic = existing as RectTransform;
            if (graphic == null) return;

            PlaceOptionRect(graphic, new Vector2(0f, 606f), new Vector2(360f, 124f));
            Image image = graphic.GetComponent<Image>();
            if (image == null) image = graphic.gameObject.AddComponent<Image>();
            image.sprite = titleSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            graphic.gameObject.SetActive(titleSprite != null);

            Text fallback = fallbackTitle != null ? fallbackTitle.GetComponent<Text>() : null;
            if (fallback != null) fallback.enabled = titleSprite == null;
            Transform stroke = panel.Find("OptionHeadingPaintStroke");
            if (stroke != null) stroke.gameObject.SetActive(titleSprite == null);
            if (titleSprite != null) graphic.SetAsLastSibling();
        }

        private static void UsePlainLocalizedOptionLabel(RectTransform rect, string key)
        {
            if (rect == null) return;
            PrefixedLocalizedText prefixed = rect.GetComponent<PrefixedLocalizedText>();
            if (prefixed != null) prefixed.enabled = false;
            LocalizedText localized = rect.GetComponent<LocalizedText>();
            if (localized == null) localized = rect.gameObject.AddComponent<LocalizedText>();
            localized.enabled = true;
            localized.SetKey(key);
        }

        private static void EnsureOptionScrapbookDecorations(RectTransform panel)
        {
            QuietMenuBackdrop.Apply(panel, "OptionScrapbookDecorations",
                QuietMenuBackdropPreset.Option);
        }

        private static void EnsureOptionIcon(RectTransform panel, string name, int iconIndex,
            Vector2 position, float size)
        {
            Transform existing = panel.Find(name);
            RectTransform root;
            if (existing == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                root = obj.GetComponent<RectTransform>();
            }
            else root = existing as RectTransform;
            if (root == null) return;

            PlaceOptionRect(root, position, new Vector2(size, size));
            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(iconIndex);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Ink;
            image.raycastTarget = false;
        }

        private static void EnsureOptionBadge(RectTransform panel, string name, Vector2 position,
            Vector2 size)
        {
            Transform existing = panel.Find(name);
            RectTransform root;
            if (existing == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                root = obj.GetComponent<RectTransform>();
            }
            else root = existing as RectTransform;
            if (root == null) return;

            PlaceOptionRect(root, position, size);
            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            DoodlePaperUi.Apply(image, new Color(1f, 0.995f, 0.94f, 0.96f));
            image.raycastTarget = false;
            EnsureOutline(root.gameObject, 1f, 0.2f);
        }

        private static void EnsureOptionButtonIcon(RectTransform button, string name, int iconIndex,
            bool alignRight, float rotation, float size)
        {
            if (button == null) return;
            Transform existing = button.Find(name);
            RectTransform root;
            if (existing == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(button, false);
                root = obj.GetComponent<RectTransform>();
            }
            else root = existing as RectTransform;
            if (root == null) return;

            float anchor = alignRight ? 1f : 0f;
            root.anchorMin = root.anchorMax = new Vector2(anchor, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(alignRight ? -24f : 31f, 0f);
            root.sizeDelta = new Vector2(size, size);
            root.localRotation = Quaternion.Euler(0f, 0f, rotation);
            root.SetAsLastSibling();

            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite(iconIndex);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Ink;
            image.raycastTarget = false;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label == null) return;
            if (alignRight) label.rectTransform.offsetMax = new Vector2(-48f, label.rectTransform.offsetMax.y);
            else label.rectTransform.offsetMin = new Vector2(58f, label.rectTransform.offsetMin.y);
        }

        private static void ThemeOptionInput(RectTransform input)
        {
            if (input == null) return;
            Image image = input.GetComponent<Image>();
            if (image != null)
            {
                DoodlePaperUi.Apply(image, new Color(1f, 0.97f, 0.72f, 1f));
            }
            EnsureOutline(input.gameObject, 1.5f, 0.62f);
            EnsureShadow(input.gameObject, new Vector2(3f, -3f), 0.13f);
        }

        private static void EnsureOptionHeadingPaintStroke(
            RectTransform panel,
            RectTransform title,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            if (panel == null || title == null) return;
            Transform existing = panel.Find("OptionHeadingPaintStroke");
            GameObject stroke = existing != null ? existing.gameObject : null;
            if (stroke == null)
            {
                stroke = new GameObject("OptionHeadingPaintStroke", typeof(RectTransform));
                stroke.transform.SetParent(panel, false);
                for (int i = 0; i < 4; i++)
                {
                    GameObject stripe = new GameObject("PaintStripe" + i,
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    stripe.transform.SetParent(stroke.transform, false);
                    RectTransform stripeRect = stripe.GetComponent<RectTransform>();
                    stripeRect.anchorMin = stripeRect.anchorMax = new Vector2(0.5f, 0.5f);
                    stripeRect.pivot = new Vector2(0.5f, 0.5f);
                    stripeRect.anchoredPosition = new Vector2((i - 1.5f) * 4f, (i - 1.5f) * 3f);
                    stripeRect.sizeDelta = new Vector2(size.x - i * 13f, 13f + i * 2f);
                    stripeRect.localRotation = Quaternion.Euler(0f, 0f, -1.6f + i * 1.1f);
                    Image image = stripe.GetComponent<Image>();
                    image.raycastTarget = false;
                    image.color = new Color(color.r, color.g, color.b, 0.2f + i * 0.07f);
                }
            }

            PlaceOptionRect(stroke.GetComponent<RectTransform>(), position, size);
            stroke.transform.SetAsFirstSibling();
            title.SetAsLastSibling();
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

            PlaceOptionRect(row, new Vector2(0f, y), new Vector2(820f, 62f));
            Image image = row.GetComponent<Image>();
            DoodlePaperUi.Apply(image, color);
            image.raycastTarget = false;
            EnsureOutline(row.gameObject, 1.2f, 0.24f);
            EnsureShadow(row.gameObject, new Vector2(2f, -3f), 0.08f);
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
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Min(12, fontSize);
            text.resizeTextMaxSize = fontSize;
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

            Image image = rect.GetComponent<Image>();
            if (image != null)
            {
                DoodlePaperUi.Apply(image, color);
            }
            EnsureOutline(rect.gameObject, 2f, 0.7f);
            EnsureShadow(rect.gameObject, new Vector2(3f, -3f), 0.16f);
            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.font = fallbackFont;
                label.fontSize = fontSize;
                label.fontStyle = FontStyle.Bold;
                label.color = Ink;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = fontSize;
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
                "OptionBgmLabel", "OptionBgmSlider", "OptionBgmValueBadge", "OptionBgmValue",
                "OptionSeLabel", "OptionSeSlider", "OptionSeValueBadge", "OptionSeValue",
                "OptionLanguageLabel", "OptionJapaneseButton", "OptionEnglishButton",
                "OptionScreenModeButton", "OptionResolutionButton",
                "OptionPlayerNameLabel", "OptionPlayerNameInput", "OptionPlayerNameError",
                "OptionBgmIcon", "OptionSeIcon", "OptionLanguageIcon", "OptionDisplayIcon",
                "OptionPlayerNameIcon",
                "OptionDataResetButton", "OptionPlayerNameRegisterButton", "TitleOptionBackButton",
                "OptionLanguagePopup", "OptionDataResetPopup"
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
                SetPaperSurface(drawer, new Color(1f, 0.975f, 0.89f, 0.97f));
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

                SetPaperSurface(panel, PaperRaised);
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

                Color baseColor = button.targetGraphic != null ? button.targetGraphic.color : PaperRaised;
                Image buttonSurface = rect.GetComponent<Image>();
                if (buttonSurface != null)
                {
                    DoodlePaperUi.Apply(buttonSurface, baseColor);
                }

                // Apply the paper surface first. The shared effect helpers can then
                // distinguish a pasted paper control from a non-paper icon/slider and
                // avoid rebuilding the former's silhouette with a heavy black outline.
                bool titlePaperButton = IsTitlePaperButton(button.name);
                if (titlePaperButton)
                {
                    EnsureOutline(button.gameObject, 1.35f, 0.46f);
                    EnsureShadow(button.gameObject, new Vector2(4f, -5f), 0.17f);
                }
                else
                {
                    EnsureOutline(button.gameObject, 2.8f, 0.86f);
                    EnsureShadow(button.gameObject, new Vector2(5f, -5f), 0.21f);
                }
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

                float restingRotation = IsTitlePrimaryButton(button.name)
                    ? Mathf.DeltaAngle(0f, rect.localEulerAngles.z)
                    : 0f;
                if (!IsTitlePrimaryButton(button.name)) rect.localRotation = Quaternion.identity;
                motion.Configure(restingRotation);

                StageCardHover legacyHover = button.GetComponent<StageCardHover>();
                if (legacyHover != null)
                {
                    legacyHover.enabled = false;
                }

                ApplyButtonLabelSpacing(button);
            }

            Dropdown[] dropdowns = GetComponentsInChildren<Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                DoodleDropdownSfx feedback = dropdowns[i].GetComponent<DoodleDropdownSfx>();
                if (feedback == null) feedback = dropdowns[i].gameObject.AddComponent<DoodleDropdownSfx>();
                feedback.Configure(dropdowns[i]);
            }

            Toggle[] toggles = GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                DoodleToggleSfx feedback = toggles[i].GetComponent<DoodleToggleSfx>();
                if (feedback == null) feedback = toggles[i].gameObject.AddComponent<DoodleToggleSfx>();
                feedback.Configure(toggles[i]);
            }
        }

        private void ApplyAllButtonLabelSpacing()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                EnsureButtonBreathingRoom(buttons[i]);
                ApplyButtonLabelSpacing(buttons[i]);
            }
        }

        private static void EnsureButtonBreathingRoom(Button button)
        {
            if (button == null) return;
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) return;

            Text caption = null;
            Text[] texts = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text candidate = texts[i];
                if (candidate == null || candidate.GetComponentInParent<Button>() != button
                    || string.IsNullOrWhiteSpace(candidate.text)
                    || candidate.name.Contains("Placeholder")) continue;
                caption = candidate;
                break;
            }
            if (caption == null) return;

            float width = Mathf.Abs(buttonRect.rect.width);
            float height = Mathf.Abs(buttonRect.rect.height);
            if (width < 34f || height < 22f) return;

            RectTransform captionRect = caption.rectTransform;
            float reservedWidth = captionRect.anchorMax.x - captionRect.anchorMin.x > 0.8f
                ? Mathf.Max(0f, captionRect.offsetMin.x - captionRect.offsetMax.x)
                : Mathf.Max(0f, width - Mathf.Abs(captionRect.rect.width));
            float reservedHeight = captionRect.anchorMax.y - captionRect.anchorMin.y > 0.8f
                ? Mathf.Max(0f, captionRect.offsetMin.y - captionRect.offsetMax.y)
                : Mathf.Max(0f, height - Mathf.Abs(captionRect.rect.height));

            float desiredWidth = caption.preferredWidth + reservedWidth + 34f;
            float desiredHeight = caption.preferredHeight + reservedHeight + 16f;
            float maximumWidthGrowth = width >= 220f ? 28f : width >= 140f ? 20f : width >= 84f ? 14f : 8f;
            float maximumHeightGrowth = height >= 56f ? 10f : 6f;
            float widthGrowth = Mathf.Clamp(desiredWidth - width, 0f, maximumWidthGrowth);
            float heightGrowth = Mathf.Clamp(desiredHeight - height, 0f, maximumHeightGrowth);
            if (widthGrowth < 0.1f && heightGrowth < 0.1f) return;

            buttonRect.sizeDelta += new Vector2(widthGrowth, heightGrowth);
        }

        private static void ApplyButtonLabelSpacing(Button button)
        {
            if (button == null) return;

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (buttonRect == null) return;

            float width = Mathf.Abs(buttonRect.rect.width);
            float height = Mathf.Abs(buttonRect.rect.height);
            if (width < 18f || height < 16f) return;

            // Compact editor/page buttons need less inset than the large menu
            // cards. Padding is applied to the caption only: neighbouring UI
            // cannot be pushed into an overlap by this shared pass.
            float horizontalPadding = width >= 140f ? 18f : width >= 72f ? 12f : 6f;
            float verticalPadding = height >= 60f ? 9f : height >= 34f ? 6f : 3f;

            Text[] captions = button.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < captions.Length; i++)
            {
                Text caption = captions[i];
                if (caption == null || caption.name.Contains("Placeholder")) continue;

                // A button can contain another button (dropdown templates and
                // a few composite controls do this). Only touch text owned by
                // the current button, never a nested control's caption.
                if (caption.GetComponentInParent<Button>() != button) continue;

                // Badges and text-based decorations can also live below a button
                // without being its caption. If an intermediate visual/selectable
                // owns the text, leave that deliberately small layout untouched.
                // Plain grouping transforms are allowed so existing label wrappers
                // still receive the shared inset.
                Transform ancestor = caption.transform.parent;
                bool belongsToButtonCaption = true;
                while (ancestor != null && ancestor != button.transform)
                {
                    if (ancestor.GetComponent<Graphic>() != null
                        || ancestor.GetComponent<Selectable>() != null)
                    {
                        belongsToButtonCaption = false;
                        break;
                    }

                    ancestor = ancestor.parent;
                }

                if (!belongsToButtonCaption || ancestor != button.transform) continue;

                RectTransform captionRect = caption.rectTransform;
                bool stretchesHorizontally = captionRect.anchorMax.x - captionRect.anchorMin.x > 0.8f;
                bool stretchesVertically = captionRect.anchorMax.y - captionRect.anchorMin.y > 0.8f;

                if (stretchesHorizontally)
                {
                    Vector2 minimum = captionRect.offsetMin;
                    Vector2 maximum = captionRect.offsetMax;
                    minimum.x = Mathf.Max(minimum.x, horizontalPadding);
                    maximum.x = Mathf.Min(maximum.x, -horizontalPadding);
                    captionRect.offsetMin = minimum;
                    captionRect.offsetMax = maximum;
                }

                if (stretchesVertically)
                {
                    Vector2 minimum = captionRect.offsetMin;
                    Vector2 maximum = captionRect.offsetMax;
                    minimum.y = Mathf.Max(minimum.y, verticalPadding);
                    maximum.y = Mathf.Min(maximum.y, -verticalPadding);
                    captionRect.offsetMin = minimum;
                    captionRect.offsetMax = maximum;
                }

                int designedMaximum = caption.resizeTextForBestFit && caption.resizeTextMaxSize > 0
                    ? caption.resizeTextMaxSize
                    : caption.fontSize;
                designedMaximum = Mathf.Max(10, designedMaximum);
                caption.resizeTextForBestFit = true;
                caption.resizeTextMinSize = Mathf.Clamp(designedMaximum - 10, 10, designedMaximum);
                caption.resizeTextMaxSize = designedMaximum;
                caption.horizontalOverflow = HorizontalWrapMode.Wrap;
                caption.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private static bool IsTitlePrimaryButton(string buttonName)
        {
            switch (buttonName)
            {
                case "TitleSingleButton":
                case "TitleMultiButton":
                case "TitleDrawButton":
                case "TitleOptionButton":
                case "TitleExitButton":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTitlePaperButton(string buttonName)
        {
            return IsTitlePrimaryButton(buttonName)
                || buttonName == "TitleFeedbackButton"
                || buttonName == "TitleWishlistButton";
        }

        private void NormalizeBackButtonLabels()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !IsStandardBackButton(button.name)) continue;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label == null) continue;
                LocalizedText localized = label.GetComponent<LocalizedText>();
                if (localized == null) localized = label.gameObject.AddComponent<LocalizedText>();
                localized.SetKey("ui_back_esc");
                label.fontStyle = FontStyle.Bold;
                if (button.name == "CancelDrawButton")
                {
                    label.fontSize = 13;
                    label.resizeTextForBestFit = false;
                }
                else
                {
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 13;
                }
            }
        }

        private static bool IsStandardBackButton(string buttonName)
        {
            switch (buttonName)
            {
                case "TitleOptionBackButton":
                case "StageSelectBackButton":
                case "MultiBackTitleButton":
                case "MultiRoomBackButton":
                case "MultiCreateBackButton":
                case "MultiJoinBackButton":
                case "RuntimeEditCloseButton":
                case "CancelDrawButton":
                    return true;
                default:
                    return false;
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

        private void ConfigureLocalizedTextSafety()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text.name.Contains("Placeholder")) continue;

                Button ownerButton = text.GetComponentInParent<Button>();
                if (ownerButton != null)
                {
                    // Button captions are one of the first places a translated
                    // string overflows. Preserve the designed maximum size, but
                    // allow longer languages to shrink inside the same frame.
                    int maximum = Mathf.Max(12, text.fontSize);
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = Mathf.Clamp(maximum - 9, 11, maximum);
                    text.resizeTextMaxSize = maximum;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    text.verticalOverflow = VerticalWrapMode.Truncate;
                    continue;
                }

                string objectName = text.name;
                bool isHeading = objectName.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || objectName.IndexOf("Heading", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isHeading || text.rectTransform.rect.width < 80f) continue;

                int headingMaximum = Mathf.Max(14, text.fontSize);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Mathf.Clamp(headingMaximum - 10, 13, headingMaximum);
                text.resizeTextMaxSize = headingMaximum;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
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

            SetPaperSurface(rect, background);
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

            SetPaperSurface(rect, color);
            EnsureOutline(rect.gameObject, 2.2f, 0.86f);
            EnsureShadow(rect.gameObject, new Vector2(3f, -3f), 0.2f);
        }

        private enum TitleMenuIcon
        {
            Play,
            Group,
            Pencil,
            Gear,
            Exit
        }

        private void SetTitleMenuButton(RectTransform parent, string name, Vector2 position,
            Color color, TitleMenuIcon icon)
        {
            RectTransform rect = FindRect(parent, name);
            if (rect == null) return;

            Vector2 size = new Vector2(166f, 68f);
            SetButtonLayout(parent, name, position, size, color, 0f);
            float angle = icon == TitleMenuIcon.Play ? -1.1f
                : icon == TitleMenuIcon.Group ? 0.7f
                : icon == TitleMenuIcon.Pencil ? -0.45f
                : icon == TitleMenuIcon.Gear ? 0.55f
                : -0.7f;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image surface = rect.GetComponent<Image>();
            if (surface != null)
            {
                DoodlePaperUi.Apply(surface, color);
            }
            EnsureOutline(rect.gameObject, 1.35f, 0.46f);
            EnsureShadow(rect.gameObject, new Vector2(4f, -5f), 0.17f);

            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 21;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.rectTransform.offsetMin = new Vector2(45f, 3f);
                label.rectTransform.offsetMax = new Vector2(-7f, -3f);
            }

            EnsureTitleButtonIcon(rect, icon);
            EnsureTitleButtonPaperFibres(rect, color);
        }

        private static void EnsureTitleButtonIcon(RectTransform button, TitleMenuIcon icon)
        {
            Transform found = button.Find("TitleButtonIcon");
            RectTransform root;
            if (found == null)
            {
                GameObject obj = new GameObject("TitleButtonIcon", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(button, false);
                root = obj.GetComponent<RectTransform>();
            }
            else
            {
                root = found as RectTransform;
            }
            if (root == null) return;

            root.anchorMin = root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(27f, 0f);
            root.sizeDelta = new Vector2(36f, 36f);
            root.localRotation = Quaternion.identity;
            root.SetAsLastSibling();
            for (int i = 0; i < root.childCount; i++) root.GetChild(i).gameObject.SetActive(false);

            Image image = root.GetComponent<Image>();
            if (image == null) image = root.gameObject.AddComponent<Image>();
            image.sprite = DoodleRuntimeAssets.GetTitleMenuIconSprite((int)icon);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = new Color(Ink.r, Ink.g, Ink.b, 0.98f);
            image.raycastTarget = false;
        }

        private static void EnsureTitleButtonPaperFibres(RectTransform button, Color color)
        {
            Transform found = button.Find("PaperFibres");
            RectTransform fibres;
            if (found == null)
            {
                GameObject obj = new GameObject("PaperFibres", typeof(RectTransform));
                obj.transform.SetParent(button, false);
                fibres = obj.GetComponent<RectTransform>();
            }
            else fibres = found as RectTransform;
            if (fibres == null) return;
            Stretch(fibres);
            fibres.SetAsFirstSibling();
            if (fibres.childCount > 0) return;

            Color stroke = new Color(Mathf.Max(0f, color.r - 0.18f), Mathf.Max(0f, color.g - 0.18f),
                Mathf.Max(0f, color.b - 0.18f), 0.16f);
            for (int i = 0; i < 7; i++)
            {
                float y = -23f + i * 8f;
                CreateLine(fibres, new Vector2(-72f + (i % 3) * 5f, y),
                    new Vector2(70f - (i % 2) * 9f, y + (i % 2 == 0 ? 1.5f : -1.5f)),
                    1.2f, stroke, "CrayonFibre");
            }
        }

        private void EnsureTitleScrapbookDecorations(RectTransform panel)
        {
            QuietMenuBackdrop.Apply(panel, "TitleScrapbookDecorations",
                QuietMenuBackdropPreset.Title);
        }

        private static void EnsureTitleLogoMount(RectTransform panel, RectTransform logo)
        {
            Transform found = panel.Find("TitleLogoMount");
            RectTransform mount;
            if (found == null)
            {
                GameObject obj = new GameObject("TitleLogoMount", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(panel, false);
                mount = obj.GetComponent<RectTransform>();
            }
            else
            {
                mount = found as RectTransform;
            }
            if (mount == null) return;

            mount.gameObject.SetActive(true);
            mount.anchorMin = logo.anchorMin;
            mount.anchorMax = logo.anchorMax;
            mount.pivot = new Vector2(0.5f, 0.5f);
            mount.anchoredPosition = logo.anchoredPosition;
            mount.sizeDelta = new Vector2(760f, 250f);
            mount.localRotation = Quaternion.identity;
            mount.localScale = logo.localScale;
            Image paper = mount.GetComponent<Image>();
            if (paper != null)
            {
                paper.enabled = false;
                paper.raycastTarget = false;
            }
            Transform leftTape = mount.Find("LogoTapeLeft");
            if (leftTape != null) leftTape.gameObject.SetActive(false);
            Transform rightTape = mount.Find("LogoTapeRight");
            if (rightTape != null) rightTape.gameObject.SetActive(false);

            TitleLogoAccentAnimator accent = mount.GetComponent<TitleLogoAccentAnimator>();
            if (accent == null)
            {
                accent = mount.gameObject.AddComponent<TitleLogoAccentAnimator>();
            }
            accent.Configure();

            // Keep the paper directly behind the logo while allowing the menu shelf
            // to be raised to the front later in ThemeTitle.
            mount.SetAsLastSibling();
            logo.SetAsLastSibling();
        }

        private static void EnsureTitleLogoTape(RectTransform parent, string name,
            Vector2 position, Vector2 size, float rotation, Color color)
        {
            Transform found = parent.Find(name);
            RectTransform tape;
            if (found == null)
            {
                GameObject obj = new GameObject(name, typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(parent, false);
                tape = obj.GetComponent<RectTransform>();
            }
            else tape = found as RectTransform;
            if (tape == null) return;

            tape.anchorMin = tape.anchorMax = new Vector2(0.5f, 0.5f);
            tape.pivot = new Vector2(0.5f, 0.5f);
            tape.anchoredPosition = position;
            tape.sizeDelta = size;
            tape.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = tape.GetComponent<Image>();
            image.sprite = DoodleRuntimeAssets.SquareSprite;
            image.color = color;
            image.raycastTarget = false;
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
            SetPaperSurface(rect, color);
            EnsureOutline(rect.gameObject, 2.5f, 0.84f);
            EnsureShadow(rect.gameObject, new Vector2(5f, -5f), 0.21f);

            Text label = rect.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 23;
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
            SetPaperSurface(rect, color);
            EnsureOutline(rect.gameObject, 2.8f, 0.86f);
            EnsureShadow(rect.gameObject, new Vector2(5f, -5f), 0.21f);
        }

        private void SetNamedButtonColor(RectTransform parent, string name, Color color)
        {
            RectTransform button = FindRect(parent, name);
            if (button != null)
            {
                SetPaperSurface(button, color);
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
            SetPaperSurface(sheet, PaperRaised);
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

        private static void EnsurePaintStrokeHighlight(RectTransform textRect, Color color)
        {
            if (textRect == null || textRect.parent == null) return;

            string markerName = textRect.name + "MarkerHighlight";
            Transform existing = textRect.parent.Find(markerName);
            RectTransform marker;
            if (existing == null)
            {
                GameObject obj = new GameObject(markerName, typeof(RectTransform));
                obj.transform.SetParent(textRect.parent, false);
                marker = obj.GetComponent<RectTransform>();
            }
            else
            {
                marker = existing as RectTransform;
            }
            if (marker == null) return;

            Image oldFlatHighlight = marker.GetComponent<Image>();
            if (oldFlatHighlight != null) oldFlatHighlight.enabled = false;
            marker.gameObject.SetActive(true);
            marker.anchorMin = textRect.anchorMin;
            marker.anchorMax = textRect.anchorMax;
            marker.pivot = textRect.pivot;
            marker.anchoredPosition = textRect.anchoredPosition + new Vector2(0f, -4f);
            marker.sizeDelta = new Vector2(textRect.sizeDelta.x * 0.98f, 50f);
            marker.localRotation = Quaternion.identity;

            for (int i = 0; i < 4; i++)
            {
                string stripeName = "InkStroke" + i;
                Transform found = marker.Find(stripeName);
                RectTransform stripe;
                if (found == null)
                {
                    GameObject obj = new GameObject(stripeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    obj.transform.SetParent(marker, false);
                    stripe = obj.GetComponent<RectTransform>();
                }
                else
                {
                    stripe = found as RectTransform;
                }
                if (stripe == null) continue;

                stripe.anchorMin = stripe.anchorMax = new Vector2(0.5f, 0.5f);
                stripe.pivot = new Vector2(0.5f, 0.5f);
                stripe.anchoredPosition = new Vector2((i - 1.5f) * 4f, (i - 1.5f) * 3f);
                stripe.sizeDelta = new Vector2(marker.sizeDelta.x - i * 14f, 13f + i * 2f);
                stripe.localRotation = Quaternion.Euler(0f, 0f, -1.6f + i * 1.1f);
                Image image = stripe.GetComponent<Image>();
                image.color = new Color(color.r, color.g, color.b, 0.2f + i * 0.07f);
                image.raycastTarget = false;
            }

            marker.SetSiblingIndex(Mathf.Max(0, textRect.GetSiblingIndex()));
            textRect.SetAsLastSibling();
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
                CreateLine(parent, from, to, width, color, "DoodleCircle");
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

        private static void SetPaperSurface(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            DoodlePaperUi.Apply(rect.GetComponent<Image>(), color);
        }

        private static void DisableGraphicEffects(GameObject target)
        {
            if (target == null) return;
            Shadow[] effects = target.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++) effects[i].enabled = false;
        }

        private static void EnsureOutline(GameObject target, float distance, float alpha)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            Image surface = target.GetComponent<Image>();
            if (DoodlePaperUi.IsApplied(surface))
            {
                outline.enabled = false;
                return;
            }

            outline.enabled = true;
            outline.effectColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        private static void EnsureShadow(GameObject target, Vector2 distance, float alpha)
        {
            Image surface = target != null ? target.GetComponent<Image>() : null;
            bool isPaperSurface = DoodlePaperUi.IsApplied(surface);
            if (isPaperSurface)
            {
                // The shared nine-slice already carries a softly diffused, offset paper
                // shadow. Adding Unity's single hard mesh copy on top makes the edge look
                // like a digital UI panel again, so paper surfaces keep only the baked lift.
                DoodlePaperUi.DisableLegacyEffects(target);
                return;
            }

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

            shadow.enabled = true;
            shadow.effectColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        /// <summary>
        /// Screen-specific polishers may add their own Outline/Shadow after the shared
        /// theme pass. Normalize every shared paper surface once those polishers finish:
        /// the sprite itself supplies the cut/pencil edge and diffuse lifted-paper shadow.
        /// </summary>
        private void NormalizePaperSurfaceEffects()
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int imageIndex = 0; imageIndex < images.Length; imageIndex++)
            {
                Image image = images[imageIndex];
                if (!DoodlePaperUi.IsApplied(image)) continue;
                DoodlePaperUi.DisableLegacyEffects(image.gameObject);
            }
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
        public void OnSelect(BaseEventData eventData)
        {
            SetHovered(true);
            if (eventData is AxisEventData)
            {
                GameSfx.Play(SfxId.UiCursorMove);
            }
        }
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = 0.96f;
            targetRotation = restingRotation;
            GameSfx.Play(ResolvePressSfx());
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

        private SfxId ResolvePressSfx()
        {
            string objectName = gameObject.name;
            if (objectName.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Return", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Cancel", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Exit", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Leave", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SfxId.UiButtonBack;
            }

            if (objectName.IndexOf("Tab", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Page", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Species", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Part", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SfxId.UiTabChange;
            }

            return SfxId.UiButtonPress;
        }
    }

    public sealed class DoodleDropdownSfx : MonoBehaviour, IPointerDownHandler
    {
        private Dropdown dropdown;

        public void Configure(Dropdown value)
        {
            if (dropdown != null) dropdown.onValueChanged.RemoveListener(HandleChanged);
            dropdown = value;
            if (dropdown != null) dropdown.onValueChanged.AddListener(HandleChanged);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (dropdown != null && dropdown.IsInteractable()) GameSfx.Play(SfxId.UiDropdownOpen);
        }

        private void HandleChanged(int value)
        {
            if (dropdown != null && dropdown.IsInteractable() && gameObject.activeInHierarchy)
            {
                GameSfx.Play(SfxId.UiDropdownSelect);
            }
        }
    }

    public sealed class DoodleToggleSfx : MonoBehaviour
    {
        private Toggle toggle;

        public void Configure(Toggle value)
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(HandleChanged);
            toggle = value;
            if (toggle != null) toggle.onValueChanged.AddListener(HandleChanged);
        }

        private void HandleChanged(bool value)
        {
            if (toggle != null && toggle.IsInteractable() && gameObject.activeInHierarchy)
            {
                GameSfx.Play(value ? SfxId.UiToggleOn : SfxId.UiToggleOff);
            }
        }
    }
}
