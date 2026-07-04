using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    [RequireComponent(typeof(Button))]
    public sealed class StageSelectEditModeButtonCommand : MonoBehaviour
    {
        [SerializeField] private StageManager stageManager;
        [SerializeField] private Text label;

        private Button button;
        private Image background;

        private void Awake()
        {
            if (stageManager == null)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            button = GetComponent<Button>();
            background = GetComponent<Image>();
            if (label == null)
            {
                label = GetComponentInChildren<Text>();
            }

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(ToggleEditMode);
            UpdateView();
        }

        private void OnEnable()
        {
            UpdateView();
        }

        private void ToggleEditMode()
        {
            stageManager?.ToggleStageSelectEditMode();
            UpdateView();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void UpdateView()
        {
            bool editing = stageManager != null && stageManager.StageSelectEditMode;
            if (label != null)
            {
                label.text = editing
                    ? LocalizationManager.T("stage_select_edit_on")
                    : LocalizationManager.T("stage_select_edit_off");
                label.color = Color.black;
            }

            if (background != null)
            {
                background.color = editing
                    ? new Color(0.78f, 0.9f, 1f, 0.95f)
                    : new Color(0.98f, 0.96f, 0.9f, 0.95f);
            }
        }
    }
}
