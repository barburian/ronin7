using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>P0-11: runlog.jsonl holds exactly one JSON record per line.</summary>
    public class RunLogTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), "ronin7_runlog_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            RunLog.DirectoryOverride = testDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            RunLog.DirectoryOverride = null;
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }

        private static RunLogEntry Sample(string deathBy) => new RunLogEntry
        {
            utc = "2026-10-12T00:00:00.0000000Z",
            seed = 4000000000u, // above int.MaxValue: uint must survive the round trip
            won = false,
            depth = 7,
            kills = 31,
            boons = 3,
            echoes = 120,
            seconds = 912.5f,
            boonIds = new[] { "ironskin", "ironskin", "keen_edge" },
            deathBy = deathBy,
            build = "0.1.0-dev",
        };

        [Test]
        public void Roundtrip_SingleLine_NoEmbeddedNewline()
        {
            string line = RunLog.ToLine(Sample("The-Warden\nwith a newline"));

            Assert.IsTrue(line.EndsWith("\n"));
            Assert.AreEqual(1, line.Split('\n').Length - 1, "A record must be exactly one line.");

            var back = JsonUtility.FromJson<RunLogEntry>(line.TrimEnd('\n'));
            Assert.AreEqual(4000000000u, back.seed);
            Assert.AreEqual(7, back.depth);
            Assert.AreEqual("The-Warden\nwith a newline", back.deathBy);
            CollectionAssert.AreEqual(new[] { "ironskin", "ironskin", "keen_edge" }, back.boonIds);
        }

        [Test]
        public void Append_AddsOneLinePerRun()
        {
            RunLog.Append(Sample("a"));
            RunLog.Append(Sample("b"));

            string[] lines = File.ReadAllLines(RunLog.FilePath);
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("b", JsonUtility.FromJson<RunLogEntry>(lines[1]).deathBy);
        }

        [Test]
        public void KillerName_StripsCloneSuffix()
        {
            var go = new GameObject("The-Warden(Clone)");
            try
            {
                Assert.AreEqual("The-Warden", RunDirector.KillerName(go));
                Assert.AreEqual("unknown", RunDirector.KillerName(null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RunLog_RecordsAbandonedRun_AndArchetypeCounts()
        {
            RunTally.Reset();
            RunTally.NoteSpawn("CoilGanger");
            RunTally.NoteSpawn("CoilGanger");
            RunTally.NoteSpawn("ShieldTrooper", elite: true);
            RunTally.NoteAffix("Volatile");
            RunTally.NoteKill("CoilGanger");
            RunTally.NoteKill("Player");       // never spawned: ignored
            RunTally.Resisted += 2;
            RunTally.Exploited += 1;

            var entry = Sample("abandoned");
            entry.evt = "end";
            entry.outcome = RunLog.Outcome(false, "abandoned");
            entry.sector = 1;
            entry.elites = RunTally.Elites;
            entry.resistedHits = RunTally.Resisted;
            entry.weaknessHits = RunTally.Exploited;
            entry.spawnCounts = RunTally.Spawns();
            entry.killCounts = RunTally.Kills();
            entry.affixCounts = RunTally.Affixes();
            RunTally.NoteOffer(new[] { "ironskin", "keen_edge" }, "keen_edge");
            RunTally.NoteOffer(new[] { "ironskin" }, "");
            entry.boonOffers = RunTally.Offers();
            RunLog.Append(entry);

            var back = JsonUtility.FromJson<RunLogEntry>(File.ReadAllLines(RunLog.FilePath)[0]);
            Assert.AreEqual("abandoned", back.outcome);
            Assert.AreEqual(1, back.sector);
            Assert.AreEqual(1, back.elites);
            Assert.AreEqual(2, back.resistedHits);
            Assert.AreEqual(1, back.weaknessHits);
            CollectionAssert.AreEqual(new[] { "CoilGanger:2", "ShieldTrooper:1" }, back.spawnCounts);
            CollectionAssert.AreEqual(new[] { "CoilGanger:1" }, back.killCounts);
            CollectionAssert.AreEqual(new[] { "Volatile:1" }, back.affixCounts);
            CollectionAssert.AreEqual(new[] { "ironskin,keen_edge>keen_edge", "ironskin>" }, back.boonOffers);
            RunTally.Reset();
        }

        [Test]
        public void Outcome_DistinguishesWonDiedAbandoned()
        {
            Assert.AreEqual("won", RunLog.Outcome(true, null));
            Assert.AreEqual("died", RunLog.Outcome(false, "The-Warden"));
            Assert.AreEqual("abandoned", RunLog.Outcome(false, "abandoned"));
        }

        [Test]
        public void OldLine_WithoutNewFields_StillParses()
        {
            const string old = "{\"utc\":\"x\",\"seed\":5,\"won\":true,\"depth\":3,\"kills\":9,\"boons\":1,\"echoes\":2,\"seconds\":1.5,\"boonIds\":[],\"deathBy\":\"\",\"build\":\"0.1\"}";
            var back = JsonUtility.FromJson<RunLogEntry>(old);
            Assert.AreEqual(9, back.kills);
            Assert.IsTrue(string.IsNullOrEmpty(back.evt));
            Assert.AreEqual(0, back.elites);
        }

        [Test]
        public void Reset_ClearsCounters()
        {
            RunTally.NoteSpawn("A", elite: true);
            RunTally.Resisted++;
            RunTally.Reset();
            Assert.AreEqual(0, RunTally.Spawns().Length);
            Assert.AreEqual(0, RunTally.Elites);
            Assert.AreEqual(0, RunTally.Resisted);
        }

        [Test]
        public void BankAndDeleteSavedRun_LogsAbandonedEntry()
        {
            var save = Path.Combine(testDirectory, "save");
            Directory.CreateDirectory(save);
            SaveSystem.DirectoryOverride = save;
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            try
            {
                var snap = new RunSnapshot { version = RunSnapshot.CurrentVersion, seed = 77u, depth = 4, echoesEarned = 5, runKills = 12 };
                RunSnapshot.Write(snap);
                RunDirector.BankAndDeleteSavedRun();

                var back = JsonUtility.FromJson<RunLogEntry>(File.ReadAllLines(RunLog.FilePath)[0]);
                Assert.AreEqual("abandoned", back.outcome);
                Assert.AreEqual("end", back.evt);
                Assert.AreEqual(77u, back.seed);
                Assert.AreEqual(4, back.depth);
                Assert.AreEqual(12, back.kills);
            }
            finally
            {
                SaveSystem.DirectoryOverride = null;
                MetaProgression.Reset();
            }
        }
    }
}
