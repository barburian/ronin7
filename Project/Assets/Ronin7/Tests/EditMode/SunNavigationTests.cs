using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Pure-math coverage for the sun-navigation cores (see <c>Docs/SunNavigation-Design.md</c>):
    /// <see cref="SunGravityWell.BoostFactor"/> / <see cref="SunGravityWell.HeatPerSecond"/> ramps,
    /// <see cref="SunGlare.Intensity"/> glare curve, and <see cref="SunCompass.BearingDeg"/> headings.
    /// All side-effect free — no scene/MonoBehaviour instance needed.
    /// </summary>
    public class SunNavigationTests
    {
        private const float Eps = 1e-4f;

        // ---- SunGravityWell.BoostFactor: 0 outside outer, monotonic ramp, maxBoost at/inside danger ----

        [Test]
        public void BoostFactor_ZeroAtAndOutsideOuterRadius()
        {
            Assert.AreEqual(0f, SunGravityWell.BoostFactor(400f, 400f, 150f, 0.5f), Eps);
            Assert.AreEqual(0f, SunGravityWell.BoostFactor(900f, 400f, 150f, 0.5f), Eps);
        }

        [Test]
        public void BoostFactor_MaxAtAndInsideDangerRadius()
        {
            Assert.AreEqual(0.5f, SunGravityWell.BoostFactor(150f, 400f, 150f, 0.5f), Eps);
            Assert.AreEqual(0.5f, SunGravityWell.BoostFactor(10f, 400f, 150f, 0.5f), Eps);
        }

        [Test]
        public void BoostFactor_RampsMonotonicallyInward()
        {
            // Midpoint of the outer→danger band is half the max boost.
            Assert.AreEqual(0.25f, SunGravityWell.BoostFactor(275f, 400f, 150f, 0.5f), Eps);

            // Strictly increasing as the ship dives inward (distance shrinks).
            float prev = -1f;
            for (float d = 400f; d >= 150f; d -= 25f)
            {
                float b = SunGravityWell.BoostFactor(d, 400f, 150f, 0.5f);
                Assert.GreaterOrEqual(b, prev);
                prev = b;
            }
        }

        // ---- SunGravityWell.HeatPerSecond: 0 outside danger, ramps, clamps at core ----

        [Test]
        public void HeatPerSecond_ZeroAtAndOutsideDangerRadius()
        {
            Assert.AreEqual(0f, SunGravityWell.HeatPerSecond(150f, 150f, 60f, 30f), Eps);
            Assert.AreEqual(0f, SunGravityWell.HeatPerSecond(400f, 150f, 60f, 30f), Eps);
        }

        [Test]
        public void HeatPerSecond_MaxAtAndInsideCoreRadius()
        {
            Assert.AreEqual(30f, SunGravityWell.HeatPerSecond(60f, 150f, 60f, 30f), Eps);
            Assert.AreEqual(30f, SunGravityWell.HeatPerSecond(0f, 150f, 60f, 30f), Eps);
        }

        [Test]
        public void HeatPerSecond_RampsMonotonicallyInward()
        {
            // Midpoint of the danger→core band is half the max heat.
            Assert.AreEqual(15f, SunGravityWell.HeatPerSecond(105f, 150f, 60f, 30f), Eps);

            float prev = -1f;
            for (float d = 150f; d >= 60f; d -= 10f)
            {
                float h = SunGravityWell.HeatPerSecond(d, 150f, 60f, 30f);
                Assert.GreaterOrEqual(h, prev);
                prev = h;
            }
        }

        // ---- SunGlare.Intensity: 0 when sun behind/outside start, 1 dead-on, monotonic between ----

        [Test]
        public void GlareIntensity_ZeroWhenSunBehindOrOutsideStart()
        {
            // Sun directly behind the view direction.
            Assert.AreEqual(0f, SunGlare.Intensity(Vector3.forward, Vector3.back, 35f, 8f), Eps);
            // Sun 90° to the side — well outside the 35° start angle.
            Assert.AreEqual(0f, SunGlare.Intensity(Vector3.forward, Vector3.right, 35f, 8f), Eps);
        }

        [Test]
        public void GlareIntensity_OneLookingStraightAtSun()
        {
            Assert.AreEqual(1f, SunGlare.Intensity(Vector3.forward, Vector3.forward, 35f, 8f), Eps);
            // Inside the full angle is still fully clamped to 1.
            Assert.AreEqual(1f, SunGlare.Intensity(Vector3.forward, Quaternion.Euler(4f, 0f, 0f) * Vector3.forward, 35f, 8f), Eps);
        }

        [Test]
        public void GlareIntensity_RampsMonotonicallyAsViewNearsSun()
        {
            // Sweep the sun from outside the start angle in toward dead-centre; intensity must rise.
            float prev = -1f;
            for (float angle = 40f; angle >= 0f; angle -= 5f)
            {
                Vector3 toSun = Quaternion.Euler(angle, 0f, 0f) * Vector3.forward;
                float g = SunGlare.Intensity(Vector3.forward, toSun, 35f, 8f);
                Assert.GreaterOrEqual(g, prev);
                prev = g;
            }
            // Halfway across the 35°→8° band (~21.5°) sits near the middle of the ramp.
            float mid = SunGlare.Intensity(Vector3.forward, Quaternion.Euler(21.5f, 0f, 0f) * Vector3.forward, 35f, 8f);
            Assert.AreEqual(0.5f, mid, 0.05f);
        }

        // ---- SunCompass.BearingDeg: 0 dead-ahead, +90 starboard, sign correctness, wrap at ±180 ----

        [Test]
        public void BearingDeg_ZeroWhenSunDeadAhead()
        {
            Assert.AreEqual(0f, SunCompass.BearingDeg(Vector3.forward, Vector3.forward, Vector3.up), Eps);
        }

        [Test]
        public void BearingDeg_PositiveNinetyForStarboardSun()
        {
            Assert.AreEqual(90f, SunCompass.BearingDeg(Vector3.forward, Vector3.right, Vector3.up), Eps);
        }

        [Test]
        public void BearingDeg_NegativeForPortSun()
        {
            Assert.AreEqual(-90f, SunCompass.BearingDeg(Vector3.forward, Vector3.left, Vector3.up), Eps);
        }

        [Test]
        public void BearingDeg_WrapsToOneEightyWhenSunBehind()
        {
            Assert.AreEqual(180f, Mathf.Abs(SunCompass.BearingDeg(Vector3.forward, Vector3.back, Vector3.up)), Eps);
        }

        [Test]
        public void BearingDeg_StaysWithinPlusMinus180()
        {
            // A spread of sun directions in the horizontal plane never escapes the signed range.
            for (float a = 0f; a < 360f; a += 15f)
            {
                Vector3 toSun = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                float bearing = SunCompass.BearingDeg(Vector3.forward, toSun, Vector3.up);
                Assert.GreaterOrEqual(bearing, -180f);
                Assert.LessOrEqual(bearing, 180f);
            }
        }

        [Test]
        public void ElevationDeg_SignedAboveAndBelowHorizon()
        {
            Assert.AreEqual(0f, SunCompass.ElevationDeg(Vector3.forward, Vector3.up), Eps);
            Assert.AreEqual(90f, SunCompass.ElevationDeg(Vector3.up, Vector3.up), Eps);
            Assert.AreEqual(-90f, SunCompass.ElevationDeg(Vector3.down, Vector3.up), Eps);
        }
    }
}
