using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards FactionCombatant.RetargetNearestEnemy's nearest-hostile selection now that it iterates
    /// <see cref="Health.Active"/> (perf fix P-B) instead of scanning the scene with
    /// FindObjectsByType every retarget tick. Exercises the real private method via reflection, same
    /// idiom as UnbrokenWardTests/ProtectNpcObjectiveTests.
    /// </summary>
    public class FactionCombatantRetargetTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private static void Life(Health h, string method) =>
            typeof(Health).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(h, null);

        private Health MakeHealth(Vector3 pos)
        {
            var go = new GameObject("HealthTarget");
            go.transform.position = pos;
            _spawned.Add(go);
            var h = go.AddComponent<Health>();
            h.Configure(10f); // EditMode skips Awake; seed via Configure per Health's contract.
            Life(h, "OnEnable");
            return h;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            Health.Active.Clear();
        }

        [Test]
        public void RetargetNearestEnemy_PicksNearestRivalFaction_IgnoresSelfAndSameFaction()
        {
            var selfHealth = MakeHealth(Vector3.zero);
            var self = selfHealth.gameObject.AddComponent<FactionCombatant>();
            self.SetFaction(0);

            var sameFactionHealth = MakeHealth(new Vector3(1f, 0f, 0f));
            sameFactionHealth.gameObject.AddComponent<FactionCombatant>().SetFaction(0); // ignored: same faction

            var farRivalHealth = MakeHealth(new Vector3(10f, 0f, 0f));
            farRivalHealth.gameObject.AddComponent<FactionCombatant>().SetFaction(1);

            var nearRivalHealth = MakeHealth(new Vector3(3f, 0f, 0f));
            nearRivalHealth.gameObject.AddComponent<FactionCombatant>().SetFaction(1);

            InvokeRetarget(self);

            Assert.AreSame(nearRivalHealth, self.CurrentTarget);
        }

        [Test]
        public void RetargetNearestEnemy_SkipsDeadTargets()
        {
            var selfHealth = MakeHealth(Vector3.zero);
            var self = selfHealth.gameObject.AddComponent<FactionCombatant>();
            self.SetFaction(0);

            var deadNearHealth = MakeHealth(new Vector3(1f, 0f, 0f));
            deadNearHealth.gameObject.AddComponent<FactionCombatant>().SetFaction(1);
            deadNearHealth.ApplyDamage(new DamageInfo(9999f, Vector3.zero, Vector3.forward, null, DamageType.Melee));

            var aliveFarHealth = MakeHealth(new Vector3(5f, 0f, 0f));
            aliveFarHealth.gameObject.AddComponent<FactionCombatant>().SetFaction(1);

            InvokeRetarget(self);

            Assert.AreSame(aliveFarHealth, self.CurrentTarget);
        }

        private static void InvokeRetarget(FactionCombatant fc) =>
            typeof(FactionCombatant).GetMethod("RetargetNearestEnemy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fc, null);
    }
}
