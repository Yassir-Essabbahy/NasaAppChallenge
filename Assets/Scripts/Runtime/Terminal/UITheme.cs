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

        [Header("Core Surface Colors (Soft Slate-Blue)")]
        public Color DesktopBg = new Color(0.078f, 0.118f, 0.188f, 1f);        // Soft dark slate-blue (#141E30)
        public Color PanelBg = new Color(0.118f, 0.176f, 0.271f, 0.96f);       // Lighter panel surface (#1E2D45)
        public Color PanelRaisedBg = new Color(0.149f, 0.216f, 0.329f, 0.98f); // Card surface (#263754)
        public Color WindowBg = new Color(0.102f, 0.153f, 0.239f, 0.99f);      // Window body (#1A273D)
        public Color TaskbarBg = new Color(0.063f, 0.094f, 0.153f, 0.98f);     // Bottom taskbar (#101827)
        public Color TitleBarFocusedBg = new Color(0.165f, 0.247f, 0.380f, 1f);
        public Color TitleBarUnfocusedBg = new Color(0.125f, 0.184f, 0.282f, 1f);

        [Header("Rounded Borders")]
        public float BorderThickness = 2f;
        public Color BorderFocused = new Color(0.420f, 0.720f, 1.000f, 1f);
        public Color BorderUnfocused = new Color(0.240f, 0.340f, 0.480f, 1f);
        public Color GuidedPulseColor = new Color(1.000f, 0.800f, 0.260f, 1f);

        [Header("Text & Accents")]
        public Color TextPrimary = new Color(0.957f, 0.969f, 1.000f, 1f);      // Crisp white (#F4F7FF)
        public Color TextAmber = new Color(1.000f, 0.820f, 0.320f, 1f);        // Warm friendly gold
        public Color TextMuted = new Color(0.650f, 0.740f, 0.850f, 1f);        // Readable soft blue-grey

        [Header("Status Colors (Always Paired With Icon + Word Tag)")]
        public Color StatusOk = new Color(0.180f, 0.898f, 0.490f, 1f);         // GOOD green (#2EE57D)
        public Color StatusWarn = new Color(1.000f, 0.745f, 0.180f, 1f);       // LOW amber (#FFBE2E)
        public Color StatusCrit = new Color(1.000f, 0.302f, 0.345f, 1f);       // DANGER coral red (#FF4D58)

        [Header("Per-App Bright Accents")]
        public Color AccentPower = new Color(1.000f, 0.831f, 0.231f, 1f);      // Power: Yellow (#FFD43B)
        public Color AccentLifeSupport = new Color(0.220f, 0.851f, 0.961f, 1f);// Life Support: Cyan (#38D9F5)
        public Color AccentGreenhouse = new Color(0.298f, 0.878f, 0.478f, 1f); // Greenhouse: Green (#4CE07A)
        public Color AccentRadiation = new Color(1.000f, 0.549f, 0.220f, 1f);  // Radiation: Orange (#FF8C38)
        public Color AccentCrew = new Color(1.000f, 0.420f, 0.616f, 1f);       // Crew: Pink (#FF6B9D)
        public Color AccentComms = new Color(0.690f, 0.486f, 1.000f, 1f);      // Comms: Purple (#B07CFF)
        public Color AccentLog = new Color(1.000f, 0.722f, 0.220f, 1f);        // Log: Amber (#FFB838)
        public Color AccentSources = new Color(0.361f, 0.659f, 1.000f, 1f);    // Sources: Blue (#5CA8FF)
        public Color AccentHelp = new Color(0.227f, 0.847f, 0.710f, 1f);       // Help: Teal

        [Header("Buttons & Controls")]
        public Color ButtonBg = new Color(0.165f, 0.243f, 0.369f, 1f);
        public Color ButtonAccentBg = new Color(0.169f, 0.506f, 0.365f, 1f);
        public Color ButtonDangerBg = new Color(0.720f, 0.220f, 0.250f, 1f);
        public Color BarTrackBg = new Color(0.067f, 0.098f, 0.161f, 1f);

        [Header("Swappable UI Icons & Wallpaper (Assets/Art/UI/Icons)")]
        public Sprite MarsWallpaperSprite;
        public Sprite[] IconSprites = new Sprite[19];

        public Sprite GetIconSprite(TerminalIconId id)
        {
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

            DesktopBg = new Color(0.078f, 0.118f, 0.188f, 1f);
            PanelBg = new Color(0.118f, 0.176f, 0.271f, 0.96f);
            PanelRaisedBg = new Color(0.149f, 0.216f, 0.329f, 0.98f);
            WindowBg = new Color(0.102f, 0.153f, 0.239f, 0.99f);
            TaskbarBg = new Color(0.063f, 0.094f, 0.153f, 0.98f);
            TitleBarFocusedBg = new Color(0.165f, 0.247f, 0.380f, 1f);
            TitleBarUnfocusedBg = new Color(0.125f, 0.184f, 0.282f, 1f);

            BorderThickness = 2f;
            BorderFocused = new Color(0.420f, 0.720f, 1.000f, 1f);
            BorderUnfocused = new Color(0.240f, 0.340f, 0.480f, 1f);
            GuidedPulseColor = new Color(1.000f, 0.800f, 0.260f, 1f);

            TextPrimary = new Color(0.957f, 0.969f, 1.000f, 1f);
            TextAmber = new Color(1.000f, 0.820f, 0.320f, 1f);
            TextMuted = new Color(0.650f, 0.740f, 0.850f, 1f);

            StatusOk = new Color(0.180f, 0.898f, 0.490f, 1f);
            StatusWarn = new Color(1.000f, 0.745f, 0.180f, 1f);
            StatusCrit = new Color(1.000f, 0.302f, 0.345f, 1f);

            AccentPower = new Color(1.000f, 0.831f, 0.231f, 1f);
            AccentLifeSupport = new Color(0.220f, 0.851f, 0.961f, 1f);
            AccentGreenhouse = new Color(0.298f, 0.878f, 0.478f, 1f);
            AccentRadiation = new Color(1.000f, 0.549f, 0.220f, 1f);
            AccentCrew = new Color(1.000f, 0.420f, 0.616f, 1f);
            AccentComms = new Color(0.690f, 0.486f, 1.000f, 1f);
            AccentLog = new Color(1.000f, 0.722f, 0.220f, 1f);
            AccentSources = new Color(0.361f, 0.659f, 1.000f, 1f);
            AccentHelp = new Color(0.227f, 0.847f, 0.710f, 1f);

            ButtonBg = new Color(0.165f, 0.243f, 0.369f, 1f);
            ButtonAccentBg = new Color(0.169f, 0.506f, 0.365f, 1f);
            ButtonDangerBg = new Color(0.720f, 0.220f, 0.250f, 1f);
            BarTrackBg = new Color(0.067f, 0.098f, 0.161f, 1f);
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
