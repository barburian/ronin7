using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// The pure Perlin-noise flicker core of ConsoleFlickerLight: band-limited output range,
    /// determinism, seed variation, and the anti-strobe bound on how much a small time step can move
    /// the intensity. All side-effect free — no scene/MonoBehaviour instance needed.
    /// </summary>
    public class ConsoleFlickerLightTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void MaxLessOrEqualMin_ReturnsMin()
        {
            Assert.AreEqual(0.8f, ConsoleFlickerLight.FlickerIntensity(3f, 0.6f, 1f, 0.8f, 0.8f), Eps);
            Assert.AreEqual(0.8f, ConsoleFlickerLight.FlickerIntensity(3f, 0.6f, 1f, 0.8f, 0.5f), Eps);
        }

        [Test]
        public void StaysWithinMinMaxRange()
        {
            for (float t = 0f; t <= 20f; t += 0.1f)
            {
                float v = ConsoleFlickerLight.FlickerIntensity(t, 0.6f, 1f, 0.8f, 1.2f);
                Assert.GreaterOrEqual(v, 0.8f);
                Assert.LessOrEqual(v, 1.2f);
            }
        }

        [Test]
        public void SameInputs_IsDeterministic()
        {
            float a = ConsoleFlickerLight.FlickerIntensity(4.2f, 0.6f, 3f, 0.8f, 1.2f);
            float b = ConsoleFlickerLight.FlickerIntensity(4.2f, 0.6f, 3f, 0.8f, 1.2f);
            Assert.AreEqual(a, b, Eps);
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            bool foundDifference = false;
            for (float t = 0f; t <= 10f; t += 0.5f)
            {
                float a = ConsoleFlickerLight.FlickerIntensity(t, 0.6f, 1f, 0f, 1f);
                float b = ConsoleFlickerLight.FlickerIntensity(t, 0.6f, 7f, 0f, 1f);
                if (System.Math.Abs(a - b) > Eps)
                {
                    foundDifference = true;
                    break;
                }
            }
            Assert.IsTrue(foundDifference, "Different seeds should sample a different noise row.");
        }

        [Test]
        public void ZeroSpeed_IsConstantOverTime()
        {
            float first = ConsoleFlickerLight.FlickerIntensity(0f, 0f, 2f, 0.8f, 1.2f);
            for (float t = 1f; t <= 50f; t += 5f)
            {
                float v = ConsoleFlickerLight.FlickerIntensity(t, 0f, 2f, 0.8f, 1.2f);
                Assert.AreEqual(first, v, Eps);
            }
        }

        [Test]
        public void SmallTimeStep_ProducesBoundedDelta()
        {
            const float dt = 0.001f;
            const float speed = 1f;
            const float min = 0f;
            const float max = 1f;

            float maxDelta = 0f;
            for (float t = 0f; t <= 20f; t += 0.37f)
            {
                float a = ConsoleFlickerLight.FlickerIntensity(t, speed, 5f, min, max);
                float b = ConsoleFlickerLight.FlickerIntensity(t + dt, speed, 5f, min, max);
                maxDelta = System.Math.Max(maxDelta, System.Math.Abs(b - a));
            }

            // Anti-strobe: Perlin continuity means a 1ms step can only nudge the intensity a small
            // fraction of the full range, unlike a fresh-random-sample-per-frame flicker which could
            // jump anywhere in [min, max]. Bound derived empirically from the sweep above with margin.
            Assert.Less(maxDelta, 0.05f);
        }
    }
}
