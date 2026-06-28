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
        [SerializeField] private OnlineManager onlineManager;
        [SerializeField] private Command command;
        private Button button;
        private Vector3 initialScale;

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

            if (onlineManager == null)
            {
                onlineManager = FindObjectOfType<OnlineManager>();
            }

            button = GetComponent<Button>();
            initialScale = transform.localScale;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(Execute);
            RefreshOnlineVisibility();
        }

        private void Update()
        {
            RefreshOnlineVisibility();
        }

        private void Execute()
        {
            if (IsCharacterManagementCommand() && IsOnlineActive())
            {
                EventSystem.current?.SetSelectedGameObject(null);
                return;
            }

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

        private void RefreshOnlineVisibility()
        {
            if (!IsCharacterManagementCommand())
            {
                return;
            }

            bool visible = !IsOnlineActive();
            transform.localScale = visible ? initialScale : Vector3.zero;
            if (button != null)
            {
                button.interactable = visible;
            }
        }

        private bool IsCharacterManagementCommand()
        {
            return command == Command.AddCharacter
                || command == Command.DeleteCharacter
                || command == Command.SwitchCharacter;
        }

        private bool IsOnlineActive()
        {
            if (onlineManager == null)
            {
                return false;
            }

            return onlineManager.State == OnlineConnectionState.InLobby
                || onlineManager.State == OnlineConnectionState.Playing
                || onlineManager.State == OnlineConnectionState.Matching;
        }
    }
}
