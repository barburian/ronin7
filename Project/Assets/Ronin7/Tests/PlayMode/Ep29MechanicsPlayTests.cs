using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP29 LeashBreakController's conviction meter and leash-break event:
    /// evidence reads drain conviction, and once conviction falls to or below the break threshold,
    /// the leash breaks (one-shot event fires). These need MonoBehaviour lifecycle (Awake sets
    /// conviction to startingConviction), so they live in PlayMode. The state logic (meter drain,
    /// break threshold) is tested here.
    /// </summary>
    public class Ep29MechanicsPlayTests
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
        /// Build a LeashBreakController on one active GameObject (the "rig").
        /// Call inside a [UnityTest] and yield return null so LeashBreakController.Awake()
        /// sets conviction = startingConviction.
        /// </summary>
        private LeashBreakController MakeController()
        {
            var go = new GameObject("LeashRig");
            _spawned.Add(go);
            var controller = go.AddComponent<LeashBreakController>();
            controller.AutoAdvance = false; // deterministic: drive ReadEvidence and Clear by hand
            return controller;
        }

        [UnityTest]
        public IEnumerator Inactive_StartsAtFullConvictionUnbroken()
        {
            var controller = MakeController();
            yield return null;

            Assert.AreEqual(1f, controller.Conviction, 0.001f, "Conviction should start at 1.0 after Awake.");
            Assert.IsFalse(controller.IsBroken, "IsBroken should be false on inactive controller.");
        }

        [UnityTest]
        public IEnumerator ReadEvidence_DrainsConviction()
        {
            var controller = MakeController();
            yield return null;

            float before = controller.Conviction;
            controller.ReadEvidence();

            Assert.Less(controller.Conviction, before, "Conviction should decrease after ReadEvidence.");
        }

        [UnityTest]
        public IEnumerator EnoughReads_BreaksLeashOnce()
        {
            var controller = MakeController();
            yield return null;

            // Track how many times onLeashBreak fires
            int breakCount = 0;
            controller.onLeashBreak.AddListener(() => breakCount++);

            // Call ReadEvidence repeatedly until conviction crosses the default 0.25 threshold
            for (int i = 0; i < 5; i++)
            {
                controller.ReadEvidence();
            }

            Assert.IsTrue(controller.IsBroken, "IsBroken should be true after conviction drains to break threshold.");
            Assert.AreEqual(1, breakCount, "onLeashBreak should fire exactly once.");

            // Verify it doesn't fire again on subsequent reads
            controller.ReadEvidence();
            Assert.AreEqual(1, breakCount, "onLeashBreak should not fire a second time.");
        }

        [UnityTest]
        public IEnumerator Clear_ResetsConvictionAndBreak()
        {
            var controller = MakeController();
            yield return null;

            // Break the leash
            for (int i = 0; i < 5; i++)
            {
                controller.ReadEvidence();
            }
            Assert.IsTrue(controller.IsBroken, "Leash should be broken before Clear.");

            // Reset
            controller.Clear();

            Assert.AreEqual(1f, controller.Conviction, 0.001f, "Conviction should reset to 1.0 after Clear.");
            Assert.IsFalse(controller.IsBroken, "IsBroken should be false after Clear.");
        }
    }
}
