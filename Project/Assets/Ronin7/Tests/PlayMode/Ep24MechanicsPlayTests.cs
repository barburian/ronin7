using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP24 HiveCascadeController mechanic. With autoCascade off we drive states manually
    /// and assert the core invariant: only Attacking members have their MeleeAttacker enabled; Frozen and
    /// Conflicted members are disengaged. Lifecycle (OnEnable→Initialize) requires PlayMode — EditMode does
    /// not run Awake/OnEnable.
    /// </summary>
    public class Ep24MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{field}' not found on {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        private TrainingDummy MakeDummy()
        {
            var go = new GameObject("CloneMember");
            _spawned.Add(go);
            go.AddComponent<Health>();
            return go.AddComponent<TrainingDummy>();
        }

        [UnityTest]
        public IEnumerator OnlyAttackingMembers_HaveMeleeEnabled()
        {
            var d0 = MakeDummy();
            var d1 = MakeDummy();

            var controllerGo = new GameObject("HiveCascade");
            _spawned.Add(controllerGo);
            controllerGo.SetActive(false);
            var hive = controllerGo.AddComponent<HiveCascadeController>();
            SetPrivate(hive, "members", new List<MeleeAttacker> { d0, d1 });
            SetPrivate(hive, "autoCascade", false);
            controllerGo.SetActive(true);

            // Let OnEnable → Initialize run.
            yield return null;

            Assert.AreEqual(2, hive.MemberCount);

            // Default after init: Attacking → melee enabled.
            Assert.IsTrue(d0.enabled, "Member 0 should start Attacking (melee enabled).");

            // Freeze member 0 → melee disabled.
            hive.SetMemberState(0, HiveCascadeController.CascadeState.Frozen);
            Assert.AreEqual(HiveCascadeController.CascadeState.Frozen, hive.GetMemberState(0));
            Assert.IsFalse(d0.enabled, "Frozen member must not attack.");

            // Conflicted member 1 → also disengaged.
            hive.SetMemberState(1, HiveCascadeController.CascadeState.Conflicted);
            Assert.AreEqual(HiveCascadeController.CascadeState.Conflicted, hive.GetMemberState(1));
            Assert.IsFalse(d1.enabled, "Conflicted member must not attack the player.");

            // Back to Attacking → re-engages.
            hive.SetMemberState(0, HiveCascadeController.CascadeState.Attacking);
            Assert.IsTrue(d0.enabled, "Returning to Attacking must re-enable melee.");
        }
    }
}
