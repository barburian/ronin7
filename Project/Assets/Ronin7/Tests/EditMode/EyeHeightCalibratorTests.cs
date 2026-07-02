using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    public class EyeHeightCalibratorTests
    {
        [Test]
        public void ComputeOffset_SeatedPlayer_RaisesToTarget()
        {
            // Head tracked at 1.1m, want 1.6m → raise by 0.5m.
            Assert.AreEqual(0.5f, XREyeHeightCalibrator.ComputeOffset(1.1f, 1.6f), 1e-4f);
        }

        [Test]
        public void ComputeOffset_AlreadyAtTarget_NoShift()
        {
            Assert.AreEqual(0f, XREyeHeightCalibrator.ComputeOffset(1.6f, 1.6f), 1e-4f);
        }

        [Test]
        public void ComputeOffset_TallPlayer_LowersBelowZero()
        {
            // Head at 1.8m, target 1.6m → lower by 0.2m.
            Assert.AreEqual(-0.2f, XREyeHeightCalibrator.ComputeOffset(1.8f, 1.6f), 1e-4f);
        }

        [Test]
        public void ComputeOffset_IsIdempotent_AcrossRepeatedCalibration()
        {
            const float target = 1.6f;
            const float rawHead = 1.1f; // physical head height, constant for a given player

            // First press: offset measured from a zero starting offset.
            float offset1 = XREyeHeightCalibrator.ComputeOffset(rawHead, target);

            // Second press: the calibrator removes the applied offset before measuring, so it
            // feeds the same rawHead back in — the result must not drift.
            float offset2 = XREyeHeightCalibrator.ComputeOffset(rawHead, target);

            Assert.AreEqual(offset1, offset2, 1e-4f);
        }
    }
}
