using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static GameObject CreateStageSelectPanel(Transform parent, Font font, StageManager stageManager)
        {
            GameObject panel = CreatePanel("StageSelectPanel", parent, new Color(0.965f, 0.945f, 0.88f, 0.98f));
            AddPaperTexture(panel, new Color(0.965f, 0.945f, 0.88f, 1f), new Color(0.74f, 0.66f, 0.5f, 1f), 0.08f, 8811);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Stretch(panelRect);

            Button debugButton = CreateButton("Stage_1_0_Button", panel.transform, font, LocalizationManager.T("debug_stage"), new Vector2(-470f, 646f), new Vector2(170f, 44f), new Color(0.72f, 0.95f, 0.72f, 0.95f));
            SetButtonLabelColor(debugButton, Color.black);
            AddLocalizedText(debugButton.GetComponentInChildren<Text>().gameObject, "debug_stage");
            AddStageSelectCommand(debugButton.gameObject, stageManager, "1-0");

            Button editButton = CreateButton("StageSelectEditModeButton", panel.transform, font, LocalizationManager.T("stage_select_edit_off"), new Vector2(-520f, 36f), new Vector2(170f, 54f), new Color(0.98f, 0.96f, 0.9f, 0.95f));
            SetButtonLabelColor(editButton, Color.black);
            SetButtonLabelFontSize(editButton, 20);
            AddSketchFrame(editButton.transform, new Vector2(170f, 54f), new Color(0.25f, 0.18f, 0.12f, 0.5f), 1.5f);
            StageSelectEditModeButtonCommand editModeCommand = editButton.gameObject.AddComponent<StageSelectEditModeButtonCommand>();
            AssignObject(editModeCommand, "stageManager", stageManager);
            AssignObject(editModeCommand, "label", editButton.GetComponentInChildren<Text>());

            const int groups = 15;
            const int variants = 3;
            const int worldsPerPage = 5;
            const int pageCount = 3;
            float startX = -456f;
            float cardY = 184f;
            float groupSpacingX = 228f;
            Vector2 cardSize = new Vector2(200f, 330f);
            GameObject[] pages = new GameObject[pageCount];

            for (int page = 0; page < pageCount; page++)
            {
                GameObject pageObject = new GameObject($"StageSelectPage{page + 1}");
                pageObject.transform.SetParent(panel.transform, false);
                RectTransform pageRect = pageObject.AddComponent<RectTransform>();
                Stretch(pageRect);
                pageObject.SetActive(page == 0);
                pages[page] = pageObject;
            }

            for (int group = 1; group <= groups; group++)
            {
                int page = (group - 1) / worldsPerPage;
                int column = (group - 1) % worldsPerPage;
                float x = startX + column * groupSpacingX;

                GameObject card = CreatePanel($"World{group}Card", pages[page].transform, new Color(0.98f, 0.94f, 0.82f, 0.92f));
                RectTransform cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.5f, 0f);
                cardRect.anchorMax = new Vector2(0.5f, 0f);
                cardRect.pivot = new Vector2(0.5f, 0f);
                cardRect.anchoredPosition = new Vector2(x, cardY);
                cardRect.sizeDelta = cardSize;
                AddSketchFrame(card.transform, cardSize, new Color(0.25f, 0.18f, 0.12f, 0.58f), 1.6f);

                Text groupLabel = CreateText($"StageGroup{group}Label", card.transform, font, 26, TextAnchor.MiddleCenter);
                groupLabel.text = LocalizationManager.Format("stage_world_label", group);
                groupLabel.color = Color.black;
                groupLabel.fontStyle = FontStyle.Bold;
                groupLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
                groupLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
                groupLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
                groupLabel.rectTransform.anchoredPosition = new Vector2(0f, -22f);
                groupLabel.rectTransform.sizeDelta = new Vector2(-28f, 44f);

                CreateIconLine(card.transform, new Vector2(-72f, 250f), new Vector2(72f, 250f), 2.2f, new Color(0.95f, 0.75f, 0.12f, 0.86f));

                for (int variant = 1; variant <= variants; variant++)
                {
                    string stageId = $"{group}-{variant}";
                    Button button = CreateButton(
                        $"Stage_{group}_{variant}_Button",
                        card.transform,
                        font,
                        $"{stageId}    \u25a1",
                        new Vector2(0f, 164f - (variant - 1) * 62f),
                        new Vector2(156f, 46f),
                        new Color(0.98f, 0.96f, 0.9f, 0.95f));
                    SetButtonLabelColor(button, Color.black);
                    SetButtonLabelFontSize(button, 22);
                    StageCardHover hover = button.gameObject.AddComponent<StageCardHover>();
                    AssignObject(hover, "targetImage", button.GetComponent<Image>());
                    AssignColor(hover, "normalColor", new Color(0.98f, 0.96f, 0.9f, 0.95f));
                    AssignColor(hover, "hoverColor", new Color(1f, 0.88f, 0.34f, 0.98f));
                    AddStageSelectCommand(button.gameObject, stageManager, stageId);
                }
            }

            Button previous = CreateButton("StageSelectPreviousPageButton", panel.transform, font, "\u25c0", new Vector2(-110f, 96f), new Vector2(70f, 46f), new Color(0.98f, 0.94f, 0.82f, 0.94f));
            Button next = CreateButton("StageSelectNextPageButton", panel.transform, font, "\u25b6", new Vector2(110f, 96f), new Vector2(70f, 46f), new Color(0.98f, 0.94f, 0.82f, 0.94f));
            SetButtonLabelColor(previous, Color.black);
            SetButtonLabelColor(next, Color.black);
            SetButtonLabelFontSize(previous, 26);
            SetButtonLabelFontSize(next, 26);

            Text pageText = CreateText("StageSelectPageText", panel.transform, font, 22, TextAnchor.MiddleCenter);
            pageText.text = "1 / 3";
            pageText.color = Color.black;
            pageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            pageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            pageText.rectTransform.pivot = new Vector2(0.5f, 0f);
            pageText.rectTransform.anchoredPosition = new Vector2(0f, 102f);
            pageText.rectTransform.sizeDelta = new Vector2(120f, 34f);

            StageSelectPageController pageController = panel.AddComponent<StageSelectPageController>();
            AssignObjectArray(pageController, "pages", pages);
            AssignObject(pageController, "pageText", pageText);
            AssignObject(pageController, "previousButton", previous);
            AssignObject(pageController, "nextButton", next);

            Button backButton = CreateButton("StageSelectBackButton", panel.transform, font, LocalizationManager.T("stage_editor_back"), new Vector2(520f, 36f), new Vector2(150f, 54f), new Color(0.98f, 0.78f, 0.72f, 0.95f), "stage_editor_back");
            SetButtonLabelColor(backButton, Color.black);
            SetButtonLabelFontSize(backButton, 22);
            AddSketchFrame(backButton.transform, new Vector2(150f, 54f), new Color(0.25f, 0.18f, 0.12f, 0.5f), 1.5f);
            AddTitleCommand(backButton.gameObject, stageManager, TitleButtonCommand.Command.StageSelectBack);
            panel.AddComponent<StageSelectVisualPolisher>();

            return panel;
        }
    }
}
