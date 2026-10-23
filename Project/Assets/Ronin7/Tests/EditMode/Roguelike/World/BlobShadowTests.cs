using NUnit.Framework;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    public class BlobShadowTests
    {
        [Test]
        public void Wants_OnlyOnLowTier_PcKeepsRealtimeShadows()
        {
            Assert.IsTrue(BlobShadow.Wants(GraphicsQuality.Low));
            Assert.IsFalse(BlobShadow.Wants(GraphicsQuality.High));
        }

        [Test]
        public void Diameter_FromWiderXzFootprint_PaddedAndClamped()
        {
            Assert.AreEqual(1f * BlobShadow.Pad, BlobShadow.Diameter(new Vector3(0.6f, 2f, 1f)), 1e-5f); // height ignored
            Assert.AreEqual(BlobShadow.MinDiameter, BlobShadow.Diameter(new Vector3(0.05f, 1f, 0.05f)));
            Assert.AreEqual(BlobShadow.MaxDiameter, BlobShadow.Diameter(new Vector3(10f, 1f, 2f)));
        }

        [Test]
        public void Material_InResources_Instanced_NoZWrite()
        {
            var m = Resources.Load<Material>("Vfx/BlobShadow");
            Assert.IsNotNull(m);
            Assert.IsTrue(m.enableInstancing);
            Assert.AreEqual(0f, m.GetFloat("_ZWrite"));
            Assert.Greater(m.renderQueue, (int)UnityEngine.Rendering.RenderQueue.GeometryLast); // transparent range
        }
    }
}
