using System;
using System.IO;
using NUnit.Framework;
using Outpost.Editor.VOX;
using Outpost.Runtime.Audio;
using UnityEditor;
using UnityEngine;

namespace Outpost.Tests.EditMode
{
    public class VoxDspTests
    {
        private static float[] GenerateSyntheticSpeechBurst(int sampleRate, float durationSec, float leadSilenceSec = 0f, float trailSilenceSec = 0f)
        {
            int leadSamples = Mathf.RoundToInt(leadSilenceSec * sampleRate);
            int toneSamples = Mathf.RoundToInt(durationSec * sampleRate);
            int trailSamples = Mathf.RoundToInt(trailSilenceSec * sampleRate);
            float[] samples = new float[leadSamples + toneSamples + trailSamples];

            for (int i = 0; i < toneSamples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Sin(Mathf.PI * (i / (float)Math.Max(1, toneSamples - 1)));
                samples[leadSamples + i] = env * 0.7f * (
                    0.6f * Mathf.Sin(2f * Mathf.PI * 220f * t) +
                    0.35f * Mathf.Sin(2f * Mathf.PI * 660f * t) +
                    0.15f * Mathf.Sin(2f * Mathf.PI * 1450f * t));
            }
            return samples;
        }

        [Test]
        public void Process_AllPresets_NonEmpty_Finite_AndPeakUnderMinusOneDb()
        {
            const int inRate = 44100;
            // Stereo interleaved synthetic speech burst
            float[] mono = GenerateSyntheticSpeechBurst(inRate, 0.45f);
            float[] stereo = new float[mono.Length * 2];
            for (int i = 0; i < mono.Length; i++)
            {
                stereo[i * 2] = mono[i];
                stereo[i * 2 + 1] = mono[i] * 0.8f;
            }

            VoxFxPreset[] presets =
            {
                VoxFxPreset.CreateClassicDefaults(),
                VoxFxPreset.CreateHeavyEchoDefaults(),
                VoxFxPreset.CreateCleanRadioDefaults(),
                VoxFxPreset.CreateStingDefaults()
            };

            float maxAllowedPeak = VoxFxDsp.DbToLinear(-1.0f) + 1e-4f;

            try
            {
                foreach (var preset in presets)
                {
                    float[] result = VoxFxDsp.Process(stereo, 2, inRate, preset, out int outRate);
                    Assert.AreEqual(22050, outRate, $"Unexpected rate for {preset.presetName}");
                    Assert.Greater(result.Length, 0, $"Empty output for {preset.presetName}");

                    float peak = 0f;
                    for (int i = 0; i < result.Length; i++)
                    {
                        Assert.IsTrue(float.IsFinite(result[i]), $"Non-finite sample at {i} for {preset.presetName}");
                        float abs = Mathf.Abs(result[i]);
                        if (abs > peak) peak = abs;
                    }

                    Assert.Greater(peak, 0.05f, $"Output unexpectedly silent for {preset.presetName}");
                    Assert.LessOrEqual(peak, maxAllowedPeak, $"Peak {VoxFxDsp.LinearToDb(peak):0.00} dBFS exceeded -1.0 dBFS for {preset.presetName}");
                }
            }
            finally
            {
                foreach (var preset in presets)
                    UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void TrimSilence_RemovesLeadingAndTrailingSilenceWithPadding()
        {
            const int sr = 22050;
            // 1.0s silence + 0.3s burst + 1.0s silence = 2.3s total
            float[] padded = GenerateSyntheticSpeechBurst(sr, 0.30f, leadSilenceSec: 1.0f, trailSilenceSec: 1.0f);
            float[] trimmed = VoxFxDsp.TrimSilence(padded, sr, thresholdDb: -45f, paddingMs: 30f);

            // Trimmed output should be ~0.36s (< 0.50s), drastically shorter than 2.3s
            float trimmedSec = trimmed.Length / (float)sr;
            Assert.Less(trimmedSec, 0.50f);
            Assert.Greater(trimmedSec, 0.25f);
        }

        [Test]
        public void Baker_Idempotent_SkipsUnchangedAndOverwritesOnForce()
        {
            string testId = "_test_vox_idempotent";
            string tempSrcPath = $"Assets/Audio/Announcer/{testId}.wav";
            string tempOutPath = $"{VoxFxBaker.ProcessedDir}/{testId}.wav";
            VoxFxPreset preset = VoxFxPreset.CreateStingDefaults();

            try
            {
                VoxFxBaker.EnsureFolders();
                float[] srcSamples = GenerateSyntheticSpeechBurst(44100, 0.35f, 0.05f, 0.05f);
                VoxFxBaker.WriteWav16Mono(tempSrcPath, srcSamples, 44100);
                AssetDatabase.ImportAsset(tempSrcPath, ImportAssetOptions.ForceUpdate);

                // First bake writes WAV + Vorbis import settings
                var first = VoxFxBaker.BakeSingleClip(tempSrcPath, testId, preset, force: false);
                Assert.IsTrue(first.Succeeded, first.Message);
                Assert.IsFalse(first.Skipped);
                Assert.IsTrue(File.Exists(tempOutPath));

                var importer = AssetImporter.GetAtPath(tempOutPath) as AudioImporter;
                Assert.IsNotNull(importer);
                Assert.IsTrue(importer.forceToMono);
                Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, importer.defaultSampleSettings.loadType);

                // Second bake without force skips unchanged file
                var second = VoxFxBaker.BakeSingleClip(tempSrcPath, testId, preset, force: false);
                Assert.IsTrue(second.Succeeded, second.Message);
                Assert.IsTrue(second.Skipped, "Second bake without Force should skip unchanged output.");

                // Third bake with force overwrites cleanly
                var third = VoxFxBaker.BakeSingleClip(tempSrcPath, testId, preset, force: true);
                Assert.IsTrue(third.Succeeded, third.Message);
                Assert.IsFalse(third.Skipped, "Forced bake should overwrite output.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
                if (File.Exists(tempSrcPath) || AssetDatabase.LoadMainAssetAtPath(tempSrcPath) != null)
                    AssetDatabase.DeleteAsset(tempSrcPath);
                if (File.Exists(tempOutPath) || AssetDatabase.LoadMainAssetAtPath(tempOutPath) != null)
                    AssetDatabase.DeleteAsset(tempOutPath);
                AssetDatabase.Refresh();
            }
        }
    }
}
