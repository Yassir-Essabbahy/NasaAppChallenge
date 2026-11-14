using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class SourcesAppWindow : WindowView
    {
        private SourcesDatabase _database;
        private const int MaxRows = 12;
        private readonly RectTransform[] _rowPanels = new RectTransform[MaxRows];
        private readonly TextMeshProUGUI[] _rowLabels = new TextMeshProUGUI[MaxRows];
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private int _selectedIdx;

        public void SetDatabase(SourcesDatabase db)
        {
            _database = db;
        }

        protected override void BuildContent(RectTransform content)
        {
            var header = TerminalUiBuilder.CreateText("SourcesHdr", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            header.text = Localization.Get("sources.friendly_intro");
            TerminalUiBuilder.SetAnchors(header.rectTransform, 0f, 0.90f, 1f, 1f);

            for (int i = 0; i < MaxRows; i++)
            {
                int slot = i;
                float top = 0.885f - i * 0.072f;
                float bot = top - 0.065f;
                var panel = TerminalUiBuilder.CreateFlatPanel($"SrcRow_{i}", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
                TerminalUiBuilder.SetAnchors(panel, 0f, bot, 0.44f, top);
                var inner = TerminalUiBuilder.GetInner(panel);

                var btn = panel.gameObject.AddComponent<Button>();
                btn.targetGraphic = inner.GetComponent<Image>();
                btn.onClick.AddListener(() => SelectIndex(slot));
                panel.gameObject.AddComponent<ButtonHoverFeedback>();

                var txt = TerminalUiBuilder.CreateText("Label", inner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                txt.overflowMode = TextOverflowModes.Ellipsis;
                TerminalUiBuilder.SetAnchors(txt.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);

                _rowPanels[i] = panel;
                _rowLabels[i] = txt;
            }

            var detailBox = TerminalUiBuilder.CreateFlatPanel("SrcDetail", content, Theme.PanelBg, Theme.AccentSources, 2f);
            TerminalUiBuilder.SetAnchors(detailBox, 0.46f, 0.01f, 1f, 0.88f);
            var detailInner = TerminalUiBuilder.GetInner(detailBox);

            _detailTitle = TerminalUiBuilder.CreateText("DetailTitle", detailInner, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.TopLeft, Scaler);
            _detailTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_detailTitle.rectTransform, 0f, 0.70f, 1f, 1f, 14f, 4f, 14f, 12f);

            _detailBody = TerminalUiBuilder.CreateText("DetailBody", detailInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailBody.rectTransform, 0f, 0.03f, 1f, 0.68f, 14f, 8f, 14f, 4f);
        }

        private void SelectIndex(int idx)
        {
            _selectedIdx = idx;
            if (Session?.CurrentSnapshot != null) Refresh(Session.CurrentSnapshot);
        }

        public override void Refresh(SessionSnapshot s)
        {
            int count = _database != null && _database.Entries != null ? _database.Entries.Count : 0;
            for (int i = 0; i < MaxRows; i++)
            {
                if (i < count)
                {
                    var rec = _database.Entries[i];
                    _rowPanels[i].gameObject.SetActive(true);
                    bool sel = i == _selectedIdx;
                    TerminalUiBuilder.SetBorderColor(_rowPanels[i], sel ? Theme.AccentSources : Theme.BorderUnfocused);
                    _rowLabels[i].color = sel ? Theme.TextAmber : Theme.TextPrimary;
                    _rowLabels[i].text = $"{rec.Id} ({rec.Organization})";
                }
                else
                {
                    _rowPanels[i].gameObject.SetActive(false);
                }
            }

            if (count > 0 && _selectedIdx >= 0 && _selectedIdx < count)
            {
                var rec = _database.Entries[_selectedIdx];
                _detailTitle.text = $"{rec.Title}\n{rec.Organization}";
                _detailBody.text = Localization.Format("sources.detail_fmt", rec.Id, rec.UrlText, rec.UsedByConstantsAndEvents);
            }
        }
    }
}
