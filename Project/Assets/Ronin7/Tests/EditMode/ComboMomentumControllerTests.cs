using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Locks in Follow-Through's combo-chain scheme — see
    /// <see cref="ComboMomentumController"/>'s class doc for the full rationale: same-target hits
    /// punish the chain to 0, while expired-window hits start a fresh Count-1 chain (not 0).</summary>
    public class ComboMomentumControllerTests
    {
        private const float Window = 1.2f;
        private const int MaxCombo = 4;

        [Test]
        public void RegisterHit_FirstEverHit_StartsChainAtOne()
        {
            var result = ComboMomentumController.RegisterHit(default, targetId: 1, now: 0f, Window, MaxCombo);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result.LastTargetId);
        }

        [Test]
        public void RegisterHit_DistinctTargetWithinWindow_Increments()
        {
            var first = ComboMomentumController.RegisterHit(default, 1, 0f, Window, MaxCombo);
            var second = ComboMomentumController.RegisterHit(first, 2, 0.5f, Window, MaxCombo);
            Assert.AreEqual(2, second.Count);
            Assert.AreEqual(2, second.LastTargetId);
        }

        [Test]
        public void RegisterHit_DistinctTargets_CapsAtMaxCombo()
        {
            var state = ComboMomentumController.RegisterHit(default, 1, 0f, Window, MaxCombo);
            for (int i = 2; i <= 10; i++)
                state = ComboMomentumController.RegisterHit(state, i, i * 0.1f, Window, MaxCombo);
            Assert.AreEqual(MaxCombo, state.Count);
        }

        [Test]
        public void RegisterHit_SameTargetConsecutive_ResetsCountToZero()
        {
            var first = ComboMomentumController.RegisterHit(default, 1, 0f, Window, MaxCombo);
            var second = ComboMomentumController.RegisterHit(first, 2, 0.3f, Window, MaxCombo); // Count 2
            var third = ComboMomentumController.RegisterHit(second, 2, 0.5f, Window, MaxCombo); // same target (2) again
            Assert.AreEqual(0, third.Count);
            Assert.AreEqual(2, third.LastTargetId);
        }

        [Test]
        public void RegisterHit_WindowExpired_StartsFreshChainAtOne_NotZero()
        {
            var first = ComboMomentumController.RegisterHit(default, 1, 0f, Window, MaxCombo);
            var second = ComboMomentumController.RegisterHit(first, 2, 0.3f, Window, MaxCombo); // Count 2
            // 0.01f past the window: float rounding at the exact boundary (0.3f + 1.2f == 1.5f
            // yields an elapsed delta of 1.1999999f) would otherwise land a hair under it.
            var afterExpiry = ComboMomentumController.RegisterHit(second, 3, 0.3f + Window + 0.01f, Window, MaxCombo);
            Assert.AreEqual(1, afterExpiry.Count);
            Assert.AreEqual(3, afterExpiry.LastTargetId);
        }

        [Test]
        public void MultiplierForCombo_MatchesFormula()
        {
            Assert.AreEqual(1.0f, ComboMomentumController.MultiplierForCombo(0, 0.15f), 1e-5f);
            Assert.AreEqual(1.6f, ComboMomentumController.MultiplierForCombo(4, 0.15f), 1e-5f);
        }
    }
}
