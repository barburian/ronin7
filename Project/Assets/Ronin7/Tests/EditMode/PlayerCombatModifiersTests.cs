using NUnit.Framework;
using Ronin7.Combat;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Guards the default-neutral contract <see cref="BladeDamager"/> relies on: a rig with no
    /// (or a freshly-added) <see cref="PlayerCombatModifiers"/> must deal unmodified damage, plus the
    /// slot-product/cap math backing the combined <see cref="PlayerCombatModifiers.DamageMultiplier"/>.</summary>
    public class PlayerCombatModifiersTests
    {
        private GameObject go;

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void AllSlots_DefaultToOne_DamageMultiplierIsOne()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            Assert.IsFalse(mods.WeakpointSightActive);
            Assert.AreEqual(1f, mods.ParryFlowMultiplier);
            Assert.AreEqual(1f, mods.ComboMultiplier);
            Assert.AreEqual(1f, mods.BoonMultiplier);
            Assert.AreEqual(0f, mods.BoonParryFlowBonus);
            Assert.AreEqual(0f, mods.BoonComboBonus);
            Assert.AreEqual(1f, mods.DamageMultiplier);
        }

        [Test]
        public void DamageMultiplier_IsProductOfSlots()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.ParryFlowMultiplier = 1.2f;
            mods.ComboMultiplier = 1.1f;
            Assert.AreEqual(1.2f * 1.1f, mods.DamageMultiplier, 1e-5f);
        }

        [Test]
        public void DamageMultiplier_CappedAt3x()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.ParryFlowMultiplier = 1.4f;
            mods.OverdriveMultiplier = 1.5f;
            mods.ComboMultiplier = 1.6f; // product = 4.48, well past the 3x cap
            Assert.AreEqual(3f, mods.DamageMultiplier);
        }

        [Test]
        public void DamageMultiplier_BoonMultiplierAppliesOnTopOfTransientCap()
        {
            // A1.5: even at the transient cap, a boon multiplier still applies fully (up to its own cap)
            // — this is the exact case the flat single-cap bug made dead weight.
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.ParryFlowMultiplier = 1.4f;
            mods.OverdriveMultiplier = 1.5f;
            mods.ComboMultiplier = 1.6f; // transient product 4.48, capped to 3x
            mods.BoonMultiplier = 1.5f;  // under its own 2x cap, applies fully
            Assert.AreEqual(3f * 1.5f, mods.DamageMultiplier, 1e-5f);
        }

        [Test]
        public void DamageMultiplier_BoonMultiplierCappedAt2x()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.BoonMultiplier = 5f; // well past the 2x boon cap
            Assert.AreEqual(2f, mods.DamageMultiplier);
        }

        [Test]
        public void DamageMultiplier_WorstCase_TransientAndBoonCapsMultiplyIndependently()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            mods.ParryFlowMultiplier = 1.4f;
            mods.OverdriveMultiplier = 1.5f;
            mods.ComboMultiplier = 1.6f; // capped to 3x
            mods.BoonMultiplier = 10f;   // capped to 2x
            Assert.AreEqual(6f, mods.DamageMultiplier); // 3 * 2, the documented worst case
        }

        [Test]
        public void BoonStats_DefaultZero_SetBoonRoundTrips_AndCoverEveryKind()
        {
            go = new GameObject("Rig");
            var mods = go.AddComponent<PlayerCombatModifiers>();
            Assert.AreEqual(System.Enum.GetValues(typeof(BoonEffectKind)).Length, PlayerCombatModifiers.BoonKindCount);
            for (int k = 0; k < PlayerCombatModifiers.BoonKindCount; k++)
                Assert.AreEqual(0f, mods.Boon((BoonEffectKind)k));

            mods.SetBoon(BoonEffectKind.HealOnKill, 3f);
            Assert.AreEqual(3f, mods.Boon(BoonEffectKind.HealOnKill));
            Assert.AreEqual(0f, mods.Boon((BoonEffectKind)999), "out-of-range kind is neutral, not a throw");
        }
    }
}
