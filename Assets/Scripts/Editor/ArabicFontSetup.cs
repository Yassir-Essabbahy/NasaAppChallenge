#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Terminal;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Outpost.EditorTools
{
    /// <summary>
    /// Builds a self-contained static Arabic SDF fallback font asset from Assets/Fonts/TahomaArabic.ttf
    /// and registers it in TMP Settings and LiberationSans SDF so Arabic renders in Editor and Release WebGL.
    /// Also provides one-shot project setup utilities for intro trimming and player movement speed tuning.
    /// </summary>
    public static class ArabicFontSetup
    {
        private const string SourceTtfPath = "Assets/Fonts/TahomaArabic.ttf";
        private const string OutputFontAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/TahomaArabic SDF.asset";
        private const string DefaultLibFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string IntroDataAssetPath = "Assets/Resources/IntroSequenceData.asset";
        private const string FpsPrefabPath = "Assets/ModularFirstPersonController/FirstPersonController/FirstPersonController.prefab";

        [MenuItem("Outpost/Localization/Setup Arabic TMP Fallback")]
        public static void SetupArabicFallbackFont()
        {
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(SourceTtfPath);
            if (ttf == null)
            {
                Debug.LogError($"[ArabicFontSetup] Missing source TTF at {SourceTtfPath}.");
                return;
            }

            // Build or recreate the static SDF font asset covering ASCII, punctuation, and Arabic ranges
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(OutputFontAssetPath);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                ttf,
                40,
                4,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);

            if (fontAsset == null)
            {
                Debug.LogError("[ArabicFontSetup] Failed to create TMP_FontAsset from TahomaArabic.ttf.");
                return;
            }

            fontAsset.name = "TahomaArabic SDF";
            AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);

            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = "TahomaArabic SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "TahomaArabic SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            string charset = BuildArabicAndCommonCharset();
            bool addedAll = fontAsset.TryAddCharacters(charset, out string missingChars);
            if (fontAsset.atlasTexture != null)
                fontAsset.atlasTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            // Switch to Static so TMP_Settings.ClearDynamicDataOnBuild does not wipe Arabic glyphs in WebGL
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            // Register on TMP_Settings global fallback list
            var settings = TMP_Settings.instance;
            if (settings != null)
            {
                var fallbacks = TMP_Settings.fallbackFontAssets;
                if (!fallbacks.Contains(fontAsset))
                    fallbacks.Add(fontAsset);
                EditorUtility.SetDirty(settings);
            }

            // Also attach on LiberationSans SDF fallback list so every theme font picks it up immediately
            var libFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultLibFontPath);
            if (libFont != null)
            {
                if (libFont.fallbackFontAssetTable == null)
                    libFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!libFont.fallbackFontAssetTable.Contains(fontAsset))
                    libFont.fallbackFontAssetTable.Add(fontAsset);
                EditorUtility.SetDirty(libFont);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ArabicFontSetup] Created static {OutputFontAssetPath} (addedAll={addedAll}, missing={missingChars?.Length ?? 0}, glyphs={fontAsset.glyphTable?.Count ?? 0}).");
        }

        [MenuItem("Outpost/Localization/Apply Intro & Speed Updates")]
        public static void ApplyIntroAndSpeedUpdates()
        {
            // 1. Trim IntroSequenceData.asset to leave ONLY "THE SUN IS FAR AWAY."
            var introAsset = AssetDatabase.LoadAssetAtPath<IntroSequenceData>(IntroDataAssetPath);
            if (introAsset != null)
            {
                introAsset.PopulateDefaultCards();
                EditorUtility.SetDirty(introAsset);
                Debug.Log($"[ArabicFontSetup] Updated {IntroDataAssetPath} card count -> {introAsset.Cards.Count}.");
            }
            else
            {
                Debug.LogWarning($"[ArabicFontSetup] Could not load {IntroDataAssetPath}.");
            }

            // 2. Slightly reduce FirstPersonController walkSpeed/sprintSpeed on the shared prefab
            var prefabRoot = PrefabUtility.LoadPrefabContents(FpsPrefabPath);
            if (prefabRoot != null)
            {
                Component fpc = prefabRoot.GetComponent("FirstPersonController");
                if (fpc != null)
                {
                    var so = new SerializedObject(fpc);
                    SerializedProperty walkProp = so.FindProperty("walkSpeed");
                    SerializedProperty sprintProp = so.FindProperty("sprintSpeed");
                    if (walkProp != null) walkProp.floatValue = 3.6f;
                    if (sprintProp != null) sprintProp.floatValue = 5.2f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(fpc);
                    Debug.Log($"[ArabicFontSetup] Updated FirstPersonController speed -> walk={walkProp?.floatValue}, sprint={sprintProp?.floatValue}.");
                }
                else
                {
                    Debug.LogWarning("[ArabicFontSetup] FirstPersonController component not found on prefab.");
                }
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, FpsPrefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            else
            {
                Debug.LogWarning($"[ArabicFontSetup] Could not load {FpsPrefabPath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string BuildArabicAndCommonCharset()
        {
            var seen = new HashSet<char>();
            var sb = new System.Text.StringBuilder(512);

            void AddChar(char c)
            {
                if (seen.Add(c)) sb.Append(c);
            }

            void AddRange(int start, int end)
            {
                for (int cp = start; cp <= end; cp++)
                    AddChar((char)cp);
            }

            // Basic ASCII & symbols used alongside Arabic
            AddRange(0x0020, 0x007E);
            AddChar('\u00A0'); // Non-breaking space
            AddChar('\u00B0'); // Degree symbol
            AddChar('\u00B7'); // Middle dot
            AddChar('\u2013'); // En dash
            AddChar('\u2014'); // Em dash
            AddChar('\u2022'); // Bullet
            AddChar('\u2026'); // Ellipsis

            // Core Arabic block + Presentation Forms-A ligatures + Presentation Forms-B
            AddRange(0x060C, 0x060C); // Arabic comma
            AddRange(0x061B, 0x061B); // Arabic semicolon
            AddRange(0x061F, 0x061F); // Arabic question mark
            AddRange(0x0621, 0x064A); // Arabic letters
            AddRange(0x0660, 0x066A); // Arabic-Indic digits + percent
            AddRange(0x06CC, 0x06CC); // Farsi Yeh
            AddRange(0xFEF5, 0xFEFC); // Lam-Alef ligatures
            AddRange(0xFE70, 0xFEFF); // Arabic Presentation Forms-B

            return sb.ToString();
        }
    }
}
#endif
