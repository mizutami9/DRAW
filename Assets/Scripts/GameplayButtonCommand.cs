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
            SwitchCharacter,
            Continue,
            Option,
            Title,
            Exit,
            LeaveSession
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
                stageManager = FindFirstObjectByType<StageManager>();
            }

            if (uiManager == null)
            {
                uiManager = FindFirstObjectByType<UIManager>();
            }

            if (onlineManager == null)
            {
                onlineManager = FindFirstObjectByType<OnlineManager>();
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
            if (IsCharacterManagementCommand()
                && stageManager != null
                && !stageManager.CanUseGameplayCharacterControls)
            {
                stageManager.ShowReadyRoomOnlyCharacterChangeNotice();
                EventSystem.current?.SetSelectedGameObject(null);
                return;
            }

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
                    uiManager?.HideMenu();
                    stageManager?.Retry();
                    break;
                case Command.Menu:
                    uiManager?.ToggleMenu();
                    break;
                case Command.Continue:
                    uiManager?.HideMenu();
                    break;
                case Command.Option:
                    uiManager?.HideMenu();
                    stageManager?.OpenOptionMenu();
                    break;
                case Command.Title:
                    uiManager?.HideMenu();
                    stageManager?.EnterTitle();
                    break;
                case Command.Exit:
                    uiManager?.HideMenu();
                    stageManager?.ExitGame();
                    break;
                case Command.LeaveSession:
                    stageManager?.RequestLeaveSession();
                    break;
                case Command.CloseDrawing:
                    stageManager?.CancelDrawingMode();
                    break;
                case Command.StageSelect:
                    if (stageManager != null && stageManager.IsOnlineStageActive)
                    {
                        if (stageManager.IsOnlineStageHost)
                        {
                            stageManager.OpenStageSelectFromMultiLobby();
                        }
                    }
                    else
                    {
                        stageManager?.OpenStageSelect();
                    }
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

        public void Configure(Command nextCommand)
        {
            command = nextCommand;
        }

        private void RefreshOnlineVisibility()
        {
            if (!IsCharacterManagementCommand())
            {
                return;
            }

            bool visible = !IsOnlineActive();
            bool available = visible
                && (stageManager == null || stageManager.CanUseGameplayCharacterControls);
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? available ? 1f : 0.42f : 0f;
            group.interactable = available;
            group.blocksRaycasts = available;
            transform.localScale = initialScale;
            if (button != null)
            {
                button.interactable = available;
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
