using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP12 HesitantAttacker mechanic (the Shardborn "integration gap"):
    /// damage outside the committed window is mostly refunded (guard up); damage inside the
    /// window lands at full value and fires onWindowExploited; lethal window damage still kills.
    /// </summary>
    public class Ep12MechanicsPlayTests
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

        /// <summary>Build an enemy with Health + HesitantAttacker, autoCycle disabled for determinism.</summary>
        private (Health health, HesitantAttacker attacker) MakeShardborn(float maxHealth = 100f)
        {
            var go = new GameObject("Shardborn");
            _spawned.Add(go);

            var health = go.AddComponent<Health>();
            health.Configure(maxHealth);

            var attacker = go.AddComponent<HesitantAttacker>();
            // Disable timed cycling so the test drives IsCommitted directly.
            typeof(HesitantAttacker)
                .GetField("autoCycle", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(attacker, false);

            // Reflection-wiring serialized refs happens after OnEnable — toggle to re-subscribe.
            attacker.enabled = false;
            attacker.enabled = true;

            return (health, attacker);
        }

        private static DamageInfo Hit(float amount) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, null);

        [UnityTest]
        public IEnumerator GuardUp_DamageIsMostlyRefunded()
        {
            var (health, attacker) = MakeShardborn(100f);
            yield return null;

            attacker.IsCommitted = false;
            health.ApplyDamage(Hit(40f));

            // 40 applied, then 40 * 0.85 = 34 refunded -> net 6 lost -> 94 remaining.
            Assert.AreEqual(94f, health.Current, 0.01f, "Guard-up damage should be mostly refunded");
            Assert.IsTrue(health.IsAlive);
        }

        [UnityTest]
        public IEnumerator CommittedWindow_DamageLandsAndFiresEvent()
        {
            var (health, attacker) = MakeShardborn(100f);
            yield return null;

            int exploited = 0;
            attacker.OnWindowExploited.AddListener(() => exploited++);

            attacker.IsCommitted = true;
            health.ApplyDamage(Hit(40f));

            Assert.AreEqual(60f, health.Current, 0.01f, "Committed-window damage should land at full value");
            Assert.AreEqual(1, exploited, "onWindowExploited should fire once on a window hit");

            // Second window hit should not re-fire the one-time event.
            health.ApplyDamage(Hit(10f));
            Assert.AreEqual(1, exploited, "onWindowExploited is one-shot");
        }

        [UnityTest]
        public IEnumerator CommittedWindow_LethalDamageStillKills()
        {
            var (health, attacker) = MakeShardborn(100f);
            yield return null;

            attacker.IsCommitted = true;
            health.ApplyDamage(Hit(200f));

            Assert.IsFalse(health.IsAlive, "Lethal damage inside the window must remain lethal");
            Assert.AreEqual(0f, health.Current, 0.01f);
        }
    }
}
