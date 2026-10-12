using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers <see cref="VrUiMaterials.SharedUnlit"/>: the cached material that keeps runtime-built
    /// primitives from rendering magenta on player builds.
    /// </summary>
    public class VrUiMaterialsTests
    {
        [Test]
        public void SharedUnlit_IsNonNull_WithUrpOrDocumentedFallbackShader()
        {
            var mat = VrUiMaterials.SharedUnlit;
            Assert.IsNotNull(mat);
            Assert.IsNotNull(mat.shader);
            var shaderName = mat.shader.name;
            Assert.IsTrue(shaderName.StartsWith("Universal Render Pipeline/") || shaderName == "Unlit/Color",
                $"Unexpected shader '{shaderName}'");
        }

        [Test]
        public void SharedUnlit_ReturnsSameInstance()
        {
            Assert.AreSame(VrUiMaterials.SharedUnlit, VrUiMaterials.SharedUnlit);
        }
    }
}
