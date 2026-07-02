using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    public class CollapseSequenceControllerTests
    {
        /// <summary>
        /// Test that HighestDroppableSegment never returns a segment at or ahead of the player.
        /// </summary>
        [Test]
        public void HighestDroppableSegment_NeverReturnsSegmentAheadOfPlayer()
        {
            var thresholds = new float[] { 0.2f, 0.4f, 0.6f, 0.8f };

            // At progress 0.1f (before first threshold), nothing drops.
            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(0.1f, thresholds));

            // At progress 0.25f, segment 0 is droppable (past segment 1's threshold? No, 0.25 < 0.4).
            // Actually: segment i droppable when progress > thresholds[i+1].
            // So segment 0 droppable when progress > thresholds[1] = 0.4f.
            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(0.25f, thresholds));

            // At progress 0.41f, segment 0 is droppable.
            Assert.AreEqual(0, CollapseSequenceController.HighestDroppableSegment(0.41f, thresholds));

            // At progress 0.61f, segments 0 and 1 are droppable; highest is 1.
            Assert.AreEqual(1, CollapseSequenceController.HighestDroppableSegment(0.61f, thresholds));

            // At progress 0.81f, segments 0, 1, 2 are droppable; highest is 2.
            Assert.AreEqual(2, CollapseSequenceController.HighestDroppableSegment(0.81f, thresholds));

            // At progress 0.99f, all segments including last are droppable; highest is 3.
            Assert.AreEqual(3, CollapseSequenceController.HighestDroppableSegment(0.99f, thresholds));
        }

        /// <summary>
        /// Test monotonic behavior: as progress increases, HighestDroppableSegment never decreases.
        /// </summary>
        [Test]
        public void HighestDroppableSegment_IsMonotonicWithProgress()
        {
            var thresholds = new float[] { 0.25f, 0.5f, 0.75f };
            int last = -1;

            for (float progress = 0f; progress <= 1f; progress += 0.05f)
            {
                int current = CollapseSequenceController.HighestDroppableSegment(progress, thresholds);
                Assert.GreaterOrEqual(current, last, $"Progress {progress}: should not decrease from {last} to {current}");
                last = current;
            }
        }

        /// <summary>
        /// Test with a single segment (length 1): nothing at progress 0, last segment droppable near 1.
        /// </summary>
        [Test]
        public void HighestDroppableSegment_SingleSegment()
        {
            var thresholds = new float[] { 0.5f };

            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(0f, thresholds));
            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(0.5f, thresholds));
            Assert.AreEqual(0, CollapseSequenceController.HighestDroppableSegment(0.98f, thresholds));
            Assert.AreEqual(0, CollapseSequenceController.HighestDroppableSegment(1f, thresholds));
        }

        /// <summary>
        /// Test with empty segments array: always returns -1.
        /// </summary>
        [Test]
        public void HighestDroppableSegment_EmptyArray()
        {
            var thresholds = new float[0];
            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(0.5f, thresholds));
            Assert.AreEqual(-1, CollapseSequenceController.HighestDroppableSegment(1f, thresholds));
        }
    }
}
