using NUnit.Framework;
using Ronin7.Audio.Vfx;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Phase 3 combat-VFX tests: the pool recycles instead of instantiating per call, the controller
    /// subscribes/unsubscribes cleanly with no leaked handlers, and the built prefabs honour the Quest
    /// caps (maxParticles + playOnAwake=false).
    /// </summary>
    public class CombatVfxTests
    {
        [SetUp] public void SetUp() => EventBus.Clear();
        [TearDown] public void TearDown() => EventBus.Clear();

        [Test]
        public void VfxPool_ReusesInstances_DoesNotInstantiatePerCall()
        {
            var parent = new GameObject("PoolParent").transform;
            var prefab = new GameObject("Fx").AddComponent<ParticleSystem>();
            try
            {
                const int capacity = 4;
                var pool = new VfxPool(parent, capacity);

                for (int i = 0; i < 12; i++) // 3× the capacity
                    pool.Play(prefab, Vector3.zero, 5);

                // One ring of `capacity` instances was allocated once and reused — never 12.
                Assert.AreEqual(capacity, pool.Instantiations,
                    "VfxPool should allocate exactly `capacity` instances per prefab and recycle them.");
            }
            finally
            {
                Object.DestroyImmediate(prefab.gameObject);
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void Controller_SubscribesOnSubscribe_AndUnsubscribesWithNoLeaks()
        {
            var go = new GameObject("CombatVfx");
            var controller = go.AddComponent<CombatVfxController>();
            try
            {
                controller.Subscribe();
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, null));
                Assert.AreEqual(1, controller.HandledCount, "Handler should fire while subscribed.");

                controller.Unsubscribe();
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, null));
                Assert.AreEqual(1, controller.HandledCount, "Handler must not fire after Unsubscribe (no leaked delegate).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BuiltPrefab_IsQuestCapped_AndNotPlayOnAwake()
        {
            const string path = "Assets/Ronin7/Resources/Vfx/SwordSpark.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Assert.Inconclusive("VFX prefabs not built yet — run Tools/Space Samurai/Art/Build Combat VFX Prefabs.");

            var ps = prefab.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, "Built VFX prefab must carry a ParticleSystem.");
            var main = ps.main;
            Assert.LessOrEqual(main.maxParticles, 30, "maxParticles must stay within the Quest budget (≤30).");
            Assert.IsFalse(main.playOnAwake, "VFX prefab must not play on awake (the pool drives it).");
            Assert.LessOrEqual(main.startLifetime.constantMax, 0.4f, "Lifetime must be short (≤0.4s).");
        }
    }
}
