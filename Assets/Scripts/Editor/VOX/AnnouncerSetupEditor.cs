using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Outpost.Editor.VOX
{
    /// <summary>
    /// Automatically populates AnnouncerDatabase with the 11 canonical announcer lines,
    /// links original audio clips by id + processed clips from Assets/Audio/Announcer/Processed/,
    /// and wires AnnouncerSystem / AnnouncerSubtitleHud references into the open Outpost scene.
    /// </summary>
    public static class AnnouncerSetupEditor
    {
        public const string DatabaseAssetPath = "Assets/Resources/AnnouncerDatabase.asset";

        [MenuItem("Outpost/Link Announcer Clips")]
        public static void LinkAnnouncerClipsMenu()
        {
            var db = EnsureAndLinkDatabase(out int linkedOriginals, out int linkedProcessed, out int missingOriginals);
            WireActiveScene(db);
            Debug.Log($"[Outpost Announcer] Linked AnnouncerDatabase ({db.lines.Count} lines): {linkedOriginals} original clip(s) matched, {linkedProcessed} processed clip(s) linked, {missingOriginals} original line(s) missing in Assets/.");
        }

        public static AnnouncerDatabase EnsureAndLinkDatabase(
            out int linkedOriginals,
            out int linkedProcessed,
            out int missingOriginals)
        {
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            var db = AssetDatabase.LoadAssetAtPath<AnnouncerDatabase>(DatabaseAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<AnnouncerDatabase>();
                AssetDatabase.CreateAsset(db, DatabaseAssetPath);
            }

            db.EnsureDefaultLines(onlyAddMissing: true);

            List<AudioCandidateInfo> candidates = VoxFxBaker.ScanSourceAudioAssets();
            var stingCandidate = VoxFxBaker.FindShortestWarningSting(candidates, out _);
            if (stingCandidate.HasValue)
                db.alertStingOriginal = AssetDatabase.LoadAssetAtPath<AudioClip>(stingCandidate.Value.AssetPath);

            string processedStingPath = $"{VoxFxBaker.ProcessedDir}/alert_sting.wav";
            if (File.Exists(processedStingPath))
                db.alertStingProcessed = AssetDatabase.LoadAssetAtPath<AudioClip>(processedStingPath);

            string processedWelcomePath = $"{VoxFxBaker.ProcessedDir}/vox_welcome.wav";
            db.welcomeVoiceProcessed = File.Exists(processedWelcomePath)
                ? AssetDatabase.LoadAssetAtPath<AudioClip>(processedWelcomePath)
                : null;

            linkedOriginals = 0;
            linkedProcessed = 0;
            missingOriginals = 0;

            for (int i = 0; i < db.lines.Count; i++)
            {
                var line = db.lines[i];
                if (line == null || string.IsNullOrWhiteSpace(line.id)) continue;

                string origPath = VoxFxBaker.FindMatchingOriginalPath(line.id, candidates);
                if (!string.IsNullOrEmpty(origPath))
                {
                    line.originalClip = AssetDatabase.LoadAssetAtPath<AudioClip>(origPath);
                    if (line.originalClip != null) linkedOriginals++;
                }
                else
                {
                    missingOriginals++;
                }

                string procPath = $"{VoxFxBaker.ProcessedDir}/{line.id}.wav";
                if (File.Exists(procPath))
                {
                    line.processedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(procPath);
                    if (line.processedClip != null) linkedProcessed++;
                }
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        public static void WireActiveScene(AnnouncerDatabase db)
        {
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null) return;

            var announcer = Object.FindAnyObjectByType<AnnouncerSystem>();
            if (announcer == null)
                announcer = bootstrap.gameObject.AddComponent<AnnouncerSystem>();

            var hud = Object.FindAnyObjectByType<AnnouncerSubtitleHud>();
            if (hud == null)
            {
                var hudGo = new GameObject("AnnouncerSubtitleCanvas");
                hudGo.transform.SetParent(bootstrap.transform, false);
                hud = hudGo.AddComponent<AnnouncerSubtitleHud>();
            }

            var airlock = Object.FindAnyObjectByType<AirlockController>();

            var soAnn = new SerializedObject(announcer);
            soAnn.FindProperty("database").objectReferenceValue = db;
            soAnn.FindProperty("subtitleHud").objectReferenceValue = hud;
            soAnn.ApplyModifiedPropertiesWithoutUndo();

            var soBoot = new SerializedObject(bootstrap);
            soBoot.FindProperty("announcerDatabase").objectReferenceValue = db;
            soBoot.FindProperty("announcerSystem").objectReferenceValue = announcer;
            soBoot.FindProperty("announcerSubtitleHud").objectReferenceValue = hud;
            if (airlock != null)
                soBoot.FindProperty("airlockController").objectReferenceValue = airlock;
            soBoot.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
        }
    }
}
