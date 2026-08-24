using UnityEngine;

namespace DrawBody.Prototype
{
    [DisallowMultipleComponent]
    public sealed class StageChallengeClock : MonoBehaviour
    {
        private TextMesh digits;
        private TextMesh shadow;
        private TextMesh progress;
        private SpriteRenderer statusLed;
        private StageManager stageManager;
        private RuntimeStageEditor stageEditor;
        private float previewSeconds = 60f;

        public void Configure(
            TextMesh targetDigits,
            TextMesh targetShadow,
            TextMesh targetProgress,
            SpriteRenderer targetStatusLed,
            float defaultSeconds)
        {
            digits = targetDigits;
            shadow = targetShadow;
            progress = targetProgress;
            statusLed = targetStatusLed;
            previewSeconds = Mathf.Max(0f, defaultSeconds);
        }

        private void Start()
        {
            stageManager = Object.FindFirstObjectByType<StageManager>();
            stageEditor = Object.FindFirstObjectByType<RuntimeStageEditor>();
        }

        private void Update()
        {
            bool editing = stageEditor != null && stageEditor.IsEditing;
            float seconds = editing
                ? stageEditor.StageTimeLimitSeconds
                : stageManager != null ? stageManager.ChallengeRemainingSeconds : previewSeconds;
            bool timeUp = !editing && stageManager != null && stageManager.ChallengeTimeUp;
            bool starting = !editing && stageManager != null && stageManager.ChallengeStarting;
            bool activeRule = editing
                ? stageEditor.IsTimedCollectionRule
                : stageManager != null && stageManager.IsTimedCollectionChallenge;
            bool urgent = activeRule && !starting && seconds <= 10f;
            bool showColon = editing || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            string value = starting
                ? stageManager.ChallengeStartCountdownText
                : timeUp
                    ? "TIME UP"
                    : activeRule ? FormatTime(seconds, showColon) : "--:--.-";
            if (digits != null)
            {
                digits.text = value;
                digits.color = starting
                    ? new Color(1f, 0.78f, 0.16f, 1f)
                    : timeUp && Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 0
                    ? new Color(1f, 0.16f, 0.1f, 1f)
                    : urgent
                        ? new Color(1f, 0.38f, 0.12f, 1f)
                        : new Color(0.2f, 1f, 0.68f, 1f);
            }
            if (shadow != null)
            {
                shadow.text = value;
            }
            if (statusLed != null)
            {
                statusLed.color = starting
                    ? new Color(1f, 0.72f, 0.08f, 1f)
                    : timeUp
                    ? new Color(1f, 0.12f, 0.08f, 1f)
                    : urgent
                        ? new Color(1f, 0.62f, 0.08f, 1f)
                        : activeRule
                            ? new Color(0.18f, 0.9f, 0.38f, 1f)
                            : new Color(0.35f, 0.38f, 0.4f, 1f);
            }
            if (progress != null)
            {
                StageObjectType target = editing && stageEditor != null
                    ? stageEditor.StageCollectionTarget
                    : stageManager != null
                        ? stageManager.ChallengeCollectionTarget
                        : StageObjectType.CollectibleFish;
                int caught = editing || stageManager == null ? 0 : stageManager.ChallengeCollectedCount;
                int total = editing && stageEditor != null
                    ? stageEditor.StagePlacedCollectionTargetCount
                    : stageManager != null
                        ? stageManager.ChallengeRequiredCollectionCount
                        : 0;
                progress.text = $"{GetTargetCode(target)}  {caught} / {total}";
                progress.color = total > 0 && caught >= total
                    ? new Color(0.35f, 1f, 0.48f, 1f)
                    : new Color(0.72f, 0.9f, 1f, 1f);
            }
        }

        private static string GetTargetCode(StageObjectType type)
        {
            switch (type)
            {
                case StageObjectType.CollectibleCoin:
                    return "COIN";
                case StageObjectType.CollectibleStar:
                    return "STAR";
                default:
                    return "FISH";
            }
        }

        private static string FormatTime(float totalSeconds, bool showColon)
        {
            totalSeconds = Mathf.Max(0f, totalSeconds);
            int minutes = Mathf.FloorToInt(totalSeconds / 60f);
            float seconds = totalSeconds - minutes * 60f;
            return $"{minutes:00}{(showColon ? ":" : " ")}{seconds:00.0}";
        }
    }
}
