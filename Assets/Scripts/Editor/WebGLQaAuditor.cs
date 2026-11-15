using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost.Editor
{
    /// <summary>
    /// Pre-build QA audit & asset import optimizer for WebGL release readiness:
    /// checks Build Settings scene order, scans scenes & prefabs for missing scripts/references,
    /// audits & optimizes AudioClip / Texture2D WebGL import settings, lists largest assets, and clears dev PlayerPrefs.
    /// </summary>
    public static class WebGLQaAuditor
    {
        [MenuItem("Outpost/QA/Reset Dev PlayerPrefs")]
        public static void ClearDevPlayerPrefs()
        {
            PlayerPrefs.DeleteKey("introSeen");
            PlayerPrefs.DeleteKey("Outpost_OnboardingDone");
            PlayerPrefs.DeleteKey("Outpost_LanguageSelected");
            PlayerPrefs.DeleteKey("Outpost_Language");
            PlayerPrefs.Save();
            Debug.Log("[WebGLQaAuditor] Cleared dev PlayerPrefs keys ('introSeen', 'Outpost_OnboardingDone', 'Outpost_LanguageSelected', 'Outpost_Language').");
        }

        [MenuItem("Outpost/QA/Optimize Audio & Textures For WebGL")]
        public static void OptimizeAudioAndTexturesForWebGL()
        {
            int audioUpdated = 0;
            int texUpdated = 0;

            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < audioGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    if (importer == null) continue;

                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    float lengthSec = clip != null ? clip.length : 1f;
                    bool isAtmosphere = path.IndexOf("/Sounds/Atmosphere/", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isVoxOrProcessed = path.IndexOf("/Sounds/VOX/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            path.IndexOf("/Audio/Announcer/", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isFootstepOrSfx = path.IndexOf("step", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           path.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           path.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool desiredMono = isVoxOrProcessed || isFootstepOrSfx || !isAtmosphere;
                    bool changed = false;

                    if (importer.forceToMono != desiredMono)
                    {
                        importer.forceToMono = desiredMono;
                        changed = true;
                    }

                    // Unity 6 Windows Editor FMOD rejects CompressedInMemory + Vorbis when activeTarget=WebGL,
                    // whereas WebAudio in WebGL works cleanly with DecompressOnLoad + Vorbis.
                    AudioImporterSampleSettings webglSettings = importer.GetOverrideSampleSettings("WebGL");
                    AudioClipLoadType desiredLoadType = AudioClipLoadType.DecompressOnLoad;
                    AudioCompressionFormat desiredFormat = AudioCompressionFormat.Vorbis;
                    const float desiredQuality = 0.45f;
                    const uint desiredSampleRate = 44100u;

                    bool hasOverride = importer.ContainsSampleSettingsOverride("WebGL");
                    if (!hasOverride ||
                        webglSettings.loadType != desiredLoadType ||
                        webglSettings.compressionFormat != desiredFormat ||
                        !webglSettings.preloadAudioData ||
                        Mathf.Abs(webglSettings.quality - desiredQuality) > 0.02f)
                    {
                        webglSettings.loadType = desiredLoadType;
                        webglSettings.compressionFormat = desiredFormat;
                        webglSettings.quality = desiredQuality;
                        webglSettings.preloadAudioData = true;
                        // Unity WebGL AudioImporter requires OverrideSampleRate with 44100
                        if (webglSettings.sampleRateOverride == 0)
                            webglSettings.sampleRateOverride = desiredSampleRate;
                        webglSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                        if (importer.SetOverrideSampleSettings("WebGL", webglSettings))
                            changed = true;
                        else
                            Debug.LogWarning($"[WebGLQaAuditor] Failed to set WebGL audio override on '{path}'.");
                    }

                    AudioImporterSampleSettings defSettings = importer.defaultSampleSettings;
                    if (defSettings.loadType != desiredLoadType || !defSettings.preloadAudioData)
                    {
                        defSettings.loadType = desiredLoadType;
                        defSettings.preloadAudioData = true;
                        importer.defaultSampleSettings = defSettings;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        audioUpdated++;
                    }
                }

                string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
                for (int i = 0; i < texGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                    if (string.IsNullOrEmpty(path)) continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (importer.maxTextureSize > 2048)
                    {
                        importer.maxTextureSize = 2048;
                        changed = true;
                    }

                    long fileBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                    var webglTex = importer.GetPlatformTextureSettings("WebGL");
                    int targetMaxSize = path.IndexOf("Scifi_Corridor_PRO_LITE", StringComparison.OrdinalIgnoreCase) >= 0 ? 512 : 1024;
                    bool shouldCompressWebGl = importer.textureCompression == TextureImporterCompression.Uncompressed && fileBytes > 256 * 1024;

                    if (shouldCompressWebGl || (webglTex != null && webglTex.overridden && webglTex.maxTextureSize > 2048))
                    {
                        webglTex.name = "WebGL";
                        webglTex.overridden = true;
                        webglTex.maxTextureSize = Mathf.Min(importer.maxTextureSize, targetMaxSize);
                        webglTex.format = TextureImporterFormat.Automatic;
                        webglTex.textureCompression = TextureImporterCompression.Compressed;
                        importer.SetPlatformTextureSettings(webglTex);
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        texUpdated++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WebGLQaAuditor] Optimized WebGL import settings: {audioUpdated} audio clips updated, {texUpdated} oversized textures capped <= 2048.");
        }

        [MenuItem("Outpost/QA/Audit Project For WebGL")]
        public static void AuditProject()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WEBGL PRE-BUILD QA AUDIT ===");

            // 1. Build Settings Scenes
            var buildScenes = EditorBuildSettings.scenes;
            sb.AppendLine($"Build Settings Scenes ({buildScenes.Length}):");
            bool hasMainMenu = false, hasOutpost = false, hasTerminal = false;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var bs = buildScenes[i];
                bool exists = !string.IsNullOrEmpty(bs.path) && File.Exists(bs.path);
                sb.AppendLine($"  [{i}] enabled={bs.enabled} exists={exists} path={bs.path}");
                if (bs.enabled && bs.path.EndsWith("MainMenu.unity", StringComparison.OrdinalIgnoreCase)) hasMainMenu = true;
                if (bs.enabled && bs.path.EndsWith("Outpost.unity", StringComparison.OrdinalIgnoreCase)) hasOutpost = true;
                if (bs.enabled && bs.path.EndsWith("Terminal.unity", StringComparison.OrdinalIgnoreCase)) hasTerminal = true;
            }
            if (!hasMainMenu || !hasOutpost || !hasTerminal)
                Debug.LogWarning("[WebGLQaAuditor] Expected MainMenu, Outpost, and Terminal enabled in Build Settings!");

            // 2. Scan Build Scenes for missing scripts & core scene objects
            int missingScriptObjects = 0;
            int sceneWarnings = 0;
            int totalBuildSceneGos = 0;
            string initialActiveScenePath = SceneManager.GetActiveScene().path;

            for (int s = 0; s < buildScenes.Length; s++)
            {
                string scenePath = buildScenes[s].path;
                if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath)) continue;

                Scene alreadyOpen = SceneManager.GetSceneByPath(scenePath);
                bool openedTemp = !alreadyOpen.isLoaded;
                Scene scene = openedTemp
                    ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                    : alreadyOpen;

                try
                {
                    bool hasEventSystem = false;
                    bool hasCamera = false;
                    var roots = scene.GetRootGameObjects();
                    for (int r = 0; r < roots.Length; r++)
                    {
                        var transforms = roots[r].GetComponentsInChildren<Transform>(true);
                        for (int t = 0; t < transforms.Length; t++)
                        {
                            totalBuildSceneGos++;
                            GameObject go = transforms[t].gameObject;
                            if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)
                                hasEventSystem = true;
                            if (go.GetComponent<Camera>() != null)
                                hasCamera = true;
                            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                            if (missing > 0)
                            {
                                missingScriptObjects++;
                                Debug.LogError($"[WebGLQaAuditor] Missing script ({missing}) in scene '{scenePath}' on '{GetHierarchyPath(go.transform)}'");
                            }
                        }
                    }

                    if (!hasCamera)
                    {
                        sceneWarnings++;
                        Debug.LogWarning($"[WebGLQaAuditor] Scene '{scenePath}' has no Camera.");
                    }
                    if (!hasEventSystem)
                    {
                        sceneWarnings++;
                        Debug.LogWarning($"[WebGLQaAuditor] Scene '{scenePath}' has no EventSystem.");
                    }
                }
                finally
                {
                    if (openedTemp && scene.IsValid())
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            // Restore initial active scene if needed
            if (!string.IsNullOrEmpty(initialActiveScenePath))
            {
                var orig = SceneManager.GetSceneByPath(initialActiveScenePath);
                if (orig.IsValid())
                    SceneManager.SetActiveScene(orig);
            }

            // 3. Scan Prefabs for missing scripts
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            int prefabsChecked = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                prefabsChecked++;

                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[t].gameObject);
                    if (missing > 0)
                    {
                        missingScriptObjects++;
                        Debug.LogError($"[WebGLQaAuditor] Missing script ({missing}) in prefab '{path}' on '{GetHierarchyPath(transforms[t])}'");
                    }
                }
            }

            // 4. Audio & Texture Import Audit
            int streamingAudioCount = 0;
            int pcmLargeAudioCount = 0;
            int noWebGlOverrideCount = 0;
            var streamingPaths = new List<string>();
            string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            for (int i = 0; i < audioGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                bool hasWebGl = importer.ContainsSampleSettingsOverride("WebGL");
                if (!hasWebGl) noWebGlOverrideCount++;
                var eff = hasWebGl ? importer.GetOverrideSampleSettings("WebGL") : importer.defaultSampleSettings;
                if (eff.loadType == AudioClipLoadType.Streaming)
                {
                    streamingAudioCount++;
                    streamingPaths.Add(path);
                }
                long fileBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (eff.compressionFormat == AudioCompressionFormat.PCM && fileBytes > 256 * 1024)
                    pcmLargeAudioCount++;
            }

            int largeTexCount = 0;
            int uncompressedLargeTex = 0;
            var uncompressedTexPaths = new List<string>();
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            for (int i = 0; i < texGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                var webglTex = importer.GetPlatformTextureSettings("WebGL");
                int effectiveMaxSize = (webglTex != null && webglTex.overridden) ? webglTex.maxTextureSize : importer.maxTextureSize;
                TextureImporterCompression effectiveComp = (webglTex != null && webglTex.overridden) ? webglTex.textureCompression : importer.textureCompression;
                if (effectiveMaxSize > 2048) largeTexCount++;
                long fileBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (effectiveComp == TextureImporterCompression.Uncompressed && fileBytes > 256 * 1024)
                {
                    uncompressedLargeTex++;
                    uncompressedTexPaths.Add($"{fileBytes / 1024f:0.0} KB ({importer.filterMode}, {effectiveMaxSize}): {path}");
                }
            }

            // 5. Resources Folder & Largest Non-Code Assets
            var nonCodeFiles = new List<(string relPath, long bytes)>();
            long resourcesBytes = 0;
            int resourcesFiles = 0;
            string[] allFiles = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories);
            for (int i = 0; i < allFiles.Length; i++)
            {
                string f = allFiles[i].Replace('\\', '/');
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;

                long len = new FileInfo(f).Length;
                nonCodeFiles.Add((f, len));
                if (f.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                {
                    resourcesBytes += len;
                    resourcesFiles++;
                }
            }

            var topAssets = nonCodeFiles.OrderByDescending(x => x.bytes).Take(10).ToList();

            sb.AppendLine($"Scanned {totalBuildSceneGos} scene GameObjects & {prefabsChecked} prefabs -> MissingScriptObjects={missingScriptObjects}, SceneWarnings={sceneWarnings}");
            sb.AppendLine($"Audio Clips ({audioGuids.Length}): NoWebGLOverride={noWebGlOverrideCount}, Streaming={streamingAudioCount}, LargePCM={pcmLargeAudioCount}");
            sb.AppendLine($"Textures ({texGuids.Length}): MaxSize>2048={largeTexCount}, LargeUncompressed={uncompressedLargeTex}");
            Debug.Log(sb.ToString());

            Debug.Log($"[WebGLQaAuditor] Resources Folder: {resourcesFiles} files, {resourcesBytes / 1024f:0.0} KB");
            foreach (string sPath in streamingPaths)
                Debug.LogWarning($"[WebGLQaAuditor] Streaming Audio (unsafe on WebGL): {sPath}");
            foreach (string uPath in uncompressedTexPaths)
                Debug.Log($"[WebGLQaAuditor] Large Uncompressed Texture: {uPath}");
            for (int i = 0; i < topAssets.Count; i++)
                Debug.Log($"[WebGLQaAuditor] Top {i + 1}: {topAssets[i].bytes / (1024f * 1024f):0.00} MB | {topAssets[i].relPath}");
        }

        private static string GetHierarchyPath(Transform t)
        {
            if (t == null) return string.Empty;
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
