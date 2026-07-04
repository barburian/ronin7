using NUnit.Framework;
using Ronin7.Combat;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Guards the pure math behind Sunder Beat: parry-quality falloff, streak progression
    /// (late parries don't reset), and the resulting flow multiplier.</summary>
    public class ParryTimingTests
    {
        [Test]
        public void ParryQuality_AtZeroElapsed_IsOne()
        {
            Assert.AreEqual(1f, ParryTiming.ParryQuality(0f, 0.12f), 1e-5f);
        }

        [Test]
        public void ParryQuality_AtOrPastWindow_IsZero()
        {
            Assert.AreEqual(0f, ParryTiming.ParryQuality(0.12f, 0.12f));
            Assert.AreEqual(0f, ParryTiming.ParryQuality(0.5f, 0.12f));
        }

        [Test]
        public void ParryQuality_MidWindow_IsPartial()
        {
            Assert.AreEqual(0.5f, ParryTiming.ParryQuality(0.06f, 0.12f), 1e-5f);
        }

        [Test]
        public void ParryQuality_NonPositiveWindow_IsZero()
        {
            Assert.AreEqual(0f, ParryTiming.ParryQuality(0f, 0f));
            Assert.AreEqual(0f, ParryTiming.ParryQuality(0f, -1f));
        }

        [Test]
        public void NextStreak_PositiveQuality_Increments()
        {
            Assert.AreEqual(1, ParryTiming.NextStreak(0, 0.5f, 5));
        }

        [Test]
        public void NextStreak_CapsAtMaxStreak()
        {
            Assert.AreEqual(5, ParryTiming.NextStreak(5, 1f, 5));
        }

        [Test]
        public void NextStreak_ZeroQuality_LeavesStreakUnchanged()
        {
            // Late parry: does NOT reset the streak, unlike a miss or a hit taken.
            Assert.AreEqual(3, ParryTiming.NextStreak(3, 0f, 5));
        }

        [Test]
        public void FlowMultiplier_MatchesFormula()
        {
            Assert.AreEqual(1f, ParryTiming.FlowMultiplier(0, 0.08f), 1e-5f);
            Assert.AreEqual(1.4f, ParryTiming.FlowMultiplier(5, 0.08f), 1e-5f);
        }

        // ---- IsClash (G4 Blade Clash: a genuine mutual clash needs BOTH blades hitting the threshold). ----

        [Test]
        public void IsClash_BothAboveThreshold_IsTrue()
        {
            Assert.IsTrue(ParryTiming.IsClash(5f, 5f, 4.5f));
        }

        [Test]
        public void IsClash_PlayerBelowThreshold_IsFalse()
        {
            Assert.IsFalse(ParryTiming.IsClash(4f, 5f, 4.5f));
        }

        [Test]
        public void IsClash_EnemyBelowThreshold_IsFalse()
        {
            Assert.IsFalse(ParryTiming.IsClash(5f, 4f, 4.5f));
        }

        [Test]
        public void IsClash_BothBelowThreshold_IsFalse()
        {
            Assert.IsFalse(ParryTiming.IsClash(2f, 2f, 4.5f));
        }

        [Test]
        public void IsClash_ExactlyAtThreshold_IsTrue()
        {
            Assert.IsTrue(ParryTiming.IsClash(4.5f, 4.5f, 4.5f));
        }
    }
}
