using System;
using System.Collections.Generic;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// ScriptableObject storing one educational NASA/ESA/USGS fact linked to an event, constant, and SourcesDatabase IDs.
    /// Text should stay at or below 25 words for quick student readability in the Debrief screen.
    /// </summary>
    [CreateAssetMenu(fileName = "FactCard", menuName = "Outpost/Fact Card", order = 3)]
    public class FactCard : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea(2, 4)]
        public string text;
        public string sourceId;
        public List<string> sourceIds = new List<string>();
        public string relatedEventId;
        public string relatedConstantId;

        [Header("Localization Keys")]
        public string titleKey;
        public string textKey;

        public string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(titleKey))
            {
                string loc = Localization.Get(titleKey);
                if (!string.IsNullOrEmpty(loc) && !string.Equals(loc, titleKey, StringComparison.Ordinal))
                    return loc;
            }
            return title ?? string.Empty;
        }

        public string ResolveText()
        {
            if (!string.IsNullOrEmpty(textKey))
            {
                string loc = Localization.Get(textKey);
                if (!string.IsNullOrEmpty(loc) && !string.Equals(loc, textKey, StringComparison.Ordinal))
                    return loc;
            }
            return text ?? string.Empty;
        }

        public IReadOnlyList<string> GetSourceIds()
        {
            if (sourceIds != null && sourceIds.Count > 0)
                return sourceIds;

            if (!string.IsNullOrWhiteSpace(sourceId))
            {
                string[] parts = sourceId.Split(',');
                var list = new List<string>(parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    string trimmed = parts[i].Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        list.Add(trimmed);
                }
                return list;
            }

            return Array.Empty<string>();
        }
    }
}
