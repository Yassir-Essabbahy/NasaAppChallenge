using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// ScriptableObject mapping logical icon IDs (e.g. "Icon_Bolt", "Icon_Oxygen") to Sprite assets.
    /// </summary>
    [CreateAssetMenu(fileName = "IconLibrary", menuName = "Outpost/Icon Library")]
    public class IconLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        private Dictionary<string, Sprite> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public Sprite GetSprite(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            EnsureLookup();
            return _lookup.TryGetValue(id, out Sprite s) ? s : null;
        }

        public void SetEntries(List<Entry> newEntries)
        {
            entries = newEntries ?? new List<Entry>();
            RebuildLookup();
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void EnsureLookup()
        {
            if (_lookup == null)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].id;
                Sprite spr = entries[i].sprite;
                if (!string.IsNullOrEmpty(key) && spr != null)
                    _lookup[key] = spr;
            }
        }
    }
}
