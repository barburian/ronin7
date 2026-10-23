using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Ronin7.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P5-06 invariants: per-biome AI skyboxes, their import settings, the far vista budget and
    /// the 300 m far clip. Rebuild via "Tools/Space Samurai/Art/Build Skies" if these fail.</summary>
    public class SkyVistaTests
    {
        private const string LibraryPath = "Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset";
        private static readonly string[] SkyNames = { "Rust", "Program", "Garden", "BootNebula" };

        private static ArenaRoomLibrary Library() => AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>(LibraryPath);

        [Test]
        public void EveryBiome_HasCubemapSkybox()
        {
            foreach (var b in Library().biomes)
            {
                Assert.IsNotNull(b.skybox, $"{b.id}: no skybox");
                Assert.AreEqual("Skybox/Cubemap", b.skybox.shader.name, b.id);
                Assert.IsInstanceOf<Cubemap>(b.skybox.GetTexture("_Tex"), b.id);
            }
        }

        [TestCaseSource(nameof(SkyNames))]
        public void SkyTexture_ImportSettings(string sky)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath($"Assets/Ronin7/Art/Sky/Sky_{sky}.png");
            Assert.IsNotNull(imp, sky);
            Assert.AreEqual(TextureImporterShape.TextureCube, imp.textureShape);
            Assert.AreEqual(TextureImporterGenerateCubemap.Cylindrical, imp.generateCubemap, "Lat-Long");
            Assert.IsFalse(imp.mipmapEnabled);
            Assert.LessOrEqual(imp.maxTextureSize, 2048);
            var android = imp.GetPlatformTextureSettings("Android");
            Assert.IsTrue(android.overridden);
            Assert.LessOrEqual(android.maxTextureSize, 1024);
            Assert.AreEqual(TextureImporterFormat.ASTC_8x8, android.format);
        }

        [Test]
        public void EveryBiome_VistaIsOneUnlitDrawUnder2kTrisInsideFarClip()
        {
            foreach (var b in Library().biomes)
            {
                Assert.IsNotNull(b.vista, $"{b.id}: no vista");
                var renderers = b.vista.GetComponentsInChildren<Renderer>(true);
                Assert.AreEqual(1, renderers.Length, "vista must be one draw");
                var r = renderers[0];
                Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode);
                Assert.AreEqual(1, r.sharedMaterials.Length);
                Assert.AreEqual("Ronin7/VistaUnlit", r.sharedMaterial.shader.name, "unlit, no fog");
                Assert.AreEqual(0, b.vista.GetComponentsInChildren<Light>(true).Length);
                Mesh m = b.vista.GetComponent<MeshFilter>().sharedMesh;
                Assert.LessOrEqual(m.triangles.Length / 3, 2000);
                float far = 0f;
                foreach (Vector3 v in m.vertices) far = Mathf.Max(far, v.magnitude);
                Assert.Less(far, 290f, "vista must sit inside the 300 m far clip");
            }
        }

        [TestCase("Assets/Ronin7/Scenes/Phase6_Boot.unity")]
        [TestCase("Assets/Ronin7/Scenes/RunArena.unity")]
        public void RoguelikeScene_CameraFarClipAtMost300(string scenePath)
        {
            var matches = Regex.Matches(File.ReadAllText(scenePath), @"far clip plane: ([0-9.eE+-]+)");
            Assert.Greater(matches.Count, 0, "no camera in " + scenePath);
            foreach (Match m in matches)
                Assert.LessOrEqual(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 300f, scenePath);
        }
    }
}
