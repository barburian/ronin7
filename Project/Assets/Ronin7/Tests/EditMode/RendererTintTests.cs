using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>BUG-17: the emission overload drives the GrittyCyber rim; the 2-arg Apply must not touch it.</summary>
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
        public void Apply_WithEmission_SetsBaseColorAndEmission()
        {
            var rim = new Color(2.5f, 1f, 0.6f);
            var mpb = Applied(r => RendererTint.Apply(r, Color.red, rim));

            Assert.AreEqual(Color.red, mpb.GetColor("_BaseColor"));
            Assert.AreEqual(Color.red, mpb.GetColor("_Color"));
            Assert.IsTrue(mpb.HasColor("_EmissionColor"));
            // SetColor/GetColor round-trips through linear space, so compare with tolerance.
            var got = mpb.GetColor("_EmissionColor");
            Assert.AreEqual(rim.r, got.r, 1e-3f);
            Assert.AreEqual(rim.g, got.g, 1e-3f);
            Assert.AreEqual(rim.b, got.b, 1e-3f);
        }

        [Test]
        public void Apply_TwoArg_LeavesEmissionUnset()
        {
            var mpb = Applied(r => RendererTint.Apply(r, Color.red));

            Assert.AreEqual(Color.red, mpb.GetColor("_BaseColor"));
            Assert.IsFalse(mpb.HasColor("_EmissionColor"));
        }

        [Test]
        public void Apply_WithEmission_NullRenderer_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RendererTint.Apply(null, Color.red, Color.black));
        }
    }
}
