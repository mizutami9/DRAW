#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace DrawBody.EditorTools
{
    /// <summary>
    /// Keeps runtime doodle art compact on Windows without modifying source PNGs.
    /// The art is displayed at relatively small sizes, so 1024px preserves the
    /// hand-drawn look while avoiding multi-megabyte GPU textures per sprite.
    /// </summary>
    public sealed class RuntimeAssetOptimizationImporter : AssetPostprocessor
    {
        private const string DecorationRoot = "Assets/Resources/StageDecorations/";
        private const string StageObjectRoot = "Assets/Resources/StageObjects/";
        private const string BgmRoot = "Assets/Resources/Bgm/";
        private const int WindowsMaxTextureSize = 1024;

        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessTexture()
        {
            if (!IsRuntimeArt(assetPath)) return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = false;
            importer.isReadable = false;

            TextureImporterPlatformSettings windows = importer.GetPlatformTextureSettings("Standalone");
            windows.name = "Standalone";
            windows.overridden = true;
            windows.maxTextureSize = WindowsMaxTextureSize;
            windows.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
            windows.format = TextureImporterFormat.Automatic;
            windows.textureCompression = TextureImporterCompression.CompressedHQ;
            windows.compressionQuality = 70;
            windows.crunchedCompression = true;
            importer.SetPlatformTextureSettings(windows);
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(BgmRoot, StringComparison.Ordinal)) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.65f;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
        }

        private static bool IsRuntimeArt(string path)
        {
            return path.StartsWith(DecorationRoot, StringComparison.Ordinal)
                || path.StartsWith(StageObjectRoot, StringComparison.Ordinal);
        }
    }
}
#endif
