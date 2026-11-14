MARS OUTPOST: LAST SHIFT — FONT SETUP GUIDE
===========================================

The planned fonts are not bundled in Assets/ right now, so FontTheme
defaults to LiberationSans SDF. Drop the free Google Font TTF/OTF files here when ready:

1. Assets/Fonts/BebasNeue-Regular.ttf (or .otf)  -> FontTheme.TitleFont
   Planned for: Cinematic Intro big cards, "MARS OUTPOST", Ending bookend, Titles.

2. Assets/Fonts/ShareTechMono-Regular.ttf          -> FontTheme.StampFont
   Planned for: Bottom-left intro stamp, small intro lines, radio line, subtitles.

3. Assets/Fonts/Nunito-Regular.ttf / Nunito-Bold.ttf -> FontTheme.TerminalFont
   Planned for: Friendly Mission Control terminal UI, tiles, windows, HELIOS hints, debrief.
   (Formerly VT323; replaced with friendly sans Nunito for crisp readability for ages 9-14.)

HOW TO GENERATE TMP FONT ASSETS IN UNITY:
1. Open Window > TextMeshPro > Font Asset Creator.
2. Source Font File: pick one of the fonts placed in Assets/Fonts/.
3. Sampling Point Size: Auto Sizing (or 90 for Bebas Neue / Nunito).
4. Padding: 9.
5. Atlas Resolution: 2048 x 2048.
6. Character Set: ASCII (32-126) + Extended ASCII / Latin Extended (e.g., Unicode Hex range 0020-00FF).
7. Render Mode: SDFAA.
8. Generate Font Atlas and Save As:
   - Assets/Fonts/BebasNeue SDF.asset
   - Assets/Fonts/ShareTechMono SDF.asset
   - Assets/Fonts/Nunito SDF.asset
9. Select Assets/Resources/FontTheme.asset and assign TitleFont, StampFont, and TerminalFont.
   (Also reflected automatically by UITheme / TerminalTheme.asset.)
