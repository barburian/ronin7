using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="AsteroidHazard.InRange"/>, the pure sphere-overlap check extracted out of
    /// the duplicated logic in <c>ScanPlayer</c>/<c>ScanEnemies</c>. "In range" means touching or
    /// closer, i.e. distance <= sum of radii (inclusive at the boundary).
    /// </summary>
    public class AsteroidHazardRangeTests
    {
        [Test]
        public void InRange_WellOutsideCombinedRadii_ReturnsFalse()
        {
            Vector3 a = Vector3.zero;
            Vector3 b = new Vector3(100f, 0f, 0f);

            Assert.IsFalse(AsteroidHazard.InRange(a, 1f, b, 1f));
        }

        [Test]
        public void InRange_WellInsideCombinedRadii_ReturnsTrue()
        {
            Vector3 a = Vector3.zero;
            Vector3 b = new Vector3(2f, 0f, 0f);

            Assert.IsTrue(AsteroidHazard.InRange(a, 5f, b, 5f));
        }

        [Test]
        public void InRange_ExactlyAtCombinedRadii_ReturnsTrue()
        {
            Vector3 a = Vector3.zero;
            Vector3 b = new Vector3(10f, 0f, 0f);

            // distance == radiusA + radiusB exactly -> boundary must count as in-range.
            Assert.IsTrue(AsteroidHazard.InRange(a, 4f, b, 6f));
        }

        [Test]
        public void InRange_ZeroRadiiBothAtSamePoint_ReturnsTrue()
        {
            Vector3 p = new Vector3(3f, 4f, 5f);

            Assert.IsTrue(AsteroidHazard.InRange(p, 0f, p, 0f));
        }
    }
}
