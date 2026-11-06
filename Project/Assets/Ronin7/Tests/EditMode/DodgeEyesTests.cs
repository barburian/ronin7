using System;
using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>R12: red blinking eyes on dodge-only strikes.</summary>
    public class DodgeEyesTests
    {
        private static Strike Parry => Strike.Chop;

        [Test]
        public void EyeTriangles_FaceForwardPlusZ()
        {
            var t = DodgeEyes.Triangles;
            // Unity front faces are clockwise from the front, i.e. cross(v1-v0, v2-v0) points +Z when seen from +Z.
            Vector3[] v = new Vector3[8];
            for (int e = 0; e < 2; e++)
            {
                float cx = e == 0 ? -0.5f : 0.5f;
                v[e * 4] = new Vector3(cx - 0.25f, -0.125f); v[e * 4 + 1] = new Vector3(cx - 0.25f, 0.125f);
                v[e * 4 + 2] = new Vector3(cx + 0.25f, 0.125f); v[e * 4 + 3] = new Vector3(cx + 0.25f, -0.125f);
            }
            for (int i = 0; i < t.Length; i += 3)
                Assert.Greater(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).z, 0f);
        }

        [Test]
        public void WhichStrikesLightEyes()
        {
            var dodge = Parry; dodge.unparryable = true;
            var slam = Parry; slam.aoeRadius = 3f;
            var bolt = Parry; bolt.boltSpeed = 10f;
            var markedBolt = bolt; markedBolt.unparryable = true;
            Assert.IsFalse(DodgeEyes.IsDodge(Parry));
            Assert.IsTrue(DodgeEyes.IsDodge(dodge));
            Assert.IsTrue(DodgeEyes.IsDodge(slam));
            Assert.IsFalse(DodgeEyes.IsDodge(bolt), "plain ranged bolts stay as they are");
            Assert.IsTrue(DodgeEyes.IsDodge(markedBolt));
        }

        [Test]
        public void OffOutsideWindupAndActive()
        {
            var dodge = Parry; dodge.unparryable = true;
            Assert.IsTrue(DodgeEyes.ShouldShow(true, dodge));
            Assert.IsFalse(DodgeEyes.ShouldShow(false, dodge), "stagger / dead / idle");
            Assert.IsFalse(DodgeEyes.ShouldShow(true, Parry));
        }

        [Test]
        public void BlinksAtSixHzAndReduceFlashesIsSteady()
        {
            Assert.IsTrue(DodgeEyes.BlinkOn(0.01f, false));
            Assert.IsFalse(DodgeEyes.BlinkOn(0.5f / DodgeEyes.BlinkHz + 0.01f, false));
            Assert.IsTrue(DodgeEyes.BlinkOn(1f / DodgeEyes.BlinkHz + 0.01f, false));
            for (float t = 0f; t < 1f; t += 0.013f) Assert.IsTrue(DodgeEyes.BlinkOn(t, true));
        }

        [Test]
        public void PlacementIsUpperFrontAndScalesWithBody()
        {
            DodgeEyes.Layout(new Vector3(0f, 1f, 0f), new Vector3(0.8f, 2f, 0.6f), out var pos, out var gap);
            Assert.Greater(pos.y, 1.5f);
            Assert.AreEqual(0.35f, pos.z, 1e-4f); // front of face + 5 cm so the dots are not clipped
            Assert.AreEqual(0.16f, gap, 1e-4f);
            DodgeEyes.Layout(Vector3.zero, new Vector3(5f, 5f, 5f), out _, out var big);
            DodgeEyes.Layout(Vector3.zero, new Vector3(0.1f, 1f, 1f), out _, out var small);
            Assert.AreEqual(0.2f, big, 1e-4f);
            Assert.AreEqual(0.16f, small, 1e-4f);
        }

        [Test]
        public void DodgeTintDiffersFromParryTelegraph()
        {
            var tele = new Color(1f, 0.4f, 0.25f);
            Assert.AreEqual(tele, DodgeEyes.TintFor(false, tele));
            Assert.AreNotEqual(tele, DodgeEyes.TintFor(true, tele));
            Assert.Less(DodgeEyes.DodgeColor.g, 0.1f);
        }

        [Test]
        public void TickLogicAllocatesNothing()
        {
            var dodge = Parry; dodge.unparryable = true;
            bool sink = false;
            DodgeEyes.BlinkOn(0.1f, false); DodgeEyes.ShouldShow(true, dodge);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) sink ^= DodgeEyes.ShouldShow(true, dodge) && DodgeEyes.BlinkOn(i * 0.01f, i % 7 == 0);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.IsNotNull(sink.ToString());
        }
    }
}
