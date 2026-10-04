using System;
using System.Collections.Generic;
using System.IO;
using DrawBody.Prototype;
using UnityEditor;

namespace DrawBody.EditorTools
{
    internal sealed class DemoStageBuildFilter : IDisposable
    {
        private const int FileMoveMaxAttempts = 30;
        private const int FileMoveRetryDelayMilliseconds = 100;
        private static readonly string BackupRoot =
            Path.Combine("Library", "NicoDrawDemoStageBackup");

        private static readonly HashSet<string> AllowedBgmNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "title", "1", "6", "8", "9", "11", "14"
        };

        private readonly List<MovedFile> movedFiles = new List<MovedFile>();
        private readonly string backupDirectory;
        private readonly bool enabled;
        private bool disposed;
        private bool activeFilterReleased;
        private static int activeFilterCount;

        internal static bool IsFilteringDemoBuild => activeFilterCount > 0;

        [InitializeOnLoadMethod]
        private static void ScheduleInterruptedBackupRecovery()
        {
            EditorApplication.delayCall += RecoverInterruptedBackups;
        }

        internal static bool IsStageAllowedForDemo(string stageId)
        {
            return DemoAccessPolicy.IsIncludedStage(stageId);
        }

        private DemoStageBuildFilter(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled) return;

            activeFilterCount++;

