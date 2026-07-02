using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    public class Ep09MechanicsPlayTests
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

        /// <summary>
        /// DuelYield_YieldsWhenHealthCrossesThreshold: GameObject with Health (100 max) + DuelYield
        /// wired (opponent = Health, threshold 0.3); ApplyDamage to 25; yield frame; assert State == Yielded
        /// and opponent is still alive.
        /// </summary>
        [UnityTest]
        public IEnumerator DuelYield_YieldsWhenHealthCrossesThreshold()
        {
            // Create the opponent (Health component only, no other systems)
            var opponentGo = new GameObject("OpponentHealth");
            _spawned.Add(opponentGo);
            var opponentHealth = opponentGo.AddComponent<Health>();
            opponentHealth.Configure(100f);

            // Create the DuelYield component on its own GameObject
            var duelGo = new GameObject("DuelYield");
            _spawned.Add(duelGo);
            var duelYield = duelGo.AddComponent<DuelYield>();

            // Wire the opponent reference via reflection
            var opponentField = typeof(DuelYield).GetField("opponent", BindingFlags.NonPublic | BindingFlags.Instance);
            opponentField?.SetValue(duelYield, opponentHealth);

            // Set threshold to 0.3 (30%)
            var thresholdField = typeof(DuelYield).GetField("yieldThreshold", BindingFlags.NonPublic | BindingFlags.Instance);
            thresholdField?.SetValue(duelYield, 0.3f);

            // OnEnable already ran with a null opponent (reflection wiring happens after AddComponent),
            // so re-toggle the component to re-run the event subscription with the wired opponent.
            duelYield.enabled = false;
            duelYield.enabled = true;

            yield return null;

            // Apply damage: 100 - 75 = 25 (25% of max, crosses 30% threshold)
            opponentHealth.ApplyDamage(new DamageInfo(75f, Vector3.zero, Vector3.zero, null));
            yield return null;

            Assert.AreEqual(DuelYield.DuelState.Yielded, duelYield.State, "DuelYield should enter Yielded state when health crosses threshold");
            Assert.IsTrue(opponentHealth.IsAlive, "Opponent should still be alive (health > 0)");
        }

        /// <summary>
        /// MemoryEchoVignette_TriggerSetsHasPlayedAndActivatesRoot: create vignette with inactive
        /// echoRoot child; call TriggerVignette(); yield; assert HasPlayed is true and echoRoot is active.
        /// </summary>
        [UnityTest]
        public IEnumerator MemoryEchoVignette_TriggerSetsHasPlayedAndActivatesRoot()
        {
            // Create the trigger root
            var vignetteGo = new GameObject("MemoryEchoVignette");
            _spawned.Add(vignetteGo);
            vignetteGo.AddComponent<BoxCollider>();
            var vignette = vignetteGo.AddComponent<MemoryEchoVignette>();

            // Create the echo root child (inactive)
            var echoRoot = new GameObject("EchoRoot");
            _spawned.Add(echoRoot);
            echoRoot.transform.SetParent(vignetteGo.transform, false);
            echoRoot.SetActive(false);

            // Wire the echo root via reflection
            var echoRootField = typeof(MemoryEchoVignette).GetField("echoRoot", BindingFlags.NonPublic | BindingFlags.Instance);
            echoRootField?.SetValue(vignette, echoRoot);

            // Set oneShot=true (default behavior)
            var oneShotField = typeof(MemoryEchoVignette).GetField("oneShot", BindingFlags.NonPublic | BindingFlags.Instance);
            oneShotField?.SetValue(vignette, true);

            yield return null;

            // Trigger the vignette
            vignette.TriggerVignette();
            yield return null;

            Assert.IsTrue(vignette.HasPlayed, "MemoryEchoVignette.HasPlayed should be true after TriggerVignette");
            Assert.IsTrue(echoRoot.activeSelf, "Echo root should be activated after trigger");
        }

        /// <summary>
        /// AllyCombatant_TargetsNearestEnemy: create ally with AllyCombatant; create one enemy
        /// with Health + MeleeAttacker at distance 5; tick combat a few times; assert CurrentTarget
        /// is the enemy's Health. Then kill the enemy, tick past retarget interval, assert null.
        /// </summary>
        [UnityTest]
        public IEnumerator AllyCombatant_TargetsNearestEnemy()
        {
            // Components in this minimal scene may log wiring warnings — they are not under test.
            LogAssert.ignoreFailingMessages = true;

            // Create ally
            var allyGo = new GameObject("Ally");
            _spawned.Add(allyGo);
            allyGo.transform.position = Vector3.zero;
            var ally = allyGo.AddComponent<AllyCombatant>();

            // Create enemy: Health + a MeleeAttacker stub. TrainingDummy is the minimal concrete
            // MeleeAttacker — no bladeTip/definition wiring required (Enemy.Awake would LogError).
            var enemyGo = new GameObject("Enemy");
            _spawned.Add(enemyGo);
            enemyGo.transform.position = new Vector3(5f, 0f, 0f);

            var enemyHealth = enemyGo.AddComponent<Health>();
            enemyHealth.Configure(20f);
            enemyGo.AddComponent<TrainingDummy>();

            yield return null;

            // Tick combat a few times to trigger retargeting
            ally.TickCombat(0.1f);
            ally.TickCombat(0.1f);
            ally.TickCombat(0.1f);
            ally.TickCombat(1.5f);  // Exceed retarget interval

            Assert.AreEqual(enemyHealth, ally.CurrentTarget, "AllyCombatant should target the nearest enemy");

            // Kill the enemy
            enemyHealth.ApplyDamage(new DamageInfo(100f, Vector3.zero, Vector3.zero, null));
            yield return null;

            // Tick past retarget interval to clear the target
            ally.TickCombat(1.5f);

            Assert.IsNull(ally.CurrentTarget, "AllyCombatant should clear target when enemy dies");
        }
    }
}
