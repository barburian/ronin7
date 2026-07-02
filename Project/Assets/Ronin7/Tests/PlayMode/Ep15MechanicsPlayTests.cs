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
    /// Exercises the EP15 FactionCombatant mechanic: units from different factions attack each other
    /// (and the player) in a three-way desert battle.
    /// </summary>
    public class Ep15MechanicsPlayTests
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

        /// <summary>Build a faction unit with Health + FactionCombatant, with specified faction.</summary>
        private (Health health, FactionCombatant combatant) MakeFactionCombatant(int factionId, float maxHealth = 100f)
        {
            var go = new GameObject($"FactionCombatant_Faction{factionId}");
            _spawned.Add(go);

            var health = go.AddComponent<Health>();
            health.Configure(maxHealth);

            var combatant = go.AddComponent<FactionCombatant>();
            combatant.SetFaction(factionId);

            // Toggle to re-run Awake/OnEnable wiring if needed
            combatant.enabled = false;
            combatant.enabled = true;

            return (health, combatant);
        }

        /// <summary>Build a player unit with Health + CharacterController (identified as player by FactionCombatant targeting logic).</summary>
        private Health MakePlayer(float maxHealth = 100f)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);

            var health = go.AddComponent<Health>();
            health.Configure(maxHealth);

            var cc = go.AddComponent<CharacterController>();
            cc.center = Vector3.zero;
            cc.radius = 0.4f;
            cc.height = 2f;

            return health;
        }

        private static DamageInfo Hit(float amount) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, null);

        [Test]
        public void ShouldTargetFaction_DifferentFactionsAreHostile()
        {
            // Units from faction 0 should target units from faction 1
            Assert.IsTrue(FactionCombatant.ShouldTargetFaction(0, 1), "Different factions should be hostile");

            // Units from faction 1 should target units from faction 0
            Assert.IsTrue(FactionCombatant.ShouldTargetFaction(1, 0), "Different factions should be hostile");

            // Units from the same faction should not target each other
            Assert.IsFalse(FactionCombatant.ShouldTargetFaction(0, 0), "Same faction should not be hostile");
            Assert.IsFalse(FactionCombatant.ShouldTargetFaction(1, 1), "Same faction should not be hostile");
        }

        [UnityTest]
        public IEnumerator FactionCombatant_AttacksDifferentFactionUnit()
        {
            var (attacker, combatant0) = MakeFactionCombatant(factionId: 0, maxHealth: 100f);
            var (target, combatant1) = MakeFactionCombatant(factionId: 1, maxHealth: 100f);

            // Place attacker at origin, target nearby
            combatant0.transform.position = Vector3.zero;
            combatant1.transform.position = new Vector3(1f, 0f, 0f);

            yield return null;

            // Tick combat repeatedly until target takes damage or a reasonable time passes
            float initialHealth = target.Current;
            for (int i = 0; i < 100; i++)
            {
                combatant0.TickCombat(0.1f);
                if (target.Current < initialHealth)
                {
                    break;
                }
            }

            Assert.Less(target.Current, initialHealth, "Different-faction unit should take damage from attacker");
        }

        [UnityTest]
        public IEnumerator FactionCombatant_DoesNotAttackSameFaction()
        {
            var (unit1, combatant0) = MakeFactionCombatant(factionId: 0, maxHealth: 100f);
            var (unit2, combatant1) = MakeFactionCombatant(factionId: 0, maxHealth: 100f);

            // Place both units nearby (within range)
            combatant0.transform.position = Vector3.zero;
            combatant1.transform.position = new Vector3(1f, 0f, 0f);

            yield return null;

            // Tick combat for several intervals
            float initialHealth = unit2.Current;
            for (int i = 0; i < 50; i++)
            {
                combatant0.TickCombat(0.1f);
            }

            Assert.IsNull(combatant0.CurrentTarget, "Same-faction unit should not be targeted");
            Assert.AreEqual(initialHealth, unit2.Current, 0.01f, "Same-faction unit should not take damage");
        }

        [UnityTest]
        public IEnumerator FactionCombatant_TargetsPlayer()
        {
            var (unit, combatant) = MakeFactionCombatant(factionId: 0, maxHealth: 100f);
            var playerHealth = MakePlayer(maxHealth: 100f);

            // Place combatant at origin, player nearby
            unit.transform.position = Vector3.zero;
            playerHealth.transform.position = new Vector3(1f, 0f, 0f);

            yield return null;

            // Tick combat to trigger retarget
            combatant.TickCombat(1.1f);

            Assert.AreEqual(playerHealth, combatant.CurrentTarget, "FactionCombatant should target the player regardless of faction");
        }
    }
}
