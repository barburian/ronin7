using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>P5-08: the boot holo map table's read-only view of runlog.jsonl.</summary>
    public class LastRunReaderTests
    {
        private const string Sample =
            "{\"utc\":\"a\",\"seed\":7,\"won\":false,\"depth\":3,\"evt\":\"end\",\"outcome\":\"died\",\"path\":[0,1,4,6]}\n" +
            "{\"utc\":\"b\",\"seed\":99,\"won\":false,\"depth\":0,\"evt\":\"start\",\"outcome\":\"\"}\n";

        [Test]
        public void ParseLast_SkipsTrailingStartLine_ReturnsLastEnd()
        {
            Assert.IsTrue(LastRunReader.TryParseLast(Sample, out RunLogEntry e));
            Assert.AreEqual(7u, e.seed);
            Assert.AreEqual(3, e.depth);
            CollectionAssert.AreEqual(new[] { 0, 1, 4, 6 }, e.path);
            StringAssert.Contains("SEED 7", LastRunReader.Caption(e));
            StringAssert.Contains("DEPTH 4/15", LastRunReader.Caption(e));
            StringAssert.Contains("DIED", LastRunReader.Caption(e));
        }

        [Test]
        public void ParseLast_TornLastLine_FallsBackToPreviousRun()
        {
            Assert.IsTrue(LastRunReader.TryParseLast(Sample + "{\"utc\":\"c\",\"se", out RunLogEntry e));
            Assert.AreEqual(7u, e.seed);
        }

        [Test]
        public void ParseLast_LegacyLineWithoutEvt_Counts_AndOutcomeIsDerived()
        {
            Assert.IsTrue(LastRunReader.TryParseLast("{\"seed\":5,\"won\":true,\"depth\":14}", out RunLogEntry e));
            StringAssert.Contains("WON", LastRunReader.Caption(e));
        }

        [Test]
        public void EmptyOrOnlyStarts_IsNoRun()
        {
            Assert.IsFalse(LastRunReader.TryParseLast("", out _));
            Assert.IsFalse(LastRunReader.TryParseLast(null, out _));
            Assert.IsFalse(LastRunReader.TryParseLast("{\"seed\":1,\"evt\":\"start\"}\n", out _));
        }

        [Test]
        public void MissingFile_IsNoRun()
        {
            string path = Path.Combine(Path.GetTempPath(), "ronin7_no_such_dir_" + System.Guid.NewGuid(), RunLog.FileName);
            Assert.IsFalse(LastRunReader.TryReadLast(path, out _));
        }

        [Test]
        public void NodeStates_PathLit_ElseReachedRows()
        {
            RunMap map = RunMapGenerator.GenerateGraph(7);
            var walk = map.PathTo(map.Nodes.Length - 1).ToArray();
            var withPath = LastRunReader.NodeStates(map, new RunLogEntry { depth = 14, path = walk });
            for (int i = 0; i < map.Nodes.Length; i++)
                Assert.AreEqual(System.Array.IndexOf(walk, i) >= 0 ? 2 : 0, withPath[i]);

            var legacy = LastRunReader.NodeStates(map, new RunLogEntry { depth = 2, path = new[] { 999 } });
            CollectionAssert.AreEqual(new int[map.Nodes.Length], legacy); // out-of-range ids ignored, no throw
            var old = LastRunReader.NodeStates(map, new RunLogEntry { depth = 2 });
            for (int i = 0; i < map.Nodes.Length; i++)
                Assert.AreEqual(map.Nodes[i].Depth <= 2 ? 1 : 0, old[i]);
        }

        [Test]
        public void MapMesh_Plate_PlusOneQuadPerNode_PlusOnePerWalkedEdge()
        {
            RunMap map = RunMapGenerator.GenerateGraph(7);
            var walk = map.PathTo(map.Nodes.Length - 1).ToArray();
            var e = new RunLogEntry { depth = 14, path = walk };
            Mesh mesh = HoloMapTable.BuildMesh(map, LastRunReader.NodeStates(map, e), walk, 0.055f, 0.13f, 0.03f);
            Assert.AreEqual(4 * (1 + map.Nodes.Length + walk.Length - 1), mesh.vertexCount);
            Object.DestroyImmediate(mesh);
        }
    }
}
