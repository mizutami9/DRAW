using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class MultiMenuButtonCommand : MonoBehaviour
    {
        public enum Command
        {
            Choice,
            Random,
            Room,
            CreateRoom,
            JoinRoom,
            Lobby,
            Draw,
            CreateRoomAction,
            JoinRoomAction,
            Ready,
            StartStage,
            CopyLobbyId,
            LeaveLobby,
            BackToTitle
        }

        [SerializeField] private StageManager stageManager;
        [SerializeField] private MultiMenuController controller;
        [SerializeField] private Command command;

        private void Awake()
        {
            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<MultiMenuController>();
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
                case Command.Choice:
                    controller?.ShowChoice();
                    break;
                case Command.Random:
                    controller?.ShowRandom();
                    break;
                case Command.Room:
                    controller?.ShowRoom();
                    break;
                case Command.CreateRoom:
                    controller?.ShowCreateRoom();
                    break;
                case Command.JoinRoom:
                    controller?.ShowJoinRoom();
                    break;
                case Command.Lobby:
                    controller?.ShowLobby();
                    break;
                case Command.CreateRoomAction:
                    controller?.CreateRoom();
                    break;
                case Command.JoinRoomAction:
                    controller?.JoinRoom();
                    break;
                case Command.Ready:
                    controller?.ToggleReady();
                    break;
                case Command.StartStage:
                    controller?.StartStage();
                    break;
                case Command.CopyLobbyId:
                    controller?.CopyLobbyId();
                    break;
                case Command.LeaveLobby:
                    controller?.LeaveLobby();
                    break;
                case Command.Draw:
                    stageManager?.EnterDrawingMode();
                    break;
                case Command.BackToTitle:
                    stageManager?.CloseTitleSubmenu();
                    break;
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
