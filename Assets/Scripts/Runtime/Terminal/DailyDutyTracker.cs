using Outpost.Runtime.Room;
using Outpost.Sim;

namespace Outpost.Runtime.Terminal
{
    public struct DailyDutyItem
    {
        public string Label;
        public bool IsDone;
        public TerminalAppId? TargetApp;
        public bool OpensEvent;
        public bool IsRoomDuty;
    }

    /// <summary>
    /// Tracks mandatory daily shift duties per Sol (1..7) across Terminal and 3D Outpost stations.
    /// Prevents spamming END SOL without resolving the daily situation, taking an operational
    /// command action, and inspecting the day's assigned facility/surface station (with indoor fallbacks
    /// during active dust storms/solar flares or standalone Terminal scene runs).
    /// </summary>
    public class DailyDutyTracker
    {
        private readonly DailyDutyItem[] _items = new DailyDutyItem[3];

        private int _trackedSol = -1;
        private bool _powerAdjustedThisSol;
        private bool _roomCheckDoneThisSol;
        private bool _terminalAuditDoneThisSol;
        private bool _hasRoomMode;

        public bool HasRoomMode => _hasRoomMode;
        public int CompletedCount { get; private set; }
        public bool AllComplete => CompletedCount >= 3;
        public string RoomHudPrompt { get; private set; } = string.Empty;
        public bool IsRoomDutyDone => _roomCheckDoneThisSol;

        public void ConfigureMode(bool hasRoomMode)
        {
            _hasRoomMode = hasRoomMode;
        }

        public void ResetForSol(int sol)
        {
            _trackedSol = sol;
            _powerAdjustedThisSol = false;
            _roomCheckDoneThisSol = false;
            _terminalAuditDoneThisSol = false;
        }

        public void NotifyPowerAdjusted()
        {
            _powerAdjustedThisSol = true;
        }

        public bool NotifyAppOpened(SessionSnapshot s, TerminalAppId appId)
        {
            if (s == null || _hasRoomMode) return false;
            EnsureSolSync(s.CurrentSol);
            if (!_terminalAuditDoneThisSol && appId == GetFallbackTerminalApp(s.CurrentSol))
            {
                _terminalAuditDoneThisSol = true;
                return true;
            }
            return false;
        }

        public bool NotifyPropInteracted(SessionSnapshot s, InteractablePropType propType)
        {
            if (s == null || !_hasRoomMode) return false;
            EnsureSolSync(s.CurrentSol);
            if (_roomCheckDoneThisSol) return false;

            if (MatchesExpectedRoomTarget(s, propType))
            {
                _roomCheckDoneThisSol = true;
                Evaluate(s);
                return true;
            }
            return false;
        }

        public DailyDutyItem[] Evaluate(SessionSnapshot s)
        {
            if (s == null) return _items;
            EnsureSolSync(s.CurrentSol);

            // 1. Daily Situation / Event Response
            bool eventDone = s.ActiveEvent == null || s.ActiveEvent.IsResolved;
            _items[0] = new DailyDutyItem
            {
                Label = DesktopLocalization.Get(s.CurrentSol == 1 ? "duty.step_event_sol1" : "duty.step_event"),
                IsDone = eventDone,
                TargetApp = null,
                OpensEvent = !eventDone,
                IsRoomDuty = false
            };

            // 2. Command Action (adjust power on Sol 1 or spend >= 1 AP on operations)
            bool actionDone = s.CurrentSol == 1
                ? (_powerAdjustedThisSol || s.ActionPoints < s.MaxActionPoints)
                : (s.ActionPoints < s.MaxActionPoints);

            TerminalAppId opApp = GetOpsTargetApp(s);
            _items[1] = new DailyDutyItem
            {
                Label = DesktopLocalization.Get(s.CurrentSol == 1 ? "duty.step_ops_sol1" : "duty.step_ops"),
                IsDone = actionDone,
                TargetApp = opApp,
                OpensEvent = false,
                IsRoomDuty = false
            };

            // 3. Daily Physical Facility / Surface Round (3D Outpost) or Subsystem Audit (Terminal-only)
            if (_hasRoomMode)
            {
                string stepKey = GetRoomDutyStepKey(s);
                _items[2] = new DailyDutyItem
                {
                    Label = DesktopLocalization.Get(stepKey),
                    IsDone = _roomCheckDoneThisSol,
                    TargetApp = null,
                    OpensEvent = false,
                    IsRoomDuty = true
                };

                RoomHudPrompt = _roomCheckDoneThisSol
                    ? DesktopLocalization.Get("duty.hud_done")
                    : DesktopLocalization.Format("duty.hud_pending", s.CurrentSol, _items[2].Label);
            }
            else
            {
                TerminalAppId auditApp = GetFallbackTerminalApp(s.CurrentSol);
                _items[2] = new DailyDutyItem
                {
                    Label = DesktopLocalization.Format("duty.step_term_audit", DesktopLocalization.GetAppTitle(auditApp)),
                    IsDone = _terminalAuditDoneThisSol,
                    TargetApp = auditApp,
                    OpensEvent = false,
                    IsRoomDuty = false
                };
                RoomHudPrompt = string.Empty;
            }

            int done = 0;
            for (int i = 0; i < 3; i++)
            {
                if (_items[i].IsDone) done++;
            }
            CompletedCount = done;
            return _items;
        }

