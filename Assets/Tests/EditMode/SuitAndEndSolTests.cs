using System.Collections.Generic;
using NUnit.Framework;
using Outpost.Runtime.Room;
using Outpost.Sim;
using UnityEngine;

namespace Outpost.Tests.EditMode
{
    public class SuitAndEndSolTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _cleanup.Count; i++)
            {
                if (_cleanup[i] != null)
                    Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
        }

        [TestCase(1, "RESCUE IN 6 SOLS")]
        [TestCase(2, "RESCUE IN 5 SOLS")]
        [TestCase(3, "RESCUE IN 4 SOLS")]
        [TestCase(4, "RESCUE IN 3 SOLS")]
        [TestCase(5, "RESCUE IN 2 SOLS")]
        [TestCase(6, "FINAL SOL. RESCUE ARRIVES TODAY.")]
        [TestCase(7, "FINAL SOL. RESCUE ARRIVES TODAY.")]
        public void EndSolBlackCard_DaysLeftFormatting_Sols1To7_NeverUsesSingularPluralBug(int completedSol, string expected)
        {
            string actual = Localization.FormatRescueRemaining(completedSol, totalSols: 7);
            Assert.AreEqual(expected, actual);
            Assert.IsFalse(actual.Contains("1 SOLS"), $"Bad pluralization for sol {completedSol}: {actual}");
        }

        [Test]
        public void EndSolBlackCard_RequiredLocalizationKeys_ResolveWithoutMissingKeyPlaceholder()
        {
            string[] requiredKeys =
            {
                "suit_put_on",
                "suit_take_off",
                "suit_required",
                "suit_cannot_remove",
                "sol_complete",
                "rescue_in_sols",
                "rescue_final_sol",
                "sol_definition"
            };

            foreach (string key in requiredKeys)
            {
                string value = Localization.Get(key);
                Assert.IsNotEmpty(value, $"Missing value for key '{key}'");
                Assert.AreNotEqual($"[{key}]", value, $"Unregistered localization key '{key}'");
            }
        }

        [Test]
        public void SuitRemovalRule_AllowedInPressurizedHabitat_BlockedOutsideOrCyclingOrDepressurized()
        {
            // Pressurized interior (spawn and airlock pressurized)
            Assert.IsTrue(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: false, isDepressurized: false, playerZ: -0.95f));
            Assert.IsTrue(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: false, isDepressurized: false, playerZ: -16.5f));

            // Blocked while airlock is cycling
            Assert.IsFalse(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: true, isDepressurized: false, playerZ: -15.5f));

            // Blocked when airlock chamber is depressurized
            Assert.IsFalse(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: false, isDepressurized: true, playerZ: -15.5f));

            // Blocked beyond the outer airlock door (z < -17.36)
            Assert.IsFalse(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: false, isDepressurized: false, playerZ: -17.37f));
            Assert.IsFalse(SuitController.EvaluateSuitRemovalAllowed(
                isAirlockCycling: false, isDepressurized: false, playerZ: -28.0f));
        }

        [Test]
        public void AirlockCycleWithSuit_RefusesOutwardWithoutSuit_AllowsWithSuitOrInward()
        {
            // Outward from pressurized habitat requires suit
            Assert.IsFalse(AirlockController.CanCycleWithSuit(isCurrentlyDepressurized: false, isWearingSuit: false));
            Assert.IsTrue(AirlockController.CanCycleWithSuit(isCurrentlyDepressurized: false, isWearingSuit: true));

            // Inward cycle (from depressurized outside/airlock back into pressurized habitat) is always allowed
            Assert.IsTrue(AirlockController.CanCycleWithSuit(isCurrentlyDepressurized: true, isWearingSuit: false));
            Assert.IsTrue(AirlockController.CanCycleWithSuit(isCurrentlyDepressurized: true, isWearingSuit: true));
        }

        [Test]
        public void AirlockController_TryCycleAirlock_OutwardWithoutSuit_ReturnsFalseWithSuitRequiredMessage()
        {
            var root = new GameObject("Test_AirlockRoot");
            _cleanup.Add(root);

            var innerGo = new GameObject("InnerDoor");
            innerGo.transform.SetParent(root.transform, false);
            var innerDoor = innerGo.AddComponent<DoorInteractable>();

            var outerGo = new GameObject("OuterDoor");
            outerGo.transform.SetParent(root.transform, false);
            var outerDoor = outerGo.AddComponent<DoorInteractable>();

            var airlock = root.AddComponent<AirlockController>();
            airlock.Configure(innerDoor, outerDoor, lightRef: null, beaconRef: null, cycleSeconds: 3.0f);
            airlock.BindRuntime(audioManager: null, showBanner: null, announcer: null);
            airlock.IsWearingSuitCheck = () => false;

            var session = new GameSession(new SimConfig(), 2026);
            SessionSnapshot snap = session.CurrentSnapshot;

            bool started = airlock.TryCycleAirlock(snap, out string statusMessage);

            Assert.IsFalse(started);
            Assert.IsFalse(airlock.IsCycling);
            Assert.IsFalse(airlock.IsDepressurized);
            Assert.AreEqual(Localization.Get("suit_required"), statusMessage);
        }
    }
}
