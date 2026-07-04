using NUnit.Framework;
using Ronin7.Audio;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure phase-advance and foot-plant-crossing math behind
    /// <see cref="NpcFootstepCadence"/>.
    /// </summary>
    public class NpcFootstepSolverTests
    {
        [Test]
        public void AdvancePhase_ZeroSpeed_PhaseUnchanged()
        {
            float phase = NpcFootstepSolver.AdvancePhase(1f, 0f, 0.9f, 0.1f);
            Assert.AreEqual(1f, phase);
        }

        [Test]
        public void AdvancePhase_AccumulatesProportionalToSpeed()
        {
            float single = NpcFootstepSolver.AdvancePhase(0f, 2f, 1f, 0.5f);
            float doubled = NpcFootstepSolver.AdvancePhase(0f, 4f, 1f, 0.5f);

            Assert.AreEqual(Mathf.PI, single, 1e-4f);
            Assert.AreEqual(2f * Mathf.PI, doubled, 1e-4f);
        }

        [Test]
        public void CrossedFootPlant_DetectsSingleCrossing()
        {
            // floor(3.0/PI) = 0, floor(3.2/PI) = 1.
            Assert.IsTrue(NpcFootstepSolver.CrossedFootPlant(3.0f, 3.2f));
        }

        [Test]
        public void CrossedFootPlant_NoFalseCrossingWithinHalfCycle()
        {
            // Both phases stay within the same [0, PI) half-cycle.
            Assert.IsFalse(NpcFootstepSolver.CrossedFootPlant(0.5f, 1.0f));
        }

        [Test]
        public void CrossedFootPlant_LargeJump_ReturnsTrueDeterministically()
        {
            // A single large dt jump crosses several half-cycles at once; the bool just reports
            // "at least one crossing happened", so repeated calls with the same inputs must agree.
            float oldPhase = 0f;
            float newPhase = 10f * Mathf.PI + 0.01f;

            bool first = NpcFootstepSolver.CrossedFootPlant(oldPhase, newPhase);
            bool second = NpcFootstepSolver.CrossedFootPlant(oldPhase, newPhase);

            Assert.IsTrue(first);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void CrossedFootPlant_ExactMultipleOfPi_HandledWithoutDoubleCounting()
        {
            // Landing exactly on PI counts as one crossing...
            Assert.IsTrue(NpcFootstepSolver.CrossedFootPlant(Mathf.PI - 0.01f, Mathf.PI));
            // ...and the very next tiny step away from that exact boundary must not recount it.
            Assert.IsFalse(NpcFootstepSolver.CrossedFootPlant(Mathf.PI, Mathf.PI + 0.01f));
        }
    }
}
