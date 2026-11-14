using System;
using System.Collections.Generic;

namespace Outpost.Sim
{
    /// <summary>
    /// Pure C# Mars outpost simulation engine (no UnityEngine references).
    /// Handles 7-sol mission loop, power allocation, life support, radiation, crew actions, events, and grading.
    /// </summary>
    public class OutpostSimulation
    {
        public readonly SimConfig Config;
        private Random _rng;
        private List<SimEventDefinition> _eventDeck;

        public int Seed { get; private set; }
        public int CurrentSol { get; private set; }
        public int ActionPoints { get; private set; }
        public MissionStatus Status { get; private set; }
        public MissionGrade Grade { get; private set; }
        public float FinalScore { get; private set; }

        // Stocks (0 - 100)
        public float Power { get; private set; }
        public float Oxygen { get; private set; }
        public float Water { get; private set; }
        public float Food { get; private set; }
        public float Shielding { get; private set; }

        // Environment & Subsystems
        public float PanelDust { get; private set; }
        public float ScrubberHealth { get; private set; }
        public float RecyclerHealth { get; private set; }
        public float CropHealth { get; private set; }
        public int DustStormSolsRemaining { get; private set; }
        public bool SolarFlareActiveThisSol { get; private set; }
        public bool CrewShelteredThisSol { get; private set; }
        public bool CrewPartialShelteredThisSol { get; private set; }
        public float StormDustFactorThisSol { get; private set; } = 1f;
        public bool RationingActiveThisSol { get; private set; }
        public bool LastSolBlackout { get; private set; }

        public PowerAllocation Allocation { get; private set; }
        public List<CrewMember> Crew { get; private set; } = new List<CrewMember>();

        public SimEventDefinition CurrentEvent { get; private set; }
        public bool CurrentEventResolved { get; private set; }
        public string LastEventOutcomeText { get; private set; }
        public List<DebriefCard> DebriefCards { get; private set; } = new List<DebriefCard>();
        public List<string> SolLog { get; private set; } = new List<string>();

        // Last completed sol summary tracking (for UI transition card)
        public int LastCompletedSol { get; private set; }
        public float LastSolPowerDelta { get; private set; }
        public float LastSolOxygenDelta { get; private set; }
        public float LastSolWaterDelta { get; private set; }
        public float LastSolFoodDelta { get; private set; }
        public string LastSolEventSummary { get; private set; } = string.Empty;

