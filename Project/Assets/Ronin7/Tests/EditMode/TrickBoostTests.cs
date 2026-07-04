using NUnit.Framework;
using Ronin7.Ship;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the pure math behind H4 "clean near-miss speed trim": the near-miss band test, streak
    /// progression, and the resulting speed-trim multiplier. The values fed into these tests (band size,
    /// per-stack bonus, streak cap) are the shipped STARTING VALUES — final feel still needs an
    /// in-headset pass (flagged by design); these tests only guard the formulas, not the tuning.
    /// </summary>
    public class TrickBoostTests
    {
        private const float Eps = 1e-5f;

        // ---- IsNearMiss ----

        [Test]
        public void IsNearMiss_ExactlyAtContactRadius_IsTrue()
        {
            // LOCKED SEMANTIC: the contact radius itself is an inclusive lower bound for "near miss",
            // never a contact — genuine overlap requires strictly less than the combined radius.
            Assert.IsTrue(TrickBoostController.IsNearMiss(10f, 10f, 4f));
        }

        [Test]
        public void IsNearMiss_JustInsideBand_IsTrue()
        {
            Assert.IsTrue(TrickBoostController.IsNearMiss(13f, 10f, 4f));
        }

        [Test]
        public void IsNearMiss_AtBandEdge_IsFalse()
        {
            // combinedContactRadius + missBand is an exclusive upper bound.
            Assert.IsFalse(TrickBoostController.IsNearMiss(14f, 10f, 4f));
        }

        [Test]
        public void IsNearMiss_InsideContactRadius_IsFalse()
        {
            Assert.IsFalse(TrickBoostController.IsNearMiss(9f, 10f, 4f));
        }

        [Test]
        public void IsNearMiss_FarBeyondBand_IsFalse()
        {
            Assert.IsFalse(TrickBoostController.IsNearMiss(100f, 10f, 4f));
        }

        // ---- NextStreak ----

        [Test]
        public void NextStreak_BelowCap_Increments()
        {
            Assert.AreEqual(1, TrickBoostController.NextStreak(0, 5));
            Assert.AreEqual(4, TrickBoostController.NextStreak(3, 5));
        }

        [Test]
        public void NextStreak_AtCap_StaysCapped()
        {
            Assert.AreEqual(5, TrickBoostController.NextStreak(5, 5));
        }

        // ---- SpeedMultiplier ----

        [Test]
        public void SpeedMultiplier_ZeroStreak_IsIdentity()
        {
            Assert.AreEqual(1f, TrickBoostController.SpeedMultiplier(0, 0.04f), Eps);
        }

        [Test]
        public void SpeedMultiplier_ScalesLinearlyWithStreak()
        {
            Assert.AreEqual(1.08f, TrickBoostController.SpeedMultiplier(2, 0.04f), Eps);
        }

        [Test]
        public void SpeedMultiplier_MaxStreak_MatchesShippedDefaults()
        {
            // Shipped defaults: perStack = 0.04, maxStreak = 5 -> 1 + 5 * 0.04 = 1.2.
            Assert.AreEqual(1.2f, TrickBoostController.SpeedMultiplier(5, 0.04f), Eps);
        }
    }
}
