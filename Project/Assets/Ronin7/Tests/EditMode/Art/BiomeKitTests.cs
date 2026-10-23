using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// P5-05 Program biome kit (A13.6): the built prefabs stay inside the kit-table triangle budgets,
    /// use BoxColliders only, match their spec footprint, and the whole kit draws with at most 4 materials.
    /// Rebuild with Tools/Space Samurai/Art/Build Biome Kit/Program if these fail after a builder change.
    /// </summary>
    public class BiomeKitTests
    {
        private static IEnumerable<(KitPiece piece, GameObject prefab)> Prefabs(BiomeKit kit)
        {
            foreach (var p in kit.Pieces)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(kit.PrefabPath(p));
                Assert.IsNotNull(go, $"missing prefab {kit.PrefabPath(p)}");
                yield return (p, go);
            }
        }

        [Test]
        public void BiomeKit_Program_PrefabsWithinTriBudget()
        {
            foreach (var (piece, go) in Prefabs(BiomeKitBuilder.Program))
            {
                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.Greater(tris, 0, piece.Name);
                Assert.LessOrEqual(tris, piece.MaxTris, piece.Name);
            }
        }

        [Test]
        public void BiomeKit_Program_BoxCollidersOnly_AndStatic()
        {
            foreach (var (piece, go) in Prefabs(BiomeKitBuilder.Program))
            {
                Assert.IsEmpty(go.GetComponentsInChildren<MeshCollider>(true), piece.Name);
                Assert.IsNotEmpty(go.GetComponents<BoxCollider>(), piece.Name);
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                Assert.IsTrue((flags & StaticEditorFlags.ContributeGI) != 0, piece.Name);
                Assert.IsTrue((flags & StaticEditorFlags.BatchingStatic) != 0, piece.Name);
            }
        }

        [Test]
        public void BiomeKit_Program_BoundsMatchSpec()
        {
            foreach (var (piece, go) in Prefabs(BiomeKitBuilder.Program))
            {
                var size = go.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                Assert.Less((size - piece.Size).magnitude, 0.05f, $"{piece.Name}: {size} vs spec {piece.Size}");
            }
        }

        [Test]
        public void BiomeKit_Program_AtMostFourMaterials()
        {
            var mats = new HashSet<Material>();
            foreach (var (piece, go) in Prefabs(BiomeKitBuilder.Program))
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, $"{piece.Name} has an empty material slot");
                        mats.Add(m);
                    }
            Assert.LessOrEqual(mats.Count, 4);
        }
    }
}
