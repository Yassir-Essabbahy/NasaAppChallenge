using Outpost.Runtime.Room;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Outpost.Editor
{
    /// <summary>
    /// Configures import settings for Assets/Sounds/Atmosphere/* and links them onto
    /// AudioManager in Outpost.unity (SpaceShip = interior loop, MarsSound = exterior loop,
    /// DoorOpen / CloseDoor = facility door SFX). Original audio files are never modified.
    /// </summary>
    public static class AtmosphereAudioSetupEditor
    {
        public const string OutpostScenePath = "Assets/Scenes/Outpost.unity";
        public const string SpaceShipPath = "Assets/Sounds/Atmosphere/SpaceShip.mp3";
        public const string MarsSoundPath = "Assets/Sounds/Atmosphere/MarsSound.wav";
        public const string DoorOpenPath = "Assets/Sounds/Atmosphere/DoorOpen.wav";
        public const string CloseDoorPath = "Assets/Sounds/Atmosphere/CloseDoor.wav";

        [MenuItem("Outpost/Link Atmosphere Audio")]
        public static void LinkAtmosphereAudio()
        {
            ConfigureAudioImporter(SpaceShipPath, AudioClipLoadType.Streaming, 0.45f, preloadDefault: false,
                AudioClipLoadType.CompressedInMemory, 0.40f);
            ConfigureAudioImporter(MarsSoundPath, AudioClipLoadType.CompressedInMemory, 0.50f, preloadDefault: true,
                AudioClipLoadType.CompressedInMemory, 0.45f);
            ConfigureAudioImporter(DoorOpenPath, AudioClipLoadType.DecompressOnLoad, 0.70f, preloadDefault: true,
                AudioClipLoadType.DecompressOnLoad, 0.70f);
            ConfigureAudioImporter(CloseDoorPath, AudioClipLoadType.DecompressOnLoad, 0.70f, preloadDefault: true,
                AudioClipLoadType.DecompressOnLoad, 0.70f);

            AudioClip spaceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SpaceShipPath);
            AudioClip marsClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MarsSoundPath);
            AudioClip doorOpenClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorOpenPath);
            AudioClip doorCloseClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CloseDoorPath);

            if (spaceClip == null || marsClip == null || doorOpenClip == null || doorCloseClip == null)
            {
                Debug.LogError(
                    $"[AtmosphereAudio] Missing clip(s): SpaceShip={spaceClip != null}, MarsSound={marsClip != null}, DoorOpen={doorOpenClip != null}, CloseDoor={doorCloseClip != null}");
                return;
            }

            Scene active = SceneManager.GetActiveScene();
            if (active.path != OutpostScenePath)
                active = EditorSceneManager.OpenScene(OutpostScenePath, OpenSceneMode.Single);

            AudioManager audioManager = Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (audioManager == null)
            {
                Debug.LogError("[AtmosphereAudio] Could not find AudioManager in Outpost.unity.");
                return;
            }

            SerializedObject so = new SerializedObject(audioManager);
            so.FindProperty("roomHumLoopClip").objectReferenceValue = spaceClip;
            so.FindProperty("windLoopClip").objectReferenceValue = marsClip;
            so.FindProperty("doorOpenClip").objectReferenceValue = doorOpenClip;
            so.FindProperty("doorCloseClip").objectReferenceValue = doorCloseClip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(audioManager);

            DoorInteractable[] doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include);
            int clearedOverrides = 0;
            foreach (DoorInteractable door in doors)
            {
                SerializedObject dso = new SerializedObject(door);
                var openProp = dso.FindProperty("overrideOpenClip");
                var closeProp = dso.FindProperty("overrideCloseClip");
                bool changed = false;
                if (openProp != null && openProp.objectReferenceValue != null)
                {
                    openProp.objectReferenceValue = null;
                    changed = true;
                }
                if (closeProp != null && closeProp.objectReferenceValue != null)
                {
                    closeProp.objectReferenceValue = null;
                    changed = true;
                }
                if (changed)
                {
                    dso.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(door);
                    clearedOverrides++;
                }
            }

            EditorSceneManager.MarkSceneDirty(active);
            EditorSceneManager.SaveScene(active);

            Debug.Log(
                $"[AtmosphereAudio] Linked onto AudioManager in Outpost.unity: " +
                $"Interior='{spaceClip.name}' ({spaceClip.length:F1}s), " +
                $"Exterior='{marsClip.name}' ({marsClip.length:F1}s), " +
                $"DoorOpen='{doorOpenClip.name}' ({doorOpenClip.length:F2}s), " +
                $"DoorClose='{doorCloseClip.name}' ({doorCloseClip.length:F2}s); " +
                $"Doors checked={doors.Length} (clearedOverrides={clearedOverrides}).");
        }

        private static void ConfigureAudioImporter(
            string assetPath,
            AudioClipLoadType defaultLoadType,
            float defaultQuality,
            bool preloadDefault,
            AudioClipLoadType webglLoadType,
            float webglQuality)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[AtmosphereAudio] AudioImporter not found for {assetPath}");
                return;
            }

            bool dirty = false;
            if (importer.forceToMono)
            {
                importer.forceToMono = false;
                dirty = true;
            }
            if (!importer.loadInBackground)
            {
                importer.loadInBackground = true;
                dirty = true;
            }

            AudioImporterSampleSettings def = importer.defaultSampleSettings;
            if (def.loadType != defaultLoadType ||
                def.compressionFormat != AudioCompressionFormat.Vorbis ||
                Mathf.Abs(def.quality - defaultQuality) > 0.01f ||
                def.preloadAudioData != preloadDefault)
            {
                def.loadType = defaultLoadType;
                def.compressionFormat = AudioCompressionFormat.Vorbis;
                def.quality = defaultQuality;
                def.preloadAudioData = preloadDefault;
                importer.defaultSampleSettings = def;
                dirty = true;
            }

            const string webglPlatform = "WebGL";
            AudioImporterSampleSettings webgl = importer.GetOverrideSampleSettings(webglPlatform);
            bool hasWebglOverride = importer.ContainsSampleSettingsOverride(webglPlatform);
            if (!hasWebglOverride ||
                webgl.loadType != webglLoadType ||
                webgl.compressionFormat != AudioCompressionFormat.Vorbis ||
                Mathf.Abs(webgl.quality - webglQuality) > 0.01f ||
                !webgl.preloadAudioData)
            {
                webgl.loadType = webglLoadType;
                webgl.compressionFormat = AudioCompressionFormat.Vorbis;
                webgl.quality = webglQuality;
                webgl.preloadAudioData = true;
                importer.SetOverrideSampleSettings(webglPlatform, webgl);
                dirty = true;
            }

            if (dirty)
                importer.SaveAndReimport();
        }
    }
}
