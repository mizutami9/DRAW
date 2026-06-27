using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class StageSelectButtonCommand : MonoBehaviour
    {
        [SerializeField] private StageManager stageManager;
        [SerializeField] private string stageId = "1-0";

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
            button.onClick.AddListener(SelectStage);
        }

        private void SelectStage()
        {
            stageManager?.SelectStage(stageId);
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
