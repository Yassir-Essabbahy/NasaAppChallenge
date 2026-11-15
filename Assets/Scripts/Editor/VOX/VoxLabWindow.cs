using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Outpost.Runtime.Audio;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor.VOX
{
    /// <summary>
    /// Interactive VOX FX Lab window: choose preset & clip, tune every DSP parameter,
    /// audition live in-memory Original vs Processed audio via AudioUtil, and bake single/all clips.
    /// </summary>
    public class VoxLabWindow : EditorWindow
    {
        private List<VoxFxPreset> _presets = new List<VoxFxPreset>();
        private List<AudioCandidateInfo> _clips = new List<AudioCandidateInfo>();
        private int _presetIdx;
        private int _clipIdx;
        private VoxFxPreset _workingCopy;
        private Vector2 _scroll;

        private AudioClip _previewClip;
        private float _lastBeforeDur;
        private float _lastAfterDur;
        private long _lastBeforeBytes;
        private long _lastAfterBytes;
        private string _statusLine = "Ready.";

        [MenuItem("Outpost/VOX/Open VOX Lab")]
        public static void Open()
        {
            var win = GetWindow<VoxLabWindow>("VOX Lab");
            win.minSize = new Vector2(520f, 640f);
            win.Show();
        }

        private void OnEnable()
        {
            RefreshLists();
        }

        private void OnDisable()
        {
            StopPreview();
            if (_previewClip != null) DestroyImmediate(_previewClip);
            if (_workingCopy != null) DestroyImmediate(_workingCopy);
        }

        private void RefreshLists()
        {
            _presets = VoxFxBaker.LoadAllPresets();
            _clips = VoxFxBaker.ScanSourceAudioAssets();
            _presetIdx = Mathf.Clamp(_presetIdx, 0, Math.Max(0, _presets.Count - 1));
            _clipIdx = Mathf.Clamp(_clipIdx, 0, Math.Max(0, _clips.Count - 1));
            LoadWorkingPresetFromSelection();
            UpdateSelectedClipStats();
        }

        private void LoadWorkingPresetFromSelection()
        {
            if (_workingCopy != null) DestroyImmediate(_workingCopy);
            if (_presets.Count > 0 && _presetIdx < _presets.Count && _presets[_presetIdx] != null)
                _workingCopy = _presets[_presetIdx].CloneInMemory();
            else
                _workingCopy = VoxFxPreset.CreateClassicDefaults();
        }

        private void UpdateSelectedClipStats()
        {
            if (_clips.Count == 0 || _clipIdx < 0 || _clipIdx >= _clips.Count)
            {
                _lastBeforeDur = 0f;
                _lastBeforeBytes = 0;
                return;
            }
            var c = _clips[_clipIdx];
            _lastBeforeDur = c.Duration;
            _lastBeforeBytes = c.FileBytes;

            string outId = GuessOutputId(c.AssetPath);
            string procPath = $"{VoxFxBaker.ProcessedDir}/{outId}.wav";
            if (File.Exists(procPath))
            {
                var procClip = AssetDatabase.LoadAssetAtPath<AudioClip>(procPath);
                _lastAfterDur = procClip != null ? procClip.length : 0f;
                _lastAfterBytes = new FileInfo(procPath).Length;
            }
            else
            {
                _lastAfterDur = 0f;
                _lastAfterBytes = 0;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("VOX FX Lab (Pure C# Offline DSP)", EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh Assets", GUILayout.Width(115f)))
                    RefreshLists();
            }

            DrawPickers();
            DrawStatsBox();

            EditorGUILayout.Space(4f);
            DrawActionButtons();

            EditorGUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_workingCopy != null)
                DrawPresetParameters(_workingCopy);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(_statusLine, MessageType.Info);
        }

        private void DrawPickers()
        {
            string[] presetNames = new string[_presets.Count];
            for (int i = 0; i < _presets.Count; i++)
                presetNames[i] = _presets[i] != null ? _presets[i].presetName : "<null>";

            int nextPreset = EditorGUILayout.Popup("Preset", _presetIdx, presetNames);
            if (nextPreset != _presetIdx)
            {
                _presetIdx = nextPreset;
                LoadWorkingPresetFromSelection();
            }

            if (_clips.Count == 0)
            {
                EditorGUILayout.HelpBox("No source .wav/.mp3/.ogg audio clips found in Assets.", MessageType.Warning);
                return;
            }

            string[] clipLabels = new string[_clips.Count];
            for (int i = 0; i < _clips.Count; i++)
                clipLabels[i] = $"{Path.GetFileName(_clips[i].AssetPath)} ({_clips[i].Duration:0.00}s)";

            int nextClip = EditorGUILayout.Popup("Source Clip", _clipIdx, clipLabels);
            if (nextClip != _clipIdx)
            {
                _clipIdx = nextClip;
                UpdateSelectedClipStats();
            }
        }

        private void DrawStatsBox()
        {
            if (_clips.Count == 0 || _clipIdx >= _clips.Count) return;
            var c = _clips[_clipIdx];
            string afterTxt = _lastAfterBytes > 0
                ? $"Processed: {_lastAfterDur:0.00}s | {_lastAfterBytes:N0} B"
                : "Processed: (not baked yet)";
            EditorGUILayout.LabelField(
                $"Original: {_lastBeforeDur:0.00}s | {c.Channels}ch {c.Frequency}Hz | {_lastBeforeBytes:N0} B   ->   {afterTxt}",
                EditorStyles.miniLabel);
        }

        private void DrawActionButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play Original", GUILayout.Height(28f)))
                    PlaySelectedOriginal();
                if (GUILayout.Button("Play Processed (Live)", GUILayout.Height(28f)))
                    PlaySelectedProcessedPreview();
                if (GUILayout.Button("Stop", GUILayout.Width(64f), GUILayout.Height(28f)))
                    StopPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Tweaks To Preset", GUILayout.Height(24f)) && _presets.Count > 0 && _presetIdx < _presets.Count)
                {
                    VoxFxPreset.CopyValues(_workingCopy, _presets[_presetIdx]);
                    EditorUtility.SetDirty(_presets[_presetIdx]);
                    AssetDatabase.SaveAssets();
                    _statusLine = $"Saved parameter changes to {_presets[_presetIdx].presetName}.";
                }

                if (GUILayout.Button("Bake This", GUILayout.Height(24f)))
                    BakeSelectedFromLab();

                if (GUILayout.Button("Bake All", GUILayout.Height(24f)))
                {
                    VoxFxPreset voicePreset = _presets.Count > 0 ? _presets[_presetIdx] : _workingCopy;
                    var results = VoxFxBaker.BakeAll(force: true, overrideVoicePreset: voicePreset);
                    int ok = 0;
                    foreach (var r in results) if (r.Succeeded) ok++;
                    _statusLine = $"Bake All finished: {ok}/{results.Count} succeeded (missing voice lines skipped cleanly).";
                    UpdateSelectedClipStats();
                }
            }
        }

        private static void DrawPresetParameters(VoxFxPreset p)
        {
            EditorGUILayout.LabelField("Format & Trim", EditorStyles.boldLabel);
            p.targetSampleRate = EditorGUILayout.IntSlider("Target Sample Rate", p.targetSampleRate, 8000, 44100);
            p.enableTrim = EditorGUILayout.Toggle("Enable Trim Silence", p.enableTrim);
            p.trimThresholdDb = EditorGUILayout.Slider("Trim Threshold (dB)", p.trimThresholdDb, -70f, -15f);
            p.trimPaddingMs = EditorGUILayout.Slider("Trim Padding (ms)", p.trimPaddingMs, 0f, 150f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Telephone Band (4th-Order)", EditorStyles.boldLabel);
            p.enableBandPass = EditorGUILayout.Toggle("Enable Band-Pass", p.enableBandPass);
            p.highPassHz = EditorGUILayout.Slider("High-Pass Hz", p.highPassHz, 80f, 1200f);
            p.lowPassHz = EditorGUILayout.Slider("Low-Pass Hz", p.lowPassHz, 1200f, 8000f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Digital Grit & Ring Mod", EditorStyles.boldLabel);
            p.enableGrit = EditorGUILayout.Toggle("Enable Digital Grit", p.enableGrit);
            p.downsampleFactor = EditorGUILayout.IntSlider("Downsample Factor", p.downsampleFactor, 1, 8);
            p.bitDepth = EditorGUILayout.IntSlider("Bit Depth", p.bitDepth, 4, 16);
            p.gritMix = EditorGUILayout.Slider("Grit Mix", p.gritMix, 0f, 1f);
            p.enableRingMod = EditorGUILayout.Toggle("Enable Ring Mod", p.enableRingMod);
            p.ringModHz = EditorGUILayout.Slider("Ring Mod Hz", p.ringModHz, 20f, 220f);
            p.ringModMix = EditorGUILayout.Slider("Ring Mod Mix", p.ringModMix, 0f, 1f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Saturation & Echo", EditorStyles.boldLabel);
            p.enableSaturation = EditorGUILayout.Toggle("Enable Saturation", p.enableSaturation);
            p.saturationDrive = EditorGUILayout.Slider("Tanh Drive", p.saturationDrive, 1f, 5f);
            p.enableEcho = EditorGUILayout.Toggle("Enable Echo", p.enableEcho);
            p.echoTap1Ms = EditorGUILayout.Slider("Echo Tap 1 (ms)", p.echoTap1Ms, 20f, 350f);
            p.echoTap1Gain = EditorGUILayout.Slider("Echo Tap 1 Gain", p.echoTap1Gain, 0f, 0.8f);
            p.echoTap2Ms = EditorGUILayout.Slider("Echo Tap 2 (ms)", p.echoTap2Ms, 40f, 600f);
            p.echoTap2Gain = EditorGUILayout.Slider("Echo Tap 2 Gain", p.echoTap2Gain, 0f, 0.8f);
            p.echoLowPassHz = EditorGUILayout.Slider("Echo LP Cutoff Hz", p.echoLowPassHz, 800f, 6000f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Schroeder Hall Reverb", EditorStyles.boldLabel);
            p.enableReverb = EditorGUILayout.Toggle("Enable Reverb", p.enableReverb);
            p.reverbFeedback = EditorGUILayout.Slider("Comb Feedback", p.reverbFeedback, 0f, 0.92f);
            p.reverbDamping = EditorGUILayout.Slider("HF Damping", p.reverbDamping, 0.05f, 0.85f);
            p.reverbWet = EditorGUILayout.Slider("Reverb Wet", p.reverbWet, 0f, 1f);
            p.reverbTailSec = EditorGUILayout.Slider("Reverb Tail (s)", p.reverbTailSec, 0f, 1.5f);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Radio Squelch & Output", EditorStyles.boldLabel);
            p.enableSquelch = EditorGUILayout.Toggle("Enable Squelch", p.enableSquelch);
            p.squelchStartMs = EditorGUILayout.Slider("Squelch Start (ms)", p.squelchStartMs, 0f, 150f);
            p.squelchEndMs = EditorGUILayout.Slider("Squelch End (ms)", p.squelchEndMs, 0f, 120f);
            p.squelchLevelDb = EditorGUILayout.Slider("Squelch Level (dB)", p.squelchLevelDb, -50f, -15f);
            p.enableNormalize = EditorGUILayout.Toggle("Enable Normalize", p.enableNormalize);
            p.normalizeTargetDb = EditorGUILayout.Slider("Normalize Peak (dB)", p.normalizeTargetDb, -6f, -1f);
            p.fadeMs = EditorGUILayout.Slider("Fade In/Out (ms)", p.fadeMs, 0f, 60f);
            p.limiterCeilingDb = EditorGUILayout.Slider("Limiter Ceiling (dB)", p.limiterCeilingDb, -3f, -0.5f);
        }

        private void PlaySelectedOriginal()
        {
            if (_clips.Count == 0 || _clipIdx >= _clips.Count) return;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(_clips[_clipIdx].AssetPath);
            if (clip == null) return;
            PlayEditorClip(clip);
            _statusLine = $"Playing original: {Path.GetFileName(_clips[_clipIdx].AssetPath)}";
        }

        private void PlaySelectedProcessedPreview()
        {
            if (_clips.Count == 0 || _clipIdx >= _clips.Count) return;
            string path = _clips[_clipIdx].AssetPath;
            if (!VoxFxBaker.TryReadSourcePcm(path, out float[] src, out int ch, out int sr, out float beforeDur))
            {
                _statusLine = "Failed to decode source audio for preview.";
                return;
            }

            float[] processed = VoxFxDsp.Process(src, ch, sr, _workingCopy, out int outRate);
            if (_previewClip != null) DestroyImmediate(_previewClip);
            _previewClip = VoxFxDsp.CreateRuntimeClip("VOX_LivePreview", processed, outRate);
            _lastBeforeDur = beforeDur;
            _lastAfterDur = _previewClip.length;
            PlayEditorClip(_previewClip);
            _statusLine = $"Live preview ({_workingCopy.presetName}): {beforeDur:0.00}s -> {_previewClip.length:0.00}s ({processed.Length} samples @ {outRate} Hz).";
        }

        private void BakeSelectedFromLab()
        {
            if (_clips.Count == 0 || _clipIdx >= _clips.Count) return;
            string srcPath = _clips[_clipIdx].AssetPath;
            string outId = GuessOutputId(srcPath);
            var res = VoxFxBaker.BakeSingleClip(srcPath, outId, _workingCopy, force: true);
            if (res.Succeeded)
            {
                _lastAfterDur = res.AfterDuration;
                _lastAfterBytes = res.AfterBytes;
                _statusLine = $"Baked {outId}: {res.BeforeDuration:0.00}s ({res.BeforeBytes:N0} B) -> {res.AfterDuration:0.00}s ({res.AfterBytes:N0} B).";
            }
            else
            {
                _statusLine = $"Bake failed: {res.Message}";
            }
        }

        private static string GuessOutputId(string srcPath)
        {
            string stem = Path.GetFileNameWithoutExtension(srcPath);
            if (stem.IndexOf("alert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                stem.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
                return "alert_sting";
            return stem;
        }

        private static void PlayEditorClip(AudioClip clip)
        {
            StopPreview();
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var play = audioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            play?.Invoke(null, new object[] { clip, 0, false });
        }

        private static void StopPreview()
        {
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var stop = audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            stop?.Invoke(null, null);
        }
    }
}
