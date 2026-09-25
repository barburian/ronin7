using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Coverage for <see cref="RunSummary"/>: the capture/clear lifecycle and the pure display
    /// formatters. The formatters matter because there was previously no way to tell a won run from a
    /// lost one after the fact — the summary must be unambiguous in wording, not just colour.
    /// </summary>
    public class RunSummaryTests
    {
        [TearDown]
        public void TearDown() => RunSummary.Clear();

        [Test]
        public void Capture_SetsHasResultAndValues()
        {
            RunSummary.Capture(depth: 7, won: false, enemiesKilled: 42, boonsHeld: 5, echoesEarned: 130);

            Assert.IsTrue(RunSummary.HasResult);
            Assert.AreEqual(7, RunSummary.Depth);
            Assert.IsFalse(RunSummary.Won);
            Assert.AreEqual(42, RunSummary.EnemiesKilled);
            Assert.AreEqual(5, RunSummary.BoonsHeld);
            Assert.AreEqual(130, RunSummary.EchoesEarned);
        }

        [Test]
        public void Capture_ClampsNegativesToZero()
        {
            // EnemiesKilled is a delta of a lifetime counter, so a reset mid-run could go negative.
            RunSummary.Capture(depth: -3, won: true, enemiesKilled: -1, boonsHeld: -2, echoesEarned: -5);

            Assert.AreEqual(0, RunSummary.Depth);
            Assert.AreEqual(0, RunSummary.EnemiesKilled);
            Assert.AreEqual(0, RunSummary.BoonsHeld);
            Assert.AreEqual(0, RunSummary.EchoesEarned);
        }

        [Test]
        public void Clear_DropsTheResult()
        {
            RunSummary.Capture(3, true, 10, 2, 50);
            RunSummary.Clear();

            Assert.IsFalse(RunSummary.HasResult);
            Assert.AreEqual(0, RunSummary.Depth);
            Assert.AreEqual(0, RunSummary.EchoesEarned);
        }

        [Test]
        public void FormatOutcome_DistinguishesWinFromLoss()
        {
            Assert.AreEqual("RUN COMPLETE", RunSummary.FormatOutcome(true));
            Assert.AreEqual("RUN LOST", RunSummary.FormatOutcome(false));
            Assert.AreNotEqual(RunSummary.FormatOutcome(true), RunSummary.FormatOutcome(false));
        }

        [TestCase(0, false, 1)]    // died in the first room
        [TestCase(7, false, 8)]    // depth is 0-based, display is 1-based
        [TestCase(14, false, 15)]  // died on the final node
        [TestCase(0, true, RunMapGenerator.TotalNodes)]  // a win always reads the full map
        [TestCase(14, true, RunMapGenerator.TotalNodes)]
        public void RoomsClearedFor_IsOneBasedAndClamped(int depth, bool won, int expected)
        {
            Assert.AreEqual(expected, RunSummary.RoomsClearedFor(depth, won));
        }

        [Test]
        public void RoomsClearedFor_NeverExceedsTheMapOrDropsBelowOne()
        {
            Assert.AreEqual(1, RunSummary.RoomsClearedFor(-5, false));
            Assert.AreEqual(RunMapGenerator.TotalNodes, RunSummary.RoomsClearedFor(9999, false));
        }

        [Test]
        public void FormatDepth_ReadsAsProgressOutOfTheMap()
        {
            Assert.AreEqual($"8 / {RunMapGenerator.TotalNodes}", RunSummary.FormatDepth(7, false));
            Assert.AreEqual($"{RunMapGenerator.TotalNodes} / {RunMapGenerator.TotalNodes}",
                RunSummary.FormatDepth(14, true));
        }

        [Test]
        public void FormatOutcomeDetail_NamesTheRoomOnALossAndTheFullMapOnAWin()
        {
            StringAssert.Contains("8", RunSummary.FormatOutcomeDetail(false, 7));
            StringAssert.Contains(RunMapGenerator.TotalNodes.ToString(), RunSummary.FormatOutcomeDetail(true, 14));
            Assert.AreNotEqual(RunSummary.FormatOutcomeDetail(true, 14),
                               RunSummary.FormatOutcomeDetail(false, 14));
        }
    }
}
