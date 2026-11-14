# ASSET_SOURCES & Project Credits

Master record of every external asset, texture, 3D model, font, UI icon, package, procedural generator, and AI/development tool used in this project.
Generated cleanly from shared `SourcesDatabase` (`Assets/Resources/SourcesDatabase.asset`) — keep updated whenever an asset or package is added.

## NASA/USGS data and models

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Mars Global Color Map (Assets/Models/Mars.jpg) | NASA / USGS Astrogeology (UNVERIFIED - check source exact attribution) | UNVERIFIED - check before submission | Public Domain (assumed NASA/USGS — UNVERIFIED - check before submission) | 2026-11-14 | MarsSphere prefab (TitleScreen backdrop & Command Module sky window) | Imported with Point filter, No Mipmaps, Max Size 2048, Wrap Repeat |
| 25042_Perseverance.glb (Mars 2020 Perseverance Rover) | NASA / JPL-Caltech (UNVERIFIED - confirm catalog ID) | UNVERIFIED - check before submission | NASA Media Usage Guidelines / Public Domain (UNVERIFIED - check before submission) | 2026-11-14 | Assets/Prefabs/Prop_Rover.prefab; placed near Solar Array on Mars surface | Scaled uniformly to ~3.0m length, PSX Lit material conversion, BoxCollider added (199,309 tris; recommend decimation in Blender) |
| Mars MOLA / HiRISE Heightmap (Pending RAW Import) | NASA / USGS Astrogeology | TO CONFIRM when added | TO CONFIRM when added | TO CONFIRM | BackdropTerrain (Outpost/Import NASA Heightmap (RAW)) | Pending user RAW export (GDAL gdal_translate -of ENVI) |

## Corridor kit

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| SciFi Corridor PRO LITE (v1.0.0) | hopefulsham (source PBR textures: Poly Haven) | https://hopefulsham.itch.io/scifi-modular-corridor-pro | Commercial & non-commercial projects without attribution; no resale/redistribution/AI training (Poly Haven source textures CC0 1.0) | 2026-11-14 | Assets/Prefabs/Interior/Corridor_Straight, Corridor_Doors, Corridor_End, Prop_Crate, Prop_Locker, Prop_Console | Used FBX only (GLB skipped); Point-filtered textures; PSX shader conversion preserving emissives; separated DoorLeaf sliding mesh |

## UI icons

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Neon Command UI Kit - Free Sampler (Arctic colorway) | gen-xia | https://gen-xia.itch.io/neon-command-ui-kit | Commercial & non-commercial projects permitted; modify/recolor/crop allowed; no standalone resale (AI-assisted per LICENSE.txt) | 2026-11-14 | Assets/Art/UI/Icons/ (12 copied 128x128 UI icons) + Terminal HUD & Apps via IconLibrary.asset | Renamed to Icon_* and registered in IconLibrary.asset |

## Fonts

| Name | Author / Organization | Source URL | License | Date Added | Where Used in Project | Modified? |
|---|---|---|---|---|---|---|
| Terminal & HUD Typography | TextMeshPro / Unity LegacyRuntime / Google Fonts (if ShareTech/Roboto/Orbitron installed — UNVERIFIED exact TTF source) | Check Assets/Fonts or FontTheme assets | SIL OFL 1.1 / Unity Built-in (verify before submission) | 2026-11-14 | Assets/Art/UI/TerminalTheme.asset, OutpostFlowCanvas, RoomHudCanvas | No |

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

## Scientific & Fact Sources (In-Game Sources App & POIs)

| Source ID | Title | Organization | URL | Used By Constants / Events / POIs |
|---|---|---|---|---|
| `src_dust` | [Placeholder] Solar Array Dust Accumulation | NASA JPL / Mars Exploration | https://science.nasa.gov/mission/mars-exploration-rovers/ | DustAccumulationPerSol, evt_panel_dust |
| `src_co2` | [Placeholder] Cabin CO2 Scrubbing & ECLSS | NASA Human Spaceflight | https://www.nasa.gov/missions/station/eclss/ | OxygenGenAtFull, LowScrubberThreshold, evt_scrubber_clog |
| `src_storm` | [Placeholder] Martian Dust Storms & Optical Depth | NASA Mars Science | https://science.nasa.gov/resource/mars-dust-storms/ | StormSolarFactor, StormExtraDustPerSol, evt_dust_storm |
| `src_rad` | [Placeholder] Mars Surface Radiation & SPE | NASA / Curiosity RAD | https://science.nasa.gov/mission/msl-curiosity/radiation-assessment-detector/ | BaselineSurfaceDoseMsvPerSol, SolarFlareUnshieldedDoseMsv, evt_solar_flare |
| `src_water` | [Placeholder] Closed-Loop Water Recovery | NASA MSFC / ISS | https://www.nasa.gov/reference/environmental-control-and-life-support-systems/ | WaterGenAtFull, WaterFixedLoss, evt_water_recycler |
| `src_crops` | [Placeholder] Space Crop & Veggie Systems | NASA KSC | https://www.nasa.gov/exploration-research-and-technology/growing-plants-in-space/ | FoodGenAtFull, GreenhouseOxygenBonus, evt_greenhouse_blight |
| `src_cold` | [Placeholder] Martian Thermal Environment | NASA GSFC Mars Fact Sheet | https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html | HeatingSafeThreshold, ColdHealthPenalty, evt_cold_snap |
| `src_psych` | [Placeholder] Isolated Crew Behavioral Health | NASA JSC CHAPEA / HERA | https://www.nasa.gov/humans-in-space/chapea/ | LowMoraleThreshold, RationMoraleCost, evt_crew_tension |
| `src_comms` | [Placeholder] Earth-Mars Light-Time Delay | NASA Deep Space Network | https://eyes.nasa.gov/apps/dsn-now/dsn.html | CommsMaxPower, evt_earth_message |
| `src_shield` | [Placeholder] Regolith Shielding & Micrometeoroids | NASA Moon to Mars | https://www.nasa.gov/humans-in-space/radiation/ | ShieldingMaxReduction, ReinforceShieldingGain, evt_micrometeorite |
| `nasa-perseverance-model` | Mars 2020 Perseverance Rover 3D Model (Placeholder Fact) | NASA / JPL-Caltech | https://science.nasa.gov/resource/perseverance-rover-3d-model/ (UNVERIFIED - check before submission) | Exterior Prop_Rover POI near Solar Array |

