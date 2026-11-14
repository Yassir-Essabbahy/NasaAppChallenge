using System;
using System.Text;

namespace Outpost.Sim
{
    public enum BotStrategy
    {
        Careless,
        Average,
        Optimal
    }

    public class BalanceReport
    {
        public BotStrategy Strategy;
        public int MissionsPlayed;
        public int Wins;
        public float WinRate => MissionsPlayed > 0 ? (float)Wins / MissionsPlayed : 0f;
        public float AverageCrewAlive;
        public float AverageLivingHealth;
    }

    /// <summary>
    /// Headless balance runner that simulates N seeded missions across Careless, Average, and Optimal strategies.
    /// </summary>
    public static class BalanceRunner
    {
        public static BalanceReport RunStrategy(BotStrategy strategy, int missionCount = 1000, SimConfig config = null)
        {
            var cfg = config ?? new SimConfig();
            int wins = 0;
            int totalAlive = 0;
            float totalHealth = 0f;

            for (int seed = 1; seed <= missionCount; seed++)
            {
                var sim = new OutpostSimulation(cfg);
                sim.StartNewMission(seed);

                while (sim.Status == MissionStatus.InProgress)
                {
                    PlaySol(sim, strategy, seed);
                    sim.EndSol();
                }

                if (sim.Status == MissionStatus.Won) wins++;

                int alive = sim.LivingCrewCount;
                totalAlive += alive;
                if (alive > 0)
                {
                    float sumHp = 0f;
                    for (int i = 0; i < sim.Crew.Count; i++)
                    {
                        if (sim.Crew[i].IsAlive) sumHp += sim.Crew[i].Health;
                    }
                    totalHealth += sumHp / alive;
                }
            }

            return new BalanceReport
            {
                Strategy = strategy,
                MissionsPlayed = missionCount,
                Wins = wins,
                AverageCrewAlive = (float)totalAlive / missionCount,
                AverageLivingHealth = totalHealth / missionCount
            };
        }

        public static string RunFullReport(int missionCount = 1000, SimConfig config = null)
        {
            var careless = RunStrategy(BotStrategy.Careless, missionCount, config);
            var average = RunStrategy(BotStrategy.Average, missionCount, config);
            var optimal = RunStrategy(BotStrategy.Optimal, missionCount, config);

            var sb = new StringBuilder();
            sb.AppendLine($"=== MARS OUTPOST HEADLESS BALANCE REPORT ({missionCount} SEEDED MISSIONS EACH) ===");
            sb.AppendLine(FormatLine(careless));
            sb.AppendLine(FormatLine(average));
            sb.AppendLine(FormatLine(optimal));
            return sb.ToString();
        }

        private static string FormatLine(BalanceReport r)
        {
            return $"{r.Strategy,-10} | WinRate: {r.WinRate * 100f,5:0.0}% ({r.Wins}/{r.MissionsPlayed}) | AvgCrewAlive: {r.AverageCrewAlive,4:0.00} | AvgHP: {r.AverageLivingHealth,5:0.0}";
        }

