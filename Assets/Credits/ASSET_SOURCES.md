# ASSET_SOURCES & Project Credits

Master record of every external asset, texture, 3D model, font, UI icon, package, procedural generator, and AI/development tool used in this project.
Generated cleanly from shared `SourcesDatabase` (`Assets/Resources/SourcesDatabase.asset`) — keep updated whenever an asset or package is added.

## NASA/USGS data and models

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Mars Global Color Map (Assets/Models/Mars.jpg) | NASA / USGS Astrogeology (UNVERIFIED - check source exact attribution) | UNVERIFIED - check before submission | Public Domain (assumed NASA/USGS — UNVERIFIED - check before submission) | 2026-11-14 | Assets/Prefabs/MarsSphere.prefab; rotating Mars planet backdrop in MainMenu.unity (kept out of Martian surface sky in Outpost.unity for orbital accuracy) | Imported with Point filter, No Mipmaps, Max Size 2048, Wrap Repeat |
| 25042_Perseverance.glb (Mars 2020 Perseverance Rover) | NASA / JPL-Caltech (UNVERIFIED - confirm catalog ID) | UNVERIFIED - check before submission | NASA Media Usage Guidelines / Public Domain (UNVERIFIED - check before submission) | 2026-11-14 | Assets/Prefabs/Prop_Rover.prefab; placed near Solar Array on Mars surface | Scaled uniformly to ~3.0m length, PSX Lit material conversion, BoxCollider added (199,309 tris; recommend decimation in Blender) |
| Mars MOLA / HiRISE Heightmap (Pending RAW Import) | NASA / USGS Astrogeology | TO CONFIRM when added | TO CONFIRM when added | TO CONFIRM | BackdropTerrain (Outpost/Import NASA Heightmap (RAW)) | Pending user RAW export (GDAL gdal_translate -of ENVI) |

## Corridor kit

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| SciFi Corridor PRO LITE (v1.0.0) | hopefulsham (source PBR textures: Poly Haven) | https://hopefulsham.itch.io/scifi-modular-corridor-pro | Commercial & non-commercial projects without attribution; no resale/redistribution/AI training (Poly Haven source textures CC0 1.0) | 2026-11-14 | Assets/Prefabs/Interior/Corridor_Straight, Corridor_Doors, Corridor_End, Prop_Crate, Prop_Locker, Prop_Console | Used FBX only (GLB skipped); Point-filtered textures; PSX shader conversion preserving emissives; separated DoorLeaf sliding mesh; replaced panel_grey/panel_white with clean PSX station plating |
| PSX Space Station Interior - Free Sample (6 Models + Atlas) | Lowbit Lab | https://lowbit-lab.itch.io/ (Lowbit Lab on itch.io - verify exact pack URL) | Royalty-free commercial & non-commercial games/apps; no resale/redistribution of raw assets (per README.txt) | 2026-11-15 | Assets/Prefabs/Interior/Prop_CryoPod, Prop_ControlConsoleWide, Prop_SpaceSuitRack, Prop_MaintenanceRobot, Prop_AirlockHatchRound, Corridor_Corner_PSX + sliced T_Station_* interior/hull textures | Point-filtered 128x128 atlas & PSX Lit vertex-color conversion; upright axis alignment (-90 X); sliced tiles for wall/floor/ceiling/hull base textures |
| PSX Style Satellite Radio | Third-party PSX model pack (Assets/Models/psx-style-satellite-radio - confirm author before submission) | UNVERIFIED - check before submission | UNVERIFIED - check before submission | 2026-11-15 | Assets/Prefabs/Interior/Prop_SatelliteRadio.prefab (Command Module desk PF_Radio & Mars surface DSN_FieldRadio) | Extracted Satellite Radio.fbx from source zip, scaled to ~0.48m desk span, converted textures to Point-filtered PSX Lit |
| Radiation Hazard Sign (Assets/Models/radiation_sign.glb) | Third-party 3D model (confirm author before submission) | UNVERIFIED - check before submission | UNVERIFIED - check before submission | 2026-11-15 | Assets/Prefabs/Prop_RadiationSign.prefab (Mars surface POI_RadiationPost + perimeter hazard signs) | Baked embedded glTF texture to Point-filtered PNG (T_RadiationSign.png), scaled to 2.05m, converted to PSX Lit |

