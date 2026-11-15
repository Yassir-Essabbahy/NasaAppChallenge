# NASA Space Apps Challenge 2026 — Submission Copy

*Prepared for Team **Pluto Outpost** (Solo Developer: **Yassir**) — Challenge: **Build a Junior Astronaut Mission Trainer**. Every section below is written in plain language and kept under 120 words.*

---

## Project Name & Tagline

**Mars Outpost: Last Shift**
Run life support, suit up for Martian EVAs, and keep four astronauts alive for seven sols until rescue.

---

## High-Level Summary

*Mars Outpost: Last Shift* is a first-person 3D Mars base simulator and command terminal for students. Players wake up as injured Commander Alex Vale inside Outpost Helios and manage seven Martian days (sols) until rescue arrives. Each sol combines hands-on exploration—putting on an EVA suit, cycling the airlock, and inspecting base stations—with **OUTPOST OS**, a retro-futuristic desktop terminal. Students balance daytime solar battery power across life support, heating, crops, and comms, spend three daily Action Points, and respond to dust storms and solar flares. Every crisis pairs decisions with plain-language NASA, ESA, and USGS science cards so students learn why real Mars missions must carefully budget every watt, liter, and millisiévert.

---

## How It Works

Before the main menu, players choose English or Modern Standard Arabic using a smooth pill slider. After a short intro card (`"THE SUN IS FAR AWAY."`), Commander Vale inspects the base in first person (`WASD`, mouse look, `E` to interact) and sits at the Command Terminal. Players can toggle between draggable **Desktop OS** windows and a **Classic View** dashboard hosting nine subsystems. Before ending each sol, players must complete three daily duties: resolve that sol's event, allocate power or spend an Action Point, and inspect an assigned room or outdoor surface station (`Medbay`, `Storage`, `Greenhouse`, or EVA surface sites requiring the suit rack and airlock cycle).

---

## How We Built It

The project was built in Unity `6000.5.5f1` (`URP`, New Input System, `TextMeshPro`) targeting WebGL (`1280x720`) so teachers and students can play directly in a browser. A pure C# simulation layer (`Outpost.Sim`) keeps rules deterministic, testable, and separate from Unity UI and 3D room scenes (`MainMenu`, `Outpost`, `Terminal`). We validated the game with NUnit EditMode tests and a headless `BalanceRunner` across `1,000` seeded missions per strategy (`Careless`, `Average`, `Optimal`). Offline C# DSP filters (`VoxFxDsp`) give facility announcements a retro radio feel, alongside PSX-style shaders, accessibility toggles, and full English/Arabic text shaping.

---

## Use of Space Agency Data

Space agency science shapes both events and educational cards. Baseline surface radiation (`0.67 mSv/sol`) adapts Curiosity rover measurements (Hassler et al. / NASA), Mars's `24h 39m` sol and `~95% CO2` atmosphere come from the NASA Mars Fact Sheet, Earth–Mars `4–24` minute radio delays reflect ESA Mars Express data, and water conservation references the ISS `98%` recycling milestone. Gameplay numbers like `70%` starting oxygen or `-50%` storm solar factor are simplified units so students can see clear cause and effect. In the **Sources** app and **What NASA Knows** debrief, every metric is explicitly labeled `REAL`, `ADAPTED`, or `GAME` alongside source URLs.

---

## Impact & Classroom Value

Instead of memorizing vocabulary, students experience how life-support systems depend on one another. Cutting heating below `50%` warms nothing and freezes the crew; letting batteries hit `0%` breaks scrubbers or recyclers; ignoring dust storms starves solar power; and stepping outside requires an EVA suit and airlock safety check. Built-in bilingual English and Arabic support, large-text mode, flicker reduction, and an inline Mars glossary help diverse classrooms explore together. Teachers can launch the WebGL build (`ITCH_URL_TODO`) in seconds on school laptops and use each Sol's NASA science card to spark discussions on engineering trade-offs, radiation shielding, and crew isolation studies.

---

## AI, Tools & Attribution

Unity (`6000.5.5f1`), **Unity MCP**, and AI coding assistants (Google DeepMind Antigravity) helped write C# scripts, editor builders, tests, and documentation, and plan system architecture. Facility announcer voice clips were generated using **ElevenLabs** text-to-speech and processed locally via custom DSP filters. All game design decisions, balance choices, Arabic localization review, scene integration, and final testing were carried out by **Yassir** (**Team Pluto Outpost**). Third-party 3D models, UI icons, fonts, and audio keep their own licenses as documented in `CREDITS.md` (with `UNVERIFIED` items flagged for license check before public release). This project is not affiliated with or endorsed by NASA.
