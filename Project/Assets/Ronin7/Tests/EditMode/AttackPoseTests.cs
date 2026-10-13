using NUnit.Framework;
using Ronin7.Enemies;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-15: the windup pose must reach full draw-back before the parry window opens (0.75 x telegraph).</summary>
    public class AttackPoseTests
    {
        [TestCase(0.8f), TestCase(0.85f), TestCase(1.05f)]
        public void WindupPoseProgress_ReachesFullBeforeParryWindow(float telegraph)
        {
            Assert.AreEqual(0f, MeleeAttacker.WindupPoseProgress(0f, telegraph), 1e-6f);
            Assert.AreEqual(1f, MeleeAttacker.WindupPoseProgress(0.75f * telegraph, telegraph), 1e-6f);
            float prev = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float p = MeleeAttacker.WindupPoseProgress(telegraph * i / 100f, telegraph);
                Assert.GreaterOrEqual(p, prev, $"non-monotonic at step {i}");
                Assert.LessOrEqual(p, 1f);
                prev = p;
            }
        }
    }
}
