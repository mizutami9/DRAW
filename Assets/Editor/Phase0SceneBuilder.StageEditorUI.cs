using DrawBody.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.EditorTools
{
    public static partial class Phase0SceneBuilder
    {
        private static GameObject CreateRuntimeStageEditorPanel(Transform parent, Font font, StageManager stageManager, out RectTransform uiBlocker, out Text stageText, out Text selectedText, out Text statusText, out Dropdown categoryDropdown, out Dropdown objectTypeDropdown, out InputField searchInput)
        {
            GameObject panel = new GameObject("RuntimeStageEditorPanel");
            panel.transform.SetParent(parent, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            Stretch(panelRect);

            stageText = CreateText("RuntimeStageEditorTitle", panel.transform, font, 28, TextAnchor.UpperLeft);
            stageText.text = LocalizationManager.Format("stage_label", "1-1");
            stageText.color = Color.black;
            stageText.rectTransform.anchorMin = new Vector2(0f, 1f);
            stageText.rectTransform.anchorMax = new Vector2(0f, 1f);
            stageText.rectTransform.pivot = new Vector2(0f, 1f);
            stageText.rectTransform.anchoredPosition = new Vector2(24f, -18f);
            stageText.rectTransform.sizeDelta = new Vector2(300f, 42f);

            GameObject listPanel = CreatePanel("RuntimeStageEditorListPanel", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.86f));
            AddUiOutline(listPanel, new Color(0.12f, 0.11f, 0.1f, 0.72f), new Vector2(2f, -2f));
            RectTransform listRect = listPanel.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 1f);
            listRect.anchorMax = new Vector2(0f, 1f);
            listRect.pivot = new Vector2(0f, 1f);
            listRect.anchoredPosition = new Vector2(24f, -72f);
            listRect.sizeDelta = new Vector2(318f, 360f);

            Text listTitle = CreateText("RuntimeStageEditorListTitle", listPanel.transform, font, 20, TextAnchor.UpperCenter);
            listTitle.text = LocalizationManager.T("stage_editor_object_list");
            AddLocalizedText(listTitle.gameObject, "stage_editor_object_list");
            listTitle.color = Color.black;
            listTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            listTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            listTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            listTitle.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            listTitle.rectTransform.sizeDelta = new Vector2(-24f, 28f);

            Button objectsTab = CreateButton("RuntimeStageEditorObjectsTab", listPanel.transform, font, LocalizationManager.T("stage_editor_objects_tab"), new Vector2(-74f, 88f), new Vector2(132f, 30f), new Color(0.88f, 0.94f, 1f, 0.92f), "stage_editor_objects_tab");
            Button linksTab = CreateButton("RuntimeStageEditorLinksTab", listPanel.transform, font, LocalizationManager.T("stage_editor_links_tab"), new Vector2(74f, 88f), new Vector2(132f, 30f), new Color(0.88f, 1f, 0.9f, 0.92f), "stage_editor_links_tab");
            SetButtonLabelColor(objectsTab, Color.black);
            SetButtonLabelColor(linksTab, Color.black);
            AddRuntimeStageEditorCommand(objectsTab.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ListObjects);
            AddRuntimeStageEditorCommand(linksTab.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ListLinks);

            RuntimeStageEditorButtonCommand.Command[] listCommands =
            {
                RuntimeStageEditorButtonCommand.Command.ListItem0,
                RuntimeStageEditorButtonCommand.Command.ListItem1,
                RuntimeStageEditorButtonCommand.Command.ListItem2,
                RuntimeStageEditorButtonCommand.Command.ListItem3,
                RuntimeStageEditorButtonCommand.Command.ListItem4,
            };

            for (int i = 0; i < listCommands.Length; i++)
            {
                Button itemButton = CreateButton($"RuntimeStageEditorListItem{i}", listPanel.transform, font, "", new Vector2(0f, 48f - i * 32f), new Vector2(278f, 28f), new Color(0.98f, 0.96f, 0.9f, 0.88f));
                Text itemLabel = itemButton.GetComponentInChildren<Text>();
                if (itemLabel != null)
                {
                    itemLabel.gameObject.name = $"RuntimeStageEditorListItem{i}Label";
                    itemLabel.alignment = TextAnchor.MiddleLeft;
                    itemLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
                    itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
                    itemLabel.color = Color.black;
                }

                AddRuntimeStageEditorCommand(itemButton.gameObject, stageManager, listCommands[i]);
            }

            Button listPrev = CreateButton("RuntimeStageEditorListPrev", listPanel.transform, font, "◀", new Vector2(-88f, 132f), new Vector2(48f, 26f), new Color(0.98f, 0.94f, 0.82f, 0.92f));
            Button listNext = CreateButton("RuntimeStageEditorListNext", listPanel.transform, font, "▶", new Vector2(88f, 132f), new Vector2(48f, 26f), new Color(0.98f, 0.94f, 0.82f, 0.92f));
            SetButtonLabelColor(listPrev, Color.black);
            SetButtonLabelColor(listNext, Color.black);
            AddRuntimeStageEditorCommand(listPrev.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ListPrevious);
            AddRuntimeStageEditorCommand(listNext.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ListNext);

            Text listPage = CreateText("RuntimeStageEditorListPage", listPanel.transform, font, 16, TextAnchor.MiddleCenter);
            listPage.text = "1 / 1";
            listPage.color = Color.black;
            listPage.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            listPage.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            listPage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            listPage.rectTransform.anchoredPosition = new Vector2(0f, -60f);
            listPage.rectTransform.sizeDelta = new Vector2(90f, 26f);

            GameObject toolPanel = CreatePanel("RuntimeStageEditorTools", panel.transform, new Color(0.96f, 0.93f, 0.86f, 0.9f));
            AddUiOutline(toolPanel, new Color(0.12f, 0.11f, 0.1f, 0.72f), new Vector2(2f, -2f));
            RectTransform toolRect = toolPanel.GetComponent<RectTransform>();
            toolRect.anchorMin = new Vector2(1f, 1f);
            toolRect.anchorMax = new Vector2(1f, 1f);
            toolRect.pivot = new Vector2(1f, 1f);
            toolRect.anchoredPosition = new Vector2(-24f, -36f);
            toolRect.sizeDelta = new Vector2(360f, 560f);
            uiBlocker = toolRect;

            Text help = CreateText("RuntimeStageEditorHelp", toolPanel.transform, font, 15, TextAnchor.UpperLeft);
            help.text = LocalizationManager.T("stage_editor_help_runtime");
            AddLocalizedText(help.gameObject, "stage_editor_help_runtime");
            help.color = Color.black;
            help.rectTransform.anchorMin = new Vector2(0f, 1f);
            help.rectTransform.anchorMax = new Vector2(1f, 1f);
            help.rectTransform.pivot = new Vector2(0.5f, 1f);
            help.rectTransform.anchoredPosition = new Vector2(12f, -12f);
            help.rectTransform.sizeDelta = new Vector2(-24f, 62f);

            Text categoryLabel = CreateText("RuntimeStageEditorCategoryLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            categoryLabel.text = LocalizationManager.T("stage_editor_category");
            AddLocalizedText(categoryLabel.gameObject, "stage_editor_category");
            categoryLabel.color = Color.black;
            categoryLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            categoryLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            categoryLabel.rectTransform.pivot = new Vector2(0f, 1f);
            categoryLabel.rectTransform.anchoredPosition = new Vector2(24f, -78f);
            categoryLabel.rectTransform.sizeDelta = new Vector2(90f, 24f);

            categoryDropdown = CreateStageCategoryDropdown(
                "RuntimeStageCategoryDropdown",
                toolPanel.transform,
                font,
                new Vector2(22f, -104f),
                new Vector2(316f, 40f));

            Text searchLabel = CreateText("RuntimeStageEditorSearchLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            searchLabel.text = LocalizationManager.T("stage_editor_search");
            AddLocalizedText(searchLabel.gameObject, "stage_editor_search");
            searchLabel.color = Color.black;
            searchLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            searchLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            searchLabel.rectTransform.pivot = new Vector2(0f, 1f);
            searchLabel.rectTransform.anchoredPosition = new Vector2(24f, -148f);
            searchLabel.rectTransform.sizeDelta = new Vector2(90f, 24f);

            searchInput = CreateStageSearchInput(
                "RuntimeStageSearchInput",
                toolPanel.transform,
                font,
                new Vector2(22f, -174f),
                new Vector2(316f, 40f));

            Text typeLabel = CreateText("RuntimeStageEditorTypeLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            typeLabel.text = LocalizationManager.T("stage_editor_type");
            AddLocalizedText(typeLabel.gameObject, "stage_editor_type");
            typeLabel.color = Color.black;
            typeLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            typeLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            typeLabel.rectTransform.pivot = new Vector2(0f, 1f);
            typeLabel.rectTransform.anchoredPosition = new Vector2(24f, -218f);
            typeLabel.rectTransform.sizeDelta = new Vector2(70f, 24f);

            objectTypeDropdown = CreateStageObjectDropdown(
                "RuntimeStageObjectTypeDropdown",
                toolPanel.transform,
                font,
                new Vector2(22f, -244f),
                new Vector2(316f, 42f));

            selectedText = CreateText("RuntimeStageEditorSelected", toolPanel.transform, font, 15, TextAnchor.UpperLeft);
            selectedText.text = LocalizationManager.Format("stage_editor_selected_add", LocalizationManager.T("stage_object_platform"), "ON");
            selectedText.color = Color.black;
            selectedText.rectTransform.anchorMin = new Vector2(0f, 1f);
            selectedText.rectTransform.anchorMax = new Vector2(1f, 1f);
            selectedText.rectTransform.pivot = new Vector2(0.5f, 1f);
            selectedText.rectTransform.anchoredPosition = new Vector2(12f, -294f);
            selectedText.rectTransform.sizeDelta = new Vector2(-24f, 34f);

            Text sizeLabel = CreateText("RuntimeStageEditorSizeLabel", toolPanel.transform, font, 15, TextAnchor.MiddleLeft);
            sizeLabel.text = LocalizationManager.T("stage_editor_size");
            sizeLabel.color = Color.black;
            AddLocalizedText(sizeLabel.gameObject, "stage_editor_size");
            sizeLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            sizeLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            sizeLabel.rectTransform.pivot = new Vector2(0f, 1f);
            sizeLabel.rectTransform.anchoredPosition = new Vector2(24f, -334f);
            sizeLabel.rectTransform.sizeDelta = new Vector2(70f, 28f);

            Button widthMinus = CreateButton("RuntimeEditWidthMinus", toolPanel.transform, font, LocalizationManager.T("stage_editor_width_minus"), new Vector2(-108f, 154f), new Vector2(62f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_width_minus");
            Button widthPlus = CreateButton("RuntimeEditWidthPlus", toolPanel.transform, font, LocalizationManager.T("stage_editor_width_plus"), new Vector2(-36f, 154f), new Vector2(62f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_width_plus");
            Button heightMinus = CreateButton("RuntimeEditHeightMinus", toolPanel.transform, font, LocalizationManager.T("stage_editor_height_minus"), new Vector2(36f, 154f), new Vector2(62f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_height_minus");
            Button heightPlus = CreateButton("RuntimeEditHeightPlus", toolPanel.transform, font, LocalizationManager.T("stage_editor_height_plus"), new Vector2(108f, 154f), new Vector2(62f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_height_plus");
            SetButtonLabelColor(widthMinus, Color.black);
            SetButtonLabelColor(widthPlus, Color.black);
            SetButtonLabelColor(heightMinus, Color.black);
            SetButtonLabelColor(heightPlus, Color.black);
            AddRuntimeStageEditorCommand(widthMinus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.WidthMinus);
            AddRuntimeStageEditorCommand(widthPlus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.WidthPlus);
            AddRuntimeStageEditorCommand(heightMinus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.HeightMinus);
            AddRuntimeStageEditorCommand(heightPlus.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.HeightPlus);

            Button undo = CreateButton("RuntimeEditUndoButton", toolPanel.transform, font, LocalizationManager.T("undo"), new Vector2(-126f, 116f), new Vector2(70f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "undo");
            Button redo = CreateButton("RuntimeEditRedoButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_redo"), new Vector2(-42f, 116f), new Vector2(70f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_redo");
            Button snap = CreateButton("RuntimeEditSnapButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_snap_attach"), new Vector2(42f, 116f), new Vector2(70f, 30f), new Color(0.98f, 0.96f, 0.9f, 0.92f), "stage_editor_snap_attach");
            Button delete = CreateButton("RuntimeEditDeleteButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_delete"), new Vector2(126f, 116f), new Vector2(70f, 30f), new Color(0.98f, 0.78f, 0.72f, 0.92f), "stage_editor_delete");
            SetButtonLabelColor(undo, Color.black);
            SetButtonLabelColor(redo, Color.black);
            SetButtonLabelColor(snap, Color.black);
            SetButtonLabelColor(delete, Color.black);
            AddRuntimeStageEditorCommand(undo.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Undo);
            AddRuntimeStageEditorCommand(redo.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Redo);
            AddRuntimeStageEditorCommand(snap.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ToggleSnap);
            AddRuntimeStageEditorCommand(delete.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Delete);

            Button linkSource = CreateButton("RuntimeEditLinkSourceButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_link_source"), new Vector2(-104f, 78f), new Vector2(90f, 30f), new Color(0.88f, 0.94f, 1f, 0.92f), "stage_editor_link_source");
            Button linkTarget = CreateButton("RuntimeEditLinkTargetButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_link_target"), new Vector2(0f, 78f), new Vector2(90f, 30f), new Color(0.88f, 1f, 0.9f, 0.92f), "stage_editor_link_target");
            Button clearLink = CreateButton("RuntimeEditClearLinkButton", toolPanel.transform, font, LocalizationManager.T("stage_editor_unlink"), new Vector2(104f, 78f), new Vector2(90f, 30f), new Color(0.98f, 0.88f, 0.78f, 0.92f), "stage_editor_unlink");
            SetButtonLabelColor(linkSource, Color.black);
            SetButtonLabelColor(linkTarget, Color.black);
            SetButtonLabelColor(clearLink, Color.black);
            AddRuntimeStageEditorCommand(linkSource.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.LinkSource);
            AddRuntimeStageEditorCommand(linkTarget.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.LinkTarget);
            AddRuntimeStageEditorCommand(clearLink.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.ClearLink);

            Button save = CreateButton("RuntimeEditSaveButton", toolPanel.transform, font, "F5 " + LocalizationManager.T("stage_editor_save"), new Vector2(-78f, 38f), new Vector2(118f, 34f), new Color(0.78f, 0.9f, 1f, 0.92f));
            Button test = CreateButton("RuntimeEditTestButton", toolPanel.transform, font, "F6 " + LocalizationManager.T("stage_editor_test"), new Vector2(78f, 38f), new Vector2(118f, 34f), new Color(0.76f, 0.95f, 0.76f, 0.92f));
            SetButtonLabelColor(save, Color.black);
            SetButtonLabelColor(test, Color.black);
            AddRuntimeStageEditorCommand(save.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Save);
            AddRuntimeStageEditorCommand(test.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Test);

            Button close = CreateButton("RuntimeEditCloseButton", panel.transform, font, "Esc " + LocalizationManager.T("stage_editor_back"), new Vector2(0f, 18f), new Vector2(260f, 38f), new Color(0.98f, 0.96f, 0.9f, 0.92f));
            SetButtonLabelColor(close, Color.black);
            AddRuntimeStageEditorCommand(close.gameObject, stageManager, RuntimeStageEditorButtonCommand.Command.Close);

            statusText = CreateText("RuntimeStageEditorStatus", panel.transform, font, 16, TextAnchor.LowerLeft);
            statusText.text = "";
            statusText.color = Color.black;
            statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
            statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.anchoredPosition = new Vector2(24f, 18f);
            statusText.rectTransform.sizeDelta = new Vector2(-48f, 30f);

            panel.AddComponent<StageEditorVisualPolisher>();
            panel.SetActive(false);
            return panel;
        }
    }
}
