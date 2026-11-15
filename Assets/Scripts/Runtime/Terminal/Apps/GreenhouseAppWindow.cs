using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class GreenhouseAppWindow : WindowView
    {
        private TextMeshProUGUI _introText;
        private StatusBarWidget _foodBar;
        private StatusBarWidget _cropBar;
        private TextMeshProUGUI _statusText;
        private Button _rationBtn;
        private TextMeshProUGUI _rationLabel;
        private Button _repairBtn;
        private TextMeshProUGUI _repairLabel;
        private FoldoutWidget _detailsFoldout;
        private TextMeshProUGUI _detailsText;

        protected override void BuildContent(RectTransform content)
        {
            _introText = TerminalUiBuilder.CreateText("Intro", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            _introText.text = Localization.Get("gh.friendly_intro");
            TerminalUiBuilder.SetAnchors(_introText.rectTransform, 0f, 0.89f, 1f, 1f);

            _foodBar = TerminalUiBuilder.CreateStatusBar("FoodBar", content, Localization.Get("mc.status.food"), Theme, Scaler, 0f, 0.67f, 1f, 0.87f, TerminalIconId.Food, Theme.AccentGreenhouse);
            TerminalUiBuilder.AttachTooltip(_foodBar.FillImage.transform.parent.parent, "gloss.food.term", "gloss.food.def");

            _cropBar = TerminalUiBuilder.CreateStatusBar("CropBar", content, Localization.Get("ui.crops"), Theme, Scaler, 0f, 0.45f, 1f, 0.65f, TerminalIconId.Food, Theme.AccentGreenhouse);
            TerminalUiBuilder.AttachTooltip(_cropBar.FillImage.transform.parent.parent, "gloss.food.term", "gloss.food.def");

            _statusText = TerminalUiBuilder.CreateText("Status", content, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_statusText.rectTransform, 0f, 0.30f, 1f, 0.43f);

            _rationBtn = TerminalUiBuilder.CreateButton("RationBtn", content, Localization.Get("act.ration"), Theme, Theme.ButtonBg, Theme.AccentGreenhouse, Theme.TextAmber, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.RationSupplies);
                }, Scaler);
            _rationLabel = _rationBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_rationBtn.GetComponent<RectTransform>(), 0f, 0.14f, 0.54f, 0.27f);

            _repairBtn = TerminalUiBuilder.CreateButton("RepairCropBtn", content, Localization.Get("act.repair"), Theme, Theme.ButtonAccentBg, Theme.AccentGreenhouse, Theme.TextPrimary, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.RepairSystem);
                }, Scaler);
            _repairLabel = _repairBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_repairBtn.GetComponent<RectTransform>(), 0.56f, 0.14f, 1f, 0.27f);

            _detailsFoldout = TerminalUiBuilder.CreateMoreDetailsFoldout(
                "GreenDetails", content, Theme, Scaler,
                0f, 0.01f, 0.34f, 0.10f,
                0.36f, 0.01f, 1f, 0.10f);
            _detailsText = TerminalUiBuilder.CreateText("DetailsTxt", _detailsFoldout.BodyRect, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailsText.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);
        }

        public override void Refresh(SessionSnapshot s)
        {
            TerminalUiBuilder.UpdateStatusBar(_foodBar, s.Food, $"{s.Food:0}% ({s.NetFoodDelta:+0.0;-0.0;0}{Localization.Get("mc.per_sol")})", Theme, 25f, 5f);
            TerminalUiBuilder.UpdateStatusBar(_cropBar, s.CropHealthPct, $"{s.CropHealthPct:0}%", Theme, 65f, 35f);

            if (s.Food <= 0f)
            {
                _statusText.color = Theme.StatusCrit;
                _statusText.text = Localization.Get("alert.food_zero");
            }
            else if (s.CropHealthPct < 60f)
            {
                _statusText.color = Theme.StatusWarn;
                _statusText.text = $"{Localization.Get("status.word.low")} — {Localization.Get("ui.crops")} {s.CropHealthPct:0}%";
            }
            else
            {
                _statusText.color = Theme.StatusOk;
                _statusText.text = $"{Localization.Get("status.word.good")} — {Localization.Format("pwr.conseq.gh", s.GreenhouseAllocPct, s.NetFoodDelta)}";
            }

            _detailsText.text = Localization.ShapeDisplay($"{Localization.GetRaw("pwr.gh.name")}: {s.GreenhouseAllocPct}% | {Localization.GetRaw("act.ration")}: {(s.RationingActive ? $"{Localization.GetRaw("pause.on")} (-25%)" : Localization.GetRaw("pause.off"))}");

            bool editable = !IsReadOnly && s.Status == MissionStatus.InProgress;
            _rationBtn.interactable = editable && !s.RationingActive && s.ActionPoints > 0;
            _rationLabel.text = s.RationingActive
                ? Localization.ShapeDisplay($"{Localization.GetRaw("act.ration")}: {Localization.GetRaw("pause.on")}")
                : Localization.ShapeDisplay(s.ActionPoints > 0 ? $"{Localization.GetRaw("act.ration")} (1 AP)" : $"{Localization.GetRaw("act.ration")} (0 AP)");

            _repairBtn.interactable = editable && s.ActionPoints > 0;
            _repairLabel.text = s.ActionPoints > 0 ? $"{Localization.Get("act.repair")} (1 AP)" : $"{Localization.Get("act.repair")} (0 AP)";
        }
    }
}
