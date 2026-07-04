using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Covers InteriorVolume.WorldBounds -- combining the authored center offset + size
    /// with the GameObject's transform position. ReverbPresetSelector.Classify itself is already
    /// covered by ReverbPresetSelectorTests, so this doesn't re-test that.</summary>
    public class InteriorVolumeTests
    {
        [Test]
        public void WorldBounds_CombinesCenterOffsetAndTransformPosition()
        {
            var go = new GameObject("TestInteriorVolume");
            go.transform.position = new Vector3(10f, 0f, 5f);
            var volume = go.AddComponent<InteriorVolume>();
            volume.centerOffset = new Vector3(1f, 2f, 3f);
            volume.size = new Vector3(4f, 6f, 8f);

            Bounds bounds = volume.WorldBounds;

            Assert.AreEqual(new Vector3(11f, 2f, 8f), bounds.center);
            Assert.AreEqual(new Vector3(4f, 6f, 8f), bounds.size);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WorldBounds_ZeroOffset_UsesTransformPositionOnly()
        {
            var go = new GameObject("TestInteriorVolume");
            go.transform.position = new Vector3(-2f, 1f, 3f);
            var volume = go.AddComponent<InteriorVolume>();
            // centerOffset left at its default (Vector3.zero).

            Bounds bounds = volume.WorldBounds;

            Assert.AreEqual(go.transform.position, bounds.center);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WorldBounds_MovingTransform_MovesBoundsByTheSameAmount()
        {
            var go = new GameObject("TestInteriorVolume");
            var volume = go.AddComponent<InteriorVolume>();
            volume.centerOffset = new Vector3(0f, 1f, 0f);

            Vector3 before = volume.WorldBounds.center;
            go.transform.position += new Vector3(5f, 0f, -3f);
            Vector3 after = volume.WorldBounds.center;

            Assert.AreEqual(new Vector3(5f, 0f, -3f), after - before);

            Object.DestroyImmediate(go);
        }
    }
}
