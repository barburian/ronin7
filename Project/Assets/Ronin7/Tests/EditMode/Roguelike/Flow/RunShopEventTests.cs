using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P2-04: shop pricing / affordability, the four authored events, and the path-panel rule.</summary>
    public class RunShopEventTests
    {
        private const string EventsFolder = "Assets/Ronin7/Data/Roguelike/Events/";

        [TearDown]
        public void TearDown() => RunState.Reset();

        private static RunEventDefinition Load(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<RunEventDefinition>(EventsFolder + name + ".asset");
            Assert.IsNotNull(def, name + " event asset missing — run Tools/Space Samurai/Roguelike/Build Run Events");
            return def;
        }

        // ---- Shop ----

        [Test]
        public void ShopPrice_ByRarity_30_60_120()
        {
            Assert.AreEqual(30, RunDirector.ShopPrice(BoonRarity.Common));
            Assert.AreEqual(60, RunDirector.ShopPrice(BoonRarity.Rare));
            Assert.AreEqual(120, RunDirector.ShopPrice(BoonRarity.Epic));
            Assert.AreEqual(40, RunDirector.MendPrice);
        }

        [Test]
        public void TrySpendShards_Insufficient_NoChange_Sufficient_Deducts()
        {
            RunState.Begin(1);
            RunState.AddShards(59);
            Assert.IsFalse(RunState.TrySpendShards(RunDirector.ShopPrice(BoonRarity.Rare)));
            Assert.AreEqual(59, RunState.Shards);
            Assert.IsTrue(RunState.TrySpendShards(RunDirector.ShopPrice(BoonRarity.Common)));
            Assert.AreEqual(29, RunState.Shards, "spent Shards are gone");
            Assert.IsFalse(RunState.TrySpendShards(-5));
        }

        [Test]
        public void MendResult_QuarterOfMax_Clamped()
        {
            Assert.AreEqual(65f, RunDirector.MendResult(40f, 100f), 1e-4f);
            Assert.AreEqual(100f, RunDirector.MendResult(90f, 100f), 1e-4f);
        }

        // ---- Events ----

        [Test]
        public void BloodPact_Minus15MaxHp_GainsRareBoon()
        {
            var o = Resolve(Load("BloodPact").optionA, 1);
            Assert.AreEqual(-15f, o.MaxHealthDelta);
            CollectionAssert.AreEqual(new[] { BoonRarity.Rare }, o.Boons);
            RunEventResolver.ApplyHealth(100f, 110f, o, out float max, out float cur);
            Assert.AreEqual(95f, max);
            Assert.AreEqual(95f, cur, "current HP never sits above the lowered max");
        }

        [Test]
        public void Gamble_DeterministicPerSeed_EpicOrTwentyFiveDamage()
        {
            var opt = Load("Gamble").optionA;
            bool sawWin = false, sawLoss = false;
            for (uint seed = 1; seed <= 64; seed++)
            {
                var a = Resolve(opt, seed);
                var b = Resolve(opt, seed);
                Assert.AreEqual(a.Success, b.Success, "same RunRng substream, same outcome");
                if (a.Success) { sawWin = true; CollectionAssert.AreEqual(new[] { BoonRarity.Epic }, a.Boons); Assert.AreEqual(0f, a.Damage); }
                else { sawLoss = true; Assert.AreEqual(25f, a.Damage); Assert.IsEmpty(a.Boons); }
            }
            Assert.IsTrue(sawWin && sawLoss, "a 50% gamble lands both ways across 64 seeds");
            RunEventResolver.ApplyHealth(10f, 100f, new RunEventOutcome { Damage = 25f }, out _, out float cur);
            Assert.AreEqual(1f, cur, "an event never kills");
        }

        [Test]
        public void Meditate_Heals30Percent()
        {
            var o = Resolve(Load("Meditate").optionA, 1);
            Assert.AreEqual(0.3f, o.HealFraction, 1e-5f);
            RunEventResolver.ApplyHealth(40f, 100f, o, out _, out float cur);
            Assert.AreEqual(70f, cur, 1e-4f);
        }

        [Test]
        public void ForgottenShrine_Spends40_PlusReroll_Heals15()
        {
            var opt = Load("ForgottenShrine").optionA;
            Assert.AreEqual(40, RunEventResolver.ShardCost(opt));
            Assert.IsFalse(RunEventResolver.CanAfford(opt, 39));
            Assert.IsTrue(RunEventResolver.CanAfford(opt, 40));
            var o = Resolve(opt, 1);
            Assert.AreEqual(40, o.ShardsSpent);
            Assert.AreEqual(1, o.Rerolls);
            Assert.AreEqual(0.15f, o.HealFraction, 1e-5f);
        }

        [Test]
        public void EveryEvent_HasTwoLabelledOptions_DeclineIsFree()
        {
            foreach (var name in new[] { "BloodPact", "Gamble", "Meditate", "ForgottenShrine" })
            {
                var def = Load(name);
                Assert.IsFalse(string.IsNullOrEmpty(def.title), name);
                Assert.IsFalse(string.IsNullOrEmpty(def.optionA.label), name);
                Assert.IsFalse(string.IsNullOrEmpty(def.optionB.label), name);
                Assert.AreEqual(0, RunEventResolver.ShardCost(def.optionB), name);
            }
        }

        /// <summary>Mirrors RunDirector.OpenEvent: the node substream first picks the event, then rolls.
        /// Seeds are spread like real run seeds (random 31-bit), not 1..64.</summary>
        private static RunEventOutcome Resolve(RunEventOption option, uint seed)
        {
            var rng = RunRng.ForNode(seed * 2654435761u, 3);
            rng.NextInt(4);
            return RunEventResolver.Resolve(option, ref rng);
        }

        [Test]
        public void PickOfRarity_ReturnsOnlyThatRarity_NullWhenNone()
        {
            var common = Boon("c", BoonRarity.Common);
            var rare = Boon("r", BoonRarity.Rare);
            var pool = new List<BoonDefinition> { common, rare };
            var rng = new RunRng(5);
            Assert.AreSame(rare, BoonOfferPicker.PickOfRarity(pool, _ => 0, BoonRarity.Rare, ref rng));
            Assert.IsNull(BoonOfferPicker.PickOfRarity(pool, _ => 0, BoonRarity.Epic, ref rng));
            Assert.IsNull(BoonOfferPicker.PickOfRarity(pool, id => id == "r" ? 3 : 0, BoonRarity.Rare, ref rng), "maxed boons are not offered");
            Object.DestroyImmediate(common);
            Object.DestroyImmediate(rare);
        }

        private static BoonDefinition Boon(string id, BoonRarity rarity)
        {
            var b = ScriptableObject.CreateInstance<BoonDefinition>();
            b.id = id;
            b.rarity = rarity;
            b.maxStacks = 3;
            return b;
        }

        // ---- Path choice ----

        [Test]
        public void PathPanel_ShowsWhenSeated_OrAfterSixStillSeconds()
        {
            Assert.IsTrue(RunDirector.ShouldShowPathPanel(true, 0f));
            Assert.IsFalse(RunDirector.ShouldShowPathPanel(false, 5.9f));
            Assert.IsTrue(RunDirector.ShouldShowPathPanel(false, 6f));
        }

        [Test]
        public void ColumnX_CentresOneTwoThreeOptions()
        {
            Assert.AreEqual(0f, BoonOfferPanel.ColumnX(0, 1));
            Assert.AreEqual(-160f, BoonOfferPanel.ColumnX(0, 2));
            Assert.AreEqual(160f, BoonOfferPanel.ColumnX(1, 2));
            Assert.AreEqual(-320f, BoonOfferPanel.ColumnX(0, 3));
        }
    }
}
