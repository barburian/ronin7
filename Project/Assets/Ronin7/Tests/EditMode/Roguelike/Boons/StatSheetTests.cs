using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Boons
{
    /// <summary>P4-02 stat sheet: one test per consumer, through its pure helper (caps included).</summary>
    public class StatSheetTests
    {
        // ---- EdgeSensitivity -> WeaponDefinition.DamageForSpeed ----

        [Test]
        public void DamageForSpeed_ThresholdScale_ReachesMaxEarlier_SameMax()
        {
            var def = ScriptableObject.CreateInstance<WeaponDefinition>(); // 1.5..6 m/s -> 8..40
            try
            {
                float scale = WeaponDefinition.EdgeThresholdScale(0.3f); // x0.70
                Assert.AreEqual(0f, def.DamageForSpeed(1.2f), "below the raw minimum: no damage");
                Assert.Greater(def.DamageForSpeed(1.2f, scale), 0f, "1.2 m/s clears the 1.05 m/s scaled minimum");
                Assert.AreEqual(def.maxDamage, def.DamageForSpeed(4.2f, scale), 1e-4f, "max reached at 6 x 0.7");
                Assert.Less(def.DamageForSpeed(4.2f), def.maxDamage);
                Assert.AreEqual(def.maxDamage, def.DamageForSpeed(20f, scale), 1e-4f, "same max, never higher");
                Assert.AreEqual(def.DamageForSpeed(3f), def.DamageForSpeed(3f, 1f), 1e-6f, "scale 1 = old overload");
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void EdgeThresholdScale_Neutral_And_Capped70()
        {
            Assert.AreEqual(1f, WeaponDefinition.EdgeThresholdScale(0f), 1e-6f);
            Assert.AreEqual(0.9f, WeaponDefinition.EdgeThresholdScale(0.1f), 1e-6f);
            Assert.AreEqual(0.7f, WeaponDefinition.EdgeThresholdScale(5f), 1e-6f);
        }

        // ---- Precision -> BladeDamager.PrecisionMultiplier ----

        [Test]
        public void PrecisionMultiplier_Bonus_RaisesBase_CappedAtTwo()
        {
            Assert.AreEqual(1.25f, BladeDamager.PrecisionMultiplier(true, false, 0f), 1e-6f);
            Assert.AreEqual(1.5f, BladeDamager.PrecisionMultiplier(true, false, 0.25f), 1e-6f);
            Assert.AreEqual(2f, BladeDamager.PrecisionMultiplier(true, false, 3f), 1e-6f);
            Assert.AreEqual(2f, BladeDamager.PrecisionMultiplier(true, true, 3f), 1e-6f, "Sight stays 2x");
            Assert.AreEqual(1f, BladeDamager.PrecisionMultiplier(false, false, 1f), 1e-6f, "body hits untouched");
        }

        // ---- DamageReduction -> Health.ApplyDamage ----

        [Test]
        public void IncomingDamage_Capped40()
        {
            Assert.AreEqual(1f, Health.DamageReductionScale(0f), 1e-6f);
            Assert.AreEqual(0.8f, Health.DamageReductionScale(0.2f), 1e-6f);
            Assert.AreEqual(0.6f, Health.DamageReductionScale(0.9f), 1e-6f);

            var go = new GameObject("rig");
            try
            {
                var health = go.AddComponent<Health>();
                var mods = go.AddComponent<PlayerCombatModifiers>();
                health.Configure(100f);
                mods.SetBoon(BoonEffectKind.DamageReduction, 0.9f);
                health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));
                Assert.AreEqual(70f, health.Current, 1e-4f, "50 damage at the 40% cap -> 30 taken");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- PostureDamage -> PostureMeter.OnDamaged ----

        [Test]
        public void PostureDamageScale_Neutral_And_Capped2()
        {
            Assert.AreEqual(1f, PostureMeter.PostureDamageScale(0f), 1e-6f);
            Assert.AreEqual(1.25f, PostureMeter.PostureDamageScale(0.25f), 1e-6f);
            Assert.AreEqual(2f, PostureMeter.PostureDamageScale(4f), 1e-6f);
        }

        // ---- ParryWindow / StaggerDuration -> MeleeAttacker.Deflect ----

        [Test]
        public void ParryQuality_WindowBonus()
        {
            Assert.AreEqual(0.12f, ParryTiming.PerfectWindow(0.12f, 0f), 1e-6f);
            Assert.AreEqual(0.132f, ParryTiming.PerfectWindow(0.12f, 0.1f), 1e-6f);
            Assert.AreEqual(0.16f, ParryTiming.PerfectWindow(0.12f, 5f), 1e-6f, "capped at +0.04 s");
            Assert.AreEqual(0f, ParryTiming.ParryQuality(0.13f, ParryTiming.PerfectWindow(0.12f, 0f)));
            Assert.Greater(ParryTiming.ParryQuality(0.13f, ParryTiming.PerfectWindow(0.12f, 1f)), 0f,
                "a late-ish deflect is still perfect with the widened window");
        }

        [Test]
        public void StaggerScale_Neutral_And_Capped150()
        {
            Assert.AreEqual(1f, ParryTiming.StaggerScale(0f), 1e-6f);
            Assert.AreEqual(1.25f, ParryTiming.StaggerScale(0.25f), 1e-6f);
            Assert.AreEqual(1.5f, ParryTiming.StaggerScale(3f), 1e-6f);
        }

        // ---- AbilityHaste -> PhaseStep / Mirror / Overdrive ----

        [Test]
        public void AbilityHaste_CooldownAndChargeScale_Capped40()
        {
            Assert.AreEqual(1f, PlayerCombatModifiers.CooldownScale(0f), 1e-6f);
            Assert.AreEqual(0.6f, PlayerCombatModifiers.CooldownScale(1f), 1e-6f);
            Assert.AreEqual(1.4f, PlayerCombatModifiers.ChargeScale(1f), 1e-6f);
        }

        [Test]
        public void MirrorSummon_Haste_ShortensCooldownOnly()
        {
            var logic = new MirrorSummonLogic(15f, 30f);
            Assert.IsTrue(logic.TrySummon(PlayerCombatModifiers.CooldownScale(0.4f))); // 18 s cooldown
            logic.Tick(15f);
            Assert.IsFalse(logic.IsActive, "active window unchanged by haste");
            logic.Tick(2.9f);
            Assert.IsFalse(logic.CanSummon);
            logic.Tick(0.2f);
            Assert.IsTrue(logic.CanSummon, "ready after 18 s, not 30 s");
        }

        [Test]
        public void Overdrive_Haste_ScalesChargePerHit()
        {
            var logic = new OverdriveLogic(0.1f, 0.25f, 1f);
            logic.AddCharge(PlayerCombatModifiers.ChargeScale(0.4f));
            Assert.AreEqual(0.14f, logic.Charge, 1e-5f);
        }

        // ---- PhaseCharges -> PhaseStepController ----

        [Test]
        public void PhaseCharges_MaxCharges_BaseOne_Capped3()
        {
            Assert.AreEqual(1, PhaseStepController.MaxCharges(0f));
            Assert.AreEqual(2, PhaseStepController.MaxCharges(1f));
            Assert.AreEqual(3, PhaseStepController.MaxCharges(9f));
        }

        [Test]
        public void PhaseCharges_RechargeSequence()
        {
            int max = 3, charges = 3;
            float recharge = 0f, cd = 2f;

            Assert.IsTrue(PhaseStepController.TryConsumeCharge(ref charges, ref recharge, cd));
            Assert.IsTrue(PhaseStepController.TryConsumeCharge(ref charges, ref recharge, cd));
            Assert.AreEqual(1, charges);
            Assert.AreEqual(2f, recharge, 1e-6f, "second spend does not restart a running timer");

            PhaseStepController.TickCharges(ref charges, ref recharge, 1.9f, max, cd);
            Assert.AreEqual(1, charges);
            PhaseStepController.TickCharges(ref charges, ref recharge, 0.2f, max, cd);
            Assert.AreEqual(2, charges, "one charge per cooldown");
            Assert.AreEqual(2f, recharge, 1e-6f, "next charge starts recharging");

            Assert.IsTrue(PhaseStepController.TryConsumeCharge(ref charges, ref recharge, cd));
            Assert.IsTrue(PhaseStepController.TryConsumeCharge(ref charges, ref recharge, cd));
            Assert.IsFalse(PhaseStepController.TryConsumeCharge(ref charges, ref recharge, cd), "empty");

            PhaseStepController.RefundCharge(ref charges, ref recharge, max);
            Assert.AreEqual(1, charges, "a refused blink refunds its charge");

            PhaseStepController.TickCharges(ref charges, ref recharge, 10f, max, cd);
            PhaseStepController.TickCharges(ref charges, ref recharge, 10f, max, cd);
            Assert.AreEqual(3, charges);
            Assert.AreEqual(0f, recharge, "full: timer idle");
        }

        // ---- Luck -> BoonOfferPicker ----

        [Test]
        public void RarityWeight_Luck_AddsRareAndEpic_Capped3()
        {
            int common = BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Combat);
            int rare = BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Combat);
            int epic = BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Combat);

            Assert.AreEqual(common, BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Combat, 2f));
            Assert.AreEqual(rare + 8, BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Combat, 2f));
            Assert.AreEqual(epic + 4, BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Combat, 2f));
            Assert.AreEqual(rare + 12, BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Combat, 10f));
            Assert.AreEqual(rare, BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Combat, 0f));
            Assert.AreEqual(0, BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Treasure, 3f),
                "Treasure still excludes Common");
        }

        // ---- EchoGain / HealOnKill -> RunDirector ----

        [Test]
        public void ApplyEchoGain_Scales_Capped150()
        {
            Assert.AreEqual(20, RunDirector.ApplyEchoGain(20, 0f));
            Assert.AreEqual(25, RunDirector.ApplyEchoGain(20, 0.25f));
            Assert.AreEqual(30, RunDirector.ApplyEchoGain(20, 2f));
        }

        [Test]
        public void ShouldHealOnKill_FalseForNonEnemyVictim()
        {
            var ally = new GameObject("mirror ally");
            var player = new GameObject("player");
            try
            {
                Assert.IsFalse(RunDirector.ShouldHealOnKill(ally, player, 3f, victimIsEnemy: false));
                Assert.IsTrue(RunDirector.ShouldHealOnKill(ally, player, 3f, victimIsEnemy: true));
            }
            finally
            {
                Object.DestroyImmediate(ally);
                Object.DestroyImmediate(player);
            }
        }
    }
}
