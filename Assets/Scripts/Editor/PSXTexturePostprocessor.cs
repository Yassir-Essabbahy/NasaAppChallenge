using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Enforces crisp PSX Point filtering, no mipmaps, and transparency settings on any
    /// texture imported under Assets/Art/PSX or any folder named "PSXTextures".
    /// </summary>
    public class PSXTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/Art/PSX/") && !normalized.Contains("/PSXTextures/"))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.anisoLevel = 0;
        }
    }
}
