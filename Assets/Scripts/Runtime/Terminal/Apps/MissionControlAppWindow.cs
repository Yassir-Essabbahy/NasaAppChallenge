using System;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    /// <summary>
    /// Desktop Mission Control window (9th app slot) displaying the Outpost Alert card,
    /// 7-Sol Rescue Goal track, 5 clickable supply indicators, and 4 crew status rows.
    /// Reuses HintEngine and SessionSnapshot with zero simulation logic duplication.
    /// </summary>
    public class MissionControlAppWindow : WindowView
    {
        private Action<TerminalAppId> _onOpenApp;
        private Action _onViewAlert;

        // Alert card
        private RectTransform _alertPanel;
        private Image _alertInner;
        private Image _alertIcon;
        private TextMeshProUGUI _alertTitle;
        private TextMeshProUGUI _alertBody;
        private Button _alertBtn;
        private Image _alertBtnBg;
        private TextMeshProUGUI _alertBtnLabel;
        private TerminalAppId? _nextTargetApp;
        private bool _nextOpensEvent;

        // Mission goal + 7-sol track
        private TextMeshProUGUI _goalHeadline;
        private readonly Image[] _solPips = new Image[7];
        private readonly TextMeshProUGUI[] _solLabels = new TextMeshProUGUI[7];

        // 5 Supplies
        private readonly TextMeshProUGUI[] _supVals = new TextMeshProUGUI[5];
        private readonly Image[] _supBadges = new Image[5];
        private readonly TextMeshProUGUI[] _supWords = new TextMeshProUGUI[5];

        // 4 Crew
        private readonly TextMeshProUGUI[] _crewNames = new TextMeshProUGUI[4];
        private readonly TextMeshProUGUI[] _crewStats = new TextMeshProUGUI[4];

        public void SetNavigationCallbacks(Action<TerminalAppId> onOpenApp, Action onViewAlert)
        {
            _onOpenApp = onOpenApp;
            _onViewAlert = onViewAlert;
        }

        protected override void BuildContent(RectTransform content)
        {
            // 1. Outpost Alert / Next Step Card
            _alertPanel = TerminalUiBuilder.CreateFlatPanel("McAlertCard", content, Theme.PanelBg, Theme.StatusWarn, 2f, true);
            TerminalUiBuilder.SetAnchors(_alertPanel, 0f, 0.775f, 1f, 0.995f);
            var aInner = TerminalUiBuilder.GetInner(_alertPanel);
            _alertInner = aInner.GetComponent<Image>();

            _alertIcon = TerminalUiBuilder.CreateIcon("AlertIcon", aInner, TerminalIconId.Scan, Theme.StatusOk);
            TerminalUiBuilder.SetAnchors(_alertIcon.rectTransform, 0.02f, 0.24f, 0.085f, 0.76f);

            _alertTitle = TerminalUiBuilder.CreateText("AlertTitle", aInner, Theme, Theme.BodyFontSize, Theme.StatusOk, TextAlignmentOptions.MidlineLeft, Scaler);
            _alertTitle.fontStyle = FontStyles.Bold;
            _alertTitle.textWrappingMode = TextWrappingModes.NoWrap;
            TerminalUiBuilder.SetAnchors(_alertTitle.rectTransform, 0.10f, 0.56f, 0.69f, 0.94f);

            _alertBody = TerminalUiBuilder.CreateText("AlertBody", aInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            _alertBody.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_alertBody.rectTransform, 0.10f, 0.06f, 0.69f, 0.56f);

            _alertBtn = TerminalUiBuilder.CreateButton("AlertBtn", aInner, DesktopLocalization.Get("desktop.view_alert"), Theme, Theme.StatusOk, Color.white, new Color(0.03f, 0.08f, 0.12f, 1f), Theme.BodyFontSize, OnAlertActionClicked, Scaler);
            _alertBtnLabel = _alertBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (_alertBtnLabel != null)
            {
                _alertBtnLabel.textWrappingMode = TextWrappingModes.NoWrap;
                _alertBtnBg = _alertBtnLabel.transform.parent.GetComponent<Image>();
            }
            TerminalUiBuilder.SetAnchors(_alertBtn.GetComponent<RectTransform>(), 0.705f, 0.18f, 0.98f, 0.82f);

            // 2. Mission Goal Card + 7-Sol Progress Strip
            var goalPanel = TerminalUiBuilder.CreateFlatPanel("McGoalCard", content, new Color(0.035f, 0.125f, 0.165f, 0.96f), UITheme.PaletteTeal, 2f, true);
            TerminalUiBuilder.SetAnchors(goalPanel, 0f, 0.555f, 1f, 0.758f);
            var gInner = TerminalUiBuilder.GetInner(goalPanel);

            var gIcon = TerminalUiBuilder.CreateIcon("GoalIcon", gInner, TerminalIconId.Goal, Theme.StatusOk);
            TerminalUiBuilder.SetAnchors(gIcon.rectTransform, 0.02f, 0.56f, 0.065f, 0.92f);

            var gHdr = TerminalUiBuilder.CreateText("GoalHdr", gInner, Theme, Theme.BodyFontSize, Theme.StatusOk, TextAlignmentOptions.MidlineLeft, Scaler);
            gHdr.fontStyle = FontStyles.Bold;
            gHdr.textWrappingMode = TextWrappingModes.NoWrap;
            gHdr.text = DesktopLocalization.Get("desktop.mission_goal_short");
            TerminalUiBuilder.SetAnchors(gHdr.rectTransform, 0.075f, 0.54f, 0.41f, 0.94f);

            _goalHeadline = TerminalUiBuilder.CreateText("GoalHeadline", gInner, Theme, Theme.HeaderFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
            _goalHeadline.fontStyle = FontStyles.Bold;
            _goalHeadline.textWrappingMode = TextWrappingModes.NoWrap;
            _goalHeadline.overflowMode = TextOverflowModes.Ellipsis;
            TerminalUiBuilder.SetAnchors(_goalHeadline.rectTransform, 0.42f, 0.52f, 0.98f, 0.96f);

            var rounded = TerminalUiArt.GetRoundedRect8px();
            for (int i = 0; i < 7; i++)
            {
                float x0 = 0.02f + i * 0.138f;
                float x1 = x0 + 0.128f;
                var pip = TerminalUiBuilder.CreateRect($"McSolPip_{i + 1}", gInner);
                TerminalUiBuilder.SetAnchors(pip, x0, 0.10f, x1, 0.44f);
                var pImg = pip.gameObject.AddComponent<Image>();
                pImg.sprite = rounded;
                pImg.type = Image.Type.Sliced;
                pImg.color = new Color(0.07f, 0.14f, 0.21f, 1f);
                _solPips[i] = pImg;

                var pTxt = TerminalUiBuilder.CreateCompactDebugText("Lbl", pip, Theme, 18f, Theme.TextMuted, TextAlignmentOptions.Center, Scaler);
                pTxt.fontStyle = FontStyles.Bold;
                pTxt.text = DesktopLocalization.Format(i == 6 ? "ui.sol_pip_last" : "ui.sol_pip", i + 1);
                TerminalUiBuilder.SetStretch(pTxt.rectTransform);
                _solLabels[i] = pTxt;
            }

            // 3. Bottom Left: 5 Supplies
            var supPanel = TerminalUiBuilder.CreateFlatPanel("McSupplies", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(supPanel, 0f, 0.005f, 0.50f, 0.538f);
            var sInner = TerminalUiBuilder.GetInner(supPanel);

            (string key, TerminalIconId icon, Color accent, TerminalAppId app)[] supDefs =
            {
                ("mc.status.power",  TerminalIconId.Bolt,   UITheme.PaletteTeal,      TerminalAppId.PowerGrid),
                ("mc.status.oxygen", TerminalIconId.Oxygen, UITheme.PaletteLightBlue, TerminalAppId.LifeSupport),
                ("mc.status.water",  TerminalIconId.Water,  UITheme.PaletteLightBlue, TerminalAppId.LifeSupport),
                ("mc.status.food",   TerminalIconId.Food,   UITheme.PaletteTeal,      TerminalAppId.Greenhouse),
                ("mc.status.shield", TerminalIconId.Shield, UITheme.PaletteLightBlue, TerminalAppId.Radiation)
            };

            for (int i = 0; i < 5; i++)
            {
                var def = supDefs[i];
                float top = 0.975f - i * 0.192f;
                float bot = top - 0.175f;

                var row = TerminalUiBuilder.CreateRect($"McSupRow_{i}", sInner);
                TerminalUiBuilder.SetAnchors(row, 0.02f, bot, 0.98f, top);
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = rounded;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.06f, 0.12f, 0.18f, 0.65f);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                row.gameObject.AddComponent<ButtonHoverFeedback>();
                var target = def.app;
                btn.onClick.AddListener(() => _onOpenApp?.Invoke(target));

                var icon = TerminalUiBuilder.CreateIcon("Icon", row, def.icon, def.accent);
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.02f, 0.15f, 0.10f, 0.85f);

                var lbl = TerminalUiBuilder.CreateText("Lbl", row, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                lbl.fontStyle = FontStyles.Bold;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                lbl.text = DesktopLocalization.Get(def.key);
                TerminalUiBuilder.SetAnchors(lbl.rectTransform, 0.12f, 0f, 0.48f, 1f);

                var val = TerminalUiBuilder.CreateText("Val", row, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineRight, Scaler);
                val.fontStyle = FontStyles.Bold;
                val.textWrappingMode = TextWrappingModes.NoWrap;
                TerminalUiBuilder.SetAnchors(val.rectTransform, 0.48f, 0f, 0.70f, 1f);
                _supVals[i] = val;

                var badge = TerminalUiBuilder.CreateRect("Badge", row);
                TerminalUiBuilder.SetAnchors(badge, 0.72f, 0.12f, 0.98f, 0.88f);
                var bImg = badge.gameObject.AddComponent<Image>();
                bImg.sprite = rounded;
                bImg.type = Image.Type.Sliced;
                bImg.color = Theme.StatusOk;
                _supBadges[i] = bImg;

                var bTxt = TerminalUiBuilder.CreateCompactDebugText("Word", badge, Theme, 19f, new Color(0.03f, 0.08f, 0.12f, 1f), TextAlignmentOptions.Center, Scaler);
                bTxt.fontStyle = FontStyles.Bold;
                TerminalUiBuilder.SetStretch(bTxt.rectTransform);
                _supWords[i] = bTxt;
            }

            // 4. Bottom Right: 4 Crew Status Rows
            var crewPanel = TerminalUiBuilder.CreateFlatPanel("McCrew", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(crewPanel, 0.515f, 0.005f, 1f, 0.538f);
            var cInner = TerminalUiBuilder.GetInner(crewPanel);

            for (int i = 0; i < 4; i++)
            {
                float top = 0.975f - i * 0.242f;
                float bot = top - 0.224f;

                var row = TerminalUiBuilder.CreateRect($"McCrewRow_{i}", cInner);
                TerminalUiBuilder.SetAnchors(row, 0.025f, bot, 0.975f, top);
                var bg = row.gameObject.AddComponent<Image>();
                bg.sprite = rounded;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.06f, 0.12f, 0.18f, 0.65f);

                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                row.gameObject.AddComponent<ButtonHoverFeedback>();
                btn.onClick.AddListener(() => _onOpenApp?.Invoke(TerminalAppId.Crew));

                var nameTxt = TerminalUiBuilder.CreateText("Name", row, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.textWrappingMode = TextWrappingModes.NoWrap;
                nameTxt.overflowMode = TextOverflowModes.Ellipsis;
                TerminalUiBuilder.SetAnchors(nameTxt.rectTransform, 0.04f, 0.48f, 0.96f, 0.96f);
                _crewNames[i] = nameTxt;

                var statTxt = TerminalUiBuilder.CreateText("Stat", row, Theme, 20f, Theme.StatusOk, TextAlignmentOptions.MidlineLeft, Scaler);
                statTxt.fontStyle = FontStyles.Bold;
                statTxt.textWrappingMode = TextWrappingModes.NoWrap;
                statTxt.overflowMode = TextOverflowModes.Ellipsis;
                TerminalUiBuilder.SetAnchors(statTxt.rectTransform, 0.04f, 0.04f, 0.96f, 0.48f);
                _crewStats[i] = statTxt;
            }
        }

        private void OnAlertActionClicked()
        {
            if (_nextOpensEvent)
                _onViewAlert?.Invoke();
            else if (_nextTargetApp.HasValue)
                _onOpenApp?.Invoke(_nextTargetApp.Value);
        }

        public override void Refresh(SessionSnapshot s)
        {
            if (s == null) return;

            var eval = HintEngine.Evaluate(s);
            bool hasPendingEvent = s.ActiveEvent != null && !s.ActiveEvent.IsResolved;
            bool hazardCrit = s.AlertSeverity == 2 || s.SolarFlareActive;
            bool airlockLocked = s.SolarFlareActive || s.DustStormSolsRemaining > 0;
            bool showAlertStyle = hasPendingEvent || eval.IsUrgent || s.AlertSeverity >= 1 || airlockLocked;

            Color cardBg;
            Color cardBorder;
            Color accentCol;
            TerminalIconId cardIconId;
            if (hazardCrit)
            {
                cardBg = new Color(0.29f, 0.09f, 0.12f, 0.96f);
                cardBorder = Theme.StatusCrit;
                accentCol = Theme.StatusCrit;
                cardIconId = TerminalIconId.Hazard;
            }
            else if (showAlertStyle)
            {
                cardBg = new Color(0.25f, 0.17f, 0.04f, 0.96f);
                cardBorder = Theme.StatusWarn;
                accentCol = Theme.StatusWarn;
                cardIconId = (!hasPendingEvent && s.AlertSeverity == 0 && airlockLocked) ? TerminalIconId.Lock : TerminalIconId.Warning;
            }
            else
            {
                cardBg = new Color(0.04f, 0.12f, 0.17f, 0.95f);
                cardBorder = UITheme.PaletteTeal;
                accentCol = Theme.StatusOk;
                cardIconId = TerminalIconId.Scan;
            }

            TerminalUiBuilder.SetBorderColor(_alertPanel, cardBorder);
            if (_alertInner != null) _alertInner.color = cardBg;
            if (_alertIcon != null)
            {
                _alertIcon.sprite = TerminalUiArt.GetIcon(cardIconId);
                _alertIcon.color = (cardIconId == TerminalIconId.Warning || cardIconId == TerminalIconId.Hazard) ? Color.white : accentCol;
            }
            if (_alertTitle != null)
            {
                _alertTitle.text = DesktopLocalization.Get(hazardCrit ? "desktop.status_critical" : (showAlertStyle ? "desktop.status_alert" : "desktop.status_nominal"));
                _alertTitle.color = accentCol;
            }

            string body = hasPendingEvent ? DesktopLocalization.Get("next.event_active") : eval.NextStepText;
            if (airlockLocked) body += " " + DesktopLocalization.Get("desktop.eva_locked");
            _alertBody.text = body;

            _nextTargetApp = eval.TargetApp;
            _nextOpensEvent = !eval.TargetApp.HasValue && hasPendingEvent;
            _alertBtnLabel.text = (_nextOpensEvent ? DesktopLocalization.Get("desktop.view_alert") : eval.NextStepButtonText).ToUpperInvariant();
            _alertBtnBg.color = hazardCrit ? Theme.StatusCrit : (showAlertStyle ? Theme.StatusWarn : UITheme.PaletteTeal);
            TerminalUiBuilder.SetBorderColor(_alertBtn.GetComponent<RectTransform>(), Color.Lerp(_alertBtnBg.color, Color.white, 0.28f));

            // Goal + 7-sol track
            _goalHeadline.text = s.SolsRemainingForRescue > 0
                ? DesktopLocalization.Get("desktop.goal_survive")
                : DesktopLocalization.Get("mc.goal_last_sol");
            for (int i = 0; i < 7; i++)
            {
                int solNum = i + 1;
                if (solNum < s.CurrentSol)
                {
                    _solPips[i].color = Theme.StatusOk;
                    _solLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else if (solNum == s.CurrentSol)
                {
                    _solPips[i].color = Theme.TextAmber;
                    _solLabels[i].color = new Color(0.03f, 0.08f, 0.12f, 1f);
                }
                else
                {
                    _solPips[i].color = new Color(0.07f, 0.14f, 0.21f, 1f);
                    _solLabels[i].color = Theme.TextMuted;
                }
            }

            // 5 Supplies
            SetSupplyRow(0, s.Power, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            SetSupplyRow(1, s.Oxygen, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            SetSupplyRow(2, s.Water, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            SetSupplyRow(3, s.Food, s.StatusLightOkThreshold, s.StatusLightLowThreshold);
            SetSupplyRow(4, s.Shielding, 45f, 20f);

            // 4 Crew
            for (int i = 0; i < 4 && i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                _crewNames[i].text = c.Name;
                Color cCol = !c.IsAlive || c.Health < 35f ? Theme.StatusCrit
                    : (c.IsFallenCommander || c.Health < 65f ? Theme.StatusWarn : Theme.StatusOk);
                _crewStats[i].color = cCol;
                _crewStats[i].text = $"{c.Health:0} {DesktopLocalization.Get("ui.health")} · {c.StatusTag}";
            }
        }

        private void SetSupplyRow(int idx, float value, float okThreshold, float lowThreshold)
        {
            Color col;
            string key;
            if (value < lowThreshold) { col = Theme.StatusCrit; key = "status.word.danger"; }
            else if (value < okThreshold) { col = Theme.StatusWarn; key = "status.word.low"; }
            else { col = Theme.StatusOk; key = "status.word.good"; }

            _supVals[idx].text = $"{value:0}%";
            _supBadges[idx].color = col;
            _supWords[idx].text = DesktopLocalization.Get(key);
        }
    }
}
