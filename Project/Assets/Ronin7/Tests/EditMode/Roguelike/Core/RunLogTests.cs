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
    }
}
