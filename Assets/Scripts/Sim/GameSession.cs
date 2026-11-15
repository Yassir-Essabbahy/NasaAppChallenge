using System;
using System.Collections.Generic;

namespace Outpost.Sim
{
    public enum PowerSystemType
    {
        LifeSupport,
        Greenhouse,
        Heating,
        Comms
    }

    public enum TerminalAppId
    {
        PowerGrid = 0,
        LifeSupport = 1,
        Greenhouse = 2,
        Radiation = 3,
        Crew = 4,
        Comms = 5,
        CommandersLog = 6,
        Sources = 7
    }

    public class CrewSnapshot
    {
        public int Id;
        public string Name;
        public CrewRole Role;
        public string RoleHint;
        public float Health;
        public float Morale;
        public float DoseMsv;
        public bool IsFallenCommander;
        public bool IsAlive;
        public string StatusTag; // OK / LOW / CRITICAL / MEDBAY / LOST
    }

    public class EventChoiceSnapshot
    {
        public int Index;
        public string Label;
        public string OutcomePreview;
        public bool CanSelect;
        public int ApCost;
    }

    public class EventSnapshot
    {
        public string Id;
        public string Title;
        public string Description;
        public string FactText;
        public string SourceId;
        public bool IsResolved;
        public string OutcomeText;
        public List<EventChoiceSnapshot> Choices = new List<EventChoiceSnapshot>();
    }

    public class CommsMessageSnapshot
    {
        public string Id;
        public int Sol;
        public string Subject;
        public string DelayText;
        public string Body;
        public bool IsRead;
    }

    public class LogEntrySnapshot
    {
        public string Id;
        public int Sol;
        public string Title;
        public string Body;
        public bool IsRead;
        public TerminalAppId? HighlightApp;
    }

    public class SolSummarySnapshot
    {
        public int CompletedSol;
        public int NewSol;
        public float PowerDelta;
        public float OxygenDelta;
        public float WaterDelta;
        public float FoodDelta;
        public string EventSummary;
    }

    public class SessionSnapshot
    {
        public int Seed;
        public string CommanderName;
        public int CurrentSol;
        public int TotalSols;
        public int SolsRemainingForRescue;
        public int ActionPoints;
        public int MaxActionPoints;
        public MissionStatus Status;
        public MissionGrade Grade;
        public float FinalScore;
        public int LivingCrewCount;

        // Stocks (0-100)
        public float Power;
        public float Oxygen;
        public float Water;
        public float Food;
        public float Shielding;

        // Subsystems & Environment
        public float PanelDustPct;
        public float ScrubberHealthPct;
        public float RecyclerHealthPct;
        public float CropHealthPct;
        public int DustStormSolsRemaining;
        public bool SolarFlareActive;
        public bool RationingActive;
        public bool LastSolBlackout;

        // Allocations (0-100)
        public int LifeSupportAllocPct;
        public int GreenhouseAllocPct;
        public int HeatingAllocPct;
        public int CommsAllocPct;

        // Forecast & Power
        public float SolarGeneration;
        public string GenerationReason;
        public float TotalDemand;
        public float NetPowerDelta;
        public float ProjectedBatteryAfter;
        public bool WillBlackout;
        public float NetOxygenDelta;
        public float NetWaterDelta;
        public float NetFoodDelta;
        public float ProjectedDoseMsv;
        public bool IsHeatingCold;
        public float DoseHealthThresholdMsv;
        public float StatusLightOkThreshold;
        public float StatusLightLowThreshold;
        public string ForecastSummaryLine;

        // Urgent Alert
        public string AlertText;
        public int AlertSeverity; // 0 = OK, 1 = WARN, 2 = CRIT

        // End-Sol Safety Confirm
        public bool NeedsEndSolConfirm;
        public string EndSolDangerSummary;

