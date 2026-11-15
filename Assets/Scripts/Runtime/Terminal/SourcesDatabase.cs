using System;
using System.Collections.Generic;
using Outpost.Sim;
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
        public string License = "UNVERIFIED - check before submission";
        public string DateAdded = "2026-11-15";
        public string WhereUsed = "";
        public string ModifiedNotes = "No";

        [Header("Localization & Secondary Reference")]
        public string TitleKey;
        public string OrganizationKey;
        public string SupportsKey;
        public string SupportsNote;
        public string SecondaryUrl;

        public string ResolveTitle()
        {
            if (!string.IsNullOrEmpty(TitleKey))
            {
                string loc = Localization.Get(TitleKey);
                if (!string.IsNullOrEmpty(loc) && !string.Equals(loc, TitleKey, StringComparison.Ordinal))
                    return loc;
            }
            return Title ?? string.Empty;
        }

        public string ResolveOrganization()
        {
            if (!string.IsNullOrEmpty(OrganizationKey))
            {
                string loc = Localization.Get(OrganizationKey);
                if (!string.IsNullOrEmpty(loc) && !string.Equals(loc, OrganizationKey, StringComparison.Ordinal))
                    return loc;
            }
            return Organization ?? string.Empty;
        }

        public string ResolveSupports()
        {
            if (!string.IsNullOrEmpty(SupportsKey))
            {
                string loc = Localization.Get(SupportsKey);
                if (!string.IsNullOrEmpty(loc) && !string.Equals(loc, SupportsKey, StringComparison.Ordinal))
                    return loc;
            }
            if (!string.IsNullOrEmpty(SupportsNote))
                return SupportsNote;
            return UsedByConstantsAndEvents ?? string.Empty;
        }
    }

    /// <summary>
    /// Shared master database for scientific sources AND all integrated third-party / in-project
    /// asset, package, font, and tool credits (read by SourcesAppWindow, MissionFlowController, and ASSET_SOURCES.md exporter).
    /// </summary>
    [CreateAssetMenu(fileName = "SourcesDatabase", menuName = "Outpost/Sources Database", order = 2)]
    public class SourcesDatabase : ScriptableObject
    {
        public SourceRecord FindById(string id)
        {
            if (string.IsNullOrEmpty(id) || Entries == null) return null;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null && string.Equals(Entries[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Entries[i];
            }
            return null;
        }

        public static void OpenSourceUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return;
            string cleaned = rawUrl.Trim();
            int spaceIdx = cleaned.IndexOf(' ');
            if (spaceIdx > 0) cleaned = cleaned.Substring(0, spaceIdx);
            if (!cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return;
            Application.OpenURL(cleaned);
        }

        private static SourceRecord MakeScienceSource(string id, string title, string org, string url, string supports, string usedBy, string license = "UNVERIFIED - check before submission", string secondaryUrl = "")
        {
            return new SourceRecord
            {
                Id = id,
                Title = title,
                Organization = org,
                UrlText = url,
                SupportsNote = supports,
                UsedByConstantsAndEvents = usedBy,
                TitleKey = $"src.{id}.title",
                OrganizationKey = $"src.{id}.org",
                SupportsKey = $"src.{id}.supports",
                Section = "Scientific & Reference Sources",
                License = license,
                DateAdded = "2026-11-15",
                WhereUsed = usedBy,
                ModifiedNotes = "Reference metadata & FactCard adaptation",
                SecondaryUrl = secondaryUrl
            };
        }

        public List<SourceRecord> Entries = new List<SourceRecord>
        {
            MakeScienceSource("nasa-jpl-opportunity-recovery", "Six Things About Opportunity's Recovery Efforts", "NASA/JPL", "https://www.jpl.nasa.gov/news/six-things-about-opportunitys-recovery-efforts/", "Supports Dust storm event (fact_dust_storm): why sunlight loss halts solar-powered Mars hardware.", "evt_dust_storm, fact_dust_storm"),
            MakeScienceSource("nasa-jpl-opportunity-hunkers", "Opportunity Hunkers Down During Dust Storm", "NASA/JPL", "https://www.jpl.nasa.gov/news/opportunity-hunkers-down-during-dust-storm/", "Supports Dust storm event (fact_dust_storm): how Opportunity entered safe mode during planet-wide dust storms.", "evt_dust_storm, fact_dust_storm"),
            MakeScienceSource("nasa-ntrs-mer-dust", "MER Opportunity dust-storm recovery operations and implications for future Mars surface missions", "NASA NTRS", "https://ntrs.nasa.gov/citations/20210015954", "Supports Panel dust build-up (fact_panel_dust): cutting rover power loads to preserve battery before storms.", "evt_panel_dust, fact_panel_dust"),
            MakeScienceSource("nasa-artemis2-sun", "To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun", "NASA", "https://science.nasa.gov/missions/artemis/artemis-2/to-protect-artemis-ii-astronauts-nasa-experts-keep-eyes-on-sun/", "Supports Solar particle event (fact_solar_flare): monitoring solar flares so crews can shelter in time.", "evt_solar_flare, fact_solar_flare"),
            MakeScienceSource("nasa-svs-radiation", "How NASA Will Protect Astronauts From Space Radiation", "NASA SVS", "https://svs.gsfc.nasa.gov/vis/a010000/a013200/a013275/script_27781_00.html", "Supports Solar particle event (fact_solar_flare): shielding techniques and crew storm shelters.", "evt_solar_flare, fact_solar_flare"),
            MakeScienceSource("hassler-2014-rad", "Mars' Surface Radiation Environment Measured with the Mars Science Laboratory's Curiosity Rover (Hassler et al., Science)", "Science / SwRI / NASA", "https://doi.org/10.1126/science.1244797", "Supports Baseline radiation (fact_radiation_baseline & BaselineSurfaceDoseMsvPerSol): ~0.67 mSv/day measured by Curiosity.", "BaselineSurfaceDoseMsvPerSol, fact_radiation_baseline"),
            MakeScienceSource("iss-water-98", "NASA Achieves Water Recovery Milestone on International Space Station (98% ECLSS)", "NASA / ABC News", "https://www.nasa.gov/missions/station/iss-research/nasa-achieves-water-recovery-milestone-on-international-space-station/", "Supports Water recycler fault (fact_water_recycler), ~98% ISS recycling & ~1 gal/day water use (ABC report: https://abc7.com/13433546/).", "evt_water_recycler, fact_water_recycler, WaterGenAtFull", secondaryUrl: "https://abc7.com/13433546/"),
            MakeScienceSource("nasa-veggie-lettuce", "Astronauts Take First Bites of Lettuce Grown in Space", "Scientific American (NASA Veggie)", "https://www.scientificamerican.com/article/astronauts-take-first-bites-of-lettuce-grown-in-space/", "Supports Greenhouse blight (fact_greenhouse): growing and harvesting fresh crops aboard the ISS.", "evt_greenhouse_blight, fact_greenhouse"),
            MakeScienceSource("npr-veggie-mood", "The Salad Frontier: Why Astronauts Need To Grow Lettuce In Space", "NPR", "https://www.npr.org/blogs/thesalt/2014/06/10/320321553/the-salad-frontier-why-astronauts-need-to-grow-lettuce-in-space", "Supports Greenhouse blight (fact_greenhouse): fresh vegetables support astronaut morale in isolated missions.", "evt_greenhouse_blight, fact_greenhouse"),
            MakeScienceSource("esa-mars-facts", "Facts about Mars", "ESA", "https://www.esa.int/content/view/full/200994", "Supports ambient announcer facts f_gravity (~38% Earth gravity) and f_moons (Phobos & Deimos).", "f_gravity, f_moons"),
            MakeScienceSource("esa-meet-mars", "Meet Mars", "ESA", "https://m.esa.int/Our_Activities/Human_and_Robotic_Exploration/Exploration/ExoMars/Meet_Mars", "Supports Cold snap (fact_cold_snap) and ambient f_cold (-133 C winter pole to +27 C summer).", "evt_cold_snap, fact_cold_snap, f_cold"),
            MakeScienceSource("nasa-mars-fact-sheet", "Mars Fact Sheet", "NASA NSSDCA", "https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html", "Supports CO2 scrubber clog (fact_co2_scrubber: ~95% CO2), ambient f_sol (24 h 39 m) & f_cold.", "evt_scrubber_clog, fact_co2_scrubber, f_sol, f_cold"),
            MakeScienceSource("space-mars-atmosphere", "Mars' atmosphere: Facts about composition and climate (MOXIE)", "Space.com", "https://www.space.com/16903-mars-atmosphere-climate-weather.html", "Supports CO2 scrubber clog (fact_co2_scrubber): 95% CO2 atmosphere and Perseverance MOXIE oxygen experiment.", "evt_scrubber_clog, fact_co2_scrubber"),
            MakeScienceSource("esa-light-time", "Spacecraft event time vs. Earth receive time", "ESA Mars Express blog", "https://blogs.esa.int/mex/?p=257", "Supports Message from Earth (fact_earth_message) and ambient f_signal: 4-24 min one-way light delay.", "evt_earth_message, fact_earth_message, f_signal"),
            MakeScienceSource("sis-habitat", "Building a space habitat in the classroom", "Science in School", "https://scienceinschool.org/2011/issue19/habitat", "Supports Airlock dust / impact (fact_airlock_dust): protecting crew from radiation, micrometeorites, dust, vacuum, cold.", "evt_micrometeorite, fact_airlock_dust"),
            MakeScienceSource("abc-chapea", "NASA says 98% of astronauts' urine, sweat can be recycled (also mentions CHAPEA Mars-analog study)", "ABC News", "https://abc7ny.com/post/nasa-recycle-urine-sweat/13433546/", "Supports Crew tension (fact_crew_tension): year-long Mars habitat isolation study (primary NASA CHAPEA page found via search).", "evt_crew_tension, fact_crew_tension", secondaryUrl: "https://www.nasa.gov/missions/analog-field-testing/chapea/first-mars-crew-completes-yearlong-simulated-red-planet-nasa-mission/"),
            MakeScienceSource("usgs-jezero-dem", "Mars 2020 Jezero crater landing site products", "USGS Astrogeology", "https://astrogeology.usgs.gov/search/map/mars-2020-jezero-crater-landing-site-products", "Supports Mars surface terrain exploration backdrop at Outpost Helios (Jezero Crater).", "Exterior Mars Terrain & Environment"),
            MakeScienceSource("nasa-3d-mars", "NASA 3D Resources: Mars texture (Viking, USGS/JPL)", "NASA", "https://science.nasa.gov/3d-resources/mars", "Supports Mars global color texture and orbital backdrop on the Title Screen.", "MainMenu Mars orb & Mars.jpg")
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
                WhereUsed = "Assets/Prefabs/MarsSphere.prefab; rotating Mars planet backdrop in MainMenu.unity (kept out of Martian surface sky in Outpost.unity for orbital accuracy)",
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
                ModifiedNotes = "Used FBX only (GLB skipped); Point-filtered textures; PSX shader conversion preserving emissives; separated DoorLeaf sliding mesh; replaced panel_grey/panel_white with clean PSX station plating"
            },
            new SourceRecord
            {
                Id = "lowbitlab-station-sample",
                Section = "Corridor kit",
                Title = "PSX Space Station Interior - Free Sample (6 Models + Atlas)",
                Organization = "Lowbit Lab",
                UrlText = "https://lowbit-lab.itch.io/ (Lowbit Lab on itch.io - verify exact pack URL)",
                License = "Royalty-free commercial & non-commercial games/apps; no resale/redistribution of raw assets (per README.txt)",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Prefabs/Interior/Prop_CryoPod, Prop_ControlConsoleWide, Prop_SpaceSuitRack, Prop_MaintenanceRobot, Prop_AirlockHatchRound, Corridor_Corner_PSX + sliced T_Station_* interior/hull textures",
                ModifiedNotes = "Point-filtered 128x128 atlas & PSX Lit vertex-color conversion; upright axis alignment (-90 X); sliced tiles for wall/floor/ceiling/hull base textures"
            },
            new SourceRecord
            {
                Id = "psx-satellite-radio",
                Section = "Corridor kit",
                Title = "PSX Style Satellite Radio",
                Organization = "Third-party PSX model pack (Assets/Models/psx-style-satellite-radio - confirm author before submission)",
                UrlText = "UNVERIFIED - check before submission",
                License = "UNVERIFIED - check before submission",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Prefabs/Interior/Prop_SatelliteRadio.prefab (Command Module desk PF_Radio & Mars surface DSN_FieldRadio)",
                ModifiedNotes = "Extracted Satellite Radio.fbx from source zip, scaled to ~0.48m desk span, converted textures to Point-filtered PSX Lit"
            },
            new SourceRecord
            {
                Id = "radiation-sign-glb",
                Section = "Corridor kit",
                Title = "Radiation Hazard Sign (Assets/Models/radiation_sign.glb)",
                Organization = "Third-party 3D model (confirm author before submission)",
                UrlText = "UNVERIFIED - check before submission",
                License = "UNVERIFIED - check before submission",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Prefabs/Prop_RadiationSign.prefab (Mars surface POI_RadiationPost + perimeter hazard signs)",
                ModifiedNotes = "Baked embedded glTF texture to Point-filtered PNG (T_RadiationSign.png), scaled to 2.05m, converted to PSX Lit"
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
                License = "SIL OFL 1.1 / Unity Built-in (UNVERIFIED - check before submission)",
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

            // Sound effects
            new SourceRecord
            {
                Id = "sfx-warning-klaxon",
                Section = "Sound effects",
                Title = "Warning.wav (Facility Hazard Alert Sting)",
                Organization = "Project Audio Import (Exact Author/Source URL UNVERIFIED — update before submission)",
                UrlText = "Assets/Sounds/VOX/Warning.wav (origin URL UNVERIFIED)",
                License = "UNVERIFIED - check before submission (verify original SFX license)",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Sounds/VOX/Warning.wav -> baked lightly via Sting preset to Assets/Audio/Announcer/Processed/alert_sting.wav",
                ModifiedNotes = "Original untouched; baked copy trimmed, lightly reverbed (-1.5 dBFS mono 22050 Hz)"
            },
            new SourceRecord
            {
                Id = "sfx-atmosphere-doors",
                Section = "Sound effects",
                Title = "Outpost Atmosphere & Door SFX (SpaceShip.mp3, MarsSound.wav, DoorOpen.wav, CloseDoor.wav, footsteps)",
                Organization = "Project Audio Import (Exact Author/Source URL UNVERIFIED — update before submission)",
                UrlText = "Assets/Sounds/Atmosphere/ (origin URL UNVERIFIED)",
                License = "UNVERIFIED - check before submission (verify original SFX license)",
                DateAdded = "2026-11-15",
                WhereUsed = "AudioManager in Outpost.unity: SpaceShip.mp3 (interior loop), MarsSound.wav (exterior Mars loop), DoorOpen.wav / CloseDoor.wav, exterior stone footsteps",
                ModifiedNotes = "Originals untouched; configured Unity AudioImporter Vorbis compression"
            },

            // Generated audio
            new SourceRecord
            {
                Id = "audio-generated-tts",
                Section = "Generated audio",
                Title = "ElevenLabs Voice Clips & Facility Announcer Lines",
                Organization = "ElevenLabs Speech Synthesis (Voices: Lauren, Jessica) / Project Announcer Recordings",
                UrlText = "https://elevenlabs.io/ (License/Tier UNVERIFIED)",
                License = "UNVERIFIED - check before submission (check ElevenLabs plan commercial terms)",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Sounds/VOX/ (8 ElevenLabs test voice clips + pending 11 canonical AnnouncerDatabase lines)",
                ModifiedNotes = "Originals kept intact and unmodified"
            },

            // Processed audio (baked in-project)
            new SourceRecord
            {
                Id = "audio-baked-vox-dsp",
                Section = "Processed audio (baked in-project)",
                Title = "VOX FX Offline DSP Chain & Baked Facility Audio",
                Organization = "In-project pure-C# DSP (VoxFxDsp.cs / VoxFxBaker.cs)",
                UrlText = "Internal project asset (Zero external plugins / zero AudioMixer dependency)",
                License = "Original project work",
                DateAdded = "2026-11-15",
                WhereUsed = "Assets/Audio/Announcer/Processed/ (22050 Hz mono PCM -> Vorbis 0.45): telephone bandpass, S&H bitcrush, 70Hz ring mod, tanh saturation, dark echo, Schroeder hall reverb, radio squelch",
                ModifiedNotes = "Offline idempotent bake with VOX Classic, Heavy Echo, Clean Radio, and Sting presets"
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
