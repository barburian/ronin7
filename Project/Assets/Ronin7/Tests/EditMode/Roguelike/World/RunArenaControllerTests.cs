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
    }
}
