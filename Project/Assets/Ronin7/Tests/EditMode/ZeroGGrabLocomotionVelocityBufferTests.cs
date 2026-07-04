using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="ZeroGGrabLocomotion.CircularBuffer{T}"/>, the hand-velocity history used to
    /// compute the release-drift impulse. Before the fix, <c>Average()</c> always divided by the
    /// buffer's full capacity even when fewer samples had been added, diluting a short grab's
    /// release velocity toward zero via unwritten (zero) slots.
    /// </summary>
    public class ZeroGGrabLocomotionVelocityBufferTests
    {
        private static readonly System.Func<Vector3, Vector3, Vector3> Add = (a, b) => a + b;
        private static readonly System.Func<Vector3, float, Vector3> Scale = (sum, scale) => sum * scale;

        [Test]
        public void Average_FewerAddsThanCapacity_AveragesOnlyValidSamples()
        {
            var buffer = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(5);
            buffer.Add(new Vector3(2f, 0f, 0f));
            buffer.Add(new Vector3(4f, 0f, 0f));

            Vector3 avg = buffer.Average(Add, Scale);

            // True average of the 2 samples added (3), NOT diluted by the 3 unwritten zero slots
            // (which would give 6/5 = 1.2).
            Assert.That(avg.x, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void Average_AfterCapacityExceeded_AveragesFullWindow()
        {
            var buffer = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(3);
            buffer.Add(new Vector3(1f, 0f, 0f));
            buffer.Add(new Vector3(2f, 0f, 0f));
            buffer.Add(new Vector3(3f, 0f, 0f));
            buffer.Add(new Vector3(4f, 0f, 0f));
            buffer.Add(new Vector3(5f, 0f, 0f));

            Vector3 avg = buffer.Average(Add, Scale);

            // Once the window is full, the average must still cover exactly `capacity` samples
            // (the last 3 added: 3, 4, 5 -> avg 4), not fewer and not more.
            Assert.That(avg.x, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void Average_NoSamplesYet_ReturnsZeroWithoutThrowing()
        {
            var buffer = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(5);

            Vector3 avg = default;
            Assert.DoesNotThrow(() => avg = buffer.Average(Add, Scale));
            Assert.AreEqual(Vector3.zero, avg);
        }

        [Test]
        public void Constructor_ZeroOrNegativeCapacity_ClampsToOneAndDoesNotThrowOnAdd()
        {
            var zeroCapacity = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(0);
            var negativeCapacity = new ZeroGGrabLocomotion.CircularBuffer<Vector3>(-3);

            Assert.DoesNotThrow(() => zeroCapacity.Add(new Vector3(7f, 0f, 0f)));
            Assert.DoesNotThrow(() => negativeCapacity.Add(new Vector3(9f, 0f, 0f)));

            Assert.That(zeroCapacity.Average(Add, Scale).x, Is.EqualTo(7f).Within(0.0001f));
            Assert.That(negativeCapacity.Average(Add, Scale).x, Is.EqualTo(9f).Within(0.0001f));
        }
    }
}
