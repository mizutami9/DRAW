using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private GameObject drawingHintPanel;
        [SerializeField] private GameObject clearPanel;
        [SerializeField] private GameObject gameplayHudPanel;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject titlePanel;
        [SerializeField] private GameObject multiPanel;
        [SerializeField] private GameObject optionPanel;
        [SerializeField] private GameObject stageSelectPanel;
        [SerializeField] private GameObject stageEditorPanel;
        [SerializeField] private Text statusText;

        private bool drawing;
        private bool cleared;
        private bool titleShowing;
        private bool multiShowing;
        private bool optionShowing;
        private bool stageSelecting;
        private bool stageEditing;

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += RefreshText;
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshText;
        }

        public void SetDrawing(bool drawing)
        {
            this.drawing = drawing;
            if (drawingHintPanel != null)
            {
                drawingHintPanel.SetActive(drawing);
            }

            RefreshHudVisibility();
            RefreshText();
        }

        public void ToggleMenu()
        {
            if (stageSelecting || titleShowing)
            {
                return;
            }

            if (menuPanel == null)
            {
                return;
            }

            menuPanel.SetActive(!menuPanel.activeSelf);
            RefreshHudVisibility();
        }

        public void HideMenu()
        {
            if (menuPanel != null)
            {
                menuPanel.SetActive(false);
            }

            RefreshHudVisibility();
        }

        public void SetStageSelect(bool selecting)
        {
            stageSelecting = selecting;
            if (stageSelectPanel != null)
            {
                stageSelectPanel.SetActive(selecting);
            }

            if (selecting)
            {
                SetTitle(false);
            }

            if (menuPanel != null && selecting)
            {
                menuPanel.SetActive(false);
            }

            RefreshHudVisibility();
        }

        public void SetTitle(bool showing)
        {
            titleShowing = showing;
            if (titlePanel != null)
            {
                titlePanel.SetActive(showing);
            }

            if (!showing)
            {
                SetMulti(false);
                SetOption(false);
            }

            RefreshHudVisibility();
        }

        public void SetMulti(bool showing)
        {
            multiShowing = showing;
            if (multiPanel != null)
            {
                multiPanel.SetActive(showing);
            }

            if (titlePanel != null && titleShowing)
            {
                titlePanel.SetActive(!multiShowing && !optionShowing);
            }

            if (showing)
            {
                SetOption(false);
            }

            RefreshHudVisibility();
        }

        public void SetOption(bool showing)
        {
            optionShowing = showing;
            if (optionPanel != null)
            {
                optionPanel.SetActive(showing);
            }

            if (titlePanel != null && titleShowing)
            {
                titlePanel.SetActive(!multiShowing && !optionShowing);
            }

            if (showing)
            {
                SetMulti(false);
            }

            RefreshHudVisibility();
        }

        public void SetStageEditor(bool editing)
        {
            stageEditing = editing;
            if (stageEditorPanel != null)
            {
                stageEditorPanel.SetActive(editing);
            }

            if (menuPanel != null && editing)
            {
                menuPanel.SetActive(false);
            }

            RefreshHudVisibility();
        }

        public void SetCleared(bool cleared)
        {
            this.cleared = cleared;
            if (clearPanel != null)
            {
                clearPanel.SetActive(cleared);
            }

            RefreshHudVisibility();
            RefreshText();
        }

        private void RefreshHudVisibility()
        {
            if (gameplayHudPanel != null)
            {
                gameplayHudPanel.SetActive(!titleShowing && !multiShowing && !optionShowing && !stageSelecting && !stageEditing && !drawing && !cleared && (menuPanel == null || !menuPanel.activeSelf));
            }
        }

        private void RefreshText()
        {
            if (statusText == null)
            {
                return;
            }

            if (cleared)
            {
                statusText.text = LocalizationManager.T("status_clear");
            }
            else if (drawing)
            {
                statusText.text = LocalizationManager.T("status_draw");
            }
            else
            {
                statusText.text = LocalizationManager.T("status_play");
            }
        }
    }
}
