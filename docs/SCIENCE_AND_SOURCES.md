# Science & Sources — Mars Outpost: Last Shift

This document records the simulation constants table shown inside the terminal's **Sources** app (`SourcesAppWindow.cs`), every in-game **NASA Science Fact** card (`Assets/Resources/Facts/*.asset`), and the NASA, ESA, USGS, and peer-reviewed references behind them (`Assets/Resources/SourcesDatabase.asset`).

> **About simplified game numbers:** Resource tanks, battery percentages, crop output, and action costs in *Mars Outpost: Last Shift* are simplified gameplay units designed so students can reason clearly about trade-offs over a 7-sol shift—not exact NASA flight hardware telemetry or engineering sizing. Real astrophysical and biological benchmarks (such as Mars sol length, Curiosity surface radiation dose, ~95% CO2 atmosphere, ISS 98% water recovery, and Earth–Mars light-time delay) are separated from game balancing using the `REAL`, `ADAPTED`, and `GAME` status tags below.
>
> **Verification note:** In [`Assets/Credits/ASSET_SOURCES.md`](../Assets/Credits/ASSET_SOURCES.md), external URL license/copyright statuses carry `UNVERIFIED - check before submission`, and `iss-water-98` lists both a NASA article URL and an ABC News URL (`https://abc7.com/13433546/`) pending final verification of the primary NASA page.

---

## 1. Real Data & Simulation Constants Table

Copied directly from `SourcesAppWindow.RebuildConstantsTableIfNeeded()` (`Assets/Scripts/Runtime/Terminal/Apps/SourcesAppWindow.cs`), `SimConfig.cs`, and `Localization.cs`:

| Constant / Metric | Value | Status | Source ID |
|---|---|---|---|
| Baseline Surface Radiation | `0.67 mSv / sol` (per Earth day avg; `BaselineSurfaceDoseMsvPerSol = 0.67f`) | `ADAPTED` | `hassler-2014-rad` |
| Mars Sol Length | `24 h 39 m` (1 sol per turn; `TotalSols = 7` sols) | `REAL` | `nasa-mars-fact-sheet` |
| Earth-Mars Message Delay | `~12 min` Sol 1 (`12–21 min` in Comms; real `4–24 min`) | `ADAPTED` | `esa-light-time` |
| Crew Water Use & ISS Recycling | `~1 gal/day` & `98%` ISS recovery (sim: `1.5%` tank/crew/sol; `WaterUsePerCrew = 1.5f`) | `REAL` | `iss-water-98` |
| Mars Atmosphere Composition | `~95% CO2` (MOXIE O2 test) | `REAL` | `nasa-mars-fact-sheet` |
| Stocks, Dust Storm Loss, Shielding & Rates | Start O2/H2O/Food `70`/`70`/`60%`, Storm `40%` solar (`StormSolarFactor = 0.40f`), Shield max `-50%` dose (`ShieldingMaxReduction = 0.50f`) | `GAME` | `(sources-inspired)` |

---

## 2. In-Game Fact Cards & Linked Sources

Each of the 11 `FactCard` entries in `Assets/Resources/FactDatabase.asset` appears during its corresponding mission event, on the HUD fact banner, or in the terminal/debrief **What NASA Knows** screen:

### 1. Martian Global Dust Storms (`fact_dust_storm`)
- **In-Game Card Text:** In 2018 a planet-wide dust storm cut off sunlight. Opportunity, a solar-powered rover, lost power and fell silent.
- **Linked Event / Constant:** `evt_dust_storm` / `StormSolarFactor`
- **Sources:**
  - *Six Things About Opportunity's Recovery Efforts* — NASA/JPL — https://www.jpl.nasa.gov/news/six-things-about-opportunitys-recovery-efforts/ *(License status: `UNVERIFIED`)*
  - *Opportunity Hunkers Down During Dust Storm* — NASA/JPL — https://www.jpl.nasa.gov/news/opportunity-hunkers-down-during-dust-storm/ *(License status: `UNVERIFIED`)*

### 2. Solar Particle Events (`fact_solar_flare`)
- **In-Game Card Text:** In a solar particle event, crews shelter in the most shielded part of their spacecraft until radiation drops.
- **Linked Event / Constant:** `evt_solar_flare` / `ShieldingMaxReduction`
- **Sources:**
  - *To Protect Artemis II Astronauts, NASA Experts Keep Eyes on Sun* — NASA — https://science.nasa.gov/missions/artemis/artemis-2/to-protect-artemis-ii-astronauts-nasa-experts-keep-eyes-on-sun/ *(License status: `UNVERIFIED`)*
  - *How NASA Will Protect Astronauts From Space Radiation* — NASA SVS — https://svs.gsfc.nasa.gov/vis/a010000/a013200/a013275/script_27781_00.html *(License status: `UNVERIFIED`)*

### 3. Power Saving Before Storms (`fact_panel_dust`)
- **In-Game Card Text:** Before the 2018 storm hit, Opportunity's team cut the rover's power use to save battery charge.
- **Linked Event / Constant:** `evt_panel_dust` / `PanelMaintenanceGain`
- **Source:**
  - *MER Opportunity dust-storm recovery operations and implications for future Mars surface missions* — NASA NTRS — https://ntrs.nasa.gov/citations/20210015954 *(License status: `UNVERIFIED`)*

