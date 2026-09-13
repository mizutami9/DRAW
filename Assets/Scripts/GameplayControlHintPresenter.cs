using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public sealed class GameplayControlHintPresenter : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private StageManager stageManager;

        private DrawManager.Species lastSpecies = (DrawManager.Species)(-1);
        private bool lastOnline;
        private float nextRefreshAt;

        public void Configure(Text targetLabel, StageManager manager)
        {
            label = targetLabel;
            stageManager = manager;
            Refresh(true);
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += HandleLanguageChanged;
            Refresh(true);
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= HandleLanguageChanged;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshAt) return;
            nextRefreshAt = Time.unscaledTime + 0.2f;
            Refresh(false);
        }

        private void HandleLanguageChanged()
        {
            Refresh(true);
        }

        private void Refresh(bool force)
        {
            if (label == null) return;
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();

            DrawManager.Species species = stageManager != null
                ? stageManager.ActiveLocalSpecies
                : DrawManager.Species.Human;
            bool online = stageManager != null && stageManager.IsOnlineStageActive;
            if (!force && species == lastSpecies && online == lastOnline) return;
            lastSpecies = species;
            lastOnline = online;

            string abilityKey = "ability_control_" + species.ToString().ToLowerInvariant();
            string text = LocalizationManager.T(abilityKey);
            if (!online)
            {
                text += "\nQ: " + LocalizationManager.T("gameplay_control_switch");
            }
            label.text = text;
            label.font = LocalizationManager.LoadCurrentFont(label.font);
            label.alignment = LocalizationManager.CurrentLanguageIsRightToLeft
                ? TextAnchor.MiddleRight
                : TextAnchor.MiddleLeft;
        }
    }
}
