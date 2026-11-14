using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class CommsAppWindow : WindowView
    {
        private const int MaxVisiblePackets = 7;

        private TextMeshProUGUI _statusBanner;
        private Image _scanIcon;
        private readonly RectTransform[] _msgPanels = new RectTransform[MaxVisiblePackets];
        private readonly TextMeshProUGUI[] _msgLabels = new TextMeshProUGUI[MaxVisiblePackets];
        private TextMeshProUGUI _bodyTitle;
        private TextMeshProUGUI _bodyText;
        private string _selectedId;

        protected override void BuildContent(RectTransform content)
        {
            var intro = TerminalUiBuilder.CreateText("Intro", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            intro.text = Localization.Get("comms.friendly_intro");
            TerminalUiBuilder.SetAnchors(intro.rectTransform, 0f, 0.91f, 0.56f, 1f);
            TerminalUiBuilder.AttachTooltip(intro, "gloss.dsn.term", "gloss.dsn.def");

            _scanIcon = TerminalUiBuilder.CreateIcon("ScanIcon", content, TerminalIconId.Scan, Theme.StatusOk);
            TerminalUiBuilder.SetAnchors(_scanIcon.rectTransform, 0.50f, 0.915f, 0.555f, 0.995f);

            _statusBanner = TerminalUiBuilder.CreateText("CommsStatus", content, Theme, Theme.BodyFontSize, Theme.StatusOk, TextAlignmentOptions.MidlineRight, Scaler);
            _statusBanner.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_statusBanner.rectTransform, 0.565f, 0.91f, 1f, 1f);

            for (int i = 0; i < MaxVisiblePackets; i++)
            {
                int slot = i;
                float top = 0.88f - i * 0.125f;
                float bot = top - 0.114f;
                var panel = TerminalUiBuilder.CreateFlatPanel($"Packet_{i}", content, Theme.PanelBg, Theme.BorderUnfocused, 2f);
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

                _msgPanels[i] = panel;
                _msgLabels[i] = txt;
            }

            var detailPanel = TerminalUiBuilder.CreateFlatPanel("DetailBox", content, Theme.PanelBg, Theme.AccentComms, 2f);
            TerminalUiBuilder.SetAnchors(detailPanel, 0.46f, 0.01f, 1f, 0.88f);
            var detailInner = TerminalUiBuilder.GetInner(detailPanel);

            _bodyTitle = TerminalUiBuilder.CreateText("BodyTitle", detailInner, Theme, Theme.BodyFontSize, Theme.TextAmber, TextAlignmentOptions.TopLeft, Scaler);
            _bodyTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_bodyTitle.rectTransform, 0f, 0.72f, 1f, 1f, 14f, 4f, 14f, 12f);

            _bodyText = TerminalUiBuilder.CreateText("BodyText", detailInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_bodyText.rectTransform, 0f, 0.03f, 1f, 0.70f, 14f, 8f, 14f, 4f);
        }

        public override void OnOpen()
        {
            if (Session?.CurrentSnapshot?.CommsInbox != null && Session.CurrentSnapshot.CommsInbox.Count > 0)
            {
                var newest = Session.CurrentSnapshot.CommsInbox[0];
                _selectedId = newest.Id;
                Session.MarkCommsRead(newest.Id);
            }
        }

        private void SelectSlot(int slot)
        {
            var inbox = Session?.CurrentSnapshot?.CommsInbox;
            if (inbox == null || slot < 0 || slot >= inbox.Count) return;
            _selectedId = inbox[slot].Id;
            Session.MarkCommsRead(_selectedId);
        }

        public override void Refresh(SessionSnapshot s)
        {
            if (s.CommsOnline)
            {
                _statusBanner.color = Theme.StatusOk;
                _statusBanner.text = $"{Localization.Get("status.word.good")} ({s.CommsAllocPct}%)";
            }
            else
            {
                _statusBanner.color = Theme.StatusCrit;
                _statusBanner.text = $"{Localization.Get("status.word.danger")} (0%)";
            }
            if (_scanIcon != null)
                _scanIcon.color = _statusBanner.color;

            CommsMessageSnapshot active = null;
            for (int i = 0; i < MaxVisiblePackets; i++)
            {
                if (i < s.CommsInbox.Count)
                {
                    var m = s.CommsInbox[i];
                    _msgPanels[i].gameObject.SetActive(true);
                    if (string.IsNullOrEmpty(_selectedId) && i == 0) _selectedId = m.Id;
                    bool isSel = m.Id == _selectedId;
                    if (isSel) active = m;

                    TerminalUiBuilder.SetBorderColor(_msgPanels[i], isSel ? Theme.AccentComms : Theme.BorderUnfocused);
                    _msgLabels[i].color = m.IsRead ? Theme.TextMuted : Theme.TextAmber;
                    string tag = m.IsRead ? "" : Localization.Get("ui.new_badge");
                    _msgLabels[i].text = $"{tag}Sol {m.Sol} ({m.DelayText})";
                }
                else
                {
                    _msgPanels[i].gameObject.SetActive(false);
                }
            }

            if (active != null)
            {
                _bodyTitle.text = $"{active.Subject}\n({active.DelayText})";
                _bodyText.text = active.Body;
            }
            else
            {
                _bodyTitle.text = Localization.Get("pwr.conseq.cm_off");
                _bodyText.text = Localization.Get("gloss.dsn.def");
            }
        }
    }
}
