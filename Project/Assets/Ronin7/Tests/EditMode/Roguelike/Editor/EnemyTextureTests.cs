using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-12 (BUG-23) guard: every baked enemy material samples extracted, ASTC-compressed PNGs
    /// (not glTFast's embedded textures) and reads the MR map as glTF ORM.</summary>
    public class EnemyTextureTests
    {
        private const string MatFolder = "Assets/Ronin7/Prefabs/Roguelike/Enemies/Materials";

        [Test]
        public void EveryEnemyMaterial_UsesExtractedCompressedTextures()
        {
            var paths = AssetDatabase.FindAssets("t:Material", new[] { MatFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Enemy.mat")).ToArray();
            Assert.AreEqual(14, paths.Length, "expected the 14 RL_ enemy materials");
            foreach (var p in paths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                string n = mat.name.Replace("_Enemy", "");
                Assert.AreEqual(1f, mat.GetFloat("_MRGltf"), p + " _MRGltf");
                Check(mat, "_BaseMap", n, "Albedo", p, true, false);
                Check(mat, "_MetallicGlossMap", n, "MR", p, false, false);
                Check(mat, "_BumpMap", n, "Normal", p, false, true);
            }
        }

        private static void Check(Material mat, string prop, string n, string leaf, string ctx, bool srgb, bool normal)
        {
            var tex = mat.GetTexture(prop);
            string tp = tex != null ? AssetDatabase.GetAssetPath(tex) : "<null>";
            StringAssert.EndsWith("/" + n + "_Tex/" + leaf + ".png", tp, ctx + " " + prop);
            var imp = (TextureImporter)AssetImporter.GetAtPath(tp);
            if (!normal) Assert.AreEqual(srgb, imp.sRGBTexture, tp + " sRGB");
            Assert.AreEqual(normal ? TextureImporterType.NormalMap : TextureImporterType.Default, imp.textureType, tp + " type");
            Assert.IsTrue(imp.mipmapEnabled, tp + " mipmaps");
            Assert.LessOrEqual(imp.maxTextureSize, 1024, tp + " maxSize");
            var a = imp.GetPlatformTextureSettings("Android");
            Assert.IsTrue(a.overridden, tp + " Android override");
            Assert.AreEqual(1024, a.maxTextureSize, tp + " Android maxSize");
            Assert.AreEqual(normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6, a.format, tp + " Android format");
        }
    }
}
