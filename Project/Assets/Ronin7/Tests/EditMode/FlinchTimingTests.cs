using NUnit.Framework;
using Ronin7.Enemies;

namespace Ronin7.Tests.EditMode
{
    public class FlinchTimingTests
    {
        [Test] public void IdleHit_Flinches()
            => Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1f, 0f));

        [Test] public void Dead_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(false, false, false, false, true, 1f, 0f));

        [Test] public void AlreadyStaggered_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, true, false, false, true, 1f, 0f));

        [Test] public void ActiveStrike_NoFlinch()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, true, false, true, 1f, 0f));

        [Test] public void Windup_CancelsWhenAllowed()
            => Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, true, true, 1f, 0f));

        [Test] public void Windup_IgnoredWhenCancelDisabled()
            => Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, false, true, false, 1f, 0f));

        [Test] public void ImmunityWindow_BlocksThenExpires()
        {
            Assert.IsFalse(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1.3f, 1.4f));
            Assert.IsTrue(FlinchTiming.ShouldFlinch(true, false, false, false, true, 1.4f, 1.4f));
        }

        [Test] public void TiltEnvelope_ZeroAtEnds_PeakMid()
        {
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(0f), 1e-5f);
            Assert.AreEqual(1f, FlinchTiming.TiltEnvelope(0.5f), 1e-5f);
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(1f), 1e-5f);
            Assert.AreEqual(0f, FlinchTiming.TiltEnvelope(2f), 1e-5f);
        }
    }
}
