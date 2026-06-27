using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class TitleButtonCommand : MonoBehaviour
    {
        public enum Command
        {
            Single,
            Multi,
            Draw,
            Option,
            Exit,
            Title,
            Back,
            RandomMatch,
            Room
        }

        [SerializeField] private StageManager stageManager;
        [SerializeField] private Text statusText;
        [SerializeField] private Command command;

        private void Awake()
        {
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
            switch (command)
            {
                case Command.Single:
                    stageManager?.OpenSingleMenu();
                    break;
                case Command.Multi:
                    stageManager?.OpenMultiMenu();
                    break;
                case Command.Draw:
                    stageManager?.EnterDrawingMode();
                    break;
                case Command.Option:
                    stageManager?.OpenOptionMenu();
                    break;
                case Command.Exit:
                    stageManager?.ExitGame();
                    break;
                case Command.Title:
                    stageManager?.EnterTitle();
                    break;
                case Command.Back:
                    stageManager?.CloseTitleSubmenu();
                    break;
                case Command.RandomMatch:
                    SetStatus("Matching...\n[.....]");
                    break;
                case Command.Room:
                    SetStatus("Create Room\nRoom Name  [........]\nPlayers  2 / 3 / 4\nPublic / Private\n\nJoin Room\nRoom ID  [......]");
                    break;
            }

            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }
    }
}
