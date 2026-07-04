using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;

namespace Ronin7.Tests.EditMode
{
    public class DreamPhantomTests
    {
        private GameObject go;
        private Health health;
        private DreamPhantom phantom;

        private void BuildPhantom()
        {
            // Add components to an already-active GameObject so Unity fires Awake/OnEnable
            // synchronously in EditMode: Health.Awake sets Current=Max, and DreamPhantom.OnEnable
            // subscribes to Health's Damaged/Died events. (SetActive(true) does NOT run these in
            // EditMode, so the inactive-then-activate pattern would leave Current=0 / unsubscribed.)
            go = new GameObject();
            health = go.AddComponent<Health>();
            phantom = go.AddComponent<DreamPhantom>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        [Test]
        public void StartsSolid()
        {
            BuildPhantom();
            Assert.AreEqual(DreamPhantom.Phase.Solid, phantom.CurrentPhase);
            Assert.IsFalse(phantom.IsPhased);
        }

        [Test]
        public void SetPhase_Works()
        {
            BuildPhantom();
            phantom.SetPhase(DreamPhantom.Phase.Phased);
            Assert.IsTrue(phantom.IsPhased);
        }

        [Test]
        public void Tick_TogglesPhaseAtInterval()
        {
            BuildPhantom();
            phantom.SetPhase(DreamPhantom.Phase.Solid);

            phantom.Tick(2f);
            Assert.IsTrue(phantom.IsPhased);

            phantom.Tick(2f);
            Assert.IsFalse(phantom.IsPhased);
        }

        private void SetPhaseInterval(float value)
        {
            typeof(DreamPhantom).GetField("phaseInterval", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(phantom, value);
        }

        [Test]
        public void Tick_ZeroPhaseInterval_ReturnsWithoutTogglingOrHanging()
        {
            // phaseInterval == 0 used to spin `while (phaseTimer >= phaseInterval)` forever.
            BuildPhantom();
            SetPhaseInterval(0f);

            phantom.Tick(1f);

            Assert.AreEqual(DreamPhantom.Phase.Solid, phantom.CurrentPhase);
        }

        [Test]
        public void Tick_NegativePhaseInterval_ReturnsWithoutToggling()
        {
            BuildPhantom();
            SetPhaseInterval(-1f);

            phantom.Tick(1f);

            Assert.AreEqual(DreamPhantom.Phase.Solid, phantom.CurrentPhase);
        }

        // NOTE: Tests that exercise Health-event integration (damage refund while Phased, and
        // dissolve-on-death) require MonoBehaviour lifecycle (Awake/OnEnable) which does not run
        // in EditMode. Those live in Ep21MechanicsPlayTests (PlayMode) instead.

        [Test]
        public void Dissolve_IsIdempotent()
        {
            BuildPhantom();

            int counter = 0;
            phantom.onDissolved.AddListener(() => counter++);

            phantom.Dissolve();
            phantom.Dissolve();

            Assert.AreEqual(1, counter);
        }
    }
}