        public bool TryGetFirstPendingDuty(SessionSnapshot s, out DailyDutyItem pending)
        {
            Evaluate(s);
            for (int i = 0; i < 3; i++)
            {
                if (!_items[i].IsDone)
                {
                    pending = _items[i];
                    return true;
                }
            }
            pending = default;
            return false;
        }

        private void EnsureSolSync(int currentSol)
        {
            if (_trackedSol != currentSol)
                ResetForSol(currentSol);
        }

        private static bool EvaLocked(SessionSnapshot s)
        {
            return s.SolarFlareActive || s.DustStormSolsRemaining > 0;
        }

        private static bool MatchesExpectedRoomTarget(SessionSnapshot s, InteractablePropType prop)
        {
            bool locked = EvaLocked(s);
            switch (s.CurrentSol)
            {
                case 1: return prop == InteractablePropType.MedbayPatient;
                case 2: return prop == InteractablePropType.StorageOxygenTank;
                case 3: return locked ? prop == InteractablePropType.StorageWaterTank : (prop == InteractablePropType.SurfaceRadiationPost || prop == InteractablePropType.SurfaceSolarArray);
                case 4: return prop == InteractablePropType.StorageWaterTank;
                case 5: return locked ? prop == InteractablePropType.Radio : (prop == InteractablePropType.SurfaceAntenna || prop == InteractablePropType.Radio);
                case 6: return prop == InteractablePropType.GreenhousePlanter;
                default: return locked ? prop == InteractablePropType.MedbayPatient : (prop == InteractablePropType.SurfaceSolarArray || prop == InteractablePropType.SurfaceRover);
            }
        }

        private static string GetRoomDutyStepKey(SessionSnapshot s)
        {
            bool locked = EvaLocked(s);
            switch (s.CurrentSol)
            {
                case 1: return "duty.room.sol1";
                case 2: return "duty.room.sol2";
                case 3: return locked ? "duty.room.sol3_indoor" : "duty.room.sol3_eva";
                case 4: return "duty.room.sol4";
                case 5: return locked ? "duty.room.sol5_indoor" : "duty.room.sol5_eva";
                case 6: return "duty.room.sol6";
                default: return locked ? "duty.room.sol7_indoor" : "duty.room.sol7_eva";
            }
        }

        private static TerminalAppId GetOpsTargetApp(SessionSnapshot s)
        {
            if (s.CurrentSol == 1) return TerminalAppId.PowerGrid;
            if (s.PanelDustPct > 35f && !EvaLocked(s)) return TerminalAppId.PowerGrid;
            if (s.ScrubberHealthPct < 80f || s.Water < 45f || s.Oxygen < 45f) return TerminalAppId.LifeSupport;
            if (s.Shielding < 50f) return TerminalAppId.Radiation;
            return TerminalAppId.Crew;
        }

        private static TerminalAppId GetFallbackTerminalApp(int sol)
        {
            switch (sol)
            {
                case 1: return TerminalAppId.Crew;
                case 2: return TerminalAppId.LifeSupport;
                case 3: return TerminalAppId.Radiation;
                case 4: return TerminalAppId.PowerGrid;
                case 5: return TerminalAppId.Comms;
                case 6: return TerminalAppId.Greenhouse;
                default: return TerminalAppId.Sources;
            }
        }
    }
}
