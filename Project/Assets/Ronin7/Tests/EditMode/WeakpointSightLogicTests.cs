using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the two pure helpers behind Ch7's weakpoint-sight ability: the on/off toggle + damage
    /// multiplier state machine (<see cref="WeakpointSightState"/>) and the marker distance-filter
    /// (<see cref="WeakpointSightMarkers"/>). Mirrors <c>MultiObjectiveLogicTests</c>' style.
    /// </summary>
    public class WeakpointSightLogicTests
    {
        [Test]
        public void InitialState_IsInactive_MultiplierIsOne()
        {
            var state = new WeakpointSightState(2f);
            Assert.IsFalse(state.IsActive);
            Assert.AreEqual(1f, state.DamageMultiplier);
        }

        [Test]
        public void Toggle_FirstCall_ActivatesAndReturnsTrue()
        {
            var state = new WeakpointSightState(2f);
            bool result = state.Toggle();
            Assert.IsTrue(result);
            Assert.IsTrue(state.IsActive);
            Assert.AreEqual(2f, state.DamageMultiplier);
        }

        [Test]
        public void Toggle_SecondCall_DeactivatesAndReturnsFalse()
        {
            var state = new WeakpointSightState(2f);
            state.Toggle();
            bool result = state.Toggle();
            Assert.IsFalse(result);
            Assert.IsFalse(state.IsActive);
            Assert.AreEqual(1f, state.DamageMultiplier);
        }

        [Test]
        public void Deactivate_WhileActive_ForcesInactive()
        {
            var state = new WeakpointSightState(2f);
            state.Toggle();
            state.Deactivate();
            Assert.IsFalse(state.IsActive);
            Assert.AreEqual(1f, state.DamageMultiplier);
        }

        [Test]
        public void Deactivate_WhileAlreadyInactive_IsIdempotent()
        {
            var state = new WeakpointSightState(2f);
            state.Deactivate();
            Assert.IsFalse(state.IsActive);
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
