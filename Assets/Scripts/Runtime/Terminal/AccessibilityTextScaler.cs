using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Accessibility hook attached to TerminalRoot that applies a larger-text multiplier across registered TMP labels.
    /// Avoids scanning the scene each frame by registering text components once on UI creation.
    /// </summary>
    public class AccessibilityTextScaler : MonoBehaviour
    {
        [SerializeField] private bool largerTextEnabled;
        [SerializeField] private float largerTextMultiplier = 1.15f;

        private readonly List<(TextMeshProUGUI tmp, float baseSize)> _entries = new List<(TextMeshProUGUI, float)>();

        public bool LargerTextEnabled => largerTextEnabled;

        public event Action<bool> OnAccessibilityChanged;

        public void RegisterText(TextMeshProUGUI tmp, float baseFontSize)
        {
            if (tmp == null) return;
            _entries.Add((tmp, baseFontSize));
            ApplyToEntry(tmp, baseFontSize);
        }

        public void SetLargerText(bool enabled)
        {
            if (largerTextEnabled == enabled) return;
            largerTextEnabled = enabled;
            RefreshAll();
            OnAccessibilityChanged?.Invoke(largerTextEnabled);
        }

        public void RefreshAll()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.tmp != null) ApplyToEntry(e.tmp, e.baseSize);
            }
        }

        private void ApplyToEntry(TextMeshProUGUI tmp, float baseSize)
        {
            tmp.fontSize = largerTextEnabled ? baseSize * largerTextMultiplier : baseSize;
        }
    }
}
