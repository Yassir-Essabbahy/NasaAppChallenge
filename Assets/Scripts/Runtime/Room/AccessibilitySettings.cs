using System;
using Outpost.Runtime.Terminal;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Central accessibility configuration component for Milestone 3+.
    /// Controls reduce-motion / reduce-flicker (disables head bob, light flicker, intro grain/jitter/shake,
    /// optional film grain) and larger UI text scaling. Persisted in PlayerPrefs.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        private const string PrefReduceMotion = "Outpost_ReduceMotion";
        private const string PrefReduceFlicker = "Outpost_ReduceFlicker";
        private const string PrefLargerText = "Outpost_LargerText";

        [SerializeField] private bool reduceMotion;
        [SerializeField] private bool reduceFlicker;
        [SerializeField] private bool largerText;
        [SerializeField] private AccessibilityTextScaler textScaler;

        public bool ReduceMotion => reduceMotion || reduceFlicker;
        public bool ReduceFlicker => reduceFlicker || reduceMotion;
        public bool LargerText => largerText;

        public event Action OnSettingsChanged;

        private void Awake()
        {
            reduceMotion = PlayerPrefs.GetInt(PrefReduceMotion, reduceMotion ? 1 : 0) == 1;
            reduceFlicker = PlayerPrefs.GetInt(PrefReduceFlicker, reduceFlicker ? 1 : 0) == 1;
            largerText = PlayerPrefs.GetInt(PrefLargerText, largerText ? 1 : 0) == 1;
            ApplySettings();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplySettings();
        }

        public void BindTextScaler(AccessibilityTextScaler scaler)
        {
            textScaler = scaler;
            ApplySettings();
        }

        public void SetReduceMotion(bool enabled)
        {
            if (reduceMotion == enabled && reduceFlicker == enabled) return;
            reduceMotion = enabled;
            reduceFlicker = enabled;
            PlayerPrefs.SetInt(PrefReduceMotion, enabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefReduceFlicker, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnSettingsChanged?.Invoke();
        }

        public void SetReduceFlicker(bool enabled)
        {
            SetReduceMotion(enabled);
        }

        public void SetLargerText(bool enabled)
        {
            if (largerText == enabled && (textScaler == null || textScaler.LargerTextEnabled == enabled))
                return;
            largerText = enabled;
            PlayerPrefs.SetInt(PrefLargerText, enabled ? 1 : 0);
            PlayerPrefs.Save();
            if (textScaler != null)
                textScaler.SetLargerText(enabled);
            OnSettingsChanged?.Invoke();
        }

        private void ApplySettings()
        {
            if (textScaler != null)
                textScaler.SetLargerText(largerText);
        }
    }
}
