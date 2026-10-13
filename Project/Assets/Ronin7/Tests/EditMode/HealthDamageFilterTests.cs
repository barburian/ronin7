using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-01: <see cref="Health.DamageFilter"/> / <see cref="Health.IncomingDamageMultiplier"/> pre-damage hook.</summary>
    public class HealthDamageFilterTests
    {
        private GameObject _go;
        private Health _health;
        private int _damaged, _died;
        private float _lastAmount;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _go = new GameObject("FilterTarget");
            _health = _go.AddComponent<Health>();
            _health.Configure(100f);
            _damaged = _died = 0;
            _health.Damaged += i => { _damaged++; _lastAmount = i.Amount; };
            _health.Died += () => _died++;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            EventBus.Clear();
        }

        private static DamageInfo Hit(float a) => new DamageInfo(a, Vector3.zero, Vector3.forward, null);

        [Test]
        public void Filter_Null_AppliesUnchanged()
        {
            _health.ApplyDamage(Hit(30f));
            Assert.AreEqual(70f, _health.Current);
            Assert.AreEqual(30f, _lastAmount);
        }

        [Test]
        public void Filter_Zero_NoDamagedNoDied()
        {
            _health.DamageFilter = _ => 0f;
            _health.ApplyDamage(Hit(500f));
            Assert.AreEqual(100f, _health.Current);
            Assert.AreEqual(0, _damaged);
            Assert.AreEqual(0, _died);
        }

        [Test]
        public void Filter_NaN_Rejected()
        {
            _health.DamageFilter = _ => float.NaN;
            _health.ApplyDamage(Hit(30f));
            Assert.AreEqual(100f, _health.Current);
            Assert.AreEqual(0, _damaged);
        }

        [Test]
        public void Filter_Scales_EventCarriesFilteredAmount()
        {
            _health.DamageFilter = i => i.Amount * 0.5f;
            _health.ApplyDamage(Hit(40f));
            Assert.AreEqual(80f, _health.Current);
            Assert.AreEqual(20f, _lastAmount);
        }

        [Test]
        public void Filter_Lethal_StillRespectsDeathInterceptor()
        {
            _health.DamageFilter = i => i.Amount * 2f;
            _health.DeathInterceptor = () => true;
            _health.ApplyDamage(Hit(60f));
            Assert.IsTrue(_health.IsAlive);
            Assert.AreEqual(0, _died);
        }

        [Test]
        public void IncomingMultiplier_ScalesDamage()
        {
            _health.IncomingDamageMultiplier = 0.6f;
            _health.ApplyDamage(Hit(50f));
            Assert.AreEqual(70f, _health.Current, 1e-4f);
        }
    }
}
