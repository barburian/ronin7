using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// Exercises the EP26 "The Requiem Protocol" mechanic:
    ///  - ConfessorLink: the confessor-link duel of wills, tracking hunger rise/decay based on
    ///    link activity and restrain status. If hunger reaches the consume threshold, Cipher is
    ///    consumed (fail state); winning below the threshold means Cipher refused the hunger.
    /// </summary>
    public class Ep26MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>Create a ConfessorLink on a fresh GameObject for test control.</summary>
        private ConfessorLink MakeConfessorLink()
        {
            var go = new GameObject("ConfessorLink");
            _spawned.Add(go);
            var link = go.AddComponent<ConfessorLink>();
            // A [Test] advances no frames, so Update never auto-ticks — we drive Tick manually.
            return link;
        }

        // ---- ConfessorLink ----

        [Test]
        public void Hunger_StartsAtZero_AndDoesNotRiseWhileLinkInactive()
        {
            var link = MakeConfessorLink();

            Assert.AreEqual(0f, link.Hunger, 1e-3f, "Hunger should start at zero.");
            Assert.IsFalse(link.LinkActive, "Link should start inactive.");
            Assert.IsFalse(link.IsConsumed, "IsConsumed should be false when hunger is zero.");

            // Tick a few times while link is inactive: hunger stays 0.
            for (int i = 0; i < 5; i++)
            {
                link.Tick(0.1f);
            }

            Assert.AreEqual(0f, link.Hunger, 1e-3f, "Hunger must stay at zero while link is inactive.");
            Assert.IsFalse(link.IsConsumed, "IsConsumed should remain false.");
        }

        [Test]
        public void Hunger_RisesWhileLinkActiveAndUnrestrained()
        {
            var link = MakeConfessorLink();

            link.SetLinkActive(true);
            Assert.IsTrue(link.LinkActive, "Link should be active.");
            Assert.IsFalse(link.Restrained, "Should start unrestrained.");

            // Tick several times: hunger should rise by riseRate * dt.
            link.Tick(1f);
            Assert.Greater(link.Hunger, 0f, "Hunger should be positive after active, unrestrained Tick.");

            float afterFirstTick = link.Hunger;
            link.Tick(1f);
            Assert.Greater(link.Hunger, afterFirstTick, "Hunger should continue rising with additional Ticks.");
        }

        [Test]
        public void Hunger_DecaysWhileRestrained()
        {
            var link = MakeConfessorLink();

            // Raise hunger first via active + unrestrained ticks.
            link.SetLinkActive(true);
            link.Tick(2f);
            Assert.Greater(link.Hunger, 0f, "Hunger should be raised.");
            float raised = link.Hunger;

            // Now restrain and tick: hunger should decay.
            link.Restrain(true);
            Assert.IsTrue(link.Restrained, "Restrain flag should be set.");
            link.Tick(1f);

            Assert.Less(link.Hunger, raised, "Hunger should decay while restrained.");
        }

        [Test]
        public void Feed_SpikesHunger()
        {
            var link = MakeConfessorLink();

            link.SetLinkActive(true);
            float before = link.Hunger;
            link.Feed(0.5f);

            Assert.AreEqual(before + 0.5f, link.Hunger, 1e-3f, "Feed(0.5) should spike hunger by 0.5.");

            // Feed cannot exceed consumeThreshold.
            link.Feed(1f);
            Assert.LessOrEqual(link.Hunger, 1f, "Hunger must be clamped at consumeThreshold.");
        }

        [Test]
        public void Hunger_ReachesThreshold_SetsIsConsumed()
        {
            var link = MakeConfessorLink();

            link.SetLinkActive(true);
            Assert.IsFalse(link.IsConsumed, "Should not be consumed initially.");

            link.Feed(1f);
            Assert.IsTrue(link.IsConsumed, "IsConsumed should be true when hunger reaches threshold.");
            Assert.AreEqual(1f, link.Hunger, 1e-3f, "Hunger should be clamped at consumeThreshold.");
        }

        [Test]
        public void Resolve_SetsRefusedTrue_WhenBelowThreshold()
        {
            var link = MakeConfessorLink();

            // Keep hunger low.
            link.SetLinkActive(true);
            link.Tick(0.1f); // Minimal rise, well below threshold.
            Assert.Less(link.Hunger, 1f, "Hunger should be below threshold.");
            Assert.IsFalse(link.IsConsumed);

            link.Resolve();
            Assert.IsTrue(link.Resolved, "Resolved should be set.");
            Assert.IsTrue(link.Refused, "Refused should be true when below threshold.");
        }

        [Test]
        public void Resolve_SetsRefusedFalse_WhenConsumed()
        {
            var link = MakeConfessorLink();

            // Feed past the threshold.
            link.SetLinkActive(true);
            link.Feed(1f);
            Assert.IsTrue(link.IsConsumed, "Should be consumed.");

            link.Resolve();
            Assert.IsTrue(link.Resolved, "Resolved should be set.");
            Assert.IsFalse(link.Refused, "Refused should be false when consumed.");
        }
    }
}
