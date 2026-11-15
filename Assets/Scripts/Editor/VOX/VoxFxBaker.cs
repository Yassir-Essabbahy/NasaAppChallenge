using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Outpost.Runtime.Audio;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor.VOX
{
    public struct VoxBakeResult
    {
        public string Id;
        public string SourcePath;
        public string OutputPath;
        public float BeforeDuration;
        public float AfterDuration;
        public long BeforeBytes;
        public long AfterBytes;
        public bool Skipped;
        public bool Succeeded;
        public string Message;
    }

    public struct AudioCandidateInfo
    {
        public string AssetPath;
        public float Duration;
        public int Channels;
        public int Frequency;
        public long FileBytes;
    }

    /// <summary>
    /// Offline pure-C# VOX audio baker.
    /// Reads original clips via temporary non-destructive copies, runs VoxFxDsp,
    /// writes 16-bit mono 22050 Hz WAV files under Assets/Audio/Announcer/Processed/,
    /// and applies Vorbis/LoadType import settings for Default + WebGL.
    /// </summary>
    public static class VoxFxBaker
    {
        public const string ProcessedDir = "Assets/Audio/Announcer/Processed";
        public const string PresetsDir = "Assets/Audio/Announcer/Presets";
        public const string ClassicPresetPath = PresetsDir + "/VOX_Classic.asset";
        public const string HeavyEchoPresetPath = PresetsDir + "/VOX_HeavyEcho.asset";
        public const string CleanRadioPresetPath = PresetsDir + "/VOX_CleanRadio.asset";
        public const string StingPresetPath = PresetsDir + "/VOX_Sting.asset";

        [MenuItem("Outpost/VOX/Bake All Announcer Voices")]
        public static void BakeAllMenu()
        {
            var results = BakeAll(force: false);
            LogBakeSummary("Bake All Announcer Voices", results);
        }

        [MenuItem("Outpost/VOX/Bake All Announcer Voices (Force)")]
        public static void BakeAllForceMenu()
        {
            var results = BakeAll(force: true);
            LogBakeSummary("Bake All Announcer Voices (Force)", results);
        }

        [MenuItem("Outpost/VOX/Bake Selected Clip")]
        public static void BakeSelectedMenu()
        {
            var clip = Selection.activeObject as AudioClip;
            if (clip == null)
            {
                Debug.LogWarning("[VOX Baker] Select an AudioClip in the Project window before running 'Bake Selected Clip'.");
                return;
            }

            EnsureDefaultPresets(out var classic, out _, out _, out var sting);
            string assetPath = AssetDatabase.GetAssetPath(clip);
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            bool looksLikeSting = stem.IndexOf("alert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  stem.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0;
            string outId = looksLikeSting ? "alert_sting" : SanitizeId(stem);
            var preset = looksLikeSting ? sting : classic;

            var res = BakeSingleClip(assetPath, outId, preset, force: true);
            LogBakeSummary("Bake Selected Clip", new List<VoxBakeResult> { res });
        }

        [MenuItem("Outpost/VOX/Bake Selected Clip", true)]
        private static bool ValidateBakeSelectedMenu() => Selection.activeObject is AudioClip;

        public static void EnsureFolders()
        {
            EnsureAssetFolder("Assets/Audio");
            EnsureAssetFolder("Assets/Audio/Announcer");
            EnsureAssetFolder(ProcessedDir);
            EnsureAssetFolder(PresetsDir);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            if (!string.IsNullOrEmpty(parent))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        public static void EnsureDefaultPresets(
            out VoxFxPreset classic,
            out VoxFxPreset heavyEcho,
            out VoxFxPreset cleanRadio,
            out VoxFxPreset sting)
        {
            EnsureFolders();
            classic = EnsurePresetAsset(ClassicPresetPath, () => VoxFxPreset.CreateClassicDefaults());
            heavyEcho = EnsurePresetAsset(HeavyEchoPresetPath, () => VoxFxPreset.CreateHeavyEchoDefaults());
            cleanRadio = EnsurePresetAsset(CleanRadioPresetPath, () => VoxFxPreset.CreateCleanRadioDefaults());
            sting = EnsurePresetAsset(StingPresetPath, () => VoxFxPreset.CreateStingDefaults());
        }

        private static VoxFxPreset EnsurePresetAsset(string path, Func<VoxFxPreset> factory)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VoxFxPreset>(path);
            if (existing != null) return existing;
            var created = factory();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static List<VoxFxPreset> LoadAllPresets()
        {
            EnsureDefaultPresets(out _, out _, out _, out _);
            var list = new List<VoxFxPreset>();
            foreach (string guid in AssetDatabase.FindAssets("t:VoxFxPreset", new[] { "Assets" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<VoxFxPreset>(p);
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        /// <summary>
        /// Scans Assets (excluding ProcessedDir) for all audio files matching .mp3, .ogg, .wav.
        /// </summary>
        public static List<AudioCandidateInfo> ScanSourceAudioAssets()
        {
            var results = new List<AudioCandidateInfo>();
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith(ProcessedDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
                if (path.IndexOf("/_temp_", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                string ext = Path.GetExtension(path);
                if (!string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".ogg", StringComparison.OrdinalIgnoreCase))
                    continue;

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                results.Add(new AudioCandidateInfo
                {
                    AssetPath = path,
                    Duration = clip.length,
                    Channels = clip.channels,
                    Frequency = clip.frequency,
                    FileBytes = bytes
                });
            }
            results.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        public static string FindMatchingOriginalPath(string lineId, List<AudioCandidateInfo> candidates)
        {
            if (candidates == null || string.IsNullOrEmpty(lineId)) return null;
            // Exact filename stem match first
            foreach (var c in candidates)
            {
                string stem = Path.GetFileNameWithoutExtension(c.AssetPath);
                if (string.Equals(stem, lineId, StringComparison.OrdinalIgnoreCase))
                    return c.AssetPath;
            }
            // Substring match fallback
            foreach (var c in candidates)
            {
                string stem = Path.GetFileNameWithoutExtension(c.AssetPath);
                if (stem.IndexOf(lineId, StringComparison.OrdinalIgnoreCase) >= 0)
                    return c.AssetPath;
            }
            return null;
        }

        public static AudioCandidateInfo? FindShortestWarningSting(List<AudioCandidateInfo> candidates, out List<AudioCandidateInfo> allCandidates)
        {
            allCandidates = new List<AudioCandidateInfo>();
            if (candidates == null) return null;

            foreach (var c in candidates)
            {
                string stem = Path.GetFileNameWithoutExtension(c.AssetPath);
                if (stem.IndexOf("alert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    stem.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    allCandidates.Add(c);
                }
            }

            if (allCandidates.Count == 0) return null;
            return allCandidates.OrderBy(c => c.Duration).First();
        }

        public static List<VoxBakeResult> BakeAll(bool force = false, VoxFxPreset overrideVoicePreset = null)
        {
            EnsureDefaultPresets(out var classic, out _, out _, out var stingPreset);
            var voicePreset = overrideVoicePreset != null ? overrideVoicePreset : classic;
            var candidates = ScanSourceAudioAssets();
            var report = new List<VoxBakeResult>();

            // 1. Warning sting
            var stingInfo = FindShortestWarningSting(candidates, out var stingCandidates);
            if (stingCandidates.Count > 1)
            {
                Debug.Log("[VOX Baker] Multiple warning/alert sting candidates found: " +
                          string.Join(", ", stingCandidates.Select(s => $"{s.AssetPath} ({s.Duration:0.00}s)")) +
                          $". Selected shortest: {stingInfo?.AssetPath}");
            }

            if (stingInfo.HasValue)
            {
                report.Add(BakeSingleClip(stingInfo.Value.AssetPath, "alert_sting", stingPreset, force));
            }
            else
            {
                report.Add(new VoxBakeResult
                {
                    Id = "alert_sting",
                    Succeeded = false,
                    Message = "MISSING warning/alert sound effect in Assets."
                });
            }

            // 2. Canonical 11 announcer voice lines
            foreach (var entry in AnnouncerDatabase.CanonicalLines)
            {
                string matchPath = FindMatchingOriginalPath(entry.id, candidates);
                if (!string.IsNullOrEmpty(matchPath))
                {
                    report.Add(BakeSingleClip(matchPath, entry.id, voicePreset, force));
                }
                else
                {
                    report.Add(new VoxBakeResult
                    {
                        Id = entry.id,
                        Succeeded = false,
                        Message = $"Original file not found in Assets (expected {entry.id}.wav/.mp3/.ogg)."
                    });
                }
            }

            // 3. Fallback phrase stitching from ElevenLabs VOX word clips if exact line files aren't added yet
            TryBakeConcatenatedElevenLabsPhrase(
                id: "n_systems",
                wordClipMarkers: new[] { "11_30_20_Lauren", "11_31_06_Lauren", "11_31_18_Lauren" },
                wordGapSec: 0.06f,
                candidates: candidates,
                preset: voicePreset,
                force: force,
                report: report);

            TryBakeConcatenatedElevenLabsPhrase(
                id: "vox_welcome",
                wordClipMarkers: new[] { "11_37_45_Jessica", "11_38_03_Jessica", "11_38_30_Jessica", "11_38_40_Jessica", "11_38_48_Jessica" },
                wordGapSec: 0.045f,
                candidates: candidates,
                preset: voicePreset,
                force: force,
                report: report);

            return report;
        }

        private static void TryBakeConcatenatedElevenLabsPhrase(
            string id,
            string[] wordClipMarkers,
            float wordGapSec,
            List<AudioCandidateInfo> candidates,
            VoxFxPreset preset,
            bool force,
            List<VoxBakeResult> report)
        {
            // Only synthesize stitched phrase if no exact single file was already baked for this id
            string outputPath = $"{ProcessedDir}/{id}.wav";
            if (report.Any(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase) && r.Succeeded))
                return;

            var matchedPaths = new List<string>();
            foreach (string marker in wordClipMarkers)
            {
                var found = candidates.FirstOrDefault(c => c.AssetPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
                if (string.IsNullOrEmpty(found.AssetPath))
                    return;
                matchedPaths.Add(found.AssetPath);
            }

            const int intermediateRate = 22050;
            var combined = new List<float>();
            long totalBeforeBytes = 0;
            float totalBeforeDur = 0f;
            int gapSamples = Mathf.RoundToInt(wordGapSec * intermediateRate);

            foreach (string p in matchedPaths)
            {
                totalBeforeBytes += File.Exists(p) ? new FileInfo(p).Length : 0;
                if (!TryReadSourcePcm(p, out float[] src, out int ch, out int sr, out float dur))
                    return;
                totalBeforeDur += dur;

                float[] mono = VoxFxDsp.MixToMonoAndResample(src, ch, sr, intermediateRate);
                mono = VoxFxDsp.TrimSilence(mono, intermediateRate, -40f, 15f);
                combined.AddRange(mono);
                for (int g = 0; g < gapSamples; g++) combined.Add(0f);
            }

            if (combined.Count == 0) return;

            float[] processed = VoxFxDsp.Process(combined.ToArray(), 1, intermediateRate, preset, out int outRate);
            float afterDur = processed.Length / (float)Math.Max(1, outRate);
            string sig = $"stitched|{string.Join("+", matchedPaths)}|{preset.ComputeSignature()}";

            if (!force && File.Exists(outputPath))
            {
                var existingImp = AssetImporter.GetAtPath(outputPath) as AudioImporter;
                var existingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(outputPath);
                if (existingImp != null && existingClip != null && existingImp.userData == sig)
                    return;
            }

            WriteWav16Mono(outputPath, processed, outRate);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
            ConfigureProcessedImporter(outputPath, afterDur, sig);
            long afterBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;

            report.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase) && !r.Succeeded);
            report.Add(new VoxBakeResult
            {
                Id = id,
                SourcePath = string.Join(" + ", matchedPaths.Select(Path.GetFileName)),
                OutputPath = outputPath,
                BeforeDuration = totalBeforeDur,
                AfterDuration = afterDur,
                BeforeBytes = totalBeforeBytes,
                AfterBytes = afterBytes,
                Skipped = false,
                Succeeded = true,
                Message = "Stitched from ElevenLabs VOX clips & baked."
            });
        }

        public static VoxBakeResult BakeSingleClip(string sourceAssetPath, string outputId, VoxFxPreset preset, bool force)
        {
            EnsureFolders();
            if (preset == null)
                EnsureDefaultPresets(out preset, out _, out _, out _);

            string cleanId = SanitizeId(outputId);
            string outputPath = $"{ProcessedDir}/{cleanId}.wav";

            if (string.IsNullOrEmpty(sourceAssetPath) || !File.Exists(sourceAssetPath))
            {
                return new VoxBakeResult
                {
                    Id = cleanId,
                    SourcePath = sourceAssetPath,
                    OutputPath = outputPath,
                    Succeeded = false,
                    Message = "Source file does not exist."
                };
            }

            long beforeBytes = new FileInfo(sourceAssetPath).Length;
            string expectedSig = BuildBakeSignature(sourceAssetPath, beforeBytes, preset);

            if (!force && File.Exists(outputPath))
            {
                var existingImp = AssetImporter.GetAtPath(outputPath) as AudioImporter;
                var existingClip = AssetDatabase.LoadAssetAtPath<AudioClip>(outputPath);
                if (existingImp != null && existingClip != null && existingImp.userData == expectedSig)
                {
                    var origClip = AssetDatabase.LoadAssetAtPath<AudioClip>(sourceAssetPath);
                    return new VoxBakeResult
                    {
                        Id = cleanId,
                        SourcePath = sourceAssetPath,
                        OutputPath = outputPath,
                        BeforeDuration = origClip != null ? origClip.length : 0f,
                        AfterDuration = existingClip.length,
                        BeforeBytes = beforeBytes,
                        AfterBytes = new FileInfo(outputPath).Length,
                        Skipped = true,
                        Succeeded = true,
                        Message = "Skipped (unchanged)."
                    };
                }
            }

            if (!TryReadSourcePcm(sourceAssetPath, out float[] sourceSamples, out int channels, out int sampleRate, out float beforeDuration))
            {
                return new VoxBakeResult
                {
                    Id = cleanId,
                    SourcePath = sourceAssetPath,
                    OutputPath = outputPath,
                    BeforeBytes = beforeBytes,
                    Succeeded = false,
                    Message = "Failed to decode source AudioClip PCM data."
                };
            }

            float[] processed = VoxFxDsp.Process(sourceSamples, channels, sampleRate, preset, out int outRate);
            float afterDuration = processed.Length / (float)Math.Max(1, outRate);

            WriteWav16Mono(outputPath, processed, outRate);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            ConfigureProcessedImporter(outputPath, afterDuration, expectedSig);
            long afterBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;

            return new VoxBakeResult
            {
                Id = cleanId,
                SourcePath = sourceAssetPath,
                OutputPath = outputPath,
                BeforeDuration = beforeDuration,
                AfterDuration = afterDuration,
                BeforeBytes = beforeBytes,
                AfterBytes = afterBytes,
                Skipped = false,
                Succeeded = true,
                Message = "Baked."
            };
        }

        public static bool TryReadSourcePcm(
            string sourceAssetPath,
            out float[] samples,
            out int channels,
            out int sampleRate,
            out float durationSec)
        {
            samples = null;
            channels = 1;
            sampleRate = 44100;
            durationSec = 0f;

            EnsureFolders();
            string ext = Path.GetExtension(sourceAssetPath);
            string tempPath = $"Assets/Audio/Announcer/_temp_read_{Guid.NewGuid():N}{ext}";

            try
            {
                if (!AssetDatabase.CopyAsset(sourceAssetPath, tempPath))
                    return false;

                AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(tempPath) as AudioImporter;
                if (imp != null)
                {
                    imp.loadInBackground = false;
                    var s = imp.defaultSampleSettings;
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.preloadAudioData = true;
                    s.compressionFormat = AudioCompressionFormat.PCM;
                    imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(tempPath);
                if (clip == null) return false;
                if (clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();

                channels = clip.channels;
                sampleRate = clip.frequency;
                durationSec = clip.length;
                int total = clip.samples * Math.Max(1, channels);
                if (total <= 0) return false;

                samples = new float[total];
                return clip.GetData(samples, 0);
            }
            finally
            {
                if (AssetDatabase.LoadMainAssetAtPath(tempPath) != null || File.Exists(tempPath))
                    AssetDatabase.DeleteAsset(tempPath);
            }
        }

        private static void ConfigureProcessedImporter(string outputPath, float durationSec, string bakeSignature)
        {
            var importer = AssetImporter.GetAtPath(outputPath) as AudioImporter;
            if (importer == null) return;

            AudioClipLoadType loadType = durationSec < 3.0f
                ? AudioClipLoadType.DecompressOnLoad
                : AudioClipLoadType.CompressedInMemory;

            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.userData = bakeSignature;

            var def = importer.defaultSampleSettings;
            def.compressionFormat = AudioCompressionFormat.Vorbis;
            def.quality = 0.45f;
            def.loadType = loadType;
            def.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            def.preloadAudioData = true;
            importer.defaultSampleSettings = def;

            var webGl = importer.GetOverrideSampleSettings("WebGL");
            webGl.compressionFormat = AudioCompressionFormat.Vorbis;
            webGl.quality = 0.45f;
            webGl.loadType = loadType;
            webGl.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            webGl.preloadAudioData = true;
            importer.SetOverrideSampleSettings("WebGL", webGl);

            importer.SaveAndReimport();
        }

        public static void WriteWav16Mono(string filePath, float[] monoSamples, int sampleRate)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int sampleCount = monoSamples != null ? monoSamples.Length : 0;
            int dataBytes = sampleCount * 2;
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs, Encoding.ASCII);

            // RIFF header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataBytes);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt subchunk (PCM mono 16-bit)
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);               // Subchunk1Size for PCM
            bw.Write((short)1);         // AudioFormat = 1 (PCM)
            bw.Write((short)1);         // NumChannels = 1 (mono)
            bw.Write(sampleRate);       // SampleRate
            bw.Write(sampleRate * 2);   // ByteRate
            bw.Write((short)2);         // BlockAlign
            bw.Write((short)16);        // BitsPerSample

            // data subchunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);

            for (int i = 0; i < sampleCount; i++)
            {
                float s = Mathf.Clamp(monoSamples[i], -1f, 1f);
                short pcm = (short)Mathf.RoundToInt(s * short.MaxValue);
                bw.Write(pcm);
            }
        }

        private static string BuildBakeSignature(string sourcePath, long sourceBytes, VoxFxPreset preset)
        {
            long ticks = File.Exists(sourcePath) ? File.GetLastWriteTimeUtc(sourcePath).Ticks : 0L;
            return $"v1|{sourcePath}|{sourceBytes}|{ticks}|{preset.ComputeSignature()}";
        }

        private static string SanitizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "unnamed_clip";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString();
        }

        private static void LogBakeSummary(string title, List<VoxBakeResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[VOX Baker] {title} complete ({results.Count} entries):");
            foreach (var r in results)
            {
                if (r.Succeeded)
                {
                    sb.AppendLine($"  - {r.Id}: {(r.Skipped ? "SKIPPED" : "BAKED")} | {r.SourcePath} ({r.BeforeDuration:0.00}s, {r.BeforeBytes} B) -> {r.OutputPath} ({r.AfterDuration:0.00}s, {r.AfterBytes} B)");
                }
                else
                {
                    sb.AppendLine($"  - {r.Id}: MISSING/FAILED | {r.Message}");
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
