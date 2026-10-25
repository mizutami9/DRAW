using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class DrawButtonCommand : MonoBehaviour, IPointerClickHandler
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
                drawManager = FindFirstObjectByType<DrawManager>();
            }

            commandButton = GetComponent<Button>();
            // Commands are dispatched with the pointer position so two local
            // DRAW viewports cannot both accept one hardware-mouse click.
            commandButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (command == Command.Confirm)
            {
                // An occupied species is still actionable: Confirm opens the
                // requester-side confirmation before any swap message is sent.
                bool canConfirm = drawManager != null;
                if (commandButton != null && commandButton.interactable != canConfirm)
                {
                    commandButton.interactable = canConfirm;
                }
            }
        }

        public void BindDrawManager(DrawManager manager)
        {
            drawManager = manager;
        }

        public void RebindForClonedSession(DrawManager manager)
        {
            drawManager = manager;
            if (commandButton == null) commandButton = GetComponent<Button>();
            // A cloned UnityEvent can retain the source panel's runtime target.
            // The clone owns this command, so rebuild its event from scratch.
            commandButton.onClick.RemoveAllListeners();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || drawManager == null
                || !drawManager.OwnsScreenPoint(eventData.position)) return;
            Execute();
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
