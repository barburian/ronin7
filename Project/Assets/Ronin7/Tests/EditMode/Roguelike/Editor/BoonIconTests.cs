using System.IO;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P5-07: every boon in the catalog carries a tintable icon (alpha from luminance). Rebuild via
    /// "Tools/Space Samurai/Art/Build Boon Icons" then "Tools/Space Samurai/Roguelike/Build Boon Catalog".</summary>
    public class BoonIconTests
    {
        [Test]
        public void BoonCatalog_EveryBoonHasIcon()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoonCatalog>("Assets/Ronin7/Data/Boons/BoonCatalog.asset");
            Assert.IsNotNull(catalog);
            Assert.AreEqual(45, catalog.boons.Length);
            foreach (var boon in catalog.boons)
            {
                Assert.IsNotNull(boon.icon, $"{boon.name}: no icon");
                string path = AssetDatabase.GetAssetPath(boon.icon);
                StringAssert.EndsWith("/Icon_" + boon.name + ".png", path, boon.name);

                // Read the source PNG (the import is not CPU-readable): the corner must be transparent.
                var tex = new Texture2D(2, 2);
                try
                {
                    tex.LoadImage(File.ReadAllBytes(path));
                    Assert.Less(tex.GetPixel(0, 0).a, 0.1f, $"{boon.name}: icon has no alpha (opaque corner)");
                    float maxA = 0f;
                    foreach (var c in tex.GetPixels32()) maxA = Mathf.Max(maxA, c.a);
                    Assert.Greater(maxA, 128f, $"{boon.name}: icon is blank");
                }
                finally { Object.DestroyImmediate(tex); }
            }
        }
    }
}
