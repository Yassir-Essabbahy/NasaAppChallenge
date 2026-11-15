using Outpost.Runtime.Room;
using Outpost.Runtime.Terminal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Audio
{
    /// <summary>
    /// Persistent on-screen ANNOUNCEMENT caption bar styled to match Terminal/Room HUD.
    /// Does not require runtime AudioMixer or shaders and respects Large Text scaling.
    /// </summary>
    public class AnnouncerSubtitleHud : MonoBehaviour
    {
        [SerializeField] private UITheme theme;
        [SerializeField] private AccessibilitySettings accessibility;

        private Canvas _canvas;
        private GameObject _panelGo;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _bodyText;
        private Image _accentBar;
        private AccessibilityTextScaler _textScaler;
        private bool _built;

        public bool IsShowing => _panelGo != null && _panelGo.activeSelf;

        public void Initialize(UITheme uiTheme, AccessibilitySettings accessSettings)
        {
            if (uiTheme != null) theme = uiTheme;
            if (accessibility != null)
                accessibility.OnSettingsChanged -= SyncAccessibilityTextSize;
            if (accessSettings != null) accessibility = accessSettings;
            EnsureBuilt();
            if (accessibility != null)
            {
                accessibility.OnSettingsChanged -= SyncAccessibilityTextSize;
                accessibility.OnSettingsChanged += SyncAccessibilityTextSize;
                SyncAccessibilityTextSize();
            }
        }

        private void OnDestroy()
        {
            if (accessibility != null)
                accessibility.OnSettingsChanged -= SyncAccessibilityTextSize;
        }

        private void SyncAccessibilityTextSize()
        {
            if (_textScaler != null && accessibility != null)
                _textScaler.SetLargerText(accessibility.LargerText);
        }

        public void ShowAnnouncement(string subtitle, bool isHazardAlert)
        {
            EnsureBuilt();
            if (_panelGo == null) return;

            if (_headerText != null)
            {
                _headerText.text = isHazardAlert ? "ANNOUNCEMENT // ADVISORY" : "ANNOUNCEMENT";
                if (theme != null)
                    _headerText.color = isHazardAlert ? theme.TextAmber : theme.AccentHelp;
            }

            if (_accentBar != null && theme != null)
                _accentBar.color = isHazardAlert ? theme.TextAmber : theme.AccentHelp;

            if (_bodyText != null)
                _bodyText.text = subtitle ?? string.Empty;

            _panelGo.SetActive(true);
        }

        public void Hide()
        {
            if (_panelGo != null)
                _panelGo.SetActive(false);
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (theme == null)
                theme = Resources.Load<UITheme>("TerminalTheme");

            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 45;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            _textScaler = gameObject.GetComponent<AccessibilityTextScaler>();
            if (_textScaler == null)
                _textScaler = gameObject.AddComponent<AccessibilityTextScaler>();
            if (accessibility != null)
                _textScaler.SetLargerText(accessibility.LargerText);

            Color bg = theme != null ? new Color(theme.PanelBg.r, theme.PanelBg.g, theme.PanelBg.b, 0.92f) : new Color(0.05f, 0.08f, 0.13f, 0.92f);
            Color border = theme != null ? theme.BorderFocused : new Color(0.24f, 0.72f, 0.78f, 1f);
            Color headerColor = theme != null ? theme.AccentHelp : new Color(0.45f, 0.88f, 0.95f, 1f);
            Color bodyColor = theme != null ? theme.TextPrimary : new Color(0.92f, 0.96f, 1f, 1f);

            var rootRect = GetComponent<RectTransform>();
            var panel = TerminalUiBuilder.CreateFlatPanel("AnnouncementCaptionBar", rootRect, bg, border, 2f, addDropShadow: true);
            // Anchored low-center cleanly above screen edge and below central reticle/interact prompts
            TerminalUiBuilder.SetAnchors(panel, 0.22f, 0.035f, 0.78f, 0.145f);
            _panelGo = panel.gameObject;

            var inner = TerminalUiBuilder.GetInner(panel);
            foreach (var g in panel.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;

            var barRect = TerminalUiBuilder.CreateRect("AccentBar", inner);
            TerminalUiBuilder.SetAnchors(barRect, 0.0f, 0.0f, 0.012f, 1.0f);
            _accentBar = barRect.gameObject.AddComponent<Image>();
            _accentBar.color = headerColor;
            _accentBar.raycastTarget = false;

            _headerText = TerminalUiBuilder.CreateText("AnnouncementLabel", inner, theme, 18f, headerColor, TextAlignmentOptions.MidlineLeft, _textScaler);
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.characterSpacing = 6f;
            _headerText.text = "ANNOUNCEMENT";
            TerminalUiBuilder.SetAnchors(_headerText.rectTransform, 0.03f, 0.64f, 0.97f, 0.94f);

            _bodyText = TerminalUiBuilder.CreateText("AnnouncementSubtitle", inner, theme, 22f, bodyColor, TextAlignmentOptions.TopLeft, _textScaler);
            _bodyText.textWrappingMode = TextWrappingModes.Normal;
            TerminalUiBuilder.SetAnchors(_bodyText.rectTransform, 0.03f, 0.08f, 0.97f, 0.64f);

            _panelGo.SetActive(false);
        }
    }
}
