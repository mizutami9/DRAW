using System;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public static class DemoAccessPolicy
    {
        private static readonly string[] AllowedStageIds =
        {
            "1-1", "1-2", "6-3", "8-2", "9-3", "11-2", "11-3", "14-3"
        };

        public static bool IsDemoBuild
        {
            get
            {
#if NICO_DRAW_DEMO && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsStageAllowed(string stageId)
        {
            return !IsDemoBuild || IsIncludedStage(stageId);
        }

        public static bool IsIncludedStage(string stageId)
        {
            return Array.IndexOf(AllowedStageIds, stageId) >= 0;
        }

        public static string NormalizeStageId(string stageId)
        {
            return IsStageAllowed(stageId) ? stageId : AllowedStageIds[0];
        }

        public static string GetNextStageId(string stageId)
        {
            if (!IsDemoBuild) return null;
            int index = Array.IndexOf(AllowedStageIds, stageId);
            return index >= 0 && index + 1 < AllowedStageIds.Length
                ? AllowedStageIds[index + 1]
                : null;
        }

        public static void ApplyStageSelectRestrictions(GameObject stageSelectPanel, bool globallyLocked = false)
        {
            if (!IsDemoBuild || stageSelectPanel == null) return;

            StageSelectButtonCommand[] commands =
                stageSelectPanel.GetComponentsInChildren<StageSelectButtonCommand>(true);
            for (int i = 0; i < commands.Length; i++)
            {
                StageSelectButtonCommand command = commands[i];
                if (command == null) continue;
                if (command.StageId == "1-0")
                {
                    command.gameObject.SetActive(false);
                    continue;
                }

                Button button = command.GetComponent<Button>();
                if (button != null)
                {
                    bool stageAllowed = IsStageAllowed(command.StageId);
                    button.interactable = !globallyLocked && stageAllowed;
                    SetUnavailableBadge(command.transform, !stageAllowed);
                }
            }

            Transform[] transforms = stageSelectPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == "StageSelectEditModeButton")
                {
                    transforms[i].gameObject.SetActive(false);
                }
            }
        }

        private static void SetUnavailableBadge(Transform card, bool visible)
        {
            if (card == null) return;
            Transform existing = card.Find("DemoStageUnavailableBadge");
            GameObject badge = existing != null ? existing.gameObject : null;
            if (badge == null && visible)
            {
                badge = new GameObject(
                    "DemoStageUnavailableBadge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text),
                    typeof(Outline),
                    typeof(DemoStageLockedBadgePresenter));
                badge.transform.SetParent(card, false);
                RectTransform rect = badge.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.08f, 0f);
                rect.anchorMax = new Vector2(0.92f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(0f, 15f);

                Text source = FindBaseStageLabel(card, badge.transform);
                Text label = badge.GetComponent<Text>();
                label.font = source != null ? source.font : DoodleRuntimeAssets.HandwrittenFont;
                label.fontSize = 11;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(0.72f, 0.12f, 0.12f, 1f);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8;
                label.resizeTextMaxSize = 11;
                Outline outline = badge.GetComponent<Outline>();
                outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
                badge.GetComponent<DemoStageLockedBadgePresenter>().Configure(label);
            }
            if (badge != null) badge.SetActive(visible);

            Text stageLabel = FindBaseStageLabel(card, badge != null ? badge.transform : null);
            if (stageLabel != null && visible)
            {
                RectTransform labelRect = stageLabel.rectTransform;
                labelRect.anchoredPosition = new Vector2(0f, 7f);
                labelRect.sizeDelta = new Vector2(-12f, -17f);
                stageLabel.fontSize = 20;
                stageLabel.resizeTextMaxSize = 20;
            }
        }

        private static Text FindBaseStageLabel(Transform card, Transform badge)
        {
            Text[] labels = card.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                Text label = labels[i];
                if (label == null || badge != null && label.transform.IsChildOf(badge)) continue;
                return label;
            }
            return null;
        }
    }

    public sealed class DemoStageLockedBadgePresenter : MonoBehaviour
    {
        private Text label;

        public void Configure(Text target)
        {
            label = target;
            Refresh();
        }

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (label == null) label = GetComponent<Text>();
            if (label == null) return;
            label.text = LocalizationManager.T("demo_stage_unavailable");
            label.font = LocalizationManager.LoadCurrentFont(label.font);
        }
    }
}
