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
    /// Exercises the EP23 CryoChillController's Health integration: while frostbitten it damages
    /// the player's Health on its damage tick, and warming/clearing stops it. These need
    /// MonoBehaviour lifecycle (Awake auto-wires playerHealth; Health.Awake sets Current=Max),
    /// so they live in PlayMode. The lifecycle-free meter logic is covered by the EditMode
    /// CryoChillControllerTests.
    /// </summary>
    public class Ep23MechanicsPlayTests
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
        /// Build a CryoChillController + Health on one active GameObject (the "rig").
        /// Call inside a [UnityTest] and yield return null so Health.Awake() sets Current=Max
        /// and CryoChillController.Awake() auto-wires playerHealth = that Health.
        /// </summary>
        private CryoChillController MakeRig(out Health health)
        {
            var go = new GameObject("CryoRig");
            _spawned.Add(go);
            health = go.AddComponent<Health>();
            var chill = go.AddComponent<CryoChillController>();
            chill.AutoAdvance = false; // deterministic: drive TickDamage by hand
            return chill;
        }

        [UnityTest]
        public IEnumerator Frostbite_DamagesPlayerHealth()
        {
            var chill = MakeRig(out var health);
            yield return null; // let Awake run

            chill.SetChill(1f); // engage frostbite
            Assert.IsTrue(chill.IsFrostbitten, "Chill at max should be frostbitten.");

            float before = health.Current;
            chill.TickDamage(100f); // one interval elapses -> exactly one damage tick

            Assert.Less(health.Current, before, "Frostbite should damage the player's Health.");
        }

        [UnityTest]
        public IEnumerator NotFrostbitten_NoDamage()
        {
            var chill = MakeRig(out var health);
            yield return null;

            float before = health.Current;
            chill.TickDamage(100f); // not frostbitten -> no-op

            Assert.AreEqual(before, health.Current, 0.001f, "No damage should occur while warm.");
        }

        [UnityTest]
        public IEnumerator Clear_StopsFrostbiteDamage()
        {
            var chill = MakeRig(out var health);
            yield return null;

            chill.SetChill(1f);
            chill.Clear();
            Assert.IsFalse(chill.IsFrostbitten, "Clear() should reset the frostbite state.");

            float before = health.Current;
            chill.TickDamage(100f);

            Assert.AreEqual(before, health.Current, 0.001f, "Cleared frostbite should deal no damage.");
        }
    }
}
