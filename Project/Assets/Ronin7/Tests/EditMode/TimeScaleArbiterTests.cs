using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the thin <c>Time.*</c>-facing applier in <see cref="TimeScaleArbiter"/>: baseline capture
    /// on first use and that <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c> track the composed
    /// <see cref="TimeScaleArbiter.EffectiveScale"/>. The pure MIN-composition logic itself (replace-not-
    /// stack, the Overdrive/deflect regression scenarios) is covered by <c>TimeScaleCompositionTests</c>
    /// without touching real Time state; this class only verifies the arbiter's wiring on top of it.
    /// </summary>
    public class TimeScaleArbiterTests
    {
        private float originalFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            // Reset BEFORE each test too: a baseline captured earlier in the session (another
            // fixture, or an editor play-mode run) differs from the current Time.fixedDeltaTime by
            // float32 write-back rounding (~5e-9), which failed RestoresBaselineExactly's 1e-9
            // assert only in full-suite order. Fresh capture per test makes the fixture
            // order-independent.
            TimeScaleArbiter.ResetForTests();
            originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            TimeScaleArbiter.ResetForTests();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }

        [Test]
        public void SetRequest_AppliesScaleToTimeTimeScale()
        {
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            Assert.AreEqual(0.35f, Time.timeScale, 1e-6f);
            Assert.AreEqual(0.35f, TimeScaleArbiter.EffectiveScale, 1e-6f);
        }

        [Test]
        public void SetRequest_DerivesFixedDeltaTimeFromCapturedBaseline()
        {
            float baseline = Time.fixedDeltaTime;

            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            Assert.AreEqual(baseline * 0.35f, Time.fixedDeltaTime, 1e-6f);
        }

        [Test]
        public void ClearRequest_AfterOnlyRequest_RestoresBaselineExactly()
        {
            float baseline = Time.fixedDeltaTime;
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            TimeScaleArbiter.ClearRequest(TimeScaleChannel.Overdrive);

            Assert.AreEqual(1f, Time.timeScale);
            // Within one Fixed Timestep quantum, not exact: Unity's fixedDeltaTime setter converts
            // the float to a rational tick count (1/141120000s units), so a write-then-read never
            // round-trips bit-exactly — each restore may land one ~7.1e-9 tick away. Asserting
            // tighter than the quantum made this test fail whenever the session's timestep sat on
            // a rounding boundary (reliably after any editor play session).
            Assert.AreEqual(baseline, Time.fixedDeltaTime, 1.5e-8f);
        }

        [Test]
        public void ClearRequest_OneOfTwoOverlapping_LeavesTheOtherApplied()
        {
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            TimeScaleArbiter.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);

            TimeScaleArbiter.ClearRequest(TimeScaleChannel.DeflectSlowMo);

            Assert.AreEqual(0.35f, Time.timeScale, 1e-6f,
                "Releasing deflect's request while Overdrive is still active must not snap timeScale to 1 (regression a).");
        }

        [Test]
        public void ClearRequest_BaselineNeverCorruptedByOverlappingActivation_RestoresExactlyRegardlessOfOrder()
        {
            float baseline = Time.fixedDeltaTime;
            // Deflect slow-mo first (as in regression b), Overdrive activates while it's still scaled.
            TimeScaleArbiter.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);
            TimeScaleArbiter.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            TimeScaleArbiter.ClearRequest(TimeScaleChannel.Overdrive);
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.DeflectSlowMo);

            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(baseline, Time.fixedDeltaTime, 1e-9f,
                "fixedDeltaTime must return to the original baseline, not one derived from an already-scaled snapshot (regression b).");
        }
    }
}
