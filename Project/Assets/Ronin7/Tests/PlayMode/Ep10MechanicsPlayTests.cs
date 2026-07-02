using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    public class Ep10MechanicsPlayTests
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
        /// ShockMine_DamagesPlayerAndDisarms: create a player rig (CharacterController + Health),
        /// add ShockMine with trigger collider, set armDelay=0, toggle enabled, move player into
        /// trigger, yield, assert Health dropped by damage and mine.Detonated is true. Re-entering
        /// the trigger should do nothing further.
        /// </summary>
        [UnityTest]
        public IEnumerator ShockMine_DamagesPlayerAndDisarms()
        {
            // Create the player rig (CharacterController identifies the player)
            var playerGo = new GameObject("Player");
            _spawned.Add(playerGo);
            playerGo.transform.position = Vector3.zero;
            var playerController = playerGo.AddComponent<CharacterController>();
            playerController.height = 1.8f;
            playerController.center = Vector3.up * 0.9f;
            var playerHealth = playerGo.AddComponent<Health>();
            playerHealth.Configure(100f);

            // Create the ShockMine (off to the side)
            var mineGo = new GameObject("ShockMine");
            _spawned.Add(mineGo);
            mineGo.transform.position = new Vector3(2f, 0f, 0f);
            var mine = mineGo.AddComponent<ShockMine>();
            var triggerCollider = mineGo.AddComponent<SphereCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.radius = 1f;
            // A kinematic Rigidbody on the trigger volume is required for OnTriggerEnter to fire
            // against a teleported CharacterController (a Rigidbody-less trigger sends no message).
            var mineBody = mineGo.AddComponent<Rigidbody>();
            mineBody.isKinematic = true;
            mineBody.useGravity = false;

            // Wire armDelay=0 via reflection
            var armDelayField = typeof(ShockMine).GetField("armDelay", BindingFlags.NonPublic | BindingFlags.Instance);
            armDelayField?.SetValue(mine, 0f);

            // Re-toggle to re-run Awake/OnEnable with the new armDelay
            mine.enabled = false;
            mine.enabled = true;

            yield return null;

            // Record initial health
            float initialHealth = playerHealth.Current;

            // Move player into the trigger zone. OnTriggerEnter is dispatched during the physics
            // step, so wait for FixedUpdate(s) rather than a render frame.
            playerGo.transform.position = mineGo.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            // Assert damage was applied and mine detonated
            Assert.Less(playerHealth.Current, initialHealth, "Player health should decrease after mine detonation");
            Assert.IsTrue(mine.Detonated, "Mine should be marked as detonated");

            float healthAfterDetonation = playerHealth.Current;

            // Try to re-enter (should do nothing)
            playerGo.transform.position = mineGo.transform.position + Vector3.forward * 2f;
            yield return null;
            playerGo.transform.position = mineGo.transform.position;
            yield return null;

            Assert.AreEqual(healthAfterDetonation, playerHealth.Current, "Health should not change on re-entry");
        }

        /// <summary>
        /// PatternedDuelist_RefundsRepeatedSameSideHits: create enforcer with Health and PatternedDuelist,
        /// apply two hits from the same side (high X value, right side of enforcer), verify second hit is
        /// refunded; then apply a hit from the opposite side (low X value, left side), verify onPatternBroken
        /// was invoked.
        /// </summary>
        [UnityTest]
        public IEnumerator PatternedDuelist_RefundsRepeatedSameSideHits()
        {
            // Create the enforcer
            var enforcerGo = new GameObject("Enforcer");
            _spawned.Add(enforcerGo);
            enforcerGo.transform.position = Vector3.zero;

            var enforcerHealth = enforcerGo.AddComponent<Health>();
            enforcerHealth.Configure(100f);

            var duelist = enforcerGo.AddComponent<PatternedDuelist>();

            // Wire refundFraction via reflection for testing
            var refundField = typeof(PatternedDuelist).GetField("refundFraction", BindingFlags.NonPublic | BindingFlags.Instance);
            refundField?.SetValue(duelist, 0.85f);

            // Wire onPatternBroken event for verification
            var onPatternBrokenField = typeof(PatternedDuelist).GetField("onPatternBroken", BindingFlags.NonPublic | BindingFlags.Instance);
            var onPatternBrokenEvent = onPatternBrokenField?.GetValue(duelist) as UnityEngine.Events.UnityEvent;

            bool patternBrokenCalled = false;
            if (onPatternBrokenEvent != null)
            {
                onPatternBrokenEvent.AddListener(() => { patternBrokenCalled = true; });
            }

            // Re-toggle to re-run event subscriptions
            duelist.enabled = false;
            duelist.enabled = true;

            yield return null;

            // First hit from the right side (positive X)
            float damageAmount = 20f;
            Vector3 rightSidePoint = enforcerGo.transform.position + Vector3.right * 5f;
            enforcerHealth.ApplyDamage(new DamageInfo(damageAmount, rightSidePoint, Vector3.left, null));
            yield return null;

            float healthAfterFirstHit = enforcerHealth.Current;
            Assert.AreEqual(100f - damageAmount, healthAfterFirstHit, "First hit should deal full damage");

            // Second hit from the same right side (should be refunded)
            float healthBeforeSecondHit = enforcerHealth.Current;
            enforcerHealth.ApplyDamage(new DamageInfo(damageAmount, rightSidePoint, Vector3.left, null));
            yield return null;

            float healthAfterSecondHit = enforcerHealth.Current;
            float expectedDamageSecondHit = damageAmount * (1f - 0.85f); // 15% of full damage
            float expectedHealthAfterSecondHit = healthBeforeSecondHit - expectedDamageSecondHit;
            Assert.AreEqual(expectedHealthAfterSecondHit, healthAfterSecondHit, 0.01f,
                "Second hit from same side should be mostly refunded (85% refund)");

            // Third hit from the left side (opposite side, should break pattern)
            Vector3 leftSidePoint = enforcerGo.transform.position + Vector3.left * 5f;
            enforcerHealth.ApplyDamage(new DamageInfo(damageAmount, leftSidePoint, Vector3.right, null));
            yield return null;

            Assert.IsTrue(patternBrokenCalled, "onPatternBroken should be invoked on side change");
        }

        /// <summary>
        /// PatternedDuelist_StillDiesFromAlternatingHits: verify that PatternedDuelist cannot
        /// survive indefinitely by only refunding repeated-side hits. Alternate sides on each
        /// hit, ensuring each hit deals full damage, until the enforcer dies.
        /// </summary>
        [UnityTest]
        public IEnumerator PatternedDuelist_StillDiesFromAlternatingHits()
        {
            // Create the enforcer with low max health for speed
            var enforcerGo = new GameObject("Enforcer");
            _spawned.Add(enforcerGo);
            enforcerGo.transform.position = Vector3.zero;

            var enforcerHealth = enforcerGo.AddComponent<Health>();
            enforcerHealth.Configure(50f);

            var duelist = enforcerGo.AddComponent<PatternedDuelist>();

            // Re-toggle to run event subscriptions
            duelist.enabled = false;
            duelist.enabled = true;

            yield return null;

            float damagePerHit = 20f;
            Vector3 rightSidePoint = enforcerGo.transform.position + Vector3.right * 5f;
            Vector3 leftSidePoint = enforcerGo.transform.position + Vector3.left * 5f;

            // Alternate sides until dead (max 10 iterations to prevent infinite loops)
            int maxIterations = 10;
            int iteration = 0;
            bool rightSide = true;

            while (enforcerHealth.IsAlive && iteration < maxIterations)
            {
                Vector3 hitPoint = rightSide ? rightSidePoint : leftSidePoint;
                Vector3 direction = rightSide ? Vector3.left : Vector3.right;
                enforcerHealth.ApplyDamage(new DamageInfo(damagePerHit, hitPoint, direction, null));
                rightSide = !rightSide;
                iteration++;
                yield return null;
            }

            Assert.IsFalse(enforcerHealth.IsAlive, "Enforcer should die from alternating hits within bounded iterations");
            Assert.Less(iteration, maxIterations, "Enforcer should die before hitting iteration limit");
        }
    }
}
