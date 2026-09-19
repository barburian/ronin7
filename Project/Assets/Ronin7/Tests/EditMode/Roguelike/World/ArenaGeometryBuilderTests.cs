using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    /// <summary>
    /// Covers <see cref="ArenaGeometryBuilder"/>: the deterministic prop layout (pure, no
    /// GameObjects) and the built shell's structural contract — primitive colliders only (never a
    /// MeshCollider — this project has an explicit, documented prohibition on introducing one),
    /// floor/ceiling/four walls/accent lights present, graceful greybox fallback with no biome.
    /// </summary>
    public class ArenaGeometryBuilderTests
    {
        private GameObject _parent;

        [TearDown]
        public void TearDown()
        {
            if (_parent != null) Object.DestroyImmediate(_parent);
            // Static on the test framework — must not leak into the next test class.
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- PropPositions: pure, deterministic layout ----

        [Test]
        public void PropPositions_ZeroOrNegativeCount_ReturnsEmptyList()
        {
            var rng = new RunRng(1);
            Assert.AreEqual(0, ArenaGeometryBuilder.PropPositions(12f, 0, ref rng).Count);

            var rng2 = new RunRng(1);
            Assert.AreEqual(0, ArenaGeometryBuilder.PropPositions(12f, -2, ref rng2).Count);
        }

        [Test]
        public void PropPositions_ReturnsExactlyRequestedCount()
        {
            var rng = new RunRng(5);
            var positions = ArenaGeometryBuilder.PropPositions(12f, 6, ref rng);
            Assert.AreEqual(6, positions.Count);
        }

        [Test]
        public void PropPositions_AllWithinHalfExtentMargin()
        {
            var rng = new RunRng(9);
            float halfExtent = 12f;
            // The real guarantee is halfExtent minus PropPositions' own wall margin (private
            // PropWallMargin = 1.5f), not the raw halfExtent — asserting against halfExtent alone
            // would still pass with a prop standing inside the wall.
            float maxRadius = halfExtent - 1.5f;
            var positions = ArenaGeometryBuilder.PropPositions(halfExtent, 8, ref rng);

            foreach (var p in positions)
            {
                Assert.LessOrEqual(new Vector2(p.x, p.z).magnitude, maxRadius + 0.001f);
                Assert.AreEqual(0f, p.y); // props sit on the floor
            }
        }

        [Test]
        public void PropPositions_SameSeed_ProducesIdenticalLayout()
        {
            var rngA = new RunRng(777);
            var rngB = new RunRng(777);

            var a = ArenaGeometryBuilder.PropPositions(12f, 5, ref rngA);
            var b = ArenaGeometryBuilder.PropPositions(12f, 5, ref rngB);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
                Assert.AreEqual(a[i], b[i]);
        }

        // ---- Build: structural contract ----

        [Test]
        public void Build_CreatesFloorCeilingFourWallsAndAccentLights()
        {
            _parent = new GameObject("ArenaParent");
            var rng = new RunRng(1);

            ArenaGeometryBuilder.Build(_parent.transform, 12f, null, ref rng);

            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Floor"));
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Ceiling"));
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Wall_North"));
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Wall_South"));
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Wall_East"));
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Wall_West"));

            var lights = _parent.GetComponentsInChildren<Light>();
            Assert.AreEqual(2, lights.Length); // A5.7: dropped 4 -> 2 (Quest per-pixel light budget)
        }

        [Test]
        public void Build_NeverCreatesAMeshCollider()
        {
            _parent = new GameObject("ArenaParent");
            var rng = new RunRng(2);

            ArenaGeometryBuilder.Build(_parent.transform, 12f, null, ref rng);

            var colliders = _parent.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
                Assert.IsNotInstanceOf<MeshCollider>(c, $"{c.gameObject.name} must not carry a MeshCollider");
        }

        [Test]
        public void Build_PropPrefabCarryingAMeshCollider_StripsItAndLogsError()
        {
            // A5.7: Build_NeverCreatesAMeshCollider above passes a null biome, so it never actually
            // exercises the prop-scatter path — a designer-authored Tripo prop prefab can carry a
            // MeshCollider (e.g. from FBX "Generate Colliders") straight into the arena undetected.
            _parent = new GameObject("ArenaParent");
            var propSource = new GameObject("PropWithMeshCollider");
            var meshFilter = propSource.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = new Mesh();
            propSource.AddComponent<MeshCollider>();

            var biome = new ArenaRoomLibrary.Biome { propPrefabs = new[] { propSource } };
            var rng = new RunRng(11);

            // The scatter places several props from this one prefab, so the builder logs one error
            // per stripped collider. LogAssert.Expect only consumes a single message and the test
            // framework fails on any unhandled LogError, so tolerate the repeats — the Expect below
            // still proves at least one was logged.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MeshCollider"));
            LogAssert.ignoreFailingMessages = true;
            ArenaGeometryBuilder.Build(_parent.transform, 12f, biome, ref rng);

            var meshColliders = _parent.GetComponentsInChildren<MeshCollider>(true);
            Assert.AreEqual(0, meshColliders.Length, "The instantiated prop's MeshCollider must be stripped.");

            Object.DestroyImmediate(propSource);
        }

        [Test]
        public void Build_FloorAndWallsHaveEnabledColliders_CeilingColliderIsDisabled()
        {
            // A5.2: Object.Destroy on a Collider is illegal outside play mode (logs an error, does
            // nothing) and would otherwise ship an invisible ceiling collider teleport arcs/raycasts
            // hit. The ceiling keeps its collider component but disabled — never Destroy.
            _parent = new GameObject("ArenaParent");
            var rng = new RunRng(3);

            ArenaGeometryBuilder.Build(_parent.transform, 12f, null, ref rng);

            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Floor").GetComponent<Collider>());
            Assert.IsTrue(_parent.transform.Find("ArenaGeometry/Floor").GetComponent<Collider>().enabled);
            Assert.IsNotNull(_parent.transform.Find("ArenaGeometry/Wall_North").GetComponent<Collider>());
            Assert.IsTrue(_parent.transform.Find("ArenaGeometry/Wall_North").GetComponent<Collider>().enabled);

            var ceilingCollider = _parent.transform.Find("ArenaGeometry/Ceiling").GetComponent<Collider>();
            Assert.IsNotNull(ceilingCollider, "Ceiling must still carry a Collider component (never destroyed outside play mode).");
            Assert.IsFalse(ceilingCollider.enabled, "Ceiling's collider must be disabled, not left enabled.");
        }

        [Test]
        public void Build_WithNullBiome_DoesNotThrow_AndUsesDefaultColors()
        {
            _parent = new GameObject("ArenaParent");
            var rng = new RunRng(4);

            GameObject root = null;
            Assert.DoesNotThrow(() => root = ArenaGeometryBuilder.Build(_parent.transform, 12f, null, ref rng));
            Assert.IsNotNull(root);
        }

        [Test]
        public void Build_BiomeWithNoPropPrefabs_SpawnsNoExtraChildren()
        {
            _parent = new GameObject("ArenaParent");
            var biome = new ArenaRoomLibrary.Biome { propPrefabs = null };
            var rng = new RunRng(6);

            ArenaGeometryBuilder.Build(_parent.transform, 12f, biome, ref rng);

            // Floor, Ceiling, 4 walls, 2 accent lights (A5.7) = 8 children under ArenaGeometry; no props.
            Transform geometry = _parent.transform.Find("ArenaGeometry");
            Assert.AreEqual(8, geometry.childCount);
        }

        [Test]
        public void Build_UsesRendererTint_AvoidsPerRendererMaterialClones()
        {
            // A5.7: renamed from a prior name that claimed this is what keeps SRP batching intact —
            // backwards. The SRP Batcher does NOT batch renderers carrying a MaterialPropertyBlock;
            // RendererTint is still the right call here because it avoids each renderer cloning its
            // own `.material` instance, which is the actual cost this guards against.
            _parent = new GameObject("ArenaParent");
            var rng = new RunRng(8);

            ArenaGeometryBuilder.Build(_parent.transform, 12f, null, ref rng);

            var floorRenderer = _parent.transform.Find("ArenaGeometry/Floor").GetComponent<Renderer>();
            var wallRenderer = _parent.transform.Find("ArenaGeometry/Wall_North").GetComponent<Renderer>();

            // Both are cubes tinted via MaterialPropertyBlock (RendererTint), so they still share
            // Unity's default primitive material instead of each cloning their own.
            Assert.AreSame(floorRenderer.sharedMaterial, wallRenderer.sharedMaterial);
        }
    }
}
