using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DrawBody.EditorTools
{
    public static class WindowsBuildMenu
    {
        [MenuItem("PICO/Build Windows EXE")]
        public static void BuildWindowsExe()
        {
            BuildWindows("Builds/DrawBodyOnline", false);
        }

        [MenuItem("PICO/Build Windows Steam Release")]
        public static void BuildWindowsSteamRelease()
        {
            if (!IsWindowsIl2CppInstalled())
            {
                UnityEngine.Debug.LogError(
                    "Steam release build stopped: install 'Windows Build Support (IL2CPP)' for Unity 6000.1.2f1 in Unity Hub. "
                    + "The normal PICO/Build Windows EXE menu remains available for local testing.");
                return;
            }

            string configuredVersion = System.Environment.GetEnvironmentVariable("PICO_BUILD_VERSION");
            PlayerSettings.bundleVersion = !string.IsNullOrWhiteSpace(configuredVersion)
                ? configuredVersion.Trim()
                : "0.1.0-playtest.1";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);
            WarnExternalReleasePrerequisites();
            BuildWindows("Builds/NICODRAWSteamPlaytest", true);
        }

        private static void BuildWindows(string outputDirectory, bool hardenedRelease)
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

            Directory.CreateDirectory(outputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outputDirectory, hardenedRelease ? "NICO DRAW.exe" : "DrawBody.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = hardenedRelease ? new[] { "NICO_DRAW_DEMO" } : null
            };

            BuildReport report;
            using (DemoStageBuildFilter.Enter(hardenedRelease))
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            if (report.summary.result == BuildResult.Succeeded)
            {
                if (hardenedRelease)
                {
                    RemoveIl2CppBackupArtifacts(outputDirectory);
                    ValidateHardenedBuild(outputDirectory);
                }
                string kind = hardenedRelease ? "Hardened Steam release (IL2CPP + signed content)" : "Windows test build";
                UnityEngine.Debug.Log(kind + " created: " + options.locationPathName);
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

        [MenuItem("PICO/Build Windows Steam Release", true)]
        private static bool ValidateBuildWindowsSteamRelease()
        {
            return ValidateBuildWindowsExe();
        }

        private static bool IsWindowsIl2CppInstalled()
        {
            string editorDirectory = Path.GetDirectoryName(EditorApplication.applicationPath);
            string variations = Path.Combine(editorDirectory, "Data", "PlaybackEngines",
                "WindowsStandaloneSupport", "Variations");
            return Directory.Exists(variations)
                && Directory.GetDirectories(variations, "*il2cpp*", SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static void WarnExternalReleasePrerequisites()
        {
            const string toolsConfigPath = "etc/config/eos_plugin_tools_config.json";
            if (!File.Exists(toolsConfigPath)
                || !File.ReadAllText(toolsConfigPath).Contains("\"useEAC\": true"))
            {
                UnityEngine.Debug.LogWarning(
                    "EOS Easy Anti-Cheat is not configured. This does not block local gameplay, but configure EAC before a protected Steam release if anti-cheat is required.");
            }

            if (!File.Exists("Assets/StreamingAssets/EOS/eos_steam_config.json"))
            {
                UnityEngine.Debug.LogWarning(
                    "Steam integrated-platform configuration is not present. The current Device ID login does not prove Steam ownership; complete Steam ticket authentication before paid release.");
            }
        }

        private static void ValidateHardenedBuild(string outputDirectory)
        {
            string executable = Path.Combine(outputDirectory, "NICO DRAW.exe");
            string dataDirectory = Path.Combine(outputDirectory, "NICO DRAW_Data");
            if (!File.Exists(executable) || !File.Exists(Path.Combine(outputDirectory, "GameAssembly.dll")))
                throw new BuildFailedException("Steam release validation failed: the IL2CPP executable is incomplete.");
            if (File.Exists(Path.Combine(dataDirectory, "Managed", "Assembly-CSharp.dll")))
                throw new BuildFailedException("Steam release validation failed: a replaceable Mono gameplay assembly was found.");
            if (File.Exists(Path.Combine(outputDirectory, "steam_appid.txt")))
                throw new BuildFailedException("Steam release validation failed: steam_appid.txt must not be uploaded to the depot.");
        }

        private static void RemoveIl2CppBackupArtifacts(string outputDirectory)
        {
            string executableName = "NICO DRAW";
            string backupDirectory = Path.Combine(outputDirectory,
                executableName + "_BackUpThisFolder_ButDontShipItWithYourGame");
            if (!Directory.Exists(backupDirectory)) return;

            Directory.Delete(backupDirectory, true);
            UnityEngine.Debug.Log("Removed Unity IL2CPP backup artifacts from the Steam distribution folder.");
        }
    }
}