## UI icons

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Neon Command UI Kit - Free Sampler (Arctic colorway) | gen-xia | https://gen-xia.itch.io/neon-command-ui-kit | Commercial & non-commercial projects permitted; modify/recolor/crop allowed; no standalone resale (AI-assisted per LICENSE.txt) | 2026-11-14 | Assets/Art/UI/Icons/ (12 copied 128x128 UI icons) + Terminal HUD & Apps via IconLibrary.asset | Renamed to Icon_* and registered in IconLibrary.asset |

## Fonts

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Terminal & HUD Typography | TextMeshPro / Unity LegacyRuntime / Google Fonts (if ShareTech/Roboto/Orbitron installed — UNVERIFIED exact TTF source) | Check Assets/Fonts or FontTheme assets | SIL OFL 1.1 / Unity Built-in (UNVERIFIED - check before submission) | 2026-11-14 | Assets/Art/UI/TerminalTheme.asset, OutpostFlowCanvas, RoomHudCanvas | No |

## Sound effects

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Warning.wav (Facility Hazard Alert Sting) | Project Audio Import (Exact Author/Source URL UNVERIFIED — update before submission) | Assets/Sounds/VOX/Warning.wav (origin URL UNVERIFIED) | UNVERIFIED - check before submission (verify original SFX license) | 2026-11-15 | Assets/Sounds/VOX/Warning.wav -> baked lightly via Sting preset to Assets/Audio/Announcer/Processed/alert_sting.wav | Original untouched; baked copy trimmed, lightly reverbed (-1.5 dBFS mono 22050 Hz) |
| Outpost Atmosphere & Door SFX (SpaceShip.mp3, MarsSound.wav, DoorOpen.wav, CloseDoor.wav, footsteps) | Project Audio Import (Exact Author/Source URL UNVERIFIED — update before submission) | Assets/Sounds/Atmosphere/ (origin URL UNVERIFIED) | UNVERIFIED - check before submission (verify original SFX license) | 2026-11-15 | AudioManager in Outpost.unity: SpaceShip.mp3 (interior loop), MarsSound.wav (exterior Mars loop), DoorOpen.wav / CloseDoor.wav, exterior stone footsteps | Originals untouched; configured Unity AudioImporter Vorbis compression |

## Generated audio

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| ElevenLabs Voice Clips & Facility Announcer Lines | ElevenLabs Speech Synthesis (Voices: Lauren, Jessica) / Project Announcer Recordings | https://elevenlabs.io/ (License/Tier UNVERIFIED) | UNVERIFIED - check before submission (check ElevenLabs plan commercial terms) | 2026-11-15 | Assets/Sounds/VOX/ (8 ElevenLabs test voice clips + pending 11 canonical AnnouncerDatabase lines) | Originals kept intact and unmodified |

## Processed audio (baked in-project)

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| VOX FX Offline DSP Chain & Baked Facility Audio | In-project pure-C# DSP (VoxFxDsp.cs / VoxFxBaker.cs) | Internal project asset (Zero external plugins / zero AudioMixer dependency) | Original project work | 2026-11-15 | Assets/Audio/Announcer/Processed/ (22050 Hz mono PCM -> Vorbis 0.45): telephone bandpass, S&H bitcrush, 70Hz ring mod, tanh saturation, dark echo, Schroeder hall reverb, radio squelch | Offline idempotent bake with VOX Classic, Heavy Echo, Clean Radio, and Sting presets |

## Packages

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| glTFast (com.unity.cloud.gltfast 6.10.1) | Unity Technologies / Andreas Atteneder | https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.10/manual/index.html | Unity Companion License / Apache-2.0 | 2026-11-14 | Packages/manifest.json (imports 25042_Perseverance.glb) | No |
| Universal RP (com.unity.render-pipelines.universal 17.5.0) & Input System | Unity Technologies | https://unity.com/ | Unity Companion License | 2026-11-14 | Active render pipeline and FPS/terminal input | No |

