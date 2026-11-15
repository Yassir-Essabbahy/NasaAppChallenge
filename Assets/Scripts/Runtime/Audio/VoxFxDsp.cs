using System;
using UnityEngine;

namespace Outpost.Runtime.Audio
{
    /// <summary>
    /// Pure C# offline VOX DSP chain (zero native plugins, zero Unity AudioMixer/AudioFilter dependencies).
    /// Produces 22050 Hz mono float[] PCM suitable for deterministic baking and live Editor auditioning.
    /// </summary>
    public static class VoxFxDsp
    {
        private struct Biquad
        {
            private double _b0, _b1, _b2, _a1, _a2;
            private double _z1, _z2;

            public static Biquad CreateHighPass(double sampleRate, double cutoffHz, double q)
            {
                double fc = Math.Max(20.0, Math.Min(cutoffHz, sampleRate * 0.45));
                double w0 = 2.0 * Math.PI * fc / sampleRate;
                double cosW0 = Math.Cos(w0);
                double alpha = Math.Sin(w0) / (2.0 * Math.Max(0.1, q));
                double a0 = 1.0 + alpha;
                return new Biquad
                {
                    _b0 = (1.0 + cosW0) * 0.5 / a0,
                    _b1 = -(1.0 + cosW0) / a0,
                    _b2 = (1.0 + cosW0) * 0.5 / a0,
                    _a1 = (-2.0 * cosW0) / a0,
                    _a2 = (1.0 - alpha) / a0
                };
            }

            public static Biquad CreateLowPass(double sampleRate, double cutoffHz, double q)
            {
                double fc = Math.Max(40.0, Math.Min(cutoffHz, sampleRate * 0.48));
                double w0 = 2.0 * Math.PI * fc / sampleRate;
                double cosW0 = Math.Cos(w0);
                double alpha = Math.Sin(w0) / (2.0 * Math.Max(0.1, q));
                double a0 = 1.0 + alpha;
                return new Biquad
                {
                    _b0 = (1.0 - cosW0) * 0.5 / a0,
                    _b1 = (1.0 - cosW0) / a0,
                    _b2 = (1.0 - cosW0) * 0.5 / a0,
                    _a1 = (-2.0 * cosW0) / a0,
                    _a2 = (1.0 - alpha) / a0
                };
            }

            public float Process(float x)
            {
                double y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return (float)y;
            }
        }

        private sealed class DampedComb
        {
            private readonly float[] _buf;
            private int _idx;
            private float _lpState;

            public DampedComb(int delaySamples)
            {
                _buf = new float[Math.Max(1, delaySamples)];
            }

            public float Process(float input, float feedback, float damping)
            {
                float y = _buf[_idx];
                _lpState = y * (1f - damping) + _lpState * damping;
                _buf[_idx] = input + _lpState * feedback;
                _idx++;
                if (_idx >= _buf.Length) _idx = 0;
                return y;
            }
        }

        private sealed class AllPass
        {
            private readonly float[] _buf;
            private int _idx;

            public AllPass(int delaySamples)
            {
                _buf = new float[Math.Max(1, delaySamples)];
            }

            public float Process(float input, float gain)
            {
                float buffered = _buf[_idx];
                float v = input + buffered * gain;
                _buf[_idx] = v;
                _idx++;
                if (_idx >= _buf.Length) _idx = 0;
                return buffered - v * gain;
            }
        }

        public static float DbToLinear(float db) => Mathf.Pow(10f, db / 20f);

        public static float LinearToDb(float linear) =>
            linear <= 1e-6f ? -120f : 20f * Mathf.Log10(linear);

