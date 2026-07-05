using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="TimeScaleComposition"/> — the pure MIN-composition logic behind
    /// <see cref="TimeScaleArbiter"/> — without touching real <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c>.
    /// Also expresses the two regression scenarios the arbiter fixes (Overdrive-vs-deflect overlap)
    /// purely against this composition, deriving fixedDeltaTime as <c>baseline * EffectiveScale</c> the
    /// same way the arbiter does.
    /// </summary>
    public class TimeScaleCompositionTests
    {
        private const float Baseline90Hz = 1f / 90f;

        [Test]
        public void NoRequests_EffectiveScaleIsOne()
        {
            var composition = new TimeScaleComposition();
            Assert.AreEqual(1f, composition.EffectiveScale);
        }

        [Test]
        public void SingleRequest_EffectiveScaleIsThatRequest()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            Assert.AreEqual(0.35f, composition.EffectiveScale, 1e-6f);
        }

        [Test]
        public void TwoOverlappingRequests_MinWins()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);
            Assert.AreEqual(0.25f, composition.EffectiveScale, 1e-6f);
        }

        [Test]
        public void ReleasingOne_RestoresTheOther()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);

            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo);

            Assert.AreEqual(0.35f, composition.EffectiveScale, 1e-6f);
        }

        [Test]
        public void ReleasingBoth_RestoresEffectiveScaleOfOne()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);

            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo);
            composition.ClearRequest(TimeScaleChannel.Overdrive);

            Assert.AreEqual(1f, composition.EffectiveScale);
        }

        [Test]
        public void ReEntrantSameChannelUpdate_ReplacesNotStacks()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.5f); // e.g. re-activation with a tuned value

            Assert.AreEqual(0.5f, composition.EffectiveScale, 1e-6f);

            composition.ClearRequest(TimeScaleChannel.Overdrive);
            Assert.AreEqual(1f, composition.EffectiveScale, "A single ClearRequest must fully release the channel — no stacked leftover entry.");
        }

        [Test]
        public void ClearRequest_UnknownChannel_IsNoOp()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo); // never set

            Assert.AreEqual(0.35f, composition.EffectiveScale, 1e-6f);
        }

        // ---- Regression (a): deflect's short RestoreTime() must not kill an Overdrive burst early. ----

        [Test]
        public void Regression_DeflectDuringOverdriveBurst_ThenDeflectEnds_OverdriveScaleSurvives()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f); // burst running
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f); // deflect fires mid-burst
            Assert.AreEqual(0.25f, composition.EffectiveScale, 1e-6f, "Deflect's deeper slow-mo should win while both are active.");

            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo); // deflect's 0.15s window ends

            Assert.AreEqual(0.35f, composition.EffectiveScale, 1e-6f,
                "Overdrive's burst must resume its own scale, not snap to 1, when deflect's slow-mo ends first.");
        }

        // ---- Regression (b): activating Overdrive while deflect slow-mo is active must not corrupt the
        // baseline fixedDeltaTime, and releasing both (in either order) must land back on the exact
        // baseline — never a value derived from an already-scaled "original".

        [Test]
        public void Regression_OverdriveActivatedDuringDeflect_ReleaseDeflectThenOverdrive_DerivedFixedDeltaTimeReturnsToBaselineExactly()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f); // deflect slow-mo already active
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f); // Overdrive activates mid-slow-mo

            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo);
            composition.ClearRequest(TimeScaleChannel.Overdrive);

            float derivedFixedDeltaTime = Baseline90Hz * composition.EffectiveScale;
            Assert.AreEqual(1f, composition.EffectiveScale);
            Assert.AreEqual(Baseline90Hz, derivedFixedDeltaTime, 1e-9f);
        }

        [Test]
        public void Regression_OverdriveActivatedDuringDeflect_ReleaseOverdriveThenDeflect_DerivedFixedDeltaTimeReturnsToBaselineExactly()
        {
            var composition = new TimeScaleComposition();
            composition.SetRequest(TimeScaleChannel.DeflectSlowMo, 0.25f);
            composition.SetRequest(TimeScaleChannel.Overdrive, 0.35f);

            composition.ClearRequest(TimeScaleChannel.Overdrive);
            composition.ClearRequest(TimeScaleChannel.DeflectSlowMo);

            float derivedFixedDeltaTime = Baseline90Hz * composition.EffectiveScale;
            Assert.AreEqual(1f, composition.EffectiveScale);
            Assert.AreEqual(Baseline90Hz, derivedFixedDeltaTime, 1e-9f);
        }
    }
}
