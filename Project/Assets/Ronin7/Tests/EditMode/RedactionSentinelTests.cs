using NUnit.Framework;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Pure-seam tests for RedactionSentinel.CanSee (dot-product FOV + range, no occlusion).</summary>
    public class RedactionSentinelTests
    {
        private static readonly Vector3 Origin = Vector3.zero;
        private static readonly Vector3 Forward = Vector3.forward;
        private const float Range = 8f;
        private const float HalfAngle = 45f;

        [Test]
        public void CanSee_TargetStraightAheadInRange_IsTrue()
        {
            Assert.IsTrue(RedactionSentinel.CanSee(Origin, Forward, new Vector3(0f, 0f, 5f), Range, HalfAngle));
        }

        [Test]
        public void CanSee_TargetOutsideCone_IsFalse()
        {
            // atan(4/2) ≈ 63.4° off forward — outside the 45° half-angle, inside range.
            Assert.IsFalse(RedactionSentinel.CanSee(Origin, Forward, new Vector3(4f, 0f, 2f), Range, HalfAngle));
        }

        [Test]
        public void CanSee_TargetBehind_IsFalse()
        {
            Assert.IsFalse(RedactionSentinel.CanSee(Origin, Forward, new Vector3(0f, 0f, -3f), Range, HalfAngle));
        }

        [Test]
        public void CanSee_TargetOutOfRange_IsFalse()
        {
            Assert.IsFalse(RedactionSentinel.CanSee(Origin, Forward, new Vector3(0f, 0f, 8.5f), Range, HalfAngle));
        }

        [Test]
        public void CanSee_JustInsideEdgeAngle_IsTrue()
        {
            Vector3 target = new Vector3(Mathf.Sin(44f * Mathf.Deg2Rad), 0f, Mathf.Cos(44f * Mathf.Deg2Rad)) * 5f;
            Assert.IsTrue(RedactionSentinel.CanSee(Origin, Forward, target, Range, HalfAngle));
        }

        [Test]
        public void CanSee_JustOutsideEdgeAngle_IsFalse()
        {
            Vector3 target = new Vector3(Mathf.Sin(46f * Mathf.Deg2Rad), 0f, Mathf.Cos(46f * Mathf.Deg2Rad)) * 5f;
            Assert.IsFalse(RedactionSentinel.CanSee(Origin, Forward, target, Range, HalfAngle));
        }

        [Test]
        public void CanSee_TargetAtSentinelPosition_IsFalse()
        {
            // Degenerate zero-distance case must not divide by zero or count as seen.
            Assert.IsFalse(RedactionSentinel.CanSee(Origin, Forward, Origin, Range, HalfAngle));
        }

        [Test]
        public void CanSee_UnnormalizedForward_StillWorks()
        {
            // The seam normalizes forward itself — patrol code hands in transform.forward, but
            // builder-authored vectors may not be unit length.
            Assert.IsTrue(RedactionSentinel.CanSee(Origin, new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, 5f), Range, HalfAngle));
        }
    }
}
