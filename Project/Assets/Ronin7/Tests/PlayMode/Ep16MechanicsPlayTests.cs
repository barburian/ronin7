using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP16 SelfSeveranceTrigger mechanic: the player (Cipher) cuts his own spinal
    /// locator chip to sever the Dominion's tracking beacon. Tests idempotency and proper event firing.
    /// </summary>
    public class Ep16MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            CampaignState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            CampaignState.Reset();
        }

        /// <summary>Build a GameObject with a SelfSeveranceTrigger component.</summary>
        private SelfSeveranceTrigger MakeSelfSeveranceTrigger()
        {
            var go = new GameObject("SelfSeveranceTrigger");
            _spawned.Add(go);

            var trigger = go.AddComponent<SelfSeveranceTrigger>();
            return trigger;
        }

        [Test]
        public void Sever_SetsFlagAndFiresEvent()
        {
            var trigger = MakeSelfSeveranceTrigger();
            int eventFireCount = 0;

            trigger.onSevered.AddListener(() => eventFireCount++);

            Assert.IsFalse(trigger.HasSevered, "HasSevered should be false before Sever()");
            Assert.IsFalse(CampaignState.HasFlag("ep16_locator_severed"), "Flag should not be set before Sever()");

            trigger.Sever();

            Assert.IsTrue(trigger.HasSevered, "HasSevered should be true after Sever()");
            Assert.IsTrue(CampaignState.HasFlag("ep16_locator_severed"), "Flag should be set after Sever()");
            Assert.AreEqual(1, eventFireCount, "onSevered event should fire exactly once");
        }

        [Test]
        public void Sever_IsIdempotent()
        {
            var trigger = MakeSelfSeveranceTrigger();
            int eventFireCount = 0;

            trigger.onSevered.AddListener(() => eventFireCount++);

            trigger.Sever();
            Assert.AreEqual(1, eventFireCount, "Event should fire on first Sever()");

            trigger.Sever();
            Assert.AreEqual(1, eventFireCount, "Event should not fire again on second Sever()");

            Assert.IsTrue(trigger.HasSevered, "HasSevered should remain true");
            Assert.IsTrue(CampaignState.HasFlag("ep16_locator_severed"), "Flag should remain set");
        }

        [Test]
        public void BeforeSever_FlagNotSet_AndHasSeveredFalse()
        {
            var trigger = MakeSelfSeveranceTrigger();

            Assert.IsFalse(trigger.HasSevered, "Fresh component should have HasSevered false");
            Assert.IsFalse(CampaignState.HasFlag("ep16_locator_severed"), "Flag should not be set on fresh component");
        }
    }
}
