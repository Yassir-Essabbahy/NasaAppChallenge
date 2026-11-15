using System;
using System.Collections.Generic;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Controls the "OUTPOST OS" retro-futuristic Desktop skin inside TerminalRoot:
    /// Mars horizon wallpaper, top pinned resource status strip, 9-app desktop icon grid
    /// with unread badges, floating window workspace, Start menu, bottom taskbar with
    /// open window buttons + Sol/clock/rescue/bell + END SOL CTA, alert toast, and widgets.
    /// </summary>
    public class DesktopSkinController : MonoBehaviour
    {
        public const string PrefUiMode = "outpost.terminal.ui_mode"; // 1 = Desktop, 0 = Classic
        public static readonly Vector2 DesktopWindowSize = new Vector2(880f, 600f);

        private static readonly (TerminalAppId appId, string labelKey, string tipTitle, string tipBody, TerminalIconId icon, Color accent)[] AppEntries =
        {
            (DesktopLocalization.MissionControlAppId, "app.mission_control", "app.mission_control", "title.goal_hook",         TerminalIconId.Home, UITheme.PaletteTeal),
            (TerminalAppId.PowerGrid,                 "app.power",           "app.power",           "gloss.power.def",         TerminalIconId.Bolt,    UITheme.PaletteTeal),
            (TerminalAppId.LifeSupport,               "app.lifesupport",     "app.lifesupport",     "gloss.o2.def",            TerminalIconId.Water,   UITheme.PaletteLightBlue),
            (TerminalAppId.Greenhouse,                "app.greenhouse",      "app.greenhouse",      "gloss.food.def",          TerminalIconId.Food,    UITheme.PaletteTeal),
            (TerminalAppId.Radiation,                 "app.radiation",       "app.radiation",       "gloss.rad.def",           TerminalIconId.Shield,  UITheme.PaletteLightBlue),
            (TerminalAppId.Crew,                      "app.crew",            "app.crew",            "gloss.ap.def",            TerminalIconId.Crew,    UITheme.PaletteLightBlue),
            (TerminalAppId.Comms,                     "app.comms",           "app.comms",           "gloss.dsn.def",           TerminalIconId.Signal,  UITheme.PaletteTeal),
            (TerminalAppId.CommandersLog,             "app.log",             "app.log",             "log.friendly_intro",      TerminalIconId.Log,     UITheme.PaletteLightBlue),
            (TerminalAppId.Sources,                   "app.sources",         "app.sources",         "sources.friendly_intro",  TerminalIconId.Sources, UITheme.PaletteLightBlue)
        };

        private UITheme _theme;
        private AccessibilityTextScaler _scaler;
        private WindowManager _windowManager;

        private Action<TerminalAppId> _onOpenApp;
        private Action _onEndSol;
        private Action _onToggleHints;
        private Action _onOpenHelp;
        private Action _onLeaveTerminal;
        private Action _onViewAlert;
        private Action _onToggleMode;
        private Action _onUiSound;
        private Action<int> _onDutyClicked;

        private RectTransform _desktopRoot;
        private RectTransform _windowLayer;
        private DesktopWidgetsPanel _widgets;

        // Top pinned strip (5 resource chips + AP)
        private readonly Image[] _chipBgs = new Image[5];
        private readonly TextMeshProUGUI[] _chipPercents = new TextMeshProUGUI[5];
        private readonly TextMeshProUGUI[] _chipWords = new TextMeshProUGUI[5];
        private readonly Image[] _chipBadges = new Image[5];
        private TextMeshProUGUI _chipApText;

        // Desktop icons (9 apps)
        private readonly RectTransform[] _desktopIconPanels = new RectTransform[9];
        private readonly TextMeshProUGUI[] _desktopIconBadges = new TextMeshProUGUI[9];
        private TerminalAppId? _pulsingApp;

        // Forecast & Taskbar
        private GameObject _startMenuPopup;
        private readonly Button[] _taskWinBtns = new Button[WindowManager.MaxOpenWindows];
        private readonly RectTransform[] _taskWinPanels = new RectTransform[WindowManager.MaxOpenWindows];
        private readonly Image[] _taskWinInners = new Image[WindowManager.MaxOpenWindows];
        private readonly Image[] _taskWinIcons = new Image[WindowManager.MaxOpenWindows];
        private readonly TextMeshProUGUI[] _taskWinLabels = new TextMeshProUGUI[WindowManager.MaxOpenWindows];
        private readonly GameObject[] _taskWinFocusBars = new GameObject[WindowManager.MaxOpenWindows];
        private readonly TerminalAppId[] _taskWinApps = new TerminalAppId[WindowManager.MaxOpenWindows];

        private TextMeshProUGUI _solText;
        private TextMeshProUGUI _clockText;
        private TextMeshProUGUI _rescueText;
        private Button _bellBtn;
        private Image _bellIcon;
        private GameObject _bellBadgeGo;
        private TextMeshProUGUI _bellBadgeText;
        private Button _hintsBtn;
        private TextMeshProUGUI _hintsLabel;
        private Button _leaveBtn;
        private Button _endSolBtn;
        private RectTransform _endSolRect;
        private Image _endSolBgImg;
        private ButtonHoverFeedback _endSolHover;
        private TextMeshProUGUI _endSolLabel;
        private TextMeshProUGUI _forecastBarText;
        private TooltipTrigger _endSolTooltip;

        // Alert Toast near bell
        private GameObject _bellToastGo;
        private TextMeshProUGUI _bellToastText;
        private float _bellToastTimer;

        public RectTransform WindowLayer => _windowLayer;
        public bool IsDesktopActive => _desktopRoot != null && _desktopRoot.gameObject.activeSelf;

        public void Build(
            RectTransform screenParent,
            UITheme theme,
            AccessibilityTextScaler scaler,
            WindowManager windowManager,
            Action<TerminalAppId> onOpenApp,
            Action onEndSol,
            Action onToggleHints,
            Action onOpenHelp,
            Action onLeaveTerminal,
            Action onViewAlert,
            Action onToggleMode,
            Action onUiSound,
            Action<int> onDutyClicked = null)
        {
            _theme = theme;
            _scaler = scaler;
            _windowManager = windowManager;
            _onOpenApp = onOpenApp;
            _onEndSol = onEndSol;
            _onToggleHints = onToggleHints;
            _onOpenHelp = onOpenHelp;
            _onLeaveTerminal = onLeaveTerminal;
            _onViewAlert = onViewAlert;
            _onToggleMode = onToggleMode;
            _onUiSound = onUiSound;
            _onDutyClicked = onDutyClicked;

            _desktopRoot = TerminalUiBuilder.CreateRect("DesktopOsRoot", screenParent);
            _desktopRoot.anchorMin = Vector2.zero;
            _desktopRoot.anchorMax = Vector2.one;
            _desktopRoot.offsetMin = new Vector2(12f, 6f);
            _desktopRoot.offsetMax = new Vector2(-12f, -94f);

            BuildWallpaper();
            BuildPinnedStatusStrip();
            BuildDesktopWorkspace();
            BuildForecastStrip();
            BuildTaskbar();
            BuildStartMenu();
            BuildBellToast();

            _desktopRoot.gameObject.SetActive(false);
        }

        public void SetModeVisible(bool desktopMode)
        {
            if (_desktopRoot != null)
                _desktopRoot.gameObject.SetActive(desktopMode);
            if (_startMenuPopup != null)
                _startMenuPopup.SetActive(false);
        }

        public void SetReadOnlyBannerOffset(bool readOnly)
        {
            if (_desktopRoot == null) return;
            float top = readOnly ? -142f : -94f;
            _desktopRoot.offsetMax = new Vector2(-12f, top);
        }

        public void SetShowLeaveButton(bool show)
        {
            if (_leaveBtn != null)
                _leaveBtn.gameObject.SetActive(show);
        }

        public void UpdateClock(int sol, int totalSols, string clockStr)
        {
            if (_solText != null)
                _solText.text = DesktopLocalization.Format("mc.sol_progress", sol, totalSols).ToUpperInvariant();
            if (_clockText != null)
                _clockText.text = clockStr;
        }

        public void TriggerBellToast(string headline, float duration = 4.5f)
        {
            if (_bellToastGo == null || string.IsNullOrEmpty(headline)) return;
            _bellToastText.text = DesktopLocalization.Format("desktop.toast_alert", headline);
            _bellToastGo.SetActive(true);
            _bellToastGo.transform.SetAsLastSibling();
            _bellToastTimer = duration;
        }

        public void HideStartMenu()
        {
            if (_startMenuPopup != null)
                _startMenuPopup.SetActive(false);
        }

        public void ResetEndSolHover()
        {
            _endSolHover?.ResetScaleImmediate();
        }

        private void Update()
        {
            if (_bellToastTimer > 0f && _bellToastGo != null && _bellToastGo.activeSelf)
            {
                _bellToastTimer -= Time.unscaledDeltaTime;
                if (_bellToastTimer <= 0f)
                    _bellToastGo.SetActive(false);
            }

            if (_pulsingApp.HasValue && IsDesktopActive)
            {
                int slot = FindIconSlot(_pulsingApp.Value);
                bool focused = _windowManager != null && _windowManager.FocusedApp.HasValue && _windowManager.FocusedApp.Value == _pulsingApp.Value;
                if (!focused && slot >= 0 && _desktopIconPanels[slot] != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                    Color c = Color.Lerp(new Color(0.18f, 0.34f, 0.45f, 1f), _theme.GuidedPulseColor, pulse);
                    TerminalUiBuilder.SetBorderColor(_desktopIconPanels[slot], c);
                }
            }
        }

        private void BuildWallpaper()
        {
            var wpBg = TerminalUiBuilder.CreateRect("DesktopWallpaperBg", _desktopRoot);
            TerminalUiBuilder.SetStretch(wpBg);
            var bgImg = wpBg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.039f, 0.075f, 0.125f, 1f);
            bgImg.raycastTarget = false;

            var wpArt = TerminalUiBuilder.CreateRect("DesktopMarsWallpaper", _desktopRoot);
            TerminalUiBuilder.SetStretch(wpArt);
            var artImg = wpArt.gameObject.AddComponent<Image>();
            artImg.sprite = TerminalUiArt.GetMarsWallpaperSprite(_theme);
            artImg.color = new Color(0.95f, 0.97f, 1f, 0.88f);
            artImg.preserveAspect = false;
            artImg.raycastTarget = false;
        }

        private void BuildPinnedStatusStrip()
        {
            var strip = TerminalUiBuilder.CreateFlatPanel("PinnedStatusStrip", _desktopRoot, new Color(0.035f, 0.082f, 0.130f, 0.94f), new Color(0.18f, 0.36f, 0.48f, 1f), 2f, true);
            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.offsetMin = new Vector2(4f, -58f);
            strip.offsetMax = new Vector2(-4f, -4f);
            var inner = TerminalUiBuilder.GetInner(strip);
            var rounded = TerminalUiArt.GetRoundedRect8px();

            (string labelKey, TerminalIconId icon, Color accent, TerminalAppId target, string tipTerm, string tipDef)[] defs =
            {
                ("mc.status.power",  TerminalIconId.Bolt,   UITheme.PaletteTeal,      TerminalAppId.PowerGrid,   "gloss.power.term",  "gloss.power.def"),
                ("mc.status.oxygen", TerminalIconId.Oxygen, UITheme.PaletteLightBlue, TerminalAppId.LifeSupport, "gloss.o2.term",     "gloss.o2.def"),
                ("mc.status.water",  TerminalIconId.Water,  UITheme.PaletteLightBlue, TerminalAppId.LifeSupport, "gloss.water.term",  "gloss.water.def"),
                ("mc.status.food",   TerminalIconId.Food,   UITheme.PaletteTeal,      TerminalAppId.Greenhouse,  "gloss.food.term",   "gloss.food.def"),
                ("mc.status.shield", TerminalIconId.Shield, UITheme.PaletteLightBlue, TerminalAppId.Radiation,   "gloss.shield.term", "gloss.shield.def")
            };

            const float chipSpan = 0.892f / 5f;
            for (int i = 0; i < 5; i++)
            {
                var def = defs[i];
                float x0 = 0.004f + i * chipSpan;
                float x1 = x0 + chipSpan - 0.005f;

                var chipPanel = TerminalUiBuilder.CreateFlatPanel($"StatusChip_{i}", inner, new Color(0.055f, 0.115f, 0.175f, 0.96f), new Color(0.14f, 0.28f, 0.38f, 1f), 1.5f);
                TerminalUiBuilder.SetAnchors(chipPanel, x0, 0.08f, x1, 0.92f);
                var chip = TerminalUiBuilder.GetInner(chipPanel);
                var bg = chip.GetComponent<Image>();
                _chipBgs[i] = bg;

                var btn = chipPanel.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                chipPanel.gameObject.AddComponent<ButtonHoverFeedback>();
                var app = def.target;
                btn.onClick.AddListener(() =>
                {
                    _onUiSound?.Invoke();
                    _onOpenApp?.Invoke(app);
                });
                TerminalUiBuilder.AttachTooltip(chipPanel, def.tipTerm, def.tipDef);

                var icon = TerminalUiBuilder.CreateIcon("Icon", chip, def.icon, def.accent);
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.02f, 0.16f, 0.095f, 0.84f);

                var lbl = TerminalUiBuilder.CreateText("Name", chip, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
                lbl.fontStyle = FontStyles.Bold;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.overflowMode = TextOverflowModes.Ellipsis;
                lbl.text = DesktopLocalization.Get(def.labelKey);
                TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.10f, 0f, 0.465f, 1f);

                var pct = TerminalUiBuilder.CreateText("Pct", chip, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineRight, _scaler);
                pct.fontStyle = FontStyles.Bold;
                pct.textWrappingMode = TextWrappingModes.NoWrap;
                TerminalUiBuilder.SetAnchors(pct.rectTransform, 0.47f, 0f, 0.675f, 1f);
                _chipPercents[i] = pct;

                var badge = TerminalUiBuilder.CreateRect("Badge", chip);
                TerminalUiBuilder.SetAnchors(badge, 0.69f, 0.12f, 0.98f, 0.88f);
                var bImg = badge.gameObject.AddComponent<Image>();
                bImg.sprite = rounded;
                bImg.type = Image.Type.Sliced;
                bImg.color = _theme.StatusOk;
                _chipBadges[i] = bImg;

                var word = TerminalUiBuilder.CreateText("Word", badge, _theme, 20f, new Color(0.03f, 0.08f, 0.12f, 1f), TextAlignmentOptions.Center, _scaler);
                word.fontStyle = FontStyles.Bold;
                word.textWrappingMode = TextWrappingModes.NoWrap;
                TerminalUiBuilder.SetStretch(word.rectTransform, 2f);
                _chipWords[i] = word;
            }

            // Action Points Chip
            var apChip = TerminalUiBuilder.CreateFlatPanel("ApChip", inner, new Color(0.07f, 0.14f, 0.21f, 0.96f), _theme.TextAmber, 1.5f);
            TerminalUiBuilder.SetAnchors(apChip, 0.898f, 0.08f, 0.996f, 0.92f);
            _chipApText = TerminalUiBuilder.CreateText("ApText", TerminalUiBuilder.GetInner(apChip), _theme, 24f, _theme.TextAmber, TextAlignmentOptions.Center, _scaler);
            _chipApText.fontStyle = FontStyles.Bold;
            _chipApText.textWrappingMode = TextWrappingModes.NoWrap;
            TerminalUiBuilder.SetStretch(_chipApText.rectTransform, 4f);
            TerminalUiBuilder.AttachTooltip(apChip, "gloss.ap.term", "gloss.ap.def");
        }

        private void BuildDesktopWorkspace()
        {
            var workspace = TerminalUiBuilder.CreateRect("DesktopWorkspace", _desktopRoot);
            workspace.anchorMin = Vector2.zero;
            workspace.anchorMax = Vector2.one;
            workspace.offsetMin = new Vector2(4f, 108f);
            workspace.offsetMax = new Vector2(-4f, -64f);

            // 1. Desktop App Icons Grid (Left / Center, 3x3)
            var iconGrid = TerminalUiBuilder.CreateRect("DesktopIconGrid", workspace);
            TerminalUiBuilder.SetAnchors(iconGrid, 0.005f, 0.04f, 0.485f, 0.98f);

            for (int i = 0; i < AppEntries.Length; i++)
            {
                var entry = AppEntries[i];
                int col = i % 3;
                int row = i / 3;
                float x0 = col * 0.335f;
                float x1 = x0 + 0.315f;
                float y1 = 1f - row * 0.335f;
                float y0 = y1 - 0.310f;

                var tile = TerminalUiBuilder.CreateFlatPanel($"DesktopIcon_{entry.appId}", iconGrid, new Color(0.045f, 0.105f, 0.165f, 0.86f), new Color(0.18f, 0.34f, 0.45f, 1f), 2f, true);
                TerminalUiBuilder.SetAnchors(tile, x0, y0, x1, y1);
                var inner = TerminalUiBuilder.GetInner(tile);
                var innerImg = inner.GetComponent<Image>();

                var btn = tile.gameObject.AddComponent<Button>();
                btn.targetGraphic = innerImg;
                var cols = btn.colors;
                cols.normalColor = Color.white;
                cols.highlightedColor = new Color(1.22f, 1.32f, 1.42f, 1f);
                cols.pressedColor = new Color(0.85f, 0.95f, 1f, 1f);
                btn.colors = cols;
                tile.gameObject.AddComponent<ButtonHoverFeedback>();
                TerminalUiBuilder.AttachTooltip(tile, entry.tipTitle, entry.tipBody);

                var appId = entry.appId;
                btn.onClick.AddListener(() =>
                {
                    _onUiSound?.Invoke();
                    HideStartMenu();
                    _onOpenApp?.Invoke(appId);
                });

                var icon = TerminalUiBuilder.CreateIcon("AppIcon", inner, entry.icon, entry.accent);
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.28f, 0.36f, 0.72f, 0.84f);

                var keyChip = TerminalUiBuilder.CreateText("Shortcut", inner, _theme, 18f, new Color(0.60f, 0.78f, 0.88f, 0.9f), TextAlignmentOptions.TopLeft, _scaler);
                keyChip.fontStyle = FontStyles.Bold;
                keyChip.text = (i + 1).ToString();
                TerminalUiBuilder.SetAnchors(keyChip.rectTransform, 0.06f, 0.72f, 0.24f, 0.96f);

                var badge = TerminalUiBuilder.CreateText("Badge", inner, _theme, 20f, _theme.TextAmber, TextAlignmentOptions.TopRight, _scaler);
                badge.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetAnchors(badge.rectTransform, 0.56f, 0.72f, 0.94f, 0.96f);

                var label = TerminalUiBuilder.CreateText("Label", inner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.Center, _scaler);
                label.fontStyle = FontStyles.Bold;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.text = DesktopLocalization.GetAppTitle(entry.appId);
                TerminalUiBuilder.SetAnchors(label.rectTransform, 0.04f, 0.04f, 0.96f, 0.34f);

                _desktopIconPanels[i] = tile;
                _desktopIconBadges[i] = badge;
            }

            // 2. Right Desktop Widgets Stack (Mission Goal, Daily Duties, HELIOS)
            var widgetCol = TerminalUiBuilder.CreateRect("DesktopWidgets", workspace);
            TerminalUiBuilder.SetAnchors(widgetCol, 0.665f, 0.02f, 0.995f, 0.98f);
            _widgets = new DesktopWidgetsPanel(widgetCol, _theme, _scaler, _onOpenApp, _onViewAlert, _onUiSound, _onDutyClicked);

            // 3. Floating Desktop Window Layer (topmost inside workspace)
            _windowLayer = TerminalUiBuilder.CreateRect("DesktopWindowLayer", workspace);
            TerminalUiBuilder.SetStretch(_windowLayer);
        }

        private void BuildForecastStrip()
        {
            var bar = TerminalUiBuilder.CreateFlatPanel("DesktopForecastStrip", _desktopRoot, new Color(0.032f, 0.072f, 0.115f, 0.92f), new Color(0.14f, 0.26f, 0.36f, 1f), 1.5f);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.offsetMin = new Vector2(4f, 74f);
            bar.offsetMax = new Vector2(-4f, 104f);

            var inner = TerminalUiBuilder.GetInner(bar);
            _forecastBarText = TerminalUiBuilder.CreateText("ForecastLine", inner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
            _forecastBarText.textWrappingMode = TextWrappingModes.NoWrap;
            _forecastBarText.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_forecastBarText.rectTransform, 0.01f, 0f, 0.99f, 1f);
        }

        private void BuildTaskbar()
        {
            var bar = TerminalUiBuilder.CreateFlatPanel("DesktopTaskbar", _desktopRoot, new Color(0.030f, 0.068f, 0.108f, 0.98f), new Color(0.18f, 0.36f, 0.46f, 1f), 2f, true);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.offsetMin = new Vector2(4f, 2f);
            bar.offsetMax = new Vector2(-4f, 70f);
            var inner = TerminalUiBuilder.GetInner(bar);

            // 1. OUTPOST Start Button
            var startBtn = TerminalUiBuilder.CreateButton("StartButton", inner, DesktopLocalization.Get("desktop.start"), _theme, new Color(0.08f, 0.34f, 0.38f, 1f), UITheme.PaletteTeal, Color.white, 24f, () =>
            {
                _onUiSound?.Invoke();
                if (_startMenuPopup != null)
                {
                    _startMenuPopup.SetActive(!_startMenuPopup.activeSelf);
                    _startMenuPopup.transform.SetAsLastSibling();
                }
            }, _scaler, true);
            TerminalUiBuilder.SetAnchors(startBtn.GetComponent<RectTransform>(), 0.004f, 0.10f, 0.102f, 0.90f);
            var startLbl = startBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (startLbl != null)
            {
                startLbl.textWrappingMode = TextWrappingModes.NoWrap;
                var sIcon = TerminalUiBuilder.CreateIcon("StartIcon", startLbl.transform.parent as RectTransform, TerminalIconId.Home, UITheme.PaletteTeal);
                TerminalUiBuilder.SetAnchors(sIcon.rectTransform, 0.06f, 0.20f, 0.25f, 0.80f);
                TerminalUiBuilder.SetAnchors(startLbl.rectTransform, 0.27f, 0f, 0.96f, 1f);
            }

            // 2. Open Window Taskbar Buttons (up to 4)
            const float winSpan = 0.282f / WindowManager.MaxOpenWindows;
            for (int i = 0; i < WindowManager.MaxOpenWindows; i++)
            {
                int slot = i;
                float x0 = 0.106f + i * winSpan;
                float x1 = x0 + winSpan - 0.004f;

                var wPanel = TerminalUiBuilder.CreateFlatPanel($"TaskWin_{i}", inner, new Color(0.055f, 0.115f, 0.175f, 0.96f), UITheme.PaletteLightBlue, 1.5f);
                TerminalUiBuilder.SetAnchors(wPanel, x0, 0.10f, x1, 0.90f);
                var wInner = TerminalUiBuilder.GetInner(wPanel);
                var wInnerImg = wInner.GetComponent<Image>();

                var wBtn = wPanel.gameObject.AddComponent<Button>();
                wBtn.targetGraphic = wInnerImg;
                wPanel.gameObject.AddComponent<ButtonHoverFeedback>();
                wBtn.onClick.AddListener(() =>
                {
                    _onUiSound?.Invoke();
                    HideStartMenu();
                    _windowManager?.ToggleWindowFromTaskbar(_taskWinApps[slot]);
                });

                var wIcon = TerminalUiBuilder.CreateIcon("Icon", wInner, TerminalIconId.Home, UITheme.PaletteTeal);
                TerminalUiBuilder.SetAnchors(wIcon.rectTransform, 0.04f, 0.20f, 0.18f, 0.80f);

                var wLbl = TerminalUiBuilder.CreateText("Lbl", wInner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
                wLbl.fontStyle = FontStyles.Bold;
                wLbl.textWrappingMode = TextWrappingModes.NoWrap;
                wLbl.overflowMode = TextOverflowModes.Ellipsis;
                TerminalUiBuilder.SetAnchors(wLbl.rectTransform, 0.20f, 0f, 0.98f, 1f);

                var focusBar = TerminalUiBuilder.CreateRect("FocusBar", wInner);
                TerminalUiBuilder.SetAnchors(focusBar, 0.06f, 0.02f, 0.94f, 0.12f);
                focusBar.gameObject.AddComponent<Image>().color = UITheme.PaletteTeal;

                _taskWinPanels[i] = wPanel;
                _taskWinBtns[i] = wBtn;
                _taskWinInners[i] = wInnerImg;
                _taskWinIcons[i] = wIcon;
                _taskWinLabels[i] = wLbl;
                _taskWinFocusBars[i] = focusBar.gameObject;
                wPanel.gameObject.SetActive(false);
            }

            // 3. Status Cluster: Sol + Clock + Rescue + Bell
            var statusBox = TerminalUiBuilder.CreateFlatPanel("TaskbarStatus", inner, new Color(0.045f, 0.095f, 0.145f, 0.95f), new Color(0.16f, 0.30f, 0.40f, 1f), 1.5f);
            TerminalUiBuilder.SetAnchors(statusBox, 0.392f, 0.10f, 0.706f, 0.90f);
            var stInner = TerminalUiBuilder.GetInner(statusBox);

            _solText = TerminalUiBuilder.CreateText("Sol", stInner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.Center, _scaler);
            _solText.fontStyle = FontStyles.Bold;
            _solText.textWrappingMode = TextWrappingModes.NoWrap;
            TerminalUiBuilder.SetAnchors(_solText.rectTransform, 0.01f, 0f, 0.285f, 1f);

            _clockText = TerminalUiBuilder.CreateText("Clock", stInner, _theme, 24f, UITheme.PaletteLightBlue, TextAlignmentOptions.Center, _scaler);
            _clockText.fontStyle = FontStyles.Bold;
            _clockText.textWrappingMode = TextWrappingModes.NoWrap;
            _clockText.text = "06:00";
            TerminalUiBuilder.SetAnchors(_clockText.rectTransform, 0.29f, 0f, 0.425f, 1f);

            _rescueText = TerminalUiBuilder.CreateText("Rescue", stInner, _theme, 24f, _theme.StatusOk, TextAlignmentOptions.Center, _scaler);
            _rescueText.fontStyle = FontStyles.Bold;
            _rescueText.textWrappingMode = TextWrappingModes.NoWrap;
            _rescueText.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_rescueText.rectTransform, 0.43f, 0f, 0.895f, 1f);

            // Bell button with badge
            var bellPanel = TerminalUiBuilder.CreateFlatPanel("AlertBell", stInner, new Color(0.06f, 0.13f, 0.20f, 1f), new Color(0.22f, 0.42f, 0.54f, 1f), 1.5f);
            TerminalUiBuilder.SetAnchors(bellPanel, 0.900f, 0.08f, 0.994f, 0.92f);
            var bellInner = TerminalUiBuilder.GetInner(bellPanel);
            _bellBtn = bellPanel.gameObject.AddComponent<Button>();
            _bellBtn.targetGraphic = bellInner.GetComponent<Image>();
            bellPanel.gameObject.AddComponent<ButtonHoverFeedback>();
            _bellBtn.onClick.AddListener(() =>
            {
                _onUiSound?.Invoke();
                HideStartMenu();
                _onViewAlert?.Invoke();
            });
            TerminalUiBuilder.AttachTooltip(bellPanel, "desktop.bell_tooltip_title", "desktop.bell_tooltip_body");

            _bellIcon = TerminalUiBuilder.CreateIcon("BellIcon", bellInner, TerminalIconId.Warning, UITheme.PaletteLightBlue);
            TerminalUiBuilder.SetAnchors(_bellIcon.rectTransform, 0.14f, 0.14f, 0.86f, 0.86f);

            var badgeRect = TerminalUiBuilder.CreateRect("BellBadge", bellInner);
            TerminalUiBuilder.SetAnchors(badgeRect, 0.50f, 0.48f, 1.02f, 1.02f);
            var badgeImg = badgeRect.gameObject.AddComponent<Image>();
            badgeImg.sprite = TerminalUiArt.GetRoundedRect8px();
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = _theme.StatusCrit;
            _bellBadgeGo = badgeRect.gameObject;

            _bellBadgeText = TerminalUiBuilder.CreateText("Cnt", badgeRect, _theme, 18f, Color.white, TextAlignmentOptions.Center, _scaler);
            _bellBadgeText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetStretch(_bellBadgeText.rectTransform);
            _bellBadgeGo.SetActive(false);

            // 4. Hints, Help, Leave, END SOL
            _hintsBtn = TerminalUiBuilder.CreateButton("DesktopHintsBtn", inner, DesktopLocalization.Get("helios.hints_on"), _theme, new Color(0.055f, 0.11f, 0.16f, 1f), new Color(0.18f, 0.34f, 0.44f, 1f), _theme.TextPrimary, 24f, () =>
            {
                _onUiSound?.Invoke();
                _onToggleHints?.Invoke();
            }, _scaler);
            _hintsLabel = _hintsBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (_hintsLabel != null)
            {
                _hintsLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _hintsLabel.overflowMode = TextOverflowModes.Truncate;
            }
            TerminalUiBuilder.SetAnchors(_hintsBtn.GetComponent<RectTransform>(), 0.710f, 0.10f, 0.794f, 0.90f);

            var helpBtn = TerminalUiBuilder.CreateButton("DesktopHelpBtn", inner, "?", _theme, new Color(0.055f, 0.11f, 0.16f, 1f), new Color(0.18f, 0.34f, 0.44f, 1f), _theme.StatusOk, 24f, () =>
            {
                _onUiSound?.Invoke();
                HideStartMenu();
                _onOpenHelp?.Invoke();
            }, _scaler);
            TerminalUiBuilder.SetAnchors(helpBtn.GetComponent<RectTransform>(), 0.798f, 0.10f, 0.824f, 0.90f);

            _leaveBtn = TerminalUiBuilder.CreateButton("DesktopLeaveBtn", inner, DesktopLocalization.Get("desktop.leave_short"), _theme, new Color(0.06f, 0.12f, 0.18f, 1f), new Color(0.22f, 0.38f, 0.48f, 1f), _theme.TextPrimary, 24f, () =>
            {
                _onUiSound?.Invoke();
                _onLeaveTerminal?.Invoke();
            }, _scaler);
            var leaveLbl = _leaveBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (leaveLbl != null)
            {
                leaveLbl.textWrappingMode = TextWrappingModes.NoWrap;
                leaveLbl.overflowMode = TextOverflowModes.Truncate;
            }
            TerminalUiBuilder.SetAnchors(_leaveBtn.GetComponent<RectTransform>(), 0.828f, 0.10f, 0.894f, 0.90f);
            _leaveBtn.gameObject.SetActive(false);

            _endSolBtn = TerminalUiBuilder.CreateButton("DesktopEndSolBtn", inner, DesktopLocalization.Get("desktop.end_sol_short"), _theme, UITheme.PaletteTeal, Color.white, new Color(0.03f, 0.08f, 0.12f, 1f), 24f, () =>
            {
                _onUiSound?.Invoke();
                HideStartMenu();
                _endSolHover?.ResetScaleImmediate();
                _onEndSol?.Invoke();
            }, _scaler, true);
            _endSolRect = _endSolBtn.GetComponent<RectTransform>();
            _endSolHover = _endSolBtn.GetComponent<ButtonHoverFeedback>();
            var endCols = _endSolBtn.colors;
            endCols.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.65f);
            _endSolBtn.colors = endCols;
            _endSolLabel = _endSolBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (_endSolLabel != null)
            {
                _endSolLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _endSolLabel.overflowMode = TextOverflowModes.Truncate;
                _endSolBgImg = _endSolLabel.transform.parent.GetComponent<Image>();
            }
            TerminalUiBuilder.SetAnchors(_endSolRect, 0.898f, 0.08f, 0.996f, 0.92f);
            _endSolTooltip = TerminalUiBuilder.AttachTooltip(_endSolBtn, "desktop.forecast_tip_title", "ui.end_sol");
        }

        private void BuildStartMenu()
        {
            var menu = TerminalUiBuilder.CreateFlatPanel("DesktopStartMenu", _desktopRoot, new Color(0.032f, 0.078f, 0.125f, 0.98f), UITheme.PaletteTeal, 2f, true);
            menu.anchorMin = new Vector2(0f, 0f);
            menu.anchorMax = new Vector2(0f, 0f);
            menu.pivot = new Vector2(0f, 0f);
            menu.offsetMin = new Vector2(8f, 74f);
            menu.offsetMax = new Vector2(390f, 598f);
            _startMenuPopup = menu.gameObject;

            var inner = TerminalUiBuilder.GetInner(menu);
            var hdr = TerminalUiBuilder.CreateText("StartHdr", inner, _theme, 24f, UITheme.PaletteTeal, TextAlignmentOptions.MidlineLeft, _scaler);
            hdr.fontStyle = FontStyles.Bold;
            hdr.text = DesktopLocalization.Get("desktop.start_header");
            TerminalUiBuilder.SetAnchors(hdr.rectTransform, 0.05f, 0.91f, 0.95f, 0.99f);

            const float step = 0.076f;
            for (int i = 0; i < AppEntries.Length; i++)
            {
                var entry = AppEntries[i];
                float top = 0.90f - i * step;
                float bot = top - step + 0.008f;
                var appId = entry.appId;

                var itemBtn = TerminalUiBuilder.CreateButton($"StartItem_{entry.appId}", inner, $"{i + 1}. {DesktopLocalization.GetAppTitle(appId)}", _theme, new Color(0.05f, 0.11f, 0.17f, 1f), new Color(0.18f, 0.34f, 0.44f, 1f), _theme.TextPrimary, 24f, () =>
                {
                    _onUiSound?.Invoke();
                    HideStartMenu();
                    _onOpenApp?.Invoke(appId);
                }, _scaler);
                var lbl = itemBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.alignment = TextAlignmentOptions.MidlineLeft;
                    lbl.textWrappingMode = TextWrappingModes.NoWrap;
                    var itemInner = lbl.transform.parent as RectTransform;
                    var icon = TerminalUiBuilder.CreateIcon("Icon", itemInner, entry.icon, entry.accent);
                    TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.03f, 0.18f, 0.13f, 0.82f);
                    TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.16f, 0f, 0.96f, 1f);
                }
                TerminalUiBuilder.SetAnchors(itemBtn.GetComponent<RectTransform>(), 0.04f, bot, 0.96f, top);
            }

            var classicBtn = TerminalUiBuilder.CreateButton("StartSwitchClassic", inner, DesktopLocalization.Get("desktop.start_classic"), _theme, new Color(0.07f, 0.16f, 0.24f, 1f), UITheme.PaletteLightBlue, UITheme.PaletteLightBlue, 24f, () =>
            {
                _onUiSound?.Invoke();
                HideStartMenu();
                _onToggleMode?.Invoke();
            }, _scaler);
            TerminalUiBuilder.SetAnchors(classicBtn.GetComponent<RectTransform>(), 0.04f, 0.11f, 0.96f, 0.195f);

            var helpBtn = TerminalUiBuilder.CreateButton("StartHelp", inner, DesktopLocalization.Get("desktop.start_help"), _theme, new Color(0.05f, 0.11f, 0.17f, 1f), _theme.BorderUnfocused, _theme.StatusOk, 24f, () =>
            {
                _onUiSound?.Invoke();
                HideStartMenu();
                _onOpenHelp?.Invoke();
            }, _scaler);
            TerminalUiBuilder.SetAnchors(helpBtn.GetComponent<RectTransform>(), 0.04f, 0.02f, 0.96f, 0.10f);

            _startMenuPopup.SetActive(false);
        }

        private void BuildBellToast()
        {
            var toast = TerminalUiBuilder.CreateFlatPanel("DesktopAlertToast", _desktopRoot, new Color(0.24f, 0.15f, 0.04f, 0.98f), _theme.TextAmber, 2f, true);
            toast.anchorMin = new Vector2(0.50f, 0f);
            toast.anchorMax = new Vector2(0.88f, 0f);
            toast.offsetMin = new Vector2(0f, 110f);
            toast.offsetMax = new Vector2(0f, 166f);
            _bellToastGo = toast.gameObject;

            var inner = TerminalUiBuilder.GetInner(toast);
            var btn = toast.gameObject.AddComponent<Button>();
            btn.targetGraphic = inner.GetComponent<Image>();
            btn.onClick.AddListener(() =>
            {
                _onUiSound?.Invoke();
                _bellToastGo.SetActive(false);
                _onViewAlert?.Invoke();
            });

            var icon = TerminalUiBuilder.CreateIcon("ToastIcon", inner, TerminalIconId.Warning, _theme.TextAmber);
            TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.02f, 0.18f, 0.085f, 0.82f);

            _bellToastText = TerminalUiBuilder.CreateText("ToastText", inner, _theme, 24f, _theme.TextAmber, TextAlignmentOptions.MidlineLeft, _scaler);
            _bellToastText.fontStyle = FontStyles.Bold;
            _bellToastText.textWrappingMode = TextWrappingModes.NoWrap;
            _bellToastText.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_bellToastText.rectTransform, 0.10f, 0f, 0.98f, 1f);

            _bellToastGo.SetActive(false);
        }

        public void Refresh(
            SessionSnapshot s,
            HintEvaluation eval,
            bool isReadOnly,
            bool deferInitialEvent,
            DailyDutyTracker duties,
            bool hintsEnabled)
        {
            if (_desktopRoot == null || s == null) return;

            _pulsingApp = s.GuidedHighlightApp;

            // 1. Pinned status chips
            UpdateChip(0, s.Power, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            UpdateChip(1, s.Oxygen, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            UpdateChip(2, s.Water, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            UpdateChip(3, s.Food, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            UpdateChip(4, s.Shielding, 45f, 20f);
            if (_chipApText != null)
                _chipApText.text = DesktopLocalization.Format("desktop.ap_chip", s.ActionPoints, s.MaxActionPoints);

            // 2. Desktop icon unread badges
            for (int i = 0; i < AppEntries.Length; i++)
            {
                var id = AppEntries[i].appId;
                if (_desktopIconBadges[i] != null)
                {
                    if (id == TerminalAppId.Comms && s.UnreadCommsCount > 0)
                        _desktopIconBadges[i].text = $"[{s.UnreadCommsCount}]";
                    else if (id == TerminalAppId.CommandersLog && s.UnreadLogCount > 0)
                        _desktopIconBadges[i].text = $"[{s.UnreadLogCount}]";
                    else
                        _desktopIconBadges[i].text = "";
                }
                if (_desktopIconPanels[i] != null)
                {
                    bool isOpen = _windowManager != null && _windowManager.IsWindowOpen(id);
                    bool isFocus = _windowManager != null && _windowManager.FocusedApp.HasValue && _windowManager.FocusedApp.Value == id;
                    Color bCol = isFocus ? UITheme.PaletteTeal : (isOpen ? UITheme.PaletteLightBlue : new Color(0.18f, 0.34f, 0.45f, 1f));
                    TerminalUiBuilder.SetBorderColor(_desktopIconPanels[i], bCol);
                }
            }

            // 3. Rescue counter + Alert Bell
            if (_solText != null)
                _solText.text = DesktopLocalization.Format("mc.sol_progress", s.CurrentSol, s.TotalSols).ToUpperInvariant();
            if (_rescueText != null)
            {
                if (s.SolsRemainingForRescue <= 0)
                    _rescueText.text = DesktopLocalization.Get("desktop.rescue_tonight");
                else if (s.SolsRemainingForRescue == 1)
                    _rescueText.text = DesktopLocalization.Get("desktop.rescue_one");
                else
                    _rescueText.text = DesktopLocalization.Format("desktop.rescue_in", s.SolsRemainingForRescue);
                _rescueText.color = s.SolsRemainingForRescue <= 1 ? _theme.TextAmber : _theme.StatusOk;
            }

            bool hasPendingEvent = !deferInitialEvent && s.ActiveEvent != null && !s.ActiveEvent.IsResolved;
            int alertCount = (hasPendingEvent ? 1 : 0) + (s.AlertSeverity >= 1 ? 1 : 0);
            if (_bellBadgeGo != null) _bellBadgeGo.SetActive(alertCount > 0);
            if (_bellBadgeText != null && alertCount > 0) _bellBadgeText.text = alertCount.ToString();
            if (_bellIcon != null) _bellIcon.color = alertCount > 0 ? (s.AlertSeverity >= 2 ? _theme.StatusCrit : _theme.TextAmber) : UITheme.PaletteLightBlue;

            // 4. Hints, Forecast, END SOL
            if (_hintsLabel != null)
                _hintsLabel.text = DesktopLocalization.Get(hintsEnabled ? "helios.hints_on" : "helios.hints_off");

            if (_forecastBarText != null)
            {
                string lbl = DesktopLocalization.Get("desktop.forecast_label");
                _forecastBarText.text = Localization.CurrentLanguage == Language.Arabic
                    ? $"{s.ForecastSummaryLine} <color=#7FDFFF><b>{lbl}</b></color>"
                    : $"<color=#7FDFFF><b>{lbl}</b></color> {s.ForecastSummaryLine}";
            }

            if (_endSolTooltip != null)
                _endSolTooltip.SetRawText(DesktopLocalization.Get("desktop.forecast_tip_title"), s.ForecastSummaryLine);

            if (_endSolBtn != null)
            {
                _endSolBtn.gameObject.SetActive(!isReadOnly);
                if (!isReadOnly)
                {
                    bool eventBlocks = !deferInitialEvent && hasPendingEvent;
                    bool dutiesIncomplete = duties != null && !duties.AllComplete;
                    _endSolBtn.interactable = s.Status == MissionStatus.InProgress;

                    Color btnBg = eventBlocks ? _theme.StatusWarn
                        : (dutiesIncomplete ? _theme.TextAmber : UITheme.PaletteTeal);
                    Color btnBorder = (eventBlocks || dutiesIncomplete)
                        ? Color.Lerp(btnBg, Color.white, 0.25f)
                        : Color.white;
                    if (_endSolBgImg != null) _endSolBgImg.color = btnBg;
                    if (_endSolRect != null) TerminalUiBuilder.SetBorderColor(_endSolRect, btnBorder);

                    if (_endSolLabel != null)
                    {
                        _endSolLabel.text = eventBlocks
                            ? DesktopLocalization.Get("desktop.resolve_event_short")
                            : (dutiesIncomplete
                                ? DesktopLocalization.Format("desktop.tasks_short", duties.CompletedCount)
                                : DesktopLocalization.Get("desktop.end_sol_short"));
                        _endSolLabel.color = new Color(0.03f, 0.08f, 0.12f, 1f);
                    }
                }
            }

            // 5. Widgets + Taskbar open window buttons
            _widgets?.Refresh(s, eval, duties, hintsEnabled);
            RefreshTaskbarWindows();
        }

        public void RefreshTaskbarWindows()
        {
            if (_windowManager == null) return;
            var order = _windowManager.OpenOrder;
            var focused = _windowManager.FocusedApp;

            for (int i = 0; i < WindowManager.MaxOpenWindows; i++)
            {
                if (_taskWinPanels[i] == null) continue;
                if (i >= order.Count)
                {
                    _taskWinPanels[i].gameObject.SetActive(false);
                    continue;
                }

                var appId = order[i];
                _taskWinApps[i] = appId;
                _taskWinPanels[i].gameObject.SetActive(true);

                bool isFocused = focused.HasValue && focused.Value == appId;
                bool isMin = _windowManager.IsWindowMinimized(appId);

                Color border = isFocused ? UITheme.PaletteTeal : (isMin ? _theme.BorderUnfocused : UITheme.PaletteLightBlue);
                Color bg = isFocused ? new Color(0.08f, 0.34f, 0.36f, 0.98f)
                    : (isMin ? new Color(0.035f, 0.07f, 0.11f, 0.90f) : new Color(0.055f, 0.13f, 0.20f, 0.96f));

                TerminalUiBuilder.SetBorderColor(_taskWinPanels[i], border);
                if (_taskWinInners[i] != null) _taskWinInners[i].color = bg;
                if (_taskWinIcons[i] != null)
                {
                    _taskWinIcons[i].sprite = TerminalUiArt.GetIcon(GetIconId(appId));
                    _taskWinIcons[i].color = isFocused ? Color.white : (isMin ? _theme.TextMuted : UITheme.PaletteLightBlue);
                }
                if (_taskWinLabels[i] != null)
                {
                    _taskWinLabels[i].text = DesktopLocalization.GetAppShortLabel(appId);
                    _taskWinLabels[i].color = isFocused ? Color.white : (isMin ? _theme.TextMuted : _theme.TextPrimary);
                }
                if (_taskWinFocusBars[i] != null)
                    _taskWinFocusBars[i].SetActive(isFocused);
            }
        }

        private void UpdateChip(int idx, float value, float okThreshold, float lowThreshold)
        {
            Color col;
            string wordKey;
            if (value < lowThreshold) { col = _theme.StatusCrit; wordKey = "status.word.danger"; }
            else if (value < okThreshold) { col = _theme.StatusWarn; wordKey = "status.word.low"; }
            else { col = _theme.StatusOk; wordKey = "status.word.good"; }

            if (_chipPercents[idx] != null) _chipPercents[idx].text = $"{value:0}%";
            if (_chipBadges[idx] != null) _chipBadges[idx].color = col;
            if (_chipWords[idx] != null) _chipWords[idx].text = DesktopLocalization.Get(wordKey);
        }

        private static int FindIconSlot(TerminalAppId id)
        {
            for (int i = 0; i < AppEntries.Length; i++)
                if (AppEntries[i].appId == id) return i;
            return -1;
        }

        private static TerminalIconId GetIconId(TerminalAppId id)
        {
            for (int i = 0; i < AppEntries.Length; i++)
                if (AppEntries[i].appId == id) return AppEntries[i].icon;
            return TerminalIconId.Home;
        }
    }
}
