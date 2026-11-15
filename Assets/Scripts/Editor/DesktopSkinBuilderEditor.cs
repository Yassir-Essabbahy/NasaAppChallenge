using System.IO;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outpost.Editor
{
    /// <summary>
    /// Idempotent Editor tool under "Outpost/UI/Build Desktop Skin" that wires
    /// DesktopSkinController, WindowManager, AccessibilityTextScaler, and the Mars wallpaper
    /// sprite onto PF_TerminalRoot.prefab, TerminalTheme assets, Outpost.unity, and Terminal.unity.
    /// </summary>
    public static class DesktopSkinBuilderEditor
    {
        private const string TerminalPrefabPath = "Assets/Prefabs/UI/PF_TerminalRoot.prefab";
        private const string OutpostScenePath = "Assets/Scenes/Outpost.unity";
        private const string TerminalScenePath = "Assets/Scenes/Terminal.unity";
        private const string WallpaperPath = "Assets/Art/UI/Icons/MarsWallpaper.png";
        private const string FallbackWallpaperPath = "Assets/Resources/UI/MarsWallpaper.png";

        [MenuItem("Outpost/UI/Build Desktop Skin")]
        public static void BuildDesktopSkin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[DesktopSkinBuilder] Exit Play mode before running Build Desktop Skin.");
                return;
            }

            Sprite wallpaper = EnsureMarsWallpaperSprite();
            WireThemeAsset("Assets/Resources/TerminalTheme.asset", wallpaper);
            WireThemeAsset("Assets/Art/UI/TerminalTheme.asset", wallpaper);

            bool prefabUpdated = WireTerminalPrefab();
            int scenesUpdated = WireScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[DesktopSkinBuilder] Built Desktop OS skin wiring safely. PrefabUpdated={prefabUpdated}, ScenesUpdated={scenesUpdated}, Wallpaper={(wallpaper != null ? wallpaper.name : "Procedural")}");
        }

        private static Sprite EnsureMarsWallpaperSprite()
        {
            string targetPath = File.Exists(WallpaperPath) ? WallpaperPath : FallbackWallpaperPath;
            if (!File.Exists(targetPath))
            {
                string dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Texture2D tex = TerminalUiArt.BuildMarsWallpaperTexture();
                File.WriteAllBytes(targetPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(targetPath);
        }

        private static void WireThemeAsset(string themeAssetPath, Sprite wallpaper)
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(themeAssetPath);
            if (theme == null) return;

            var so = new SerializedObject(theme);
            var wpProp = so.FindProperty("MarsWallpaperSprite");
            if (wpProp != null && wallpaper != null && wpProp.objectReferenceValue == null)
            {
                wpProp.objectReferenceValue = wallpaper;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        private static bool WireTerminalPrefab()
        {
            if (!File.Exists(TerminalPrefabPath))
            {
                Debug.LogWarning($"[DesktopSkinBuilder] Prefab not found at {TerminalPrefabPath}.");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(TerminalPrefabPath);
            try
            {
                bool changed = EnsureTerminalComponents(root);
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, TerminalPrefabPath);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int WireScenes()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            int updatedCount = 0;

            string[] scenePaths = { OutpostScenePath, TerminalScenePath };
            foreach (string path in scenePaths)
            {
                if (!File.Exists(path)) continue;

                Scene scene = EditorSceneManager.GetSceneByPath(path);
                bool openedHere = !scene.isLoaded;
                if (openedHere)
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

                bool sceneDirty = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    var controllers = root.GetComponentsInChildren<TerminalController>(true);
                    foreach (var tc in controllers)
                    {
                        if (EnsureTerminalComponents(tc.gameObject))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(tc);
                            sceneDirty = true;
                        }

                        var scaler = tc.GetComponentInParent<CanvasScaler>();
                        if (scaler != null && scaler.referenceResolution != new Vector2(1920f, 1080f))
                        {
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                            scaler.referenceResolution = new Vector2(1920f, 1080f);
                            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                            scaler.matchWidthOrHeight = 0.5f;
                            EditorUtility.SetDirty(scaler);
                            sceneDirty = true;
                        }
                    }
                }

                if (sceneDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    updatedCount++;
                }

                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }

            if (!string.IsNullOrEmpty(originalScenePath))
            {
                var orig = EditorSceneManager.GetSceneByPath(originalScenePath);
                if (orig.isLoaded)
                    SceneManager.SetActiveScene(orig);
            }

            return updatedCount;
        }

        private static bool EnsureTerminalComponents(GameObject terminalGo)
        {
            if (terminalGo == null) return false;
            bool changed = false;

            var tc = terminalGo.GetComponent<TerminalController>();
            if (tc == null) return false;

            var wm = terminalGo.GetComponent<WindowManager>();
            if (wm == null)
            {
                terminalGo.AddComponent<WindowManager>();
                changed = true;
            }

            var scaler = terminalGo.GetComponent<AccessibilityTextScaler>();
            if (scaler == null)
            {
                terminalGo.AddComponent<AccessibilityTextScaler>();
                changed = true;
            }

            var skin = terminalGo.GetComponent<DesktopSkinController>();
            if (skin == null)
            {
                skin = terminalGo.AddComponent<DesktopSkinController>();
                changed = true;
            }

            var so = new SerializedObject(tc);
            var skinProp = so.FindProperty("desktopSkin");
            if (skinProp != null && skinProp.objectReferenceValue != skin)
            {
                skinProp.objectReferenceValue = skin;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(terminalGo);

            return changed;
        }
    }
}