## Generated in-project

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Procedural MarsPlayTerrain, BackdropTerrain & 3 Low-Poly Rock Variants | In-project script (MarsTerrainGenerator.cs) | Internal project asset | Original project work | 2026-11-14 | Outpost scene exterior Mars terrain, BackdropTerrain ring, Rock_Small/Medium/Large prefabs | Generated via Outpost/Generate Mars Terrain |
| PSX Shaders & 64x64 Procedural Surface/Terrain Textures | In-project script (PSXMaterialApplier.cs, Assets/Shaders/*) | Internal project asset | Original project work | 2026-11-14 | Assets/Shaders/ (PSXLit, PSXUnlit, PSXLit_Cutout, PSXParticle, PSXPost, PSXTerrain, PSXSkyGradient) & Assets/Art/PSX/ | Generated procedurally at 64x64 with Point filtering |
| 8 Matching 128x128 Procedural UI Icons | In-project script (TerminalIconAssetBuilder.cs) | Internal project asset | Original project work | 2026-11-14 | Assets/Art/UI/Icons/ (Icon_Oxygen, Icon_Water, Icon_Food, Icon_Signal, Icon_Log, Icon_Sources, Icon_Help, Icon_Goal) | Generated in matching teal/light-blue/yellow/coral 6px line style |

## Tools and AI assistance

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Unity Editor (6000.x), Unity MCP (com.coplaydev.unity-mcp), Python & Antigravity AI Assistant | Unity Technologies / CoplayDev / Google DeepMind Antigravity | https://unity.com/ | Unity Personal / MIT / Assistant workflow | 2026-11-14 | Scene assembly, editor automation, shader/UI integration & verification | AI-assisted implementation and integration scripts |

## Scientific & Fact Sources (In-Game Sources App & Debrief)

| Source ID | Title | Organization | URL | License | Supports |
|---|---|---|---|---|---|
| `nasa-jpl-opportunity-recovery` | Six Things About Opportunity's Recovery Efforts | NASA/JPL | https://www.jpl.nasa.gov/news/six-things-about-opportunitys-recovery-efforts/ | UNVERIFIED - check before submission | Supports Dust storm event (fact_dust_storm): why sunlight loss halts solar-powered Mars hardware. |
| `nasa-jpl-opportunity-hunkers` | Opportunity Hunkers Down During Dust Storm | NASA/JPL | https://www.jpl.nasa.gov/news/opportunity-hunkers-down-during-dust-storm/ | UNVERIFIED - check before submission | Supports Dust storm event (fact_dust_storm): how Opportunity entered safe mode during planet-wide dust storms. |
| `nasa-ntrs-mer-dust` | MER Opportunity dust-storm recovery operations and implications for future Mars surface missions | NASA NTRS | https://ntrs.nasa.gov/citations/20210015954 | UNVERIFIED - check before submission | Supports Panel dust build-up (fact_panel_dust): cutting rover power loads to preserve battery before storms. |
| `nasa-artemis2-sun` | To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun | NASA | https://science.nasa.gov/missions/artemis/artemis-2/to-protect-artemis-ii-astronauts-nasa-experts-keep-eyes-on-sun/ | UNVERIFIED - check before submission | Supports Solar particle event (fact_solar_flare): monitoring solar flares so crews can shelter in time. |
| `nasa-svs-radiation` | How NASA Will Protect Astronauts From Space Radiation | NASA SVS | https://svs.gsfc.nasa.gov/vis/a010000/a013200/a013275/script_27781_00.html | UNVERIFIED - check before submission | Supports Solar particle event (fact_solar_flare): shielding techniques and crew storm shelters. |
| `hassler-2014-rad` | Mars' Surface Radiation Environment Measured with the Mars Science Laboratory's Curiosity Rover (Hassler et al., Science) | Science / SwRI / NASA | https://doi.org/10.1126/science.1244797 | UNVERIFIED - check before submission | Supports Baseline radiation (fact_radiation_baseline & BaselineSurfaceDoseMsvPerSol): ~0.67 mSv/day measured by Curiosity. |
| `iss-water-98` | NASA Achieves Water Recovery Milestone on International Space Station (98% ECLSS) | NASA / ABC News | https://www.nasa.gov/missions/station/iss-research/nasa-achieves-water-recovery-milestone-on-international-space-station/ | UNVERIFIED - check before submission | Supports Water recycler fault (fact_water_recycler), ~98% ISS recycling & ~1 gal/day water use (ABC report: https://abc7.com/13433546/). |
| `nasa-veggie-lettuce` | Astronauts Take First Bites of Lettuce Grown in Space | Scientific American (NASA Veggie) | https://www.scientificamerican.com/article/astronauts-take-first-bites-of-lettuce-grown-in-space/ | UNVERIFIED - check before submission | Supports Greenhouse blight (fact_greenhouse): growing and harvesting fresh crops aboard the ISS. |
| `npr-veggie-mood` | The Salad Frontier: Why Astronauts Need To Grow Lettuce In Space | NPR | https://www.npr.org/blogs/thesalt/2014/06/10/320321553/the-salad-frontier-why-astronauts-need-to-grow-lettuce-in-space | UNVERIFIED - check before submission | Supports Greenhouse blight (fact_greenhouse): fresh vegetables support astronaut morale in isolated missions. |
| `esa-mars-facts` | Facts about Mars | ESA | https://www.esa.int/content/view/full/200994 | UNVERIFIED - check before submission | Supports ambient announcer facts f_gravity (~38% Earth gravity) and f_moons (Phobos & Deimos). |
| `esa-meet-mars` | Meet Mars | ESA | https://m.esa.int/Our_Activities/Human_and_Robotic_Exploration/Exploration/ExoMars/Meet_Mars | UNVERIFIED - check before submission | Supports Cold snap (fact_cold_snap) and ambient f_cold (-133 C winter pole to +27 C summer). |
| `nasa-mars-fact-sheet` | Mars Fact Sheet | NASA NSSDCA | https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html | UNVERIFIED - check before submission | Supports CO2 scrubber clog (fact_co2_scrubber: ~95% CO2), ambient f_sol (24 h 39 m) & f_cold. |
| `space-mars-atmosphere` | Mars' atmosphere: Facts about composition and climate (MOXIE) | Space.com | https://www.space.com/16903-mars-atmosphere-climate-weather.html | UNVERIFIED - check before submission | Supports CO2 scrubber clog (fact_co2_scrubber): 95% CO2 atmosphere and Perseverance MOXIE oxygen experiment. |
| `esa-light-time` | Spacecraft event time vs. Earth receive time | ESA Mars Express blog | https://blogs.esa.int/mex/?p=257 | UNVERIFIED - check before submission | Supports Message from Earth (fact_earth_message) and ambient f_signal: 4-24 min one-way light delay. |
| `sis-habitat` | Building a space habitat in the classroom | Science in School | https://scienceinschool.org/2011/issue19/habitat | UNVERIFIED - check before submission | Supports Airlock dust / impact (fact_airlock_dust): protecting crew from radiation, micrometeorites, dust, vacuum, cold. |
| `abc-chapea` | NASA says 98% of astronauts' urine, sweat can be recycled (also mentions CHAPEA Mars-analog study) | ABC News | https://abc7ny.com/post/nasa-recycle-urine-sweat/13433546/ | UNVERIFIED - check before submission | Supports Crew tension (fact_crew_tension): year-long Mars habitat isolation study (primary NASA CHAPEA page found via search). |
| `usgs-jezero-dem` | Mars 2020 Jezero crater landing site products | USGS Astrogeology | https://astrogeology.usgs.gov/search/map/mars-2020-jezero-crater-landing-site-products | UNVERIFIED - check before submission | Supports Mars surface terrain exploration backdrop at Outpost Helios (Jezero Crater). |
| `nasa-3d-mars` | NASA 3D Resources: Mars texture (Viking, USGS/JPL) | NASA | https://science.nasa.gov/3d-resources/mars | UNVERIFIED - check before submission | Supports Mars global color texture and orbital backdrop on the Title Screen. |

