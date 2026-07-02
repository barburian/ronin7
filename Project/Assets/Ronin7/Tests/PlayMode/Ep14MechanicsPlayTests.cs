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
    /// Exercises the EP14 EchoHunter mechanic (the hunter drone trained on Cipher's movements):
    /// damage while the drone is predicting a repeated pattern is mostly refunded (guard up);
    /// damage from a novel/varied strike lands at full value and fires onPredictionBroken;
    /// lethal novel damage still kills.
    /// </summary>
    public class Ep14MechanicsPlayTests
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

        /// <summary>Build an enemy with Health + EchoHunter, autoTrack disabled for determinism.</summary>
        private (Health health, EchoHunter attacker) MakeEchoHunter(float maxHealth = 100f)
        {
            var go = new GameObject("EchoHunter");
            _spawned.Add(go);

            var health = go.AddComponent<Health>();
            health.Configure(maxHealth);

            var attacker = go.AddComponent<EchoHunter>();
            // Disable timed cycling so the test drives IsPredicting directly.
            typeof(EchoHunter)
                .GetField("autoTrack", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(attacker, false);

            // Reflection-wiring serialized refs happens after OnEnable — toggle to re-subscribe.
            attacker.enabled = false;
            attacker.enabled = true;

            return (health, attacker);
        }

        private static DamageInfo Hit(float amount) =>
            new DamageInfo(amount, Vector3.zero, Vector3.forward, null);

        [UnityTest]
        public IEnumerator PredictedStrike_DamageIsMostlyRefunded()
        {
            var (health, attacker) = MakeEchoHunter(100f);
            yield return null;

            attacker.IsPredicting = true;
            health.ApplyDamage(Hit(40f));

            // 40 applied, then 40 * 0.85 = 34 refunded -> net 6 lost -> 94 remaining.
            Assert.AreEqual(94f, health.Current, 0.01f, "Predicted damage should be mostly refunded");
            Assert.IsTrue(health.IsAlive);
        }

        [UnityTest]
        public IEnumerator NovelStrike_DamageLandsAndFiresEvent()
        {
            var (health, attacker) = MakeEchoHunter(100f);
            yield return null;

            int predictionBroken = 0;
            attacker.OnPredictionBroken.AddListener(() => predictionBroken++);

            attacker.IsPredicting = false;
            health.ApplyDamage(Hit(40f));

            Assert.AreEqual(60f, health.Current, 0.01f, "Novel-strike damage should land at full value");
            Assert.AreEqual(1, predictionBroken, "OnPredictionBroken should fire once on a novel hit");

            // Second novel hit should not re-fire the one-time event.
            health.ApplyDamage(Hit(10f));
            Assert.AreEqual(1, predictionBroken, "OnPredictionBroken is one-shot");
        }

        [UnityTest]
        public IEnumerator NovelStrike_LethalDamageStillKills()
        {
            var (health, attacker) = MakeEchoHunter(100f);
            yield return null;

            attacker.IsPredicting = false;
            health.ApplyDamage(Hit(200f));

            Assert.IsFalse(health.IsAlive, "Lethal damage from a novel strike must remain lethal");
            Assert.AreEqual(0f, health.Current, 0.01f);
        }
    }
}
