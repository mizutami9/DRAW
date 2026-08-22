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
        private Button commandButton;

        private void Awake()
        {
            if (drawManager == null)
            {
                drawManager = FindObjectOfType<DrawManager>();
            }

            commandButton = GetComponent<Button>();
            commandButton.onClick.AddListener(Execute);
        }

        private void Update()
        {
            if (command == Command.Confirm)
            {
                // An occupied species is still actionable: Confirm sends a swap
                // request instead of completing the drawing immediately.
                bool canConfirm = drawManager != null;
                if (commandButton != null && commandButton.interactable != canConfirm)
                {
                    commandButton.interactable = canConfirm;
                }
            }
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
