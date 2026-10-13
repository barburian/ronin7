using NUnit.Framework;
using Ronin7.Enemies;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class BladeEdgeTests
    {
        [TestCase(0f), TestCase(49f), TestCase(-49f), TestCase(50f)]
        public void TelegraphFor_InsideCone_IsBase(float angle)
            => Assert.AreEqual(0.8f, MeleeAttacker.TelegraphFor(0.8f, angle), 1e-5f);

        [TestCase(51f), TestCase(-51f), TestCase(180f), TestCase(-180f), TestCase(230f)]
        public void TelegraphFor_OutsideCone_AddsGrace(float angle)
            => Assert.AreEqual(1.05f, MeleeAttacker.TelegraphFor(0.8f, angle), 1e-5f);

        [Test]
        public void EveryEnemyPrefab_HasEdgeWithSharedMaterial()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Ronin7/Prefabs/Roguelike/Enemies" });
            Assert.IsNotEmpty(guids);
            Material shared = null;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var lr = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<LineRenderer>(true);
                Assert.IsNotNull(lr, path);
                Assert.AreEqual(2, lr.positionCount, path);
                Assert.IsNotNull(lr.sharedMaterial, path);
                shared = shared != null ? shared : lr.sharedMaterial;
                Assert.AreSame(shared, lr.sharedMaterial, path);
            }
        }
    }
}
