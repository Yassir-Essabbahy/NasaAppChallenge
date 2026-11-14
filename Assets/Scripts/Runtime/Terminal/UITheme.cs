using Outpost.Sim;
using TMPro;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Shared Terminal OS theme ScriptableObject so all colors, per-app accents, borders, and fonts
    /// can be restyled in one place. Soft dark slate-blue friendly mission-control palette.
    /// </summary>
    [CreateAssetMenu(fileName = "TerminalTheme", menuName = "Outpost/Terminal UI Theme", order = 1)]
    public class UITheme : ScriptableObject
    {
        [Header("Typography (Swap Font Here)")]
        public FontTheme FontTheme;
        public TMP_FontAsset Font;
        public float TitleFontSize = 36f;
        public float HeaderFontSize = 28f;
        public float BodyFontSize = 24f;

        [Header("Core Surface Colors (Dark Slate + Icon Palette)")]
        public static readonly Color PaletteTeal = new Color(0.271f, 0.890f, 0.827f, 1f);      // #45E3D3
        public static readonly Color PaletteLightBlue = new Color(0.498f, 0.875f, 1.000f, 1f); // #7FDFFF
        public static readonly Color PaletteYellow = new Color(0.969f, 0.788f, 0.282f, 1f);    // #F7C948
        public static readonly Color PaletteCoral = new Color(1.000f, 0.420f, 0.420f, 1f);     // #FF6B6B
        public static Color AccentPrimary => PaletteTeal;
        public static Color AccentSecondary => PaletteLightBlue;
        public static Color WarnStatic => PaletteYellow;

        public Color DesktopBg = new Color(0.067f, 0.098f, 0.149f, 1f);        // Dark slate (#111926)
        public Color PanelBg = new Color(0.102f, 0.149f, 0.220f, 0.96f);       // Slate panel (#1A2638)
        public Color PanelRaisedBg = new Color(0.133f, 0.192f, 0.282f, 0.98f); // Raised card (#223148)
        public Color WindowBg = new Color(0.090f, 0.133f, 0.200f, 0.99f);      // Window body (#172233)
        public Color TaskbarBg = new Color(0.055f, 0.082f, 0.129f, 0.98f);     // Bottom taskbar (#0E1521)
        public Color TitleBarFocusedBg = new Color(0.137f, 0.224f, 0.306f, 1f);
        public Color TitleBarUnfocusedBg = new Color(0.110f, 0.157f, 0.231f, 1f);

        [Header("Rounded Borders")]
        public float BorderThickness = 2f;
        public Color BorderFocused = new Color(0.271f, 0.890f, 0.827f, 1f);    // Teal #45E3D3
        public Color BorderUnfocused = new Color(0.216f, 0.314f, 0.424f, 1f);
        public Color GuidedPulseColor = new Color(0.969f, 0.788f, 0.282f, 1f); // Yellow #F7C948

        [Header("Text & Accents")]
        public Color TextPrimary = new Color(0.949f, 0.973f, 1.000f, 1f);      // Crisp white (#F2F8FF)
        public Color TextAmber = new Color(0.969f, 0.788f, 0.282f, 1f);        // Warning yellow (#F7C948)
        public Color TextMuted = new Color(0.620f, 0.745f, 0.847f, 1f);        // Soft slate-blue

        [Header("Status Colors (Always Paired With Icon + Word Tag)")]
        public Color StatusOk = new Color(0.271f, 0.890f, 0.827f, 1f);         // GOOD teal (#45E3D3)
        public Color StatusWarn = new Color(0.969f, 0.788f, 0.282f, 1f);       // LOW yellow (#F7C948)
        public Color StatusCrit = new Color(1.000f, 0.420f, 0.420f, 1f);       // DANGER coral (#FF6B6B)

        [Header("Per-App Subtle Palette Accents")]
        public Color AccentPower = new Color(0.650f, 0.855f, 0.620f, 1f);      // Subtle warm-teal
        public Color AccentLifeSupport = new Color(0.498f, 0.875f, 1.000f, 1f);// Light blue (#7FDFFF)
        public Color AccentGreenhouse = new Color(0.271f, 0.890f, 0.827f, 1f); // Teal (#45E3D3)
        public Color AccentRadiation = new Color(0.860f, 0.770f, 0.480f, 1f);  // Subtle muted amber-slate
        public Color AccentCrew = new Color(0.450f, 0.850f, 0.860f, 1f);       // Subtle aqua-teal
        public Color AccentComms = new Color(0.560f, 0.840f, 0.980f, 1f);      // Subtle sky-blue
        public Color AccentLog = new Color(0.750f, 0.835f, 0.640f, 1f);        // Subtle sage-gold
        public Color AccentSources = new Color(0.498f, 0.875f, 1.000f, 1f);    // Light blue (#7FDFFF)
        public Color AccentHelp = new Color(0.271f, 0.890f, 0.827f, 1f);       // Teal (#45E3D3)

        [Header("Buttons & Controls")]
        public Color ButtonBg = new Color(0.149f, 0.220f, 0.325f, 1f);
        public Color ButtonAccentBg = new Color(0.137f, 0.443f, 0.435f, 1f);
        public Color ButtonDangerBg = new Color(0.660f, 0.255f, 0.275f, 1f);
        public Color BarTrackBg = new Color(0.063f, 0.094f, 0.145f, 1f);

        [Header("Swappable UI Icons & Wallpaper (Assets/Art/UI/Icons)")]
        public Sprite MarsWallpaperSprite;
        public IconLibrary IconLibrary;
        public Sprite[] IconSprites = new Sprite[30];

        public Sprite GetIconSprite(TerminalIconId id)
        {
            if (IconLibrary != null)
            {
                string key = TerminalUiArt.GetIconKey(id);
                if (!string.IsNullOrEmpty(key))
                {
                    var fromLib = IconLibrary.GetSprite(key);
                    if (fromLib != null) return fromLib;
                }
            }

            int idx = (int)id;
            if (IconSprites != null && idx >= 0 && idx < IconSprites.Length)
                return IconSprites[idx];
            return null;
        }

        public void ApplyFriendlyThemeDefaults()
        {
            TitleFontSize = 36f;
            HeaderFontSize = 28f;
            BodyFontSize = 24f;

            DesktopBg = new Color(0.067f, 0.098f, 0.149f, 1f);
            PanelBg = new Color(0.102f, 0.149f, 0.220f, 0.96f);
            PanelRaisedBg = new Color(0.133f, 0.192f, 0.282f, 0.98f);
            WindowBg = new Color(0.090f, 0.133f, 0.200f, 0.99f);
            TaskbarBg = new Color(0.055f, 0.082f, 0.129f, 0.98f);
            TitleBarFocusedBg = new Color(0.137f, 0.224f, 0.306f, 1f);
            TitleBarUnfocusedBg = new Color(0.110f, 0.157f, 0.231f, 1f);

            BorderThickness = 2f;
            BorderFocused = PaletteTeal;
            BorderUnfocused = new Color(0.216f, 0.314f, 0.424f, 1f);
            GuidedPulseColor = PaletteYellow;

            TextPrimary = new Color(0.949f, 0.973f, 1.000f, 1f);
            TextAmber = PaletteYellow;
            TextMuted = new Color(0.620f, 0.745f, 0.847f, 1f);

            StatusOk = PaletteTeal;
            StatusWarn = PaletteYellow;
            StatusCrit = PaletteCoral;

            AccentPower = new Color(0.650f, 0.855f, 0.620f, 1f);
            AccentLifeSupport = PaletteLightBlue;
            AccentGreenhouse = PaletteTeal;
            AccentRadiation = new Color(0.860f, 0.770f, 0.480f, 1f);
            AccentCrew = new Color(0.450f, 0.850f, 0.860f, 1f);
            AccentComms = new Color(0.560f, 0.840f, 0.980f, 1f);
            AccentLog = new Color(0.750f, 0.835f, 0.640f, 1f);
            AccentSources = PaletteLightBlue;
            AccentHelp = PaletteTeal;

            ButtonBg = new Color(0.149f, 0.220f, 0.325f, 1f);
            ButtonAccentBg = new Color(0.137f, 0.443f, 0.435f, 1f);
            ButtonDangerBg = new Color(0.660f, 0.255f, 0.275f, 1f);
            BarTrackBg = new Color(0.063f, 0.094f, 0.145f, 1f);
        }

        public Color GetAppAccent(TerminalAppId id)
        {
            switch (id)
            {
                case TerminalAppId.PowerGrid: return AccentPower;
                case TerminalAppId.LifeSupport: return AccentLifeSupport;
                case TerminalAppId.Greenhouse: return AccentGreenhouse;
                case TerminalAppId.Radiation: return AccentRadiation;
                case TerminalAppId.Crew: return AccentCrew;
                case TerminalAppId.Comms: return AccentComms;
                case TerminalAppId.CommandersLog: return AccentLog;
                case TerminalAppId.Sources: return AccentSources;
                default: return BorderFocused;
            }
        }

        public TMP_FontAsset ResolveFont()
        {
            if (FontTheme != null && FontTheme.TerminalFont != null)
                return FontTheme.TerminalFont;
            if (Font != null) return Font;
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;
            Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (runtimeFont == null)
                runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (runtimeFont != null)
            {
                Font = TMP_FontAsset.CreateFontAsset(runtimeFont);
                Font.name = "TerminalFallbackTMPFont";
            }
            return Font;
        }

        public TMP_FontAsset ResolveTitleFont()
        {
            return FontTheme != null ? FontTheme.ResolveTitleFont(ResolveFont()) : ResolveFont();
        }

        public TMP_FontAsset ResolveStampFont()
        {
            return FontTheme != null ? FontTheme.ResolveStampFont(ResolveFont()) : ResolveFont();
        }
    }
}
