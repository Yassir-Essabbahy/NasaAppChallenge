using System.IO;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Generates the procedural 1024x576 first-person EVA helmet visor overlay sprite:
    /// ~12% dark rounded-rectangle helmet frame, thin bottom chin guard, soft inner vignette,
    /// subtle cyan glass tint, and two low-alpha diagonal glare streaks.
    /// Re-run anytime via Outpost/Suit/Generate Visor Sprite or replace the asset directly.
    /// </summary>
    public static class SuitVisorSpriteGenerator
    {
        public const string VisorAssetPath = "Assets/Art/UI/Suit/T_SuitVisor_1024x576.png";
        private const int Width = 1024;
        private const int Height = 576;

        [MenuItem("Outpost/Suit/Generate Visor Sprite")]
        public static void GenerateVisorSpriteMenu()
        {
            var sprite = GenerateOrLoadVisorSprite(forceRegenerate: true);
            if (sprite != null)
            {
                Selection.activeObject = sprite;
                Debug.Log($"[SuitVisorSpriteGenerator] Generated {Width}x{Height} visor sprite at {VisorAssetPath}");
            }
        }

        public static Sprite GenerateOrLoadVisorSprite(bool forceRegenerate = false)
        {
            if (!forceRegenerate)
            {
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(VisorAssetPath);
                if (existing != null)
                    return existing;
            }

            string dir = Path.GetDirectoryName(VisorAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: false, linear: false);
            var pixels = new Color32[Width * Height];

            // Viewport geometry in UV space: ~12% outer border on sides/top and raised chin guard at bottom
            const float centerX = 0.50f;
            const float centerY = 0.515f;
            const float halfW = 0.390f; // leaves ~11% margin each side
            const float halfH = 0.365f; // leaves ~12% top, ~15% bottom avg
            const float cornerRadius = 0.115f;
            const float aspect = (float)Width / Height;

            for (int y = 0; y < Height; y++)
            {
                float v = (y + 0.5f) / Height;
                int rowOffset = y * Width;

                for (int x = 0; x < Width; x++)
                {
                    float u = (x + 0.5f) / Width;

                    // Thin raised sculpted chin piece along bottom-center of viewport
                    float chinBump = 0f;
                    float dxCenter = Mathf.Abs(u - 0.5f);
                    if (v < 0.34f && dxCenter < 0.26f)
                    {
                        float t = 1f - SmoothStep01(0.07f, 0.26f, dxCenter);
                        float vFade = 1f - SmoothStep01(0.20f, 0.34f, v);
                        chinBump = 0.042f * t * vFade;
                    }

                    // Signed distance to rounded-rectangle clear opening in aspect-corrected units
                    float qx = (Mathf.Abs(u - centerX) - (halfW - cornerRadius / aspect)) * aspect;
                    float py = (v - chinBump) - centerY;
                    float qy = Mathf.Abs(py) - (halfH - cornerRadius);

                    float outsideLen = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float insideDist = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float sdf = outsideLen + insideDist - cornerRadius;

                    if (sdf >= 0f)
                    {
                        // Outer helmet frame: dark slate metal with subtle rim bevel & seam lines
                        float bevel = 1f - Mathf.Clamp01(sdf / 0.022f);
                        float shade = Mathf.Lerp(0.055f, 0.145f, bevel * 0.7f + (1f - v) * 0.18f);

                        // Subtle gasket highlight right on inner lip (0..0.008)
                        float lipGlow = sdf < 0.008f ? (1f - sdf / 0.008f) * 0.07f : 0f;

                        // Subtle helmet top brow ridge & chin seam groove
                        float seam = (Mathf.Abs(u - 0.5f) < 0.004f && v < 0.18f) ? -0.025f : 0f;
                        float r = Mathf.Clamp01(shade * 0.85f + lipGlow * 0.45f + seam);
                        float g = Mathf.Clamp01(shade * 0.96f + lipGlow * 0.85f + seam);
                        float b = Mathf.Clamp01(shade * 1.18f + lipGlow * 1.00f + seam);

                        pixels[rowOffset + x] = new Color(r, g, b, 0.985f);
                    }
                    else
                    {
                        // Glass interior: anti-aliased inner rim + soft inner vignette + slight cyan tint + 2 diagonal glare streaks
                        float rimDist = -sdf;
                        float edgeAa = 1f - Mathf.Clamp01(rimDist / 0.005f);
                        float vignette = Mathf.Pow(1f - Mathf.Clamp01(rimDist / 0.135f), 2.1f);

                        // Subtle cyan glass tint (keeps low-res PSX lighting legible)
                        float glassR = 0.20f;
                        float glassG = 0.82f;
                        float glassB = 0.92f;
                        float glassA = 0.032f;

                        // Soft inner dark vignette near frame edges
                        float vigAlpha = vignette * 0.34f + edgeAa * 0.65f;

                        // Two soft diagonal glare streaks at low alpha across upper/lower glass
                        float diag1 = (u * 0.72f + v) - 0.98f;
                        float streak1 = Mathf.Exp(-(diag1 * diag1) / (2f * 0.022f * 0.022f)) * 0.045f;
                        float streak1Mask = SmoothStep01(0.18f, 0.55f, v) * SmoothStep01(0.88f, 0.45f, u);
                        streak1 *= streak1Mask;

                        float diag2 = (u * 0.72f + v) - 1.18f;
                        float streak2 = Mathf.Exp(-(diag2 * diag2) / (2f * 0.011f * 0.011f)) * 0.030f;
                        float streak2Mask = SmoothStep01(0.28f, 0.68f, v) * SmoothStep01(0.92f, 0.55f, u);
                        streak2 *= streak2Mask;

                        float totalGlare = streak1 + streak2;

                        // Composite dark rim vignette and subtle cyan glare over clear viewport
                        float outA = Mathf.Clamp01(glassA + vigAlpha + totalGlare);
                        float outR = (glassR * glassA + 0.04f * vigAlpha + 0.68f * totalGlare) / Mathf.Max(0.0001f, outA);
                        float outG = (glassG * glassA + 0.06f * vigAlpha + 0.92f * totalGlare) / Mathf.Max(0.0001f, outA);
                        float outB = (glassB * glassA + 0.09f * vigAlpha + 1.00f * totalGlare) / Mathf.Max(0.0001f, outA);

                        pixels[rowOffset + x] = new Color(Mathf.Clamp01(outR), Mathf.Clamp01(outG), Mathf.Clamp01(outB), outA);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(VisorAssetPath, png);
            AssetDatabase.ImportAsset(VisorAssetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(VisorAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(VisorAssetPath);
        }

        private static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
