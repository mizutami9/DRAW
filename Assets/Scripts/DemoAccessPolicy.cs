using System;
using UnityEngine;
using UnityEngine.UI;

namespace DrawBody.Prototype
{
    public static class DemoAccessPolicy
    {
        private static readonly string[] AllowedStageIds =
        {
            "1-1", "1-2", "1-3", "6-3", "8-2", "9-3", "11-2", "14-3"
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
            if (!IsDemoBuild) return true;
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
                    button.interactable = !globallyLocked && IsStageAllowed(command.StageId);
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
    }
}
