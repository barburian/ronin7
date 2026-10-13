using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-06: precision zone (head top 20% or thrust) and Weakpoint Sight as a precision-only multiplier.</summary>
    public class BladePrecisionTests
    {
        private static readonly Bounds Body = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f)); // y 0..2
        private static readonly Vector3 Axis = Vector3.forward;

        [TestCase(1.9f, true)]   // top 20% starts at 1.6
        [TestCase(1.61f, true)]
        [TestCase(1.59f, false)]
        [TestCase(1.0f, false)]
        public void IsPrecisionHit_HeadZone(float hitY, bool expected)
            => Assert.AreEqual(expected, BladeDamager.IsPrecisionHit(hitY, Body, Vector3.right * 5f, Axis));

        [Test] public void IsPrecisionHit_ThrustAligned_True()
            => Assert.IsTrue(BladeDamager.IsPrecisionHit(1f, Body, Vector3.forward * 5f, Axis));

        [Test] public void IsPrecisionHit_SlashPerpendicular_False()
            => Assert.IsFalse(BladeDamager.IsPrecisionHit(1f, Body, Vector3.up * 5f, Axis));

        [Test] public void IsPrecisionHit_ZeroVelocityBody_False()
            => Assert.IsFalse(BladeDamager.IsPrecisionHit(1f, Body, Vector3.zero, Axis));

        [Test] public void IsPrecisionHit_ZeroVelocityHead_True()
            => Assert.IsTrue(BladeDamager.IsPrecisionHit(1.9f, Body, Vector3.zero, Axis));

        [Test]
        public void IsPrecisionHit_ThrustThreshold()
        {
            Assert.IsTrue(BladeDamager.IsPrecisionHit(1f, Body, new Vector3(0.5f, 0f, 1f), Axis));   // dot ~0.894
            Assert.IsFalse(BladeDamager.IsPrecisionHit(1f, Body, new Vector3(1f, 0f, 0.5f), Axis));  // dot ~0.447
        }

        [Test]
        public void WeakpointSight_DoesNotChangeBodyHitDamage()
        {
            var go = new GameObject("Rig");
            try
            {
                var mods = go.AddComponent<PlayerCombatModifiers>();
                float before = mods.DamageMultiplier;
                mods.WeakpointSightActive = true;
                Assert.AreEqual(before, mods.DamageMultiplier);
                Assert.AreEqual(1f, BladeDamager.PrecisionMultiplier(false, true));
                Assert.AreEqual(1f, BladeDamager.PrecisionMultiplier(false, false));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PrecisionMultiplier_BaselineAndSight()
        {
            Assert.AreEqual(1.25f, BladeDamager.PrecisionMultiplier(true, false));
            Assert.AreEqual(2f, BladeDamager.PrecisionMultiplier(true, true));
        }
    }
}
