using System;
using System.Collections.Generic;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime.Terminal.Apps
{
    public class SourcesAppWindow : WindowView
    {
        private SourcesDatabase _database;

        private Button _tabSourcesBtn;
        private Button _tabRealDataBtn;
        private RectTransform _tabSourcesPanel;
        private RectTransform _tabRealDataPanel;

        private GameObject _sourcesPageGo;
        private GameObject _realDataPageGo;

        // Sources Tab
        private RectTransform _sourceListContent;
        private readonly List<(RectTransform panel, TextMeshProUGUI label, SourceRecord rec)> _sourceRows
            = new List<(RectTransform, TextMeshProUGUI, SourceRecord)>();
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailMeta;
        private TextMeshProUGUI _detailSupports;
        private TextMeshProUGUI _detailUrlText;
        private Button _openUrlBtn;
        private Button _openSecondaryUrlBtn;
        private TextMeshProUGUI _openSecondaryLabel;
        private int _selectedIdx;
        private string _selectedUrl = string.Empty;
        private string _selectedSecondaryUrl = string.Empty;
        private int _builtEntryCount = -1;

        // Real Data & Constants Tab
        private TextMeshProUGUI _realDataBodyText;
        private RectTransform _constantsContent;
        private string _constantsConfigSignature = string.Empty;

        private bool _showingRealDataTab;

        public void SetDatabase(SourcesDatabase db)
        {
            _database = db;
            _builtEntryCount = -1;
        }

        protected override void BuildContent(RectTransform content)
        {
            var header = TerminalUiBuilder.CreateText("SourcesHdr", content, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft, Scaler);
            header.text = Localization.Get("sources.friendly_intro");
            TerminalUiBuilder.SetAnchors(header.rectTransform, 0f, 0.91f, 0.50f, 1f);

            // Top-right tab switcher
            _tabSourcesBtn = TerminalUiBuilder.CreateButton("TabSourcesBtn", content, "", Theme, Theme.ButtonAccentBg, Theme.AccentSources, Theme.TextPrimary, Theme.BodyFontSize, () => SwitchTab(false), Scaler);
            TerminalUiBuilder.SetAnchors(_tabSourcesBtn.GetComponent<RectTransform>(), 0.51f, 0.915f, 0.72f, 0.99f);
            _tabSourcesPanel = _tabSourcesBtn.GetComponent<RectTransform>();

            _tabRealDataBtn = TerminalUiBuilder.CreateButton("TabRealDataBtn", content, Localization.Get("sources.tab.realdata"), Theme, Theme.ButtonBg, Theme.BorderUnfocused, Theme.TextPrimary, Theme.BodyFontSize, () => SwitchTab(true), Scaler);
            TerminalUiBuilder.SetAnchors(_tabRealDataBtn.GetComponent<RectTransform>(), 0.73f, 0.915f, 1f, 0.99f);
            _tabRealDataPanel = _tabRealDataBtn.GetComponent<RectTransform>();

            BuildSourcesTab(content);
            BuildRealDataTab(content);
            SwitchTab(false);
        }

        private void BuildSourcesTab(RectTransform parent)
        {
            var page = TerminalUiBuilder.CreateRect("SourcesPage", parent);
            TerminalUiBuilder.SetAnchors(page, 0f, 0f, 1f, 0.90f);
            _sourcesPageGo = page.gameObject;

            var listPanel = TerminalUiBuilder.CreateFlatPanel("SrcListPanel", page, Theme.PanelBg, Theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(listPanel, 0f, 0.01f, 0.45f, 0.99f);
            _sourceListContent = CreateScrollContent("SrcListScroll", TerminalUiBuilder.GetInner(listPanel), spacing: 6f, padding: 6);

            var detailBox = TerminalUiBuilder.CreateFlatPanel("SrcDetail", page, Theme.PanelBg, Theme.AccentSources, 2f);
            TerminalUiBuilder.SetAnchors(detailBox, 0.47f, 0.01f, 1f, 0.99f);
            var detailInner = TerminalUiBuilder.GetInner(detailBox);

            _detailTitle = TerminalUiBuilder.CreateText("DetailTitle", detailInner, Theme, Theme.HeaderFontSize, Theme.TextAmber, TextAlignmentOptions.TopLeft, Scaler);
            _detailTitle.fontStyle = FontStyles.Bold;
            TerminalUiBuilder.SetAnchors(_detailTitle.rectTransform, 0.03f, 0.76f, 0.97f, 0.97f);

            _detailMeta = TerminalUiBuilder.CreateText("DetailMeta", detailInner, Theme, Theme.BodyFontSize, Theme.TextMuted, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailMeta.rectTransform, 0.03f, 0.59f, 0.97f, 0.75f);

            _detailSupports = TerminalUiBuilder.CreateText("DetailSupports", detailInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailSupports.rectTransform, 0.03f, 0.31f, 0.97f, 0.58f);

            float smallFont = Mathf.Max(16f, Theme.BodyFontSize - 4f);
            _detailUrlText = TerminalUiBuilder.CreateText("DetailUrl", detailInner, Theme, smallFont, Theme.AccentHelp, TextAlignmentOptions.TopLeft, Scaler);
            TerminalUiBuilder.SetAnchors(_detailUrlText.rectTransform, 0.03f, 0.14f, 0.97f, 0.30f);

            _openUrlBtn = TerminalUiBuilder.CreateButton("OpenUrlBtn", detailInner, Localization.Get("sources.open_url"), Theme, Theme.ButtonAccentBg, Theme.AccentSources, Theme.TextPrimary, Theme.BodyFontSize,
                () => SourcesDatabase.OpenSourceUrl(_selectedUrl), Scaler);
            TerminalUiBuilder.SetAnchors(_openUrlBtn.GetComponent<RectTransform>(), 0.03f, 0.02f, 0.52f, 0.12f);

            _openSecondaryUrlBtn = TerminalUiBuilder.CreateButton("OpenSecondaryBtn", detailInner, Localization.Get("sources.open_ref"), Theme, Theme.ButtonBg, Theme.BorderUnfocused, Theme.TextPrimary, smallFont,
                () => SourcesDatabase.OpenSourceUrl(_selectedSecondaryUrl), Scaler);
            TerminalUiBuilder.SetAnchors(_openSecondaryUrlBtn.GetComponent<RectTransform>(), 0.54f, 0.02f, 0.97f, 0.12f);
            _openSecondaryLabel = _openSecondaryUrlBtn.GetComponentInChildren<TextMeshProUGUI>();
            _openSecondaryUrlBtn.gameObject.SetActive(false);
        }

        private void BuildRealDataTab(RectTransform parent)
        {
            var page = TerminalUiBuilder.CreateRect("RealDataPage", parent);
            TerminalUiBuilder.SetAnchors(page, 0f, 0f, 1f, 0.90f);
            _realDataPageGo = page.gameObject;

            var summaryBox = TerminalUiBuilder.CreateFlatPanel("RealDataBox", page, Theme.PanelBg, Theme.AccentSources, 2f);
            TerminalUiBuilder.SetAnchors(summaryBox, 0f, 0.62f, 1f, 0.99f);
            var sumInner = TerminalUiBuilder.GetInner(summaryBox);

            var heading = TerminalUiBuilder.CreateText("RealDataHdr", sumInner, Theme, Theme.HeaderFontSize, Theme.TextAmber, TextAlignmentOptions.MidlineLeft, Scaler);
            heading.fontStyle = FontStyles.Bold;
            heading.text = Localization.Get("sources.realdata.heading");
            TerminalUiBuilder.SetAnchors(heading.rectTransform, 0.02f, 0.78f, 0.98f, 0.97f);

            _realDataBodyText = TerminalUiBuilder.CreateText("RealDataBody", sumInner, Theme, Theme.BodyFontSize, Theme.TextPrimary, TextAlignmentOptions.TopLeft, Scaler);
            _realDataBodyText.text =
                Localization.Get("sources.realdata.line1") + "\n" +
                Localization.Get("sources.realdata.line2") + "\n" +
                Localization.Get("sources.realdata.line3") + "\n" +
                Localization.Get("sources.realdata.line4");
            TerminalUiBuilder.SetAnchors(_realDataBodyText.rectTransform, 0.02f, 0.04f, 0.98f, 0.76f);

            var tableBox = TerminalUiBuilder.CreateFlatPanel("ConstantsTableBox", page, Theme.PanelBg, Theme.BorderUnfocused, 2f);
            TerminalUiBuilder.SetAnchors(tableBox, 0f, 0.01f, 1f, 0.60f);
            var tableInner = TerminalUiBuilder.GetInner(tableBox);

            // Column headers
            var hdrRow = TerminalUiBuilder.CreateRect("TableHeader", tableInner);
            TerminalUiBuilder.SetAnchors(hdrRow, 0.01f, 0.87f, 0.99f, 0.99f);
            CreateHeaderCol(hdrRow, "ColMetric", Localization.Get("sources.col.metric"), 0.00f, 0.30f);
            CreateHeaderCol(hdrRow, "ColVal", Localization.Get("sources.col.value"), 0.31f, 0.68f);
            CreateHeaderCol(hdrRow, "ColStatus", Localization.Get("sources.col.status"), 0.69f, 0.81f);
            CreateHeaderCol(hdrRow, "ColSrc", Localization.Get("sources.col.source"), 0.82f, 1.00f);

            var listArea = TerminalUiBuilder.CreateRect("ConstantsScrollArea", tableInner);
            TerminalUiBuilder.SetAnchors(listArea, 0.01f, 0.02f, 0.99f, 0.86f);
            _constantsContent = CreateScrollContent("ConstantsScroll", listArea, spacing: 5f, padding: 4);
        }

        private void CreateHeaderCol(RectTransform parent, string name, string text, float xMin, float xMax)
        {
            float smallFont = Mathf.Max(16f, Theme.BodyFontSize - 4f);
            var tmp = TerminalUiBuilder.CreateText(name, parent, Theme, smallFont, Theme.TextAmber, TextAlignmentOptions.MidlineLeft, Scaler);
            tmp.fontStyle = FontStyles.Bold;
            tmp.text = text;
            TerminalUiBuilder.SetAnchors(tmp.rectTransform, xMin, 0f, xMax, 1f, 4f, 0f, 4f, 0f);
        }

        private RectTransform CreateScrollContent(string name, Transform parent, float spacing, int padding)
        {
            var viewport = TerminalUiBuilder.CreateRect(name + "_Viewport", parent);
            TerminalUiBuilder.SetStretch(viewport, 2f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = TerminalUiBuilder.CreateRect(name + "_Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            return content;
        }

        private void SwitchTab(bool showRealData)
        {
            _showingRealDataTab = showRealData;
            if (_sourcesPageGo != null) _sourcesPageGo.SetActive(!showRealData);
            if (_realDataPageGo != null) _realDataPageGo.SetActive(showRealData);

            if (_tabSourcesPanel != null)
            {
                TerminalUiBuilder.SetBorderColor(_tabSourcesPanel, !showRealData ? Theme.AccentSources : Theme.BorderUnfocused);
                var img = TerminalUiBuilder.GetInner(_tabSourcesPanel).GetComponent<Image>();
                if (img != null) img.color = !showRealData ? Theme.ButtonAccentBg : Theme.ButtonBg;
            }
            if (_tabRealDataPanel != null)
            {
                TerminalUiBuilder.SetBorderColor(_tabRealDataPanel, showRealData ? Theme.AccentSources : Theme.BorderUnfocused);
                var img = TerminalUiBuilder.GetInner(_tabRealDataPanel).GetComponent<Image>();
                if (img != null) img.color = showRealData ? Theme.ButtonAccentBg : Theme.ButtonBg;
            }
        }

        private void SelectIndex(int idx)
        {
            _selectedIdx = idx;
            UpdateSourceSelectionVisuals();
        }

        private void RebuildSourceListIfNeeded()
        {
            if (_sourceListContent == null) return;
            int count = _database != null && _database.Entries != null ? _database.Entries.Count : 0;
            if (count == _builtEntryCount) return;

            _builtEntryCount = count;
            _sourceRows.Clear();
            for (int i = _sourceListContent.childCount - 1; i >= 0; i--)
                Destroy(_sourceListContent.GetChild(i).gameObject);

            if (_tabSourcesBtn != null)
            {
                var lbl = _tabSourcesBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.text = Localization.Format("sources.tab.sources", count);
            }

            for (int i = 0; i < count; i++)
            {
                int slot = i;
                var rec = _database.Entries[i];
                var rowPanel = TerminalUiBuilder.CreateFlatPanel($"SrcRow_{i}", _sourceListContent, Theme.DesktopBg, Theme.BorderUnfocused, 2f);
                var le = rowPanel.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 46f;
                le.minHeight = 42f;

                var inner = TerminalUiBuilder.GetInner(rowPanel);
                var btn = rowPanel.gameObject.AddComponent<Button>();
                btn.targetGraphic = inner.GetComponent<Image>();
                btn.onClick.AddListener(() => SelectIndex(slot));
                rowPanel.gameObject.AddComponent<ButtonHoverFeedback>();

                float smallFont = Mathf.Max(16f, Theme.BodyFontSize - 4f);
                var txt = TerminalUiBuilder.CreateText("Label", inner, Theme, smallFont, Theme.TextPrimary, TextAlignmentOptions.MidlineLeft, Scaler);
                txt.textWrappingMode = TextWrappingModes.NoWrap;
                txt.overflowMode = TextOverflowModes.Ellipsis;
                txt.text = $"{rec.Id} ({rec.ResolveOrganization()})";
                TerminalUiBuilder.SetAnchors(txt.rectTransform, 0f, 0f, 1f, 1f, 8f, 2f, 8f, 2f);

                _sourceRows.Add((rowPanel, txt, rec));
            }

            if (_selectedIdx >= count) _selectedIdx = 0;
            UpdateSourceSelectionVisuals();
        }

        private void UpdateSourceSelectionVisuals()
        {
            for (int i = 0; i < _sourceRows.Count; i++)
            {
                bool sel = i == _selectedIdx;
                TerminalUiBuilder.SetBorderColor(_sourceRows[i].panel, sel ? Theme.AccentSources : Theme.BorderUnfocused);
                _sourceRows[i].label.color = sel ? Theme.TextAmber : Theme.TextPrimary;
            }

            if (_database != null && _database.Entries != null && _selectedIdx >= 0 && _selectedIdx < _database.Entries.Count)
            {
                var rec = _database.Entries[_selectedIdx];
                if (_detailTitle != null) _detailTitle.text = rec.ResolveTitle();
                if (_detailMeta != null)
                {
                    string lic = string.IsNullOrEmpty(rec.License) ? "UNVERIFIED - check before submission" : rec.License;
                    _detailMeta.text = $"{rec.Id}  |  {rec.ResolveOrganization()}\n{Localization.Get("ui.license")}: {lic}";
                }
                if (_detailSupports != null)
                    _detailSupports.text = $"<b>{Localization.Get("ui.supports")}:</b>\n{rec.ResolveSupports()}";
                if (_detailUrlText != null)
                    _detailUrlText.text = rec.UrlText;

                _selectedUrl = rec.UrlText ?? string.Empty;
                _selectedSecondaryUrl = rec.SecondaryUrl ?? string.Empty;
                if (_openUrlBtn != null)
                    _openUrlBtn.interactable = !string.IsNullOrWhiteSpace(_selectedUrl);
                if (_openSecondaryUrlBtn != null)
                {
                    bool hasSec = !string.IsNullOrWhiteSpace(_selectedSecondaryUrl);
                    _openSecondaryUrlBtn.gameObject.SetActive(hasSec);
                    if (hasSec && _openSecondaryLabel != null)
                        _openSecondaryLabel.text = _selectedSecondaryUrl.Contains("nasa.gov") ? Localization.Get("sources.open_nasa") : Localization.Get("sources.open_news");
                }
            }
        }

        private void RebuildConstantsTableIfNeeded()
        {
            if (_constantsContent == null) return;
            SimConfig cfg = Session?.Engine?.Config ?? new SimConfig();
            string sig = $"{Localization.CurrentLanguage}|{cfg.BaselineSurfaceDoseMsvPerSol:0.00}|{cfg.TotalSols}|{cfg.WaterUsePerCrew:0.0}|{cfg.StartOxygen:0}|{cfg.StartWater:0}|{cfg.StartFood:0}|{cfg.StormSolarFactor:0.00}|{cfg.ShieldingMaxReduction:0.00}";
            if (sig == _constantsConfigSignature) return;
            _constantsConfigSignature = sig;

            for (int i = _constantsContent.childCount - 1; i >= 0; i--)
                Destroy(_constantsContent.GetChild(i).gameObject);

            var rows = new (string name, string value, string status, Color statusCol, string srcId)[]
            {
                (
                    Localization.Get("sources.const.rad.name"),
                    Localization.Format("sources.const.rad.val", cfg.BaselineSurfaceDoseMsvPerSol),
                    "ADAPTED",
                    Theme.StatusWarn,
                    "hassler-2014-rad"
                ),
                (
                    Localization.Get("sources.const.sol.name"),
                    Localization.Format("sources.const.sol.val", cfg.TotalSols),
                    "REAL",
                    Theme.StatusOk,
                    "nasa-mars-fact-sheet"
                ),
                (
                    Localization.Get("sources.const.delay.name"),
                    Localization.Get("sources.const.delay.val"),
                    "ADAPTED",
                    Theme.StatusWarn,
                    "esa-light-time"
                ),
                (
                    Localization.Get("sources.const.water.name"),
                    Localization.Format("sources.const.water.val", cfg.WaterUsePerCrew),
                    "REAL",
                    Theme.StatusOk,
                    "iss-water-98"
                ),
                (
                    Localization.Get("sources.const.atmo.name"),
                    Localization.Get("sources.const.atmo.val"),
                    "REAL",
                    Theme.StatusOk,
                    "nasa-mars-fact-sheet"
                ),
                (
                    Localization.Get("sources.const.game.name"),
                    Localization.Format("sources.const.game.val", cfg.StartOxygen, cfg.StartWater, cfg.StartFood, cfg.StormSolarFactor * 100f, cfg.ShieldingMaxReduction * 100f),
                    "GAME",
                    Theme.AccentHelp,
                    "(sources-inspired)"
                )
            };

            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var rowPanel = TerminalUiBuilder.CreateFlatPanel($"ConstRow_{i}", _constantsContent, Theme.DesktopBg, Theme.BorderUnfocused, 1f);
                var le = rowPanel.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 52f;
                le.minHeight = 46f;

                var inner = TerminalUiBuilder.GetInner(rowPanel);
                CreateTableCell(inner, "Name", r.name, Theme.TextPrimary, 0.00f, 0.30f);
                CreateTableCell(inner, "Val", r.value, Theme.TextMuted, 0.31f, 0.68f);
                CreateTableCell(inner, "Status", r.status, r.statusCol, 0.69f, 0.81f, bold: true);
                CreateTableCell(inner, "Src", r.srcId, Theme.AccentHelp, 0.82f, 1.00f);
            }
        }

        private void CreateTableCell(RectTransform parent, string name, string text, Color col, float xMin, float xMax, bool bold = false)
        {
            float smallFont = Mathf.Max(16f, Theme.BodyFontSize - 4f);
            var tmp = TerminalUiBuilder.CreateText(name, parent, Theme, smallFont, col, TextAlignmentOptions.MidlineLeft, Scaler);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            tmp.text = text;
            TerminalUiBuilder.SetAnchors(tmp.rectTransform, xMin, 0f, xMax, 1f, 4f, 2f, 4f, 2f);
        }

        public override void Refresh(SessionSnapshot s)
        {
            RebuildSourceListIfNeeded();
            RebuildConstantsTableIfNeeded();
        }
    }
}
