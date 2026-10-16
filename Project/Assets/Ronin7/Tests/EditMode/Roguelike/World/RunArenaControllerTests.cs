using System;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>
    /// Covers <see cref="RunArenaController.ReleaseCount"/>, the A3.3/A5.5 concurrency-cap pure seam:
    /// direct unit coverage of the formula, plus a full-node simulation gate (A5.8) proving that for
    /// every reachable depth x <see cref="RoomKind"/>, concurrent spawns never exceed
    /// <see cref="RunScaling.MaxConcurrentEnemies"/> and every queued enemy eventually activates.
    /// This is a VR constraint (no off-screen hit feedback, no camera shake), so it gets a gate.
    /// </summary>
    public class RunArenaControllerTests
    {
        [Test]
        public void BossAdds_FiveAdds_AllReleaseAndRoomClears()
        {
            // The boss is alive (1) while its adds release through the cap; adds die between waves.
            var waves = WaveComposer.SplitWaves(5, 3);
            Assert.AreEqual(5, System.Linq.Enumerable.Sum(waves));
            int queued = 5, alive = 1;
            foreach (int size in waves)
            {
                int released = Math.Min(size, RunArenaController.ReleaseCount(alive, queued, RunScaling.MaxConcurrentEnemies));
                Assert.AreEqual(size, released, "no wave is clipped by the cap");
                queued -= released;
                // that wave dies before the next is due (alive <= 1 => boss only)
            }
            Assert.AreEqual(0, queued, "Remaining reaches 0 once the released adds die");
        }

        // ---- ReleaseCount: direct formula coverage ----

        [TestCase(0, 0, 0)] // nothing queued -> nothing to release
        [TestCase(0, 1, 1)]
        [TestCase(0, 4, 4)] // initial activation: min(N, cap)
        [TestCase(0, 5, 4)]
        [TestCase(0, 10, 4)]
        public void ReleaseCount_InitialActivation_IsMinOfQueuedAndCap(int aliveNow, int queuedRemaining, int expected)
        {
            Assert.AreEqual(expected, RunArenaController.ReleaseCount(aliveNow, queuedRemaining, 4));
        }

        [Test]
        public void ReleaseCount_AliveAtCap_ReturnsZero()
        {
            Assert.AreEqual(0, RunArenaController.ReleaseCount(4, 10, 4));
        }

        [Test]
        public void ReleaseCount_ZeroQueuedRemaining_ReturnsZero()
        {
            Assert.AreEqual(0, RunArenaController.ReleaseCount(0, 0, 4));
            Assert.AreEqual(0, RunArenaController.ReleaseCount(2, 0, 4));
        }

        [Test]
        public void ReleaseCount_OneDeathAtCap_ReleasesExactlyOne()
        {
            // The steady-state case: alive was at cap (4), one dies (aliveNow=3), plenty queued.
            Assert.AreEqual(1, RunArenaController.ReleaseCount(3, 10, 4));
        }

        [Test]
        public void ReleaseCount_NeverReturnsNegative()
        {
            for (int alive = 0; alive <= 6; alive++)
                for (int queued = 0; queued <= 6; queued++)
                    Assert.GreaterOrEqual(RunArenaController.ReleaseCount(alive, queued, 4), 0,
                        $"alive={alive} queued={queued}");
        }

        [Test]
        public void ReleaseCount_NeverExceedsQueuedRemaining()
        {
            for (int alive = 0; alive <= 6; alive++)
                for (int queued = 0; queued <= 6; queued++)
                    Assert.LessOrEqual(RunArenaController.ReleaseCount(alive, queued, 4), queued,
                        $"alive={alive} queued={queued}");
        }

        [Test]
        public void ReleaseCount_SequentialDeaths_NeverOverReleases()
        {
            // 6 total, cap 4: activate 4 up front, then kill two in immediate succession — each death
            // callback must complete (and read a freshly-decremented aliveNow) before the next, so
            // exactly one release happens per death, never more.
            const int cap = 4;
            int alive = RunArenaController.ReleaseCount(0, 6, cap);
            int queued = 6 - alive;
            Assert.AreEqual(4, alive);

            alive--; // first death
            int released1 = RunArenaController.ReleaseCount(alive, queued, cap);
            alive += released1; queued -= released1;
            Assert.AreEqual(1, released1);
            Assert.LessOrEqual(alive, cap);

            alive--; // second death
            int released2 = RunArenaController.ReleaseCount(alive, queued, cap);
            alive += released2; queued -= released2;
            Assert.AreEqual(1, released2);
            Assert.LessOrEqual(alive, cap);

            Assert.AreEqual(0, queued);
        }

        // ---- Full-node simulation gate (A5.8) ----

        [Test]
        public void ReleaseCount_FullNodeSimulation_NeverExceedsCap_ForEveryDepthAndKind()
        {
            foreach (RoomKind kind in (RoomKind[])Enum.GetValues(typeof(RoomKind)))
            {
                for (int depth = 0; depth <= 14; depth++)
                {
                    var rng = new RunRng((uint)(depth * 7 + (int)kind + 1));
                    int total = RunScaling.EnemyCount(depth, kind, ref rng);

                    int alive = 0;
                    int remaining = total;
                    int activated = RunArenaController.ReleaseCount(alive, remaining, RunScaling.MaxConcurrentEnemies);
                    alive += activated;
                    remaining -= activated;
                    int maxConcurrentSeen = alive;

                    while (alive > 0)
                    {
                        alive--; // one enemy dies
                        int release = RunArenaController.ReleaseCount(alive, remaining, RunScaling.MaxConcurrentEnemies);
                        alive += release;
                        remaining -= release;
                        maxConcurrentSeen = Mathf.Max(maxConcurrentSeen, alive);
                    }

                    Assert.LessOrEqual(maxConcurrentSeen, RunScaling.MaxConcurrentEnemies,
                        $"depth {depth} kind {kind}: exceeded the concurrency cap");
                    Assert.AreEqual(0, remaining, $"depth {depth} kind {kind}: queue never fully drained");
                }
            }
        }

        // ---- BUG-04: OutOfArena — the out-of-bounds kill test ----

        [TestCase(0f, 0f, 0f, false)]
        [TestCase(12.4f, 0f, 0f, false)]   // inside the 0.5 m margin past a 12 m wall
        [TestCase(12.6f, 0f, 0f, true)]
        [TestCase(-12.6f, 0f, 0f, true)]
        [TestCase(0f, 0f, 12.6f, true)]
        [TestCase(0f, 0f, -12.6f, true)]
        [TestCase(0f, -1.9f, 0f, false)]
        [TestCase(0f, -2.1f, 0f, true)]    // fell through the floor
        [TestCase(0f, 30f, 0f, false)]     // airborne is not lost
        public void OutOfArena_Table(float x, float y, float z, bool expected)
        {
            Assert.AreEqual(expected, RunArenaController.OutOfArena(new Vector3(x, y, z), 12f));
        }

        // ---- P2-06: deferred release ----

        [Test]
        public void ReleaseSchedule_NotDueBeforeDelay()
        {
            Assert.IsFalse(RunArenaController.ReleaseSchedule(10.5f, 10f, 1.2f));
        }

        [Test]
        public void ReleaseSchedule_DueAtAndAfterDelay()
        {
            Assert.IsTrue(RunArenaController.ReleaseSchedule(11.25f, 10f, 1.2f));
            Assert.IsTrue(RunArenaController.ReleaseSchedule(20f, 10f, 1.2f));
        }

        [Test]
        public void ReleaseSchedule_ZeroDelay_DueImmediately()
        {
            Assert.IsTrue(RunArenaController.ReleaseSchedule(10f, 10f, 0f));
        }

        [Test]
        public void WaveDue_PendingEnemiesCount_NoDoubleTrigger()
        {
            // Wave 1 (3) is scheduled but not yet active: alive 0, pending 3, two waves left.
            Assert.IsFalse(RunArenaController.WaveDue(0, 3, 2));
            // Once pending drains into alive the old rule applies.
            Assert.IsFalse(RunArenaController.WaveDue(3, 0, 2));
            Assert.IsTrue(RunArenaController.WaveDue(1, 0, 2));
            Assert.IsFalse(RunArenaController.WaveDue(1, 1, 2));
            Assert.IsFalse(RunArenaController.WaveDue(0, 0, 0));
        }

        // ---- P3-07: boss phases, adds, bar ----

        [Test]
        public void HealthBarFill_Clamps()
        {
            Assert.AreEqual(0.5f, RunArenaController.HealthBarFill(50f, 100f), 1e-6f);
            Assert.AreEqual(1f, RunArenaController.HealthBarFill(150f, 100f));
            Assert.AreEqual(0f, RunArenaController.HealthBarFill(-5f, 100f));
            Assert.AreEqual(0f, RunArenaController.HealthBarFill(10f, 0f));
        }

        [Test]
        public void PhaseHealths_SplitTargetByWeight_SumsToTarget()
        {
            float[] equal = RunArenaController.PhaseHealths(900f, new[] { 300f, 300f, 300f });
            CollectionAssert.AreEqual(new[] { 300f, 300f, 300f }, equal);
            float[] warden = RunArenaController.PhaseHealths(450f, new[] { 450f, 450f }); // P2 at 50%
            CollectionAssert.AreEqual(new[] { 225f, 225f }, warden);
            float[] weighted = RunArenaController.PhaseHealths(600f, new[] { 1f, 2f });
            Assert.AreEqual(200f, weighted[0], 1e-4f); Assert.AreEqual(400f, weighted[1], 1e-4f);
            float[] bad = RunArenaController.PhaseHealths(600f, new[] { 0f, 2f }); // non-positive weight -> equal shares
            CollectionAssert.AreEqual(new[] { 300f, 300f }, bad);
        }

        [Test]
        public void BossAdds_RespectConcurrentCap()
        {
            // The adds wave goes through ActivateNextWave: released = ReleaseCount(alive + pending, adds, cap).
            int cap = RunScaling.MaxConcurrentEnemies;
            for (int alive = 1; alive <= cap; alive++) // the boss is always alive when its adds are called
            for (int pending = 0; pending + alive <= cap; pending++)
            for (int adds = 0; adds <= 4; adds++)
            {
                int released = RunArenaController.ReleaseCount(alive + pending, adds, cap);
                Assert.LessOrEqual(alive + pending + released, cap, $"alive {alive} pending {pending} adds {adds}");
            }
            Assert.AreEqual(2, RunArenaController.ReleaseCount(1, 2, cap), "boss alone: both adds come in");
        }

        [Test]
        public void ScaledStats_BossAddOnBossNode_UsesDepthMultipliers()
        {
            RunArenaController.ScaledStats(30f, 10f, 4, RoomKind.Boss, 0, out float hp, out _, isBoss: false);
            Assert.AreEqual(30f * RunScaling.HealthMultiplier(4, RoomKind.Boss), hp, 1e-4f);
            RunArenaController.ScaledStats(30f, 10f, 4, RoomKind.Boss, 0, out float bossHp, out _);
            Assert.AreEqual(RunScaling.BossTargetHealth(0), bossHp);
        }
    }
}
