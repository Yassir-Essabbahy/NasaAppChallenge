using System;
using System.Collections.Generic;

namespace Outpost.Sim
{
    public class SimEventChoice
    {
        public string LabelKey;
        public string OutcomeKey;
        public int ApCost;
        public float PowerDelta;
        public float OxygenDelta;
        public float WaterDelta;
        public float FoodDelta;
        public float ShieldingDelta;
        public float DustDelta;
        public float ScrubberHealthDelta;
        public float RecyclerHealthDelta;
        public float CropHealthDelta;
        public float CrewHealthDelta;
        public float CrewMoraleDelta;
        public float CommanderHealthDelta;
        public float ExtraDoseMsv;
        public bool TriggerDustStorm;
        public float StormDustFactor = 1f;
        public bool ShelterSolarFlare;
        public bool PartialShelterSolarFlare;
        public bool TriggerSolarFlare;
        public bool ApplyRationThisSol;
        public bool IsEarthTelemetryPatch;
        public float EvaDoseMsv;
    }

    public class SimEventDefinition
    {
        public string Id;
        public string TitleKey;
        public string DescriptionKey;
        public string FactKey;
        public string SourceId;
        public float OnTriggerShieldingDelta;
        public bool StartsDustStorm;
        public bool StartsSolarFlare;
        public List<SimEventChoice> Choices = new List<SimEventChoice>();
    }

    public static class EventCatalog
    {
        public static readonly List<NasaSource> Sources = new List<NasaSource>
        {
            new NasaSource { Id = "src_dust", TitleKey = "src.dust.title", AgencyKey = "src.dust.agency", UrlText = "https://science.nasa.gov/mission/mars-exploration-rovers/", FactKey = "evt.panel_dust.fact" },
            new NasaSource { Id = "src_co2", TitleKey = "src.co2.title", AgencyKey = "src.co2.agency", UrlText = "https://www.nasa.gov/missions/station/eclss/", FactKey = "evt.scrubber_clog.fact" },
            new NasaSource { Id = "src_storm", TitleKey = "src.storm.title", AgencyKey = "src.storm.agency", UrlText = "https://science.nasa.gov/resource/mars-dust-storms/", FactKey = "evt.dust_storm.fact" },
            new NasaSource { Id = "src_rad", TitleKey = "src.rad.title", AgencyKey = "src.rad.agency", UrlText = "https://science.nasa.gov/mission/msl-curiosity/radiation-assessment-detector/", FactKey = "evt.solar_flare.fact" },
            new NasaSource { Id = "src_water", TitleKey = "src.water.title", AgencyKey = "src.water.agency", UrlText = "https://www.nasa.gov/reference/environmental-control-and-life-support-systems/", FactKey = "evt.water_recycler.fact" },
            new NasaSource { Id = "src_crops", TitleKey = "src.crops.title", AgencyKey = "src.crops.agency", UrlText = "https://www.nasa.gov/exploration-research-and-technology/growing-plants-in-space/", FactKey = "evt.greenhouse_blight.fact" },
            new NasaSource { Id = "src_cold", TitleKey = "src.cold.title", AgencyKey = "src.cold.agency", UrlText = "https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html", FactKey = "evt.cold_snap.fact" },
            new NasaSource { Id = "src_psych", TitleKey = "src.psych.title", AgencyKey = "src.psych.agency", UrlText = "https://www.nasa.gov/humans-in-space/chapea/", FactKey = "evt.crew_tension.fact" },
            new NasaSource { Id = "src_comms", TitleKey = "src.comms.title", AgencyKey = "src.comms.agency", UrlText = "https://eyes.nasa.gov/apps/dsn-now/dsn.html", FactKey = "evt.earth_message.fact" },
            new NasaSource { Id = "src_shield", TitleKey = "src.shield.title", AgencyKey = "src.shield.agency", UrlText = "https://www.nasa.gov/humans-in-space/radiation/", FactKey = "evt.micrometeorite.fact" }
        };

        public static readonly Dictionary<string, SimEventDefinition> EventsById = BuildCatalog();

