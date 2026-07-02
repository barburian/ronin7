using NUnit.Framework;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// T3.3: the ship integrates its orientation by repeated quaternion multiply. Without
    /// renormalisation the quaternion slowly drifts off unit length over a long session and would
    /// skew/scale the rendered world. These cover the pure accumulate+normalise helper used in Update.
    /// </summary>
    public class ShipControllerRotationTests
    {
        [Test]
        public void AccumulateRotation_OverManySteps_StaysUnitLength()
        {
            Quaternion q = Quaternion.identity;
            float maxDeviation = 0f;

            // A long session's worth of small per-frame steps (~16 min at 90 fps).
            for (int i = 0; i < 90000; i++)
            {
                q = ShipController.AccumulateRotation(q, 0.37f, -0.21f, 0.13f);
                float length = Mathf.Sqrt(Quaternion.Dot(q, q));
                maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(length - 1f));
            }

            Assert.That(maxDeviation, Is.LessThan(1e-4f),
                $"Quaternion drifted off unit length (max deviation {maxDeviation}).");
        }

        [Test]
        public void AccumulateRotation_PreservesRotationDirection()
        {
            // Normalising must not change the orientation: a single step matches a raw multiply (which
            // is itself near-unit), so flight feel is identical.
            Quaternion start = Quaternion.Euler(12f, -34f, 56f);
            Quaternion raw = start * Quaternion.Euler(0.4f, 1.1f, -0.7f);
            Quaternion helper = ShipController.AccumulateRotation(start, 0.4f, 1.1f, -0.7f);

            Vector3 v = new Vector3(0.3f, -0.6f, 1.2f);
            Assert.That(Vector3.Distance(raw * v, helper * v), Is.LessThan(1e-4f),
                "Normalising should not change the rotation direction.");
        }
    }
}
