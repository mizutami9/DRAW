using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace DrawBody.EditorTools
{
    internal sealed class DemoStageBuildFilter : IDisposable
    {
        private static readonly HashSet<string> AllowedStageIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "1-1", "1-2", "1-3", "6-3", "8-2", "9-3", "11-2", "14-3"
        };

        private readonly List<MovedFile> movedFiles = new List<MovedFile>();
        private readonly string backupDirectory;
        private readonly bool enabled;
        private bool disposed;

        private DemoStageBuildFilter(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled) return;

            backupDirectory = Path.Combine("Library", "NicoDrawDemoStageBackup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDirectory);
            try
            {
                string stageDirectory = Path.Combine("Assets", "Resources", "Stages");
                string[] stageFiles = Directory.GetFiles(stageDirectory, "*.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < stageFiles.Length; i++)
                {
                    string stageId = Path.GetFileNameWithoutExtension(stageFiles[i]);
                    if (AllowedStageIds.Contains(stageId)) continue;
                    MoveToBackup(stageFiles[i]);
                    string metaPath = stageFiles[i] + ".meta";
                    if (File.Exists(metaPath)) MoveToBackup(metaPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch
            {
                RestoreFiles();
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
            disposed = true;
            if (!enabled) return;
            RestoreFiles();
        }

        private void MoveToBackup(string sourcePath)
        {
            string destination = Path.Combine(backupDirectory, Path.GetFileName(sourcePath));
            File.Move(sourcePath, destination);
            movedFiles.Add(new MovedFile(sourcePath, destination));
        }

        private void RestoreFiles()
        {
            bool restoredAll = true;
            for (int i = movedFiles.Count - 1; i >= 0; i--)
            {
                MovedFile moved = movedFiles[i];
                if (!File.Exists(moved.Destination)) continue;
                if (File.Exists(moved.Source))
                {
                    restoredAll = false;
                    continue;
                }
                File.Move(moved.Destination, moved.Source);
            }

            if (restoredAll)
            {
                movedFiles.Clear();
                if (!string.IsNullOrEmpty(backupDirectory) && Directory.Exists(backupDirectory))
                {
                    Directory.Delete(backupDirectory, true);
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!restoredAll)
            {
                throw new IOException("Demo stage backup could not be restored because a source file already exists: "
                    + backupDirectory);
            }
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
