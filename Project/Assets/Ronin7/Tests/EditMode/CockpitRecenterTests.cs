using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="CockpitRecenter.TryFlattenYaw"/>, the pure yaw-flattening core of the
    /// recenter action: pitch/roll in the head's forward vector must not leak into the cockpit's new
    /// heading, and a perfectly vertical look (no defined yaw) must report failure so the caller
    /// leaves the cockpit's rotation unchanged rather than snapping to an arbitrary heading.
    /// </summary>
    public class CockpitRecenterTests
    {
        [Test]
        public void PitchedForward_YieldsSameYawAsUnpitched()
        {
            Vector3 pitched = Quaternion.Euler(30f, 40f, 0f) * Vector3.forward;
            Vector3 pureYaw = Quaternion.Euler(0f, 40f, 0f) * Vector3.forward;

            Assert.IsTrue(CockpitRecenter.TryFlattenYaw(pitched, out Quaternion yawFromPitched));
            Assert.IsTrue(CockpitRecenter.TryFlattenYaw(pureYaw, out Quaternion yawFromPure));

            Assert.Less(Quaternion.Angle(yawFromPitched, yawFromPure), 0.01f);
        }

        [Test]
        public void PitchedAndRolledForward_YieldsZeroRollCorrectYaw()
        {
            // Roll rotates around the forward axis itself, so it never changes forward's own
            // direction — the flattened yaw must match the roll=0 case exactly, and the output must
            // carry no roll of its own (its "up" stays aligned to world-up, per LookRotation).
            Vector3 pitchedOnly = Quaternion.Euler(25f, 40f, 0f) * Vector3.forward;
            Vector3 pitchedAndRolled = Quaternion.Euler(25f, 40f, 60f) * Vector3.forward;

            Assert.IsTrue(CockpitRecenter.TryFlattenYaw(pitchedOnly, out Quaternion yawA));
            Assert.IsTrue(CockpitRecenter.TryFlattenYaw(pitchedAndRolled, out Quaternion yawB));

            Assert.Less(Quaternion.Angle(yawA, yawB), 0.01f);
            Assert.Less(Vector3.Angle(yawB * Vector3.up, Vector3.up), 0.01f);
        }

        [Test]
        public void ExactlyVerticalForward_ReturnsFalse()
        {
            Assert.IsFalse(CockpitRecenter.TryFlattenYaw(Vector3.up, out _));
            Assert.IsFalse(CockpitRecenter.TryFlattenYaw(Vector3.down, out _));
        }
    }
}
