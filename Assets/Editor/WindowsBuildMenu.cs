using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DrawBody.EditorTools
{
    public static class WindowsBuildMenu
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";
        private const string ContentManifestPath = "Assets/Resources/Security/content_manifest.json";
        private const string ContentManifestBackupPath =
            "Library/NicoDrawBuildBackups/content_manifest.json";
        private const string ProductExecutableName = "NICO DRAW.exe";

        [Flags]
        private enum BuildFeature
        {
            None = 0,
            Demo = 1 << 0,
            AiTest = 1 << 1,
            Steam = 1 << 2,
            Playtest = 1 << 3,
            Hardened = 1 << 4
        }

        private static readonly BuildConfiguration LocalBuild = new BuildConfiguration(
            "Builds/NICO DRAW",
            "Windows test build",
            ScriptingImplementation.Mono2x,
            BuildFeature.None);

        private static readonly BuildConfiguration PublicDemoBuild = new BuildConfiguration(
            "Builds/NICO DRAW Demo",
            "Hardened public demo (IL2CPP + filtered content)",
            ScriptingImplementation.IL2CPP,
            BuildFeature.Demo | BuildFeature.Hardened);

        private static readonly BuildConfiguration DemoMultiplayerTestBuild = new BuildConfiguration(
            "Builds/NICO DRAW Demo Test",
            "Windows demo multiplayer test build",
            ScriptingImplementation.Mono2x,
            BuildFeature.Demo | BuildFeature.AiTest);

        private static readonly BuildConfiguration AiOnlineTestBuild = new BuildConfiguration(
            "Builds/NICO DRAW AI Test",
            "Windows AI online test build",
            ScriptingImplementation.Mono2x,
            BuildFeature.AiTest);

        private static readonly BuildConfiguration SteamDemoBuild = new BuildConfiguration(
            "Builds/NICO DRAW Steam Demo",
            "Hardened Steam demo (IL2CPP + filtered content)",
            ScriptingImplementation.IL2CPP,
            BuildFeature.Demo | BuildFeature.Steam | BuildFeature.Hardened,
            "0.1.0-demo.4",
            SteamDistributionIds.DemoAppId,
            SteamDistributionIds.DemoDepotId,
            "steam-demo");

        private static readonly BuildConfiguration SteamPlaytestBuild = new BuildConfiguration(
            "Builds/NICO DRAW Steam Playtest",
            "Hardened Steam Playtest (IL2CPP + filtered content)",
            ScriptingImplementation.IL2CPP,
            BuildFeature.Demo | BuildFeature.Steam | BuildFeature.Playtest | BuildFeature.Hardened,
            "0.1.0-demo.1",
            SteamDistributionIds.PlaytestAppId,
            SteamDistributionIds.PlaytestDepotId,
            "steam-playtest");

        [InitializeOnLoadMethod]
        private static void ScheduleInterruptedManifestRecovery()
        {
            EditorApplication.delayCall += RecoverInterruptedManifestSnapshot;
        }

        [MenuItem("PICO/Build Windows EXE")]
        public static void BuildWindowsExe()
        {
            RunBuild(LocalBuild);
        }

        [MenuItem("PICO/Build Windows Demo EXE")]
        public static void BuildWindowsDemoExe()
        {
            RunBuild(PublicDemoBuild);
        }

        [MenuItem("PICO/Build Windows Demo Multiplayer Test")]
        public static void BuildWindowsDemoMultiplayerTest()
        {
            RunBuild(DemoMultiplayerTestBuild);
        }

        [MenuItem("PICO/Build Windows AI Online Test")]
        public static void BuildWindowsAiOnlineTest()
        {
            RunBuild(AiOnlineTestBuild);
        }

        [MenuItem("PICO/Build Windows Steam Demo")]
        public static void BuildWindowsSteamDemo()
        {
            RunBuild(SteamDemoBuild);
        }

        [MenuItem("PICO/Build Windows Steam Playtest")]
        public static void BuildWindowsSteamPlaytest()
        {
            RunBuild(SteamPlaytestBuild);
        }

        private static void RunBuild(BuildConfiguration configuration)
        {
            if (!IsBuildMenuAvailable())
            {
                UnityEngine.Debug.LogWarning(
                    "Windows build is unavailable while Unity is compiling or in Play Mode. Try again when the Editor is idle.");
                return;
            }

            if (configuration.RequiresIl2Cpp && !IsWindowsIl2CppInstalled())
            {
                UnityEngine.Debug.LogError(
                    configuration.SuccessLabel
                    + " stopped: install 'Windows Build Support (IL2CPP)' for Unity 6000.1.2f1 in Unity Hub.");
                return;
            }

            // Keep the tracked manifest in its full-content form after demo filtering.
            // The outer scope disposes after PlayerSettingsScope, so regeneration uses
            // the project's normal bundle version instead of a temporary Steam version.
            using (new SignedManifestScope(
                ContentManifestPath,
                configuration.Has(BuildFeature.Demo)))
            using (new PlayerSettingsScope())
            {
                ApplyPlayerSettings(configuration);
                if (configuration.Has(BuildFeature.Steam)) WarnExternalReleasePrerequisites();
                BuildWindows(configuration);
            }
        }

        private static void ApplyPlayerSettings(BuildConfiguration configuration)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, configuration.ScriptingBackend);
            if (configuration.Has(BuildFeature.Hardened))
            {
                PlayerSettings.SetManagedStrippingLevel(
                    NamedBuildTarget.Standalone,
                    ManagedStrippingLevel.Low);
            }

            if (!string.IsNullOrEmpty(configuration.DefaultVersion))
            {
                string configuredVersion = Environment.GetEnvironmentVariable("PICO_BUILD_VERSION");
                PlayerSettings.bundleVersion = !string.IsNullOrWhiteSpace(configuredVersion)
                    ? configuredVersion.Trim()
                    : configuration.DefaultVersion;
            }
        }

        private static void BuildWindows(BuildConfiguration configuration)
        {
            string sceneFilePath = Path.Combine(GetProjectRoot(), ScenePath);
            if (!File.Exists(sceneFilePath)) Phase0SceneBuilder.BuildScene();
            string outputDirectory = PrepareOutputDirectory(configuration.OutputDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(outputDirectory, ProductExecutableName),
                target = BuildTarget.StandaloneWindows64,
                // Release builds must never reuse a copied/stale incremental player cache.
                // A mismatched cache can produce a player where serialized scene behaviours
                // resolve as "missing script", leaving the scene's editor-time UI state visible.
                options = (configuration.Has(BuildFeature.AiTest) ? BuildOptions.Development : BuildOptions.None)
                    | (configuration.Has(BuildFeature.Hardened) ? BuildOptions.CleanBuildCache : BuildOptions.None),
                extraScriptingDefines = GetBuildDefines(configuration.Features)
            };

            BuildReport report;
            using (DemoStageBuildFilter.Enter(configuration.Has(BuildFeature.Demo)))
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            if (report.summary.result == BuildResult.Succeeded)
            {
                if (configuration.Has(BuildFeature.Hardened))
                {
                    RemoveIl2CppBackupArtifacts(outputDirectory);
                    RemoveUnusedArm64Plugins(outputDirectory);
                    RemoveDebugSymbols(outputDirectory);
                }
                if (configuration.Has(BuildFeature.Hardened))
                    ValidateHardenedBuild(outputDirectory, configuration.Has(BuildFeature.Steam));
                if (configuration.Has(BuildFeature.Demo)) ValidateDemoContentExclusions(outputDirectory);
                // This file is the upload validator's success marker. Create it
                // only after every content and hardening check has passed.
                WriteDistributionManifest(outputDirectory, configuration);
                UnityEngine.Debug.Log(configuration.SuccessLabel + " created: " + options.locationPathName);
            }
            else
            {
                UnityEngine.Debug.LogError("Windows build failed: " + report.summary.result);
            }
        }

        private static string[] GetBuildDefines(BuildFeature features)
        {
            var defines = new List<string>();
            if ((features & BuildFeature.Steam) != 0) defines.Add("NICO_DRAW_STEAM");
            if ((features & BuildFeature.Demo) != 0) defines.Add("NICO_DRAW_DEMO");
            if ((features & BuildFeature.AiTest) != 0) defines.Add("NICO_DRAW_AI_TEST");
            if ((features & BuildFeature.Playtest) != 0) defines.Add("NICO_DRAW_PLAYTEST");
            return defines.Count > 0 ? defines.ToArray() : null;
        }

        [MenuItem("PICO/Build Windows EXE", true)]
        private static bool ValidateBuildWindowsExe()
        {
            return IsBuildMenuAvailable();
        }

        [MenuItem("PICO/Build Windows Demo EXE", true)]
        private static bool ValidateBuildWindowsDemoExe()
        {
            return IsBuildMenuAvailable();
        }

        [MenuItem("PICO/Build Windows AI Online Test", true)]
        private static bool ValidateBuildWindowsAiOnlineTest()
        {
            return IsBuildMenuAvailable();
        }

        [MenuItem("PICO/Build Windows Demo Multiplayer Test", true)]
        private static bool ValidateBuildWindowsDemoMultiplayerTest()
        {
            return IsBuildMenuAvailable();
        }

        [MenuItem("PICO/Build Windows Steam Demo", true)]
        private static bool ValidateBuildWindowsSteamDemo()
        {
            return IsBuildMenuAvailable();
        }

        [MenuItem("PICO/Build Windows Steam Playtest", true)]
        private static bool ValidateBuildWindowsSteamPlaytest()
        {
            return IsBuildMenuAvailable();
        }

        private static bool IsBuildMenuAvailable()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling;
        }

        private static void RecoverInterruptedManifestSnapshot()
        {
            if (!File.Exists(ContentManifestBackupPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ContentManifestPath));
            File.Copy(ContentManifestBackupPath, ContentManifestPath, true);
            AssetDatabase.ImportAsset(ContentManifestPath, ImportAssetOptions.ForceSynchronousImport);
            File.Delete(ContentManifestBackupPath);
            UnityEngine.Debug.Log("Recovered the signed content manifest left by an interrupted build.");
        }

        private static string PrepareOutputDirectory(string configuredPath)
        {
            string projectRoot = GetProjectRoot();
            string buildsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Builds"));
            string outputDirectory = Path.GetFullPath(Path.Combine(projectRoot, configuredPath));
            string guardedRoot = buildsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!outputDirectory.StartsWith(guardedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException(
                    "Refusing to clean a Windows build output outside the project Builds directory: "
                    + outputDirectory);
            }

            Directory.CreateDirectory(outputDirectory);

            // Unity's CleanBuildCache clears its incremental cache, not the final
            // distribution folder. Remove prior players so an obsolete DLL, JSON,
            // or secret file can never be carried into a later Steam depot.
            string[] directories = Directory.GetDirectories(outputDirectory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < directories.Length; i++) Directory.Delete(directories[i], true);

            string[] files = Directory.GetFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                // Command-line Unity may be writing its active -logFile here.
                // Steam upload rules exclude logs, so retaining it is safe.
                if (string.Equals(Path.GetExtension(files[i]), ".log", StringComparison.OrdinalIgnoreCase)) continue;
                File.Delete(files[i]);
            }

            return outputDirectory;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        }

        private static void WriteDistributionManifest(
            string outputDirectory,
            BuildConfiguration configuration)
        {
            DistributionManifest manifest = new DistributionManifest
            {
                productName = PlayerSettings.productName,
                productVersion = PlayerSettings.bundleVersion,
                flavor = configuration.Flavor,
                scriptingBackend = configuration.ScriptingBackend.ToString(),
                steamAppId = configuration.SteamAppId.ToString(),
                steamDepotId = configuration.SteamDepotId.ToString(),
                scriptingDefines = GetBuildDefines(configuration.Features) ?? Array.Empty<string>()
            };
            string manifestPath = Path.Combine(outputDirectory, "nico_draw_build.json");
            string temporaryPath = manifestPath + ".tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    UnityEngine.JsonUtility.ToJson(manifest, true) + Environment.NewLine,
                    new UTF8Encoding(false));
                File.Move(temporaryPath, manifestPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
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

        private static void ValidateHardenedBuild(string outputDirectory, bool requireSteamRuntime)
        {
            string executable = Path.Combine(outputDirectory, "NICO DRAW.exe");
            string dataDirectory = Path.Combine(outputDirectory, "NICO DRAW_Data");
            if (!File.Exists(executable) || !File.Exists(Path.Combine(outputDirectory, "GameAssembly.dll")))
                throw new BuildFailedException("Release validation failed: the IL2CPP executable is incomplete.");
            if (File.Exists(Path.Combine(dataDirectory, "Managed", "Assembly-CSharp.dll")))
                throw new BuildFailedException("Release validation failed: a replaceable Mono gameplay assembly was found.");
            if (File.Exists(Path.Combine(outputDirectory, "steam_appid.txt")))
                throw new BuildFailedException("Release validation failed: steam_appid.txt must not be distributed.");
            if (Directory.GetFiles(outputDirectory, "*.pdb", SearchOption.AllDirectories).Length > 0
                || Directory.GetFiles(outputDirectory, "*.mdb", SearchOption.AllDirectories).Length > 0)
                throw new BuildFailedException("Release validation failed: debug symbols remain in the distribution folder.");
            if (requireSteamRuntime)
            {
                string steamRuntime = Path.Combine(dataDirectory, "Plugins", "x86_64", "steam_api64.dll");
                if (!File.Exists(steamRuntime))
                    throw new BuildFailedException("Steam release validation failed: steam_api64.dll is missing from NICO DRAW_Data/Plugins/x86_64.");
            }
        }

        private static void ValidateDemoContentExclusions(string outputDirectory)
        {
            string dataDirectory = Path.Combine(outputDirectory, "NICO DRAW_Data");
            string resourcesPath = Path.Combine(dataDirectory, "resources.assets");
            if (!File.Exists(resourcesPath))
                throw new BuildFailedException("Demo validation failed: resources.assets is missing.");

            byte[] resources = File.ReadAllBytes(resourcesPath);
            string stageDirectory = Path.Combine("Assets", "Resources", "Stages");
            string[] stageFiles = Directory.GetFiles(stageDirectory, "*.json", SearchOption.TopDirectoryOnly);
            List<string> leakedStages = new List<string>();
            for (int i = 0; i < stageFiles.Length; i++)
            {
                string stageId = Path.GetFileNameWithoutExtension(stageFiles[i]);
                if (DemoStageBuildFilter.IsStageAllowedForDemo(stageId)) continue;
                string resourcePath = "Stages/" + stageId;
                if (ContainsBytes(resources, Encoding.UTF8.GetBytes(resourcePath))
                    || ContainsBytes(resources, Encoding.Unicode.GetBytes(resourcePath)))
                {
                    leakedStages.Add(stageId);
                }
            }

            if (leakedStages.Count > 0)
                throw new BuildFailedException(
                    "Demo validation failed: excluded stage data was found in resources.assets: "
                    + string.Join(", ", leakedStages));

            string scenePath = Path.Combine(dataDirectory, "level0");
            if (!File.Exists(scenePath))
                throw new BuildFailedException("Demo validation failed: built scene data is missing.");
            byte[] scene = File.ReadAllBytes(scenePath);
            string[] editorObjectNames = { "RuntimeStageEditorPanel", "RuntimeStageEditorRoot" };
            for (int i = 0; i < editorObjectNames.Length; i++)
            {
                string objectName = editorObjectNames[i];
                if (ContainsBytes(scene, Encoding.UTF8.GetBytes(objectName))
                    || ContainsBytes(scene, Encoding.Unicode.GetBytes(objectName)))
                {
                    throw new BuildFailedException(
                        "Demo validation failed: stage editor object remains in the built scene: "
                        + objectName);
                }
            }
        }

        private static bool ContainsBytes(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return false;
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        private static void RemoveIl2CppBackupArtifacts(string outputDirectory)
        {
            string executableName = "NICO DRAW";
            string backupDirectory = Path.Combine(outputDirectory,
                executableName + "_BackUpThisFolder_ButDontShipItWithYourGame");
            if (!Directory.Exists(backupDirectory)) return;

            Directory.Delete(backupDirectory, true);
            UnityEngine.Debug.Log("Removed Unity IL2CPP backup artifacts from the release distribution folder.");
        }

        private static void RemoveDebugSymbols(string outputDirectory)
        {
            string[] extensions = { "*.pdb", "*.mdb" };
            for (int extension = 0; extension < extensions.Length; extension++)
            {
                string[] files = Directory.GetFiles(outputDirectory, extensions[extension], SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++) File.Delete(files[i]);
            }

            string burstDebugDirectory = Path.Combine(outputDirectory, "NICO DRAW_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(burstDebugDirectory)) Directory.Delete(burstDebugDirectory, true);
        }

        private static void RemoveUnusedArm64Plugins(string outputDirectory)
        {
            string arm64Directory = Path.Combine(outputDirectory, "NICO DRAW_Data", "Plugins", "ARM64");
            if (!Directory.Exists(arm64Directory)) return;

            Directory.Delete(arm64Directory, true);
            UnityEngine.Debug.Log("Removed ARM64 plugins from the Windows x64 Steam distribution folder.");
        }

        private sealed class BuildConfiguration
        {
            public readonly string OutputDirectory;
            public readonly string SuccessLabel;
            public readonly ScriptingImplementation ScriptingBackend;
            public readonly BuildFeature Features;
            public readonly string DefaultVersion;
            public readonly uint SteamAppId;
            public readonly uint SteamDepotId;
            public readonly string Flavor;

            public BuildConfiguration(
                string outputDirectory,
                string successLabel,
                ScriptingImplementation scriptingBackend,
                BuildFeature features,
                string defaultVersion = null,
                uint steamAppId = 0,
                uint steamDepotId = 0,
                string flavor = null)
            {
                OutputDirectory = outputDirectory;
                SuccessLabel = successLabel;
                ScriptingBackend = scriptingBackend;
                Features = features;
                DefaultVersion = defaultVersion;
                SteamAppId = steamAppId;
                SteamDepotId = steamDepotId;
                Flavor = string.IsNullOrEmpty(flavor) ? "standalone" : flavor;
            }

            public bool RequiresIl2Cpp => ScriptingBackend == ScriptingImplementation.IL2CPP;

            public bool Has(BuildFeature feature)
            {
                return (Features & feature) != 0;
            }
        }

        [Serializable]
        private sealed class DistributionManifest
        {
            public string productName;
            public string productVersion;
            public string flavor;
            public string scriptingBackend;
            public string steamAppId;
            public string steamDepotId;
            public string[] scriptingDefines;
        }

        private sealed class PlayerSettingsScope : IDisposable
        {
            private readonly string bundleVersion = PlayerSettings.bundleVersion;
            private readonly ScriptingImplementation scriptingBackend =
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            private readonly ManagedStrippingLevel strippingLevel =
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                PlayerSettings.bundleVersion = bundleVersion;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, scriptingBackend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, strippingLevel);
            }
        }

        private sealed class SignedManifestScope : IDisposable
        {
            private readonly string assetPath;
            private readonly bool regenerateSignedManifest;
            private readonly bool existed;
            private readonly byte[] contents;
            private bool disposed;

            public SignedManifestScope(string assetPath, bool regenerateSignedManifest)
            {
                this.assetPath = assetPath.Replace('\\', '/');
                this.regenerateSignedManifest = regenerateSignedManifest;
                if (!regenerateSignedManifest) return;

                existed = File.Exists(assetPath);
                if (existed)
                {
                    contents = File.ReadAllBytes(assetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(ContentManifestBackupPath));
                    File.WriteAllBytes(ContentManifestBackupPath, contents);
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (!regenerateSignedManifest) return;

                try
                {
                    ContentManifestBuildProcessor.GenerateSignedManifestOrThrow();
                }
                catch
                {
                    if (existed)
                    {
                        File.WriteAllBytes(assetPath, contents);
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    }
                    else if (File.Exists(assetPath))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    throw;
                }
                if (File.Exists(ContentManifestBackupPath))
                {
                    // If deletion is interrupted, startup recovery will now restore
                    // the freshly regenerated full manifest rather than the old one.
                    File.Copy(assetPath, ContentManifestBackupPath, true);
                    File.Delete(ContentManifestBackupPath);
                }
            }
        }
    }
}
