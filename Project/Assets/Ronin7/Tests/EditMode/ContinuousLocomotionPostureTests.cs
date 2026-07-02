using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Verifies <see cref="ContinuousLocomotion.ResolvePosture"/> — the pure precedence surface for
    /// the on-foot posture toggles — collapses overlapping/conflicting inputs to a single
    /// well-defined state (run and crouch never both active; dashes/slides are uninterruptible).
    /// </summary>
    public class ContinuousLocomotionPostureTests
    {
        [Test]
        public void RunPressed_FromStand_StartsRunning()
        {
            var (running, crouching) = ContinuousLocomotion.ResolvePosture(false, false, true, false, false);
            Assert.IsTrue(running);
            Assert.IsFalse(crouching);
        }

        [Test]
        public void CrouchPressed_WhileRunning_SwapsToCrouchOnly()
        {
            var (running, crouching) = ContinuousLocomotion.ResolvePosture(true, false, false, true, false);
            Assert.IsFalse(running);
            Assert.IsTrue(crouching);
        }

        [Test]
        public void RunAndCrouchPressedSameFrame_ResolvesToCrouchOnly()
        {
            // Overlapping presses must not leave both flags set: crouch wins, run is cleared.
            var (running, crouching) = ContinuousLocomotion.ResolvePosture(false, false, true, true, false);
            Assert.IsFalse(running);
            Assert.IsTrue(crouching);
        }

        [Test]
        public void ActionLocked_IgnoresPosturePresses()
        {
            // Dash/slide in progress: a crouch press can't interrupt the burst (no crouching mid-dash).
            var (running, crouching) = ContinuousLocomotion.ResolvePosture(true, false, false, true, true);
            Assert.IsTrue(running);
            Assert.IsFalse(crouching);
        }

        [Test]
        public void AllInputs_FromReachableState_NeverLeaveBothActive()
        {
            // Exhaustive over every reachable starting posture (run and crouch are never both true,
            // since every mutation flows through ResolvePosture) and every press/lock combination:
            // the resolved posture is always a single, well-defined state.
            foreach (var running0 in new[] { false, true })
            foreach (var crouching0 in new[] { false, true })
            {
                if (running0 && crouching0) continue; // unreachable invalid state
                foreach (var runPressed in new[] { false, true })
                foreach (var crouchPressed in new[] { false, true })
                foreach (var actionLocked in new[] { false, true })
                {
                    var (running, crouching) = ContinuousLocomotion.ResolvePosture(
                        running0, crouching0, runPressed, crouchPressed, actionLocked);
                    Assert.IsFalse(running && crouching,
                        $"both active for start run={running0} crouch={crouching0} " +
                        $"runPressed={runPressed} crouchPressed={crouchPressed} locked={actionLocked}");
                }
            }
        }
    }
}
