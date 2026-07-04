using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class PostureMeterTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        // ---- Pure statics. ----

        [Test]
        public void Accumulate_ClampsAtMax()
        {
            Assert.AreEqual(100f, PostureMeter.Accumulate(90f, 50f, 1f, 100f));
        }

        [Test]
        public void Accumulate_NeverGoesNegative()
        {
            Assert.AreEqual(0f, PostureMeter.Accumulate(0f, -50f, 1f, 100f));
        }

        [Test]
        public void Decay_ClampsAtZero()
        {
            Assert.AreEqual(0f, PostureMeter.Decay(5f, 1f, 40f));
        }

        [Test]
        public void Decay_PartialDrain()
        {
            Assert.AreEqual(60f, PostureMeter.Decay(100f, 1f, 40f), 1e-5f);
        }

        [Test]
        public void IsBroken_BelowMax_IsFalse()
        {
            Assert.IsFalse(PostureMeter.IsBroken(99.9f, 100f));
        }

        [Test]
        public void IsBroken_AtOrAboveMax_IsTrue()
        {
            Assert.IsTrue(PostureMeter.IsBroken(100f, 100f));
            Assert.IsTrue(PostureMeter.IsBroken(150f, 100f));
        }

        // ---- Component-level integration. EditMode doesn't auto-run MonoBehaviour lifecycle methods
        // on AddComponent, so drive Awake/OnEnable explicitly via reflection, mirroring
        // UnbrokenWardTests' StartWard/Life helpers. ----

        private static void Life(MonoBehaviour c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

        private static PostureMeter StartMeter(GameObject go)
        {
            var meter = go.AddComponent<PostureMeter>();
            Life(meter, "Awake");
            Life(meter, "OnEnable");
            return meter;
        }

        [Test]
        public void OnDamaged_CrossingThreshold_ResetsPosture_AppliesBonusDamageExactlyOnce()
        {
            _go = new GameObject("PostureMeterTestTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            int brokenCount = 0;
            EventBus.Subscribe<PostureBroken>(_ => brokenCount++);

            // 120 * gainPerDamage(0.9) = 108 >= postureMax(100) -> breaks on this single hit.
            var hit = new DamageInfo(120f, Vector3.zero, Vector3.forward, null);
            health.ApplyDamage(hit);

            Assert.AreEqual(0f, meter.Current);
            Assert.AreEqual(1000f - 120f - 12f, health.Current); // original hit + exactly one 12-dmg break bonus
            Assert.AreEqual(1, brokenCount);
        }

        [Test]
        public void OnDamaged_BelowThreshold_AccumulatesWithoutBreaking()
        {
            _go = new GameObject("PostureMeterTestTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            int brokenCount = 0;
            EventBus.Subscribe<PostureBroken>(_ => brokenCount++);

            // 50 * 0.9 = 45 < postureMax(100) -> no break.
            health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(45f, meter.Current, 1e-5f);
            Assert.AreEqual(950f, health.Current);
            Assert.AreEqual(0, brokenCount);
        }
    }
}
