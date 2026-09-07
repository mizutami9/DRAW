using System;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class StageProgressStore
    {
        private const string ClearKeyPrefix = "stage_progress_clear_v1_";
        private const int MaximumWorld = 15;
        private const int VariantsPerWorld = 3;

        public static event Action Changed;

        public static bool IsCleared(string stageId)
        {
            return IsRegularStageId(stageId) && PlayerPrefs.GetInt(ClearKeyPrefix + stageId, 0) != 0;
        }

        public static void MarkCleared(string stageId)
        {
            if (!IsRegularStageId(stageId) || IsCleared(stageId)) return;
            PlayerPrefs.SetInt(ClearKeyPrefix + stageId, 1);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetClearRecords()
        {
            for (int world = 1; world <= MaximumWorld; world++)
            {
                for (int variant = 1; variant <= VariantsPerWorld; variant++)
                {
                    PlayerPrefs.DeleteKey(ClearKeyPrefix + world + "-" + variant);
                }
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static bool IsRegularStageId(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return false;
            string[] parts = stageId.Split('-');
            return parts.Length == 2
                && int.TryParse(parts[0], out int world)
                && int.TryParse(parts[1], out int variant)
                && world >= 1 && world <= MaximumWorld
                && variant >= 1 && variant <= VariantsPerWorld;
        }
    }
}
