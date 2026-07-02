using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP28 FailsafeErosionPulse's Health integration: while active it damages
    /// the player's Health on its erosion tick, and the vision degradation tracks the pulse timer.
    /// These need MonoBehaviour lifecycle (Awake auto-wires playerHealth; Health.Awake sets
    /// Current=Max), so they live in PlayMode. The state logic (timer countdown, event firing)
    /// is covered by EditMode tests.
    /// </summary>
    public class Ep28MechanicsPlayTests
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
        }

        /// <summary>
        /// Build a FailsafeErosionPulse + Health on one active GameObject (the "rig").
        /// Call inside a [UnityTest] and yield return null so Health.Awake() sets Current=Max
        /// and FailsafeErosionPulse.Awake() auto-wires playerHealth = that Health.
        /// </summary>
        private FailsafeErosionPulse MakeRig(out Health health)
        {
            var go = new GameObject("ErosionRig");
            _spawned.Add(go);
            health = go.AddComponent<Health>();
            var pulse = go.AddComponent<FailsafeErosionPulse>();
            pulse.AutoAdvance = false; // deterministic: drive Tick and TickDamage by hand
            return pulse;
        }

        [UnityTest]
        public IEnumerator Inactive_HasNoEffect()
        {
            var pulse = MakeRig(out var health);
            yield return null;

            Assert.IsFalse(pulse.IsActive, "Pulse should be inactive before triggering.");
            Assert.AreEqual(0f, pulse.VisionDegradation, 0.001f, "Vision degradation should be 0 when inactive.");
            Assert.AreEqual(1f, pulse.DamageTakenMultiplier, 0.001f, "Damage multiplier should be 1.0 when inactive.");
        }

        [UnityTest]
        public IEnumerator TriggerPulse_ActivatesAndDegradesVisionAndVulnerability()
        {
            var pulse = MakeRig(out var health);
            yield return null;

            pulse.TriggerPulse();

            Assert.IsTrue(pulse.IsActive, "Pulse should be active after triggering.");
            Assert.Greater(pulse.VisionDegradation, 0f, "Vision degradation should be > 0 when active.");
            Assert.Greater(pulse.DamageTakenMultiplier, 1f, "Damage multiplier should be > 1.0 when active.");
        }

        [UnityTest]
        public IEnumerator WhileActive_ErodesPlayerHealth()
        {
            var pulse = MakeRig(out var health);
            yield return null;

            pulse.TriggerPulse();
            float before = health.Current;
            pulse.TickDamage(100f); // one interval elapses -> exactly one erosion tick

            Assert.Less(health.Current, before, "Erosion should damage the player's Health while pulse is active.");
        }

        [UnityTest]
        public IEnumerator Tick_PastDuration_Deactivates()
        {
            var pulse = MakeRig(out var health);
            yield return null;

            pulse.TriggerPulse(5f);
            pulse.Tick(10f); // advance past the pulse duration

            Assert.IsFalse(pulse.IsActive, "Pulse should deactivate when timer expires.");
            Assert.AreEqual(0f, pulse.VisionDegradation, 0.001f, "Vision degradation should return to 0 when pulse deactivates.");
            Assert.AreEqual(1f, pulse.DamageTakenMultiplier, 0.001f, "Damage multiplier should return to 1.0 when pulse deactivates.");
        }
    }
}
