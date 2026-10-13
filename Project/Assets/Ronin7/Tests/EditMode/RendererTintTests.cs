using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>BUG-17 / P1-11: the rim overload drives the GrittyCyber _RimColor and Flash drives _FlashAmount;
    /// the 2-arg Apply must not touch the rim.</summary>
    public class RendererTintTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private MaterialPropertyBlock Applied(System.Action<Renderer> apply)
        {
            _go = new GameObject("RendererTintTestHost");
            var r = _go.AddComponent<MeshRenderer>();
            apply(r);
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            return mpb;
        }

        [Test]
        public void Apply_WritesRimAndBaseColorFactor()
        {
            var rim = new Color(2.5f, 1f, 0.6f);
            var mpb = Applied(r => RendererTint.Apply(r, Color.red, rim));

            Assert.AreEqual(Color.red, mpb.GetColor("_BaseColor"));
            Assert.AreEqual(Color.red, mpb.GetColor("_Color"));
            Assert.AreEqual(Color.red, mpb.GetColor("baseColorFactor")); // glTFast stop-gap
            Assert.IsTrue(mpb.HasColor("_RimColor"));
            // SetColor/GetColor round-trips through linear space, so compare with tolerance.
            var got = mpb.GetColor("_RimColor");
            Assert.AreEqual(rim.r, got.r, 1e-3f);
            Assert.AreEqual(rim.g, got.g, 1e-3f);
            Assert.AreEqual(rim.b, got.b, 1e-3f);
        }

        [Test]
        public void Apply_TwoArg_LeavesRimUnset()
        {
            // SamuraiToon carries its own material _RimColor; a plain tint must not black it out.
            var mpb = Applied(r => RendererTint.Apply(r, Color.red));

            Assert.AreEqual(Color.red, mpb.GetColor("_BaseColor"));
            Assert.AreEqual(Color.red, mpb.GetColor("baseColorFactor"));
            Assert.IsFalse(mpb.HasColor("_RimColor"));
        }

        [Test]
        public void Flash_WritesFlashAmount_KeepsTint()
        {
            var mpb = Applied(r =>
            {
                RendererTint.Apply(r, Color.red, Color.black);
                RendererTint.Flash(r, 0.6f);
            });

            Assert.AreEqual(0.6f, mpb.GetFloat("_FlashAmount"), 1e-5f);
            Assert.AreEqual(Color.red, mpb.GetColor("_BaseColor"));
        }

        [Test]
        public void Flash_NullRenderer_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RendererTint.Flash(null, 1f));
        }

        [Test]
        public void Apply_WithEmission_NullRenderer_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RendererTint.Apply(null, Color.red, Color.black));
        }
    }
}
