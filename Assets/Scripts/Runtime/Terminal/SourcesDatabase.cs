using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    [Serializable]
    public class SourceRecord
    {
        public string Id;
        public string Title;
        public string Organization;
        public string UrlText;
        public string UsedByConstantsAndEvents;
    }

    /// <summary>
    /// Placeholder structure for NASA/agency source entries displayed in the Sources terminal app.
    /// Populated with clean structural slots to be filled with final verified URLs/citations later.
    /// </summary>
    [CreateAssetMenu(fileName = "SourcesDatabase", menuName = "Outpost/Sources Database", order = 2)]
    public class SourcesDatabase : ScriptableObject
    {
        public List<SourceRecord> Entries = new List<SourceRecord>
        {
            new SourceRecord { Id = "src_dust", Title = "[Placeholder] Solar Array Dust Accumulation", Organization = "NASA JPL / Mars Exploration", UrlText = "https://science.nasa.gov/mission/mars-exploration-rovers/", UsedByConstantsAndEvents = "DustAccumulationPerSol, evt_panel_dust" },
            new SourceRecord { Id = "src_co2", Title = "[Placeholder] Cabin CO2 Scrubbing & ECLSS", Organization = "NASA Human Spaceflight", UrlText = "https://www.nasa.gov/missions/station/eclss/", UsedByConstantsAndEvents = "OxygenGenAtFull, LowScrubberThreshold, evt_scrubber_clog" },
            new SourceRecord { Id = "src_storm", Title = "[Placeholder] Martian Dust Storms & Optical Depth", Organization = "NASA Mars Science", UrlText = "https://science.nasa.gov/resource/mars-dust-storms/", UsedByConstantsAndEvents = "StormSolarFactor, StormExtraDustPerSol, evt_dust_storm" },
            new SourceRecord { Id = "src_rad", Title = "[Placeholder] Mars Surface Radiation & SPE", Organization = "NASA / Curiosity RAD", UrlText = "https://science.nasa.gov/mission/msl-curiosity/radiation-assessment-detector/", UsedByConstantsAndEvents = "BaselineSurfaceDoseMsvPerSol, SolarFlareUnshieldedDoseMsv, evt_solar_flare" },
            new SourceRecord { Id = "src_water", Title = "[Placeholder] Closed-Loop Water Recovery", Organization = "NASA MSFC / ISS", UrlText = "https://www.nasa.gov/reference/environmental-control-and-life-support-systems/", UsedByConstantsAndEvents = "WaterGenAtFull, WaterFixedLoss, evt_water_recycler" },
            new SourceRecord { Id = "src_crops", Title = "[Placeholder] Space Crop & Veggie Systems", Organization = "NASA KSC", UrlText = "https://www.nasa.gov/exploration-research-and-technology/growing-plants-in-space/", UsedByConstantsAndEvents = "FoodGenAtFull, GreenhouseOxygenBonus, evt_greenhouse_blight" },
            new SourceRecord { Id = "src_cold", Title = "[Placeholder] Martian Thermal Environment", Organization = "NASA GSFC Mars Fact Sheet", UrlText = "https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html", UsedByConstantsAndEvents = "HeatingSafeThreshold, ColdHealthPenalty, evt_cold_snap" },
            new SourceRecord { Id = "src_psych", Title = "[Placeholder] Isolated Crew Behavioral Health", Organization = "NASA JSC CHAPEA / HERA", UrlText = "https://www.nasa.gov/humans-in-space/chapea/", UsedByConstantsAndEvents = "LowMoraleThreshold, RationMoraleCost, evt_crew_tension" },
            new SourceRecord { Id = "src_comms", Title = "[Placeholder] Earth-Mars Light-Time Delay", Organization = "NASA Deep Space Network", UrlText = "https://eyes.nasa.gov/apps/dsn-now/dsn.html", UsedByConstantsAndEvents = "CommsMaxPower, evt_earth_message" },
            new SourceRecord { Id = "src_shield", Title = "[Placeholder] Regolith Shielding & Micrometeoroids", Organization = "NASA Moon to Mars", UrlText = "https://www.nasa.gov/humans-in-space/radiation/", UsedByConstantsAndEvents = "ShieldingMaxReduction, ReinforceShieldingGain, evt_micrometeorite" }
        };
    }
}