        /// <summary>
        /// Runs the complete preset DSP chain on interleaved source PCM samples and returns mono PCM at preset.targetSampleRate.
        /// </summary>
        public static float[] Process(float[] sourceSamples, int sourceChannels, int sourceSampleRate, VoxFxPreset preset, out int outSampleRate)
        {
            VoxFxPreset p = preset != null ? preset : VoxFxPreset.CreateClassicDefaults();
            outSampleRate = Mathf.Clamp(p.targetSampleRate, 8000, 44100);

            if (sourceSamples == null || sourceSamples.Length == 0 || sourceChannels <= 0 || sourceSampleRate <= 0)
                return new float[outSampleRate / 10]; // 100ms safe silence fallback

            // 1. Mono mix + resample
            float[] buf = MixToMonoAndResample(sourceSamples, sourceChannels, sourceSampleRate, outSampleRate);

            // 2. Silence trim
            if (p.enableTrim)
                buf = TrimSilence(buf, outSampleRate, p.trimThresholdDb, p.trimPaddingMs);

            // 3. Telephone band (4th-order HPF + 4th-order LPF via two cascaded biquads each)
            if (p.enableBandPass)
                ApplyTelephoneBand(buf, outSampleRate, p.highPassHz, p.lowPassHz);

            // 4. Digital grit (S&H downsample + bit depth crush)
            if (p.enableGrit && (p.downsampleFactor > 1 || p.bitDepth < 16) && p.gritMix > 0.001f)
                ApplyDigitalGrit(buf, p.downsampleFactor, p.bitDepth, p.gritMix);

            // 5. Robot ring modulation
            if (p.enableRingMod && p.ringModMix > 0.001f)
                ApplyRingModulation(buf, outSampleRate, p.ringModHz, p.ringModMix);

            // 6. Soft saturation
            if (p.enableSaturation && p.saturationDrive > 1.001f)
                ApplySoftSaturation(buf, p.saturationDrive);

            // 7. Echo taps with low-pass dark repeats
            if (p.enableEcho && (p.echoTap1Gain > 0.001f || p.echoTap2Gain > 0.001f))
                buf = ApplyEcho(buf, outSampleRate, p.echoTap1Ms, p.echoTap1Gain, p.echoTap2Ms, p.echoTap2Gain, p.echoLowPassHz);

            // 8. Schroeder hall reverb with extended tail
            if (p.enableReverb && p.reverbWet > 0.001f)
                buf = ApplySchroederReverb(buf, outSampleRate, p.reverbFeedback, p.reverbDamping, p.reverbWet, p.reverbTailSec);

            // 9. Radio squelch bursts (start burst + end burst & soft click)
            if (p.enableSquelch && (p.squelchStartMs > 0.5f || p.squelchEndMs > 0.5f))
                buf = ApplyRadioSquelch(buf, outSampleRate, p.squelchStartMs, p.squelchEndMs, p.squelchLevelDb);

            // 10. Normalize to target peak (-1.5 dBFS), 15ms fades, hard limiter & NaN/Inf sanitization
            NormalizeFadeAndLimit(buf, outSampleRate, p.normalizeTargetDb, p.fadeMs, p.limiterCeilingDb, p.enableNormalize);
            return buf;
        }

        public static float[] MixToMonoAndResample(float[] sourceSamples, int channels, int sourceRate, int targetRate)
        {
            int frameCount = sourceSamples.Length / Math.Max(1, channels);
            if (frameCount <= 0)
                return Array.Empty<float>();

            float[] mono = new float[frameCount];
            float invCh = 1f / channels;
            for (int i = 0; i < frameCount; i++)
            {
                float sum = 0f;
                int baseIdx = i * channels;
                for (int c = 0; c < channels; c++)
                {
                    float s = sourceSamples[baseIdx + c];
                    if (float.IsFinite(s)) sum += s;
                }
                mono[i] = sum * invCh;
            }

            if (sourceRate == targetRate)
                return mono;

            // Gentle anti-aliasing LPF before downsampling
            if (targetRate < sourceRate)
            {
                double nyquistCutoff = targetRate * 0.45;
                var aa = Biquad.CreateLowPass(sourceRate, nyquistCutoff, 0.7071);
                for (int i = 0; i < mono.Length; i++)
                    mono[i] = aa.Process(mono[i]);
            }

            double ratio = (double)sourceRate / targetRate;
            int outLen = Math.Max(1, (int)Math.Round(frameCount / ratio));
            float[] resampled = new float[outLen];
            for (int i = 0; i < outLen; i++)
            {
                double srcPos = i * ratio;
                int i0 = (int)srcPos;
                if (i0 >= frameCount - 1)
                {
                    resampled[i] = mono[frameCount - 1];
                    continue;
                }
                double frac = srcPos - i0;
                resampled[i] = (float)(mono[i0] * (1.0 - frac) + mono[i0 + 1] * frac);
            }
            return resampled;
        }

        public static float[] TrimSilence(float[] samples, int sampleRate, float thresholdDb, float paddingMs)
        {
            if (samples == null || samples.Length == 0)
                return Array.Empty<float>();

            float thresh = DbToLinear(thresholdDb);
            int first = -1;
            int last = -1;

            for (int i = 0; i < samples.Length; i++)
            {
                if (Mathf.Abs(samples[i]) >= thresh)
                {
                    first = i;
                    break;
                }
            }

            if (first < 0)
                return (float[])samples.Clone();

            for (int i = samples.Length - 1; i >= first; i--)
            {
                if (Mathf.Abs(samples[i]) >= thresh)
                {
                    last = i;
                    break;
                }
            }

            int padSamples = Mathf.RoundToInt((paddingMs * 0.001f) * sampleRate);
            int start = Math.Max(0, first - padSamples);
            int end = Math.Min(samples.Length - 1, last + padSamples);
            int len = end - start + 1;
            if (len <= 0 || len >= samples.Length)
                return (float[])samples.Clone();

            float[] trimmed = new float[len];
            Array.Copy(samples, start, trimmed, 0, len);
            return trimmed;
        }

