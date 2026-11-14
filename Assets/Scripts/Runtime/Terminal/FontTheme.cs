using TMPro;
using UnityEngine;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Central typography theme with three swappable TMP font asset slots:
    /// TitleFont (planned: Bebas Neue), StampFont (planned: Share Tech Mono), TerminalFont (planned: VT323).
    /// Wire assets in Inspector or Assets/Resources/FontTheme.asset; see Assets/Fonts/README.txt.
    /// </summary>
    [CreateAssetMenu(fileName = "FontTheme", menuName = "Outpost/Font Theme", order = 5)]
    public class FontTheme : ScriptableObject
    {
        [Header("Swappable TMP Font Asset Slots (see Assets/Fonts/README.txt)")]
        [Tooltip("Condensed bold display font for intro cards & titles (planned: Bebas Neue).")]
        public TMP_FontAsset TitleFont;

        [Tooltip("Crisp monospace stamp font for locations & sublines (planned: Share Tech Mono).")]
        public TMP_FontAsset StampFont;

        [Tooltip("Retro terminal UI font for desktop, logs & debrief (planned: VT323).")]
        public TMP_FontAsset TerminalFont;

        public TMP_FontAsset ResolveTitleFont(TMP_FontAsset fallback = null)
        {
            return ResolveSlot(TitleFont, fallback);
        }

        public TMP_FontAsset ResolveStampFont(TMP_FontAsset fallback = null)
        {
            return ResolveSlot(StampFont, fallback);
        }

        public TMP_FontAsset ResolveTerminalFont(TMP_FontAsset fallback = null)
        {
            return ResolveSlot(TerminalFont, fallback);
        }

        private static TMP_FontAsset ResolveSlot(TMP_FontAsset primary, TMP_FontAsset fallback)
        {
            if (primary != null) return primary;
            if (fallback != null) return fallback;
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;
            return null;
        }
    }
}
