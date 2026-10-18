using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P0-04 (BUG-04/BUG-27): a run can be left without ending it (SAVE &amp; QUIT keeps the
    /// node-entry snapshot), and the menu ABANDON only banks — it no longer starts a new run.</summary>
    public class RunExitPathTests
    {
        private string testDirectory;
        private GameObject directorGo;

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_exit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveSystem.DirectoryOverride = testDirectory;
            RunLog.DirectoryOverride = testDirectory; // AbandonSavedRun appends to runlog.jsonl
            MetaProgression.Load(); // fresh, from the empty temp dir
            directorGo = new GameObject("RunDirector");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(directorGo);
            RunState.Reset();
            MetaProgression.Reset();
            SaveSystem.DirectoryOverride = null;
            RunLog.DirectoryOverride = null;
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, recursive: true);
        }

        private static void WriteMidRunSnapshot(int echoes)
        {
            RunState.Begin(7u);
            RunState.Advance(RunState.NextChoices[0]);
            RunState.Advance(RunState.NextChoices[0]);
            RunState.AddShards(echoes);
            RunSnapshot.Write(RunState.ToSnapshot());
        }

        [Test]
        public void SuspendRun_LeavesSnapshot_ClearsInRun()
        {
            WriteMidRunSnapshot(12);
            var director = directorGo.AddComponent<RunDirector>();

            // No GameFlowManager in an EditMode test, so the menu load itself only logs.
            LogAssert.Expect(LogType.Error, new Regex("No GameFlowManager"));
            director.SuspendRun();

            Assert.IsFalse(RunState.InRun, "Suspending must clear InRun so ENTER THE FRACTURE works again.");
            var snapshot = RunSnapshot.Read();
            Assert.IsNotNull(snapshot, "The node-entry snapshot must survive so the menu offers RESUME RUN.");
            Assert.AreEqual(2, snapshot.depth, "two rows walked (nodeIndex is now a node id, P2-04)");
            Assert.AreEqual(0, MetaProgression.Echoes, "Suspending banks nothing.");
        }

        [Test]
        public void MainMenu_Abandon_DoesNotStartRun()
        {
            WriteMidRunSnapshot(12);
            RunState.Reset(); // at the menu: the run is saved, not live
            var director = directorGo.AddComponent<RunDirector>();

            director.AbandonSavedRun();

            Assert.IsFalse(RunState.InRun, "ABANDON must not start a new run (BUG-27).");
            Assert.IsNull(RunSnapshot.Read(), "The abandoned snapshot is deleted.");
            Assert.AreEqual(RunScaling.EchoResult(2, false), MetaProgression.Echoes, "P4-11: the loss Echo result for its depth is banked once, never its Shards.");
            Assert.AreEqual(1, MetaProgression.RunsCompleted);
        }

        /// <summary>P4-11 (D-11): unspent Shards vanish with the run; only the Echo result is banked.</summary>
        [Test]
        public void Shards_LostOnRunEnd_NeverBanked()
        {
            RunState.Begin(7u);
            RunState.Advance(RunState.NextChoices[0]);
            RunState.AddShards(500);
            var director = directorGo.AddComponent<RunDirector>();

            LogAssert.Expect(LogType.Error, new Regex("No GameFlowManager"));
            director.AbandonRun();

            Assert.AreEqual(RunScaling.EchoResult(1, false), MetaProgression.Echoes, "500 unspent Shards must not reach the bank");
            Assert.AreEqual(RunScaling.EchoResult(1, false), RunSummary.EchoesEarned, "the summary shows the Echo result");
            RunSummary.Clear();
        }

        /// <summary>P4-11: Echoes come only from the end-of-run result: 320 a win, 60 + 6/row a loss, then EchoGain
        /// (cap x1.5), then heat (+10%/rank). Room clears pay Shards, which never touch MetaProgression.</summary>
        [Test]
        public void Echoes_FromResultOnly()
        {
            Assert.AreEqual(320, RunDirector.RunEchoResult(true, 7, 0f, 0), "a win pays the same at any recorded depth");
            Assert.AreEqual(60, RunDirector.RunEchoResult(false, 0, 0f, 0));
            Assert.AreEqual(144, RunDirector.RunEchoResult(false, 14, 0f, 0));
            Assert.AreEqual(150, RunScaling.EchoResult(RunMapGenerator.TotalNodes, false));
            Assert.AreEqual(480, RunDirector.RunEchoResult(true, 0, 0.5f, 0), "EchoGain scales the result");
            Assert.AreEqual(480, RunDirector.RunEchoResult(true, 0, 9f, 0), "EchoGain caps at x1.5");
            Assert.AreEqual(448, RunDirector.RunEchoResult(true, 0, 0f, 4), "heat x1.4 scales the result");
            Assert.AreEqual(672, RunDirector.RunEchoResult(true, 0, 0.5f, 4), "EchoGain then heat");

            RunState.Begin(7u);
            RunState.AddShards(RunScaling.ShardReward(3, RoomKind.Boss));
            Assert.AreEqual(0, MetaProgression.Echoes, "earning Shards banks nothing");
        }

        [Test]
        public void Abandon_IsTwoClick()
        {
            Assert.IsFalse(RunEntryOptions.AbandonConfirmed(armed: false));
            Assert.IsTrue(RunEntryOptions.AbandonConfirmed(armed: true));
        }

        [TestCase(0f, 0.016f, true, 0.016f)]
        [TestCase(0.5f, 0.1f, true, 0.6f)]
        [TestCase(0.9f, 0.1f, false, 0f)] // releasing early resets the hold
        public void HoldToConfirm_AdvanceHold(float held, float dt, bool pressed, float expected)
        {
            Assert.AreEqual(expected, HoldToConfirmButton.AdvanceHold(held, dt, pressed), 1e-5f);
        }

        [TestCase(0.99f, false)]
        [TestCase(1f, true)]
        public void HoldToConfirm_CompletesAtOneSecond(float held, bool expected)
        {
            Assert.AreEqual(expected, HoldToConfirmButton.HoldComplete(held, 1f));
        }
    }
}
