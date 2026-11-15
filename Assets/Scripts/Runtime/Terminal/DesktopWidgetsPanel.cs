using System;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal
{
    /// <summary>
    /// Right-column Desktop OS widgets:
    /// 1) Mission Goal card with 7-sol progress track + current Alert quick action
    /// 2) Daily Shift Duties sticky-note checklist (click any pending item to jump to it)
    /// 3) HELIOS hint bubble docked cleanly above the taskbar when hints are enabled.
    /// </summary>
    public class DesktopWidgetsPanel
    {
        private readonly RectTransform _root;
        private readonly UITheme _theme;
        private readonly AccessibilityTextScaler _scaler;
        private readonly Action<TerminalAppId> _onOpenApp;
        private readonly Action _onViewAlert;
        private readonly Action _onUiSound;
        private readonly Action<int> _onDutyClicked;

        // Mission Goal Card
        private TextMeshProUGUI _goalHeadline;
        private readonly Image[] _solPips = new Image[7];
        private readonly TextMeshProUGUI[] _solLabels = new TextMeshProUGUI[7];
        private RectTransform _alertStrip;
        private Image _alertStripBg;
        private Image _alertStripIcon;
        private TextMeshProUGUI _alertStripText;
        private Button _alertBtn;
        private Image _alertBtnBg;
        private TextMeshProUGUI _alertBtnLabel;
        private TerminalAppId? _nextTargetApp;
        private bool _nextOpensEvent;

        // Daily Shift Duties Card
        private GameObject _stickyCard;
        private RectTransform _stickyPanel;
        private Image _stickyBg;
        private Image _stickyPin;
        private Image _stickyIcon;
        private TextMeshProUGUI _stickyTitle;
        private readonly Image[] _checkIcons = new Image[3];
        private readonly TextMeshProUGUI[] _checkLabels = new TextMeshProUGUI[3];

        // HELIOS Bubble
        private GameObject _heliosCard;
        private Image _heliosIcon;
        private TextMeshProUGUI _heliosTitle;
        private TextMeshProUGUI _heliosBody;

        public DesktopWidgetsPanel(
            RectTransform parent,
            UITheme theme,
            AccessibilityTextScaler scaler,
            Action<TerminalAppId> onOpenApp,
            Action onViewAlert,
            Action onUiSound,
            Action<int> onDutyClicked = null)
        {
            _root = parent;
            _theme = theme;
            _scaler = scaler;
            _onOpenApp = onOpenApp;
            _onViewAlert = onViewAlert;
            _onUiSound = onUiSound;
            _onDutyClicked = onDutyClicked;

            Build();
        }

        private void Build()
        {
            var rounded = TerminalUiArt.GetRoundedRect8px();

            // 1. Mission Goal Card (top right widget: 0.62..1.00)
            var goalPanel = TerminalUiBuilder.CreateFlatPanel("Widget_MissionGoal", _root, new Color(0.035f, 0.125f, 0.170f, 0.94f), UITheme.PaletteTeal, 2f, true);
            TerminalUiBuilder.SetAnchors(goalPanel, 0f, 0.61f, 1f, 1f);
            var gInner = TerminalUiBuilder.GetInner(goalPanel);

            var gIcon = TerminalUiBuilder.CreateIcon("GoalIcon", gInner, TerminalIconId.Goal, _theme.StatusOk);
            TerminalUiBuilder.SetAnchors(gIcon.rectTransform, 0.035f, 0.78f, 0.105f, 0.94f);

            var gHdr = TerminalUiBuilder.CreateText("GoalHdr", gInner, _theme, 24f, _theme.StatusOk, TextAlignmentOptions.MidlineLeft, _scaler);
            gHdr.fontStyle = FontStyles.Bold;
            gHdr.text = DesktopLocalization.Get("desktop.mission_goal_short");
            TerminalUiBuilder.SetAnchors(gHdr.rectTransform, 0.12f, 0.76f, 0.96f, 0.96f);

            _goalHeadline = TerminalUiBuilder.CreateText("GoalHeadline", gInner, _theme, 26f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
            _goalHeadline.fontStyle = FontStyles.Bold;
            _goalHeadline.textWrappingMode = TextWrappingModes.NoWrap;
            _goalHeadline.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_goalHeadline.rectTransform, 0.035f, 0.54f, 0.96f, 0.75f);

            for (int i = 0; i < 7; i++)
            {
                float x0 = 0.035f + i * 0.135f;
                float x1 = x0 + 0.122f;
                var pip = TerminalUiBuilder.CreateRect($"WidgetSolPip_{i + 1}", gInner);
                TerminalUiBuilder.SetAnchors(pip, x0, 0.34f, x1, 0.51f);
                var pImg = pip.gameObject.AddComponent<Image>();
                pImg.sprite = rounded;
                pImg.type = Image.Type.Sliced;
                pImg.color = new Color(0.07f, 0.14f, 0.21f, 1f);
                _solPips[i] = pImg;

                var pTxt = TerminalUiBuilder.CreateText("Lbl", pip, _theme, 20f, _theme.TextMuted, TextAlignmentOptions.Center, _scaler);
                pTxt.fontStyle = FontStyles.Bold;
                pTxt.text = i == 6 ? $"S{i + 1}*" : $"S{i + 1}";
                TerminalUiBuilder.SetStretch(pTxt.rectTransform);
                _solLabels[i] = pTxt;
            }

            // Alert action sub-strip inside Mission Goal widget
            _alertStrip = TerminalUiBuilder.CreateFlatPanel("GoalAlertStrip", gInner, new Color(0.04f, 0.10f, 0.15f, 0.96f), UITheme.PaletteLightBlue, 1.5f);
            TerminalUiBuilder.SetAnchors(_alertStrip, 0.03f, 0.05f, 0.97f, 0.30f);
            var aInner = TerminalUiBuilder.GetInner(_alertStrip);
            _alertStripBg = aInner.GetComponent<Image>();

            _alertStripIcon = TerminalUiBuilder.CreateIcon("Icon", aInner, TerminalIconId.Scan, _theme.StatusOk);
            TerminalUiBuilder.SetAnchors(_alertStripIcon.rectTransform, 0.02f, 0.18f, 0.10f, 0.82f);

            _alertStripText = TerminalUiBuilder.CreateText("Text", aInner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
            _alertStripText.textWrappingMode = TextWrappingModes.NoWrap;
            _alertStripText.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_alertStripText.rectTransform, 0.12f, 0.04f, 0.62f, 0.96f);

            _alertBtn = TerminalUiBuilder.CreateButton("ActionBtn", aInner, DesktopLocalization.Get("ui.open"), _theme, UITheme.PaletteTeal, Color.white, new Color(0.03f, 0.08f, 0.12f, 1f), 24f, OnAlertClicked, _scaler);
            _alertBtnLabel = _alertBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (_alertBtnLabel != null)
            {
                _alertBtnLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _alertBtnLabel.overflowMode = TextOverflowModes.Ellipsis;
                _alertBtnBg = _alertBtnLabel.transform.parent.GetComponent<Image>();
            }
            TerminalUiBuilder.SetAnchors(_alertBtn.GetComponent<RectTransform>(), 0.635f, 0.10f, 0.985f, 0.90f);

            // 2. Daily Shift Duties Sticky-Note Checklist (middle right: 0.285..0.585)
            var stickyPanel = TerminalUiBuilder.CreateFlatPanel("Widget_StickyNote", _root, new Color(0.20f, 0.16f, 0.05f, 0.95f), _theme.TextAmber, 2f, true);
            TerminalUiBuilder.SetAnchors(stickyPanel, 0f, 0.285f, 1f, 0.585f);
            _stickyPanel = stickyPanel;
            _stickyCard = stickyPanel.gameObject;
            var sInner = TerminalUiBuilder.GetInner(stickyPanel);
            _stickyBg = sInner.GetComponent<Image>();

            var pinBar = TerminalUiBuilder.CreateRect("StickyAccent", sInner);
            TerminalUiBuilder.SetAnchors(pinBar, 0f, 0.92f, 1f, 1f);
            var pinImg = pinBar.gameObject.AddComponent<Image>();
            pinImg.color = _theme.TextAmber;
            pinImg.raycastTarget = false;
            _stickyPin = pinImg;

            var sIcon = TerminalUiBuilder.CreateIcon("StickyIcon", sInner, TerminalIconId.Log, _theme.TextAmber);
            TerminalUiBuilder.SetAnchors(sIcon.rectTransform, 0.035f, 0.69f, 0.105f, 0.87f);
            _stickyIcon = sIcon;

            var sTitle = TerminalUiBuilder.CreateText("StickyTitle", sInner, _theme, 24f, _theme.TextAmber, TextAlignmentOptions.MidlineLeft, _scaler);
            sTitle.fontStyle = FontStyles.Bold;
            sTitle.textWrappingMode = TextWrappingModes.NoWrap;
            sTitle.overflowMode = TextOverflowModes.Truncate;
            sTitle.text = DesktopLocalization.Get("onboard.title").ToUpperInvariant();
            TerminalUiBuilder.SetAnchors(sTitle.rectTransform, 0.12f, 0.67f, 0.96f, 0.89f);
            _stickyTitle = sTitle;

            string[] keys = { "onboard.step1", "onboard.step2", "onboard.step3" };
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                float top = 0.64f - i * 0.20f;
                float bot = top - 0.185f;

                var rowRect = TerminalUiBuilder.CreateRect($"DutyRow_{i}", sInner);
                TerminalUiBuilder.SetAnchors(rowRect, 0.025f, bot, 0.975f, top);
                var rowImg = rowRect.gameObject.AddComponent<Image>();
                rowImg.sprite = rounded;
                rowImg.type = Image.Type.Sliced;
                rowImg.color = Color.white;
                var rowBtn = rowRect.gameObject.AddComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                var cols = rowBtn.colors;
                cols.normalColor = new Color(0.06f, 0.09f, 0.13f, 0.34f);
                cols.highlightedColor = new Color(0.14f, 0.30f, 0.38f, 0.82f);
                cols.pressedColor = new Color(0.10f, 0.22f, 0.30f, 0.92f);
                cols.selectedColor = cols.normalColor;
                rowBtn.colors = cols;
                rowBtn.onClick.AddListener(() =>
                {
                    _onUiSound?.Invoke();
                    _onDutyClicked?.Invoke(slot);
                });

                var chk = TerminalUiBuilder.CreateIcon($"Check_{i}", rowRect, TerminalIconId.CheckBox, _theme.TextAmber);
                chk.raycastTarget = false;
                TerminalUiBuilder.SetAnchors(chk.rectTransform, 0.015f, 0.12f, 0.09f, 0.88f);
                _checkIcons[i] = chk;

                var lbl = TerminalUiBuilder.CreateText($"Step_{i}", rowRect, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.MidlineLeft, _scaler);
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.overflowMode = TextOverflowModes.Truncate;
                lbl.raycastTarget = false;
                lbl.text = DesktopLocalization.Get(keys[i]);
                TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.105f, 0f, 0.985f, 1f);
                _checkLabels[i] = lbl;
            }

            // 3. HELIOS Hint Bubble (bottom right above taskbar: 0.00..0.26)
            var heliosPanel = TerminalUiBuilder.CreateFlatPanel("Widget_HeliosBubble", _root, new Color(0.038f, 0.105f, 0.165f, 0.95f), _theme.AccentHelp, 2f, true);
            TerminalUiBuilder.SetAnchors(heliosPanel, 0f, 0f, 1f, 0.26f);
            _heliosCard = heliosPanel.gameObject;
            var hInner = TerminalUiBuilder.GetInner(heliosPanel);

            _heliosIcon = TerminalUiBuilder.CreateIcon("HeliosIcon", hInner, TerminalIconId.Help, _theme.AccentHelp);
            TerminalUiBuilder.SetAnchors(_heliosIcon.rectTransform, 0.035f, 0.66f, 0.105f, 0.90f);

            _heliosTitle = TerminalUiBuilder.CreateText("HeliosTitle", hInner, _theme, 24f, _theme.AccentHelp, TextAlignmentOptions.MidlineLeft, _scaler);
            _heliosTitle.fontStyle = FontStyles.Bold;
            _heliosTitle.text = DesktopLocalization.Get("helios.name");
            TerminalUiBuilder.SetAnchors(_heliosTitle.rectTransform, 0.12f, 0.64f, 0.96f, 0.92f);

            _heliosBody = TerminalUiBuilder.CreateText("HeliosBody", hInner, _theme, 24f, _theme.TextPrimary, TextAlignmentOptions.TopLeft, _scaler);
            _heliosBody.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_heliosBody.rectTransform, 0.04f, 0.06f, 0.96f, 0.63f);
        }

        private void OnAlertClicked()
        {
            _onUiSound?.Invoke();
            if (_nextOpensEvent)
                _onViewAlert?.Invoke();
            else if (_nextTargetApp.HasValue)
                _onOpenApp?.Invoke(_nextTargetApp.Value);
        }

        public void Refresh(SessionSnapshot s, HintEvaluation eval, DailyDutyTracker duties, bool hintsEnabled)
        {
            if (s == null) return;

            _goalHeadline.text = s.SolsRemainingForRescue > 0
                ? DesktopLocalization.Get("desktop.goal_survive")
                : DesktopLocalization.Get("mc.goal_last_sol");

            for (int i = 0; i < 7; i++)
            {
                int solNum = i + 1;
                if (solNum < s.CurrentSol)
                {
                    _solPips[i].color = _theme.StatusOk;
                    _solLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else if (solNum == s.CurrentSol)
                {
                    _solPips[i].color = _theme.TextAmber;
                    _solLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else
                {
                    _solPips[i].color = new Color(0.07f, 0.14f, 0.21f, 1f);
                    _solLabels[i].color = _theme.TextMuted;
                }
            }

            bool hasPendingEvent = s.ActiveEvent != null && !s.ActiveEvent.IsResolved;
            bool hazardCrit = s.AlertSeverity == 2 || s.SolarFlareActive;
            bool showAlert = hasPendingEvent || eval.IsUrgent || s.AlertSeverity >= 1 || s.DustStormSolsRemaining > 0;
            Color accent = hazardCrit ? _theme.StatusCrit : (showAlert ? _theme.StatusWarn : _theme.StatusOk);

            TerminalUiBuilder.SetBorderColor(_alertStrip, accent);
            if (_alertStripBg != null)
                _alertStripBg.color = hazardCrit ? new Color(0.26f, 0.08f, 0.11f, 0.95f) : (showAlert ? new Color(0.22f, 0.15f, 0.04f, 0.95f) : new Color(0.04f, 0.11f, 0.16f, 0.95f));
            if (_alertStripIcon != null)
            {
                _alertStripIcon.sprite = TerminalUiArt.GetIcon(hazardCrit ? TerminalIconId.Hazard : (showAlert ? TerminalIconId.Warning : TerminalIconId.Scan));
                _alertStripIcon.color = (showAlert || hazardCrit) ? Color.white : _theme.StatusOk;
            }
            _alertStripText.text = DesktopLocalization.Get(hazardCrit ? "desktop.status_critical" : (showAlert ? "desktop.status_alert" : "desktop.status_nominal"));
            _alertStripText.color = accent;

            _nextTargetApp = eval.TargetApp;
            _nextOpensEvent = !eval.TargetApp.HasValue && hasPendingEvent;
            _alertBtnLabel.text = _nextOpensEvent
                ? DesktopLocalization.Get("desktop.view_alert")
                : DesktopLocalization.GetAppShortLabel(eval.TargetApp ?? DesktopLocalization.MissionControlAppId).ToUpperInvariant();
            _alertBtnBg.color = accent;

            // Daily Shift Duties Card: visible on every Sol so players know what must be completed before End Sol
            bool showDuties = duties != null;
            _stickyCard.SetActive(showDuties);
            if (showDuties)
            {
                var items = duties.Evaluate(s);
                bool ready = duties.AllComplete;
                Color dutyAccent = ready ? _theme.StatusOk : _theme.TextAmber;

                TerminalUiBuilder.SetBorderColor(_stickyPanel, dutyAccent);
                if (_stickyBg != null)
                    _stickyBg.color = ready ? new Color(0.035f, 0.145f, 0.130f, 0.95f) : new Color(0.19f, 0.15f, 0.05f, 0.95f);
                if (_stickyPin != null)
                    _stickyPin.color = dutyAccent;
                if (_stickyIcon != null)
                {
                    _stickyIcon.sprite = TerminalUiArt.GetIcon(ready ? TerminalIconId.Check : TerminalIconId.Log);
                    _stickyIcon.color = dutyAccent;
                }
                if (_stickyTitle != null)
                {
                    _stickyTitle.color = dutyAccent;
                    _stickyTitle.text = ready
                        ? DesktopLocalization.Format("duty.title_ready", s.CurrentSol)
                        : DesktopLocalization.Format("duty.title_sol", s.CurrentSol, duties.CompletedCount, items.Length);
                }

                for (int i = 0; i < 3 && i < items.Length; i++)
                {
                    _checkIcons[i].sprite = TerminalUiArt.GetIcon(items[i].IsDone ? TerminalIconId.Check : TerminalIconId.CheckBox);
                    _checkIcons[i].color = items[i].IsDone ? _theme.StatusOk : _theme.TextAmber;
                    _checkLabels[i].text = items[i].Label;
                    _checkLabels[i].color = items[i].IsDone ? _theme.StatusOk : _theme.TextPrimary;
                }
            }

            // HELIOS Hint Bubble
            _heliosCard.SetActive(hintsEnabled);
            if (hintsEnabled)
            {
                _heliosBody.text = !string.IsNullOrEmpty(eval.HeliosHintText)
                    ? eval.HeliosHintText
                    : DesktopLocalization.Get("hint.idle_ok");
            }
        }
    }
}
