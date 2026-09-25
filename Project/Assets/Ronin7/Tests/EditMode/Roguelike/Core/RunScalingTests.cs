using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class RunScalingTests
    {
        [Test]
        public void HealthMultiplier_Depth0Combat_IsOne()
        {
            Assert.AreEqual(1f, RunScaling.HealthMultiplier(0, RoomKind.Combat), 0.001f);
        }

        [Test]
        public void HealthMultiplier_Depth14Boss_IsNearMax()
        {
            Assert.AreEqual(2.6f, RunScaling.HealthMultiplier(14, RoomKind.Boss), 0.05f);
        }

        [Test]
        public void DamageMultiplier_Depth0Combat_IsOne()
        {
            Assert.AreEqual(1f, RunScaling.DamageMultiplier(0, RoomKind.Combat), 0.001f);
        }

        [Test]
        public void DamageMultiplier_Depth14Boss_IsNearMax()
        {
            Assert.AreEqual(1.9f, RunScaling.DamageMultiplier(14, RoomKind.Boss), 0.05f);
        }

        [TestCase(RoomKind.Combat)]
        [TestCase(RoomKind.Elite)]
        [TestCase(RoomKind.Treasure)]
        [TestCase(RoomKind.Forge)]
        [TestCase(RoomKind.Boss)]
        public void HealthMultiplier_MonotonicNonDecreasingWithDepth(RoomKind kind)
        {
            float previous = RunScaling.HealthMultiplier(0, kind);
            for (int depth = 1; depth <= 14; depth++)
            {
                float current = RunScaling.HealthMultiplier(depth, kind);
                Assert.GreaterOrEqual(current, previous, $"depth {depth} kind {kind}");
                previous = current;
            }
        }

        [TestCase(RoomKind.Combat)]
        [TestCase(RoomKind.Elite)]
        [TestCase(RoomKind.Treasure)]
        [TestCase(RoomKind.Forge)]
        [TestCase(RoomKind.Boss)]
        public void DamageMultiplier_MonotonicNonDecreasingWithDepth(RoomKind kind)
        {
            float previous = RunScaling.DamageMultiplier(0, kind);
            for (int depth = 1; depth <= 14; depth++)
            {
                float current = RunScaling.DamageMultiplier(depth, kind);
                Assert.GreaterOrEqual(current, previous, $"depth {depth} kind {kind}");
                previous = current;
            }
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(8)]
        [TestCase(14)]
        public void EnemyCount_Forge_AlwaysZero(int depth)
        {
            RunRng rng = new RunRng(1);
            Assert.AreEqual(0, RunScaling.EnemyCount(depth, RoomKind.Forge, ref rng));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(8)]
        [TestCase(14)]
        public void EnemyCount_Treasure_AlwaysZero(int depth)
        {
            RunRng rng = new RunRng(1);
            Assert.AreEqual(0, RunScaling.EnemyCount(depth, RoomKind.Treasure, ref rng));
        }

        [Test]
        public void EnemyCount_Boss_AlwaysOne()
        {
            RunRng rng = new RunRng(1);
            Assert.AreEqual(1, RunScaling.EnemyCount(4, RoomKind.Boss, ref rng));
            Assert.AreEqual(1, RunScaling.EnemyCount(14, RoomKind.Boss, ref rng));
        }

        [Test]
        public void EnemyCount_Combat_StaysPositive()
        {
            RunRng rng = new RunRng(1);
            for (int depth = 0; depth <= 14; depth++)
            {
                int count = RunScaling.EnemyCount(depth, RoomKind.Combat, ref rng);
                Assert.Greater(count, 0);
            }
        }

        [Test]
        public void EnemyCount_SameSeed_DeterministicSequence()
        {
            RunRng a = new RunRng(555);
            RunRng b = new RunRng(555);

            for (int depth = 0; depth <= 14; depth++)
            {
                Assert.AreEqual(
                    RunScaling.EnemyCount(depth, RoomKind.Combat, ref a),
                    RunScaling.EnemyCount(depth, RoomKind.Combat, ref b));
            }
        }

        [Test]
        public void EchoReward_DeeperNodes_RewardMoreForSameKind()
        {
            int shallow = RunScaling.EchoReward(0, RoomKind.Combat);
            int deep = RunScaling.EchoReward(14, RoomKind.Combat);

            Assert.Greater(deep, shallow);
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(13)]
        public void EchoReward_Forge_IsAlwaysZero_RegardlessOfDepth(int depth)
        {
            // A2.3 regression: baseReward was 0 for Forge but depth was added unconditionally, so a
            // depth-13 Forge paid 13 Echoes for a no-fight heal room. The original test only checked
            // depth 0, where the bug is invisible.
            Assert.AreEqual(0, RunScaling.EchoReward(depth, RoomKind.Forge));
        }

        [Test]
        public void EchoReward_Boss_ExceedsCombatAtSameDepth()
        {
            int combat = RunScaling.EchoReward(4, RoomKind.Combat);
            int boss = RunScaling.EchoReward(4, RoomKind.Boss);

            Assert.Greater(boss, combat);
        }

        [Test]
        public void MaxConcurrentEnemies_IsFour()
        {
            // A2.2/A5.5: pins the VR concurrency cap so a future edit can't silently widen it without
            // a test noticing — the value is load-bearing (no off-screen hit feedback, no camera shake).
            Assert.AreEqual(4, RunScaling.MaxConcurrentEnemies);
        }
    }
}
