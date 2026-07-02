using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class WeaponDefinitionTests
    {
        private WeaponDefinition _weapon;

        [SetUp]
        public void SetUp()
        {
            _weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            _weapon.minSwingSpeed = 1.5f;
            _weapon.referenceSwingSpeed = 6f;
            _weapon.minDamage = 8f;
            _weapon.maxDamage = 40f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_weapon != null) Object.DestroyImmediate(_weapon);
        }

        [Test]
        public void DamageForSpeed_BelowMin_IsZero()
        {
            Assert.AreEqual(0f, _weapon.DamageForSpeed(0f));
            Assert.AreEqual(0f, _weapon.DamageForSpeed(1.0f));
            Assert.AreEqual(0f, _weapon.DamageForSpeed(1.4999f));
        }

        [Test]
        public void DamageForSpeed_AtMinSwingSpeed_IsMinDamage()
        {
            float result = _weapon.DamageForSpeed(_weapon.minSwingSpeed);
            Assert.That(result, Is.EqualTo(_weapon.minDamage).Within(0.0001f));
        }

        [Test]
        public void DamageForSpeed_AtReferenceSwingSpeed_IsMaxDamage()
        {
            float result = _weapon.DamageForSpeed(_weapon.referenceSwingSpeed);
            Assert.That(result, Is.EqualTo(_weapon.maxDamage).Within(0.0001f));
        }

        [Test]
        public void DamageForSpeed_Midway_IsHalfwayBetweenMinAndMax()
        {
            float mid = (_weapon.minSwingSpeed + _weapon.referenceSwingSpeed) * 0.5f;
            float expected = (_weapon.minDamage + _weapon.maxDamage) * 0.5f;

            float result = _weapon.DamageForSpeed(mid);

            Assert.That(result, Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void DamageForSpeed_AboveReference_ClampsToMaxDamage()
        {
            // Mathf.InverseLerp clamps t to [0,1], so speeds above reference saturate at maxDamage.
            Assert.That(_weapon.DamageForSpeed(_weapon.referenceSwingSpeed + 1f),
                Is.EqualTo(_weapon.maxDamage).Within(0.0001f));
            Assert.That(_weapon.DamageForSpeed(_weapon.referenceSwingSpeed * 10f),
                Is.EqualTo(_weapon.maxDamage).Within(0.0001f));
        }
    }
}
