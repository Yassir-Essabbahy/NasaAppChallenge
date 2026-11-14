using Outpost.Sim;

namespace Outpost.Runtime.Terminal
{
    public struct HintEvaluation
    {
        public string HeliosHintText;
        public string NextStepText;
        public string NextStepButtonText;
        public TerminalAppId? TargetApp;
        public bool IsUrgent;
    }

    /// <summary>
    /// Reads SessionSnapshot and selects one prioritized hint (crew in danger > resource hitting zero within
    /// 2 sols > active event > unused action points > idle tip) without choosing for the player.
    /// All returned strings come from Localization and HELIOS tips stay <= 12 words.
    /// </summary>
    public static class HintEngine
    {
        public static HintEvaluation Evaluate(SessionSnapshot s)
        {
            if (s == null)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.idle_ok"),
                    NextStepText = Localization.Get("next.ready"),
                    NextStepButtonText = Localization.Get("next.endsol_btn"),
                    TargetApp = null,
                    IsUrgent = false
                };
            }

            // 1. Crew in danger (highest priority)
            bool crewInDanger = false;
            for (int i = 0; i < s.Crew.Count; i++)
            {
                var c = s.Crew[i];
                if (c.IsAlive && (c.Health <= 35f || c.Morale <= 20f))
                {
                    crewInDanger = true;
                    break;
                }
            }
            if (crewInDanger)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.crew_danger"),
                    NextStepText = Localization.Get("next.crew_danger"),
                    NextStepButtonText = Localization.Get("next.crew_btn"),
                    TargetApp = TerminalAppId.Crew,
                    IsUrgent = true
                };
            }

            // 2. Any resource will hit zero within 2 sols (or Heating < 50% / Shield critical)
            if (s.WillBlackout || s.Power <= 0f || (s.NetPowerDelta < 0f && s.Power + 2f * s.NetPowerDelta <= 0f))
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.power_crit"),
                    NextStepText = Localization.Get("next.power_crit"),
                    NextStepButtonText = Localization.Get("next.power_btn"),
                    TargetApp = TerminalAppId.PowerGrid,
                    IsUrgent = true
                };
            }

            if (s.Oxygen <= 0f || (s.NetOxygenDelta < 0f && s.Oxygen + 2f * s.NetOxygenDelta <= 0f) || s.ScrubberHealthPct < 45f)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.o2_crit"),
                    NextStepText = Localization.Get("next.o2_crit"),
                    NextStepButtonText = Localization.Get("next.ls_btn"),
                    TargetApp = TerminalAppId.LifeSupport,
                    IsUrgent = true
                };
            }

            if (s.Water <= 0f || (s.NetWaterDelta < 0f && s.Water + 2f * s.NetWaterDelta <= 0f))
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.water_crit"),
                    NextStepText = Localization.Get("next.water_crit"),
                    NextStepButtonText = Localization.Get("next.ls_btn"),
                    TargetApp = TerminalAppId.LifeSupport,
                    IsUrgent = true
                };
            }

            if (s.Food <= 0f || (s.NetFoodDelta < 0f && s.Food + 2f * s.NetFoodDelta <= 0f))
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.food_crit"),
                    NextStepText = Localization.Get("next.food_crit"),
                    NextStepButtonText = Localization.Get("next.gh_btn"),
                    TargetApp = TerminalAppId.Greenhouse,
                    IsUrgent = true
                };
            }

            if (s.IsHeatingCold)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.cold_warn"),
                    NextStepText = Localization.Get("next.power_crit"),
                    NextStepButtonText = Localization.Get("next.power_btn"),
                    TargetApp = TerminalAppId.PowerGrid,
                    IsUrgent = true
                };
            }

            if (s.SolarFlareActive && s.Shielding < 45f)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.shield_crit"),
                    NextStepText = Localization.Get("next.shield_crit"),
                    NextStepButtonText = Localization.Get("next.rad_btn"),
                    TargetApp = TerminalAppId.Radiation,
                    IsUrgent = true
                };
            }

            // 3. Active unresolved event
            if (s.ActiveEvent != null && !s.ActiveEvent.IsResolved)
            {
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.event_active"),
                    NextStepText = Localization.Get("next.event_active"),
                    NextStepButtonText = Localization.Get("next.event_btn"),
                    TargetApp = null,
                    IsUrgent = true
                };
            }

            // 4. Unused Action Points
            if (s.ActionPoints > 0 && s.Status == MissionStatus.InProgress)
            {
                TerminalAppId target = s.PanelDustPct > 25f ? TerminalAppId.PowerGrid : TerminalAppId.Crew;
                string btn = target == TerminalAppId.PowerGrid ? Localization.Get("next.power_btn") : Localization.Get("next.crew_btn");
                return new HintEvaluation
                {
                    HeliosHintText = Localization.Get("hint.ap_unused"),
                    NextStepText = Localization.Get("next.ap_unused"),
                    NextStepButtonText = btn,
                    TargetApp = target,
                    IsUrgent = false
                };
            }

            // 5. Idle tip
            return new HintEvaluation
            {
                HeliosHintText = Localization.Get("hint.idle_ok"),
                NextStepText = Localization.Get("next.ready"),
                NextStepButtonText = Localization.Get("next.endsol_btn"),
                TargetApp = null,
                IsUrgent = false
            };
        }
    }
}
