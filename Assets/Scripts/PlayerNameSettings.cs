using System.Text;
using UnityEngine;

namespace DrawBody.Prototype
{
    public static class PlayerNameSettings
    {
        private const string NameKey = "online_player_display_name";
        private const string ConfiguredKey = "online_player_name_configured";
        public const int MaximumLength = 12;

        public static bool IsConfigured => PlayerPrefs.GetInt(ConfiguredKey, 0) != 0
            && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(NameKey, string.Empty));

        public static string CurrentName
        {
            get
            {
                string value = Sanitize(PlayerPrefs.GetString(NameKey, string.Empty));
                return string.IsNullOrEmpty(value) ? "PLAYER" : value;
            }
        }

        public static bool TrySet(string value)
        {
            string sanitized = Sanitize(value);
            if (string.IsNullOrEmpty(sanitized)) return false;
            PlayerPrefs.SetString(NameKey, sanitized);
            PlayerPrefs.SetInt(ConfiguredKey, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder result = new StringBuilder(MaximumLength);
            bool previousSpace = false;
            string trimmed = value.Trim();
            for (int i = 0; i < trimmed.Length && result.Length < MaximumLength; i++)
            {
                char character = trimmed[i];
                if (char.IsControl(character)) continue;
                bool space = char.IsWhiteSpace(character);
                if (space && previousSpace) continue;
                result.Append(space ? ' ' : character);
                previousSpace = space;
            }
            return result.ToString().Trim();
        }
    }
}
