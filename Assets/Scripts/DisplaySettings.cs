using UnityEngine;

namespace DrawBody.Prototype
{
    internal static class DisplaySettings
    {
        private const string InitializedKey = "display_settings_initialized";
        private const string FullScreenKey = "display_fullscreen";
        private const string WidthKey = "display_width";
        private const string HeightKey = "display_height";

        internal static bool IsFullScreen { get; private set; }
        internal static int Width { get; private set; }
        internal static int Height { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            ApplySavedSettings();
        }

        internal static void ApplySavedSettings()
        {
            int desktopWidth = Mathf.Max(1280, Screen.currentResolution.width);
            int desktopHeight = Mathf.Max(720, Screen.currentResolution.height);
            bool firstRun = PlayerPrefs.GetInt(InitializedKey, 0) == 0;
            IsFullScreen = firstRun || PlayerPrefs.GetInt(FullScreenKey, 1) != 0;
            Width = Mathf.Clamp(PlayerPrefs.GetInt(WidthKey, desktopWidth), 960, desktopWidth);
            Height = Mathf.Clamp(PlayerPrefs.GetInt(HeightKey, desktopHeight), 540, desktopHeight);
            Apply();
        }

        internal static void ToggleScreenMode()
        {
            IsFullScreen = !IsFullScreen;
            Apply();
        }

        internal static void CycleResolution()
        {
            Vector2Int[] choices = GetResolutionChoices();
            int current = FindClosestChoice(choices, Width, Height);
            Vector2Int next = choices[(current + 1) % choices.Length];
            Width = next.x;
            Height = next.y;
            Apply();
        }

        internal static void Save()
        {
            PlayerPrefs.SetInt(InitializedKey, 1);
            PlayerPrefs.SetInt(FullScreenKey, IsFullScreen ? 1 : 0);
            PlayerPrefs.SetInt(WidthKey, Width);
            PlayerPrefs.SetInt(HeightKey, Height);
            PlayerPrefs.Save();
        }

        private static void Apply()
        {
            FullScreenMode mode = IsFullScreen
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.Windowed;
            Screen.SetResolution(Width, Height, mode);
        }

        private static Vector2Int[] GetResolutionChoices()
        {
            int desktopWidth = Mathf.Max(1280, Screen.currentResolution.width);
            int desktopHeight = Mathf.Max(720, Screen.currentResolution.height);
            var choices = new System.Collections.Generic.List<Vector2Int>
            {
                new Vector2Int(Mathf.Min(1280, desktopWidth), Mathf.Min(720, desktopHeight)),
                new Vector2Int(Mathf.Min(1600, desktopWidth), Mathf.Min(900, desktopHeight)),
                new Vector2Int(Mathf.Min(1920, desktopWidth), Mathf.Min(1080, desktopHeight))
            };
            Vector2Int desktop = new Vector2Int(desktopWidth, desktopHeight);
            if (!choices.Contains(desktop)) choices.Add(desktop);
            for (int i = choices.Count - 1; i > 0; i--)
                if (choices[i] == choices[i - 1]) choices.RemoveAt(i);
            return choices.ToArray();
        }

        private static int FindClosestChoice(Vector2Int[] choices, int width, int height)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < choices.Length; i++)
            {
                int distance = Mathf.Abs(choices[i].x - width) + Mathf.Abs(choices[i].y - height);
                if (distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }
    }
}
