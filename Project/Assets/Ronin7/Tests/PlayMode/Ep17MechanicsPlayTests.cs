using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP17 GravityRigController mechanic: a gladiator-pit arena whose gravity
    /// cycles between zero-g and heavy gravity between rounds, applying forces to registered bodies.
    /// </summary>
    public class Ep17MechanicsPlayTests
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

        /// <summary>Build a GravityRigController on a fresh GameObject.</summary>
        private GravityRigController MakeGravityRig()
        {
            var go = new GameObject("GravityRig");
            _spawned.Add(go);
            var rig = go.AddComponent<GravityRigController>();
            return rig;
        }

        /// <summary>Build a Rigidbody on a fresh GameObject.</summary>
        private Rigidbody MakeRigidbody()
        {
            var go = new GameObject("TestBody");
            _spawned.Add(go);
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            return rb;
        }

        [Test]
        public void GravityForState_ReturnsExpectedValues()
        {
            var rig = MakeGravityRig();

            // ZeroG should return 0
            float zeroGGrav = rig.GravityForState(GravityRigController.GravityState.ZeroG);
            Assert.AreEqual(0f, zeroGGrav, "ZeroG should return 0");

            // Heavy should return negative (< 0)
            float heavyGrav = rig.GravityForState(GravityRigController.GravityState.Heavy);
            Assert.Less(heavyGrav, 0f, "Heavy should return negative value");

            // Normal should return negative (< 0)
            float normalGrav = rig.GravityForState(GravityRigController.GravityState.Normal);
            Assert.Less(normalGrav, 0f, "Normal should return negative value");

            // Heavy should be more negative (heavier) than Normal
            Assert.Less(heavyGrav, normalGrav, "Heavy gravity should be stronger (more negative) than Normal gravity");
        }

        [Test]
        public void SetState_FiresStateChangedEvent()
        {
            var rig = MakeGravityRig();

            GravityRigController.GravityState firedState = GravityRigController.GravityState.Normal;
            bool eventFired = false;

            rig.StateChanged += (state) =>
            {
                eventFired = true;
                firedState = state;
            };

            rig.SetState(GravityRigController.GravityState.Heavy);

            Assert.IsTrue(eventFired, "StateChanged event should fire");
            Assert.AreEqual(GravityRigController.GravityState.Heavy, firedState, "Event should pass Heavy state");
            Assert.AreEqual(GravityRigController.GravityState.Heavy, rig.CurrentState, "CurrentState should be Heavy");
        }

        [Test]
        public void Advance_CyclesThroughStates()
        {
            var rig = MakeGravityRig();

            // Track states observed across 3 Advance calls
            var statesObserved = new HashSet<GravityRigController.GravityState>();

            // Call Advance() 3 times and record the state each time
            for (int i = 0; i < 3; i++)
            {
                rig.Advance();
                statesObserved.Add(rig.CurrentState);
            }

            // With default cycle { ZeroG, Heavy, ZeroG }, we should observe both ZeroG and Heavy
            Assert.IsTrue(statesObserved.Contains(GravityRigController.GravityState.ZeroG), "Advance should cycle through ZeroG");
            Assert.IsTrue(statesObserved.Contains(GravityRigController.GravityState.Heavy), "Advance should cycle through Heavy");
        }

        [UnityTest]
        public IEnumerator Tick_AppliesGravityToRegisteredBody()
        {
            var rig = MakeGravityRig();
            var rb = MakeRigidbody();

            // Register the body and set to Heavy gravity
            rig.RegisterBody(rb);
            rb.linearVelocity = Vector3.zero;
            rig.SetState(GravityRigController.GravityState.Heavy);

            yield return null;

            // Record initial velocity
            float initialVelocityY = rb.linearVelocity.y;

            // Tick multiple times with small deltaTime (total < 6f to avoid autoAdvance)
            for (int i = 0; i < 5; i++)
            {
                rig.Tick(0.1f);
            }

            // With Heavy gravity applied, velocity.y should have decreased (become more negative)
            float heavyVelocityY = rb.linearVelocity.y;
            Assert.Less(heavyVelocityY, initialVelocityY, "Heavy gravity should accelerate body downward (decrease velocity.y)");

            yield return null;

            // Now switch to ZeroG
            rig.SetState(GravityRigController.GravityState.ZeroG);
            float zeroGStartVelocityY = rb.linearVelocity.y;

            // Tick again with ZeroG (no gravity applied)
            for (int i = 0; i < 5; i++)
            {
                rig.Tick(0.1f);
            }

            // With ZeroG, velocity.y should remain unchanged (rig applies 0 acceleration)
            float zeroGVelocityY = rb.linearVelocity.y;
            Assert.AreEqual(zeroGStartVelocityY, zeroGVelocityY, 0.001f, "ZeroG should not change velocity.y");
        }
    }
}
