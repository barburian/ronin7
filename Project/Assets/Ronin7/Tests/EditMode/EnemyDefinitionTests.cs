using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Pure-math coverage for <see cref="EnemyDefinition.ScaledByGalaxy"/> and its instance
    /// conveniences: the per-galaxy ramp that lets ground-melee stats scale with story progress,
    /// mirroring SpaceEncounterManager.ComposeWave's galaxy bonus. No scene/MonoBehaviour needed —
    /// the method is side-effect free by design.
    /// </summary>
    public class EnemyDefinitionTests
    {
        [Test]
        public void ScaledByGalaxy_ZeroGalaxies_ReturnsBaseUnchanged()
        {
            Assert.AreEqual(60f, EnemyDefinition.ScaledByGalaxy(60f, 10f, 0));
        }

        [Test]
        public void ScaledByGalaxy_NGalaxies_AddsPerGalaxyTimesN()
        {
            Assert.AreEqual(90f, EnemyDefinition.ScaledByGalaxy(60f, 10f, 3));
        }

        [Test]
        public void ScaledByGalaxy_NegativeGalaxies_ClampedToZero()
        {
            Assert.AreEqual(60f, EnemyDefinition.ScaledByGalaxy(60f, 10f, -5));
        }

        [Test]
        public void ScaledByGalaxy_ZeroPerGalaxy_AlwaysReturnsBase()
        {
            Assert.AreEqual(60f, EnemyDefinition.ScaledByGalaxy(60f, 0f, 3));
            Assert.AreEqual(60f, EnemyDefinition.ScaledByGalaxy(60f, 0f, 0));
        }

        [Test]
        public void ScaledMaxHealth_UsesHealthAddedPerGalaxy()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.maxHealth = 60f;
            def.healthAddedPerGalaxy = 5f;

            Assert.AreEqual(60f, def.ScaledMaxHealth(0));
            Assert.AreEqual(75f, def.ScaledMaxHealth(3));

            Object.DestroyImmediate(def);
        }

        [Test]
        public void ScaledDamage_UsesDamageAddedPerGalaxy()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.damage = 12f;
            def.damageAddedPerGalaxy = 2f;

            Assert.AreEqual(12f, def.ScaledDamage(0));
            Assert.AreEqual(18f, def.ScaledDamage(3));

            Object.DestroyImmediate(def);
        }

        [Test]
        public void Defaults_ProduceNoScaling()
        {
            // Existing/authored assets that never touch the new fields must be unaffected.
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();

            Assert.AreEqual(0f, def.healthAddedPerGalaxy);
            Assert.AreEqual(0f, def.damageAddedPerGalaxy);
            Assert.AreEqual(0f, def.postureMaxFraction);
            Assert.AreEqual(def.maxHealth, def.ScaledMaxHealth(4));
            Assert.AreEqual(def.damage, def.ScaledDamage(4));

            Object.DestroyImmediate(def);
        }
    }
}
