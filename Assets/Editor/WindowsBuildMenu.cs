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
            // The Steam release command switches the Standalone target to IL2CPP.
            // Explicitly restore Mono for the local multiplayer build; otherwise
            // Unity refuses to overwrite an existing Mono build directory and the
            // stale executable remains runnable after the failed build.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildWindows("Builds/NICO DRAW", false, false, false);
        }

        [MenuItem("PICO/Build Windows Demo EXE")]
        public static void BuildWindowsDemoExe()
        {
            // Keep the distributable demo quick to build and compatible with the
            // local multiplayer regression launcher. DemoAccessPolicy still
            // hard-locks editing and rejects stages outside the demo set.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildWindows("Builds/NICO DRAW Demo", false, true, false);
        }

        [MenuItem("PICO/Build Windows Demo Multiplayer Test")]
        public static void BuildWindowsDemoMultiplayerTest()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildWindows("Builds/NICO DRAW Demo Test", false, true, true);
        }

        [MenuItem("PICO/Build Windows AI Online Test")]
        public static void BuildWindowsAiOnlineTest()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            BuildWindows("Builds/NICO DRAW AI Test", false, false, true);
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
            BuildWindows("Builds/NICO DRAW Steam Playtest", true, true, false);
        }

        private static void BuildWindows(string outputDirectory, bool hardenedRelease, bool demoBuild, bool aiTestBuild)
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
                locationPathName = Path.Combine(outputDirectory, "NICO DRAW.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = aiTestBuild ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = GetBuildDefines(hardenedRelease, demoBuild, aiTestBuild)
            };

            BuildReport report;
            using (DemoStageBuildFilter.Enter(demoBuild))
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            if (report.summary.result == BuildResult.Succeeded)
            {
                if (hardenedRelease)
                {
                    RemoveIl2CppBackupArtifacts(outputDirectory);
                    RemoveUnusedArm64Plugins(outputDirectory);
                    ValidateHardenedBuild(outputDirectory);
                }
                string kind = hardenedRelease
                    ? "Hardened Steam release (IL2CPP + signed content)"
                    : demoBuild && aiTestBuild ? "Windows demo multiplayer test build"
                    : aiTestBuild ? "Windows AI online test build"
                    : demoBuild ? "Windows demo build" : "Windows test build";
                UnityEngine.Debug.Log(kind + " created: " + options.locationPathName);
            }
            else
            {
                UnityEngine.Debug.LogError("Windows build failed: " + report.summary.result);
            }
        }

        private static string[] GetBuildDefines(bool steamBuild, bool demoBuild, bool aiTestBuild)
        {
            var defines = new System.Collections.Generic.List<string>();
            if (steamBuild) defines.Add("NICO_DRAW_STEAM");
            if (demoBuild) defines.Add("NICO_DRAW_DEMO");
            if (aiTestBuild) defines.Add("NICO_DRAW_AI_TEST");
            return defines.Count > 0 ? defines.ToArray() : null;
        }

        [MenuItem("PICO/Build Windows EXE", true)]
        private static bool ValidateBuildWindowsExe()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling;
        }

        [MenuItem("PICO/Build Windows Demo EXE", true)]
        private static bool ValidateBuildWindowsDemoExe()
        {
            return ValidateBuildWindowsExe();
        }

        [MenuItem("PICO/Build Windows AI Online Test", true)]
        private static bool ValidateBuildWindowsAiOnlineTest()
        {
            return ValidateBuildWindowsExe();
        }

        [MenuItem("PICO/Build Windows Demo Multiplayer Test", true)]
        private static bool ValidateBuildWindowsDemoMultiplayerTest()
        {
            return ValidateBuildWindowsExe();
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
            string steamRuntime = Path.Combine(dataDirectory, "Plugins", "x86_64", "steam_api64.dll");
            if (!File.Exists(steamRuntime))
                throw new BuildFailedException("Steam release validation failed: steam_api64.dll is missing from NICO DRAW_Data/Plugins/x86_64.");
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

        private static void RemoveUnusedArm64Plugins(string outputDirectory)
        {
            string arm64Directory = Path.Combine(outputDirectory, "NICO DRAW_Data", "Plugins", "ARM64");
            if (!Directory.Exists(arm64Directory)) return;

            Directory.Delete(arm64Directory, true);
            UnityEngine.Debug.Log("Removed ARM64 plugins from the Windows x64 Steam distribution folder.");
        }
    }
}
