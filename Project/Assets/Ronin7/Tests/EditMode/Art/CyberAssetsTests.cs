using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// Gate coverage for the Cyber-Ninja baseline art assets authored for the Sairento-style
    /// graphics pipeline (CyberNeon / DojoTech shaders, their materials, the post-FX profile,
    /// and the CyberNinja prefab). These were hand-authored on disk while the editor MCP was
    /// offline, so this test is what actually verifies them in-engine: shaders compile without
    /// errors, materials bind to the right shader, and the prefab carries exactly the VR rig
    /// the design calls for (one capsule collider, ~1.8 m, a Rigidbody, no overlapping colliders).
    /// </summary>
    public class CyberAssetsTests
    {
        private const string CyberNeonPath = "Assets/Ronin7/Art/Shaders/CyberNeon.shader";
        private const string DojoTechPath = "Assets/Ronin7/Art/Shaders/DojoTech.shader";
        private const string MatBodyPath = "Assets/Ronin7/Art/Materials/Mat_DojoTech_Body.mat";
        private const string MatCyanPath = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Cyan.mat";
        private const string MatMagentaPath = "Assets/Ronin7/Art/Materials/Mat_CyberNeon_Magenta.mat";
        private const string PrefabPath = "Assets/Ronin7/Prefabs/Art/CyberNinja.prefab";
        private const string ProfilePath = "Assets/Ronin7/Settings/Ronin7_VR_PostFX.asset";

        [TestCase(CyberNeonPath, "Ronin7/CyberNeon")]
        [TestCase(DojoTechPath, "Ronin7/DojoTech")]
        public void Shader_Imports_AndCompilesWithoutErrors(string path, string expectedName)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.IsNotNull(shader, $"Shader asset missing at {path}");
            Assert.AreEqual(expectedName, shader.name, "Shader declared name mismatch");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"{expectedName} has compile errors");
            Assert.IsTrue(shader.isSupported, $"{expectedName} is not supported on this platform");
        }

        [TestCase(MatBodyPath, "Ronin7/DojoTech")]
        [TestCase(MatCyanPath, "Ronin7/CyberNeon")]
        [TestCase(MatMagentaPath, "Ronin7/CyberNeon")]
        public void Material_BindsToExpectedShader(string path, string expectedShader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(mat, $"Material missing at {path}");
            Assert.IsNotNull(mat.shader, $"Material at {path} has no shader");
            Assert.AreEqual(expectedShader, mat.shader.name, "Material bound to wrong shader");
        }

        [Test]
        public void CyberNinjaPrefab_HasVrRig_AndNoOverlappingColliders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Prefab missing at {PrefabPath}");

            Assert.IsNotNull(prefab.GetComponent<Rigidbody>(), "CyberNinja must have a Rigidbody");

            // Exactly one collider across the whole hierarchy → no overlaps; and it is the
            // full-body capsule scaled for VR (~1.8 m tall).
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length, "Expected exactly one (non-overlapping) collider");
            var capsule = colliders[0] as CapsuleCollider;
            Assert.IsNotNull(capsule, "The single collider must be a CapsuleCollider");
            Assert.That(capsule.height, Is.EqualTo(1.8f).Within(0.05f), "Capsule height should be ~1.8 m");
        }

        [Test]
        public void CyberNinjaPrefab_UsesBothPipelineShaders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"Prefab missing at {PrefabPath}");

            bool usesDojo = false, usesNeon = false;
            foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var s = r.sharedMaterial != null ? r.sharedMaterial.shader : null;
                if (s == null) continue;
                if (s.name == "Ronin7/DojoTech") usesDojo = true;
                if (s.name == "Ronin7/CyberNeon") usesNeon = true;
            }
            Assert.IsTrue(usesDojo, "Body should use the DojoTech material");
            Assert.IsTrue(usesNeon, "Accents should use the CyberNeon material");
        }

        [Test]
        public void PostFxProfile_Imports_WithOverrides()
        {
            // Loaded as a generic ScriptableObject so the test assembly needs no URP reference;
            // the serialized "components" list holds the Bloom/ColorAdjustments/SplitToning/etc overrides.
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ProfilePath);
            Assert.IsNotNull(profile, $"VolumeProfile missing at {ProfilePath}");
            var so = new SerializedObject(profile);
            var components = so.FindProperty("components");
            Assert.IsNotNull(components, "Profile has no 'components' array");
            Assert.GreaterOrEqual(components.arraySize, 5, "Expected at least 5 post-FX overrides");
        }
    }
}
