using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed partial class RuntimeStageEditor
    {
        public void SetListModeObjects()
        {
            listShowsLinks = false;
            listPage = 0;
            RefreshListPanel();
        }

        public void SetListModeLinks()
        {
            listShowsLinks = true;
            listPage = 0;
            RefreshListPanel();
        }

        public void ChangeListPage(int delta)
        {
            BuildVisibleListItems();
            int maxPage = Mathf.Max(0, (visibleListItems.Count - 1) / listItemTexts.Length);
            listPage = Mathf.Clamp(listPage + delta, 0, maxPage);
            RefreshListPanel();
        }

        public void SelectListItem(int localIndex)
        {
            BuildVisibleListItems();
            int index = listPage * listItemTexts.Length + localIndex;
            if (index < 0 || index >= visibleListItems.Count)
            {
                return;
            }

            SelectData(visibleListItems[index]);
            UpdateSelectionBox();
            RefreshText();
            RefreshListPanel();
            SetStatus(LocalizationManager.T("stage_editor_status_selected_from_list"));
        }

        private void EnsureListReferences()
        {
            if (editorPanel == null || listTitleText != null)
            {
                return;
            }

            listTitleText = FindText("RuntimeStageEditorListTitle");
            listPageText = FindText("RuntimeStageEditorListPage");
            for (int i = 0; i < listItemTexts.Length; i++)
            {
                listItemTexts[i] = FindText($"RuntimeStageEditorListItem{i}Label");
            }
        }

        private Text FindText(string objectName)
        {
            Transform target = FindChildRecursive(editorPanel.transform, objectName);
            return target != null ? target.GetComponent<Text>() : null;
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            if (parent.name == objectName)
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void RefreshListPanel()
        {
            EnsureListReferences();
            BuildVisibleListItems();

            if (listTitleText != null)
            {
                listTitleText.text = LocalizationManager.T(listShowsLinks ? "stage_editor_link_list" : "stage_editor_object_list");
            }

            SetListTabState("RuntimeStageEditorObjectsTab", !listShowsLinks, new Color(0.62f, 0.84f, 1f, 1f));
            SetListTabState("RuntimeStageEditorLinksTab", listShowsLinks, new Color(0.62f, 0.92f, 0.66f, 1f));

            int pageSize = listItemTexts.Length;
            int maxPage = Mathf.Max(0, (visibleListItems.Count - 1) / pageSize);
            listPage = Mathf.Clamp(listPage, 0, maxPage);
            if (listPageText != null)
            {
                listPageText.text = $"{listPage + 1} / {maxPage + 1}  ({visibleListItems.Count})";
            }

            for (int i = 0; i < listItemTexts.Length; i++)
            {
                Text itemText = listItemTexts[i];
                if (itemText == null)
                {
                    continue;
                }

                int index = listPage * pageSize + i;
                Button itemButton = itemText.GetComponentInParent<Button>();
                if (index >= visibleListItems.Count)
                {
                    itemText.text = "";
                    if (itemButton != null)
                    {
                        itemButton.interactable = false;
                    }
                    continue;
                }

                StageObjectData data = visibleListItems[index];
                if (itemButton != null)
                {
                    itemButton.interactable = true;
                    Image image = itemButton.GetComponent<Image>();
                    if (image != null)
                    {
                        image.color = data == selectedData
                            ? new Color(0.64f, 0.86f, 1f, 1f)
                            : (i % 2 == 0 ? Color.white : new Color(0.95f, 0.98f, 1f, 1f));
                    }
                }

                string selectedMark = data == selectedData ? ">" : " ";
                if (listShowsLinks)
                {
                    string targetLabel = GetObjectLabelById(data.linkTargetId);
                    itemText.text = $"{selectedMark} {GetObjectLabel(data.type)} #{GetShortObjectId(data)}\n   -> {targetLabel} [{GetLinkActionListLabel(data.linkAction)}]";
                }
                else
                {
                    string separate = data.keepSeparate
                        ? $" [{LocalizationManager.T("stage_editor_separate")}]"
                        : string.Empty;
                    itemText.text = $"{selectedMark} {index + 1}. {GetObjectLabel(data.type)}{separate}  #{GetShortObjectId(data)}\n   {data.position.x:0.0}, {data.position.y:0.0}";
                }
            }
        }

        private void FocusListPageOn(StageObjectData data)
        {
            if (data == null || listShowsLinks || listItemTexts.Length == 0)
            {
                return;
            }

            BuildVisibleListItems();
            int index = visibleListItems.IndexOf(data);
            if (index >= 0)
            {
                listPage = index / listItemTexts.Length;
            }
        }

        private static string GetShortObjectId(StageObjectData data)
        {
            string id = data != null ? data.objectId : string.Empty;
            if (string.IsNullOrEmpty(id))
            {
                return "-----";
            }

            int separator = id.LastIndexOf('_');
            string suffix = separator >= 0 && separator + 1 < id.Length ? id.Substring(separator + 1) : id;
            return suffix.Length > 5 ? suffix.Substring(suffix.Length - 5) : suffix;
        }

        private void SetListTabState(string objectName, bool selected, Color selectedColor)
        {
            Transform target = FindChildRecursive(editorPanel != null ? editorPanel.transform : null, objectName);
            Image image = target != null ? target.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = selected ? selectedColor : new Color(0.92f, 0.92f, 0.88f, 1f);
            }
        }

        private void BuildVisibleListItems()
        {
            visibleListItems.Clear();
            for (int i = 0; i < objects.Count; i++)
            {
                StageObjectData data = objects[i];
                if (data == null)
                {
                    continue;
                }

                if (listShowsLinks && string.IsNullOrEmpty(data.linkTargetId))
                {
                    continue;
                }

                visibleListItems.Add(data);
            }
        }

        private string GetObjectLabelById(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return LocalizationManager.T("stage_editor_none");
            }

            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null && objects[i].objectId == objectId)
                {
                    return GetObjectLabel(objects[i].type);
                }
            }

            return objectId;
        }

        private static string GetLinkActionListLabel(string action)
        {
            if (action == "Hide")
            {
                return LocalizationManager.T("stage_editor_link_mode_hide");
            }

            if (action == "Unlock")
            {
                return LocalizationManager.T("stage_editor_link_mode_unlock");
            }

            return LocalizationManager.T("stage_editor_link_mode_reveal");
        }
    }
}
