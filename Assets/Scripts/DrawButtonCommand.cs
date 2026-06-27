using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class DrawButtonCommand : MonoBehaviour
    {
        public enum Command
        {
            Clear,
            Confirm,
            Undo,
            BrushSize,
            ToolMode
        }

        [SerializeField] private DrawManager drawManager;
        [SerializeField] private Command command;
        [SerializeField] private int intValue;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            Button button = GetComponent<Button>();
            button.onClick.AddListener(Execute);
        }

        private void Execute()
        {
            if (drawManager == null)
            {
                return;
            }

            if (command == Command.Clear)
            {
                drawManager.ClearDrawing();
            }
            else if (command == Command.Confirm)
            {
                drawManager.ConfirmDrawing();
            }
            else if (command == Command.Undo)
            {
                drawManager.UndoLastStroke();
            }
            else if (command == Command.BrushSize)
            {
                drawManager.SetBrushSize(intValue);
            }
            else if (command == Command.ToolMode)
            {
                drawManager.SetToolMode(intValue);
            }
        }
    }
}