        public int LivingCrewCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Crew.Count; i++)
                {
                    if (Crew[i].IsAlive) count++;
                }
                return count;
            }
        }

        public float StormFactor => DustStormSolsRemaining > 0 ? Config.StormSolarFactor : 1f;

        public OutpostSimulation(SimConfig config = null)
        {
            Config = config ?? new SimConfig();
            StartNewMission(42);
        }

        public void StartNewMission(int seed)
        {
            Seed = seed;
            _rng = new Random(seed);
            CurrentSol = 1;
            ActionPoints = Config.ActionPointsPerSol;
            Status = MissionStatus.InProgress;
            Grade = MissionGrade.F;
            FinalScore = 0f;

            Power = SimMath.Clamp100(Config.StartPower);
            Oxygen = SimMath.Clamp100(Config.StartOxygen);
            Water = SimMath.Clamp100(Config.StartWater);
            Food = SimMath.Clamp100(Config.StartFood);
            Shielding = SimMath.Clamp100(Config.StartShielding);

            PanelDust = SimMath.Clamp(Config.StartPanelDust, 0f, Config.MaxPanelDust);
            ScrubberHealth = 1.0f;
            RecyclerHealth = 1.0f;
            CropHealth = 1.0f;
            DustStormSolsRemaining = 0;
            LastSolBlackout = false;
            LastCompletedSol = 0;
            LastSolPowerDelta = 0f;
            LastSolOxygenDelta = 0f;
            LastSolWaterDelta = 0f;
            LastSolFoodDelta = 0f;
            LastSolEventSummary = string.Empty;

            Allocation = PowerAllocation.Default;

            Crew = new List<CrewMember>
            {
                new CrewMember { Id = 0, NameKey = "crew.medic.name", Role = CrewRole.Medic, Health = Config.StartCrewHealth, Morale = Config.StartCrewMorale, CumulativeDoseMsv = 0f, IsFallenCommander = false },
                new CrewMember { Id = 1, NameKey = "crew.engineer.name", Role = CrewRole.Engineer, Health = Config.StartCrewHealth, Morale = Config.StartCrewMorale, CumulativeDoseMsv = 0f, IsFallenCommander = false },
                new CrewMember { Id = 2, NameKey = "crew.pilot.name", Role = CrewRole.Pilot, Health = Config.StartFallenCommanderHealth, Morale = Config.StartCrewMorale, CumulativeDoseMsv = 0f, IsFallenCommander = true },
                new CrewMember { Id = 3, NameKey = "crew.scientist.name", Role = CrewRole.Scientist, Health = Config.StartCrewHealth, Morale = Config.StartCrewMorale, CumulativeDoseMsv = 0f, IsFallenCommander = false }
            };

            _eventDeck = EventCatalog.BuildMissionDeck(_rng, Config.TotalSols);
            DebriefCards.Clear();
            SolLog.Clear();
            SolLog.Add($"[SOL 1] Mission started (Seed {seed}). Rescue lands at end of Sol {Config.TotalSols}.");
            BeginSolState();
        }

        private void BeginSolState()
        {
            ActionPoints = Config.ActionPointsPerSol;
            SolarFlareActiveThisSol = false;
            CrewShelteredThisSol = false;
            CrewPartialShelteredThisSol = false;
            StormDustFactorThisSol = 1f;
            RationingActiveThisSol = false;
            CurrentEventResolved = false;
            LastEventOutcomeText = string.Empty;

            int idx = CurrentSol - 1;
            CurrentEvent = (idx >= 0 && idx < _eventDeck.Count) ? _eventDeck[idx] : null;
            if (CurrentEvent != null)
            {
                if (CurrentEvent.OnTriggerShieldingDelta != 0f)
                {
                    Shielding = SimMath.Clamp100(Shielding + CurrentEvent.OnTriggerShieldingDelta);
                    SolLog.Add($"[SOL {CurrentSol}] Event impact: Shielding {CurrentEvent.OnTriggerShieldingDelta:+0;-0}% -> {Shielding:0}%.");
                }
                if (CurrentEvent.StartsDustStorm && DustStormSolsRemaining <= 0)
                {
                    DustStormSolsRemaining = 2;
                }
                if (CurrentEvent.StartsSolarFlare)
                {
                    SolarFlareActiveThisSol = true;
                }
            }
        }

        public void SetAllocations(float lifeSupport, float greenhouse, float heating, float comms)
        {
            Allocation = new PowerAllocation
            {
                LifeSupport = lifeSupport,
                Greenhouse = greenhouse,
                Heating = heating,
                Comms = comms
            }.Clamped();
        }

        public bool HasLivingRole(CrewRole role)
        {
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].IsAlive && Crew[i].Role == role) return true;
            }
            return false;
        }

        public bool CanPerformAction(CrewActionType action, int targetCrewId = -1)
        {
            if (Status != MissionStatus.InProgress || ActionPoints <= 0) return false;
            if (LivingCrewCount == 0) return false;
            if (action == CrewActionType.ReinforceShielding && Power < Config.ReinforceShieldingPowerCost) return false;
            if (action == CrewActionType.TreatCrew && targetCrewId >= 0)
            {
                var target = GetCrew(targetCrewId);
                if (target == null || !target.IsAlive) return false;
            }
            return true;
        }

        public bool PerformAction(CrewActionType action, int targetCrewId = -1)
        {
            if (!CanPerformAction(action, targetCrewId)) return false;

            ActionPoints--;
            switch (action)
            {
                case CrewActionType.CleanPanels:
                    PanelDust = SimMath.Clamp(PanelDust - Config.CleanPanelsDustReduction, 0f, Config.MaxPanelDust);
                    AddDoseToWorkingCrew(Config.EvaCleanDoseMsv);
                    SolLog.Add($"[SOL {CurrentSol}] Action: Cleaned solar panels (-{Config.CleanPanelsDustReduction * 100f:0}% dust, +{Config.EvaCleanDoseMsv:0.00} mSv EVA dose).");
                    break;

                case CrewActionType.RepairSystem:
                    float repairAmt = Config.RepairHealthRestore + (HasLivingRole(CrewRole.Engineer) ? Config.EngineerRepairBonus : 0f);
                    SystemType lowest = GetLowestHealthSystem();
                    ApplySystemRepair(lowest, repairAmt);
                    SolLog.Add($"[SOL {CurrentSol}] Action: Repaired {lowest} (+{repairAmt * 100f:0}% health).");
                    break;

                case CrewActionType.ReinforceShielding:
                    Power = SimMath.Clamp100(Power - Config.ReinforceShieldingPowerCost);
                    Shielding = SimMath.Clamp100(Shielding + Config.ReinforceShieldingGain);
                    SolLog.Add($"[SOL {CurrentSol}] Action: Reinforced regolith shielding (+{Config.ReinforceShieldingGain:0}%, -{Config.ReinforceShieldingPowerCost:0} Power).");
                    break;

                case CrewActionType.RestMeal:
                    ModifyAllLivingCrew(Config.RestMealHealthGain, Config.RestMealMoraleGain);
                    SolLog.Add($"[SOL {CurrentSol}] Action: Crew rest & hot meal (+{Config.RestMealMoraleGain:0} Morale, +{Config.RestMealHealthGain:0} HP).");
                    break;

                case CrewActionType.TreatCrew:
                    CrewMember target = targetCrewId >= 0 ? GetCrew(targetCrewId) : GetLowestHealthLivingCrew();
                    if (target != null && target.IsAlive)
                    {
                        float heal = Config.TreatCrewHealthGain + (HasLivingRole(CrewRole.Medic) ? Config.MedicTreatBonus : 0f);
                        target.Health = SimMath.Clamp100(target.Health + heal);
                        SolLog.Add($"[SOL {CurrentSol}] Action: Treated {Localization.Get(target.NameKey)} (+{heal:0} HP -> {target.Health:0}).");
                    }
                    break;

                case CrewActionType.RationSupplies:
                    RationingActiveThisSol = true;
                    ModifyAllLivingCrew(0f, -Config.RationMoraleCost);
                    SolLog.Add($"[SOL {CurrentSol}] Action: Rationed food & water (-25% usage this sol, -{Config.RationMoraleCost:0} Morale).");
                    break;
            }
            return true;
        }

        public bool CanChooseEventChoice(int choiceIndex)
        {
            if (Status != MissionStatus.InProgress || CurrentEvent == null || CurrentEventResolved) return false;
            if (choiceIndex < 0 || choiceIndex >= CurrentEvent.Choices.Count) return false;
            var choice = CurrentEvent.Choices[choiceIndex];
            if (choice.ApCost > ActionPoints) return false;
            return true;
        }

        public bool ResolveEventChoice(int choiceIndex)
        {
            if (!CanChooseEventChoice(choiceIndex)) return false;
            var choice = CurrentEvent.Choices[choiceIndex];
            ActionPoints -= choice.ApCost;

            Power = SimMath.Clamp100(Power + choice.PowerDelta);
            Oxygen = SimMath.Clamp100(Oxygen + choice.OxygenDelta);
            Water = SimMath.Clamp100(Water + choice.WaterDelta);
            Food = SimMath.Clamp100(Food + choice.FoodDelta);
            Shielding = SimMath.Clamp100(Shielding + choice.ShieldingDelta);
            PanelDust = SimMath.Clamp(PanelDust + choice.DustDelta, 0f, Config.MaxPanelDust);

            ScrubberHealth = SimMath.Clamp(ScrubberHealth + choice.ScrubberHealthDelta, Config.MinSystemHealth, 1f);
            RecyclerHealth = SimMath.Clamp(RecyclerHealth + choice.RecyclerHealthDelta, Config.MinSystemHealth, 1f);
            CropHealth = SimMath.Clamp(CropHealth + choice.CropHealthDelta, Config.MinSystemHealth, 1f);

            ModifyAllLivingCrew(choice.CrewHealthDelta, choice.CrewMoraleDelta);

            if (choice.CommanderHealthDelta != 0f)
            {
                var cmdr = GetCrew(2);
                if (cmdr != null && cmdr.IsAlive)
                    cmdr.Health = SimMath.Clamp100(cmdr.Health + choice.CommanderHealthDelta);
            }

            if (choice.EvaDoseMsv > 0f)
                AddDoseToWorkingCrew(choice.EvaDoseMsv);
            if (choice.ExtraDoseMsv > 0f)
                AddDoseToAllLivingCrew(choice.ExtraDoseMsv);

            if (choice.TriggerDustStorm && DustStormSolsRemaining <= 0)
                DustStormSolsRemaining = 2;
            if (choice.StormDustFactor < 1f)
                StormDustFactorThisSol = choice.StormDustFactor;
            if (choice.TriggerSolarFlare)
                SolarFlareActiveThisSol = true;
            if (choice.ShelterSolarFlare)
                CrewShelteredThisSol = true;
            if (choice.PartialShelterSolarFlare)
                CrewPartialShelteredThisSol = true;
            if (choice.ApplyRationThisSol)
                RationingActiveThisSol = true;

            string outcome = Localization.Get(choice.OutcomeKey);

            if (choice.IsEarthTelemetryPatch)
            {
                // Comms allocation or luck determines whether Houston's delayed patch helps or miscalibrates.
                float successChance = Allocation.Comms >= 0.5f ? 0.72f : 0.45f;
                if (_rng.NextDouble() < successChance)
                {
                    Power = SimMath.Clamp100(Power + 12f);
                    ScrubberHealth = SimMath.Clamp(ScrubberHealth + 0.15f, Config.MinSystemHealth, 1f);
                    outcome += " (+12 Power, +15% Scrubber)";
                }
                else
                {
                    Power = SimMath.Clamp100(Power - 8f);
                    outcome += " (Delayed telemetry glitch: -8 Power)";
                }
            }

            LastEventOutcomeText = outcome;
            CurrentEventResolved = true;

            DebriefCards.Add(new DebriefCard
            {
                Sol = CurrentSol,
                EventId = CurrentEvent.Id,
                TitleKey = CurrentEvent.TitleKey,
                ChoiceLabelKey = choice.LabelKey,
                OutcomeKey = choice.OutcomeKey,
                FactKey = CurrentEvent.FactKey,
                SourceId = CurrentEvent.SourceId
            });

            SolLog.Add($"[SOL {CurrentSol}] Event '{Localization.Get(CurrentEvent.TitleKey)}': {outcome}");
            CheckMissionEnd();
            return true;
        }

        public float ComputeSolarGeneration()
        {
            return Config.BaseSolarGeneration * (1f - PanelDust) * StormFactor;
        }

        public float ComputeTotalPowerDemand()
        {
            float raw = Allocation.LifeSupport * Config.LifeSupportMaxPower
                      + Allocation.Greenhouse * Config.GreenhouseMaxPower
                      + Allocation.Heating * Config.HeatingMaxPower
                      + Allocation.Comms * Config.CommsMaxPower;
            if (HasLivingRole(CrewRole.Engineer))
                raw = Math.Max(0f, raw - Config.EngineerPowerSaved);
            return raw;
        }

        public SolForecast GetForecast()
        {
            float gen = ComputeSolarGeneration();
            float demand = ComputeTotalPowerDemand();
            float netPower = gen - demand;
            float projectedPower = Power + netPower;
            bool willBlackout = projectedPower <= 0f;
            float eff = willBlackout ? Config.ZeroBatteryEffectiveness : 1f;

            int living = LivingCrewCount;
            float ghMultiplier = HasLivingRole(CrewRole.Scientist) ? (1f + Config.ScientistGreenhouseBonus) : 1f;
            float shelterGhMult = CrewShelteredThisSol ? 0f : 1f;
            float rationMult = RationingActiveThisSol ? Config.RationConsumptionMultiplier : 1f;

            float o2Gen = Config.OxygenGenAtFull * Allocation.LifeSupport * ScrubberHealth * eff
                        + Config.GreenhouseOxygenBonus * Allocation.Greenhouse * CropHealth * eff * ghMultiplier * shelterGhMult;
            float o2Use = Config.OxygenUsePerCrew * living;

            float waterGen = Config.WaterGenAtFull * Allocation.LifeSupport * RecyclerHealth * eff;
            float waterUse = (Config.WaterUsePerCrew * living) * rationMult + Config.WaterFixedLoss;

            float foodGen = Config.FoodGenAtFull * Allocation.Greenhouse * CropHealth * eff * ghMultiplier * shelterGhMult;
            float foodUse = (Config.FoodUsePerCrew * living) * rationMult;

            float shieldingFactor = 1f - Config.ShieldingMaxReduction * (Shielding / 100f);
            float baseDose = Config.BaselineSurfaceDoseMsvPerSol * shieldingFactor;
            float flareDose = 0f;
            if (SolarFlareActiveThisSol)
            {
                if (CrewShelteredThisSol) flareDose = Config.SolarFlareUnshieldedDoseMsv * Config.SolarFlareShelterMultiplier;
                else if (CrewPartialShelteredThisSol) flareDose = Config.SolarFlareUnshieldedDoseMsv * 0.35f;
                else flareDose = Config.SolarFlareUnshieldedDoseMsv * shieldingFactor;
            }

            float effectiveHeating = Allocation.Heating * eff;
            bool isCold = effectiveHeating < Config.HeatingSafeThreshold;

            var forecast = new SolForecast
            {
                PredictedGeneration = gen,
                PredictedDemand = demand,
                NetPowerDelta = netPower,
                PredictedBatteryAfter = SimMath.Clamp100(projectedPower),
                WillBlackout = willBlackout,
                NetOxygenDelta = o2Gen - o2Use,
                NetWaterDelta = waterGen - waterUse,
                NetFoodDelta = foodGen - foodUse,
                PredictedDoseMsv = baseDose + flareDose,
                IsHeatingCold = isCold
            };

            AssignMostUrgentAlert(forecast);
            return forecast;
        }

        private void AssignMostUrgentAlert(SolForecast forecast)
        {
            if (Oxygen <= 0f) { forecast.UrgentAlertKey = "alert.o2_zero"; forecast.UrgentSeverity = 2; return; }
            if (Water <= 0f) { forecast.UrgentAlertKey = "alert.water_zero"; forecast.UrgentSeverity = 2; return; }
            if (Food <= 0f) { forecast.UrgentAlertKey = "alert.food_zero"; forecast.UrgentSeverity = 2; return; }
            if (forecast.WillBlackout) { forecast.UrgentAlertKey = "alert.blackout"; forecast.UrgentSeverity = 2; return; }
            if (SolarFlareActiveThisSol && !CrewShelteredThisSol) { forecast.UrgentAlertKey = "alert.flare"; forecast.UrgentSeverity = 2; return; }
            if (Oxygen + forecast.NetOxygenDelta < 18f) { forecast.UrgentAlertKey = "alert.o2_low"; forecast.UrgentSeverity = 2; return; }
            if (ScrubberHealth < Config.LowScrubberThreshold) { forecast.UrgentAlertKey = "alert.scrubber"; forecast.UrgentSeverity = 2; return; }
            if (forecast.IsHeatingCold) { forecast.UrgentAlertKey = "alert.cold"; forecast.UrgentSeverity = 1; return; }
            if (Water + forecast.NetWaterDelta < 18f) { forecast.UrgentAlertKey = "alert.water_low"; forecast.UrgentSeverity = 1; return; }
            if (DustStormSolsRemaining > 0) { forecast.UrgentAlertKey = "alert.storm"; forecast.UrgentSeverity = 1; return; }

            forecast.UrgentAlertKey = "alert.nominal";
            forecast.UrgentSeverity = 0;
        }

        public void EndSol()
        {
            if (Status != MissionStatus.InProgress) return;

            // If player did not click an event choice before ending sol, auto-pick the last (free) option.
            if (CurrentEvent != null && !CurrentEventResolved)
            {
                ResolveEventChoice(CurrentEvent.Choices.Count - 1);
                if (Status != MissionStatus.InProgress) return;
            }

            var forecast = GetForecast();
            float prevPower = Power;
            float prevO2 = Oxygen;
            float prevWater = Water;
            float prevFood = Food;

            // 1. Power balance & blackout check
            Power = forecast.PredictedBatteryAfter;
            LastSolBlackout = forecast.WillBlackout;
            if (LastSolBlackout)
            {
                SolLog.Add($"[SOL {CurrentSol}] BLACKOUT! Battery hit 0 — systems ran at {Config.ZeroBatteryEffectiveness * 100f:0}% effectiveness.");
            }

            // 2. Stocks update
            Oxygen = SimMath.Clamp100(Oxygen + forecast.NetOxygenDelta);
            Water = SimMath.Clamp100(Water + forecast.NetWaterDelta);
            Food = SimMath.Clamp100(Food + forecast.NetFoodDelta);

            // 3. Radiation dose accumulation
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].IsAlive)
                    Crew[i].CumulativeDoseMsv += forecast.PredictedDoseMsv;
            }

            // 4. Heating & equipment failure check
            float failureChance = Config.BaseFailureChance + (forecast.IsHeatingCold ? Config.ColdFailureChanceBonus : 0f);
            if (LastSolBlackout) failureChance += 0.25f;

            if (_rng.NextDouble() < failureChance)
            {
                int roll = _rng.Next(3);
                if (roll == 0)
                {
                    ScrubberHealth = SimMath.Clamp(ScrubberHealth - Config.FailureSystemDamage, Config.MinSystemHealth, 1f);
                    Oxygen = SimMath.Clamp100(Oxygen - Config.FailureOxygenLeak);
                    SolLog.Add($"[SOL {CurrentSol}] FAILURE: CO2 Scrubber seal failed (-{Config.FailureSystemDamage * 100f:0}% health, -{Config.FailureOxygenLeak:0} O2).");
                }
                else if (roll == 1)
                {
                    RecyclerHealth = SimMath.Clamp(RecyclerHealth - Config.FailureSystemDamage, Config.MinSystemHealth, 1f);
                    Water = SimMath.Clamp100(Water - 6f);
                    SolLog.Add($"[SOL {CurrentSol}] FAILURE: Water Recycler pump jammed (-{Config.FailureSystemDamage * 100f:0}% health, -6 Water).");
                }
                else
                {
                    CropHealth = SimMath.Clamp(CropHealth - Config.FailureSystemDamage, Config.MinSystemHealth, 1f);
                    SolLog.Add($"[SOL {CurrentSol}] FAILURE: Greenhouse thermal valve stuck (-{Config.FailureSystemDamage * 100f:0}% crop health).");
                }
            }

            // 5. Crew health & morale updates
            float pilotMoraleBonus = HasLivingRole(CrewRole.Pilot) ? Config.PilotMoraleStabilityBonus : 0f;
            for (int i = 0; i < Crew.Count; i++)
            {
                var c = Crew[i];
                if (!c.IsAlive) continue;

                float healthDelta = 0f;
                float moraleDelta = pilotMoraleBonus;

                if (forecast.IsHeatingCold)
                {
                    healthDelta -= Config.ColdHealthPenalty;
                    moraleDelta -= Config.ColdMoralePenalty;
                }

                if (Oxygen <= 0f) healthDelta -= Config.ZeroOxygenHealthPenalty;
                if (Water <= 0f) healthDelta -= Config.ZeroWaterHealthPenalty;
                if (Food <= 0f) healthDelta -= Config.ZeroFoodHealthPenalty;

                if (ScrubberHealth < Config.LowScrubberThreshold)
                    healthDelta -= Config.HighCo2HealthPenalty;

                if (c.CumulativeDoseMsv > Config.DoseHealthThresholdMsv)
                {
                    float excess = c.CumulativeDoseMsv - Config.DoseHealthThresholdMsv;
                    healthDelta -= excess * Config.DoseHealthPenaltyFactor;
                }

                if (c.Morale <= Config.LowMoraleThreshold)
                    healthDelta -= Config.LowMoraleHealthPenalty;

                c.Health = SimMath.Clamp100(c.Health + healthDelta);
                c.Morale = SimMath.Clamp100(c.Morale + moraleDelta);

                if (!c.IsAlive)
                {
                    SolLog.Add($"[SOL {CurrentSol}] CREW LOST: {Localization.Get(c.NameKey)} succumbed to harsh outpost conditions.");
                }
            }

            // 6. Panel dust accumulation & storm countdown
            float dustGain = Config.DustAccumulationPerSol;
            if (DustStormSolsRemaining > 0)
            {
                dustGain += Config.StormExtraDustPerSol * StormDustFactorThisSol;
                DustStormSolsRemaining--;
            }
            PanelDust = SimMath.Clamp(PanelDust + dustGain, 0f, Config.MaxPanelDust);

            SolLog.Add($"[SOL {CurrentSol}] End Summary: Pwr {Power:0} | O2 {Oxygen:0} | H2O {Water:0} | Food {Food:0} | Crew {LivingCrewCount}/4.");

            LastCompletedSol = CurrentSol;
            LastSolPowerDelta = Power - prevPower;
            LastSolOxygenDelta = Oxygen - prevO2;
            LastSolWaterDelta = Water - prevWater;
            LastSolFoodDelta = Food - prevFood;
            LastSolEventSummary = string.IsNullOrEmpty(LastEventOutcomeText) ? "Sol completed." : LastEventOutcomeText;

            // 7. Win / Lose resolution
            CheckMissionEnd();
            if (Status != MissionStatus.InProgress) return;

            if (CurrentSol >= Config.TotalSols)
            {
                FinishMissionWon();
                return;
            }

            CurrentSol++;
            BeginSolState();
        }

        private void CheckMissionEnd()
        {
            if (LivingCrewCount <= 0)
            {
                Status = MissionStatus.Lost;
                Grade = MissionGrade.F;
                FinalScore = 0f;
                SolLog.Add($"[SOL {CurrentSol}] MISSION FAILED: All crew lost.");
            }
        }

        private void FinishMissionWon()
        {
            Status = MissionStatus.Won;

            float totalHealth = 0f;
            int alive = 0;
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].IsAlive)
                {
                    alive++;
                    totalHealth += Crew[i].Health;
                }
            }

            float avgHealth = alive > 0 ? totalHealth / alive : 0f;
            float avgStocks = (Power + Oxygen + Water + Food + Shielding) / 5f;
            float crewScore = (alive / 4f) * 50f;
            float healthScore = (avgHealth / 100f) * 30f;
            float stockScore = (avgStocks / 100f) * 20f;
            FinalScore = crewScore + healthScore + stockScore;

            if (alive == 4 && FinalScore >= Config.GradeSMinScore) Grade = MissionGrade.S;
            else if (FinalScore >= Config.GradeAMinScore) Grade = MissionGrade.A;
            else if (FinalScore >= Config.GradeBMinScore) Grade = MissionGrade.B;
            else Grade = MissionGrade.C;

            SolLog.Add($"[SOL {CurrentSol}] RESCUE LANDED! Survived: {alive}/4 | Avg HP: {avgHealth:0} | Grade: {Grade} ({FinalScore:0.0}).");
        }

        private SystemType GetLowestHealthSystem()
        {
            if (ScrubberHealth <= RecyclerHealth && ScrubberHealth <= CropHealth) return SystemType.Scrubber;
            if (RecyclerHealth <= CropHealth) return SystemType.Recycler;
            return SystemType.Crops;
        }

        private void ApplySystemRepair(SystemType sys, float amount)
        {
            switch (sys)
            {
                case SystemType.Scrubber:
                    ScrubberHealth = SimMath.Clamp(ScrubberHealth + amount, Config.MinSystemHealth, 1f);
                    break;
                case SystemType.Recycler:
                    RecyclerHealth = SimMath.Clamp(RecyclerHealth + amount, Config.MinSystemHealth, 1f);
                    break;
                case SystemType.Crops:
                    CropHealth = SimMath.Clamp(CropHealth + amount, Config.MinSystemHealth, 1f);
                    break;
            }
        }

        private CrewMember GetCrew(int id)
        {
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].Id == id) return Crew[i];
            }
            return null;
        }

        public CrewMember GetLowestHealthLivingCrew()
        {
            CrewMember best = null;
            for (int i = 0; i < Crew.Count; i++)
            {
                var c = Crew[i];
                if (!c.IsAlive) continue;
                if (best == null || c.Health < best.Health) best = c;
            }
            return best;
        }

        private void ModifyAllLivingCrew(float healthDelta, float moraleDelta)
        {
            for (int i = 0; i < Crew.Count; i++)
            {
                var c = Crew[i];
                if (!c.IsAlive) continue;
                c.Health = SimMath.Clamp100(c.Health + healthDelta);
                c.Morale = SimMath.Clamp100(c.Morale + moraleDelta);
            }
        }

        private void AddDoseToWorkingCrew(float doseMsv)
        {
            float shieldingFactor = 1f - Config.ShieldingMaxReduction * (Shielding / 100f);
            float adjusted = doseMsv * shieldingFactor;
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].CanWork) Crew[i].CumulativeDoseMsv += adjusted;
            }
        }

        private void AddDoseToAllLivingCrew(float doseMsv)
        {
            for (int i = 0; i < Crew.Count; i++)
            {
                if (Crew[i].IsAlive) Crew[i].CumulativeDoseMsv += doseMsv;
            }
        }

        // --- DEVELOPER DEBUG HELPERS (NO EFFECT ON NORMAL GAMEPLAY) ---
        public void DebugSetStocks(float power, float oxygen, float water, float food, float shielding)
        {
            Power = SimMath.Clamp100(power);
            Oxygen = SimMath.Clamp100(oxygen);
            Water = SimMath.Clamp100(water);
            Food = SimMath.Clamp100(food);
            Shielding = SimMath.Clamp100(shielding);
        }

        public void DebugSetStorm(bool active)
        {
            DustStormSolsRemaining = active ? 2 : 0;
        }

        public void DebugSetBattery(float power)
        {
            Power = SimMath.Clamp100(power);
        }

        public void DebugSetSol(int targetSol)
        {
            int clamped = (int)SimMath.Clamp(targetSol, 1, Config.TotalSols);
            CurrentSol = clamped;
            Status = MissionStatus.InProgress;
            Grade = MissionGrade.F;
            FinalScore = 0f;
            BeginSolState();
        }

        public bool DebugTriggerEvent(string eventId)
        {
            if (Status != MissionStatus.InProgress) return false;
            if (!EventCatalog.EventsById.TryGetValue(eventId, out var evt)) return false;

            CurrentEvent = evt;
            CurrentEventResolved = false;
            LastEventOutcomeText = string.Empty;
            if (evt.OnTriggerShieldingDelta != 0f)
                Shielding = SimMath.Clamp100(Shielding + evt.OnTriggerShieldingDelta);
            if (evt.StartsDustStorm && DustStormSolsRemaining <= 0)
                DustStormSolsRemaining = 2;
            if (evt.StartsSolarFlare)
                SolarFlareActiveThisSol = true;
            return true;
        }

        public void DebugForceOutcome(bool win)
        {
            if (win)
            {
                FinishMissionWon();
            }
            else
            {
                for (int i = 0; i < Crew.Count; i++)
                    Crew[i].Health = 0f;
                CheckMissionEnd();
            }
        }
    }
}
