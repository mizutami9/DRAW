using System.Collections.Generic;
using System.IO;
using DrawBody.Prototype;
using UnityEditor;
using UnityEngine;

public static class StageDataValidationMenu
{
    [MenuItem("Tools/PICO/Validate All Stage Data")]
    public static void ValidateAllStageData()
    {
        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Stages" });
        int errors = 0;
        int warnings = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null || !path.EndsWith(".json")) continue;
            StageData stage = JsonUtility.FromJson<StageData>(asset.text);
            string expectedId = Path.GetFileNameWithoutExtension(path);
            List<StageValidationIssue> issues = StageDataValidator.Validate(stage, expectedId);
            for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
            {
                StageValidationIssue issue = issues[issueIndex];
                string message = $"{expectedId}: {issue}";
                if (issue.Severity == StageValidationSeverity.Error)
                {
                    errors++;
                    Debug.LogError(message, asset);
                }
                else
                {
                    warnings++;
                    Debug.LogWarning(message, asset);
                }
            }
        }

        if (errors == 0 && warnings == 0)
            Debug.Log($"Stage validation passed for {guids.Length} stage assets.");
        else
            Debug.LogWarning($"Stage validation finished: {errors} error(s), {warnings} warning(s).");
    }
}
