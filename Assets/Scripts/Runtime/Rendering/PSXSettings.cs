using System;
using UnityEngine;

namespace Outpost.Runtime.Rendering
{
    public enum PSXResolutionPreset
    {
        Low = 0,     // 320x180
        Medium = 1,  // 480x270
        High = 2,    // 640x360
        Off = 3      // Native resolution (PSX disabled or native RT bypass)
    }

    /// <summary>
    /// Shared ScriptableObject holding all PSX look parameters (resolution, vertex snap, affine UVs,
    /// subtle Bayer dithering, color quantization, optional grain, and Mars exterior fog).
    /// Tunable in Inspector and at runtime via Pause Menu / Backquote Debug Panel.
    /// </summary>
    [CreateAssetMenu(fileName = "PSXSettings", menuName = "Outpost/PSX Settings", order = 7)]
    public class PSXSettings : ScriptableObject
    {
        private const string PrefEnabled = "Outpost_PSX_Enabled";
        private const string PrefPreset = "Outpost_PSX_Preset";

        [Header("Master Toggle & Target Render Resolution")]
        public bool enabled = false;
        public PSXResolutionPreset preset = PSXResolutionPreset.Off;
        [Min(64)] public int renderWidth = 480;
        [Min(36)] public int renderHeight = 270;

        [Header("PS1 Geometry & Texture Warping")]
        [Range(60f, 480f)] public float vertexSnapResolution = 160f;
        [Range(0f, 1f)] public float affineAmount = 0.65f;

        [Header("Color Quantization, Subtle Dithering & Optional Grain")]
        [Range(4, 64)] public int colorLevels = 24;
        [Range(0f, 1f)] public float ditherStrength = 0.25f;
        [Range(0f, 0.35f)] public float grainAmount = 0f;

        [Header("Atmospheric Fog (Mars Exterior & Depth Cue)")]
        public Color fogColor = new Color(0.76f, 0.38f, 0.22f, 1f);
        [Range(0f, 0.15f)] public float fogDensity = 0.018f;

        public event Action OnSettingsChanged;

        private static readonly int PropPsxEnabled = Shader.PropertyToID("_PSX_Enabled");
        private static readonly int PropVertexSnapRes = Shader.PropertyToID("_PSX_VertexSnapRes");
        private static readonly int PropAffineAmount = Shader.PropertyToID("_PSX_AffineAmount");
        private static readonly int PropColorLevels = Shader.PropertyToID("_PSX_ColorLevels");
        private static readonly int PropDitherStrength = Shader.PropertyToID("_PSX_DitherStrength");
        private static readonly int PropGrainAmount = Shader.PropertyToID("_PSX_GrainAmount");
        private static readonly int PropReduceFlicker = Shader.PropertyToID("_PSX_ReduceFlicker");
        private static readonly int PropFogColor = Shader.PropertyToID("_PSX_FogColor");
        private static readonly int PropFogDensity = Shader.PropertyToID("_PSX_FogDensity");

        public void SetEnabled(bool isEnabled, bool savePrefs = true)
        {
            enabled = isEnabled;
            if (enabled && preset == PSXResolutionPreset.Off)
            {
                preset = PSXResolutionPreset.Medium;
                renderWidth = 480;
                renderHeight = 270;
            }
            if (savePrefs && Application.isPlaying)
                SaveToPlayerPrefs();
            OnSettingsChanged?.Invoke();
        }

        public void ApplyPreset(PSXResolutionPreset nextPreset, bool savePrefs = true)
        {
            preset = nextPreset;
            switch (nextPreset)
            {
                case PSXResolutionPreset.Low:
                    enabled = true;
                    renderWidth = 320;
                    renderHeight = 180;
                    break;
                case PSXResolutionPreset.Medium:
                    enabled = true;
                    renderWidth = 480;
                    renderHeight = 270;
                    break;
                case PSXResolutionPreset.High:
                    enabled = true;
                    renderWidth = 640;
                    renderHeight = 360;
                    break;
                case PSXResolutionPreset.Off:
                    enabled = false;
                    break;
            }

            if (savePrefs && Application.isPlaying)
                SaveToPlayerPrefs();
            OnSettingsChanged?.Invoke();
        }

        public void SetCustomResolutionHeight(int height)
        {
            int h = Mathf.Clamp(height, 120, 720);
            int w = Mathf.Max(160, Mathf.RoundToInt(h * (16f / 9f)));
            renderWidth = w;
            renderHeight = h;
            OnSettingsChanged?.Invoke();
        }

        public string GetPresetLabel()
        {
            if (!enabled || preset == PSXResolutionPreset.Off) return "NATIVE";
            switch (preset)
            {
                case PSXResolutionPreset.Low: return "LOW (320x180)";
                case PSXResolutionPreset.Medium: return "MED (480x270)";
                case PSXResolutionPreset.High: return "HIGH (640x360)";
                default: return $"{renderWidth}x{renderHeight}";
            }
        }

        public void LoadFromPlayerPrefs()
        {
            if (PlayerPrefs.HasKey(PrefPreset))
            {
                int raw = PlayerPrefs.GetInt(PrefPreset, (int)preset);
                if (Enum.IsDefined(typeof(PSXResolutionPreset), raw))
                    ApplyPreset((PSXResolutionPreset)raw, savePrefs: false);
            }
            if (PlayerPrefs.HasKey(PrefEnabled))
            {
                bool en = PlayerPrefs.GetInt(PrefEnabled, enabled ? 1 : 0) == 1;
                SetEnabled(en, savePrefs: false);
            }
        }

        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetInt(PrefEnabled, enabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefPreset, (int)preset);
            PlayerPrefs.Save();
        }

        public void ApplyGlobals(bool reduceFlicker)
        {
            Shader.SetGlobalFloat(PropPsxEnabled, enabled ? 1f : 0f);
            Shader.SetGlobalFloat(PropVertexSnapRes, Mathf.Max(32f, vertexSnapResolution));
            Shader.SetGlobalFloat(PropAffineAmount, enabled ? Mathf.Clamp01(affineAmount) : 0f);
            Shader.SetGlobalFloat(PropColorLevels, Mathf.Clamp(colorLevels, 4, 64));
            Shader.SetGlobalFloat(PropDitherStrength, enabled ? Mathf.Clamp01(ditherStrength) : 0f);
            Shader.SetGlobalFloat(PropGrainAmount, (enabled && !reduceFlicker) ? Mathf.Max(0f, grainAmount) : 0f);
            Shader.SetGlobalFloat(PropReduceFlicker, reduceFlicker ? 1f : 0f);
            Shader.SetGlobalColor(PropFogColor, fogColor);
            Shader.SetGlobalFloat(PropFogDensity, Mathf.Max(0f, fogDensity));
        }

        public void NotifyChanged()
        {
            OnSettingsChanged?.Invoke();
        }
    }
}
