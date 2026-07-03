using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class HealthTests
    {
        private GameObject _go;
        private Health _health;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _go = new GameObject("HealthTestTarget");
            _health = _go.AddComponent<Health>();
            // EditMode skips Awake; seed via Configure per the contract.
            _health.Configure(100f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        private static DamageInfo MakeDamage(float amount)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Melee);
        }

        [Test]
        public void Configure_SeedsCurrentToMax_AndAlive()
        {
            Assert.AreEqual(100f, _health.Current);
            Assert.AreEqual(100f, _health.Max);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_ReducesCurrent_AndFiresDamagedEvent()
        {
            DamageInfo? captured = null;
            _health.Damaged += info => captured = info;

            var dmg = MakeDamage(30f);
            _health.ApplyDamage(dmg);

            Assert.AreEqual(70f, _health.Current);
            Assert.IsTrue(captured.HasValue);
            Assert.AreEqual(30f, captured.Value.Amount);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero_DoesNotGoNegative()
        {
            _health.ApplyDamage(MakeDamage(9999f));

            Assert.AreEqual(0f, _health.Current);
            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void Death_FiresDiedEventExactlyOnce_AndFurtherDamageIsNoOp()
        {
            int diedCount = 0;
            int damagedCount = 0;
            _health.Died += () => diedCount++;
            _health.Damaged += _ => damagedCount++;

            _health.ApplyDamage(MakeDamage(100f));
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(1, damagedCount);
            Assert.IsFalse(_health.IsAlive);

            // Further damage: ApplyDamage early-outs when not alive, so no events, no change.
            _health.ApplyDamage(MakeDamage(10f));
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(1, damagedCount);
            Assert.AreEqual(0f, _health.Current);
        }

        [Test]
        public void ApplyDamage_PublishesEntityDamagedOnEventBus()
        {
            EntityDamaged? captured = null;
            EventBus.Subscribe<EntityDamaged>(e => captured = e);

            _health.ApplyDamage(MakeDamage(25f));

            Assert.IsTrue(captured.HasValue);
            Assert.AreSame(_go, captured.Value.Entity);
            Assert.AreEqual(75f, captured.Value.Current);
            Assert.AreEqual(100f, captured.Value.Max);
            Assert.AreEqual(25f, captured.Value.Info.Amount);
        }

        [Test]
        public void ApplyDamage_NegativeAmount_IsIgnored_DoesNotHeal()
        {
            _health.ApplyDamage(MakeDamage(40f)); // drop below max so a heal would be observable: Current = 60
            bool damagedFired = false;
            _health.Damaged += _ => damagedFired = true;

            _health.ApplyDamage(MakeDamage(-50f));

            Assert.AreEqual(60f, _health.Current); // no partial heal back toward max, no heal past max
            Assert.IsFalse(damagedFired);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_NaNAmount_IsIgnored()
        {
            bool damagedFired = false;
            _health.Damaged += _ => damagedFired = true;

            _health.ApplyDamage(MakeDamage(float.NaN));

            Assert.AreEqual(100f, _health.Current); // NaN must not corrupt the pool
            Assert.IsFalse(damagedFired);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_ZeroAmount_IsIgnored_NoEvents()
        {
            bool damagedFired = false;
            _health.Damaged += _ => damagedFired = true;
            EntityDamaged? captured = null;
            EventBus.Subscribe<EntityDamaged>(e => captured = e);

            _health.ApplyDamage(MakeDamage(0f));

            Assert.AreEqual(100f, _health.Current);
            Assert.IsFalse(damagedFired);
            Assert.IsFalse(captured.HasValue);
        }

        [Test]
        public void Death_PublishesEntityDiedOnEventBus()
        {
            EntityDied? captured = null;
            EventBus.Subscribe<EntityDied>(e => captured = e);

            _health.ApplyDamage(MakeDamage(100f));

            Assert.IsTrue(captured.HasValue);
            Assert.AreSame(_go, captured.Value.Entity);
        }

        // ---- DeathInterceptor (Ch11 Unbroken ward hook). ----

        [Test]
        public void ApplyDamage_InterceptorNull_DiesAsBefore()
        {
            int diedCount = 0;
            _health.Died += () => diedCount++;

            _health.ApplyDamage(MakeDamage(100f));

            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(0f, _health.Current);
            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_InterceptorReturnsFalse_Dies()
        {
            int diedCount = 0;
            _health.Died += () => diedCount++;
            _health.DeathInterceptor = () => false;

            _health.ApplyDamage(MakeDamage(100f));

            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(0f, _health.Current);
            Assert.IsFalse(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_InterceptorReturnsTrue_SurvivesAtOneHp_DiedNotInvoked()
        {
            int diedCount = 0;
            EntityDied? diedEvent = null;
            _health.Died += () => diedCount++;
            EventBus.Subscribe<EntityDied>(e => diedEvent = e);
            _health.DeathInterceptor = () => true;

            _health.ApplyDamage(MakeDamage(100f));

            Assert.AreEqual(0, diedCount);
            Assert.IsFalse(diedEvent.HasValue);
            Assert.AreEqual(1f, _health.Current);
            Assert.IsTrue(_health.IsAlive);
        }

        [Test]
        public void ApplyDamage_InterceptorSpendsOnce_SecondLethalBlowKills()
        {
            int diedCount = 0;
            _health.Died += () => diedCount++;
            bool used = false;
            // Mirrors UnbrokenWard's once-per-life contract: the interceptor itself flips to "spent"
            // after firing once.
            _health.DeathInterceptor = () =>
            {
                if (used) return false;
                used = true;
                return true;
            };

            _health.ApplyDamage(MakeDamage(100f)); // first lethal blow: survives at 1 HP
            Assert.AreEqual(0, diedCount);
            Assert.AreEqual(1f, _health.Current);

            _health.ApplyDamage(MakeDamage(100f)); // second lethal blow: ward already spent, dies
            Assert.AreEqual(1, diedCount);
            Assert.AreEqual(0f, _health.Current);
            Assert.IsFalse(_health.IsAlive);
        }
    }
}