        // Collections
        public List<CrewSnapshot> Crew = new List<CrewSnapshot>();
        public EventSnapshot ActiveEvent;
        public List<CommsMessageSnapshot> CommsInbox = new List<CommsMessageSnapshot>();
        public int UnreadCommsCount;
        public bool CommsOnline;
        public List<LogEntrySnapshot> LogEntries = new List<LogEntrySnapshot>();
        public int UnreadLogCount;
        public TerminalAppId? GuidedHighlightApp;
        public List<DebriefCard> DebriefCards = new List<DebriefCard>();
        public SolSummarySnapshot LastSolSummary;
    }

    /// <summary>
    /// Command and event facade over OutpostSimulation.
    /// Exposes read-only SessionSnapshot instances and notifies UI exclusively through C# events.
    /// </summary>
    public class GameSession
    {
        public OutpostSimulation Engine { get; }

        public event Action<SessionSnapshot> OnSolStarted;
        public event Action<SessionSnapshot> OnStateChanged;
        public event Action<SessionSnapshot> OnEventTriggered;
        public event Action<SessionSnapshot> OnMissionEnded;

        private readonly HashSet<int> _receivedCommsSols = new HashSet<int>();
        private readonly HashSet<string> _readCommsIds = new HashSet<string>();
        private readonly HashSet<string> _readLogIds = new HashSet<string>();
        private readonly List<(string id, int sol, string titleKey, string bodyKey, TerminalAppId? highlight)> _logCatalog
            = new List<(string, int, string, string, TerminalAppId?)>
            {
                ("log_s1_vale", 1, "log.sol1.vale.title", "log.sol1.vale.body", TerminalAppId.PowerGrid),
                ("log_s1_alloc", 1, "log.sol1.alloc.title", "log.sol1.alloc.body", TerminalAppId.PowerGrid),
                ("log_s1_ap", 1, "log.sol1.ap.title", "log.sol1.ap.body", TerminalAppId.Crew),
                ("log_s1_endsol", 1, "log.sol1.endsol.title", "log.sol1.endsol.body", null),
                ("log_s2_maint", 2, "log.sol2.maint.title", "log.sol2.maint.body", TerminalAppId.LifeSupport),
                ("log_s3_storms", 3, "log.sol3.storms.title", "log.sol3.storms.body", TerminalAppId.PowerGrid),
                ("log_s4_rad", 4, "log.sol4.rad.title", "log.sol4.rad.body", TerminalAppId.Radiation),
                ("log_s5_morale", 5, "log.sol5.morale.title", "log.sol5.morale.body", TerminalAppId.Greenhouse),
                ("log_s6_comms", 6, "log.sol6.comms.title", "log.sol6.comms.body", TerminalAppId.Comms),
                ("log_s7_rescue", 7, "log.sol7.rescue.title", "log.sol7.rescue.body", null)
            };

        public SessionSnapshot CurrentSnapshot { get; private set; }
        public string CommanderName { get; private set; } = "COMMANDER";

        public void SetCommanderName(string rawName)
        {
            CommanderName = SanitizeCommanderName(rawName);
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public static string SanitizeCommanderName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName))
                return Localization.Get("login.default");

