using System.Collections.Generic;
using DrawBody.Prototype;
using UnityEditor;
using UnityEngine;

public static class LocalizationValidationMenu
{
    [MenuItem("Tools/PICO/Validate Localization Tables")]
    private static void ValidateLocalizationTables()
    {
        int totalMissing = 0;
        IReadOnlyList<LocalizationManager.LanguageDefinition> languages = LocalizationManager.SupportedLanguages;
        for (int i = 0; i < languages.Count; i++)
        {
            LocalizationManager.LanguageDefinition language = languages[i];
            IReadOnlyList<string> missing = LocalizationManager.GetMissingTranslationKeys(language.code);
            totalMissing += missing.Count;
            if (missing.Count > 0)
            {
                Debug.LogWarning($"Localization '{language.code}' is missing {missing.Count} keys:\n{string.Join("\n", missing)}");
            }
        }

        if (totalMissing == 0)
        {
            Debug.Log($"Localization validation passed for {languages.Count} languages.");
        }
        else
        {
            Debug.LogWarning($"Localization validation found {totalMissing} missing entries across {languages.Count} languages.");
        }
    }
}
