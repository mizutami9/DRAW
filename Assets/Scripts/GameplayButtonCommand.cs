using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class GameplayButtonCommand : MonoBehaviour
    {
        public enum Command
        {
            Redraw,
            Retry,
            Menu,
            CloseDrawing,
            StageSelect,
            AddCharacter,
            DeleteCharacter,
            SwitchCharacter
        }

        [SerializeField] private StageManager stageManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private Command command;

        private void Awake()
        {
            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (uiManager == null)
            {
                uiManager = FindObjectOfType<UIManager>();
            }

            Button button = GetComponent<Button>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(Execute);
        }

        private void Execute()
        {
            switch (command)
            {
                case Command.Redraw:
                    stageManager?.EnterDrawingMode();
                    break;
                case Command.Retry:
                    stageManager?.Retry();
                    break;
                case Command.Menu:
                    uiManager?.ToggleMenu();
                    break;
                case Command.CloseDrawing:
                    stageManager?.CancelDrawingMode();
                    break;
                case Command.StageSelect:
                    stageManager?.OpenStageSelect();
                    break;
                case Command.AddCharacter:
                    stageManager?.AddCharacter();
                    break;
                case Command.DeleteCharacter:
                    stageManager?.DeleteAddedCharacter();
                    break;
                case Command.SwitchCharacter:
                    stageManager?.SwitchCharacter();
                    break;
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
