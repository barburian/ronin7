using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="HeartbeatPulser"/>'s pure cadence math behind the low-health heartbeat.
    /// Mirrors <c>EchoCalloutSelectorTests</c>' style: caller supplies "now" explicitly.
    /// </summary>
    public class HeartbeatPulserTests
    {
        private static HeartbeatPulser MakePulser() =>
            new HeartbeatPulser(lowHealthFraction: 0.35f, minInterval: 0.35f, maxInterval: 1f,
                minAmplitude: 0.15f, maxAmplitude: 0.6f);

        [Test]
        public void HealthAboveThreshold_ReturnsFalseAndResetsCadence()
        {
            var pulser = MakePulser();

            // Schedule a beat that (if not reset) wouldn't elapse until ~t=0.54.
            Assert.IsTrue(pulser.TryBeat(0.1f, 0f, out _));

            bool result = pulser.TryBeat(0.5f, 0.1f, out float amplitude);
            Assert.IsFalse(result);
            Assert.AreEqual(0f, amplitude);

            // Cadence reset proven: dropping low again fires immediately even though the earlier
            // scheduled beat hadn't elapsed yet.
            Assert.IsTrue(pulser.TryBeat(0.1f, 0.1f, out _));
        }

        [Test]
        public void HealthAtZero_ReturnsFalse()
        {
            var pulser = MakePulser();
            Assert.IsFalse(pulser.TryBeat(0f, 0f, out float amplitude));
            Assert.AreEqual(0f, amplitude);
        }

        [Test]
        public void FirstDropBelowThreshold_FiresImmediately()
        {
            var pulser = MakePulser();
            Assert.IsTrue(pulser.TryBeat(0.2f, 5f, out float amplitude));
            Assert.Greater(amplitude, 0f);
        }

        [Test]
        public void RespectsMinimumIntervalBetweenBeats()
        {
            var pulser = MakePulser();
            Assert.IsTrue(pulser.TryBeat(0.01f, 0f, out _));    // schedules ~0.37s out
            Assert.IsFalse(pulser.TryBeat(0.01f, 0.2f, out _)); // too soon
            Assert.IsTrue(pulser.TryBeat(0.01f, 0.4f, out _));  // interval elapsed
        }

        [Test]
        public void AmplitudeAndIntervalScaleWithUrgency()
        {
            var mild = MakePulser();
            var severe = MakePulser();

            mild.TryBeat(0.30f, 0f, out float ampMild);     // low urgency
            severe.TryBeat(0.02f, 0f, out float ampSevere); // high urgency
            Assert.Greater(ampSevere, ampMild);

            // Mild's next-interval (~0.91s) hasn't elapsed by t=0.36...
            Assert.IsFalse(mild.TryBeat(0.30f, 0.36f, out _));
            // ...but severe's next-interval (~0.39s) has elapsed by t=0.40.
            Assert.IsTrue(severe.TryBeat(0.02f, 0.40f, out _));
        }

        [Test]
        public void RecoveryAboveThresholdThenDropAgain_BeatsImmediately()
        {
            var pulser = MakePulser();
            Assert.IsTrue(pulser.TryBeat(0.1f, 0f, out _));     // schedules a future beat
            Assert.IsFalse(pulser.TryBeat(0.5f, 0.05f, out _)); // recovers: resets cadence
            Assert.IsTrue(pulser.TryBeat(0.1f, 0.06f, out _));  // drops again: fires immediately
        }
    }
}
