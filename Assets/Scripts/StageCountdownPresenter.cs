using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>Shared 3, 2, 1, START presentation for timed stage introductions.</summary>
    internal sealed class StageCountdownPresenter
    {
        private readonly UIManager uiManager;
        private string lastValue;

        internal StageCountdownPresenter(UIManager ui)
        {
            uiManager = ui;
        }

        internal void Show(float remaining)
        {
            string value = GetText(remaining);
            uiManager?.SetChallengeCountdown(true, value);
            if (value == lastValue) return;
            lastValue = value;
            GameSfx.Play(value == LocalizationManager.T("survival_start")
                ? SfxId.StageCountdownGo
                : SfxId.StageCountdownTick);
        }

        internal void Hide()
        {
            uiManager?.SetChallengeCountdown(false, string.Empty);
            lastValue = null;
        }

        internal static int GetNumber(float remaining)
        {
            return Mathf.Clamp(Mathf.CeilToInt(remaining - 1f), 1, 3);
        }

        private static string GetText(float remaining)
        {
            if (remaining > 3f) return "3";
            if (remaining > 2f) return "2";
            if (remaining > 1f) return "1";
            return LocalizationManager.T("survival_start");
        }
    }
}
