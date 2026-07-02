using NUnit.Framework;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    // Foundation guard for the toon shader stack. These tests catch the most common breakages:
    // shader fails to compile (Shader.Find returns null), the .mat regresses to the SRP error
    // shader, or a spec'd material property gets renamed/dropped.
    public class SamuraiToonShaderTests
    {
        private const string ToonShaderName = "Ronin7/SamuraiToon";
        private const string OutlineShaderName = "Ronin7/SamuraiOutline";
        private const string BaseMaterialPath = "Assets/Ronin7/Art/Materials/SamuraiToon_Base.mat";

        [Test]
        public void ToonShader_CompilesAndIsFindable()
        {
            var shader = Shader.Find(ToonShaderName);
            Assert.IsNotNull(shader, $"{ToonShaderName} did not compile or is not registered.");
        }

        [Test]
        public void OutlineShader_CompilesAndIsFindable()
        {
            var shader = Shader.Find(OutlineShaderName);
            Assert.IsNotNull(shader, $"{OutlineShaderName} did not compile or is not registered.");
        }

        [Test]
        public void BaseMaterial_LoadsWithToonShader_NotErrorShader()
        {
#if UNITY_EDITOR
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            Assert.IsNotNull(mat, $"Material not found at {BaseMaterialPath}");
            Assert.IsNotNull(mat.shader, "Material has no shader reference.");
            Assert.AreEqual(ToonShaderName, mat.shader.name,
                $"Base material resolved to '{mat.shader.name}' instead of '{ToonShaderName}'. " +
                "Likely a broken shader GUID in the .mat (resolves to FallbackError/pink).");
#else
            Assert.Ignore("AssetDatabase access requires the editor.");
#endif
        }

        [Test]
        public void BaseMaterial_ExposesSpecifiedProperties()
        {
#if UNITY_EDITOR
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            Assert.IsNotNull(mat, $"Material not found at {BaseMaterialPath}");
            // Properties from the Agent 0 spec.
            Assert.IsTrue(mat.HasProperty("_BaseMap"), "_BaseMap missing");
            Assert.IsTrue(mat.HasProperty("_BaseColor"), "_BaseColor missing");
            Assert.IsTrue(mat.HasProperty("_ShadowTint"), "_ShadowTint missing");
            Assert.IsTrue(mat.HasProperty("_ShadowThreshold"), "_ShadowThreshold missing");
            Assert.IsTrue(mat.HasProperty("_RimColor"), "_RimColor missing");
            Assert.IsTrue(mat.HasProperty("_RimPower"), "_RimPower missing");
            Assert.IsTrue(mat.HasProperty("_RimStrength"), "_RimStrength missing");
            // EP01 art-polish additions: softened ramp + emission (backward-compatible).
            Assert.IsTrue(mat.HasProperty("_RampSmoothness"), "_RampSmoothness missing");
            Assert.IsTrue(mat.HasProperty("_EmissionColor"), "_EmissionColor missing");
            Assert.IsTrue(mat.HasProperty("_EmissionMap"), "_EmissionMap missing");
#else
            Assert.Ignore("AssetDatabase access requires the editor.");
#endif
        }

        // Single Pass Instanced (the Quest default) needs all three stereo macros in EVERY custom
        // pass, or geometry renders at the wrong eye offset — e.g. the inverted-hull outline drew
        // as a dark ghost visible in one eye only. Shader.Find still succeeds without them, so the
        // compile tests above can't catch this; assert at the source level instead.
        [Test]
        public void Shaders_ContainStereoMacros_ForSinglePassInstancedVR()
        {
#if UNITY_EDITOR
            AssertStereoMacros("Assets/Ronin7/Art/Shaders/SamuraiToon.shader");
            AssertStereoMacros("Assets/Ronin7/Art/Shaders/SamuraiOutline.shader");
#else
            Assert.Ignore("File access for shader source requires the editor.");
#endif
        }

#if UNITY_EDITOR
        private static void AssertStereoMacros(string assetPath)
        {
            var projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            var full = System.IO.Path.Combine(projectRoot, assetPath);
            Assert.IsTrue(System.IO.File.Exists(full), $"Shader not found at {assetPath}");
            var src = System.IO.File.ReadAllText(full);
            Assert.IsTrue(src.Contains("UNITY_VERTEX_OUTPUT_STEREO"),
                $"{assetPath} missing UNITY_VERTEX_OUTPUT_STEREO (Varyings stereo slot).");
            Assert.IsTrue(src.Contains("UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO"),
                $"{assetPath} missing UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO (vertex setup).");
            Assert.IsTrue(src.Contains("UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX"),
                $"{assetPath} missing UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX (fragment setup).");
        }
#endif
    }
}
