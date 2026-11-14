using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    [Serializable]
    public class GlossaryEntry
    {
        public string Id;
        public string TermKey;
        public string DefinitionKey;
        public string SourceId;
    }

    /// <summary>
    /// Kid-friendly glossary of Mars outpost & spaceflight terms, optionally linked to NASA SourcesDatabase IDs.
    /// Editable in Inspector or via Assets/Resources/GlossaryDatabase.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "GlossaryDatabase", menuName = "Outpost/Glossary Database", order = 5)]
    public class GlossaryDatabase : ScriptableObject
    {
        public List<GlossaryEntry> Entries = new List<GlossaryEntry>();

        public void PopulateDefaults()
        {
            Entries = new List<GlossaryEntry>
            {
                new GlossaryEntry { Id = "sol", TermKey = "gloss.sol.term", DefinitionKey = "gloss.sol.def", SourceId = "SRC-COLD" },
                new GlossaryEntry { Id = "power", TermKey = "gloss.power.term", DefinitionKey = "gloss.power.def", SourceId = "SRC-DUST" },
                new GlossaryEntry { Id = "o2", TermKey = "gloss.o2.term", DefinitionKey = "gloss.o2.def", SourceId = "SRC-CO2" },
                new GlossaryEntry { Id = "water", TermKey = "gloss.water.term", DefinitionKey = "gloss.water.def", SourceId = "SRC-WATER" },
                new GlossaryEntry { Id = "food", TermKey = "gloss.food.term", DefinitionKey = "gloss.food.def", SourceId = "SRC-CROPS" },
                new GlossaryEntry { Id = "rad", TermKey = "gloss.rad.term", DefinitionKey = "gloss.rad.def", SourceId = "SRC-RAD" },
                new GlossaryEntry { Id = "shield", TermKey = "gloss.shield.term", DefinitionKey = "gloss.shield.def", SourceId = "SRC-SHIELD" },
                new GlossaryEntry { Id = "ap", TermKey = "gloss.ap.term", DefinitionKey = "gloss.ap.def", SourceId = "" },
                new GlossaryEntry { Id = "dsn", TermKey = "gloss.dsn.term", DefinitionKey = "gloss.dsn.def", SourceId = "SRC-COMMS" },
                new GlossaryEntry { Id = "storm", TermKey = "gloss.storm.term", DefinitionKey = "gloss.storm.def", SourceId = "SRC-STORM" }
            };
        }
    }
}
