# Mars Outpost: Last Shift

**One-line tagline:** Step into a retro-futuristic Martian habitat, manage life support through dust storms and solar flares, and keep four astronauts alive until rescue on Sol 7.

**Play it:** [Play on itch.io](https://yassir001.itch.io/mars-outpost-last-shift) *(WebGL link — `https://yassir001.itch.io/mars-outpost-last-shift`)*

## Summary

*Mars Outpost: Last Shift* is an interactive first-person Mars habitat simulator and command terminal built for **NASA Space Apps Challenge 2026** (Challenge: **Build a Junior Astronaut Mission Trainer**) by **Team Pluto Outpost** (solo developer: **Yassir**).
You play as **Commander Alex Vale**, waking up injured after a Martian shift change to walk the base, suit up for EVAs, and run the **OUTPOST OS** terminal.
Across seven Martian sols, you balance solar battery power, breathable air, recycled water, greenhouse crops, radiation shielding, and crew morale using lessons grounded in real NASA, ESA, and USGS mission data.

## How It Answers the Challenge

The game turns real Martian survival constraints into hands-on student decisions across four interconnected trade-offs:

- **Power vs. Battery Life:** Players split limited daytime solar electricity (`60` max base output, reduced by panel dust and dust storms) across **Life Support**, **Greenhouse**, **Heating**, and **Comms**. Draining the battery (`0%`) risks random subsystem failures overnight; dropping heating below `50%` causes cold injury and morale loss.
- **Closed-Loop Life Support (Air & Water):** Keeping the CO2 scrubber and water recycler healthy demonstrates how crews recover ~98% of their water and scrub toxic CO2 instead of relying on resupply.
- **Radiation Shielding:** Baseline Martian surface radiation (`~0.67 mSv/sol`, adapted from Curiosity RAD measurements) plus solar particle events force players to reinforce regolith/water shielding and limit risky EVAs.
- **Food & Crew Morale:** Running indoor LED crops and choosing when to rest, eat, treat injuries, or ration supplies connects nutrition, psychology, and mission survival.
- **Learn-as-You-Play Science:** Every crisis triggers a plain-language **NASA Science Fact** card, and the in-terminal **Sources** app (plus the post-mission **"What NASA Knows"** debrief) separates `REAL`, `ADAPTED`, and `GAME` numbers with direct reference links.

## Features

- **3D First-Person Outpost & Jezero-Inspired Surface:** Explore the Command Module, Hub Corridor, Medbay, Greenhouse, Storage, and Airlock, or step outside onto the Martian surface past solar arrays, radiation posts, a DSN field radio, and a Perseverance-style rover.
- **Command Terminal (`OUTPOST OS` + Classic View):** Switch anytime between draggable desktop windows and a classic full-screen dashboard hosting the same 9 apps: **Mission Control**, **Power Grid**, **Air & Water**, **Food**, **Shield**, **Crew**, **Communications**, **Commanders Log**, and **Sources**.
- **7-Sol Simulation Engine (`Outpost.Sim`):** Deterministic, seeded 7-sol turn simulation with `3` Action Points per sol, crew specialist bonuses (`Medic`, `Engineer`, `Pilot`, `Scientist`), `10` mission events (including global dust storms and solar flares), and `S`/`A`/`B`/`C`/`F` mission grading.
- **3-Step Daily Shift Checklist (`DailyDutyTracker`):** Prevents skipping sols by requiring daily event resolution, command action/power tuning, and an in-base or surface station inspection.
- **EVA Suit & Airlock Safety Loop:** Put on or remove your suit at the rack (`E` or `T`) with a visor overlay and cosmetic suit HUD; outward airlock cycling requires a suit, and suit removal is blocked outdoors or during depressurization.
- **Bilingual English / Arabic Support:** Pre-menu `English / Arabic` pill slider (`LanguageBootLoader` & `ArabicShaper`) localizes UI, events, glossary, and terminal panels without restart.
- **PSX Aesthetic, VOX Audio & Accessibility:** Configurable PSX lighting/resolution/flicker reduction, facility announcer with filtered voiceovers (`VoxFxDsp`) and subtitles, look sensitivity slider, larger text toggle, and volume/VOX mute controls.

## Controls

All bindings below reflect the runtime scripts (`FirstPersonController.cs`, `FirstPersonRoomController.cs`, `SuitController.cs`, `TerminalController.cs`, `MissionFlowController.cs`, `MainMenuController.cs`):

| Context | Input | Action |
|---|---|---|
| **Movement (3D Outpost / Mars Surface)** | `W` `A` `S` `D` | Move |
| | `Left Shift` | Sprint |
| | `Space` | Jump |
| | `Left Control` | Crouch |
| | `Mouse` | Look around (sensitivity adjustable in Pause Menu) |
| | `Right Click` (`Mouse1`) | Camera zoom |
| | `E` | Interact (enter Terminal, Suit Rack, Doors, Airlock, Station Inspection) |
| | `T` | Toggle EVA Suit on/off (inside pressurized habitat only) |
| **Command Terminal** | `Mouse` | Click apps/buttons, drag sliders, move Desktop OS windows |
| | `1` – `9` | Open Terminal apps (1–9) |
| | `E` | Trigger `End Sol` |
| | `Enter` / `Numpad Enter` | Confirm modal or continue card |
| | `Esc` | Close open modal, or leave Terminal back to 3D room |
| **Pre-Menu & Flow Screens** | `Left` / `Right Arrow` + `Enter` | Choose English/Arabic on language gate and continue |
| | `Esc` | Open/close Pause Menu or return from Credits/Settings |
| | `Space` / `Enter` / `Click` | Advance intro card / End-Sol summary black card |
| **Editor / Development Build Only** | `` ` `` (Backquote) | Toggle developer debug panel |

## How to Run in the Unity Editor

- **Unity Version:** `6000.5.5f1` (`ProjectSettings/ProjectVersion.txt`)
- **Render Pipeline:** Universal Render Pipeline (`URP 17.5.0`) + New Input System (`com.unity.inputsystem`)
- **Build Scene Order (`ProjectSettings/EditorBuildSettings.asset`):**
  1. `Assets/Scenes/MainMenu.unity` *(starting scene — includes pre-menu language gate and main menu)*
  2. `Assets/Scenes/Outpost.unity` *(main 3D outpost + terminal + Mars surface scene)*
  3. `Assets/Scenes/Terminal.unity` *(standalone terminal scene)*

1. Open the repository folder in **Unity Hub** using Unity **6000.5.5f1**.
2. Open **`Assets/Scenes/MainMenu.unity`**.
3. Press **Play** in the Unity Editor to test the full flow from language selection through mission debrief.

## How to Build WebGL

The project includes an automated WebGL release pipeline in `Assets/Scripts/Editor/WebGLBuildPipeline.cs`:

1. In Unity Editor, click **`Outpost > Build > Release WebGL`** (or **`Outpost > Build > Release WebGL and Run`**).
2. This configures `1280x720` WebGL PlayerSettings (`OpenGLES3`, product name `"Mars Outpost - Last Shift"`), builds `MainMenu.unity`, `Outpost.unity`, and `Terminal.unity` into `Builds/WebGL/`, writes `Builds/BUILD_NOTES.txt`, and packages an itch.io-ready archive at `Builds/MarsOutpost_WebGL.zip` (`index.html` at the zip root).
3. Alternatively, open **File > Build Profiles**, select **WebGL**, ensure all three scenes in `EditorBuildSettings.asset` are checked, and click **Build**.

## Project Structure

- `Assets/Scenes/` — `MainMenu.unity`, `Outpost.unity`, `Terminal.unity`
- `Assets/Scripts/Sim/` — Pure C# simulation engine (`OutpostSimulation`, `SimConfig`, `EventSystem`, `Localization`, `ArabicShaper`, `BalanceRunner`)
- `Assets/Scripts/Runtime/` — 3D room, suit, airlock, audio/VOX DSP, PSX rendering, UI theme, Desktop OS windows, and mission flow controllers
- `Assets/Scripts/Editor/` — Scene/UI/suit/VOX/WebGL build and QA automation menu tools (`Outpost/*`)
- `Assets/Tests/EditMode/` — NUnit EditMode automated tests
- `Assets/Resources/` — `SourcesDatabase.asset`, `FactDatabase.asset`, `Facts/*.asset`, `GlossaryDatabase.asset`, `AnnouncerDatabase.asset`, `IntroSequenceData.asset`, `OutpostConfig.asset`
- `Assets/Prefabs/` — Interior modules, props, suit rack, rover, rocks, Mars sphere
- `Assets/Art/`, `Assets/Models/`, `Assets/Materials/`, `Assets/Shaders/`, `Assets/Audio/`, `Assets/Sounds/`, `Assets/Fonts/` — Visuals, PSX shaders, audio/VOX assets, and fonts
- `Assets/Credits/` — `ASSET_SOURCES.md`
- `docs/` — Player/teacher guide, science & sources, submission copy, itch.io page text, and demo video script

## Testing & Simulation Verification

- **EditMode Tests (`Assets/Tests/EditMode/`):** Open **Window > General > Test Runner > EditMode** in Unity Editor and click **Run All**:
  - `OutpostSimTests.cs` — resource clamping, solar/dust/battery math, oxygen/water/food/heating penalties, fixed Sol 1–2 events and seeded determinism, AP/role bonuses, win/lose grading, `BalanceRunner` target win-rate check, `SessionSnapshot` parity, and single intro card validation.
  - `FactAndSourceIntegrityTests.cs` — checks zero placeholder prefixes on facts/sources, all fact/source/announcer IDs resolve, 20 headless missions + announcer scheduler run without exceptions, Arabic translations/shaping integrity, no citation tags inside dialogue/POI lines, and look sensitivity clamping/persistence.
  - `SuitAndEndSolTests.cs` — End-Sol day-count formatting (`Sols 1–7`), localization key checks, pressurized suit removal rules, and airlock outward suit checks.
  - `VoxDspTests.cs` — offline announcer DSP preset output integrity, silence trimming, and idempotent baking.
- **Headless Balance Runner (`Assets/Scripts/Sim/BalanceRunner.cs`):** `BalanceRunner.RunFullReport(missionCount: 1000)` runs `1000` seeded missions each for `Careless`, `Average`, and `Optimal` bot strategies in pure C# to verify survivability and mission balance.

## Credits & Science Sources

- Third-party assets, audio, fonts, packages, and license status: see [`CREDITS.md`](CREDITS.md) (generated from [`Assets/Credits/ASSET_SOURCES.md`](Assets/Credits/ASSET_SOURCES.md)).
- Scientific constants, fact cards, and NASA/ESA/USGS citations: see [`docs/SCIENCE_AND_SOURCES.md`](docs/SCIENCE_AND_SOURCES.md).
- Project license notice: see [`LICENSE.md`](LICENSE.md).

## AI & Tools Disclosure

- **Engine & Tooling:** Built in Unity `6000.5.5f1` using **Unity MCP** (`com.coplaydev.unity-mcp`) and **AI coding assistants** (Google DeepMind Antigravity) to help write scripts, editor builders, tests, and documentation, and plan system architecture.
- **Voice & Audio:** Facility announcer voice clips were generated using **ElevenLabs** text-to-speech and processed offline inside the project with custom C# DSP (`VoxFxDsp.cs` / `VoxFxBaker.cs`).
- **Direction & Integration:** Game design decisions, scientific framing, asset curation, Arabic review, and final integration were directed by **Yassir** (Team **Pluto Outpost**).

## Disclaimer

Not affiliated with or endorsed by NASA.