        private static void PlaySol(OutpostSimulation sim, BotStrategy strategy, int seed)
        {
            if (strategy == BotStrategy.Careless)
            {
                // Careless player under-powers heating & life support, picks neglectful event options, barely acts.
                sim.SetAllocations(0.45f, 0.35f, 0.35f, 0.90f);
                if (sim.CurrentEvent != null && !sim.CurrentEventResolved)
                {
                    // Pick risky/ignore choices (usually index 1 for solar flare, else last choice).
                    int idx = sim.CurrentEvent.Id == "evt_solar_flare" ? 1 : sim.CurrentEvent.Choices.Count - 1;
                    if (!sim.ResolveEventChoice(idx))
                        sim.ResolveEventChoice(sim.CurrentEvent.Choices.Count - 1);
                }
                // Wastes AP or reinforces unnecessary things only occasionally.
                if (sim.CurrentSol % 3 == 0) sim.PerformAction(CrewActionType.RationSupplies);
                return;
            }

            if (strategy == BotStrategy.Average)
            {
                // Average player leaves allocations high (90/85/75/70 = 47.5W), drains battery during storms,
                // only shelters ~45% of the time on flares, and acts reactively with 1 AP when thresholds look bad.
                sim.SetAllocations(0.90f, 0.85f, 0.75f, 0.70f);

                if (sim.CurrentEvent != null && !sim.CurrentEventResolved)
                {
                    int choice = 0;
                    if (sim.CurrentEvent.Id == "evt_solar_flare")
                    {
                        choice = (seed % 10) < 4 ? 0 : 1;
                    }
                    else if (sim.CurrentEvent.Id == "evt_dust_storm")
                    {
                        choice = 1; // Push through on battery
                    }
                    else if (sim.CurrentEvent.Id == "evt_scrubber_clog" || sim.CurrentEvent.Id == "evt_water_recycler")
                    {
                        choice = ((seed + sim.CurrentSol) % 3 == 0 && sim.CanChooseEventChoice(0)) ? 0 : 2;
                    }
                    else
                    {
                        choice = (sim.CurrentSol % 2 == 0 && sim.CanChooseEventChoice(0)) ? 0 : 1;
                    }

                    if (!sim.ResolveEventChoice(choice))
                        sim.ResolveEventChoice(sim.CurrentEvent.Choices.Count - 1);
                }

                // Spends up to 1 AP reactively when something is visibly broken or dusty
                if (sim.ActionPoints > 0)
                {
                    if (sim.ScrubberHealth < 0.55f || sim.RecyclerHealth < 0.55f || sim.CropHealth < 0.55f)
                    {
                        sim.PerformAction(CrewActionType.RepairSystem);
                    }
                    else if (sim.PanelDust > 0.45f)
                    {
                        sim.PerformAction(CrewActionType.CleanPanels);
                    }
                }
                return;
            }

            // Optimal strategy: smart power conservation, always shelters flares, repairs systems, cleans dust, treats crew.
            float lsAlloc = sim.Oxygen < 45f || sim.Water < 45f ? 1.0f : 0.85f;
            float ghAlloc = sim.Food < 40f ? 0.90f : 0.70f;
            float heatAlloc = 0.55f; // Safely above 50% threshold while conserving power
            float commsAlloc = (sim.CurrentEvent != null && sim.CurrentEvent.Id == "evt_earth_message") ? 0.60f : 0.10f;

            if (sim.DustStormSolsRemaining > 0 && sim.Power < 35f)
            {
                ghAlloc = 0.25f;
                lsAlloc = 0.75f;
            }

            sim.SetAllocations(lsAlloc, ghAlloc, heatAlloc, commsAlloc);

            if (sim.CurrentEvent != null && !sim.CurrentEventResolved)
            {
                int bestChoice = ChooseOptimalEventOption(sim);
                if (!sim.ResolveEventChoice(bestChoice))
                    sim.ResolveEventChoice(sim.CurrentEvent.Choices.Count - 1);
            }

            while (sim.ActionPoints > 0)
            {
                var injured = sim.GetLowestHealthLivingCrew();
                if (injured != null && injured.Health < 75f)
                {
                    sim.PerformAction(CrewActionType.TreatCrew, injured.Id);
                }
                else if (sim.ScrubberHealth < 0.80f || sim.RecyclerHealth < 0.80f || sim.CropHealth < 0.80f)
                {
                    sim.PerformAction(CrewActionType.RepairSystem);
                }
                else if (sim.PanelDust >= 0.24f)
                {
                    sim.PerformAction(CrewActionType.CleanPanels);
                }
                else if (sim.Food < 25f || sim.Water < 25f)
                {
                    sim.PerformAction(CrewActionType.RationSupplies);
                }
                else if (sim.Crew[0].Morale < 55f)
                {
                    sim.PerformAction(CrewActionType.RestMeal);
                }
                else if (sim.Shielding < 70f && sim.Power > 45f)
                {
                    sim.PerformAction(CrewActionType.ReinforceShielding);
                }
                else
                {
                    sim.PerformAction(CrewActionType.TreatCrew);
                }
            }
        }

        private static int ChooseOptimalEventOption(OutpostSimulation sim)
        {
            switch (sim.CurrentEvent.Id)
            {
                case "evt_solar_flare": return 0;      // Shelter crew
                case "evt_dust_storm": return sim.ActionPoints > 0 ? 2 : 0; // Seal panels or conserve power
                case "evt_panel_dust": return sim.ActionPoints > 0 ? 0 : 1; // Clean now
                case "evt_scrubber_clog": return sim.ActionPoints > 0 ? 0 : 1; // Rebuild scrubber
                case "evt_water_recycler": return sim.ActionPoints > 0 ? 0 : 1;
                case "evt_greenhouse_blight": return sim.Power > 35f ? 1 : 0;
                case "evt_cold_snap": return sim.Power > 35f ? 0 : 2;
                case "evt_crew_tension": return sim.ActionPoints > 0 ? 0 : 2;
                case "evt_earth_message": return 1;    // Safe local readings
                case "evt_micrometeorite": return sim.ActionPoints > 0 && sim.Power > 20f ? 0 : 1;
                default: return 0;
            }
        }
    }
}
