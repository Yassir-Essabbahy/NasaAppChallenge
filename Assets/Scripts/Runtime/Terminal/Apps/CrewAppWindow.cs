using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class CrewAppWindow : WindowView
    {
        private TextMeshProUGUI _introText;
        private TextMeshProUGUI _apText;
        private readonly RectTransform[] _rowPanels = new RectTransform[4];
        private readonly Image[] _moodIcons = new Image[4];
        private readonly TextMeshProUGUI[] _rowTexts = new TextMeshProUGUI[4];
        private TextMeshProUGUI _hintText;
        private Button _treatBtn;
        private Button _restBtn;
        private TextMeshProUGUI _treatLabel;
        private TextMeshProUGUI _restLabel;
        private FoldoutWidget _detailsFoldout;
        private TextMeshProUGUI _detailsText;
        private int _selectedCrewId = 2; // Default to fallen commander in medbay

        protected override void BuildContent(RectTransform content)
        {
            _introText = TerminalUiBuilder.CreateText("Intro", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            _introText.text = Localization.Get("crew.friendly_intro");
            TerminalUiBuilder.SetAnchors(_introText.rectTransform, 0f, 0.91f, 0.64f, 1f);

            _apText = TerminalUiBuilder.CreateText("ApText", content, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineRight, Scaler);
            _apText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_apText.rectTransform, 0.64f, 0.91f, 1f, 1f);
            TerminalUiBuilder.AttachTooltip(_apText, "gloss.ap.term", "gloss.ap.def");

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                float top = 0.89f - i * 0.135f;
                float bot = top - 0.122f;
                var panel = TerminalUiBuilder.CreateFlatPanel($"CrewRow_{i}", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
                TerminalUiBuilder.SetAnchors(panel, 0f, bot, 1f, top);
                var inner = TerminalUiBuilder.GetInner(panel);

                var btn = panel.gameObject.AddComponent<Button>();
                btn.targetGraphic = inner.GetComponent<Image>();
                btn.onClick.AddListener(() => SelectCrew(idx));
                panel.gameObject.AddComponent<ButtonHoverFeedback>();

                var icon = TerminalUiBuilder.CreateIcon("MoodIcon", inner, TerminalIconId.MoodGood, Theme.StatusOk);
                TerminalUiBuilder.SetAnchors(icon.rectTransform, 0.01f, 0.16f, 0.07f, 0.84f);
                _moodIcons[i] = icon;

                var txt = TerminalUiBuilder.CreateText("RowText", inner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                TerminalUiBuilder.SetAnchors(txt.rectTransform, 0.08f, 0f, 1f, 1f, 4f, 0f, 10f, 0f);

                _rowPanels[i] = panel;
                _rowTexts[i] = txt;
            }

            _hintText = TerminalUiBuilder.CreateText("RoleHint", content, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_hintText.rectTransform, 0f, 0.25f, 1f, 0.34f);

            _treatBtn = TerminalUiBuilder.CreateButton("TreatBtn", content, Localization.Get("act.treat"), Theme, Theme.ButtonAccentBg, Theme.AccentCrew, Theme.TextPrimary, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.TreatCrew, _selectedCrewId);
                }, Scaler);
            _treatLabel = _treatBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_treatBtn.GetComponent<RectTransform>(), 0f, 0.11f, 0.49f, 0.23f);

            _restBtn = TerminalUiBuilder.CreateButton("RestBtn", content, Localization.Get("act.rest"), Theme, Theme.ButtonBg, Theme.AccentCrew, Theme.TextAmber, Theme.BodyFontSize,
                () =>
                {
                    if (!IsReadOnly)
                        Session.DoAction(CrewActionType.RestMeal);
                }, Scaler);
            _restLabel = _restBtn.GetComponentInChildren<TextMeshProUGUI>();
            TerminalUiBuilder.SetAnchors(_restBtn.GetComponent<RectTransform>(), 0.51f, 0.11f, 1f, 0.23f);

            _detailsFoldout = TerminalUiBuilder.CreateMoreDetailsFoldout(
                "CrewDetails", content, Theme, Scaler,
                0f, 0.01f, 0.34f, 0.095f,
                0.36f, 0.01f, 1f, 0.095f);
            _detailsText = TerminalUiBuilder.CreateText("DetailsTxt", _detailsFoldout.BodyRect, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailsText.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);
        }

        private void SelectCrew(int idx)
        {
            _selectedCrewId = idx;
            if (Session != null && Session.CurrentSnapshot != null)
                Refresh(Session.CurrentSnapshot);
        }

        public override void Refresh(SessionSnapshot s)
        {
            _apText.text = Localization.Format("ui.ap_count", s.ActionPoints, s.MaxActionPoints);
            _apText.color = s.ActionPoints > 0 ? Theme.TextAmber : Theme.StatusWarn;

            CrewSnapshot selected = null;
            for (int i = 0; i < 4 && i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                bool isSelected = c.Id == _selectedCrewId;
                if (isSelected) selected = c;

                TerminalUiBuilder.SetBorderColor(_rowPanels[i], isSelected ? Theme.AccentCrew : Theme.BorderUnfocused);

                Color col = !c.IsAlive ? Theme.StatusCrit : (c.Health < 50f || c.Morale < 30f ? Theme.StatusWarn : Theme.StatusOk);
                string tagWord = !c.IsAlive ? Localization.Get("ui.deceased")
                    : (c.Health < 35f || c.Morale < 25f) ? Localization.Get("status.word.danger")
                    : (c.Health < 65f || c.Morale < 50f) ? Localization.Get("status.word.low")
                    : Localization.Get("status.word.good");

                _moodIcons[i].sprite = TerminalUiArt.GetIcon(!c.IsAlive || c.Health < 35f ? TerminalIconId.Hazard : (c.Health < 65f || c.Morale < 50f ? TerminalIconId.Warning : TerminalIconId.PlusBox));
                _moodIcons[i].color = col;

                _rowTexts[i].color = !c.IsAlive ? Theme.StatusCrit : Theme.TextPrimary;
                string rawTag = !c.IsAlive ? Localization.GetRaw("ui.deceased")
                    : (c.Health < 35f || c.Morale < 25f) ? Localization.GetRaw("status.word.danger")
                    : (c.Health < 65f || c.Morale < 50f) ? Localization.GetRaw("status.word.low")
                    : Localization.GetRaw("status.word.good");
                _rowTexts[i].text = Localization.Format("crew.row_fmt", c.Name, c.Role, c.Health, c.Morale, rawTag);
            }

            if (selected != null)
            {
                string medNote = selected.IsFallenCommander ? Localization.GetRaw("crew.medbay_tag") : "";
                _hintText.text = Localization.ShapeDisplay($"{selected.Name}{medNote}: {selected.RoleHint}");
                _detailsText.text = Localization.ShapeDisplay($"{Localization.GetRaw("ui.dose")}: {selected.DoseMsv:0.0} mSv | {Localization.GetRaw("act.treat.desc")}");
            }

            bool editable = !IsReadOnly && s.Status == MissionStatus.InProgress;
            bool hasAp = editable && s.ActionPoints > 0;
            bool canTreatSelected = hasAp && selected != null && selected.IsAlive;
            _treatBtn.interactable = canTreatSelected;
            _treatLabel.text = s.ActionPoints <= 0 ? $"{Localization.Get("act.treat")} (0 AP)" : (selected != null ? $"{Localization.Get("act.treat")} (1 AP)" : Localization.Get("act.treat"));

            _restBtn.interactable = hasAp;
            _restLabel.text = s.ActionPoints > 0 ? $"{Localization.Get("act.rest")} (1 AP)" : $"{Localization.Get("act.rest")} (0 AP)";
        }
    }
}
