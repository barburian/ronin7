using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP21 DreamPhantom mechanic's Health-event integration: damage is fully
    /// refunded while Phased (attacks "pass through"), and a lethal Solid hit dissolves the
    /// phantom. These require MonoBehaviour lifecycle (Awake/OnEnable), so they live in PlayMode
    /// (the EditMode DreamPhantomTests cover the lifecycle-free behaviour: phase cycling, dissolve).
    /// </summary>
    public class Ep21MechanicsPlayTests
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
        /// Build a DreamPhantom with Health on a fresh active GameObject.
        /// IMPORTANT: call inside a [UnityTest] and yield return null so Health.Awake() sets
        /// Current=Max and DreamPhantom.OnEnable() subscribes to Health's Damaged/Died events.
        /// </summary>
        private DreamPhantom MakePhantom(out Health health)
        {
            var go = new GameObject("DreamPhantom");
            _spawned.Add(go);
            health = go.AddComponent<Health>();
            return go.AddComponent<DreamPhantom>();
        }

        [UnityTest]
        public IEnumerator PhasedDamage_IsRefunded()
        {
            var phantom = MakePhantom(out var health);
            yield return null; // let Awake/OnEnable run

            phantom.SetPhase(DreamPhantom.Phase.Phased);

            float hpBefore = health.Current;
            Assert.Greater(hpBefore, 0f, "Phantom should start with health > 0");

            health.ApplyDamage(new DamageInfo(25f, Vector3.zero, Vector3.zero, null));

            Assert.AreEqual(hpBefore, health.Current, 0.001f,
                "Damage should be fully refunded while Phased (attack passes through)");
            Assert.IsTrue(health.IsAlive, "Phantom should remain alive while Phased");
        }

        [UnityTest]
        public IEnumerator SolidLethalDamage_DissolvesPhantom()
        {
            var phantom = MakePhantom(out var health);
            yield return null; // let Awake/OnEnable run

            phantom.SetPhase(DreamPhantom.Phase.Solid);

            int counter = 0;
            phantom.onDissolved.AddListener(() => counter++);

            health.ApplyDamage(new DamageInfo(health.Max + 100f, Vector3.zero, Vector3.zero, null));

            Assert.AreEqual(1, counter, "onDissolved should fire once on death while Solid");
            Assert.IsFalse(phantom.gameObject.activeSelf, "Phantom GameObject should deactivate on dissolve");
        }
    }
}
