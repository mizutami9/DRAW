using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class StageEditButtonCommand : MonoBehaviour
    {
        [SerializeField] private StageManager stageManager;
        [SerializeField] private string stageId = "1-1";

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
            button.onClick.AddListener(OpenEditor);
        }

        private void OpenEditor()
        {
            stageManager?.OpenStageEditor(stageId);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
