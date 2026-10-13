using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the two pure helpers behind Ch7's weakpoint-sight ability: the on/off toggle
    /// state machine (<see cref="WeakpointSightState"/>) and the marker distance-filter
    /// (<see cref="WeakpointSightMarkers"/>). Mirrors <c>MultiObjectiveLogicTests</c>' style.
    /// </summary>
    public class WeakpointSightLogicTests
    {
        [Test]
        public void InitialState_IsInactive()
        {
            var state = new WeakpointSightState();
            Assert.IsFalse(state.IsActive);
        }

        [Test]
        public void Toggle_FirstCall_ActivatesAndReturnsTrue()
        {
            var state = new WeakpointSightState();
            bool result = state.Toggle();
            Assert.IsTrue(result);
            Assert.IsTrue(state.IsActive);
        }

        [Test]
        public void Toggle_SecondCall_DeactivatesAndReturnsFalse()
        {
            var state = new WeakpointSightState();
            state.Toggle();
            bool result = state.Toggle();
            Assert.IsFalse(result);
            Assert.IsFalse(state.IsActive);
        }

        [Test]
        public void Deactivate_WhileActive_ForcesInactive()
        {
            var state = new WeakpointSightState();
            state.Toggle();
            state.Deactivate();
            Assert.IsFalse(state.IsActive);
        }

        [Test]
        public void Deactivate_WhileAlreadyInactive_IsIdempotent()
        {
            var state = new WeakpointSightState();
            state.Deactivate();
            Assert.IsFalse(state.IsActive);
        }

        [Test]
        public void HeadPoint_SitsInPrecisionBand()
        {
            // 2 m tall body from y=0: head band is y >= 1.6, marker at its centre (1.8).
            var b = new Bounds(new Vector3(3f, 1f, -2f), new Vector3(0.6f, 2f, 0.6f));
            var p = WeakpointSightMarkers.HeadPoint(b);
            Assert.AreEqual(1.8f, p.y, 1e-4f);
            Assert.AreEqual(3f, p.x, 1e-4f);
            Assert.AreEqual(-2f, p.z, 1e-4f);
            Assert.GreaterOrEqual(p.y, b.max.y - 0.2f * b.size.y);
        }

        [Test]
        public void SelectInRange_ReturnsOnlyIndicesWithinRadius()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 5f),   // inside (5 <= 10)
                new Vector3(0f, 0f, 20f),  // outside
                new Vector3(10f, 0f, 0f),  // inside (10 <= 10, boundary)
            };

            var result = WeakpointSightMarkers.SelectInRange(Vector3.zero, positions, 10f);

            CollectionAssert.AreEqual(new[] { 0, 2 }, result);
        }

        [Test]
        public void SelectInRange_NullList_ReturnsEmpty()
        {
            var result = WeakpointSightMarkers.SelectInRange(Vector3.zero, null, 10f);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SelectInRange_NonPositiveRadius_ReturnsEmpty()
        {
            var positions = new List<Vector3> { Vector3.zero };
            Assert.AreEqual(0, WeakpointSightMarkers.SelectInRange(Vector3.zero, positions, 0f).Count);
            Assert.AreEqual(0, WeakpointSightMarkers.SelectInRange(Vector3.zero, positions, -5f).Count);
        }

        [Test]
        public void SelectInRange_EmptyCandidateList_ReturnsEmpty()
        {
            var result = WeakpointSightMarkers.SelectInRange(Vector3.zero, new List<Vector3>(), 10f);
            Assert.AreEqual(0, result.Count);
        }
    }
}
