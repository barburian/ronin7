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
        public void OnDamaged_LethalBreakBonus_DoesNotStaggerTheDead()
        {
            // Regression: Health fires Died synchronously inside ApplyDamage, so a break bonus that
            // kills used to be followed by ForceStagger() overwriting State.Dead — resurrecting the
            // enemy into its post-stagger state and leaking the CombatActivity aggro count.
            _go = new GameObject("PostureMeterLethalTestTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(125f);
            var dummy = _go.AddComponent<TrainingDummy>(); // concrete MeleeAttacker; lifecycle NOT driven
            var meter = StartMeter(_go);

            // 120 dmg: leaves 5 HP (alive), posture 120*0.9=108 >= 100 breaks, bonus 12 kills.
            health.ApplyDamage(new DamageInfo(120f, Vector3.zero, Vector3.forward, null));

            Assert.IsFalse(health.IsAlive, "Break bonus should have been lethal in this setup.");
            var stateField = typeof(MeleeAttacker).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(stateField, "MeleeAttacker.state field not found — update this test.");
            Assert.AreNotEqual("Stagger", stateField.GetValue(dummy).ToString(),
                "A dead enemy must not be forced into Stagger by the posture break.");
        }

        // ---- Configure (data-driven postureMax, e.g. from EnemyDefinition.postureMaxFraction). ----

        [Test]
        public void Configure_RaisesMax_SoAccumulateNoLongerBreaksAtOldThreshold()
        {
            _go = new GameObject("PostureMeterConfigureTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            meter.Configure(600f); // e.g. a 600-HP elite instead of the default 100

            int brokenCount = 0;
            EventBus.Subscribe<PostureBroken>(_ => brokenCount++);

            // 120 * gainPerDamage(0.9) = 108 — broke the old (100) default, does not break 600.
            health.ApplyDamage(new DamageInfo(120f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(108f, meter.Current, 1e-5f);
            Assert.AreEqual(0, brokenCount);
        }

        [Test]
        public void Configure_LowerMax_BreaksSoonerThanDefault()
        {
            _go = new GameObject("PostureMeterConfigureLowTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            meter.Configure(50f); // e.g. a squishy grunt tier

            int brokenCount = 0;
            EventBus.Subscribe<PostureBroken>(_ => brokenCount++);

            // 60 * 0.9 = 54 >= 50 -> breaks, where the old default (100) would not have.
            health.ApplyDamage(new DamageInfo(60f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(0f, meter.Current);
            Assert.AreEqual(1, brokenCount);
        }

        [Test]
        public void Configure_ResetsCurrentPostureToZero()
        {
            _go = new GameObject("PostureMeterConfigureResetTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));
            Assert.Greater(meter.Current, 0f);

            meter.Configure(200f);

            Assert.AreEqual(0f, meter.Current);
        }

        [Test]
        public void WithoutConfigure_DefaultSerializedPostureMaxUnchanged()
        {
            // Regression guard: a PostureMeter that never has Configure called on it (the pre-Feature-B
            // path) must behave exactly as before — this duplicates the pre-existing threshold test to
            // pin that behavior now that Configure exists.
            _go = new GameObject("PostureMeterNoConfigureTarget");
            var health = _go.AddComponent<Health>();
            health.Configure(1000f);
            var meter = StartMeter(_go);

            int brokenCount = 0;
            EventBus.Subscribe<PostureBroken>(_ => brokenCount++);

            health.ApplyDamage(new DamageInfo(120f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(0f, meter.Current);
            Assert.AreEqual(1000f - 120f - 12f, health.Current);
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
