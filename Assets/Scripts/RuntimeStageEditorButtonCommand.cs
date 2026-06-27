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
            AddWeight
        }

        [SerializeField] private RuntimeStageEditor editor;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private Command command;

        private void Awake()
        {
            if (editor == null)
            {
                editor = FindObjectOfType<RuntimeStageEditor>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
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
                editor = FindObjectOfType<RuntimeStageEditor>();
            }

            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
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
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
