using NUnit.Framework;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>P2-04 diegetic exit gates (<see cref="ArenaGeometryBuilder.BuildExitGates"/>).</summary>
    public class ExitGateTests
    {
        [TestCase(1, 5f)]
        [TestCase(2, 5f)]
        [TestCase(3, 5f)]
        [TestCase(3, 11f)]
        public void Gates_FitTheNorthWall_WithoutOverlap(int count, float half)
        {
            for (int i = 0; i < count; i++)
            {
                float x = ArenaGeometryBuilder.GateX(i, count, half);
                Assert.LessOrEqual(Mathf.Abs(x) + ArenaGeometryBuilder.GateWidth * 0.5f + 0.2f, half - 0.2f, $"gate {i} clips a side wall");
                if (i > 0)
                    Assert.Greater(x - ArenaGeometryBuilder.GateX(i - 1, count, half), ArenaGeometryBuilder.GateWidth + 0.4f, "arches overlap");
            }
        }

        [Test]
        public void GateIndexAt_InsideArch_PicksIt_ElsewhereNone()
        {
            const float half = 5f;
            float z = half - 0.5f; // a step in front of the north wall
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(i, ArenaGeometryBuilder.GateIndexAt(new Vector2(ArenaGeometryBuilder.GateX(i, 3, half), z), 3, half));
            Assert.AreEqual(-1, ArenaGeometryBuilder.GateIndexAt(Vector2.zero, 3, half), "room centre picks nothing");
            Assert.AreEqual(-1, ArenaGeometryBuilder.GateIndexAt(new Vector2(ArenaGeometryBuilder.GateX(0, 3, half), half - 3f), 3, half), "too far from the wall");
            float between = (ArenaGeometryBuilder.GateX(0, 3, half) + ArenaGeometryBuilder.GateX(1, 3, half)) * 0.5f;
            Assert.AreEqual(-1, ArenaGeometryBuilder.GateIndexAt(new Vector2(between, z), 3, half), "between arches picks nothing");
        }

        [Test]
        public void BuildExitGates_OneArchPerChoice_WithKindLabel()
        {
            var parent = new GameObject("Arena");
            try
            {
                var root = ArenaGeometryBuilder.BuildExitGates(parent.transform, new[] { RoomKind.Elite, RoomKind.Shop }, 5f, null);
                Assert.AreEqual(2, root.transform.childCount);
                var label = root.transform.GetChild(0).GetComponentInChildren<TMPro.TextMeshPro>();
                Assert.IsNotNull(label);
                StringAssert.StartsWith("ELITE", label.text);
                StringAssert.Contains("rarer boons", label.text);
                Assert.AreEqual(RoomKindText.Color(RoomKind.Elite), label.color);
                foreach (var c in root.GetComponentsInChildren<Collider>())
                    Assert.IsTrue(c is BoxCollider, "primitive colliders only");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