            var clean = new System.Text.StringBuilder(12);
            for (int i = 0; i < rawName.Length && clean.Length < 12; i++)
            {
                char ch = rawName[i];
                if (char.IsLetterOrDigit(ch) || ch == ' ')
                    clean.Append(ch);
            }
            string trimmed = clean.ToString().Trim();
            return string.IsNullOrEmpty(trimmed) ? Localization.Get("login.default") : trimmed;
        }

        public GameSession(SimConfig config = null, int seed = 101)
        {
            Engine = new OutpostSimulation(config);
            RestartMission(seed);
        }

        public void RestartMission(int seed)
        {
            _receivedCommsSols.Clear();
            _readCommsIds.Clear();
            _readLogIds.Clear();

            Engine.StartNewMission(seed);
            TryReceiveCommsForCurrentSol();
            RebuildSnapshot();

            OnSolStarted?.Invoke(CurrentSnapshot);
            if (CurrentSnapshot.ActiveEvent != null && !CurrentSnapshot.ActiveEvent.IsResolved)
                OnEventTriggered?.Invoke(CurrentSnapshot);
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public void SetAllocation(PowerSystemType system, int pct)
        {
            if (Engine.Status != MissionStatus.InProgress) return;
            int snapped = ((int)Math.Round(pct / 10.0) * 10);
            if (snapped < 0) snapped = 0;
            if (snapped > 100) snapped = 100;
            float val = snapped / 100f;

            var a = Engine.Allocation;
            float ls = system == PowerSystemType.LifeSupport ? val : a.LifeSupport;
            float gh = system == PowerSystemType.Greenhouse ? val : a.Greenhouse;
            float ht = system == PowerSystemType.Heating ? val : a.Heating;
            float cm = system == PowerSystemType.Comms ? val : a.Comms;

            Engine.SetAllocations(ls, gh, ht, cm);
            TryReceiveCommsForCurrentSol();
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public bool DoAction(CrewActionType actionId, int targetCrewId = -1)
        {
            bool ok = Engine.PerformAction(actionId, targetCrewId);
            if (!ok) return false;
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
            return true;
        }

        public bool ChooseEventOption(int optionId)
        {
            bool ok = Engine.ResolveEventChoice(optionId);
            if (!ok) return false;
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
            if (Engine.Status != MissionStatus.InProgress)
                OnMissionEnded?.Invoke(CurrentSnapshot);
            return true;
        }

        public void EndSol()
        {
            if (Engine.Status != MissionStatus.InProgress) return;

            int previousSol = Engine.CurrentSol;
            Engine.EndSol();
            TryReceiveCommsForCurrentSol();
            RebuildSnapshot();

            OnStateChanged?.Invoke(CurrentSnapshot);
            if (Engine.Status != MissionStatus.InProgress)
            {
                OnMissionEnded?.Invoke(CurrentSnapshot);
                return;
            }

            if (Engine.CurrentSol != previousSol)
            {
                OnSolStarted?.Invoke(CurrentSnapshot);
                if (CurrentSnapshot.ActiveEvent != null && !CurrentSnapshot.ActiveEvent.IsResolved)
                    OnEventTriggered?.Invoke(CurrentSnapshot);
            }
        }

        public void MarkCommsRead(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_readCommsIds.Add(id))
            {
                RebuildSnapshot();
                OnStateChanged?.Invoke(CurrentSnapshot);
            }
        }

        public void MarkLogRead(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_readLogIds.Add(id))
            {
                RebuildSnapshot();
                OnStateChanged?.Invoke(CurrentSnapshot);
            }
        }

        public void MarkAllUnreadLogsForAppRead(TerminalAppId app)
        {
            bool changed = false;
            for (int i = 0; i < _logCatalog.Count; i++)
            {
                var entry = _logCatalog[i];
                if (entry.sol <= Engine.CurrentSol && entry.highlight == app)
                {
                    if (_readLogIds.Add(entry.id)) changed = true;
                }
            }
            if (changed)
            {
                RebuildSnapshot();
                OnStateChanged?.Invoke(CurrentSnapshot);
            }
        }

        // --- DEVELOPER DEBUG COMMANDS ---
        public void DebugSkipSol() => EndSol();

        public void DebugTriggerEvent(string eventId)
        {
            if (Engine.DebugTriggerEvent(eventId))
            {
                RebuildSnapshot();
                OnEventTriggered?.Invoke(CurrentSnapshot);
                OnStateChanged?.Invoke(CurrentSnapshot);
            }
        }

        public void DebugSetStocks(float power, float o2, float water, float food, float shielding)
        {
            Engine.DebugSetStocks(power, o2, water, food, shielding);
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public void DebugSetStorm(bool active)
        {
            Engine.DebugSetStorm(active);
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public void DebugSetBattery(float power)
        {
            Engine.DebugSetBattery(power);
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public void DebugSetSol(int sol)
        {
            Engine.DebugSetSol(sol);
            TryReceiveCommsForCurrentSol();
            RebuildSnapshot();
            OnSolStarted?.Invoke(CurrentSnapshot);
            if (CurrentSnapshot.ActiveEvent != null && !CurrentSnapshot.ActiveEvent.IsResolved)
                OnEventTriggered?.Invoke(CurrentSnapshot);
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        public void DebugForceOutcome(bool win)
        {
            Engine.DebugForceOutcome(win);
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
            OnMissionEnded?.Invoke(CurrentSnapshot);
        }

        private void TryReceiveCommsForCurrentSol()
        {
            if (Engine.Allocation.Comms > 0.01f)
            {
                for (int s = 1; s <= Engine.CurrentSol; s++)
                    _receivedCommsSols.Add(s);
            }
        }

        public void RefreshSnapshot()
        {
            RebuildSnapshot();
            OnStateChanged?.Invoke(CurrentSnapshot);
        }

        private void RebuildSnapshot()
        {
            var f = Engine.GetForecast();
            var snap = new SessionSnapshot
            {
                Seed = Engine.Seed,
                CommanderName = CommanderName,
                CurrentSol = Engine.CurrentSol,
                TotalSols = Engine.Config.TotalSols,
                SolsRemainingForRescue = Math.Max(0, Engine.Config.TotalSols - Engine.CurrentSol),
                ActionPoints = Engine.ActionPoints,
                MaxActionPoints = Engine.Config.ActionPointsPerSol,
                Status = Engine.Status,
                Grade = Engine.Grade,
                FinalScore = Engine.FinalScore,
                LivingCrewCount = Engine.LivingCrewCount,

                Power = Engine.Power,
                Oxygen = Engine.Oxygen,
                Water = Engine.Water,
                Food = Engine.Food,
                Shielding = Engine.Shielding,

                PanelDustPct = Engine.PanelDust * 100f,
                ScrubberHealthPct = Engine.ScrubberHealth * 100f,
                RecyclerHealthPct = Engine.RecyclerHealth * 100f,
                CropHealthPct = Engine.CropHealth * 100f,
                DustStormSolsRemaining = Engine.DustStormSolsRemaining,
                SolarFlareActive = Engine.SolarFlareActiveThisSol,
                RationingActive = Engine.RationingActiveThisSol,
                LastSolBlackout = Engine.LastSolBlackout,

                LifeSupportAllocPct = (int)Math.Round(Engine.Allocation.LifeSupport * 100f),
                GreenhouseAllocPct = (int)Math.Round(Engine.Allocation.Greenhouse * 100f),
                HeatingAllocPct = (int)Math.Round(Engine.Allocation.Heating * 100f),
                CommsAllocPct = (int)Math.Round(Engine.Allocation.Comms * 100f),

                SolarGeneration = f.PredictedGeneration,
                GenerationReason = BuildGenerationReason(),
                TotalDemand = f.PredictedDemand,
                NetPowerDelta = f.NetPowerDelta,
                ProjectedBatteryAfter = f.PredictedBatteryAfter,
                WillBlackout = f.WillBlackout,
                NetOxygenDelta = f.NetOxygenDelta,
                NetWaterDelta = f.NetWaterDelta,
                NetFoodDelta = f.NetFoodDelta,
                ProjectedDoseMsv = f.PredictedDoseMsv,
                IsHeatingCold = f.IsHeatingCold,
                DoseHealthThresholdMsv = Engine.Config.DoseHealthThresholdMsv,
                StatusLightOkThreshold = Engine.Config.StatusLightOkThreshold,
                StatusLightLowThreshold = Engine.Config.StatusLightLowThreshold,
                ForecastSummaryLine = Localization.Format("sim.forecast_summary", f.NetOxygenDelta, f.NetWaterDelta, f.NetFoodDelta, f.NetPowerDelta),
                CommsOnline = Engine.Allocation.Comms > 0.01f
            };

            BuildCrewSnapshots(snap);
            BuildEventSnapshot(snap);
            BuildCommsSnapshots(snap);
            BuildLogSnapshots(snap);
            BuildAlertPriority(snap);
            BuildEndSolConfirm(snap, f);
            snap.DebriefCards = new List<DebriefCard>(Engine.DebriefCards);

            if (Engine.LastCompletedSol > 0)
            {
                snap.LastSolSummary = new SolSummarySnapshot
                {
                    CompletedSol = Engine.LastCompletedSol,
                    NewSol = Engine.CurrentSol,
                    PowerDelta = Engine.LastSolPowerDelta,
                    OxygenDelta = Engine.LastSolOxygenDelta,
                    WaterDelta = Engine.LastSolWaterDelta,
                    FoodDelta = Engine.LastSolFoodDelta,
                    EventSummary = Engine.LastSolEventSummary
                };
            }

            CurrentSnapshot = snap;
        }

        private string BuildGenerationReason()
        {
            int dustLoss = (int)Math.Round(Engine.PanelDust * 100f);
            if (Engine.DustStormSolsRemaining > 0)
            {
                int stormLoss = (int)Math.Round((1f - Engine.Config.StormSolarFactor) * 100f);
                return Localization.Format("sim.gen_dust_storm", dustLoss, stormLoss);
            }
            return Localization.Format("sim.gen_dust", dustLoss);
        }

        private void BuildCrewSnapshots(SessionSnapshot snap)
        {
            for (int i = 0; i < Engine.Crew.Count; i++)
            {
                var c = Engine.Crew[i];
                string statusTag = !c.IsAlive ? Localization.Get("ui.deceased")
                    : c.Health < 30f ? Localization.Get("status.crit")
                    : c.IsFallenCommander ? Localization.Get("ui.medbay")
                    : c.Health < 60f ? Localization.Get("status.low")
                    : Localization.Get("status.ok");

                snap.Crew.Add(new CrewSnapshot
                {
                    Id = c.Id,
                    Name = Localization.Get(c.NameKey),
                    Role = c.Role,
                    RoleHint = Localization.Get("role." + c.Role),
                    Health = c.Health,
                    Morale = c.Morale,
                    DoseMsv = c.CumulativeDoseMsv,
                    IsFallenCommander = c.IsFallenCommander,
                    IsAlive = c.IsAlive,
                    StatusTag = statusTag
                });
            }
        }

        private void BuildEventSnapshot(SessionSnapshot snap)
        {
            var evt = Engine.CurrentEvent;
            if (evt == null) return;

            var es = new EventSnapshot
            {
                Id = evt.Id,
                Title = Localization.Get(evt.TitleKey),
                Description = Localization.Get(evt.DescriptionKey),
                FactText = Localization.Get(evt.FactKey),
                SourceId = evt.SourceId,
                IsResolved = Engine.CurrentEventResolved,
                OutcomeText = Engine.LastEventOutcomeText
            };

            for (int i = 0; i < evt.Choices.Count; i++)
            {
                var ch = evt.Choices[i];
                es.Choices.Add(new EventChoiceSnapshot
                {
                    Index = i,
                    Label = Localization.Get(ch.LabelKey),
                    OutcomePreview = Localization.Get(ch.OutcomeKey),
                    CanSelect = Engine.CanChooseEventChoice(i),
                    ApCost = ch.ApCost
                });
            }
            snap.ActiveEvent = es;
        }

        private void BuildCommsSnapshots(SessionSnapshot snap)
        {
            int unread = 0;
            for (int s = Engine.CurrentSol; s >= 1; s--)
            {
                if (!_receivedCommsSols.Contains(s)) continue;
                string id = $"comms_s{s}";
                bool read = _readCommsIds.Contains(id);
                if (!read) unread++;
                snap.CommsInbox.Add(new CommsMessageSnapshot
                {
                    Id = id,
                    Sol = s,
                    Subject = Localization.Get($"comms.sol{s}.subject"),
                    DelayText = Localization.Get($"comms.sol{s}.delay"),
                    Body = $"{Localization.Format("comms.to_commander", CommanderName)}\n\n{Localization.Get($"comms.sol{s}.body")}",
                    IsRead = read
                });
            }
            snap.UnreadCommsCount = unread;
        }

        private void BuildLogSnapshots(SessionSnapshot snap)
        {
            int unread = 0;
            TerminalAppId? guided = null;
            for (int i = 0; i < _logCatalog.Count; i++)
            {
                var item = _logCatalog[i];
                if (item.sol > Engine.CurrentSol) continue;
                bool read = _readLogIds.Contains(item.id);
                if (!read)
                {
                    unread++;
                    if (Engine.CurrentSol <= 2 && guided == null && item.highlight.HasValue)
                        guided = item.highlight.Value;
                }
                snap.LogEntries.Add(new LogEntrySnapshot
                {
                    Id = item.id,
                    Sol = item.sol,
                    Title = Localization.Get(item.titleKey),
                    Body = Localization.Get(item.bodyKey),
                    IsRead = read,
                    HighlightApp = item.highlight
                });
            }
            snap.UnreadLogCount = unread;
            snap.GuidedHighlightApp = guided;
        }

        /// <summary>
        /// Priority: crew about to die > resource at 0 > resource under 20 > active event > "No alerts."
        /// </summary>
        private void BuildAlertPriority(SessionSnapshot snap)
        {
            CrewSnapshot dying = null;
            for (int i = 0; i < snap.Crew.Count; i++)
            {
                var c = snap.Crew[i];
                if (c.IsAlive && c.Health <= 25f)
                {
                    if (dying == null || c.Health < dying.Health) dying = c;
                }
            }
            if (dying != null)
            {
                snap.AlertText = Localization.Format("alert.crew_dying", dying.Name, dying.Health);
                snap.AlertSeverity = 2;
                return;
            }

            if (snap.Oxygen <= 0f) { snap.AlertText = Localization.Format("alert.stock_zero", Localization.Get("ui.oxygen")); snap.AlertSeverity = 2; return; }
            if (snap.Water <= 0f) { snap.AlertText = Localization.Format("alert.stock_zero", Localization.Get("ui.water")); snap.AlertSeverity = 2; return; }
            if (snap.Food <= 0f) { snap.AlertText = Localization.Format("alert.stock_zero", Localization.Get("ui.food")); snap.AlertSeverity = 2; return; }
            if (snap.Power <= 0f) { snap.AlertText = Localization.Format("alert.stock_zero", Localization.Get("ui.power")); snap.AlertSeverity = 2; return; }

            if (snap.Oxygen < 20f) { snap.AlertText = Localization.Format("alert.stock_low", Localization.Get("ui.oxygen")); snap.AlertSeverity = 1; return; }
            if (snap.Water < 20f) { snap.AlertText = Localization.Format("alert.stock_low", Localization.Get("ui.water")); snap.AlertSeverity = 1; return; }
            if (snap.Food < 20f) { snap.AlertText = Localization.Format("alert.stock_low", Localization.Get("ui.food")); snap.AlertSeverity = 1; return; }
            if (snap.Power < 20f) { snap.AlertText = Localization.Format("alert.stock_low", Localization.Get("ui.power")); snap.AlertSeverity = 1; return; }
            if (snap.Shielding < 20f) { snap.AlertText = Localization.Format("alert.stock_low", Localization.Get("ui.shielding")); snap.AlertSeverity = 1; return; }

            if (snap.ActiveEvent != null && !snap.ActiveEvent.IsResolved)
            {
                snap.AlertText = Localization.Get("alert.event_pending");
                snap.AlertSeverity = 1;
                return;
            }

            snap.AlertText = Localization.Get("alert.none");
            snap.AlertSeverity = 0;
        }

        private void BuildEndSolConfirm(SessionSnapshot snap, SolForecast f)
        {
            var dangers = new List<string>();
            if (f.WillBlackout || f.PredictedBatteryAfter <= 0f) dangers.Add("Battery");
            if (snap.Oxygen + f.NetOxygenDelta <= 0f) dangers.Add("Oxygen");
            if (snap.Water + f.NetWaterDelta <= 0f) dangers.Add("Water");
            if (snap.Food + f.NetFoodDelta <= 0f) dangers.Add("Food");

            snap.NeedsEndSolConfirm = dangers.Count > 0;
            snap.EndSolDangerSummary = string.Join(", ", dangers);
        }
    }
}
