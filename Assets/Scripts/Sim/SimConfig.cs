using System;

namespace Outpost.Sim
{
    /// <summary>
    /// Single source of truth for all simulation balance constants.
    /// Every constant is tagged REAL, ADAPTED, or GAME to document scientific fidelity vs. gameplay tuning.
    /// </summary>
    [Serializable]
    public class SimConfig
    {
        // --- MISSION STRUCTURE ---
        public int TotalSols = 7;                        // GAME: 7-sol rescue window sized for a 10-15 minute play session.
        public int ActionPointsPerSol = 3;               // GAME: 3 crew action slots per sol to force trade-offs.

        // --- STARTING STOCKS (0-100 SCALE) ---
        public float StartPower = 80f;                   // GAME: Starting battery reserve (%).
        public float StartOxygen = 70f;                  // GAME: Starting breathable O2 reserve (%).
        public float StartWater = 70f;                   // GAME: Starting potable water reserve (%).
        public float StartFood = 60f;                    // GAME: Starting food supply (%).
        public float StartShielding = 75f;               // GAME: Starting habitat radiation/micrometeorite shielding (%).

        // --- STARTING CREW ---
        public float StartCrewHealth = 100f;             // GAME: Healthy starting crew HP.
        public float StartFallenCommanderHealth = 60f;   // GAME: Previous commander collapsed and starts in medbay at 60 HP.
        public float StartCrewMorale = 70f;              // GAME: Starting crew morale.

        // --- SYSTEM POWER DEMAND AT 100% ALLOCATION ---
        public float LifeSupportMaxPower = 20f;          // ADAPTED: ECLSS O2/CO2 scrubbing and water recovery draw heavy continuous power.
        public float GreenhouseMaxPower = 15f;           // ADAPTED: LED grow arrays and hydroponic pumps demand high power.
        public float HeatingMaxPower = 15f;              // ADAPTED: Mars averages -63C, making habitat thermal control power-intensive.
        public float CommsMaxPower = 5f;                 // ADAPTED: Deep-space transceiver array uses modest continuous power.

        // --- SOLAR GENERATION & BATTERY ---
        public float BaseSolarGeneration = 60f;          // GAME: Peak clean-array solar output per sol on Mars.
        public float StartPanelDust = 0.22f;             // GAME: Panels start 22% dusty after commander's collapse.
        public float DustAccumulationPerSol = 0.08f;     // ADAPTED: Martian electrostatic dust settles on solar panels (~8%/sol scaled for 7 sols).
        public float StormExtraDustPerSol = 0.25f;       // ADAPTED: Global/regional dust storms accelerate panel dust deposition.
        public float StormSolarFactor = 0.40f;           // ADAPTED: Elevated atmospheric optical depth (tau) cuts solar flux during dust storms.
        public float MaxPanelDust = 0.90f;               // ADAPTED: Residual ambient light even under heavy dust coating.
        public float ZeroBatteryEffectiveness = 0.20f;   // GAME: Emergency reserve allows 20% system operation when main battery hits 0.

        // --- OXYGEN ---
        public float OxygenGenAtFull = 12f;              // GAME: Nominal O2 generation from electrolysis/scrubbing at 100% allocation.
        public float GreenhouseOxygenBonus = 2.0f;       // ADAPTED: Photosynthetic crops supplement cabin O2 and scrub minor CO2.
        public float OxygenUsePerCrew = 2.5f;            // ADAPTED: Scaled from ~0.84 kg O2 consumed per astronaut per day.

        // --- WATER ---
        public float WaterGenAtFull = 10f;               // ADAPTED: Closed-loop ISS-heritage urine/humidity processor (~93-98% recovery).
        public float WaterUsePerCrew = 1.5f;             // ADAPTED: Scaled from ~2.5-3.0 L/person/day metabolic & hygiene usage.
        public float WaterFixedLoss = 3.5f;              // ADAPTED: Unrecoverable brine residue, EVA sublimator, and airlock humidity losses per sol.

        // --- FOOD ---
        public float FoodGenAtFull = 9f;                 // GAME: Hydroponic crop yield at 100% Greenhouse allocation.
        public float FoodUsePerCrew = 1.5f;              // ADAPTED: Scaled from ~0.62 kg dehydrated rations per crew per sol.
        public float RationConsumptionMultiplier = 0.75f;// GAME: Rationing reduces daily food & water consumption by 25%.
        public float RationMoraleCost = 8f;              // GAME: Caloric restriction lowers crew morale.

