using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Guards the default-neutral contract <see cref="BladeDamager"/> relies on: a rig with no
    /// (or a freshly-added) <see cref="PlayerCombatModifiers"/> must deal unmodified damage.</summary>
    public class PlayerCombatModifiersTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void DamageMultiplier_DefaultsToOne()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            Assert.AreEqual(1f, mods.DamageMultiplier);
        }

        [Test]
        public void DamageMultiplier_IsSettable()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.DamageMultiplier = 2f;
            Assert.AreEqual(2f, mods.DamageMultiplier);
        }
    }
}