        public static void ApplyTelephoneBand(float[] samples, int sampleRate, float hpHz, float lpHz)
        {
            if (samples == null || samples.Length == 0) return;
            // 4th-order Butterworth cascade: Q1 = 0.5411961, Q2 = 1.3065630
            const double q1 = 0.541196100146197;
            const double q2 = 1.3065629648763766;
            var hp1 = Biquad.CreateHighPass(sampleRate, hpHz, q1);
            var hp2 = Biquad.CreateHighPass(sampleRate, hpHz, q2);
            var lp1 = Biquad.CreateLowPass(sampleRate, lpHz, q1);
            var lp2 = Biquad.CreateLowPass(sampleRate, lpHz, q2);

            for (int i = 0; i < samples.Length; i++)
            {
                float s = hp1.Process(samples[i]);
                s = hp2.Process(s);
                s = lp1.Process(s);
                s = lp2.Process(s);
                samples[i] = s;
            }
        }

        public static void ApplyDigitalGrit(float[] samples, int downsampleFactor, int bitDepth, float mix)
        {
            if (samples == null || samples.Length == 0) return;
            int hold = Mathf.Max(1, downsampleFactor);
            int clampedBits = Mathf.Clamp(bitDepth, 4, 16);
            float levels = (1 << (clampedBits - 1)) - 1;
            float invLevels = 1f / levels;
            float clampedMix = Mathf.Clamp01(mix);

            float held = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float dry = samples[i];
                if (i % hold == 0)
                    held = dry;
                float q = Mathf.Round(Mathf.Clamp(held, -1f, 1f) * levels) * invLevels;
                samples[i] = Mathf.Lerp(dry, q, clampedMix);
            }
        }

        public static void ApplyRingModulation(float[] samples, int sampleRate, float modHz, float mix)
        {
            if (samples == null || samples.Length == 0) return;
            float clampedMix = Mathf.Clamp01(mix);
            double phaseStep = 2.0 * Math.PI * Mathf.Max(1f, modHz) / sampleRate;
            for (int i = 0; i < samples.Length; i++)
            {
                float dry = samples[i];
                float mod = (float)Math.Sin(i * phaseStep);
                float wet = dry * mod;
                samples[i] = Mathf.Lerp(dry, wet, clampedMix);
            }
        }

