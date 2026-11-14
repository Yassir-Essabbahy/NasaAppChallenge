using System.Text;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class RadiationAppWindow : WindowView
    {
        private TextMeshProUGUI _introText;
        private StatusBarWidget _shieldBar;
        private TextMeshProUGUI _flareBannerText;
        private TextMeshProUGUI _crewDosesText;
        private Button _reinforceBtn;
        private TextMeshProUGUI _reinforceLabel;
        private FoldoutWidget _detailsFoldout;
        private TextMeshProUGUI _detailsText;
        private readonly StringBuilder _sb = new StringBuilder();

        protected override void BuildContent(RectTransform content)
        {
            _introText = TerminalUiBuilder.CreateText("Intro", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            _introText.text = Localization.Get("rad.friendly_intro");
            TerminalUiBuilder.SetAnchors(_introText.rectTransform, 0f, 0.90f, 1f, 1f);

            _shieldBar = TerminalUiBuilder.CreateStatusBar("ShieldBar", content, Localization.Get("mc.status.shield"), Theme, Scaler, 0f, 0.72f, 1f, 0.89f, TerminalIconId.Shield, Theme.AccentRadiation);
            TerminalUiBuilder.AttachTooltip(_shieldBar.FillImage.transform.parent.parent, "gloss.shield.term", "gloss.shield.def");

            _flareBannerText = TerminalUiBuilder.CreateText("FlareBanner", content, Theme, Theme.BodyFontSize, Theme.StatusOk, TextAlignmentOptions.MidlineLeft, Scaler);
            _flareBannerText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_flareBannerText.rectTransform, 0f, 0.61f, 1f, 0.71f);
            TerminalUiBuilder.AttachTooltip(_flareBannerText, "gloss.rad.term", "gloss.rad.def");

            _crewDosesText = TerminalUiBuilder.CreateText("CrewDoses", content, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_crewDosesText.rectTransform, 0f, 0.25f, 1f, 0.60f);

            _reinforceBtn = TerminalUiBuilder.CreateButton("ReinforceBtn", content, Localization.Get("act.reinforce"), Theme, Theme.ButtonAccentBg, Theme.AccentRadiation, Theme.TextPrimary, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.ReinforceShielding);
                }, Scaler);
            _reinforceLabel = _reinforceBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_reinforceBtn.GetComponent<RectTransform>(), 0f, 0.11f, 0.82f, 0.23f);

            _detailsFoldout = TerminalUiBuilder.CreateMoreDetailsFoldout(
                "RadDetails", content, Theme, Scaler,
                0f, 0.01f, 0.34f, 0.095f,
                0.36f, 0.01f, 1f, 0.095f);
            _detailsText = TerminalUiBuilder.CreateText("DetailsTxt", _detailsFoldout.BodyRect, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailsText.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);
        }

        public override void Refresh(SessionSnapshot s)
        {
            TerminalUiBuilder.UpdateStatusBar(_shieldBar, s.Shielding, $"{s.Shielding:0}%", Theme, 45f, 20f);

            if (s.SolarFlareActive)
            {
                _flareBannerText.color = Theme.StatusCrit;
                _flareBannerText.text = $"{Localization.Get("status.word.danger")} — {Localization.Get("alert.flare")}";
            }
            else
            {
                _flareBannerText.color = Theme.StatusOk;
                _flareBannerText.text = $"{Localization.Get("status.word.good")} — +{s.ProjectedDoseMsv:0.00} mSv{Localization.Get("mc.per_sol")}";
            }

            _sb.Clear();
            for (int i = 0; i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                string tag = !c.IsAlive ? Localization.Get("ui.deceased")
                    : c.DoseMsv > s.DoseHealthThresholdMsv ? Localization.Get("status.word.danger")
                    : (c.DoseMsv > s.DoseHealthThresholdMsv * 0.7f ? Localization.Get("status.word.low") : Localization.Get("status.word.good"));
                _sb.AppendLine($"{c.Name}: {c.DoseMsv:0.0} / {s.DoseHealthThresholdMsv:0.0} mSv  [{tag}]");
            }
            _crewDosesText.text = _sb.ToString();

            _detailsText.text = $"+{s.ProjectedDoseMsv:0.00} mSv{Localization.Get("mc.per_sol")} | Safe Limit: {s.DoseHealthThresholdMsv:0.0} mSv";

            bool editable = !IsReadOnly && s.Status == MissionStatus.InProgress;
            bool canReinforce = editable && s.ActionPoints > 0 && s.Power >= 8f;
            _reinforceBtn.interactable = canReinforce;
            _reinforceLabel.text = s.ActionPoints <= 0
                ? $"{Localization.Get("act.reinforce")} (0 AP)"
                : (s.Power < 8f ? $"{Localization.Get("act.reinforce")} (-8 PWR)" : $"{Localization.Get("act.reinforce")} (1 AP, -8 PWR)");
        }
    }
}
