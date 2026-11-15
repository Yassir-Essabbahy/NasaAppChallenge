using System.Globalization;
using System.Text;
using UnityEngine;

namespace Outpost.Runtime.Audio
{
    /// <summary>
    /// ScriptableObject holding all offline pure-C# VOX DSP baking parameters.
    /// Shared cleanly between Editor baking / VOX Lab preview and automated DSP verification.
    /// </summary>
    [CreateAssetMenu(menuName = "Outpost/Audio/VOX FX Preset", fileName = "VOX_Preset")]
    public class VoxFxPreset : ScriptableObject
    {
        public string presetName = "VOX Classic";

        [Header("Format & Silence Trim")]
        [Range(8000, 44100)] public int targetSampleRate = 22050;
        public bool enableTrim = true;
        [Range(-70f, -15f)] public float trimThresholdDb = -45f;
        [Range(0f, 150f)] public float trimPaddingMs = 30f;

        [Header("Telephone Band (4th-Order Cascaded Biquads)")]
        public bool enableBandPass = true;
        [Range(80f, 1200f)] public float highPassHz = 320f;
        [Range(1200f, 8000f)] public float lowPassHz = 3200f;

        [Header("PSX / Radio Digital Grit")]
        public bool enableGrit = true;
        [Range(1, 8)] public int downsampleFactor = 2;
        [Range(4, 16)] public int bitDepth = 10;
        [Range(0f, 1f)] public float gritMix = 1.0f;

        [Header("Robot Ring Modulation")]
        public bool enableRingMod = true;
        [Range(20f, 220f)] public float ringModHz = 70f;
        [Range(0f, 1f)] public float ringModMix = 0.30f;

        [Header("Soft Saturation")]
        public bool enableSaturation = true;
        [Range(1f, 5f)] public float saturationDrive = 1.6f;

        [Header("Dark Echo Taps")]
        public bool enableEcho = true;
        [Range(20f, 350f)] public float echoTap1Ms = 90f;
        [Range(0f, 0.8f)] public float echoTap1Gain = 0.28f;
        [Range(40f, 600f)] public float echoTap2Ms = 180f;
        [Range(0f, 0.8f)] public float echoTap2Gain = 0.14f;
        [Range(800f, 6000f)] public float echoLowPassHz = 2500f;

        [Header("Schroeder Hall Reverb (4 Comb + 2 All-Pass)")]
        public bool enableReverb = true;
        [Range(0f, 0.92f)] public float reverbFeedback = 0.78f;
        [Range(0.05f, 0.85f)] public float reverbDamping = 0.35f;
        [Range(0f, 1f)] public float reverbWet = 0.22f;
        [Range(0f, 1.5f)] public float reverbTailSec = 1.0f;

        [Header("Radio Squelch")]
        public bool enableSquelch = true;
        [Range(0f, 150f)] public float squelchStartMs = 70f;
        [Range(0f, 120f)] public float squelchEndMs = 40f;
        [Range(-50f, -15f)] public float squelchLevelDb = -32f;

        [Header("Output Dynamics")]
        public bool enableNormalize = true;
        [Range(-6f, -1f)] public float normalizeTargetDb = -1.5f;
        [Range(0f, 60f)] public float fadeMs = 15f;
        [Range(-3f, -0.5f)] public float limiterCeilingDb = -1.0f;

        public VoxFxPreset CloneInMemory()
        {
            var copy = CreateInstance<VoxFxPreset>();
            CopyValues(this, copy);
            return copy;
        }

        public static void CopyValues(VoxFxPreset src, VoxFxPreset dst)
        {
            if (src == null || dst == null) return;
            dst.presetName = src.presetName;
            dst.targetSampleRate = src.targetSampleRate;
            dst.enableTrim = src.enableTrim;
            dst.trimThresholdDb = src.trimThresholdDb;
            dst.trimPaddingMs = src.trimPaddingMs;
            dst.enableBandPass = src.enableBandPass;
            dst.highPassHz = src.highPassHz;
            dst.lowPassHz = src.lowPassHz;
            dst.enableGrit = src.enableGrit;
            dst.downsampleFactor = src.downsampleFactor;
            dst.bitDepth = src.bitDepth;
            dst.gritMix = src.gritMix;
            dst.enableRingMod = src.enableRingMod;
            dst.ringModHz = src.ringModHz;
            dst.ringModMix = src.ringModMix;
            dst.enableSaturation = src.enableSaturation;
            dst.saturationDrive = src.saturationDrive;
            dst.enableEcho = src.enableEcho;
            dst.echoTap1Ms = src.echoTap1Ms;
            dst.echoTap1Gain = src.echoTap1Gain;
            dst.echoTap2Ms = src.echoTap2Ms;
            dst.echoTap2Gain = src.echoTap2Gain;
            dst.echoLowPassHz = src.echoLowPassHz;
            dst.enableReverb = src.enableReverb;
            dst.reverbFeedback = src.reverbFeedback;
            dst.reverbDamping = src.reverbDamping;
            dst.reverbWet = src.reverbWet;
            dst.reverbTailSec = src.reverbTailSec;
            dst.enableSquelch = src.enableSquelch;
            dst.squelchStartMs = src.squelchStartMs;
            dst.squelchEndMs = src.squelchEndMs;
            dst.squelchLevelDb = src.squelchLevelDb;
            dst.enableNormalize = src.enableNormalize;
            dst.normalizeTargetDb = src.normalizeTargetDb;
            dst.fadeMs = src.fadeMs;
            dst.limiterCeilingDb = src.limiterCeilingDb;
        }

