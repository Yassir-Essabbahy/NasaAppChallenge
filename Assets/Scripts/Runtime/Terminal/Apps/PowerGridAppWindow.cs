using System;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class PowerGridAppWindow : WindowView
    {
        public static event Action OnPlayerAdjustedPower;

        private StatusBarWidget _batteryBar;
        private TextMeshProUGUI _projectedText;

        private Button _safePresetBtn;
        private Button _balancedPresetBtn;
        private Button _savePresetBtn;

        private Slider _lsSlider, _ghSlider, _htSlider, _cmSlider;
        private TextMeshProUGUI _lsValText, _ghValText, _htValText, _cmValText;
        private TextMeshProUGUI _lsConseqText, _ghConseqText, _htConseqText, _cmConseqText;

        private FoldoutWidget _detailsFoldout;
        private TextMeshProUGUI _detailsText;

        private Button _cleanPanelsBtn;
        private TextMeshProUGUI _cleanBtnLabel;
        private bool _updating;

        protected override void BuildContent(RectTransform content)
        {
            _batteryBar = TerminalUiBuilder.CreateStatusBar(
                "BatteryBar", content, Localization.Get("mc.status.power"), Theme, Scaler,
                0f, 0.85f, 1f, 1f, TerminalIconId.Battery, Theme.AccentPower);
            TerminalUiBuilder.AttachTooltip(_batteryBar.FillImage.transform.parent.parent, "gloss.power.term", "gloss.power.def");

            _projectedText = TerminalUiBuilder.CreateText("Projected", content, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_projectedText.rectTransform, 0f, 0.77f, 1f, 0.85f);

            // Preset bar
            var presetLabel = TerminalUiBuilder.CreateText("PresetLbl", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            presetLabel.text = Localization.Get("pwr.preset.label");
            TerminalUiBuilder.SetAnchors(presetLabel.rectTransform, 0f, 0.69f, 0.25f, 0.76f);

            _safePresetBtn = TerminalUiBuilder.CreateButton("PresetSafe", content, Localization.Get("pwr.preset.safe"), Theme, Theme.ButtonBg, Theme.AccentPower, Theme.TextPrimary, Theme.BodyFontSize,
                () => ApplyPreset(90, 70, 60, 50), Scaler);
            TerminalUiBuilder.SetAnchors(_safePresetBtn.GetComponent<RectTransform>(), 0.26f, 0.69f, 0.49f, 0.76f);

            _balancedPresetBtn = TerminalUiBuilder.CreateButton("PresetBalanced", content, Localization.Get("pwr.preset.balanced"), Theme, Theme.ButtonAccentBg, Theme.AccentPower, Theme.TextPrimary, Theme.BodyFontSize,
                () => ApplyPreset(70, 60, 50, 30), Scaler);
            TerminalUiBuilder.SetAnchors(_balancedPresetBtn.GetComponent<RectTransform>(), 0.51f, 0.69f, 0.74f, 0.76f);

            _savePresetBtn = TerminalUiBuilder.CreateButton("PresetSave", content, Localization.Get("pwr.preset.save"), Theme, Theme.ButtonBg, Theme.BorderUnfocused, Theme.TextAmber, Theme.BodyFontSize,
                () => ApplyPreset(50, 30, 50, 0), Scaler);
            TerminalUiBuilder.SetAnchors(_savePresetBtn.GetComponent<RectTransform>(), 0.76f, 0.69f, 1f, 0.76f);

            // 4 allocation sliders with live plain-English consequences underneath
            (_lsSlider, _lsValText, _lsConseqText) = CreateFriendlySliderRow(content, Localization.Get("pwr.ls.name"), PowerSystemType.LifeSupport, 0.56f, 0.68f, "gloss.o2.term", "gloss.o2.def");
            (_ghSlider, _ghValText, _ghConseqText) = CreateFriendlySliderRow(content, Localization.Get("pwr.gh.name"), PowerSystemType.Greenhouse, 0.43f, 0.55f, "gloss.food.term", "gloss.food.def");
            (_htSlider, _htValText, _htConseqText) = CreateFriendlySliderRow(content, Localization.Get("pwr.ht.name"), PowerSystemType.Heating, 0.30f, 0.42f, "gloss.power.term", "gloss.power.def");
            (_cmSlider, _cmValText, _cmConseqText) = CreateFriendlySliderRow(content, Localization.Get("pwr.cm.name"), PowerSystemType.Comms, 0.17f, 0.29f, "gloss.dsn.term", "gloss.dsn.def");

            // More details foldout & technical math
            _detailsFoldout = TerminalUiBuilder.CreateMoreDetailsFoldout(
                "PowerDetails", content, Theme, Scaler,
                0f, 0.09f, 0.36f, 0.16f,
                0f, 0f, 1f, 0.085f);
            _detailsText = TerminalUiBuilder.CreateText("PowerDetailsText", _detailsFoldout.BodyRect, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailsText.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);

            _cleanPanelsBtn = TerminalUiBuilder.CreateButton("CleanPanelsBtn", content, Localization.Get("pwr.no_ap"), Theme, Theme.ButtonAccentBg, Theme.AccentPower, Theme.TextPrimary, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.CleanPanels);
                }, Scaler);
            _cleanBtnLabel = _cleanPanelsBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_cleanPanelsBtn.GetComponent<RectTransform>(), 0.38f, 0.09f, 1f, 0.16f);
        }

        private void ApplyPreset(int ls, int gh, int ht, int cm)
        {
            if (IsReadOnly || Session == null) return;
            Session.SetAllocation(PowerSystemType.LifeSupport, ls);
            Session.SetAllocation(PowerSystemType.Greenhouse, gh);
            Session.SetAllocation(PowerSystemType.Heating, ht);
            Session.SetAllocation(PowerSystemType.Comms, cm);
            OnPlayerAdjustedPower?.Invoke();
        }

        private (Slider slider, TextMeshProUGUI valText, TextMeshProUGUI conseqText) CreateFriendlySliderRow(
            RectTransform parent, string label, PowerSystemType sys, float yMin, float yMax, string tipTitleKey, string tipBodyKey)
        {
            var box = TerminalUiBuilder.CreateFlatPanel("Row_" + sys, parent, Theme.PanelBg, Theme.BorderUnfocused, 1f);
            TerminalUiBuilder.SetAnchors(box, 0f, yMin, 1f, yMax);
            var row = TerminalUiBuilder.GetInner(box);
            TerminalUiBuilder.AttachTooltip(box, tipTitleKey, tipBodyKey);

            var nameTmp = TerminalUiBuilder.CreateText("Name", row, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
            nameTmp.text = label;
            nameTmp.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(nameTmp.rectTransform, 0f, 0.48f, 0.42f, 0.96f, 10f);

            var sliderGo = TerminalUiBuilder.CreateRect("Slider", row);
            TerminalUiBuilder.SetAnchors(sliderGo, 0.43f, 0.54f, 0.85f, 0.92f);

            var trackPanel = TerminalUiBuilder.CreateFlatPanel("Track", sliderGo, Theme.BarTrackBg, Theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetStretch(trackPanel);
            var trackInner = TerminalUiBuilder.GetInner(trackPanel);

            var fillArea = TerminalUiBuilder.CreateRect("FillArea", trackInner);
            TerminalUiBuilder.SetStretch(fillArea);
            var fill = TerminalUiBuilder.CreateRect("Fill", fillArea);
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = TerminalUiArt.GetRoundedRect8px();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Theme.AccentPower;
            TerminalUiBuilder.SetStretch(fill);

            var handleArea = TerminalUiBuilder.CreateRect("HandleArea", sliderGo);
            TerminalUiBuilder.SetStretch(handleArea);
            var handle = TerminalUiBuilder.CreateRect("Handle", handleArea);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = TerminalUiArt.GetRoundedRect8px();
            handleImg.type = Image.Type.Sliced;
            handleImg.color = Theme.TextPrimary;
            handle.sizeDelta = new Vector2(20f, 0f);

            var slider = sliderGo.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.onValueChanged.AddListener(v =>
            {
                if (_updating || IsReadOnly) return;
                int snapped = Mathf.RoundToInt(v / 10f) * 10;
                if (Mathf.RoundToInt(v) != snapped)
                {
                    _updating = true;
                    slider.value = snapped;
                    _updating = false;
                }
                Session.SetAllocation(sys, snapped);
                OnPlayerAdjustedPower?.Invoke();
            });

            var valTmp = TerminalUiBuilder.CreateText("Val", row, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineRight, Scaler);
            valTmp.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(valTmp.rectTransform, 0.86f, 0.48f, 1f, 0.96f, 0f, 0f, 10f, 0f);

            var conseqTmp = TerminalUiBuilder.CreateText("Conseq", row, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(conseqTmp.rectTransform, 0f, 0.02f, 1f, 0.48f, 10f, 0f, 10f, 0f);

            return (slider, valTmp, conseqTmp);
        }

        public override void Refresh(SessionSnapshot s)
        {
            TerminalUiBuilder.UpdateStatusBar(_batteryBar, s.Power, $"{s.Power:0}%", Theme, 25f, 10f);

            if (s.WillBlackout || s.ProjectedBatteryAfter <= 0f)
            {
                _projectedText.color = Theme.StatusCrit;
                _projectedText.text = Localization.Get("alert.blackout");
            }
            else
            {
                _projectedText.color = s.NetPowerDelta < 0f ? Theme.StatusWarn : Theme.StatusOk;
                _projectedText.text = $"{Localization.Get("mc.forecast_prefix")}{Localization.Get("mc.status.power")} {s.ProjectedBatteryAfter:0}% ({s.NetPowerDelta:+0;-0;0}{Localization.Get("mc.per_sol")})";
            }

            _updating = true;
            _lsSlider.value = s.LifeSupportAllocPct;
            _ghSlider.value = s.GreenhouseAllocPct;
            _htSlider.value = s.HeatingAllocPct;
            _cmSlider.value = s.CommsAllocPct;
            _updating = false;

            bool editable = !IsReadOnly && s.Status == MissionStatus.InProgress;
            _lsSlider.interactable = editable;
            _ghSlider.interactable = editable;
            _htSlider.interactable = editable;
            _cmSlider.interactable = editable;
            _safePresetBtn.interactable = editable;
            _balancedPresetBtn.interactable = editable;
            _savePresetBtn.interactable = editable;

            _lsValText.text = $"{s.LifeSupportAllocPct}%";
            _ghValText.text = $"{s.GreenhouseAllocPct}%";
            _htValText.text = $"{s.HeatingAllocPct}%";
            _htValText.color = s.HeatingAllocPct < 50 ? Theme.StatusCrit : Theme.TextAmber;
            _cmValText.text = $"{s.CommsAllocPct}%";

            _lsConseqText.text = Localization.Format("pwr.conseq.ls", s.LifeSupportAllocPct, s.NetOxygenDelta, s.NetWaterDelta);
            _ghConseqText.text = Localization.Format("pwr.conseq.gh", s.GreenhouseAllocPct, s.NetFoodDelta);
            _htConseqText.text = s.HeatingAllocPct < 50
                ? Localization.Format("pwr.conseq.ht_cold", s.HeatingAllocPct)
                : Localization.Format("pwr.conseq.ht_ok", s.HeatingAllocPct);
            _htConseqText.color = s.HeatingAllocPct < 50 ? Theme.StatusCrit : Theme.TextMuted;
            _cmConseqText.text = s.CommsAllocPct > 0
                ? Localization.Format("pwr.conseq.cm_on", s.CommsAllocPct)
                : Localization.Get("pwr.conseq.cm_off");

            _detailsText.text = $"{Localization.Get("ui.solar_gen")}: {s.SolarGeneration:0.0}W ({s.GenerationReason}) | {Localization.Get("ui.demand")}: {s.TotalDemand:0.0}W";

            _cleanPanelsBtn.interactable = editable && s.ActionPoints > 0;
            _cleanBtnLabel.text = s.ActionPoints > 0
                ? Localization.Format("pwr.clean_btn", s.PanelDustPct)
                : Localization.Get("pwr.no_ap");
        }
    }
}
