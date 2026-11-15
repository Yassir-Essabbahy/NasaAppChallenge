using System;
using System.Collections.Generic;
using System.IO;
using Outpost.Runtime.Audio;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using UnityEditor;
using UnityEngine;

namespace Outpost.Editor
{
    /// <summary>
    /// Synchronizes on-disk ScriptableObject databases (SourcesDatabase, FactCards, FactDatabase,
    /// AnnouncerDatabase) and Assets/Credits/ASSET_SOURCES.md with canonical localization and source links.
    /// </summary>
    public static class ContentSyncUtility
    {
        private const string FactsDir = "Assets/Resources/Facts";
        private const string FactDatabasePath = "Assets/Resources/FactDatabase.asset";
        private const string SourcesDatabasePath = "Assets/Resources/SourcesDatabase.asset";
        private const string AnnouncerDatabasePath = "Assets/Resources/AnnouncerDatabase.asset";

        private readonly struct FactSpec
        {
            public readonly string AssetFileName;
            public readonly string Id;
            public readonly string RelatedEventId;
            public readonly string RelatedConstantId;
            public readonly string[] SourceIds;

            public FactSpec(string assetFileName, string id, string relatedEventId, string relatedConstantId, params string[] sourceIds)
            {
                AssetFileName = assetFileName;
                Id = id;
                RelatedEventId = relatedEventId;
                RelatedConstantId = relatedConstantId;
                SourceIds = sourceIds;
            }
        }

        private static readonly FactSpec[] CanonicalFacts =
        {
            new FactSpec("Fact_evt_dust_storm.asset", "fact_dust_storm", "evt_dust_storm", "StormSolarFactor",
                "nasa-jpl-opportunity-recovery", "nasa-jpl-opportunity-hunkers"),
            new FactSpec("Fact_evt_solar_flare.asset", "fact_solar_flare", "evt_solar_flare", "ShieldingMaxReduction",
                "nasa-artemis2-sun", "nasa-svs-radiation"),
            new FactSpec("Fact_evt_panel_dust.asset", "fact_panel_dust", "evt_panel_dust", "PanelMaintenanceGain",
                "nasa-ntrs-mer-dust"),
            new FactSpec("Fact_evt_water_recycler.asset", "fact_water_recycler", "evt_water_recycler", "WaterGenAtFull",
                "iss-water-98"),
            new FactSpec("Fact_evt_greenhouse_blight.asset", "fact_greenhouse", "evt_greenhouse_blight", "FoodGenAtFull",
                "nasa-veggie-lettuce", "npr-veggie-mood"),
            new FactSpec("Fact_evt_scrubber_clog.asset", "fact_co2_scrubber", "evt_scrubber_clog", "OxygenGenAtFull",
                "space-mars-atmosphere", "nasa-mars-fact-sheet"),
            new FactSpec("Fact_evt_cold_snap.asset", "fact_cold_snap", "evt_cold_snap", string.Empty,
                "esa-meet-mars"),
            new FactSpec("Fact_evt_crew_tension.asset", "fact_crew_tension", "evt_crew_tension", string.Empty,
                "abc-chapea"),
            new FactSpec("Fact_evt_earth_message.asset", "fact_earth_message", "evt_earth_message", string.Empty,
                "esa-light-time"),
            new FactSpec("Fact_evt_micrometeorite.asset", "fact_airlock_dust", "evt_micrometeorite", "ShieldingMaxReduction",
                "sis-habitat"),
            new FactSpec("Fact_radiation_baseline.asset", "fact_radiation_baseline", string.Empty, "BaselineSurfaceDoseMsvPerSol",
                "hassler-2014-rad")
        };

        [MenuItem("Outpost/Content/Sync Sources, FactCards & Announcer")]
        public static void SyncSourcesFactCardsAndCredits()
        {
            SyncSourcesDatabaseAsset();
            FactDatabase factDb = SyncFactDatabaseAssets();
            int announcerUpdated = SyncAnnouncerDatabaseAsset();

            AssetDatabase.SaveAssets();
            CreditsMarkdownExporter.ExportAssetSourcesMarkdown();
            AssetDatabase.Refresh();

            Debug.Log($"[ContentSyncUtility] Synced SourcesDatabase, {factDb.Cards.Count} FactCards, AnnouncerDatabase (changed={announcerUpdated > 0}), and ASSET_SOURCES.md.");
        }

        public static SourcesDatabase SyncSourcesDatabaseAsset()
        {
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");

            SourcesDatabase db = AssetDatabase.LoadAssetAtPath<SourcesDatabase>(SourcesDatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<SourcesDatabase>();
                AssetDatabase.CreateAsset(db, SourcesDatabasePath);
            }

            var fresh = ScriptableObject.CreateInstance<SourcesDatabase>();
            db.Entries = new List<SourceRecord>(fresh.Entries);
            db.AssetCredits = new List<SourceRecord>(fresh.AssetCredits);
            UnityEngine.Object.DestroyImmediate(fresh);

            EditorUtility.SetDirty(db);
            return db;
        }

        public static FactDatabase SyncFactDatabaseAssets()
        {
            if (!Directory.Exists("Assets/Resources"))
                Directory.CreateDirectory("Assets/Resources");
            if (!Directory.Exists(FactsDir))
                Directory.CreateDirectory(FactsDir);

            var cards = new List<FactCard>(CanonicalFacts.Length);
            for (int i = 0; i < CanonicalFacts.Length; i++)
            {
                FactSpec spec = CanonicalFacts[i];
                string assetPath = $"{FactsDir}/{spec.AssetFileName}";
                FactCard card = AssetDatabase.LoadAssetAtPath<FactCard>(assetPath);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<FactCard>();
                    AssetDatabase.CreateAsset(card, assetPath);
                }

                card.id = spec.Id;
                card.relatedEventId = spec.RelatedEventId;
                card.relatedConstantId = spec.RelatedConstantId;
                card.titleKey = $"fact.{spec.Id}.title";
                card.textKey = $"fact.{spec.Id}.text";
                card.title = Localization.Get(card.titleKey);
                card.text = Localization.Get(card.textKey);

                card.sourceIds ??= new List<string>();
                card.sourceIds.Clear();
                if (spec.SourceIds != null)
                {
                    for (int s = 0; s < spec.SourceIds.Length; s++)
                        card.sourceIds.Add(spec.SourceIds[s]);
                }
                card.sourceId = spec.SourceIds != null ? string.Join(", ", spec.SourceIds) : string.Empty;

                EditorUtility.SetDirty(card);
                cards.Add(card);
            }

            FactDatabase db = AssetDatabase.LoadAssetAtPath<FactDatabase>(FactDatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<FactDatabase>();
                AssetDatabase.CreateAsset(db, FactDatabasePath);
            }

            db.Cards = cards;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static int SyncAnnouncerDatabaseAsset()
        {
            AnnouncerDatabase db = AssetDatabase.LoadAssetAtPath<AnnouncerDatabase>(AnnouncerDatabasePath);
            if (db == null)
                return 0;

            bool changed = db.EnsureDefaultLines(onlyAddMissing: true);
            if (changed)
                EditorUtility.SetDirty(db);
            return changed ? 1 : 0;
        }
    }
}