        public static VoxFxPreset CreateClassicDefaults(VoxFxPreset target = null)
        {
            var p = target != null ? target : CreateInstance<VoxFxPreset>();
            p.presetName = "VOX Classic";
            p.targetSampleRate = 22050;
            p.enableTrim = true;
            p.trimThresholdDb = -45f;
            p.trimPaddingMs = 30f;
            p.enableBandPass = true;
            p.highPassHz = 320f;
            p.lowPassHz = 3200f;
            p.enableGrit = true;
            p.downsampleFactor = 2;
            p.bitDepth = 10;
            p.gritMix = 1.0f;
            p.enableRingMod = true;
            p.ringModHz = 70f;
            p.ringModMix = 0.30f;
            p.enableSaturation = true;
            p.saturationDrive = 1.6f;
            p.enableEcho = true;
            p.echoTap1Ms = 90f;
            p.echoTap1Gain = 0.28f;
            p.echoTap2Ms = 180f;
            p.echoTap2Gain = 0.14f;
            p.echoLowPassHz = 2500f;
            p.enableReverb = true;
            p.reverbFeedback = 0.78f;
            p.reverbDamping = 0.35f;
            p.reverbWet = 0.22f;
            p.reverbTailSec = 1.0f;
            p.enableSquelch = true;
            p.squelchStartMs = 70f;
            p.squelchEndMs = 40f;
            p.squelchLevelDb = -32f;
            p.enableNormalize = true;
            p.normalizeTargetDb = -1.5f;
            p.fadeMs = 15f;
            p.limiterCeilingDb = -1.0f;
            return p;
        }

        public static VoxFxPreset CreateHeavyEchoDefaults(VoxFxPreset target = null)
        {
            var p = CreateClassicDefaults(target);
            p.presetName = "Heavy Echo";
            p.reverbWet = 0.35f;
            p.echoTap1Gain = 0.40f;
            p.echoTap2Gain = 0.24f;
            p.ringModMix = 0.20f;
            return p;
        }

        public static VoxFxPreset CreateCleanRadioDefaults(VoxFxPreset target = null)
        {
            var p = CreateClassicDefaults(target);
            p.presetName = "Clean Radio";
            p.enableRingMod = false;
            p.ringModMix = 0f;
            p.enableGrit = false;
            p.downsampleFactor = 1;
            p.bitDepth = 16;
            p.gritMix = 0f;
            p.enableEcho = true;
            p.echoTap1Gain = 0.12f;
            p.echoTap2Gain = 0.05f;
            p.reverbWet = 0.10f;
            p.reverbTailSec = 0.65f;
            return p;
        }

        public static VoxFxPreset CreateStingDefaults(VoxFxPreset target = null)
        {
            var p = CreateClassicDefaults(target);
            p.presetName = "Sting";
            p.enableTrim = true;
            p.trimThresholdDb = -45f;
            p.trimPaddingMs = 20f;
            p.enableBandPass = false;
            p.enableGrit = false;
            p.downsampleFactor = 1;
            p.bitDepth = 16;
            p.gritMix = 0f;
            p.enableRingMod = false;
            p.ringModMix = 0f;
            p.enableSaturation = false;
            p.saturationDrive = 1.0f;
            p.enableEcho = false;
            p.echoTap1Gain = 0f;
            p.echoTap2Gain = 0f;
            p.enableReverb = true;
            p.reverbFeedback = 0.62f;
            p.reverbDamping = 0.45f;
            p.reverbWet = 0.08f;
            p.reverbTailSec = 0.35f;
            p.enableSquelch = false;
            p.enableNormalize = true;
            p.normalizeTargetDb = -1.5f;
            p.fadeMs = 10f;
            p.limiterCeilingDb = -1.0f;
            return p;
        }

        public string ComputeSignature()
        {
            var sb = new StringBuilder(256);
            void A(float v) => sb.Append(v.ToString("G6", CultureInfo.InvariantCulture)).Append('|');
            void B(bool v) => sb.Append(v ? '1' : '0').Append('|');
            sb.Append(targetSampleRate).Append('|');
            B(enableTrim); A(trimThresholdDb); A(trimPaddingMs);
            B(enableBandPass); A(highPassHz); A(lowPassHz);
            B(enableGrit); sb.Append(downsampleFactor).Append('|'); sb.Append(bitDepth).Append('|'); A(gritMix);
            B(enableRingMod); A(ringModHz); A(ringModMix);
            B(enableSaturation); A(saturationDrive);
            B(enableEcho); A(echoTap1Ms); A(echoTap1Gain); A(echoTap2Ms); A(echoTap2Gain); A(echoLowPassHz);
            B(enableReverb); A(reverbFeedback); A(reverbDamping); A(reverbWet); A(reverbTailSec);
            B(enableSquelch); A(squelchStartMs); A(squelchEndMs); A(squelchLevelDb);
            B(enableNormalize); A(normalizeTargetDb); A(fadeMs); A(limiterCeilingDb);
            return sb.ToString();
        }
    }
}
