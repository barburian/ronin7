using System;
using NUnit.Framework;
using Ronin7.Editor.Art;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ronin7.Tests.EditMode.Art
{
    /// <summary>
    /// Behaviour contract for <see cref="ArtPrefabRegistry.TryInstantiateOrFallback"/>:
    /// returns the prefab instance when present, otherwise runs the greybox factory.
    /// During the greybox phase (no Prefabs/Art/*.prefab exist yet) the factory path
    /// is what every BuildX call site relies on, so it gets the most coverage.
    /// </summary>
    public class ArtPrefabRegistryTests
    {
        private const string MissingPath = "Assets/__art_registry_test_missing__.prefab";
        private const string TempPrefabPath = "Assets/__art_registry_test_temp__.prefab";

        // Each test cleans up any GameObject it spawns so the scene stays empty.
        private GameObject spawned;
        private GameObject tempPrefabSource;

        [SetUp]
        public void SetUp()
        {
            // Defensive: ensure no stale assets from a prior aborted run masquerade as "present"
            // for the missing-path tests, or block the temp-prefab write.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MissingPath) != null)
                AssetDatabase.DeleteAsset(MissingPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TempPrefabPath) != null)
                AssetDatabase.DeleteAsset(TempPrefabPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (spawned != null)
            {
                Object.DestroyImmediate(spawned);
                spawned = null;
            }
            if (tempPrefabSource != null)
            {
                Object.DestroyImmediate(tempPrefabSource);
                tempPrefabSource = null;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TempPrefabPath) != null)
                AssetDatabase.DeleteAsset(TempPrefabPath);
        }

        [Test]
        public void TryInstantiateOrFallback_PrefabMissing_RunsGreyboxFactory()
        {
            spawned = ArtPrefabRegistry.TryInstantiateOrFallback(
                MissingPath,
                () => GameObject.CreatePrimitive(PrimitiveType.Cube),
                null);

            Assert.IsNotNull(spawned, "Greybox factory result should be returned when prefab is missing.");
            Assert.IsNotNull(spawned.GetComponent<MeshFilter>(),
                "Returned object should be the cube primitive from the factory.");
        }

        [Test]
        public void TryInstantiateOrFallback_PrefabPresent_InstantiatesPrefabAndSkipsFactory()
        {
            // Create a throwaway prefab on disk so the AssetDatabase lookup hits.
            tempPrefabSource = new GameObject("ArtRegistryTestSource");
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(tempPrefabSource, TempPrefabPath);
            Assert.IsNotNull(prefabAsset, "Sanity: prefab must save before the registry can find it.");

            int factoryCalls = 0;
            spawned = ArtPrefabRegistry.TryInstantiateOrFallback(
                TempPrefabPath,
                () => { factoryCalls++; return GameObject.CreatePrimitive(PrimitiveType.Sphere); },
                null);

            Assert.IsNotNull(spawned);
            Assert.AreEqual(0, factoryCalls, "Greybox factory must not run when the prefab is present.");
            // The instance is a prefab connection of the saved asset, not the loose factory sphere.
            Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(spawned),
                "Returned object should be a prefab instance, not the greybox.");
            Assert.IsNull(spawned.GetComponent<MeshFilter>(),
                "Sanity: the saved prefab was an empty GameObject, so no MeshFilter (factory's sphere had one).");
        }

        [Test]
        public void TryInstantiateOrFallback_NullFactory_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ArtPrefabRegistry.TryInstantiateOrFallback(MissingPath, null, null));
        }

        [Test]
        public void TryInstantiateOrFallback_GreyboxPath_ReparentsOrphanUnderProvidedParent()
        {
            var parentGo = new GameObject("ArtRegistryTestParent");
            try
            {
                spawned = ArtPrefabRegistry.TryInstantiateOrFallback(
                    MissingPath,
                    // Factory returns an orphan (no parent). Registry should reparent it.
                    () => GameObject.CreatePrimitive(PrimitiveType.Cube),
                    parentGo.transform);

                Assert.IsNotNull(spawned);
                Assert.AreSame(parentGo.transform, spawned.transform.parent,
                    "Registry must reparent the greybox under the supplied parent.");
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }
    }
}
