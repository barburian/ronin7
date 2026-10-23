using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// P5-05 biome kits (Program, Rust; A13.6): the built prefabs stay inside the kit-table triangle budgets,
    /// use BoxColliders only, match their spec footprint, and the whole kit draws with at most 4 materials.
    /// The hero prop (AI eye core) is the one exception: its own material, its own tri target (MaxTris 12k for the
    /// raw image->3D mesh; the pending retopo lowers it), and probe-lit instead of lightmapped.
    /// The hero's bounds are checked by max dimension (= HeroDiameter), so swapping placeholder -> AI mesh keeps passing.
    /// Rebuild with Tools/Space Samurai/Art/Build Biome Kit/&lt;biome&gt; if these fail after a builder change.
    /// </summary>
    public class BiomeKitTests
    {
        private static readonly string[] Kits = { "Program", "Rust" };
        private static BiomeKit Kit(string name) => name == "Rust" ? BiomeKitBuilder.Rust : BiomeKitBuilder.Program;

        private static IEnumerable<(KitPiece piece, GameObject prefab)> Prefabs(BiomeKit kit)
        {
            foreach (var p in kit.Pieces)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(kit.PrefabPath(p));
                Assert.IsNotNull(go, $"missing prefab {kit.PrefabPath(p)}");
                yield return (p, go);
            }
        }

        [TestCaseSource(nameof(Kits))]
        public void BiomeKit_PrefabsWithinTriBudget(string kit)
        {
            foreach (var (piece, go) in Prefabs(Kit(kit)))
            {
                int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.Greater(tris, 0, piece.Name);
                Assert.LessOrEqual(tris, piece.MaxTris, piece.Name);
            }
        }

        [TestCaseSource(nameof(Kits))]
        public void BiomeKit_BoxCollidersOnly_AndStatic(string kit)
        {
            foreach (var (piece, go) in Prefabs(Kit(kit)))
            {
                Assert.IsEmpty(go.GetComponentsInChildren<MeshCollider>(true), piece.Name);
                Assert.IsNotEmpty(go.GetComponents<BoxCollider>(), piece.Name);
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                bool hero = piece.SourceModel != null;
                Assert.AreEqual(!hero, (flags & StaticEditorFlags.ContributeGI) != 0, piece.Name);
                Assert.IsTrue((flags & StaticEditorFlags.BatchingStatic) != 0, piece.Name);
                if (hero) Assert.AreEqual(ReceiveGI.LightProbes, go.GetComponent<MeshRenderer>().receiveGI, piece.Name);
            }
        }

        [TestCaseSource(nameof(Kits))]
        public void BiomeKit_BoundsMatchSpec(string kit)
        {
            foreach (var (piece, go) in Prefabs(Kit(kit)))
            {
                var size = go.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                if (piece.SourceModel != null)
                {
                    Assert.AreEqual(piece.HeroDiameter, Mathf.Max(size.x, size.y, size.z), 0.05f, piece.Name);
                    continue;
                }
                Assert.Less((size - piece.Size).magnitude, 0.05f, $"{piece.Name}: {size} vs spec {piece.Size}");
            }
        }

        [TestCaseSource(nameof(Kits))]
        public void BiomeKit_AtMostFourMaterials(string kit)
        {
            var mats = new HashSet<Material>();
            foreach (var (piece, go) in Prefabs(Kit(kit)))
            {
                if (piece.SourceModel != null)
                {
                    Assert.AreEqual(1, go.GetComponent<Renderer>().sharedMaterials.Length, piece.Name);
                    continue; // the hero's own material is the single allowed exception
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, $"{piece.Name} has an empty material slot");
                        mats.Add(m);
                    }
            }
            Assert.LessOrEqual(mats.Count, 4);
        }
    }
}
