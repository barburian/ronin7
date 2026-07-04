using NUnit.Framework;
using Ronin7.EditorTools;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Regression coverage for the Kessler floor-clipping bug: his Tripo mesh has a centered pivot, so
    /// <c>FitNamedCharacter</c> grounds his root above y=0, but <see cref="Ronin7.World.NpcWalker"/> drags
    /// the FULL waypoint position (including Y). If his walk waypoints hard-code Y=0 while his root sits
    /// above 0, activating a leg yanks him down into the floor. These waypoints must always share whatever
    /// floor Y his root is grounded to.
    /// </summary>
    public class Chapter1BuilderTests
    {
        private const float Epsilon = 1e-5f;

        [TestCase(0f)]
        [TestCase(0.87f)]
        [TestCase(1.23f)]
        public void KesslerToHoldWaypoints_AllShareGivenFloorY(float floorY)
        {
            foreach (var wp in XRRigBuilder.KesslerToHoldWaypoints(floorY))
                Assert.AreEqual(floorY, wp.y, Epsilon);
        }

        [TestCase(0f)]
        [TestCase(0.87f)]
        [TestCase(1.23f)]
        public void KesslerWalkerWaypoints_AllShareGivenFloorY(float floorY)
        {
            foreach (var wp in XRRigBuilder.KesslerWalkerWaypoints(floorY))
                Assert.AreEqual(floorY, wp.y, Epsilon);
        }

        [Test]
        public void KesslerWaypoints_PreserveAuthoredXZLayout()
        {
            // The fix must only change Y — the X/Z story-authored positions (viewport, corridor, etc.)
            // must be untouched.
            var leg1 = XRRigBuilder.KesslerToHoldWaypoints(0.87f);
            Assert.AreEqual(new Vector3(0f, 0.87f, 2f), leg1[0]);
            Assert.AreEqual(new Vector3(0f, 0.87f, 6f), leg1[1]);
            Assert.AreEqual(new Vector3(-2f, 0.87f, 9.5f), leg1[2]);

            var leg2 = XRRigBuilder.KesslerWalkerWaypoints(0.87f);
            Assert.AreEqual(new Vector3(0f, 0.87f, 10f), leg2[0]);
            Assert.AreEqual(new Vector3(0f, 0.87f, 21f), leg2[1]);
            Assert.AreEqual(new Vector3(0f, 0.87f, 28f), leg2[2]);
            Assert.AreEqual(new Vector3(2f, 0.87f, 31f), leg2[3]);
        }
    }
}
