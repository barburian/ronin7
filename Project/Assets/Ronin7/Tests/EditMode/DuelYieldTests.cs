using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class DuelYieldTests
    {
        [Test]
        public void ShouldYield_ThresholdLogic()
        {
            // current <= 0 or current / max <= threshold → true
            // (25, 100, 0.25) → 25/100 = 0.25, at threshold → true
            Assert.IsTrue(DuelYield.ShouldYield(25f, 100f, 0.25f));

            // (26, 100, 0.25) → 26/100 = 0.26, above threshold → false
            Assert.IsFalse(DuelYield.ShouldYield(26f, 100f, 0.25f));

            // (0, 100, 0.25) → at zero → true
            Assert.IsTrue(DuelYield.ShouldYield(0f, 100f, 0.25f));

            // (-5, 100, 0.25) → negative is below zero → true
            Assert.IsTrue(DuelYield.ShouldYield(-5f, 100f, 0.25f));

            // (50, 0, 0.25) → max <= 0 → false (invalid state, no yield)
            Assert.IsFalse(DuelYield.ShouldYield(50f, 0f, 0.25f));
        }

        [Test]
        public void StateMachine_InitialStateIsFighting()
        {
            var go = new GameObject("DuelYield");
            var duelYield = go.AddComponent<DuelYield>();

            Assert.AreEqual(DuelYield.DuelState.Fighting, duelYield.State);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ForceYield_TransitionsToYieldedAndInvokesEvent()
        {
            var go = new GameObject("DuelYield");
            var duelYield = go.AddComponent<DuelYield>();

            int onYieldedCount = 0;
            int onAcceptedCount = 0;

            // onYielded/onAccepted are public UnityEvents (builders wire persistent listeners).
            duelYield.onYielded.AddListener(() => onYieldedCount++);
            duelYield.onAccepted.AddListener(() => onAcceptedCount++);

            duelYield.ForceYield();

            Assert.AreEqual(DuelYield.DuelState.Yielded, duelYield.State, "State should be Yielded after ForceYield");
            Assert.AreEqual(1, onYieldedCount, "onYielded should be invoked once");
            Assert.AreEqual(0, onAcceptedCount, "onAccepted should not be invoked");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ForceYield_DoesNotFireTwice()
        {
            var go = new GameObject("DuelYield");
            var duelYield = go.AddComponent<DuelYield>();

            int onYieldedCount = 0;
            duelYield.onYielded.AddListener(() => onYieldedCount++);

            duelYield.ForceYield();
            duelYield.ForceYield();

            Assert.AreEqual(1, onYieldedCount, "onYielded should only fire once, even if ForceYield is called twice");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ForceAccept_TransitionsToAcceptedAndInvokesEvent()
        {
            var go = new GameObject("DuelYield");
            var duelYield = go.AddComponent<DuelYield>();

            int onAcceptedCount = 0;
            duelYield.onAccepted.AddListener(() => onAcceptedCount++);

            duelYield.ForceAccept();

            Assert.AreEqual(DuelYield.DuelState.Accepted, duelYield.State, "State should be Accepted after ForceAccept");
            Assert.AreEqual(1, onAcceptedCount, "onAccepted should be invoked once");

            Object.DestroyImmediate(go);
        }
    }
}
