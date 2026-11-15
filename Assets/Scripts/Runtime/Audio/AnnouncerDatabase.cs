using System;
using System.Collections.Generic;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Audio
{
    [Serializable]
    public class AnnouncerLine
    {
        public string id;
        public bool alertSting;
        public string subtitleKey;
        public string sourceId;
        public List<string> sourceIds = new List<string>();
        public AudioClip originalClip;
        public AudioClip processedClip;

        public AudioClip GetPlaybackClip() => processedClip != null ? processedClip : originalClip;

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

    public static class AnnouncerLocalization
    {
        private static readonly Dictionary<string, string> DefaultSubtitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "vox.haz_asteroid_c", "Warning. Asteroid debris detected, sector C. No impact expected." },
            { "vox.haz_meteoroid", "Caution. Meteoroid activity near the outpost. No action required." },
            { "vox.haz_seismic", "Warning. Minor marsquake detected. Structural check in progress." },
            { "vox.n_systems", "Notice. All systems operating within normal limits." },
            { "vox.n_samples", "Notice. Sample analysis complete. Results sent to Earth." },
            { "vox.r_suit", "Reminder. Check your suit seals before every outside activity." },
            { "vox.f_sol", "Fact. A day on Mars is called a sol." },
            { "vox.f_gravity", "Fact. Gravity on Mars is about thirty-eight percent of gravity on Earth." },
            { "vox.f_moons", "Fact. Mars has two small moons, Phobos and Deimos." },
            { "vox.f_cold", "Fact. Mars is very cold. The average surface temperature is about minus sixty degrees Celsius." },
            { "vox.f_signal", "Fact. Messages from Earth take minutes to reach Mars." }
        };

        public static string GetSubtitle(string lineId, string subtitleKey)
        {
            if (string.Equals(lineId, "vox_welcome", StringComparison.OrdinalIgnoreCase))
                return Localization.Get("vox_welcome");
            string key = string.IsNullOrWhiteSpace(subtitleKey) ? ("vox." + lineId) : subtitleKey;
            string simValue = Localization.Get(key);
            if (!string.IsNullOrEmpty(simValue) && !string.Equals(simValue, key, StringComparison.Ordinal))
                return simValue;

            if (DefaultSubtitles.TryGetValue(key, out string fallback))
                return fallback;

            string byIdKey = "vox." + lineId;
            if (DefaultSubtitles.TryGetValue(byIdKey, out string byIdFallback))
                return byIdFallback;

            return lineId;
        }
    }

    [CreateAssetMenu(menuName = "Outpost/Audio/Announcer Database", fileName = "AnnouncerDatabase")]
    public class AnnouncerDatabase : ScriptableObject
    {
        [Header("Warning Sting & Stitched Welcome Clips")]
        public AudioClip alertStingOriginal;
        public AudioClip alertStingProcessed;
        public AudioClip welcomeVoiceProcessed;

        [Header("Scheduled & Debug Announcer Lines")]
        public List<AnnouncerLine> lines = new List<AnnouncerLine>();

        public static readonly (string id, bool alertSting, string subtitleKey, string[] sourceIds)[] CanonicalLines =
        {
            ("haz_asteroid_c", true, "vox.haz_asteroid_c", Array.Empty<string>()),
            ("haz_meteoroid", true, "vox.haz_meteoroid", Array.Empty<string>()),
            ("haz_seismic", true, "vox.haz_seismic", Array.Empty<string>()),
            ("n_systems", false, "vox.n_systems", Array.Empty<string>()),
            ("n_samples", false, "vox.n_samples", Array.Empty<string>()),
            ("r_suit", false, "vox.r_suit", Array.Empty<string>()),
            ("f_sol", false, "vox.f_sol", new[] { "nasa-mars-fact-sheet" }),
            ("f_gravity", false, "vox.f_gravity", new[] { "esa-mars-facts" }),
            ("f_moons", false, "vox.f_moons", new[] { "esa-mars-facts" }),
            ("f_cold", false, "vox.f_cold", new[] { "esa-meet-mars", "nasa-mars-fact-sheet" }),
            ("f_signal", false, "vox.f_signal", new[] { "esa-light-time" })
        };

        public AudioClip GetStingPlaybackClip() => alertStingProcessed != null ? alertStingProcessed : alertStingOriginal;

        public AnnouncerLine FindLine(string id)
        {
            if (lines == null || string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null && string.Equals(lines[i].id, id, StringComparison.OrdinalIgnoreCase))
                    return lines[i];
            }
            return null;
        }

        public bool EnsureDefaultLines(bool onlyAddMissing = true)
        {
            if (lines == null)
                lines = new List<AnnouncerLine>();

            bool changed = false;
            if (!onlyAddMissing)
            {
                lines.Clear();
                changed = true;
            }

            foreach (var entry in CanonicalLines)
            {
                var existing = FindLine(entry.id);
                if (existing == null)
                {
                    var line = new AnnouncerLine
                    {
                        id = entry.id,
                        alertSting = entry.alertSting,
                        subtitleKey = entry.subtitleKey
                    };
                    ApplyCanonicalSources(line, entry.sourceIds);
                    lines.Add(line);
                    changed = true;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(existing.subtitleKey))
                    {
                        existing.subtitleKey = entry.subtitleKey;
                        changed = true;
                    }
                    if (ApplyCanonicalSources(existing, entry.sourceIds))
                        changed = true;
                }
            }
            return changed;
        }

        private static bool ApplyCanonicalSources(AnnouncerLine line, string[] canonicalIds)
        {
            if (line == null || canonicalIds == null) return false;
            bool changed = false;
            if (line.sourceIds == null)
            {
                line.sourceIds = new List<string>();
                changed = true;
            }

            string joined = string.Join(", ", canonicalIds);
            if (!string.Equals(line.sourceId ?? string.Empty, joined, StringComparison.Ordinal))
            {
                line.sourceId = joined;
                changed = true;
            }

            if (line.sourceIds.Count != canonicalIds.Length)
            {
                line.sourceIds.Clear();
                line.sourceIds.AddRange(canonicalIds);
                changed = true;
            }
            else
            {
                for (int i = 0; i < canonicalIds.Length; i++)
                {
                    if (!string.Equals(line.sourceIds[i], canonicalIds[i], StringComparison.Ordinal))
                    {
                        line.sourceIds[i] = canonicalIds[i];
                        changed = true;
                    }
                }
            }
            return changed;
        }

        private void Reset()
        {
            EnsureDefaultLines(onlyAddMissing: false);
        }
    }
}
