using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure kill-streak/heal math behind G5 "Adrenaline Flow"
    /// (<see cref="AdrenalineFlow.RegisterKill"/>/<see cref="AdrenalineFlow.HealForStreak"/>).
    /// Mirrors <c>HeartbeatPulserTests</c>' style: caller supplies "now" explicitly.
    /// </summary>
    public class AdrenalineFlowTests
    {
        [Test]
        public void RegisterKill_WithinWindow_IncrementsStreak()
        {
            var state = AdrenalineFlow.RegisterKill(new KillStreakState(0, 0f), 0f, 4f);
            Assert.AreEqual(1, state.Count);

            state = AdrenalineFlow.RegisterKill(state, 2f, 4f);
            Assert.AreEqual(2, state.Count);
            Assert.AreEqual(2f, state.LastKillTime);
        }

        [Test]
        public void RegisterKill_WithinWindow_CapsAtMaxStreak()
        {
            var state = new KillStreakState(6, 10f);
            state = AdrenalineFlow.RegisterKill(state, 11f, 4f);
            Assert.AreEqual(6, state.Count);
        }

        [Test]
        public void RegisterKill_ExpiredWindow_ResetsToOne()
        {
            var state = new KillStreakState(4, 0f);
            state = AdrenalineFlow.RegisterKill(state, 10f, 4f);
            Assert.AreEqual(1, state.Count);
            Assert.AreEqual(10f, state.LastKillTime);
        }

        [Test]
        public void HealForStreak_BelowMinStreak_ReturnsZero()
        {
            float heal = AdrenalineFlow.HealForStreak(1, 0.03f, 100f, 2);
            Assert.AreEqual(0f, heal);
        }

        [Test]
        public void HealForStreak_Formula()
        {
            float heal = AdrenalineFlow.HealForStreak(3, 0.03f, 100f, 2);
            Assert.AreEqual(9f, heal, 1e-4f);
        }

        [Test]
        public void HealForStreak_ExactlyMinStreak_Heals()
        {
            float heal = AdrenalineFlow.HealForStreak(2, 0.03f, 100f, 2);
            Assert.AreEqual(6f, heal, 1e-4f);
        }
    }
}
