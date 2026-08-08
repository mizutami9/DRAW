#if UNITY_EDITOR
using UnityEditor;

namespace DrawBody.Prototype.Editor
{
    public sealed class CrayonDecorationTextureImporter : AssetPostprocessor
    {
        private const string DecorationFolder = "Assets/Resources/StageDecorations/CrayonSet/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DecorationFolder, System.StringComparison.Ordinal))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
#endif