            backupDirectory = Path.Combine(BackupRoot, Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(backupDirectory);
                MoveExcludedAssets(
                    Path.Combine("Assets", "Resources", "Stages"),
                    "*.json",
                    DemoAccessPolicy.IsIncludedStage,
                    false);
                try
                {
                    AssetDatabase.ReleaseCachedFileHandles();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning(
                        "Could not release Unity's cached asset file handles before filtering demo BGM: "
                        + exception.Message);
                }
                MoveExcludedAssets(
                    Path.Combine("Assets", "Resources", "Bgm"),
                    "*.mp3",
                    AllowedBgmNames.Contains,
                    true);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception filterException)
            {
                Exception restoreException = null;
                try
                {
                    RestoreFiles();
                }
                catch (Exception exception)
                {
                    restoreException = exception;
                }
                finally
                {
                    ReleaseActiveFilter();
                }

                if (restoreException != null)
                {
                    throw new AggregateException(
                        "Demo asset filtering failed and its backup could not be fully restored.",
                        new[] { filterException, restoreException });
                }
                throw;
            }
        }

        public static DemoStageBuildFilter Enter(bool enabled)
        {
            return new DemoStageBuildFilter(enabled);
        }

        public void Dispose()
        {
            if (disposed) return;
            if (!enabled)
            {
                disposed = true;
                return;
            }

            try
            {
                RestoreFiles();
                disposed = true;
            }
            finally
            {
                // A failed restore remains retryable through Dispose, but this
                // build-filter scope must stop affecting later scene builds.
                ReleaseActiveFilter();
            }
        }

        private void MoveExcludedAssets(
            string directory,
            string searchPattern,
            Func<string, bool> isAllowed,
            bool keepLockedAssets)
        {
            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string assetName = Path.GetFileNameWithoutExtension(files[i]);
                if (isAllowed(assetName)) continue;

                try
                {
                    MoveToBackup(files[i]);
                }
                catch (IOException exception) when (keepLockedAssets)
                {
                    // Unity's audio importer can retain an MP3 handle for the
                    // editor process lifetime. Stage JSON remains fail-closed;
                    // a locked music file may remain because it grants no stage access.
                    UnityEngine.Debug.LogWarning(
                        "Demo build kept a locked BGM asset after releasing cached file handles: "
                        + files[i] + "\n" + exception.Message);
                    continue;
                }
                string metaPath = files[i] + ".meta";
                if (File.Exists(metaPath)) MoveToBackup(metaPath);
            }
        }

        private void MoveToBackup(string sourcePath)
        {
            string normalizedSource = sourcePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedSource)
                || !normalizedSource.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new IOException("Demo build filter received an invalid asset path: " + sourcePath);
            }

            string destination = Path.Combine(backupDirectory, normalizedSource);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            MoveFileWithRetries(sourcePath, destination, "exclude asset from the demo build");
            movedFiles.Add(new MovedFile(sourcePath, destination));
        }

        private static void RecoverInterruptedBackups()
        {
            if (activeFilterCount > 0 || !Directory.Exists(BackupRoot)) return;

            string[] backupDirectories = Directory.GetDirectories(BackupRoot, "*", SearchOption.TopDirectoryOnly);
            bool restoredAny = false;
            for (int i = 0; i < backupDirectories.Length; i++)
            {
                string backup = backupDirectories[i];
                string[] files = Directory.GetFiles(backup, "*", SearchOption.AllDirectories);
                bool restoredDirectory = true;
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string sourcePath = ResolveInterruptedSourcePath(backup, files[fileIndex]);
                    if (string.IsNullOrEmpty(sourcePath) || File.Exists(sourcePath))
                    {
                        restoredDirectory = false;
                        UnityEngine.Debug.LogError(
                            "Could not automatically restore an interrupted demo-build backup: "
                            + files[fileIndex]);
                        continue;
                    }

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
                        MoveFileWithRetries(files[fileIndex], sourcePath, "recover interrupted demo-build backup");
                        restoredAny = true;
                    }
                    catch (Exception exception)
                    {
                        restoredDirectory = false;
                        UnityEngine.Debug.LogError(
                            "Could not automatically restore an interrupted demo-build backup: "
                            + exception.Message);
                    }
                }

                if (restoredDirectory && Directory.Exists(backup)) Directory.Delete(backup, true);
            }

            if (restoredAny)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                UnityEngine.Debug.Log("Recovered assets left by an interrupted demo build.");
            }
        }

        private static string ResolveInterruptedSourcePath(string backupDirectoryPath, string backupFilePath)
        {
            string relative = backupFilePath.Substring(backupDirectoryPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relative.StartsWith("Assets" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return relative;

            // Compatibility with the original flat backup layout.
            string fileName = Path.GetFileName(backupFilePath);
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".json.meta", StringComparison.OrdinalIgnoreCase))
                return Path.Combine("Assets", "Resources", "Stages", fileName);
            if (fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".mp3.meta", StringComparison.OrdinalIgnoreCase))
                return Path.Combine("Assets", "Resources", "Bgm", fileName);
            return null;
        }

        private static void MoveFileWithRetries(string sourcePath, string destinationPath, string operation)
        {
            IOException lastError = null;
            for (int attempt = 1; attempt <= FileMoveMaxAttempts; attempt++)
            {
                try
                {
                    File.Move(sourcePath, destinationPath);
                    return;
                }
                catch (IOException exception)
                {
                    lastError = exception;
                    if (attempt == FileMoveMaxAttempts) break;
                    System.Threading.Thread.Sleep(FileMoveRetryDelayMilliseconds);
                }
            }

            throw new IOException(
                $"Could not {operation} after {FileMoveMaxAttempts} attempts: {sourcePath} -> {destinationPath}",
                lastError);
        }

        private void RestoreFiles()
        {
            List<Exception> failures = new List<Exception>();
            for (int i = movedFiles.Count - 1; i >= 0; i--)
            {
                MovedFile moved = movedFiles[i];
                try
                {
                    if (!File.Exists(moved.Destination))
                    {
                        if (File.Exists(moved.Source))
                        {
                            movedFiles.RemoveAt(i);
                            continue;
                        }

                        throw new FileNotFoundException(
                            "Both the original asset and its demo-build backup are missing.",
                            moved.Destination);
                    }
                    if (File.Exists(moved.Source))
                    {
                        throw new IOException(
                            "The original asset path already exists and cannot be overwritten: " + moved.Source);
                    }

                    MoveFileWithRetries(moved.Destination, moved.Source, "restore demo-build backup");
                    movedFiles.RemoveAt(i);
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        $"Failed to restore demo-build backup: {moved.Destination} -> {moved.Source}",
                        exception));
                }
            }

            if (movedFiles.Count == 0
                && !string.IsNullOrEmpty(backupDirectory)
                && Directory.Exists(backupDirectory))
            {
                try
                {
                    Directory.Delete(backupDirectory, true);
                }
                catch (Exception exception)
                {
                    failures.Add(new IOException(
                        "Demo-build backup files were restored, but the empty backup directory could not be removed: "
                        + backupDirectory,
                        exception));
                }
            }

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                failures.Add(new IOException("AssetDatabase refresh failed after restoring demo-build assets.", exception));
            }

            if (failures.Count > 0)
            {
                throw new IOException(
                    $"Demo-build backup restoration completed with {failures.Count} error(s); "
                    + $"{movedFiles.Count} file(s) remain in: {backupDirectory}",
                    new AggregateException(failures));
            }
        }

        private void ReleaseActiveFilter()
        {
            if (activeFilterReleased) return;
            activeFilterReleased = true;
            activeFilterCount--;
        }

        private readonly struct MovedFile
        {
            public readonly string Source;
            public readonly string Destination;

            public MovedFile(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }
        }
    }
}
