using NUnit.Framework;
using Outpost.Runtime.Terminal;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Tests.EditMode
{
    public class OutpostSimTests
    {
        [Test]
        public void Stocks_ClampBetweenZeroAndHundred()
        {
            var sim = new OutpostSimulation();
            sim.StartNewMission(1);

            // Set 0% allocation across everything to drain stocks, verify floor at 0
            sim.SetAllocations(0f, 0f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                if (sim.Status == MissionStatus.InProgress)
                    sim.EndSol();
            }

            Assert.GreaterOrEqual(sim.Power, 0f);
            Assert.LessOrEqual(sim.Power, 100f);
            Assert.GreaterOrEqual(sim.Oxygen, 0f);
            Assert.LessOrEqual(sim.Oxygen, 100f);
            Assert.GreaterOrEqual(sim.Water, 0f);
            Assert.LessOrEqual(sim.Water, 100f);
            Assert.GreaterOrEqual(sim.Food, 0f);
            Assert.LessOrEqual(sim.Food, 100f);
            Assert.GreaterOrEqual(sim.Shielding, 0f);
            Assert.LessOrEqual(sim.Shielding, 100f);
        }

        [Test]
        public void PowerMath_SolarDustStormAndZeroBatteryPenalty()
        {
            var cfg = new SimConfig();
            var sim = new OutpostSimulation(cfg);
            sim.StartNewMission(7);

            // Generation = 60 * (1 - StartPanelDust) * 1.0
            float expectedGen = 60f * (1f - cfg.StartPanelDust) * 1f;
            Assert.AreEqual(expectedGen, sim.ComputeSolarGeneration(), 0.01f);

            // Allocations at 100% -> 20+15+15+5 = 55W minus Engineer bonus
            sim.SetAllocations(1f, 1f, 1f, 1f);
            float expectedDemand = 55f - cfg.EngineerPowerSaved;
            Assert.AreEqual(expectedDemand, sim.ComputeTotalPowerDemand(), 0.01f);

            // Drain battery artificially by dropping StartPower in a custom config
            var lowPwrCfg = new SimConfig { StartPower = 1f, DustAccumulationPerSol = 0.5f };
            var lowSim = new OutpostSimulation(lowPwrCfg);
            lowSim.StartNewMission(9);
            lowSim.SetAllocations(1f, 1f, 1f, 1f);
            // Force panel dust higher via Sol 1 defer choice (+0.10 -> 0.18, gen = 60*0.82 = 49.2, demand=52, net=-2.8 -> battery hits 0)
            lowSim.ResolveEventChoice(2); // Defer cleaning (+10% dust)
            var forecast = lowSim.GetForecast();
            Assert.IsTrue(forecast.WillBlackout, "Battery should project hitting 0.");
            lowSim.EndSol();
            Assert.IsTrue(lowSim.LastSolBlackout);
            Assert.AreEqual(0f, lowSim.Power, 0.001f);
        }

        [Test]
        public void LifeSupport_OxygenWaterFoodAndHeatingPenalties()
        {
            var cfg = new SimConfig { BaseFailureChance = 0f, ColdFailureChanceBonus = 0f };
            var sim = new OutpostSimulation(cfg);
            sim.StartNewMission(5);
            sim.ResolveEventChoice(2); // Defer cleaning

            // Set heating to 20% (<50% safe threshold)
            sim.SetAllocations(1f, 1f, 0.2f, 0f);
            float startHp = sim.Crew[0].Health;
            float startMorale = sim.Crew[0].Morale;

            sim.EndSol();

            Assert.Less(sim.Crew[0].Health, startHp, "Heating below 50% must reduce crew health.");
            Assert.Less(sim.Crew[0].Morale, startMorale, "Heating below 50% must reduce crew morale.");
        }

        [Test]
        public void Events_FixedTutorialSolsAndSeededDeterminism()
        {
            var simA = new OutpostSimulation();
            var simB = new OutpostSimulation();
            simA.StartNewMission(1234);
            simB.StartNewMission(1234);

            Assert.AreEqual("evt_panel_dust", simA.CurrentEvent.Id);
            simA.EndSol();
            simB.EndSol();

            Assert.AreEqual("evt_scrubber_clog", simA.CurrentEvent.Id);
            simA.EndSol();
            simB.EndSol();

            Assert.AreEqual(simA.CurrentEvent.Id, simB.CurrentEvent.Id, "Same seed must yield identical Sol 3 event.");
        }

        [Test]
        public void Actions_SpendApAndApplyRoleBonuses()
        {
            var sim = new OutpostSimulation();
            sim.StartNewMission(42);
            int startAp = sim.ActionPoints;
            float cmdrStartHp = sim.Crew[2].Health; // Fallen commander starts at 60

            bool ok = sim.PerformAction(CrewActionType.TreatCrew, 2);
            Assert.IsTrue(ok);
            Assert.AreEqual(startAp - 1, sim.ActionPoints);
            // TreatCrew (18) + Medic bonus (10) = +28 -> 88
            Assert.AreEqual(cmdrStartHp + sim.Config.TreatCrewHealthGain + sim.Config.MedicTreatBonus, sim.Crew[2].Health, 0.01f);
        }

        [Test]
        public void WinLoseConditions_AllCrewDeadLoses_SurviveSol7WinsWithGrade()
        {
            // Lose case: extreme zero O2/water/food kills crew
            var lethalCfg = new SimConfig
            {
                StartOxygen = 0f,
                StartWater = 0f,
                StartFood = 0f,
                ZeroOxygenHealthPenalty = 60f,
                ZeroWaterHealthPenalty = 40f,
                ZeroFoodHealthPenalty = 20f
            };
            var loseSim = new OutpostSimulation(lethalCfg);
            loseSim.StartNewMission(3);
            loseSim.SetAllocations(0f, 0f, 0f, 0f);
            loseSim.EndSol();
            Assert.AreEqual(MissionStatus.Lost, loseSim.Status);
            Assert.AreEqual(0, loseSim.LivingCrewCount);

            // Win case: survive 7 sols with healthy allocations
            var winSim = new OutpostSimulation();
            winSim.StartNewMission(10);
            for (int s = 1; s <= 7 && winSim.Status == MissionStatus.InProgress; s++)
            {
                winSim.SetAllocations(0.95f, 0.80f, 0.60f, 0.10f);
                if (winSim.CurrentEvent.Id == "evt_solar_flare") winSim.ResolveEventChoice(0);
                else if (winSim.CanChooseEventChoice(0)) winSim.ResolveEventChoice(0);
                else winSim.ResolveEventChoice(1);

                while (winSim.ActionPoints > 0)
                {
                    if (winSim.PanelDust > 0.24f) winSim.PerformAction(CrewActionType.CleanPanels);
                    else if (winSim.ScrubberHealth < 0.85f || winSim.RecyclerHealth < 0.85f || winSim.CropHealth < 0.85f) winSim.PerformAction(CrewActionType.RepairSystem);
                    else winSim.PerformAction(CrewActionType.TreatCrew);
                }
                winSim.EndSol();
            }

            Assert.AreEqual(MissionStatus.Won, winSim.Status);
            Assert.GreaterOrEqual(winSim.LivingCrewCount, 1);
            Assert.AreNotEqual(MissionGrade.F, winSim.Grade);
        }

        [Test]
        public void BalanceRunner_1000SeededMissions_MeetsTargetWinRates()
        {
            var cfg = new SimConfig();
            var careless = BalanceRunner.RunStrategy(BotStrategy.Careless, 1000, cfg);
            var average = BalanceRunner.RunStrategy(BotStrategy.Average, 1000, cfg);
            var optimal = BalanceRunner.RunStrategy(BotStrategy.Optimal, 1000, cfg);

            string report = BalanceRunner.RunFullReport(1000, cfg);
            Debug.Log(report);

            Assert.Less(careless.WinRate, 0.30f, $"Careless strategy won too often ({careless.WinRate:P1}).\n{report}");
            Assert.That(average.WinRate, Is.InRange(0.35f, 0.68f), $"Average strategy should win around half ({average.WinRate:P1}).\n{report}");
            Assert.GreaterOrEqual(optimal.WinRate, 0.90f, $"Optimal strategy should almost always win ({optimal.WinRate:P1}).\n{report}");
        }

        [Test]
        public void SessionSnapshot_MatchesSimulationEngineState()
        {
            var session = new GameSession(new SimConfig(), 2026);
            int stateEvents = 0;
            int solEvents = 0;
            session.OnStateChanged += _ => stateEvents++;
            session.OnSolStarted += _ => solEvents++;

            session.SetAllocation(PowerSystemType.Heating, 60);
            session.SetAllocation(PowerSystemType.Comms, 40);
            session.DoAction(CrewActionType.TreatCrew, 2);
            session.ChooseEventOption(1);

            var snap = session.CurrentSnapshot;
            var eng = session.Engine;
            var forecast = eng.GetForecast();

            Assert.AreEqual(eng.CurrentSol, snap.CurrentSol);
            Assert.AreEqual(eng.ActionPoints, snap.ActionPoints);
            Assert.AreEqual(eng.Power, snap.Power, 0.001f);
            Assert.AreEqual(eng.Oxygen, snap.Oxygen, 0.001f);
            Assert.AreEqual(eng.Water, snap.Water, 0.001f);
            Assert.AreEqual(eng.Food, snap.Food, 0.001f);
            Assert.AreEqual(eng.Shielding, snap.Shielding, 0.001f);
            Assert.AreEqual(eng.PanelDust * 100f, snap.PanelDustPct, 0.01f);
            Assert.AreEqual(eng.ScrubberHealth * 100f, snap.ScrubberHealthPct, 0.01f);
            Assert.AreEqual(eng.RecyclerHealth * 100f, snap.RecyclerHealthPct, 0.01f);
            Assert.AreEqual(eng.CropHealth * 100f, snap.CropHealthPct, 0.01f);
            Assert.AreEqual(60, snap.HeatingAllocPct);
            Assert.AreEqual(40, snap.CommsAllocPct);
            Assert.AreEqual(forecast.NetPowerDelta, snap.NetPowerDelta, 0.001f);
            Assert.AreEqual(forecast.NetOxygenDelta, snap.NetOxygenDelta, 0.001f);
            Assert.AreEqual(forecast.NetWaterDelta, snap.NetWaterDelta, 0.001f);
            Assert.AreEqual(forecast.NetFoodDelta, snap.NetFoodDelta, 0.001f);
            Assert.AreEqual(eng.Crew.Count, snap.Crew.Count);
            for (int i = 0; i < eng.Crew.Count; i++)
            {
                Assert.AreEqual(eng.Crew[i].Health, snap.Crew[i].Health, 0.001f);
                Assert.AreEqual(eng.Crew[i].Morale, snap.Crew[i].Morale, 0.001f);
                Assert.AreEqual(eng.Crew[i].CumulativeDoseMsv, snap.Crew[i].DoseMsv, 0.001f);
            }

            session.EndSol();
            Assert.AreEqual(1, solEvents);
            Assert.GreaterOrEqual(stateEvents, 5);
            Assert.AreEqual(session.Engine.CurrentSol, session.CurrentSnapshot.CurrentSol);
        }

        [Test]
        public void IntroSequenceData_HasAll8CardsWithNonEmptyLocalizationKeys()
        {
            var seq = ScriptableObject.CreateInstance<IntroSequenceData>();
            try
            {
                seq.PopulateDefaultCards();
                Assert.AreEqual(8, seq.Cards.Count);

                for (int i = 0; i < seq.Cards.Count; i++)
                {
                    var card = seq.Cards[i];
                    if (card.type == IntroCardType.BlackHold) continue;

                    Assert.IsNotNull(card.textLineKeys, $"Card {i} has null textLineKeys");
                    Assert.Greater(card.textLineKeys.Length, 0, $"Card {i} has empty textLineKeys");
                    for (int k = 0; k < card.textLineKeys.Length; k++)
                    {
                        string key = card.textLineKeys[k];
                        Assert.IsFalse(string.IsNullOrEmpty(key), $"Card {i} key {k} is empty");
                        string localized = Localization.Get(key);
                        Assert.IsFalse(string.IsNullOrEmpty(localized), $"Localization missing for {key}");
                        Assert.AreNotEqual(key, localized, $"Localization fell back to key for {key}");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(seq);
            }
        }
    }
}
