using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class RuntimeStageEditorButtonCommand : MonoBehaviour
    {
        public enum Command
        {
            AddPlatform,
            AddWall,
            AddSpawn,
            AddGoal,
            Save,
            Test,
            Close,
            Delete,
            ToggleSnap,
            WidthPlus,
            WidthMinus,
            HeightPlus,
            HeightMinus,
            AddBalanceScale,
            AddWeight,
            Undo,
            Redo,
            LinkSource,
            LinkTarget,
            ClearLink,
            ListObjects,
            ListLinks,
            ListPrevious,
            ListNext,
            ListItem0,
            ListItem1,
            ListItem2,
            ListItem3,
            ListItem4,
            ListItem5,
            ListItem6,
            ListItem7
        }

        [SerializeField] private RuntimeStageEditor editor;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private Command command;

        private void Awake()
        {
            if (editor == null)
            {
                editor = FindFirstObjectByType<RuntimeStageEditor>();
            }

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }

            Button button = GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(Execute);
        }

        private void Execute()
        {
            if (editor == null)
            {
                editor = FindFirstObjectByType<RuntimeStageEditor>();
            }

            if (stageManager == null)
            {
                stageManager = FindFirstObjectByType<StageManager>();
            }

            switch (command)
            {
                case Command.AddPlatform:
                    editor?.SetAddType(StageObjectType.Platform);
                    break;
                case Command.AddWall:
                    editor?.SetAddType(StageObjectType.Wall);
                    break;
                case Command.AddSpawn:
                    editor?.SetAddType(StageObjectType.Spawn);
                    break;
                case Command.AddGoal:
                    editor?.SetAddType(StageObjectType.Goal);
                    break;
                case Command.AddBalanceScale:
                    editor?.SetAddType(StageObjectType.BalanceScale);
                    break;
                case Command.AddWeight:
                    editor?.SetAddType(StageObjectType.Weight);
                    break;
                case Command.Save:
                    editor?.Save();
                    break;
                case Command.Test:
                    stageManager?.TestEditedStage();
                    break;
                case Command.Close:
                    stageManager?.CloseStageEditor();
                    break;
                case Command.Delete:
                    editor?.DeleteSelected();
                    break;
                case Command.ToggleSnap:
                    editor?.ToggleSnap();
                    break;
                case Command.WidthPlus:
                    editor?.ResizeSelected(new Vector2(0.5f, 0f));
                    break;
                case Command.WidthMinus:
                    editor?.ResizeSelected(new Vector2(-0.5f, 0f));
                    break;
                case Command.HeightPlus:
                    editor?.ResizeSelected(new Vector2(0f, 0.5f));
                    break;
                case Command.HeightMinus:
                    editor?.ResizeSelected(new Vector2(0f, -0.5f));
                    break;
                case Command.Undo:
                    editor?.Undo();
                    break;
                case Command.Redo:
                    editor?.Redo();
                    break;
                case Command.LinkSource:
                    editor?.MarkSelectedAsLinkSource();
                    break;
                case Command.LinkTarget:
                    editor?.LinkSelectedAsTarget();
                    break;
                case Command.ClearLink:
                    editor?.ClearSelectedLink();
                    break;
                case Command.ListObjects:
                    editor?.SetListModeObjects();
                    break;
                case Command.ListLinks:
                    editor?.SetListModeLinks();
                    break;
                case Command.ListPrevious:
                    editor?.ChangeListPage(-1);
                    break;
                case Command.ListNext:
                    editor?.ChangeListPage(1);
                    break;
                case Command.ListItem0:
                    editor?.SelectListItem(0);
                    break;
                case Command.ListItem1:
                    editor?.SelectListItem(1);
                    break;
                case Command.ListItem2:
                    editor?.SelectListItem(2);
                    break;
                case Command.ListItem3:
                    editor?.SelectListItem(3);
                    break;
                case Command.ListItem4:
                    editor?.SelectListItem(4);
                    break;
                case Command.ListItem5:
                    editor?.SelectListItem(5);
                    break;
                case Command.ListItem6:
                    editor?.SelectListItem(6);
                    break;
                case Command.ListItem7:
                    editor?.SelectListItem(7);
                    break;
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
