using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class StageSelectVisualPolisher : MonoBehaviour
    {
        private bool polished;

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
            LocalizationManager.LanguageChanged += RefreshLocalizedText;
            Polish();
            RefreshLocalizedText();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedText;
        }

        public void Polish()
        {
            if (polished)
            {
                return;
            }

            polished = true;
            HideTitle();
            PolishWorldCards();
            PolishButtons();
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
                    image.color = GetWorldCardColor(i);
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
                    image.color = GetWorldCardColor(ParseWorldNumber(rect.name));
                }

                if (rect.GetComponent<SketchPaperTexture>() == null)
                {
                    rect.gameObject.AddComponent<SketchPaperTexture>();
                }

                RemoveIfExists(rect, "WorldMaskingTapeA");
                RemoveIfExists(rect, "WorldMaskingTapeB");
                RemoveIfExists(rect, "FoldedCorner");
                RemoveShadow(rect.gameObject);
                AddBoldFrame(rect, "WorldBoldFrame", 3.1f, new Color(0.18f, 0.12f, 0.07f, 0.58f));
                rect.localRotation = Quaternion.identity;
                LayoutStageButtons(rect);
                BuildSpeciesRow(rect, ParseWorldNumber(rect.name));
            }
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

                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 146f - (variant - 1) * 54f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 42f);
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
            RefreshSpeciesRowText();
            RefreshStageCreationLabels();
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
                    texts[i].fontSize = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese ? 11 : 8;
                    texts[i].fontStyle = FontStyle.Bold;
                }
            }
        }

        private void RefreshStageCreationLabels()
        {
            StageSelectButtonCommand[] commands = GetComponentsInChildren<StageSelectButtonCommand>(true);
            for (int i = 0; i < commands.Length; i++)
            {
                StageSelectButtonCommand command = commands[i];
                if (command == null)
                {
                    continue;
                }

                Transform statusTransform = command.transform.Find("DebugCreationStatus");
                Text status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
                if (status == null)
                {
                    continue;
                }

                bool created = Resources.Load<TextAsset>($"Stages/{command.StageId}") != null;
                status.text = LocalizationManager.T(created
                    ? "stage_select_debug_created"
                    : "stage_select_debug_not_created");
                status.color = created
                    ? new Color(0.08f, 0.48f, 0.22f, 1f)
                    : new Color(0.68f, 0.18f, 0.12f, 1f);
                status.fontSize = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese ? 11 : 9;
            }
        }

        private static string BuildSpeciesNames(StageSpeciesMask availability)
        {
            System.Collections.Generic.IReadOnlyList<DrawManager.Species> species = StageSpeciesRules.GetOrderedSpecies();
            System.Text.StringBuilder value = new System.Text.StringBuilder();
            string separator = LocalizationManager.CurrentLanguage == LocalizationManager.Language.Japanese ? "\u30fb" : " / ";
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
                RemoveShadow(button.gameObject);
                AddBoldFrame(rect, "ButtonBoldFrame", 2.7f, new Color(0.2f, 0.14f, 0.08f, 0.52f));
                rect.localRotation = Quaternion.identity;

                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    StageSelectButtonCommand stageCommand = button.GetComponent<StageSelectButtonCommand>();
                    if (stageCommand != null)
                    {
                        LayoutStageButtonLabel(label.rectTransform);
                        CreateStageCreationLabel(rect, label.font);
                    }
                    label.transform.SetAsLastSibling();
                }
            }
        }

        private static void LayoutStageButtonLabel(RectTransform label)
        {
            label.anchorMin = new Vector2(0f, 1f);
            label.anchorMax = new Vector2(1f, 1f);
            label.pivot = new Vector2(0.5f, 1f);
            label.anchoredPosition = new Vector2(0f, -1f);
            label.sizeDelta = new Vector2(0f, 25f);
        }

        private static void CreateStageCreationLabel(RectTransform button, Font font)
        {
            Transform existing = button.Find("DebugCreationStatus");
            if (existing != null)
            {
                existing.SetAsLastSibling();
                return;
            }

            GameObject statusObject = new GameObject(
                "DebugCreationStatus",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            statusObject.transform.SetParent(button, false);

            Text status = statusObject.GetComponent<Text>();
            status.font = font;
            status.fontStyle = FontStyle.Bold;
            status.alignment = TextAnchor.MiddleCenter;
            status.raycastTarget = false;
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
            status.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = status.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, 16f);
        }

        private static void AddShadow(GameObject target, Vector2 distance, float alpha)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.1f, 0.07f, 0.03f, alpha);
            shadow.effectDistance = distance;
        }

        private static void RemoveShadow(GameObject target)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow != null)
            {
                Destroy(shadow);
            }
        }

        private static void AddBoldFrame(RectTransform parent, string name, float width, Color color)
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

            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private static void RemoveIfExists(RectTransform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        private static void AddMaskingTape(RectTransform parent, string name, Vector2 position, float rotation)
        {
            if (parent.Find(name) != null)
            {
                return;
            }

            GameObject tape = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tape.transform.SetParent(parent, false);
            Image image = tape.GetComponent<Image>();
            image.color = new Color(1f, 0.9f, 0.58f, 0.52f);
            image.raycastTarget = false;

            RectTransform rect = tape.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(66f, 18f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
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
