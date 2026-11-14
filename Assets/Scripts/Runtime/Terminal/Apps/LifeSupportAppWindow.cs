using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class LifeSupportAppWindow : WindowView
    {
        private TextMeshProUGUI _introText;
        private StatusBarWidget _o2Bar;
        private StatusBarWidget _waterBar;
        private StatusBarWidget _scrubberBar;
        private StatusBarWidget _recyclerBar;
        private TextMeshProUGUI _statusLineText;
        private Image _statusGaugeIcon;
        private FoldoutWidget _detailsFoldout;
        private TextMeshProUGUI _detailsText;
        private Button _repairBtn;
        private TextMeshProUGUI _repairLabel;

        protected override void BuildContent(RectTransform content)
        {
            _introText = TerminalUiBuilder.CreateText("Intro", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            _introText.text = Localization.Get("ls.friendly_intro");
            TerminalUiBuilder.SetAnchors(_introText.rectTransform, 0f, 0.90f, 1f, 1f);

            _o2Bar = TerminalUiBuilder.CreateStatusBar("O2Bar", content, Localization.Get("mc.status.oxygen"), Theme, Scaler, 0f, 0.72f, 1f, 0.89f, TerminalIconId.Oxygen, Theme.AccentLifeSupport);
            TerminalUiBuilder.AttachTooltip(_o2Bar.FillImage.transform.parent.parent, "gloss.o2.term", "gloss.o2.def");

            _waterBar = TerminalUiBuilder.CreateStatusBar("WaterBar", content, Localization.Get("mc.status.water"), Theme, Scaler, 0f, 0.54f, 1f, 0.71f, TerminalIconId.Water, Theme.AccentLifeSupport);
            TerminalUiBuilder.AttachTooltip(_waterBar.FillImage.transform.parent.parent, "gloss.water.term", "gloss.water.def");

            _scrubberBar = TerminalUiBuilder.CreateStatusBar("ScrubberBar", content, Localization.Get("ui.scrubber"), Theme, Scaler, 0f, 0.36f, 1f, 0.53f, TerminalIconId.Oxygen, Theme.AccentLifeSupport);
            TerminalUiBuilder.AttachTooltip(_scrubberBar.FillImage.transform.parent.parent, "gloss.o2.term", "gloss.o2.def");

            _recyclerBar = TerminalUiBuilder.CreateStatusBar("RecyclerBar", content, Localization.Get("ui.recycler"), Theme, Scaler, 0f, 0.18f, 1f, 0.35f, TerminalIconId.Water, Theme.AccentLifeSupport);
            TerminalUiBuilder.AttachTooltip(_recyclerBar.FillImage.transform.parent.parent, "gloss.water.term", "gloss.water.def");

            _statusGaugeIcon = TerminalUiBuilder.CreateIcon("StatusGaugeIcon", content, TerminalIconId.Gauge, Theme.AccentLifeSupport);
            TerminalUiBuilder.SetAnchors(_statusGaugeIcon.rectTransform, 0f, 0.088f, 0.055f, 0.172f);

            _statusLineText = TerminalUiBuilder.CreateText("StatusLine", content, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_statusLineText.rectTransform, 0.065f, 0.09f, 0.56f, 0.17f);

            _repairBtn = TerminalUiBuilder.CreateButton("RepairBtn", content, Localization.Get("act.repair"), Theme, Theme.ButtonAccentBg, Theme.AccentLifeSupport, Theme.TextPrimary, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.RepairSystem);
                }, Scaler);
            _repairLabel = _repairBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_repairBtn.GetComponent<RectTransform>(), 0.58f, 0.085f, 1f, 0.17f);

            _detailsFoldout = TerminalUiBuilder.CreateMoreDetailsFoldout(
                "LifeDetails", content, Theme, Scaler,
                0f, 0.01f, 0.32f, 0.08f,
                0.34f, 0.01f, 1f, 0.08f);
            _detailsText = TerminalUiBuilder.CreateText("DetailsTxt", _detailsFoldout.BodyRect, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailsText.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);
        }

        public override void Refresh(SessionSnapshot s)
        {
            TerminalUiBuilder.UpdateStatusBar(_o2Bar, s.Oxygen, $"{s.Oxygen:0}% ({s.NetOxygenDelta:+0.0;-0.0;0}{Localization.Get("mc.per_sol")})", Theme, 25f, 5f);
            TerminalUiBuilder.UpdateStatusBar(_waterBar, s.Water, $"{s.Water:0}% ({s.NetWaterDelta:+0.0;-0.0;0}{Localization.Get("mc.per_sol")})", Theme, 25f, 5f);
            TerminalUiBuilder.UpdateStatusBar(_scrubberBar, s.ScrubberHealthPct, $"{s.ScrubberHealthPct:0}%", Theme, 65f, 45f);
            TerminalUiBuilder.UpdateStatusBar(_recyclerBar, s.RecyclerHealthPct, $"{s.RecyclerHealthPct:0}%", Theme, 65f, 40f);

            if (s.Oxygen <= 0f || s.Water <= 0f)
            {
                _statusLineText.color = Theme.StatusCrit;
                _statusLineText.text = s.Oxygen <= 0f ? Localization.Get("alert.o2_zero") : Localization.Get("alert.water_zero");
            }
            else if (s.ScrubberHealthPct < 45f)
            {
                _statusLineText.color = Theme.StatusCrit;
                _statusLineText.text = Localization.Get("alert.scrubber");
            }
            else if (s.NetOxygenDelta < 0f || s.NetWaterDelta < 0f)
            {
                _statusLineText.color = Theme.StatusWarn;
                _statusLineText.text = Localization.Get("alert.o2_low");
            }
            else
            {
                _statusLineText.color = Theme.StatusOk;
                _statusLineText.text = Localization.Get("status.word.good");
            }
            if (_statusGaugeIcon != null)
                _statusGaugeIcon.color = _statusLineText.color;

            _detailsText.text = $"O2 {s.NetOxygenDelta:+0.0;-0.0;0}{Localization.Get("mc.per_sol")} | H2O {s.NetWaterDelta:+0.0;-0.0;0}{Localization.Get("mc.per_sol")} | Crew {s.LivingCrewCount}/4";

            bool editable = !IsReadOnly && s.Status == MissionStatus.InProgress;
            _repairBtn.interactable = editable && s.ActionPoints > 0;
            _repairLabel.text = s.ActionPoints > 0 ? $"{Localization.Get("act.repair")} (1 AP)" : $"{Localization.Get("act.repair")} (0 AP)";
        }
    }
}
