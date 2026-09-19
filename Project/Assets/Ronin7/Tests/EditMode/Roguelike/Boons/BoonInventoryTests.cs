using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Boons
{
    /// <summary>
    /// Covers every BoonInventory aggregation path: multiplicative stacking for the *Multiplier
    /// effects, additive sums for MaxHealthAdd/HealOnKill, revive consumption, granted-ability
    /// dedupe, and graceful handling of null/unknown input.
    /// </summary>
    public class BoonInventoryTests
    {
        private readonly List<Object> _created = new List<Object>();
        private BoonInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _inventory = new BoonInventory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
            // ConsumeRevive touches the static RunState (A1.1's durable spent-revive counter); reset it
            // so a revive consumed by one test can't leak into another.
            RunState.Reset();
        }

        private BoonDefinition MakeBoon(string id, BoonEffectKind effect, float magnitude, int maxStacks = 3)
        {
            var boon = ScriptableObject.CreateInstance<BoonDefinition>();
            boon.id = id;
            boon.effect = effect;
            boon.magnitude = magnitude;
            boon.maxStacks = maxStacks;
            _created.Add(boon);
            return boon;
        }

        [Test]
        public void Add_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _inventory.Add(null));
            Assert.AreEqual(0, _inventory.Stacks("anything"));
        }

        [Test]
        public void Add_DefinitionWithNoId_IsIgnored()
        {
            var boon = MakeBoon(null, BoonEffectKind.MaxHealthAdd, 10f);
            _inventory.Add(boon);
            Assert.AreEqual(0f, _inventory.MaxHealthAdd);
        }

        [Test]
        public void Stacks_UnknownId_ReturnsZero()
        {
            Assert.AreEqual(0, _inventory.Stacks("boon_never_added"));
        }

        [Test]
        public void Stacks_NullOrEmptyId_ReturnsZero()
        {
            Assert.AreEqual(0, _inventory.Stacks(null));
            Assert.AreEqual(0, _inventory.Stacks(""));
        }

        [Test]
        public void Add_SameBoonTwice_IncrementsStacks()
        {
            var boon = MakeBoon("boon_a", BoonEffectKind.MaxHealthAdd, 10f);
            _inventory.Add(boon);
            _inventory.Add(boon);
            Assert.AreEqual(2, _inventory.Stacks("boon_a"));
        }

        [Test]
        public void BladeDamageMultiplier_NoBoons_IsOne()
        {
            Assert.AreEqual(1f, _inventory.BladeDamageMultiplier);
        }

        [Test]
        public void BladeDamageMultiplier_SingleStack_IsOnePlusMagnitude()
        {
            var boon = MakeBoon("boon_keen", BoonEffectKind.BladeDamageMultiplier, 0.15f);
            _inventory.Add(boon);

            Assert.That(_inventory.BladeDamageMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
        }

        [Test]
        public void BladeDamageMultiplier_ThreeStacks_IsOnePlusMagnitudeCubed()
        {
            var boon = MakeBoon("boon_keen", BoonEffectKind.BladeDamageMultiplier, 0.15f, maxStacks: 5);
            _inventory.Add(boon);
            _inventory.Add(boon);
            _inventory.Add(boon);

            float expected = Mathf.Pow(1.15f, 3);
            Assert.That(_inventory.BladeDamageMultiplier, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void BladeDamageMultiplier_TwoDifferentDamageBoons_MultiplyTogether()
        {
            var a = MakeBoon("boon_a", BoonEffectKind.BladeDamageMultiplier, 0.1f);
            var b = MakeBoon("boon_b", BoonEffectKind.BladeDamageMultiplier, 0.2f);
            _inventory.Add(a);
            _inventory.Add(b);

            float expected = 1.1f * 1.2f;
            Assert.That(_inventory.BladeDamageMultiplier, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void BladeDamageMultiplier_IgnoresOtherEffectKinds()
        {
            var health = MakeBoon("boon_health", BoonEffectKind.MaxHealthAdd, 20f);
            _inventory.Add(health);

            Assert.AreEqual(1f, _inventory.BladeDamageMultiplier);
        }

        [Test]
        public void ParryFlowBonus_MultipleStacks_SumsAdditively()
        {
            var boon = MakeBoon("boon_flow_initiate", BoonEffectKind.ParryFlowBonus, 0.05f, maxStacks: 5);
            _inventory.Add(boon);
            _inventory.Add(boon);

            Assert.That(_inventory.ParryFlowBonus, Is.EqualTo(0.10f).Within(0.0001f));
        }

        [Test]
        public void ComboBonus_MultipleStacks_SumsAdditively()
        {
            var boon = MakeBoon("boon_combo_initiate", BoonEffectKind.ComboBonus, 0.05f, maxStacks: 5);
            _inventory.Add(boon);
            _inventory.Add(boon);

            Assert.That(_inventory.ComboBonus, Is.EqualTo(0.10f).Within(0.0001f));
        }

        [Test]
        public void MaxHealthAdd_NoBoons_IsZero()
        {
            Assert.AreEqual(0f, _inventory.MaxHealthAdd);
        }

        [Test]
        public void MaxHealthAdd_MultipleStacksAndBoons_SumsAdditively()
        {
            var ironskin = MakeBoon("boon_ironskin", BoonEffectKind.MaxHealthAdd, 20f, maxStacks: 5);
            var other = MakeBoon("boon_other_health", BoonEffectKind.MaxHealthAdd, 5f);
            _inventory.Add(ironskin);
            _inventory.Add(ironskin);
            _inventory.Add(other);

            Assert.AreEqual(45f, _inventory.MaxHealthAdd); // 20*2 + 5
        }

        [Test]
        public void HealOnKill_MultipleStacks_SumsAdditively()
        {
            var boon = MakeBoon("boon_bloodletter", BoonEffectKind.HealOnKill, 3f, maxStacks: 5);
            _inventory.Add(boon);
            _inventory.Add(boon);

            Assert.AreEqual(6f, _inventory.HealOnKill);
        }

        [Test]
        public void HasRevive_NoReviveBoon_IsFalse()
        {
            Assert.IsFalse(_inventory.HasRevive);
        }

        [Test]
        public void HasRevive_AfterAddingReviveBoon_IsTrue()
        {
            var boon = MakeBoon("boon_second_wind", BoonEffectKind.ReviveOnce, 0f, maxStacks: 1);
            _inventory.Add(boon);

            Assert.IsTrue(_inventory.HasRevive);
        }

        [Test]
        public void ConsumeRevive_WithPendingRevive_ReturnsTrueOnceThenFalse()
        {
            var boon = MakeBoon("boon_second_wind", BoonEffectKind.ReviveOnce, 0f, maxStacks: 1);
            _inventory.Add(boon);

            Assert.IsTrue(_inventory.ConsumeRevive());
            Assert.IsFalse(_inventory.HasRevive);
            Assert.IsFalse(_inventory.ConsumeRevive());
        }

        [Test]
        public void ConsumeRevive_NoPendingRevive_ReturnsFalse()
        {
            Assert.IsFalse(_inventory.ConsumeRevive());
        }

        [Test]
        public void ConsumeRevive_TwoStacks_ConsumesEachIndependently()
        {
            var boon = MakeBoon("boon_second_wind", BoonEffectKind.ReviveOnce, 0f, maxStacks: 2);
            _inventory.Add(boon);
            _inventory.Add(boon);

            Assert.IsTrue(_inventory.ConsumeRevive());
            Assert.IsTrue(_inventory.HasRevive);
            Assert.IsTrue(_inventory.ConsumeRevive());
            Assert.IsFalse(_inventory.HasRevive);
        }

        [Test]
        public void GrantedAbilities_NoGrantBoons_IsEmpty()
        {
            Assert.AreEqual(0, _inventory.GrantedAbilities.Count);
        }

        [Test]
        public void GrantedAbilities_GrantAbilityBoon_AddsAbilityId()
        {
            var boon = MakeBoon("boon_grant_mirror", BoonEffectKind.GrantAbility, 0f, maxStacks: 1);
            boon.abilityId = "mirror";
            _inventory.Add(boon);

            CollectionAssert.Contains(_inventory.GrantedAbilities, "mirror");
        }

        [Test]
        public void GrantedAbilities_SameBoonAddedTwice_DoesNotDuplicate()
        {
            var boon = MakeBoon("boon_grant_mirror", BoonEffectKind.GrantAbility, 0f, maxStacks: 1);
            boon.abilityId = "mirror";
            _inventory.Add(boon);
            _inventory.Add(boon);

            Assert.AreEqual(1, _inventory.GrantedAbilities.Count);
        }

        [Test]
        public void Clear_ResetsAllAggregatesAndStacks()
        {
            var damage = MakeBoon("boon_keen", BoonEffectKind.BladeDamageMultiplier, 0.15f);
            var health = MakeBoon("boon_ironskin", BoonEffectKind.MaxHealthAdd, 20f);
            var revive = MakeBoon("boon_second_wind", BoonEffectKind.ReviveOnce, 0f, maxStacks: 1);
            var grant = MakeBoon("boon_grant_mirror", BoonEffectKind.GrantAbility, 0f, maxStacks: 1);
            grant.abilityId = "mirror";
            _inventory.Add(damage);
            _inventory.Add(health);
            _inventory.Add(revive);
            _inventory.Add(grant);

            _inventory.Clear();

            Assert.AreEqual(0, _inventory.Stacks("boon_keen"));
            Assert.AreEqual(1f, _inventory.BladeDamageMultiplier);
            Assert.AreEqual(0f, _inventory.MaxHealthAdd);
            Assert.IsFalse(_inventory.HasRevive);
            Assert.AreEqual(0, _inventory.GrantedAbilities.Count);
        }

        // ---- A1.2: Add enforces maxStacks itself ----

        [Test]
        public void Add_PastMaxStacks_IsClamped()
        {
            var boon = MakeBoon("boon_capped", BoonEffectKind.MaxHealthAdd, 10f, maxStacks: 2);
            _inventory.Add(boon);
            _inventory.Add(boon);
            _inventory.Add(boon); // third add must be a no-op

            Assert.AreEqual(2, _inventory.Stacks("boon_capped"));
            Assert.AreEqual(20f, _inventory.MaxHealthAdd); // not 30
        }

        [Test]
        public void Add_MaxStacksZeroOrNegative_TreatedAsOne()
        {
            var boon = MakeBoon("boon_misauthored", BoonEffectKind.MaxHealthAdd, 10f, maxStacks: 0);
            _inventory.Add(boon);
            _inventory.Add(boon); // second add must be a no-op, not "never offerable"

            Assert.AreEqual(1, _inventory.Stacks("boon_misauthored"));
        }

        // ---- GrantAbility edge cases ----

        [Test]
        public void GrantedAbilities_NullOrEmptyAbilityId_IsSkipped()
        {
            var boonNull = MakeBoon("boon_grant_null", BoonEffectKind.GrantAbility, 0f, maxStacks: 1);
            boonNull.abilityId = null;
            var boonEmpty = MakeBoon("boon_grant_empty", BoonEffectKind.GrantAbility, 0f, maxStacks: 1);
            boonEmpty.abilityId = "";

            _inventory.Add(boonNull);
            _inventory.Add(boonEmpty);

            Assert.AreEqual(0, _inventory.GrantedAbilities.Count);
        }

        // ---- A1.1: durable revive state ----

        [Test]
        public void Clear_AfterPartialReviveConsumption_ResetsHasRevive()
        {
            var boon = MakeBoon("boon_second_wind", BoonEffectKind.ReviveOnce, 0f, maxStacks: 2);
            _inventory.Add(boon);
            _inventory.Add(boon); // 2 granted
            _inventory.ConsumeRevive(); // 1 spent, 1 remaining -> HasRevive still true

            _inventory.Clear();

            Assert.IsFalse(_inventory.HasRevive);
            Assert.AreEqual(0, _inventory.Stacks("boon_second_wind"));
        }

        [Test]
        public void ConsumeRevive_ThenReAddingSecondWindAfterRebuild_DoesNotRestoreRevive()
        {
            // The arena scene reloads every node, which rebuilds BoonInventory from scratch and
            // re-Adds every boon the run currently holds (including a spent Second Wind). Without
            // RunState.RevivesSpent surviving the rebuild, this would resurrect the revive — infinite
            // revives. This test proves it doesn't.
            RunState.Reset();
            RunState.Begin(1);

            var boon = MakeBoon(BoonId.SecondWind, BoonEffectKind.ReviveOnce, 0f, maxStacks: 1);

            _inventory.Add(boon);
            _inventory.SetRevivesSpent(RunState.RevivesSpent);
            Assert.IsTrue(_inventory.ConsumeRevive());
            Assert.IsFalse(_inventory.HasRevive);

            // Simulate the next node's rebuild: a brand-new inventory, boon re-Added, synced from the
            // durable RunState counter.
            var rebuilt = new BoonInventory();
            rebuilt.Add(boon);
            rebuilt.SetRevivesSpent(RunState.RevivesSpent);

            Assert.IsFalse(rebuilt.HasRevive, "a spent Second Wind must not resurrect on rebuild");
        }
    }
}
