using System;
using System.Collections.Generic;
using System.Text;
using Outpost.Sim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Outpost.Runtime
{
    /// <summary>
    /// M1 Throwaway Debug UI built with uGUI + TextMeshProUGUI to play and test a full 7-sol mission.
    /// </summary>
    public class M1DebugUIController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private OutpostConfigAsset configAsset;
        [SerializeField] private int startingSeed = 101;

        private OutpostSimulation _sim;
        private TMP_FontAsset _font;

        // Top Bar
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _urgentAlertText;
        private Image _urgentAlertBg;

        // Left Panel (Stocks, Systems, Sliders)
        private TextMeshProUGUI _stocksText;
        private TextMeshProUGUI _systemsText;
        private Slider _lifeSupportSlider;
        private Slider _greenhouseSlider;
        private Slider _heatingSlider;
        private Slider _commsSlider;
        private TextMeshProUGUI _lifeSupportValText;
        private TextMeshProUGUI _greenhouseValText;
        private TextMeshProUGUI _heatingValText;
        private TextMeshProUGUI _commsValText;
        private TextMeshProUGUI _powerBalanceText;

        // Center Panel (Event + Log)
        private TextMeshProUGUI _eventTitleText;
        private TextMeshProUGUI _eventDescText;
        private readonly List<Button> _eventChoiceButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> _eventChoiceLabels = new List<TextMeshProUGUI>();
        private TextMeshProUGUI _eventFactText;
        private TextMeshProUGUI _logText;

        // Right Panel (Crew, Actions, End Sol)
        private TextMeshProUGUI _crewText;
        private readonly List<(CrewActionType action, Button btn, TextMeshProUGUI label)> _actionButtons
            = new List<(CrewActionType, Button, TextMeshProUGUI)>();
        private Button _endSolButton;
        private TextMeshProUGUI _forecastText;

        // End Mission Debrief Overlay
        private GameObject _endPanel;
        private TextMeshProUGUI _endTitleText;
        private TextMeshProUGUI _endBodyText;

        private bool _updatingSliders;

        private void Awake()
        {
            ResolveFont();
            BuildUI();
            StartMission(startingSeed);
        }

        private void ResolveFont()
        {
            _font = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (_font == null)
            {
                Font runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (runtimeFont == null)
                    runtimeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (runtimeFont != null)
                {
                    _font = TMP_FontAsset.CreateFontAsset(runtimeFont);
                    _font.name = "M1FallbackTMPFont";
                }
            }
        }

        public void StartMission(int seed)
        {
            SimConfig cfg = configAsset != null ? configAsset.Config : new SimConfig();
            _sim = new OutpostSimulation(cfg);
            _sim.StartNewMission(seed);

            _updatingSliders = true;
            _lifeSupportSlider.value = _sim.Allocation.LifeSupport;
            _greenhouseSlider.value = _sim.Allocation.Greenhouse;
            _heatingSlider.value = _sim.Allocation.Heating;
            _commsSlider.value = _sim.Allocation.Comms;
            _updatingSliders = false;

            if (_endPanel != null) _endPanel.SetActive(false);
            RefreshUI();
        }

        private void OnSliderChanged(float _)
        {
            if (_updatingSliders || _sim == null) return;
            _sim.SetAllocations(
                _lifeSupportSlider.value,
                _greenhouseSlider.value,
                _heatingSlider.value,
                _commsSlider.value);
            RefreshUI();
        }

        private void OnEventChoiceClicked(int idx)
        {
            if (_sim == null) return;
            if (_sim.ResolveEventChoice(idx))
                RefreshUI();
        }

        private void OnActionClicked(CrewActionType action)
        {
            if (_sim == null) return;
            if (_sim.PerformAction(action))
                RefreshUI();
        }

        private void OnEndSolClicked()
        {
            if (_sim == null) return;
            _sim.EndSol();
            RefreshUI();
        }

        private void OnNewMissionClicked()
        {
            startingSeed++;
            StartMission(startingSeed);
        }

        private void RefreshUI()
        {
            if (_sim == null) return;

            SolForecast f = _sim.GetForecast();

            _headerText.text = $"{Localization.Get("game.title")}  |  {Localization.Get("ui.sol")} {_sim.CurrentSol}/{_sim.Config.TotalSols}  |  {Localization.Get("ui.ap")}: {_sim.ActionPoints}/{_sim.Config.ActionPointsPerSol}  |  SEED: {_sim.Seed}";

            _urgentAlertText.text = Localization.Get(f.UrgentAlertKey);
            _urgentAlertBg.color = f.UrgentSeverity == 2
                ? new Color(0.55f, 0.12f, 0.12f, 1f)
                : f.UrgentSeverity == 1
                    ? new Color(0.58f, 0.36f, 0.08f, 1f)
                    : new Color(0.12f, 0.36f, 0.22f, 1f);

            // Stocks + forecasted deltas
            _stocksText.text =
                $"<b>OUTPOST STOCKS (0-100)</b>\n" +
                $"Power (Battery): <b>{_sim.Power:0.0}</b>  ({f.NetPowerDelta:+0.0;-0.0})\n" +
                $"Oxygen:          <b>{_sim.Oxygen:0.0}</b>  ({f.NetOxygenDelta:+0.0;-0.0})\n" +
                $"Water:           <b>{_sim.Water:0.0}</b>  ({f.NetWaterDelta:+0.0;-0.0})\n" +
                $"Food:            <b>{_sim.Food:0.0}</b>  ({f.NetFoodDelta:+0.0;-0.0})\n" +
                $"Shielding:       <b>{_sim.Shielding:0.0}%</b>";

            string hazardTag = "";
            if (_sim.DustStormSolsRemaining > 0) hazardTag += $"<color=#FFAA44>DUST STORM ({_sim.DustStormSolsRemaining} sol)</color> ";
            if (_sim.SolarFlareActiveThisSol) hazardTag += "<color=#FF5555>SOLAR FLARE</color> ";
            if (string.IsNullOrEmpty(hazardTag)) hazardTag = "<color=#88DD99>Clear Mars Sky</color>";

            _systemsText.text =
                $"<b>SYSTEMS & ENVIRONMENT</b>\n" +
                $"Sky: {hazardTag}\n" +
                $"Panel Dust: <b>{_sim.PanelDust * 100f:0}%</b>  |  Solar Gen: <b>{f.PredictedGeneration:0.0}W</b>\n" +
                $"Scrubber: <b>{_sim.ScrubberHealth * 100f:0}%</b>  |  Recycler: <b>{_sim.RecyclerHealth * 100f:0}%</b>\n" +
                $"Crops: <b>{_sim.CropHealth * 100f:0}%</b>  |  Est. Dose: <b>{f.PredictedDoseMsv:0.00} mSv</b>";

            _lifeSupportValText.text = $"{_sim.Allocation.LifeSupport * 100f:0}% ({_sim.Allocation.LifeSupport * _sim.Config.LifeSupportMaxPower:0.0}W)";
            _greenhouseValText.text = $"{_sim.Allocation.Greenhouse * 100f:0}% ({_sim.Allocation.Greenhouse * _sim.Config.GreenhouseMaxPower:0.0}W)";
            _heatingValText.text = $"{_sim.Allocation.Heating * 100f:0}% ({_sim.Allocation.Heating * _sim.Config.HeatingMaxPower:0.0}W)";
            _commsValText.text = $"{_sim.Allocation.Comms * 100f:0}% ({_sim.Allocation.Comms * _sim.Config.CommsMaxPower:0.0}W)";

            string blackoutWarn = f.WillBlackout ? " <color=#FF5555>[BLACKOUT RISK: 20% EFF!]</color>" : "";
            string heatWarn = f.IsHeatingCold ? " <color=#FFAA44>[COLD <50%!]</color>" : "";
            _powerBalanceText.text = $"Gen: {f.PredictedGeneration:0.0}W  |  Demand: {f.PredictedDemand:0.0}W  |  Net: {f.NetPowerDelta:+0.0;-0.0}W{blackoutWarn}{heatWarn}";

            // Event panel
            var evt = _sim.CurrentEvent;
            if (evt != null)
            {
                _eventTitleText.text = $"SOL {_sim.CurrentSol} EVENT: {Localization.Get(evt.TitleKey)}";
                _eventDescText.text = Localization.Get(evt.DescriptionKey);

                for (int i = 0; i < _eventChoiceButtons.Count; i++)
                {
                    if (i < evt.Choices.Count)
                    {
                        _eventChoiceButtons[i].gameObject.SetActive(true);
                        _eventChoiceLabels[i].text = Localization.Get(evt.Choices[i].LabelKey);
                        _eventChoiceButtons[i].interactable = !_sim.CurrentEventResolved && _sim.CanChooseEventChoice(i);
                    }
                    else
                    {
                        _eventChoiceButtons[i].gameObject.SetActive(false);
                    }
                }

                if (_sim.CurrentEventResolved)
                {
                    var src = EventCatalog.GetSource(evt.SourceId);
                    _eventFactText.text =
                        $"<b>DECISION OUTCOME:</b> {_sim.LastEventOutcomeText}\n\n" +
                        $"<color=#88DDFF><b>DID YOU KNOW?</b></color> {Localization.Get(evt.FactKey)}\n" +
                        $"<color=#AAAAAA>Source: {Localization.Get(src.TitleKey)} ({Localization.Get(src.AgencyKey)})</color>";
                }
                else
                {
                    _eventFactText.text = "<i>Choose a response above before ending the sol.</i>";
                }
            }

            // Crew status
            var crewSb = new StringBuilder("<b>CREW STATUS (4 MEMBERS)</b>\n");
            for (int i = 0; i < _sim.Crew.Count; i++)
            {
                var c = _sim.Crew[i];
                string stateTag = !c.IsAlive ? "<color=#FF4444>[LOST]</color>"
                    : c.IsFallenCommander ? "<color=#FFBB44>[MEDBAY - NO WORK]</color>"
                    : "<color=#88EE99>[ACTIVE]</color>";
                crewSb.AppendLine($"<b>{Localization.Get(c.NameKey)}</b> ({Localization.Get("role." + c.Role)}) {stateTag}");
                crewSb.AppendLine($"   HP: {c.Health:0}/100  |  Morale: {c.Morale:0}/100  |  Dose: {c.CumulativeDoseMsv:0.00} mSv");
            }
            _crewText.text = crewSb.ToString();

            // Action buttons
            for (int i = 0; i < _actionButtons.Count; i++)
            {
                var item = _actionButtons[i];
                item.btn.interactable = _sim.CanPerformAction(item.action);
            }

            // Forecast summary & End Sol
            _endSolButton.interactable = _sim.Status == MissionStatus.InProgress;
            _forecastText.text =
                $"<b>NEXT SOL FORECAST</b>\n" +
                $"Power -> {f.PredictedBatteryAfter:0.0} ({f.NetPowerDelta:+0.0;-0.0}) | O2 {f.NetOxygenDelta:+0.0;-0.0} | H2O {f.NetWaterDelta:+0.0;-0.0} | Food {f.NetFoodDelta:+0.0;-0.0}" +
                (!_sim.CurrentEventResolved ? "\n<color=#FFBB44>Event pending: ending sol auto-selects fallback option.</color>" : "");

            // Log (latest 14 lines)
            var logSb = new StringBuilder("<b>MISSION LOG</b>\n");
            int start = Math.Max(0, _sim.SolLog.Count - 14);
            for (int i = _sim.SolLog.Count - 1; i >= start; i--)
                logSb.AppendLine(_sim.SolLog[i]);
            _logText.text = logSb.ToString();

            // End screen
            if (_sim.Status != MissionStatus.InProgress)
            {
                _endPanel.SetActive(true);
                bool won = _sim.Status == MissionStatus.Won;
                _endTitleText.text = won
                    ? $"{Localization.Get("mission.won.title")}  |  GRADE: {_sim.Grade} ({_sim.FinalScore:0.0})"
                    : Localization.Get("mission.lost.title");
                _endTitleText.color = won ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.45f, 0.45f);

                var endSb = new StringBuilder();
                endSb.AppendLine($"Crew Survived: <b>{_sim.LivingCrewCount}/4</b>  |  Final Sol: <b>{_sim.CurrentSol}</b>");
                endSb.AppendLine($"Remaining Stocks -> Power: {_sim.Power:0} | O2: {_sim.Oxygen:0} | Water: {_sim.Water:0} | Food: {_sim.Food:0} | Shield: {_sim.Shielding:0}%");
                endSb.AppendLine("\n<b>NASA MISSION DEBRIEF FACTS & SOURCES:</b>");
                for (int i = 0; i < _sim.DebriefCards.Count; i++)
                {
                    var card = _sim.DebriefCards[i];
                    var src = EventCatalog.GetSource(card.SourceId);
                    endSb.AppendLine($"• <b>Sol {card.Sol} - {Localization.Get(card.TitleKey)}:</b> {Localization.Get(card.FactKey)} <i>[{Localization.Get(src.AgencyKey)} — {src.UrlText}]</i>");
                }
                _endBodyText.text = endSb.ToString();
            }
        }

        private void BuildUI()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Root background
            var root = CreatePanel("RootBg", transform, new Color(0.06f, 0.07f, 0.09f, 1f));
            StretchFull(root);

            // Header bar
            var headerBar = CreatePanel("HeaderBar", root, new Color(0.11f, 0.14f, 0.18f, 1f));
            SetRect(headerBar, 0f, 0.92f, 1f, 1f, 8, 4, -8, -4);
            _headerText = CreateText("HeaderText", headerBar, 17, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_headerText.rectTransform, 0f, 0f, 0.84f, 1f, 12, 0, -8, 0);

            var newBtn = CreateButton("NewMissionBtn", headerBar, "NEW MISSION", new Color(0.22f, 0.35f, 0.52f, 1f), OnNewMissionClicked);
            SetRect(newBtn.GetComponent<RectTransform>(), 0.85f, 0.12f, 0.99f, 0.88f);

            // Urgent Alert Banner
            var alertRect = CreatePanel("UrgentAlert", root, new Color(0.12f, 0.36f, 0.22f, 1f));
            _urgentAlertBg = alertRect.GetComponent<Image>();
            SetRect(alertRect, 0f, 0.86f, 1f, 0.92f, 8, 2, -8, -2);
            _urgentAlertText = CreateText("UrgentAlertText", alertRect, 15, TextAlignmentOptions.Center, Color.white);
            _urgentAlertText.fontStyle = FontStyles.Bold;
            StretchFull(_urgentAlertText.rectTransform);

            // Three Columns below header (0 to 0.86 height)
            var leftCol = CreatePanel("LeftCol", root, new Color(0.10f, 0.12f, 0.15f, 1f));
            SetRect(leftCol, 0f, 0.01f, 0.32f, 0.85f, 8, 4, -4, -4);

            var centerCol = CreatePanel("CenterCol", root, new Color(0.10f, 0.12f, 0.15f, 1f));
            SetRect(centerCol, 0.32f, 0.01f, 0.68f, 0.85f, 4, 4, -4, -4);

            var rightCol = CreatePanel("RightCol", root, new Color(0.10f, 0.12f, 0.15f, 1f));
            SetRect(rightCol, 0.68f, 0.01f, 1f, 0.85f, 4, 4, -8, -4);

            BuildLeftColumn(leftCol);
            BuildCenterColumn(centerCol);
            BuildRightColumn(rightCol);
            BuildEndModal(root);
        }

        private void BuildLeftColumn(RectTransform parent)
        {
            _stocksText = CreateText("StocksText", parent, 14, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(_stocksText.rectTransform, 0f, 0.72f, 1f, 1f, 10, 4, -10, -10);

            _systemsText = CreateText("SystemsText", parent, 13, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.92f, 0.96f));
            SetRect(_systemsText.rectTransform, 0f, 0.50f, 1f, 0.72f, 10, 4, -10, -4);

            var sliderHeader = CreateText("SliderHeader", parent, 14, TextAlignmentOptions.TopLeft, new Color(1f, 0.84f, 0.45f));
            sliderHeader.text = "<b>POWER ALLOCATION (0-100%)</b>";
            SetRect(sliderHeader.rectTransform, 0f, 0.45f, 1f, 0.50f, 10, 0, -10, 0);

            (_lifeSupportSlider, _lifeSupportValText) = CreateLabeledSlider(parent, "Life Support (20W max)", 0.36f, 0.45f);
            (_greenhouseSlider, _greenhouseValText) = CreateLabeledSlider(parent, "Greenhouse (15W max)", 0.27f, 0.36f);
            (_heatingSlider, _heatingValText) = CreateLabeledSlider(parent, "Heating (15W max, >=50% safe)", 0.18f, 0.27f);
            (_commsSlider, _commsValText) = CreateLabeledSlider(parent, "Comms (5W max)", 0.09f, 0.18f);

            _powerBalanceText = CreateText("PowerBalance", parent, 13, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_powerBalanceText.rectTransform, 0f, 0.01f, 1f, 0.09f, 10, 4, -10, -4);
        }

        private void BuildCenterColumn(RectTransform parent)
        {
            _eventTitleText = CreateText("EventTitle", parent, 16, TextAlignmentOptions.TopLeft, new Color(1f, 0.75f, 0.35f));
            _eventTitleText.fontStyle = FontStyles.Bold;
            SetRect(_eventTitleText.rectTransform, 0f, 0.91f, 1f, 1f, 10, 2, -10, -8);

            _eventDescText = CreateText("EventDesc", parent, 14, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(_eventDescText.rectTransform, 0f, 0.76f, 1f, 0.91f, 10, 2, -10, -2);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                float top = 0.75f - i * 0.085f;
                float bottom = top - 0.075f;
                var btn = CreateButton($"ChoiceBtn_{i}", parent, $"Choice {i + 1}", new Color(0.20f, 0.28f, 0.38f, 1f), () => OnEventChoiceClicked(idx));
                SetRect(btn.GetComponent<RectTransform>(), 0f, bottom, 1f, top, 10, 0, -10, 0);
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.fontSize = 13;
                _eventChoiceButtons.Add(btn);
                _eventChoiceLabels.Add(lbl);
            }

            _eventFactText = CreateText("EventFact", parent, 13, TextAlignmentOptions.TopLeft, new Color(0.90f, 0.96f, 1f));
            SetRect(_eventFactText.rectTransform, 0f, 0.31f, 1f, 0.49f, 10, 4, -10, -4);

            _logText = CreateText("SolLog", parent, 12, TextAlignmentOptions.TopLeft, new Color(0.75f, 0.80f, 0.85f));
            SetRect(_logText.rectTransform, 0f, 0.01f, 1f, 0.30f, 10, 4, -10, -4);
        }

        private void BuildRightColumn(RectTransform parent)
        {
            _crewText = CreateText("CrewText", parent, 13, TextAlignmentOptions.TopLeft, Color.white);
            SetRect(_crewText.rectTransform, 0f, 0.64f, 1f, 1f, 10, 4, -10, -10);

            var actHeader = CreateText("ActHeader", parent, 14, TextAlignmentOptions.TopLeft, new Color(1f, 0.84f, 0.45f));
            actHeader.text = "<b>CREW ACTIONS (1 AP EACH)</b>";
            SetRect(actHeader.rectTransform, 0f, 0.59f, 1f, 0.64f, 10, 0, -10, 0);

            var defs = new (CrewActionType action, string titleKey, string descKey)[]
            {
                (CrewActionType.CleanPanels, "act.clean_panels", "act.clean_panels.desc"),
                (CrewActionType.RepairSystem, "act.repair", "act.repair.desc"),
                (CrewActionType.ReinforceShielding, "act.reinforce", "act.reinforce.desc"),
                (CrewActionType.RestMeal, "act.rest", "act.rest.desc"),
                (CrewActionType.TreatCrew, "act.treat", "act.treat.desc"),
                (CrewActionType.RationSupplies, "act.ration", "act.ration.desc")
            };

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                float top = 0.58f - i * 0.068f;
                float bottom = top - 0.062f;
                string labelText = $"<b>{Localization.Get(def.titleKey)}</b>\n<size=11>{Localization.Get(def.descKey)}</size>";
                var btn = CreateButton($"ActionBtn_{def.action}", parent, labelText, new Color(0.18f, 0.32f, 0.30f, 1f), () => OnActionClicked(def.action));
                SetRect(btn.GetComponent<RectTransform>(), 0f, bottom, 1f, top, 10, 0, -10, 0);
                var lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
                lbl.fontSize = 13;
                _actionButtons.Add((def.action, btn, lbl));
            }

            _forecastText = CreateText("ForecastText", parent, 12, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.90f, 0.95f));
            SetRect(_forecastText.rectTransform, 0f, 0.09f, 1f, 0.17f, 10, 2, -10, -2);

            _endSolButton = CreateButton("EndSolBtn", parent, "END SOL & ADVANCE", new Color(0.72f, 0.32f, 0.14f, 1f), OnEndSolClicked);
            SetRect(_endSolButton.GetComponent<RectTransform>(), 0f, 0.01f, 1f, 0.085f, 10, 2, -10, -2);
            _endSolButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;
        }

        private void BuildEndModal(RectTransform root)
        {
            var modal = CreatePanel("EndMissionModal", root, new Color(0.05f, 0.06f, 0.08f, 0.96f));
            StretchFull(modal);
            _endPanel = modal.gameObject;

            var card = CreatePanel("EndCard", modal, new Color(0.11f, 0.13f, 0.17f, 1f));
            SetRect(card, 0.08f, 0.08f, 0.92f, 0.92f);

            _endTitleText = CreateText("EndTitle", card, 22, TextAlignmentOptions.Center, Color.white);
            _endTitleText.fontStyle = FontStyles.Bold;
            SetRect(_endTitleText.rectTransform, 0f, 0.84f, 1f, 0.98f, 16, 4, -16, -8);

            _endBodyText = CreateText("EndBody", card, 13, TextAlignmentOptions.TopLeft, new Color(0.90f, 0.94f, 0.98f));
            SetRect(_endBodyText.rectTransform, 0f, 0.13f, 1f, 0.83f, 20, 4, -20, -4);

            var replayBtn = CreateButton("PlayAgainBtn", card, "PLAY AGAIN (NEW SEED)", new Color(0.22f, 0.45f, 0.28f, 1f), OnNewMissionClicked);
            SetRect(replayBtn.GetComponent<RectTransform>(), 0.32f, 0.02f, 0.68f, 0.11f);
            replayBtn.GetComponentInChildren<TextMeshProUGUI>().fontSize = 16;

            _endPanel.SetActive(false);
        }

        private (Slider slider, TextMeshProUGUI valText) CreateLabeledSlider(RectTransform parent, string title, float yMin, float yMax)
        {
            var holder = new GameObject(title, typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(parent, false);
            SetRect(holder, 0f, yMin, 1f, yMax, 10, 0, -10, 0);

            var lbl = CreateText("Label", holder, 12, TextAlignmentOptions.MidlineLeft, Color.white);
            lbl.text = title;
            SetRect(lbl.rectTransform, 0f, 0.50f, 0.65f, 1f);

            var val = CreateText("Val", holder, 12, TextAlignmentOptions.MidlineRight, new Color(0.65f, 0.95f, 1f));
            SetRect(val.rectTransform, 0.65f, 0.50f, 1f, 1f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(holder, false);
            var sliderRect = sliderGo.GetComponent<RectTransform>();
            SetRect(sliderRect, 0f, 0.08f, 1f, 0.48f);

            var bg = CreatePanel("Background", sliderRect, new Color(0.20f, 0.22f, 0.26f, 1f));
            StretchFull(bg);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(sliderRect, false);
            StretchFull(fillArea);

            var fill = CreatePanel("Fill", fillArea, new Color(0.25f, 0.65f, 0.85f, 1f));
            StretchFull(fill);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(sliderRect, false);
            StretchFull(handleArea);

            var handle = CreatePanel("Handle", handleArea, Color.white);
            handle.sizeDelta = new Vector2(14f, 0f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(OnSliderChanged);

            return (slider, val);
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go.GetComponent<RectTransform>();
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private Button CreateButton(string name, Transform parent, string text, Color bgColor, Action onClick)
        {
            var panel = CreatePanel(name, parent, bgColor);
            var btn = panel.gameObject.AddComponent<Button>();
            btn.targetGraphic = panel.GetComponent<Image>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.25f;
            colors.pressedColor = bgColor * 0.8f;
            colors.disabledColor = new Color(0.25f, 0.25f, 0.28f, 0.65f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateText("Text", panel, 14, TextAlignmentOptions.Center, Color.white);
            tmp.text = text;
            SetRect(tmp.rectTransform, 0f, 0f, 1f, 1f, 6, 2, -6, -2);
            return btn;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }
    }
}
