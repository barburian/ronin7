using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>ReverbPresetSelector.Classify's footprint/aspect tiering and the min/max distance
    /// it derives for each non-Off preset.</summary>
    public class ReverbPresetSelectorTests
    {
        private static Bounds Cube(float size) => new Bounds(Vector3.zero, new Vector3(size, size, size));

        [Test]
        public void Classify_2mRoom_ReturnsRoom()
        {
            var (preset, min, max) = ReverbPresetSelector.Classify(Cube(2f));
            Assert.AreEqual(AudioReverbPreset.Room, preset);
            Assert.Less(min, max);
        }

        [Test]
        public void Classify_10mChamber_ReturnsHallway()
        {
            var (preset, min, max) = ReverbPresetSelector.Classify(Cube(10f));
            Assert.AreEqual(AudioReverbPreset.Hallway, preset);
            Assert.Less(min, max);
        }

        [Test]
        public void Classify_40mHangar_ReturnsHangar()
        {
            var (preset, min, max) = ReverbPresetSelector.Classify(Cube(40f));
            Assert.AreEqual(AudioReverbPreset.Hangar, preset);
            Assert.Less(min, max);
        }

        [Test]
        public void Classify_200mOpen_ReturnsOff()
        {
            var (preset, min, max) = ReverbPresetSelector.Classify(Cube(200f));
            Assert.AreEqual(AudioReverbPreset.Off, preset);
            Assert.AreEqual(0f, min);
            Assert.AreEqual(0f, max);
        }

        [Test]
        public void Classify_ElongatedSmallFootprint_UpTiersToHallway()
        {
            // 5m long x 1m wide: below the RoomMaxSpan(6) size threshold, but a 5:1 aspect reads
            // like a corridor, not a room.
            var bounds = new Bounds(Vector3.zero, new Vector3(5f, 2f, 1f));
            var (preset, min, max) = ReverbPresetSelector.Classify(bounds);
            Assert.AreEqual(AudioReverbPreset.Hallway, preset);
            Assert.Less(min, max);
        }

        [Test]
        public void Classify_ZeroBounds_DoesNotThrowAndReturnsRoom()
        {
            (AudioReverbPreset preset, float min, float max) result = default;
            Assert.DoesNotThrow(() => result = ReverbPresetSelector.Classify(new Bounds(Vector3.zero, Vector3.zero)));
            Assert.AreEqual(AudioReverbPreset.Room, result.preset);
            Assert.Less(result.min, result.max);
        }

        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(6f)]
        [TestCase(10f)]
        [TestCase(20f)]
        [TestCase(40f)]
        [TestCase(79f)]
        public void Classify_AnyNonOffPreset_MinIsLessThanMax(float size)
        {
            var (preset, min, max) = ReverbPresetSelector.Classify(Cube(size));
            Assert.AreNotEqual(AudioReverbPreset.Off, preset, "test size chosen to stay under the Off threshold");
            Assert.Less(min, max);
        }
    }
}
