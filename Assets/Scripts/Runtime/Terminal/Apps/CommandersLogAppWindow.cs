using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class CommandersLogAppWindow : WindowView
    {
        private const int MaxEntries = 10;

        private TextMeshProUGUI _headerText;
        private readonly RectTransform[] _entryPanels = new RectTransform[MaxEntries];
        private readonly TextMeshProUGUI[] _entryLabels = new TextMeshProUGUI[MaxEntries];
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private string _selectedId;

        protected override void BuildContent(RectTransform content)
        {
            _headerText = TerminalUiBuilder.CreateText("LogHeader", content, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineLeft, Scaler);
            _headerText.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_headerText.rectTransform, 0f, 0.90f, 1f, 1f);

            for (int i = 0; i < MaxEntries; i++)
            {
                int slot = i;
                float top = 0.88f - i * 0.087f;
                float bot = top - 0.080f;
                var panel = TerminalUiBuilder.CreateFlatPanel($"LogRow_{i}", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
                TerminalUiBuilder.SetAnchors(panel, 0f, bot, 0.44f, top);
                var inner = TerminalUiBuilder.GetInner(panel);

                var btn = panel.gameObject.AddComponent<Button>();
                btn.targetGraphic = inner.GetComponent<Image>();
                btn.onClick.AddListener(() => SelectSlot(slot));
                panel.gameObject.AddComponent<ButtonHoverFeedback>();

                var txt = TerminalUiBuilder.CreateText("Label", inner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                txt.overflowMode = TextOverflowModes.Ellipsis;
                TerminalUiBuilder.SetAnchors(txt.rectTransform, 0f, 0f, 1f, 1f, 10f, 2f, 10f, 2f);

                _entryPanels[i] = panel;
                _entryLabels[i] = txt;
            }

            var detailBox = TerminalUiBuilder.CreateFlatPanel("DetailBox", content, Theme.PanelBg, Theme.AccentLog, 2f);
            TerminalUiBuilder.SetAnchors(detailBox, 0.46f, 0.01f, 1f, 0.88f);
            var detailInner = TerminalUiBuilder.GetInner(detailBox);

            _detailTitle = TerminalUiBuilder.CreateText("DetailTitle", detailInner, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.TopLeft, Scaler);
            _detailTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_detailTitle.rectTransform, 0f, 0.76f, 1f, 1f, 14f, 4f, 14f, 12f);

            _detailBody = TerminalUiBuilder.CreateText("DetailBody", detailInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailBody.rectTransform, 0f, 0.03f, 1f, 0.74f, 14f, 8f, 14f, 4f);
        }

        public override void OnOpen()
        {
            var entries = Session?.CurrentSnapshot?.LogEntries;
            if (entries != null && entries.Count > 0)
            {
                LogEntrySnapshot target = entries[entries.Count - 1];
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].IsRead) { target = entries[i]; break; }
                }
                _selectedId = target.Id;
                Session.MarkLogRead(target.Id);
            }
        }

        private void SelectSlot(int slot)
        {
            var entries = Session?.CurrentSnapshot?.LogEntries;
            if (entries == null || slot < 0 || slot >= entries.Count) return;
            _selectedId = entries[slot].Id;
            Session.MarkLogRead(_selectedId);
        }

        public override void Refresh(SessionSnapshot s)
        {
            _headerText.text = Localization.Format("log.unread_summary", Localization.Get("log.friendly_intro"), s.UnreadLogCount);

            LogEntrySnapshot active = null;
            for (int i = 0; i < MaxEntries; i++)
            {
                if (i < s.LogEntries.Count)
                {
                    var e = s.LogEntries[i];
                    _entryPanels[i].gameObject.SetActive(true);
                    if (string.IsNullOrEmpty(_selectedId) && i == 0) _selectedId = e.Id;
                    bool isSel = e.Id == _selectedId;
                    if (isSel) active = e;

                    TerminalUiBuilder.SetBorderColor(_entryPanels[i], isSel ? Theme.AccentLog : Theme.BorderUnfocused);
                    _entryLabels[i].color = e.IsRead ? Theme.TextMuted : Theme.TextAmber;
                    _entryLabels[i].text = (e.IsRead ? "" : Localization.Get("ui.new_badge")) + e.Title;
                }
                else
                {
                    _entryPanels[i].gameObject.SetActive(false);
                }
            }

            if (active != null)
            {
                _detailTitle.text = active.Title;
                _detailBody.text = active.Body;
            }
        }
    }
}