        // --- THERMAL & EQUIPMENT FAILURES ---
        public float HeatingSafeThreshold = 0.50f;       // GAME: Minimum effective heating allocation to prevent cold stress.
        public float ColdHealthPenalty = 5f;             // ADAPTED: Sustained cold exposure causes hypothermia risk and respiratory stress.
        public float ColdMoralePenalty = 5f;             // GAME: Freezing habitat conditions depress crew morale.
        public float BaseFailureChance = 0.22f;          // GAME: Baseline daily mechanical failure probability in abrasive Martian dust.
        public float ColdFailureChanceBonus = 0.40f;     // ADAPTED: Thermal cycling below safe temp stresses seals, valves, and pumps.
        public float FailureSystemDamage = 0.42f;        // GAME: System health loss when a mechanical failure occurs.
        public float FailureOxygenLeak = 12f;            // GAME: Cabin air/pressure loss when a life-support seal fails.
        public float LowMoraleThreshold = 20f;           // GAME: Fatigue/morale crisis threshold.
        public float LowMoraleHealthPenalty = 7f;        // GAME: Severe stress/exhaustion reduces crew resilience.

        // --- RADIATION (mSv) ---
        public float BaselineSurfaceDoseMsvPerSol = 0.67f; // ADAPTED: Curiosity RAD measured ~0.67 mSv/sol on Gale Crater surface (Hassler et al., 2014) - flag for verification.
        public float ShieldingMaxReduction = 0.50f;      // ADAPTED: Regolith bags & water walls attenuate up to 50% of ambient surface radiation.
        public float SolarFlareUnshieldedDoseMsv = 18.0f;// ADAPTED: Unshielded Solar Particle Event (SPE) acute radiation surge.
        public float SolarFlareShelterMultiplier = 0.10f;// ADAPTED: Storm shelter / inner core cuts acute SPE dose by ~90%.
        public float EvaCleanDoseMsv = 0.55f;            // ADAPTED: Extra radiation received outside habitat pressure shell during panel EVA.
        public float DoseHealthThresholdMsv = 3.5f;      // GAME: Scaled cumulative dose threshold where radiation sickness impacts mission health.
        public float DoseHealthPenaltyFactor = 3.4f;     // GAME: HP lost per sol per mSv above DoseHealthThresholdMsv.

        // --- STOCK DEPLETION & CRISIS PENALTIES ---
        public float ZeroOxygenHealthPenalty = 45f;      // REAL: Hypoxia/asphyxiation is rapidly fatal within minutes-hours if O2 fails.
        public float ZeroWaterHealthPenalty = 28f;       // REAL: Severe dehydration causes rapid organ failure within days.
        public float ZeroFoodHealthPenalty = 16f;        // ADAPTED: Starvation steadily degrades crew health and immune function.
        public float LowScrubberThreshold = 0.45f;       // ADAPTED: Below 45% scrubber health, CO2 buildup (hypercapnia) harms crew.
        public float HighCo2HealthPenalty = 12f;         // REAL: Elevated cabin CO2 causes headaches, cognitive impairment, and toxicity.
        public float MinSystemHealth = 0.15f;            // GAME: Damaged systems still limp along at 15% floor unless further broken.

        // --- PLAYER ACTIONS (1 AP EACH) ---
        public float CleanPanelsDustReduction = 0.40f;   // GAME: Dust removed from solar arrays by one EVA sweep.
        public float RepairHealthRestore = 0.45f;        // GAME: System health restored by one Repair action.
        public float ReinforceShieldingGain = 14f;       // GAME: Shielding integrity gained by stacking regolith bags.
        public float ReinforceShieldingPowerCost = 8f;   // GAME: Airlock cycling + rover/winch power required to haul regolith.
        public float RestMealMoraleGain = 15f;           // GAME: Morale gained by all living crew from shared rest & hot meal.
        public float RestMealHealthGain = 4f;            // GAME: Minor physical recovery from a proper rest cycle.
        public float TreatCrewHealthGain = 18f;          // GAME: Base HP restored to target crew member in medbay.
        public float MedicTreatBonus = 10f;              // GAME: Extra HP healed when the crew Medic is alive.

        // --- ROLE BONUSES ---
        public float EngineerRepairBonus = 0.20f;        // GAME: Living Engineer boosts repair amount by +20%.
        public float EngineerPowerSaved = 0f;            // GAME: Kept at 0 so displayed 20/15/15/5W power math matches UI directly.
        public float ScientistGreenhouseBonus = 0.15f;   // GAME: Living Scientist boosts Greenhouse food & O2 yield by +15%.
        public float PilotMoraleStabilityBonus = 2.0f;   // GAME: Living Pilot/Commander stabilizes crew morale (+2/sol).

        // --- GRADING THRESHOLDS ---
        public float GradeSMinScore = 88f;               // GAME: Composite score threshold for S grade.
        public float GradeAMinScore = 72f;               // GAME: Composite score threshold for A grade.
        public float GradeBMinScore = 54f;               // GAME: Composite score threshold for B grade.

        // --- ROOM STATUS LIGHT THRESHOLDS (M3) ---
        public float StatusLightOkThreshold = 40f;       // GAME: Resource >= 40 shows green (OK) on wall status panel.
        public float StatusLightLowThreshold = 20f;      // GAME: Resource 20..40 shows amber (LOW); < 20 shows red (CRITICAL).
    }
}
