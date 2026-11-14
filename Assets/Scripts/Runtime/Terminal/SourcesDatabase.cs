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
        public string Section = "NASA/USGS data and models";
        public string License = "";
        public string DateAdded = "2026-11-14";
        public string WhereUsed = "";
        public string ModifiedNotes = "No";
    }

    /// <summary>
    /// Shared master database for scientific sources AND all integrated third-party / in-project
    /// asset, package, font, and tool credits (read by SourcesAppWindow, MissionFlowController, and ASSET_SOURCES.md exporter).
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
            new SourceRecord { Id = "src_shield", Title = "[Placeholder] Regolith Shielding & Micrometeoroids", Organization = "NASA Moon to Mars", UrlText = "https://www.nasa.gov/humans-in-space/radiation/", UsedByConstantsAndEvents = "ShieldingMaxReduction, ReinforceShieldingGain, evt_micrometeorite" },
            new SourceRecord { Id = "nasa-perseverance-model", Title = "Mars 2020 Perseverance Rover 3D Model (Placeholder Fact)", Organization = "NASA / JPL-Caltech", UrlText = "https://science.nasa.gov/resource/perseverance-rover-3d-model/ (UNVERIFIED - check before submission)", UsedByConstantsAndEvents = "Exterior Prop_Rover POI near Solar Array" }
        };

        public List<SourceRecord> AssetCredits = new List<SourceRecord>
        {
            // NASA/USGS data and models
            new SourceRecord
            {
                Id = "nasa-mars-equirect",
                Section = "NASA/USGS data and models",
                Title = "Mars Global Color Map (Assets/Models/Mars.jpg)",
                Organization = "NASA / USGS Astrogeology (UNVERIFIED - check source exact attribution)",
                UrlText = "UNVERIFIED - check before submission",
                License = "Public Domain (assumed NASA/USGS — UNVERIFIED - check before submission)",
                DateAdded = "2026-11-14",
                WhereUsed = "MarsSphere prefab (TitleScreen backdrop & Command Module sky window)",
                ModifiedNotes = "Imported with Point filter, No Mipmaps, Max Size 2048, Wrap Repeat"
            },
            new SourceRecord
            {
                Id = "nasa-perseverance-model",
                Section = "NASA/USGS data and models",
                Title = "25042_Perseverance.glb (Mars 2020 Perseverance Rover)",
                Organization = "NASA / JPL-Caltech (UNVERIFIED - confirm catalog ID)",
                UrlText = "UNVERIFIED - check before submission",
                License = "NASA Media Usage Guidelines / Public Domain (UNVERIFIED - check before submission)",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Prefabs/Prop_Rover.prefab; placed near Solar Array on Mars surface",
                ModifiedNotes = "Scaled uniformly to ~3.0m length, PSX Lit material conversion, BoxCollider added (199,309 tris; recommend decimation in Blender)"
            },
            new SourceRecord
            {
                Id = "usgs-mars-dem",
                Section = "NASA/USGS data and models",
                Title = "Mars MOLA / HiRISE Heightmap (Pending RAW Import)",
                Organization = "NASA / USGS Astrogeology",
                UrlText = "TO CONFIRM when added",
                License = "TO CONFIRM when added",
                DateAdded = "TO CONFIRM",
                WhereUsed = "BackdropTerrain (Outpost/Import NASA Heightmap (RAW))",
                ModifiedNotes = "Pending user RAW export (GDAL gdal_translate -of ENVI)"
            },

            // Corridor kit
            new SourceRecord
            {
                Id = "hopefulsham-corridor-kit",
                Section = "Corridor kit",
                Title = "SciFi Corridor PRO LITE (v1.0.0)",
                Organization = "hopefulsham (source PBR textures: Poly Haven)",
                UrlText = "https://hopefulsham.itch.io/scifi-modular-corridor-pro",
                License = "Commercial & non-commercial projects without attribution; no resale/redistribution/AI training (Poly Haven source textures CC0 1.0)",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Prefabs/Interior/Corridor_Straight, Corridor_Doors, Corridor_End, Prop_Crate, Prop_Locker, Prop_Console",
                ModifiedNotes = "Used FBX only (GLB skipped); Point-filtered textures; PSX shader conversion preserving emissives; separated DoorLeaf sliding mesh"
            },

            // UI icons
            new SourceRecord
            {
                Id = "neon-command-ui-kit",
                Section = "UI icons",
                Title = "Neon Command UI Kit - Free Sampler (Arctic colorway)",
                Organization = "gen-xia",
                UrlText = "https://gen-xia.itch.io/neon-command-ui-kit",
                License = "Commercial & non-commercial projects permitted; modify/recolor/crop allowed; no standalone resale (AI-assisted per LICENSE.txt)",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Art/UI/Icons/ (12 copied 128x128 UI icons) + Terminal HUD & Apps via IconLibrary.asset",
                ModifiedNotes = "Renamed to Icon_* and registered in IconLibrary.asset"
            },

            // Fonts
            new SourceRecord
            {
                Id = "project-fonts",
                Section = "Fonts",
                Title = "Terminal & HUD Typography",
                Organization = "TextMeshPro / Unity LegacyRuntime / Google Fonts (if ShareTech/Roboto/Orbitron installed — UNVERIFIED exact TTF source)",
                UrlText = "Check Assets/Fonts or FontTheme assets",
                License = "SIL OFL 1.1 / Unity Built-in (verify before submission)",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Art/UI/TerminalTheme.asset, OutpostFlowCanvas, RoomHudCanvas",
                ModifiedNotes = "No"
            },

            // Packages
            new SourceRecord
            {
                Id = "pkg-gltfast",
                Section = "Packages",
                Title = "glTFast (com.unity.cloud.gltfast 6.10.1)",
                Organization = "Unity Technologies / Andreas Atteneder",
                UrlText = "https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.10/manual/index.html",
                License = "Unity Companion License / Apache-2.0",
                DateAdded = "2026-11-14",
                WhereUsed = "Packages/manifest.json (imports 25042_Perseverance.glb)",
                ModifiedNotes = "No"
            },
            new SourceRecord
            {
                Id = "pkg-urp",
                Section = "Packages",
                Title = "Universal RP (com.unity.render-pipelines.universal 17.5.0) & Input System",
                Organization = "Unity Technologies",
                UrlText = "https://unity.com/",
                License = "Unity Companion License",
                DateAdded = "2026-11-14",
                WhereUsed = "Active render pipeline and FPS/terminal input",
                ModifiedNotes = "No"
            },

            // Generated in-project
            new SourceRecord
            {
                Id = "gen-terrain",
                Section = "Generated in-project",
                Title = "Procedural MarsPlayTerrain, BackdropTerrain & 3 Low-Poly Rock Variants",
                Organization = "In-project script (MarsTerrainGenerator.cs)",
                UrlText = "Internal project asset",
                License = "Original project work",
                DateAdded = "2026-11-14",
                WhereUsed = "Outpost scene exterior Mars terrain, BackdropTerrain ring, Rock_Small/Medium/Large prefabs",
                ModifiedNotes = "Generated via Outpost/Generate Mars Terrain"
            },
            new SourceRecord
            {
                Id = "gen-psx-textures-shaders",
                Section = "Generated in-project",
                Title = "PSX Shaders & 64x64 Procedural Surface/Terrain Textures",
                Organization = "In-project script (PSXMaterialApplier.cs, Assets/Shaders/*)",
                UrlText = "Internal project asset",
                License = "Original project work",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Shaders/ (PSXLit, PSXUnlit, PSXLit_Cutout, PSXParticle, PSXPost, PSXTerrain, PSXSkyGradient) & Assets/Art/PSX/",
                ModifiedNotes = "Generated procedurally at 64x64 with Point filtering"
            },
            new SourceRecord
            {
                Id = "gen-ui-icons",
                Section = "Generated in-project",
                Title = "8 Matching 128x128 Procedural UI Icons",
                Organization = "In-project script (TerminalIconAssetBuilder.cs)",
                UrlText = "Internal project asset",
                License = "Original project work",
                DateAdded = "2026-11-14",
                WhereUsed = "Assets/Art/UI/Icons/ (Icon_Oxygen, Icon_Water, Icon_Food, Icon_Signal, Icon_Log, Icon_Sources, Icon_Help, Icon_Goal)",
                ModifiedNotes = "Generated in matching teal/light-blue/yellow/coral 6px line style"
            },

            // Tools and AI assistance
            new SourceRecord
            {
                Id = "tool-unity",
                Section = "Tools and AI assistance",
                Title = "Unity Editor (6000.x), Unity MCP (com.coplaydev.unity-mcp), Python & Antigravity AI Assistant",
                Organization = "Unity Technologies / CoplayDev / Google DeepMind Antigravity",
                UrlText = "https://unity.com/",
                License = "Unity Personal / MIT / Assistant workflow",
                DateAdded = "2026-11-14",
                WhereUsed = "Scene assembly, editor automation, shader/UI integration & verification",
                ModifiedNotes = "AI-assisted implementation and integration scripts"
            }
        };
    }
}