        public static NasaSource GetSource(string id)
        {
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].Id == id) return Sources[i];
            }
            return Sources[0];
        }

        private static Dictionary<string, SimEventDefinition> BuildCatalog()
        {
            var dict = new Dictionary<string, SimEventDefinition>();

            Add(dict, new SimEventDefinition
            {
                Id = "evt_panel_dust",
                TitleKey = "evt.panel_dust.title",
                DescriptionKey = "evt.panel_dust.desc",
                FactKey = "evt.panel_dust.fact",
                SourceId = "src_dust",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.panel_dust.c1", OutcomeKey = "evt.panel_dust.c1.out", ApCost = 1, DustDelta = -0.35f, EvaDoseMsv = 0.55f },
                    new SimEventChoice { LabelKey = "evt.panel_dust.c2", OutcomeKey = "evt.panel_dust.c2.out", PowerDelta = -6f, DustDelta = -0.20f },
                    new SimEventChoice { LabelKey = "evt.panel_dust.c3", OutcomeKey = "evt.panel_dust.c3.out", DustDelta = 0.10f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_scrubber_clog",
                TitleKey = "evt.scrubber_clog.title",
                DescriptionKey = "evt.scrubber_clog.desc",
                FactKey = "evt.scrubber_clog.fact",
                SourceId = "src_co2",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.scrubber_clog.c1", OutcomeKey = "evt.scrubber_clog.c1.out", ApCost = 1, ScrubberHealthDelta = 0.25f },
                    new SimEventChoice { LabelKey = "evt.scrubber_clog.c2", OutcomeKey = "evt.scrubber_clog.c2.out", PowerDelta = -10f },
                    new SimEventChoice { LabelKey = "evt.scrubber_clog.c3", OutcomeKey = "evt.scrubber_clog.c3.out", ScrubberHealthDelta = -0.35f, OxygenDelta = -8f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_dust_storm",
                TitleKey = "evt.dust_storm.title",
                DescriptionKey = "evt.dust_storm.desc",
                FactKey = "evt.dust_storm.fact",
                SourceId = "src_storm",
                StartsDustStorm = true,
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.dust_storm.c1", OutcomeKey = "evt.dust_storm.c1.out", CrewMoraleDelta = -6f, PowerDelta = 8f, TriggerDustStorm = true },
                    new SimEventChoice { LabelKey = "evt.dust_storm.c2", OutcomeKey = "evt.dust_storm.c2.out", TriggerDustStorm = true },
                    new SimEventChoice { LabelKey = "evt.dust_storm.c3", OutcomeKey = "evt.dust_storm.c3.out", ApCost = 1, TriggerDustStorm = true, StormDustFactor = 0.5f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_solar_flare",
                TitleKey = "evt.solar_flare.title",
                DescriptionKey = "evt.solar_flare.desc",
                FactKey = "evt.solar_flare.fact",
                SourceId = "src_rad",
                StartsSolarFlare = true,
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.solar_flare.c1", OutcomeKey = "evt.solar_flare.c1.out", CrewMoraleDelta = -8f, ShelterSolarFlare = true, TriggerSolarFlare = true },
                    new SimEventChoice { LabelKey = "evt.solar_flare.c2", OutcomeKey = "evt.solar_flare.c2.out", TriggerSolarFlare = true },
                    new SimEventChoice { LabelKey = "evt.solar_flare.c3", OutcomeKey = "evt.solar_flare.c3.out", WaterDelta = -10f, PartialShelterSolarFlare = true, TriggerSolarFlare = true }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_water_recycler",
                TitleKey = "evt.water_recycler.title",
                DescriptionKey = "evt.water_recycler.desc",
                FactKey = "evt.water_recycler.fact",
                SourceId = "src_water",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.water_recycler.c1", OutcomeKey = "evt.water_recycler.c1.out", ApCost = 1, RecyclerHealthDelta = 0.30f },
                    new SimEventChoice { LabelKey = "evt.water_recycler.c2", OutcomeKey = "evt.water_recycler.c2.out", CrewMoraleDelta = -6f, WaterDelta = 6f, ApplyRationThisSol = true },
                    new SimEventChoice { LabelKey = "evt.water_recycler.c3", OutcomeKey = "evt.water_recycler.c3.out", RecyclerHealthDelta = -0.30f, WaterDelta = -6f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_greenhouse_blight",
                TitleKey = "evt.greenhouse_blight.title",
                DescriptionKey = "evt.greenhouse_blight.desc",
                FactKey = "evt.greenhouse_blight.fact",
                SourceId = "src_crops",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.greenhouse_blight.c1", OutcomeKey = "evt.greenhouse_blight.c1.out", FoodDelta = -10f, CropHealthDelta = 0.25f },
                    new SimEventChoice { LabelKey = "evt.greenhouse_blight.c2", OutcomeKey = "evt.greenhouse_blight.c2.out", PowerDelta = -10f, CropHealthDelta = 0.15f },
                    new SimEventChoice { LabelKey = "evt.greenhouse_blight.c3", OutcomeKey = "evt.greenhouse_blight.c3.out", CropHealthDelta = -0.35f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_cold_snap",
                TitleKey = "evt.cold_snap.title",
                DescriptionKey = "evt.cold_snap.desc",
                FactKey = "evt.cold_snap.fact",
                SourceId = "src_cold",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.cold_snap.c1", OutcomeKey = "evt.cold_snap.c1.out", PowerDelta = -10f, CrewMoraleDelta = 4f },
                    new SimEventChoice { LabelKey = "evt.cold_snap.c2", OutcomeKey = "evt.cold_snap.c2.out", CrewMoraleDelta = -8f, CrewHealthDelta = -5f },
                    new SimEventChoice { LabelKey = "evt.cold_snap.c3", OutcomeKey = "evt.cold_snap.c3.out", FoodDelta = -8f, CropHealthDelta = -0.15f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_crew_tension",
                TitleKey = "evt.crew_tension.title",
                DescriptionKey = "evt.crew_tension.desc",
                FactKey = "evt.crew_tension.fact",
                SourceId = "src_psych",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.crew_tension.c1", OutcomeKey = "evt.crew_tension.c1.out", ApCost = 1, CrewMoraleDelta = 14f },
                    new SimEventChoice { LabelKey = "evt.crew_tension.c2", OutcomeKey = "evt.crew_tension.c2.out", CrewMoraleDelta = -10f, PowerDelta = 6f },
                    new SimEventChoice { LabelKey = "evt.crew_tension.c3", OutcomeKey = "evt.crew_tension.c3.out", FoodDelta = -7f, CrewMoraleDelta = 12f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_earth_message",
                TitleKey = "evt.earth_message.title",
                DescriptionKey = "evt.earth_message.desc",
                FactKey = "evt.earth_message.fact",
                SourceId = "src_comms",
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.earth_message.c1", OutcomeKey = "evt.earth_message.c1.out", PowerDelta = -5f, IsEarthTelemetryPatch = true },
                    new SimEventChoice { LabelKey = "evt.earth_message.c2", OutcomeKey = "evt.earth_message.c2.out", CrewMoraleDelta = 4f },
                    new SimEventChoice { LabelKey = "evt.earth_message.c3", OutcomeKey = "evt.earth_message.c3.out", PowerDelta = -5f, CommanderHealthDelta = 14f }
                }
            });

            Add(dict, new SimEventDefinition
            {
                Id = "evt_micrometeorite",
                TitleKey = "evt.micrometeorite.title",
                DescriptionKey = "evt.micrometeorite.desc",
                FactKey = "evt.micrometeorite.fact",
                SourceId = "src_shield",
                OnTriggerShieldingDelta = -15f,
                Choices = new List<SimEventChoice>
                {
                    new SimEventChoice { LabelKey = "evt.micrometeorite.c1", OutcomeKey = "evt.micrometeorite.c1.out", ApCost = 1, PowerDelta = -5f, ShieldingDelta = 15f },
                    new SimEventChoice { LabelKey = "evt.micrometeorite.c2", OutcomeKey = "evt.micrometeorite.c2.out", OxygenDelta = -6f, ShieldingDelta = 8f },
                    new SimEventChoice { LabelKey = "evt.micrometeorite.c3", OutcomeKey = "evt.micrometeorite.c3.out", CrewMoraleDelta = -5f }
                }
            });

            return dict;
        }

        private static void Add(Dictionary<string, SimEventDefinition> dict, SimEventDefinition evt)
        {
            dict[evt.Id] = evt;
        }

        /// <summary>
        /// Builds a 7-sol deterministic event sequence:
        /// Sol 1 & 2 are fixed tutorial events; Sols 3-7 are shuffled from the remaining 8 events
        /// while guaranteeing both Dust Storm and Solar Flare occur during the mission.
        /// </summary>
        public static List<SimEventDefinition> BuildMissionDeck(Random rng, int totalSols)
        {
            var deck = new List<SimEventDefinition>(totalSols);
            deck.Add(EventsById["evt_panel_dust"]);    // Sol 1 tutorial
            if (totalSols >= 2)
                deck.Add(EventsById["evt_scrubber_clog"]); // Sol 2 tutorial

            var mandatory = new List<string> { "evt_dust_storm", "evt_solar_flare" };
            var optional = new List<string>
            {
                "evt_water_recycler",
                "evt_greenhouse_blight",
                "evt_cold_snap",
                "evt_crew_tension",
                "evt_earth_message",
                "evt_micrometeorite"
            };

            Shuffle(optional, rng);
            int remaining = totalSols - deck.Count;
            var laterIds = new List<string>();
            for (int i = 0; i < mandatory.Count && laterIds.Count < remaining; i++)
                laterIds.Add(mandatory[i]);
            for (int i = 0; i < optional.Count && laterIds.Count < remaining; i++)
                laterIds.Add(optional[i]);

            Shuffle(laterIds, rng);
            for (int i = 0; i < laterIds.Count; i++)
                deck.Add(EventsById[laterIds[i]]);

            return deck;
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
