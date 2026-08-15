using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace DrawBody.EditorTools
{
    public static class WindowsBuildMenu
    {
        [MenuItem("PICO/Build Windows EXE")]
        public static void BuildWindowsExe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogWarning("Windows build is unavailable during Play Mode. Stop Play Mode and run it again.");
                return;
            }

            string scenePath = "Assets/Scenes/GameScene.unity";
            if (!File.Exists(scenePath))
            {
                Phase0SceneBuilder.BuildScene();
            }

            string outputDirectory = "Builds/DrawBodyOnline";
            Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outputDirectory, "DrawBody.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log("Windows build created: " + options.locationPathName);
            }
            else
            {
                UnityEngine.Debug.LogError("Windows build failed: " + report.summary.result);
            }
        }

        [MenuItem("PICO/Build Windows EXE", true)]
        private static bool ValidateBuildWindowsExe()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling;
        }
    }
}