        public static void ApplySoftSaturation(float[] samples, float drive)
        {
            if (samples == null || samples.Length == 0) return;
            double d = Math.Max(1.0, drive);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)Math.Tanh(samples[i] * d);
        }

        public static float[] ApplyEcho(float[] samples, int sampleRate, float tap1Ms, float tap1Gain, float tap2Ms, float tap2Gain, float echoLowPassHz)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();
            int d1 = Math.Max(1, Mathf.RoundToInt(tap1Ms * 0.001f * sampleRate));
            int d2 = Math.Max(d1 + 1, Mathf.RoundToInt(tap2Ms * 0.001f * sampleRate));
            int tailPad = Math.Min(sampleRate / 2, Mathf.RoundToInt(d2 * 0.75f));
            float[] output = new float[samples.Length + tailPad];

            var echoLp = Biquad.CreateLowPass(sampleRate, echoLowPassHz, 0.7071);
            for (int i = 0; i < output.Length; i++)
            {
                float dry = i < samples.Length ? samples[i] : 0f;
                float s1 = (i - d1 >= 0 && i - d1 < samples.Length) ? samples[i - d1] * tap1Gain : 0f;
                float s2 = (i - d2 >= 0 && i - d2 < samples.Length) ? samples[i - d2] * tap2Gain : 0f;
                float darkEcho = echoLp.Process(s1 + s2);
                output[i] = dry + darkEcho;
            }
            return output;
        }

        public static float[] ApplySchroederReverb(float[] samples, int sampleRate, float feedback, float damping, float wet, float tailSec)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<float>();
            int tailSamples = Mathf.Clamp(Mathf.RoundToInt(tailSec * sampleRate), 0, Mathf.RoundToInt(1.5f * sampleRate));
            float[] output = new float[samples.Length + tailSamples];

            // 4 parallel combs (29.7, 37.1, 41.1, 43.7 ms) -> 2 series all-pass (5.0, 1.7 ms @ 0.7)
            var c1 = new DampedComb(Mathf.RoundToInt(0.0297f * sampleRate));
            var c2 = new DampedComb(Mathf.RoundToInt(0.0371f * sampleRate));
            var c3 = new DampedComb(Mathf.RoundToInt(0.0411f * sampleRate));
            var c4 = new DampedComb(Mathf.RoundToInt(0.0437f * sampleRate));
            var ap1 = new AllPass(Mathf.RoundToInt(0.0050f * sampleRate));
            var ap2 = new AllPass(Mathf.RoundToInt(0.0017f * sampleRate));

            float fb = Mathf.Clamp(feedback, 0f, 0.94f);
            float damp = Mathf.Clamp(damping, 0.02f, 0.90f);
            float clampedWet = Mathf.Clamp01(wet);
            float dryGain = 1f - clampedWet * 0.25f;

            for (int i = 0; i < output.Length; i++)
            {
                float dry = i < samples.Length ? samples[i] : 0f;
                float combSum = (c1.Process(dry, fb, damp)
                               + c2.Process(dry, fb, damp)
                               + c3.Process(dry, fb, damp)
                               + c4.Process(dry, fb, damp)) * 0.25f;
                float diffused = ap2.Process(ap1.Process(combSum, 0.7f), 0.7f);

                // Smooth fade out on the added tail so zero truncation clicks occur
                float tailEnv = 1f;
                if (i >= samples.Length && tailSamples > 0)
                {
                    float u = (i - samples.Length) / (float)tailSamples;
                    tailEnv = 1f - u * u;
                }

                output[i] = dry * dryGain + diffused * clampedWet * tailEnv;
            }
            return output;
        }

        public static float[] ApplyRadioSquelch(float[] samples, int sampleRate, float startMs, float endMs, float levelDb)
        {
            if (samples == null) samples = Array.Empty<float>();
            int preBurst = Mathf.Max(0, Mathf.RoundToInt(startMs * 0.001f * sampleRate));
            int postBurst = Mathf.Max(0, Mathf.RoundToInt(endMs * 0.001f * sampleRate));
            int gapBeforePost = Mathf.RoundToInt(0.012f * sampleRate);

            float[] output = new float[preBurst + samples.Length + gapBeforePost + postBurst];
            if (samples.Length > 0)
                Array.Copy(samples, 0, output, preBurst, samples.Length);

            float amp = DbToLinear(levelDb);
            uint rng = 0x51A79E13u;
            var hp = Biquad.CreateHighPass(sampleRate, 450.0, 0.7071);
            var lp = Biquad.CreateLowPass(sampleRate, 2800.0, 0.7071);

            float NextBandNoise()
            {
                rng = rng * 1664525u + 1013904223u;
                float white = ((rng >> 16) / 65535f) * 2f - 1f;
                return lp.Process(hp.Process(white));
            }

            // Start squelch burst (70 ms default at ~-32 dB)
            for (int i = 0; i < preBurst; i++)
            {
                float u = i / (float)Math.Max(1, preBurst);
                float env = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                output[i] += NextBandNoise() * amp * (0.4f + 0.6f * env);
            }

            // End squelch burst (40 ms default + soft PTT release click)
            int endStart = preBurst + samples.Length + gapBeforePost;
            for (int i = 0; i < postBurst; i++)
            {
                float u = i / (float)Math.Max(1, postBurst);
                float noiseEnv = 1f - u * 0.75f;
                float click = i < sampleRate * 0.008f
                    ? Mathf.Exp(-i / (sampleRate * 0.0018f)) * Mathf.Sin(2f * Mathf.PI * 950f * (i / (float)sampleRate)) * 0.85f
                    : 0f;
                output[endStart + i] += NextBandNoise() * amp * noiseEnv + click * amp * 1.35f;
            }

            return output;
        }

        public static void NormalizeFadeAndLimit(float[] samples, int sampleRate, float normalizeTargetDb, float fadeMs, float limiterCeilingDb, bool enableNormalize)
        {
            if (samples == null || samples.Length == 0) return;

            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                if (!float.IsFinite(s))
                {
                    samples[i] = 0f;
                    continue;
                }
                float a = Mathf.Abs(s);
                if (a > peak) peak = a;
            }

            if (enableNormalize && peak > 1e-5f)
            {
                float targetPeak = DbToLinear(normalizeTargetDb);
                float gain = targetPeak / peak;
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= gain;
            }

            int fadeSamples = Mathf.Min(samples.Length / 3, Mathf.Max(0, Mathf.RoundToInt(fadeMs * 0.001f * sampleRate)));
            for (int i = 0; i < fadeSamples; i++)
            {
                float w = (i + 0.5f) / fadeSamples;
                samples[i] *= w;
                samples[samples.Length - 1 - i] *= w;
            }

            float ceiling = DbToLinear(limiterCeilingDb);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Clamp(samples[i], -ceiling, ceiling);
        }

        public static AudioClip CreateRuntimeClip(string name, float[] monoSamples, int sampleRate)
        {
            if (monoSamples == null || monoSamples.Length == 0)
                monoSamples = new float[128];
            var clip = AudioClip.Create(name, monoSamples.Length, 1, sampleRate, false);
            clip.SetData(monoSamples, 0);
            return clip;
        }
    }
}
