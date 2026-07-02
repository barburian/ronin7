using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.PlayMode
{
    public class Ep32MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        /// <summary>
        /// AddBreach_IncrementsBreachCount: add 3 breaches, assert BreachCount==3, SealedCount==0, AllSealed==false.
        /// </summary>
        [Test]
        public void AddBreach_IncrementsBreachCount()
        {
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            // Add 3 breaches
            int idx0 = controller.AddBreach("breach_1");
            int idx1 = controller.AddBreach("breach_2");
            int idx2 = controller.AddBreach("breach_3");

            Assert.AreEqual(0, idx0);
            Assert.AreEqual(1, idx1);
            Assert.AreEqual(2, idx2);
            Assert.AreEqual(3, controller.BreachCount);
            Assert.AreEqual(0, controller.SealedCount);
            Assert.IsFalse(controller.AllSealed);
        }

        /// <summary>
        /// AddSealProgress_SealsAfterSecondsToSeal: add a breach; AddSealProgress(0, 1.5f) returns false;
        /// AddSealProgress(0, 1.5f) reaches 3.0 and returns true; SealedCount==1.
        /// </summary>
        [Test]
        public void AddSealProgress_SealsAfterSecondsToSeal()
        {
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            int idx = controller.AddBreach("breach_test");

            // First 1.5 seconds of progress (default SecondsToSeal is 3)
            bool result1 = controller.AddSealProgress(idx, 1.5f);
            Assert.IsFalse(result1, "Should not be sealed after 1.5 seconds out of 3");
            Assert.AreEqual(0, controller.SealedCount);

            // Second 1.5 seconds brings total to 3.0, sealing the breach
            bool result2 = controller.AddSealProgress(idx, 1.5f);
            Assert.IsTrue(result2, "Should be sealed after total 3.0 seconds");
            Assert.AreEqual(1, controller.SealedCount);
        }

        /// <summary>
        /// Tick_PulsesDamageOnUnsealedBreachAfterInterval: Configure a live player Health; add 1 breach;
        /// Tick(0f) applies 0 pulses (not yet due); Tick(5f) applies 1 pulse and reduces player HP.
        /// </summary>
        [Test]
        public void Tick_PulsesDamageOnUnsealedBreachAfterInterval()
        {
            // Create controller
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            // Create and configure player health
            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            var playerHealth = playerGo.AddComponent<Health>();
            playerHealth.Configure(100f);

            // Wire the controller to the player
            controller.Configure(playerHealth);

            // Add one breach
            int idx = controller.AddBreach("breach_test");

            // Tick at time 0 — no pulse yet (first pulse due at breachInterval=5)
            float initialHealth = playerHealth.Current;
            int pulses0 = controller.Tick(0f);
            Assert.AreEqual(0, pulses0, "No pulse should occur at time 0");
            Assert.AreEqual(initialHealth, playerHealth.Current, "No damage should be applied yet");

            // Tick at time 5 — pulse is due
            int pulses5 = controller.Tick(5f);
            Assert.AreEqual(1, pulses5, "One pulse should occur at time >= breachInterval");
            float expectedDamage = controller.DamagePerBreach;
            float expectedHealth = initialHealth - expectedDamage;
            Assert.AreEqual(expectedHealth, playerHealth.Current, $"Health should drop by {expectedDamage}");
        }

        /// <summary>
        /// Tick_SealedBreachDoesNotPulse: add 1 breach, fully seal it, then Tick(100f) returns 0 and player HP unchanged.
        /// </summary>
        [Test]
        public void Tick_SealedBreachDoesNotPulse()
        {
            // Create controller
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            // Create and configure player health
            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            var playerHealth = playerGo.AddComponent<Health>();
            playerHealth.Configure(100f);

            // Wire the controller to the player
            controller.Configure(playerHealth);

            // Add one breach
            int idx = controller.AddBreach("breach_test");

            // Seal it completely (SecondsToSeal is default 3)
            controller.AddSealProgress(idx, 3f);
            Assert.IsTrue(controller.AllSealed);

            // Tick at a late time — sealed breach should not pulse
            float initialHealth = playerHealth.Current;
            int pulses = controller.Tick(100f);
            Assert.AreEqual(0, pulses, "Sealed breach should not pulse");
            Assert.AreEqual(initialHealth, playerHealth.Current, "Health should not change");
        }

        /// <summary>
        /// Tick_NullPlayer_NoError: do NOT Configure (player null); add a breach; Tick(100f) returns 0 (no exception).
        /// </summary>
        [Test]
        public void Tick_NullPlayer_NoError()
        {
            // Create controller
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            // Do NOT call Configure — player is null

            // Add one breach
            int idx = controller.AddBreach("breach_test");

            // Tick should safely return 0 without exception
            int pulses = controller.Tick(100f);
            Assert.AreEqual(0, pulses, "Null player should result in 0 pulses, no exception");
        }

        /// <summary>
        /// AllSealed_TrueAfterAllBreachesSealed: add 2 breaches, seal both, assert AllSealed==true and SealedCount==2.
        /// </summary>
        [Test]
        public void AllSealed_TrueAfterAllBreachesSealed()
        {
            // Create controller
            var controllerGo = new GameObject("HullBreachController");
            _spawned.Add(controllerGo);
            var controller = controllerGo.AddComponent<HullBreachRepairController>();

            // Add 2 breaches
            int idx0 = controller.AddBreach("breach_1");
            int idx1 = controller.AddBreach("breach_2");

            Assert.IsFalse(controller.AllSealed, "AllSealed should be false when breaches exist and are unsealed");

            // Seal both
            controller.AddSealProgress(idx0, 3f);
            controller.AddSealProgress(idx1, 3f);

            Assert.AreEqual(2, controller.SealedCount);
            Assert.IsTrue(controller.AllSealed, "AllSealed should be true when all breaches are sealed");
        }
    }
}
