using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class StageSelectVisualPolisher : MonoBehaviour
    {
        private void OnEnable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
            LocalizationManager.LanguageChanged += RefreshLocalizedText;
            StageProgressStore.Changed -= RefreshCompletionMarks;
            StageProgressStore.Changed += RefreshCompletionMarks;
            Polish();
            RefreshLocalizedText();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
            StageProgressStore.Changed -= RefreshCompletionMarks;
        }

        public void RefreshCompletionMarks()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                StageSelectButtonCommand command = buttons[i] != null
                    ? buttons[i].GetComponent<StageSelectButtonCommand>()
                    : null;
                Text label = buttons[i] != null ? buttons[i].GetComponentInChildren<Text>(true) : null;
                if (command != null && label != null) NormalizeStageButton(buttons[i], label);
            }
        }

        public void Polish()
        {
            HideTitle();
            PolishWorldCards();
            PolishButtons();
            DemoAccessPolicy.ApplyStageSelectRestrictions(gameObject);
        }

        public void RefreshWorldCardColors()
        {
            for (int i = 1; i <= 15; i++)
            {
                Transform card = FindDeep(transform, $"World{i}Card");
                if (card == null || !card.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Image image = card.GetComponent<Image>();
                if (image != null)
                {
                    DoodlePaperUi.Apply(image, GetWorldCardColor(i));
                }
            }
        }

        private void HideTitle()
        {
            Transform title = transform.Find("StageSelectTitle");
            if (title != null)
            {
                title.gameObject.SetActive(false);
            }
        }

        private void PolishWorldCards()
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                RectTransform rect = rects[i];
                if (rect == null || !rect.name.StartsWith("World", System.StringComparison.Ordinal) || !rect.name.EndsWith("Card", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Image image = rect.GetComponent<Image>();
                if (image != null)
                {
                    DoodlePaperUi.Apply(image, GetWorldCardColor(ParseWorldNumber(rect.name)));
                }

                SketchPaperTexture legacyPaper = rect.GetComponent<SketchPaperTexture>();
                if (legacyPaper != null)
                {
                    legacyPaper.enabled = false;
                }

                // World sheets are the clearest scrapbook element on this screen.  Tape only
                // some of them so the decoration feels hand-placed instead of becoming a
                // repeated UI border.  The tape is a child decoration and does not alter the
                // card layout or its hit area.
                EnsureWorldCardTape(rect, ParseWorldNumber(rect.name));
                RemoveIfExists(rect, "FoldedCorner");
                RemoveShadow(rect.gameObject);
                DisableLegacyFrame(rect, "WorldBoldFrame");
                rect.sizeDelta = new Vector2(200f, 330f);
                // Reserve the upper band for the crayon STAGE heading.  Moving the
                // complete cards keeps their internal layout and hit areas intact.
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 140f);
                rect.localRotation = Quaternion.identity;
                NormalizeWorldHeading(rect, ParseWorldNumber(rect.name));
                LayoutStageButtons(rect);
                BuildSpeciesRow(rect, ParseWorldNumber(rect.name));
            }
        }

        private static void NormalizeWorldHeading(RectTransform card, int world)
        {
            Transform headingTransform = card.Find($"StageGroup{world}Label");
            Text heading = headingTransform != null ? headingTransform.GetComponent<Text>() : null;
            if (heading == null) return;
            RectTransform rect = heading.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -22f);
            rect.sizeDelta = new Vector2(-28f, 44f);
            heading.fontSize = 26;
            heading.fontStyle = FontStyle.Bold;
            heading.alignment = TextAnchor.MiddleCenter;
            heading.resizeTextForBestFit = false;
            heading.horizontalOverflow = HorizontalWrapMode.Wrap;
            heading.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void LayoutStageButtons(RectTransform card)
        {
            Button[] buttons = card.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rect = buttons[i].GetComponent<RectTransform>();
                if (rect == null || !TryParseStageVariant(buttons[i].name, out int variant))
                {
                    continue;
                }

                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 146f - (variant - 1) * 54f);
                rect.sizeDelta = new Vector2(156f, 42f);
            }
        }

        private static bool TryParseStageVariant(string buttonName, out int variant)
        {
            variant = 0;
            if (string.IsNullOrEmpty(buttonName)
                || !buttonName.StartsWith("Stage_", System.StringComparison.Ordinal)
                || !buttonName.EndsWith("_Button", System.StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = buttonName.Split('_');
            return parts.Length >= 4 && int.TryParse(parts[2], out variant) && variant >= 1 && variant <= 3;
        }

        private static void BuildSpeciesRow(RectTransform card, int world)
        {
            if (card == null)
            {
                return;
            }

            Transform existingRow = card.Find("AvailableSpeciesRow");
            if (existingRow != null)
            {
                Transform obsoleteNames = existingRow.Find("AvailableSpeciesNames");
                if (obsoleteNames != null)
                {
                    Object.Destroy(obsoleteNames.gameObject);
                }
                return;
            }

            Font font = card.GetComponentInChildren<Text>(true)?.font;
            GameObject rowObject = new GameObject("AvailableSpeciesRow", typeof(RectTransform));
            rowObject.transform.SetParent(card, false);
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, 215f);
            row.sizeDelta = new Vector2(176f, 62f);

            GameObject titleObject = new GameObject("AvailableSpeciesTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleObject.transform.SetParent(row, false);
            Text title = titleObject.GetComponent<Text>();
            title.font = font;
            title.fontSize = 12;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.18f, 0.13f, 0.08f, 0.9f);
            title.raycastTarget = false;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(170f, 18f);

            StageSpeciesMask availability = StageSpeciesRules.GetAllowedForWorld(world);
            System.Collections.Generic.IReadOnlyList<DrawManager.Species> species = StageSpeciesRules.GetOrderedSpecies();
            int count = 0;
            for (int i = 0; i < species.Count; i++)
            {
                if (StageSpeciesRules.IsAllowed(availability, species[i]))
                {
                    count++;
                }
            }

            const float spacing = 32f;
            float startX = -(count - 1) * spacing * 0.5f;
            int visibleIndex = 0;
            for (int i = 0; i < species.Count; i++)
            {
                DrawManager.Species entry = species[i];
                if (!StageSpeciesRules.IsAllowed(availability, entry))
                {
                    continue;
                }

                CreateSpeciesChip(row, entry, new Vector2(startX + visibleIndex * spacing, -30f));
                visibleIndex++;
            }

        }

        private static void CreateSpeciesChip(RectTransform parent, DrawManager.Species species, Vector2 position)
        {
            GameObject chipObject = new GameObject(species + "Available", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            chipObject.transform.SetParent(parent, false);
            Image chip = chipObject.GetComponent<Image>();
            chip.color = GetSpeciesColor(species);
            chip.raycastTarget = false;
            RectTransform rect = chip.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(27f, 27f);

            Color ink = new Color(0.12f, 0.09f, 0.06f, 0.95f);
            if (TryCloneDetailedSpeciesIcon(rect, species))
            {
                return;
            }

            switch (species)
            {
                case DrawManager.Species.Cat:
                    CreateLine(rect, new Vector2(-8f, 4f), new Vector2(-4f, 10f), 1.6f, ink, "EarL");
                    CreateLine(rect, new Vector2(-4f, 10f), Vector2.zero, 1.6f, ink, "EarL2");
                    CreateLine(rect, Vector2.zero, new Vector2(5f, 10f), 1.6f, ink, "EarR");
                    CreateLine(rect, new Vector2(5f, 10f), new Vector2(9f, 4f), 1.6f, ink, "EarR2");
                    CreateLine(rect, new Vector2(-8f, 4f), new Vector2(8f, 4f), 1.6f, ink, "Face");
                    break;
                case DrawManager.Species.Bird:
                    CreateLine(rect, new Vector2(-10f, -3f), new Vector2(-2f, 5f), 2f, ink, "WingL");
                    CreateLine(rect, new Vector2(-2f, 5f), new Vector2(3f, -1f), 2f, ink, "Body");
                    CreateLine(rect, new Vector2(3f, -1f), new Vector2(10f, 5f), 2f, ink, "WingR");
                    break;
                case DrawManager.Species.Turtle:
                    CreateLine(rect, new Vector2(-8f, -4f), new Vector2(-5f, 5f), 2.3f, ink, "ShellL");
                    CreateLine(rect, new Vector2(-5f, 5f), new Vector2(5f, 5f), 2.3f, ink, "ShellTop");
                    CreateLine(rect, new Vector2(5f, 5f), new Vector2(8f, -4f), 2.3f, ink, "ShellR");
                    CreateLine(rect, new Vector2(8f, -4f), new Vector2(-8f, -4f), 2.3f, ink, "ShellBottom");
                    CreateLine(rect, new Vector2(8f, -2f), new Vector2(13f, 1f), 2.3f, ink, "Neck");
                    CreateLine(rect, new Vector2(13f, 1f), new Vector2(9f, 4f), 2.3f, ink, "Head");
                    break;
                case DrawManager.Species.Slime:
                    CreateLine(rect, new Vector2(-10f, -5f), new Vector2(-6f, 5f), 1.8f, ink, "Slime1");
                    CreateLine(rect, new Vector2(-6f, 5f), new Vector2(3f, 8f), 1.8f, ink, "Slime2");
                    CreateLine(rect, new Vector2(3f, 8f), new Vector2(10f, -5f), 1.8f, ink, "Slime3");
                    CreateLine(rect, new Vector2(10f, -5f), new Vector2(-10f, -5f), 1.8f, ink, "Slime4");
                    break;
                default:
                    CreateLine(rect, new Vector2(-4f, 8f), new Vector2(4f, 8f), 1.7f, ink, "HeadTop");
                    CreateLine(rect, new Vector2(4f, 8f), new Vector2(4f, 1f), 1.7f, ink, "HeadR");
                    CreateLine(rect, new Vector2(4f, 1f), new Vector2(-4f, 1f), 1.7f, ink, "HeadBottom");
                    CreateLine(rect, new Vector2(-4f, 1f), new Vector2(-4f, 8f), 1.7f, ink, "HeadL");
                    CreateLine(rect, new Vector2(0f, 1f), new Vector2(0f, -9f), 2f, ink, "Body");
                    break;
            }
        }

        private static bool TryCloneDetailedSpeciesIcon(RectTransform destination, DrawManager.Species species)
        {
            Transform source = FindDeep(destination.root, species + "DrawSpeciesButton");
            if (source == null)
            {
                source = FindDeep(destination.root, species + "GameplaySpeciesButton");
            }
            if (source == null)
            {
                return false;
            }

            bool cloned = false;
            for (int i = 0; i < source.childCount; i++)
            {
                Transform child = source.GetChild(i);
                if (child == null
                    || !child.gameObject.activeSelf
                    || (child.name != "IconLine" && child.name != "IconDot"))
                {
                    continue;
                }

                GameObject iconPart = Instantiate(child.gameObject, destination, false);
                iconPart.name = child.name;
                iconPart.transform.localScale = Vector3.one * 0.72f;
                Graphic graphic = iconPart.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.raycastTarget = false;
                }
                cloned = true;
            }
            return cloned;
        }

        private static Color GetSpeciesColor(DrawManager.Species species)
        {
            switch (species)
            {
                case DrawManager.Species.Cat: return new Color(1f, 0.72f, 0.38f, 0.95f);
                case DrawManager.Species.Bird: return new Color(0.45f, 0.82f, 1f, 0.95f);
                case DrawManager.Species.Turtle: return new Color(0.48f, 0.78f, 0.36f, 0.95f);
                case DrawManager.Species.Slime: return new Color(0.38f, 0.9f, 0.58f, 0.95f);
                default: return new Color(1f, 0.88f, 0.45f, 0.95f);
            }
        }

        private void RefreshLocalizedText()
        {
            Text title = FindDeep(transform, "ModernStageSelectTitle")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = LocalizationManager.T("stage_select");
            }
            RefreshSpeciesRowText();
            RemoveStageCreationLabels();
        }

        private void RefreshSpeciesRowText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "AvailableSpeciesTitle")
                {
                    Transform card = texts[i].transform.parent != null ? texts[i].transform.parent.parent : null;
                    int world = card != null ? ParseWorldNumber(card.name) : 1;
                    texts[i].text = LocalizationManager.Format(
                        "stage_species_available_compact",
                        BuildSpeciesNames(StageSpeciesRules.GetAllowedForWorld(world)));
                    texts[i].fontSize = Mathf.Clamp(Mathf.RoundToInt(11f * LocalizationManager.CurrentUiTextScale), 8, 13);
                    texts[i].fontStyle = FontStyle.Bold;
                }
            }
        }

        private void RemoveStageCreationLabels()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "DebugCreationStatus")
                {
                    texts[i].gameObject.SetActive(false);
                    Destroy(texts[i].gameObject);
                }
            }
        }

        private static string BuildSpeciesNames(StageSpeciesMask availability)
        {
            System.Collections.Generic.IReadOnlyList<DrawManager.Species> species = StageSpeciesRules.GetOrderedSpecies();
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            string separator = LocalizationManager.CurrentListSeparator;
            for (int i = 0; i < species.Count; i++)
            {
                if (!StageSpeciesRules.IsAllowed(availability, species[i]))
                {
                    continue;
                }

                if (value.Length > 0)
                {
                    value.Append(separator);
                }
                value.Append(LocalizationManager.T(StageSpeciesRules.GetSpeciesLocalizationKey(species[i])));
            }
            return value.ToString();
        }

        private static Color GetWorldCardColor(int world)
        {
            switch (Mathf.Abs(world) % 3)
            {
                case 0:
                    return new Color(0.91f, 0.97f, 1f, 0.98f);
                case 1:
                    return new Color(1f, 0.97f, 0.83f, 0.98f);
                default:
                    return new Color(0.92f, 1f, 0.89f, 0.98f);
            }
        }

        private static int ParseWorldNumber(string cardName)
        {
            const string prefix = "World";
            const string suffix = "Card";
            if (string.IsNullOrEmpty(cardName)
                || !cardName.StartsWith(prefix, System.StringComparison.Ordinal)
                || !cardName.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return 1;
            }

            string number = cardName.Substring(prefix.Length, cardName.Length - prefix.Length - suffix.Length);
            return int.TryParse(number, out int world) ? world : 1;
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

        private void PolishButtons()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                StageCardHover hover = button.GetComponent<StageCardHover>();
                if (hover == null)
                {
                    hover = button.gameObject.AddComponent<StageCardHover>();
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                if (rect == null)
                {
                    continue;
                }

                RemoveIfExists(rect, "MaskingTape");
                RemoveIfExists(rect, "StickyNoteBoldFrame");
                Image surface = button.GetComponent<Image>();
                if (surface != null)
                {
                    DoodlePaperUi.Apply(surface, surface.color);
                }
                DisableLegacyFrame(rect, "ButtonBoldFrame");
                rect.localRotation = Quaternion.identity;

                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    StageSelectButtonCommand stageCommand = button.GetComponent<StageSelectButtonCommand>();
                    if (stageCommand != null)
                    {
                        NormalizeStageButton(button, label);
                    }
                    label.transform.SetAsLastSibling();
                }
            }
        }

        private static void NormalizeStageButton(Button button, Text label)
        {
            StageSelectButtonCommand command = button.GetComponent<StageSelectButtonCommand>();
            if (command != null)
            {
                label.text = command.StageId + "    "
                    + (StageProgressStore.IsCleared(command.StageId) ? "✓" : "□");
            }
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(-12f, -4f);
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 22;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            Image image = button.GetComponent<Image>();
            if (image != null) DoodlePaperUi.Apply(image, new Color(0.98f, 0.96f, 0.9f, 0.95f));
            Outline outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.enabled = false;

            Transform status = button.transform.Find("DebugCreationStatus");
            if (status != null)
            {
                status.gameObject.SetActive(false);
                Destroy(status.gameObject);
            }
        }

        private static void RemoveShadow(GameObject target)
        {
            Shadow[] effects = target.GetComponents<Shadow>();
            for (int i = 0; i < effects.Length; i++)
            {
                // Outline inherits from Shadow. GetComponent<Shadow>() can
                // therefore return the card frame and used to delete it when
                // changing pages. Only remove the plain drop-shadow effect.
                if (effects[i] != null && effects[i].GetType() == typeof(Shadow))
                {
                    Destroy(effects[i]);
                }
            }
        }

        private static void DisableLegacyFrame(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }

            Outline outline = parent.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private static void RemoveIfExists(RectTransform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        private static void EnsureWorldCardTape(RectTransform card, int world)
        {
            bool leftTape = world % 3 == 1;
            bool rightTape = world % 3 == 0;

            SetDecorationActive(card, "WorldMaskingTapeA", leftTape);
            SetDecorationActive(card, "WorldMaskingTapeB", rightTape);

            if (leftTape)
            {
                AddMaskingTape(card, "WorldMaskingTapeA", new Vector2(-47f, 160f), -5f,
                    new Color(0.64f, 0.88f, 0.94f, 0.62f));
            }
            else if (rightTape)
            {
                AddMaskingTape(card, "WorldMaskingTapeB", new Vector2(46f, 160f), 4f,
                    new Color(1f, 0.86f, 0.48f, 0.58f));
            }
        }

        private static void SetDecorationActive(RectTransform parent, string childName, bool active)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void AddMaskingTape(RectTransform parent, string name, Vector2 position,
            float rotation, Color color)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find(name);
            GameObject tape;
            if (existing != null)
            {
                tape = existing.gameObject;
                tape.SetActive(true);
            }
            else
            {
                tape = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tape.transform.SetParent(parent, false);
            }

            Image image = tape.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = tape.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(64f, 17f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            rect.SetAsFirstSibling();

            Color paperEdge = new Color(0.34f, 0.28f, 0.16f, 0.18f);
            EnsureTapeLine(rect, "TapeTopEdge", new Vector2(-29f, 7f), new Vector2(28f, 6f),
                0.8f, paperEdge);
            EnsureTapeLine(rect, "TapeBottomEdge", new Vector2(-28f, -6f), new Vector2(29f, -7f),
                0.8f, paperEdge);

            // Two nearly invisible fibres are enough to keep the tape from reading as a flat
            // Unity rectangle at small stage-select scale.
            Color fibre = new Color(1f, 1f, 0.94f, 0.2f);
            EnsureTapeLine(rect, "TapeFibreA", new Vector2(-23f, 2f), new Vector2(21f, 1f),
                0.7f, fibre);
            EnsureTapeLine(rect, "TapeFibreB", new Vector2(-15f, -2f), new Vector2(25f, -1f),
                0.6f, fibre);
        }

        private static void EnsureTapeLine(RectTransform parent, string name, Vector2 from,
            Vector2 to, float width, Color color)
        {
            Transform existing = parent.Find(name);
            RectTransform rect;
            Image image;
            if (existing == null)
            {
                GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                line.transform.SetParent(parent, false);
                rect = line.GetComponent<RectTransform>();
                image = line.GetComponent<Image>();
            }
            else
            {
                rect = existing as RectTransform;
                image = existing.GetComponent<Image>();
            }

            if (rect == null || image == null)
            {
                return;
            }

            image.color = color;
            image.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(Vector2.Distance(from, to), width);
            rect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        }

        private static void AddFoldedCorner(RectTransform parent)
        {
            if (parent.Find("FoldedCorner") != null)
            {
                return;
            }

            GameObject fold = new GameObject("FoldedCorner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fold.transform.SetParent(parent, false);
            Image image = fold.GetComponent<Image>();
            image.color = new Color(0.9f, 0.82f, 0.58f, 0.38f);
            image.raycastTarget = false;

            RectTransform rect = fold.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.sizeDelta = new Vector2(28f, 28f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static float GetCardRotation(string name)
        {
            int hash = Mathf.Abs(name.GetHashCode());
            return -1.6f + (hash % 5) * 0.8f;
        }

        private static void CreateLine(Transform parent, Vector2 from, Vector2 to, float width, Color color, string name)
        {
            GameObject line = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
