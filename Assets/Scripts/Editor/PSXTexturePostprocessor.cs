using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Enforces crisp PSX Point filtering / no mipmaps on PSX & pack textures,
    /// Mars.jpg equirectangular settings, and crisp Sprite import settings for UI icons.
    /// </summary>
    public class PSXTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string normalized = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;

            if (normalized == "Assets/Models/Mars.jpg")
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (normalized.StartsWith("Assets/Art/UI/Icons/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;
                return;
            }

            if (normalized.StartsWith("Assets/Art/PSX/")
                || normalized.Contains("/PSXTextures/")
                || normalized.StartsWith("Assets/Models/scifi-hard-surface-corridor-kit-free-sample-lite/"))
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.anisoLevel = 0;
            }
        }
    }
}