### 4. ISS Water Recycling (98%) (`fact_water_recycler`)
- **In-Game Card Text:** On the space station, crews recover about 98% of their water by recycling. Each person uses roughly a gallon a day.
- **Linked Event / Constant:** `evt_water_recycler` / `WaterGenAtFull`
- **Source:**
  - *NASA Achieves Water Recovery Milestone on International Space Station (98% ECLSS)* (`iss-water-98`) — NASA / ABC News — https://www.nasa.gov/missions/station/iss-research/nasa-achieves-water-recovery-milestone-on-international-space-station/ and secondary link https://abc7.com/13433546/ *(Primary NASA URL verification and license status: `UNVERIFIED`)*

### 5. Space Lettuce & Crew Morale (`fact_greenhouse`)
- **In-Game Card Text:** Astronauts have grown and eaten lettuce on the space station. Fresh plants add food and may lift the crew's mood.
- **Linked Event / Constant:** `evt_greenhouse_blight` / `FoodGenAtFull`
- **Sources:**
  - *Astronauts Take First Bites of Lettuce Grown in Space* — Scientific American (NASA Veggie) — https://www.scientificamerican.com/article/astronauts-take-first-bites-of-lettuce-grown-in-space/ *(License status: `UNVERIFIED`)*
  - *The Salad Frontier: Why Astronauts Need To Grow Lettuce In Space* — NPR — https://www.npr.org/blogs/thesalt/2014/06/10/320321553/the-salad-frontier-why-astronauts-need-to-grow-lettuce-in-space *(License status: `UNVERIFIED`)*

### 6. Mars CO2 Air & MOXIE (`fact_co2_scrubber`)
- **In-Game Card Text:** Mars air is about 95% carbon dioxide. In 2021, an experiment on Perseverance made oxygen from it.
- **Linked Event / Constant:** `evt_scrubber_clog` / `OxygenGenAtFull`
- **Sources:**
  - *Mars' atmosphere: Facts about composition and climate (MOXIE)* — Space.com — https://www.space.com/16903-mars-atmosphere-climate-weather.html *(License status: `UNVERIFIED`)*
  - *Mars Fact Sheet* — NASA NSSDCA — https://nssdc.gsfc.nasa.gov/planetary/factsheet/marsfact.html *(License status: `UNVERIFIED`)*

### 7. Extreme Martian Temperatures (`fact_cold_snap`)
- **In-Game Card Text:** Mars is cold. Temperatures range from about minus 133°C at the winter pole to plus 27°C in summer.
- **Linked Event:** `evt_cold_snap`
- **Source:**
  - *Meet Mars* — ESA — https://m.esa.int/Our_Activities/Human_and_Robotic_Exploration/Exploration/ExoMars/Meet_Mars *(License status: `UNVERIFIED`)*

### 8. Year-Long Isolation Studies (`fact_crew_tension`)
- **In-Game Card Text:** NASA studies long isolation on Earth. In one study, four volunteers were selected to live in a Mars-like habitat for a year.
- **Linked Event:** `evt_crew_tension`
- **Source:**
  - *NASA says 98% of astronauts' urine, sweat can be recycled (also mentions CHAPEA Mars-analog study)* (`abc-chapea`) — ABC News / NASA — https://abc7ny.com/post/nasa-recycle-urine-sweat/13433546/ and primary NASA CHAPEA page https://www.nasa.gov/missions/analog-field-testing/chapea/first-mars-crew-completes-yearlong-simulated-red-planet-nasa-mission/ *(License status: `UNVERIFIED`)*

### 9. Earth-Mars Radio Light Delay (`fact_earth_message`)
- **In-Game Card Text:** Radio messages between Earth and Mars take about 4 to 24 minutes one way, so there are no live chats.
- **Linked Event:** `evt_earth_message`
- **Source:**
  - *Spacecraft event time vs. Earth receive time* — ESA Mars Express blog — https://blogs.esa.int/mex/?p=257 *(License status: `UNVERIFIED`)*

### 10. Habitat Shielding Hazards (`fact_airlock_dust`)
- **In-Game Card Text:** A habitat must protect its crew from radiation, tiny meteorites, dust, vacuum and extreme temperatures.
- **Linked Event / Constant:** `evt_micrometeorite` / `ShieldingMaxReduction`
- **Source:**
  - *Building a space habitat in the classroom* — Science in School — https://scienceinschool.org/2011/issue19/habitat *(License status: `UNVERIFIED`)*

### 11. Curiosity Surface Radiation (`fact_radiation_baseline`)
- **In-Game Card Text:** Curiosity measured about 0.67 millisieverts of radiation per day on Mars's surface, similar to levels on the space station.
- **Linked Constant:** `BaselineSurfaceDoseMsvPerSol` (`0.67 mSv/sol`)
- **Source:**
  - *Mars' Surface Radiation Environment Measured with the Mars Science Laboratory's Curiosity Rover (Hassler et al., Science)* — Science / SwRI / NASA — https://doi.org/10.1126/science.1244797 *(License status: `UNVERIFIED`)*

---

## 3. Additional Environment & Announcer Sources (`SourcesDatabase`)

- **`esa-mars-facts`** — *Facts about Mars* (ESA) — https://www.esa.int/content/view/full/200994 — Supports ambient announcer facts on Mars surface gravity (~38% of Earth) and moons Phobos and Deimos *(License status: `UNVERIFIED`)*.
- **`usgs-jezero-dem`** — *Mars 2020 Jezero crater landing site products* (USGS Astrogeology) — https://astrogeology.usgs.gov/search/map/mars-2020-jezero-crater-landing-site-products — Supports Jezero Crater setting context for the surface terrain backdrop *(License status: `UNVERIFIED`)*.
- **`nasa-3d-mars`** — *NASA 3D Resources: Mars texture (Viking, USGS/JPL)* (NASA) — https://science.nasa.gov/3d-resources/mars — Supports orbital Mars backdrop on the Title Screen *(License status: `UNVERIFIED`)*.
