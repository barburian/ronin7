using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP18 FloodingWaterHazard mechanic: a rising water volume that applies
    /// pressure damage over time to submerged players, with per-victim cooldown logic.
    /// </summary>
    public class Ep18MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>Build a FloodingWaterHazard on a fresh GameObject.</summary>
        private FloodingWaterHazard MakeHazard()
        {
            var go = new GameObject("FloodingWaterHazard");
            _spawned.Add(go);
            var hazard = go.AddComponent<FloodingWaterHazard>();
            go.AddComponent<BoxCollider>().isTrigger = true; // Trigger for OnTriggerStay
            return hazard;
        }

        /// <summary>
        /// Build a live player Health on a fresh GameObject with CharacterController.
        /// IMPORTANT: Must be called in [UnityTest] with yield return null to let Awake() run.
        /// </summary>
        private Health MakePlayerHealth()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var health = go.AddComponent<Health>();
            go.AddComponent<CharacterController>(); // Mark as player for damage detection
            return health;
        }

        [UnityTest]
        public IEnumerator TryApplyPressure_RespectsInterval()
        {
            // Create hazard and player Health
            var hazard = MakeHazard();
            var player = MakePlayerHealth();

            // Wait one frame for Health.Awake() to initialize Current = maxHealth
            yield return null;

            float initialHealth = player.Current;
            Assert.Greater(initialHealth, 0f, "Player should start with health > 0");

            // First damage at time 0 should succeed
            bool hit1 = hazard.TryApplyPressure(player, 0f);
            Assert.IsTrue(hit1, "First damage attempt should return true");
            float afterFirstHit = player.Current;
            Assert.Less(afterFirstHit, initialHealth, "Health should have decreased after first hit");
            Assert.AreEqual(initialHealth - hazard.DamagePerTick, afterFirstHit, 0.001f,
                "Health should drop by exactly damagePerTick");

            // Second attempt at time 0.5 (within 1.0 default interval) should fail
            bool hit2 = hazard.TryApplyPressure(player, 0.5f);
            Assert.IsFalse(hit2, "Damage within cooldown should return false");
            Assert.AreEqual(afterFirstHit, player.Current, "Health should be unchanged during cooldown");

            // Third attempt at time 1.0 (cooldown expired) should succeed
            bool hit3 = hazard.TryApplyPressure(player, 1.0f);
            Assert.IsTrue(hit3, "Damage after cooldown should return true");
            float afterSecondHit = player.Current;
            Assert.Less(afterSecondHit, afterFirstHit, "Health should have decreased again");
            Assert.AreEqual(afterFirstHit - hazard.DamagePerTick, afterSecondHit, 0.001f,
                "Second hit should also drop by exactly damagePerTick");
        }

        [Test]
        public void TryApplyPressure_IgnoresDeadOrNullTarget()
        {
            var hazard = MakeHazard();

            // Null target should return false
            bool nullResult = hazard.TryApplyPressure(null, 0f);
            Assert.IsFalse(nullResult, "TryApplyPressure(null, ...) should return false");

            // Dead Health should return false (must be in [UnityTest] to create live Health, so test separately below)
        }

        [UnityTest]
        public IEnumerator TryApplyPressure_IgnoresDead()
        {
            var hazard = MakeHazard();
            var player = MakePlayerHealth();

            // Wait for Health.Awake()
            yield return null;

            float maxHealth = player.Current;
            Assert.Greater(maxHealth, 0f, "Player should start alive");

            // Damage player to death
            while (player.IsAlive)
            {
                player.ApplyDamage(new DamageInfo(maxHealth + 10f, Vector3.zero, Vector3.zero, null));
            }

            Assert.IsFalse(player.IsAlive, "Player should be dead");

            // Try to apply pressure to dead Health — should return false
            bool hitDead = hazard.TryApplyPressure(player, 100f);
            Assert.IsFalse(hitDead, "TryApplyPressure on dead Health should return false");
        }
    }
}
