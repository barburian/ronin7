using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    public class HeatLogicTests
    {
        private static HeatLogic Make(float gain = 1f, float decay = 0.1f, float[] thresholds = null)
        {
            return new HeatLogic(gain, decay, thresholds ?? new[] { 0.5f, 1f });
        }

        [Test]
        public void InitialValue_IsZero()
        {
            var logic = Make();
            Assert.AreEqual(0f, logic.Value);
        }

        [Test]
        public void AddDetection_ScalesByGainPerDetection()
        {
            var logic = Make(gain: 0.2f);
            logic.AddDetection(1f);
            Assert.AreEqual(0.2f, logic.Value, 1e-5f);
        }

        [Test]
        public void AddDetection_NonPositiveAmount_IsIgnored()
        {
            var logic = Make();
            logic.AddDetection(0f);
            logic.AddDetection(-1f);
            Assert.AreEqual(0f, logic.Value);
        }

        [Test]
        public void Value_ClampsToOne()
        {
            var logic = Make(gain: 1f);
            logic.AddDetection(5f);
            Assert.AreEqual(1f, logic.Value);
        }

        [Test]
        public void Tick_DecaysValue_ClampedToZero()
        {
            var logic = Make(gain: 1f, decay: 0.5f);
            logic.AddDetection(0.3f);
            logic.Tick(10f); // far more decay than remaining value
            Assert.AreEqual(0f, logic.Value);
        }

        [Test]
        public void Tick_NonPositiveDt_IsIgnored()
        {
            var logic = Make(gain: 1f, decay: 0.5f);
            logic.AddDetection(0.3f);
            logic.Tick(0f);
            logic.Tick(-1f);
            Assert.AreEqual(0.3f, logic.Value, 1e-5f);
        }

        [Test]
        public void CrossingThreshold_FiresOnce()
        {
            var logic = Make(gain: 1f, thresholds: new[] { 0.5f });
            logic.AddDetection(0.6f);

            Assert.IsTrue(logic.ConsumeThresholdCrossing(out int index));
            Assert.AreEqual(0, index);
            Assert.IsFalse(logic.ConsumeThresholdCrossing(out _), "Threshold must not fire twice for one crossing.");
        }

        [Test]
        public void StayingAboveThreshold_DoesNotReFire()
        {
            var logic = Make(gain: 1f, thresholds: new[] { 0.3f });
            logic.AddDetection(0.4f);
            Assert.IsTrue(logic.ConsumeThresholdCrossing(out _));

            logic.AddDetection(0.1f); // still above threshold, no new crossing
            Assert.IsFalse(logic.ConsumeThresholdCrossing(out _));
        }

        [Test]
        public void MultipleThresholds_CrossedInOneJump_ReportInAscendingOrder()
        {
            var logic = Make(gain: 1f, thresholds: new[] { 0.3f, 0.6f, 0.9f });
            logic.AddDetection(1f); // crosses all three at once

            Assert.IsTrue(logic.ConsumeThresholdCrossing(out int first));
            Assert.AreEqual(0, first);
            Assert.IsTrue(logic.ConsumeThresholdCrossing(out int second));
            Assert.AreEqual(1, second);
            Assert.IsTrue(logic.ConsumeThresholdCrossing(out int third));
            Assert.AreEqual(2, third);
            Assert.IsFalse(logic.ConsumeThresholdCrossing(out _));
        }

        [Test]
        public void DecayBelowThreshold_ThenRise_ReArmsAndFiresAgain()
        {
            var logic = Make(gain: 1f, decay: 1f, thresholds: new[] { 0.5f });
            logic.AddDetection(0.6f);
            Assert.IsTrue(logic.ConsumeThresholdCrossing(out _));

            logic.Tick(1f); // decays back to 0
            Assert.AreEqual(0f, logic.Value);

            logic.AddDetection(0.6f); // rises past the threshold again
            Assert.IsTrue(logic.ConsumeThresholdCrossing(out int index));
            Assert.AreEqual(0, index);
        }

        [Test]
        public void ConsumeThresholdCrossing_WithNothingPending_ReturnsFalse()
        {
            var logic = Make();
            Assert.IsFalse(logic.ConsumeThresholdCrossing(out int index));
            Assert.AreEqual(-1, index);
        }
    }
}
