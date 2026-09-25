using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Boons
{
    /// <summary>
    /// Covers BoonOfferPicker.Pick's contract: distinct offers, max-stack exclusion, graceful
    /// degradation on a null/small pool, determinism for a fixed RunRng seed, and the rarity-weight
    /// shape (Combat favours Common, Elite shifts toward Rare, Boss shifts hardest toward Epic,
    /// Treasure excludes Common).
    /// </summary>
    public class BoonOfferPickerTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private BoonDefinition MakeBoon(string id, BoonRarity rarity, int maxStacks = 3)
        {
            var boon = ScriptableObject.CreateInstance<BoonDefinition>();
            boon.id = id;
            boon.rarity = rarity;
            boon.maxStacks = maxStacks;
            _created.Add(boon);
            return boon;
        }

        private List<BoonDefinition> MakeVariedPool()
        {
            return new List<BoonDefinition>
            {
                MakeBoon("boon_a", BoonRarity.Common),
                MakeBoon("boon_b", BoonRarity.Common),
                MakeBoon("boon_c", BoonRarity.Rare),
                MakeBoon("boon_d", BoonRarity.Rare),
                MakeBoon("boon_e", BoonRarity.Epic),
                MakeBoon("boon_f", BoonRarity.Epic),
            };
        }

        // ---- Degradation ----

        [Test]
        public void Pick_NullPool_ReturnsEmptyList()
        {
            var rng = new RunRng(1);
            var offer = BoonOfferPicker.Pick(null, _ => 0, RoomKind.Combat, ref rng);
            Assert.IsNotNull(offer);
            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_EmptyPool_ReturnsEmptyList()
        {
            var rng = new RunRng(1);
            var offer = BoonOfferPicker.Pick(new List<BoonDefinition>(), _ => 0, RoomKind.Combat, ref rng);
            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_PoolSmallerThanCount_ReturnsAllEligibleWithoutThrowing()
        {
            var pool = new List<BoonDefinition> { MakeBoon("boon_only", BoonRarity.Common) };
            var rng = new RunRng(1);

            List<BoonDefinition> offer = null;
            Assert.DoesNotThrow(() => offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3));
            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("boon_only", offer[0].id);
        }

        [Test]
        public void Pick_AllBoonsMaxed_ReturnsEmptyList()
        {
            var pool = new List<BoonDefinition> { MakeBoon("boon_a", BoonRarity.Common, maxStacks: 1) };
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 1, RoomKind.Combat, ref rng);

            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_NullStacksHeld_DoesNotThrowAndTreatsAllAsUnheld()
        {
            var pool = new List<BoonDefinition> { MakeBoon("boon_a", BoonRarity.Common) };
            var rng = new RunRng(1);

            List<BoonDefinition> offer = null;
            Assert.DoesNotThrow(() => offer = BoonOfferPicker.Pick(pool, null, RoomKind.Combat, ref rng));
            Assert.AreEqual(1, offer.Count);
        }

        [Test]
        public void Pick_CountZero_ReturnsEmptyList()
        {
            var pool = MakeVariedPool();
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 0);

            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_CountNegative_ReturnsEmptyList()
        {
            var pool = MakeVariedPool();
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: -1);

            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_PoolWithNullEntries_SkipsThemWithoutThrowing()
        {
            var pool = new List<BoonDefinition> { null, MakeBoon("boon_a", BoonRarity.Common), null };
            var rng = new RunRng(1);

            List<BoonDefinition> offer = null;
            Assert.DoesNotThrow(() => offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3));
            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("boon_a", offer[0].id);
        }

        [Test]
        public void Pick_PoolEntryWithNullOrEmptyId_IsSkipped()
        {
            var pool = new List<BoonDefinition>
            {
                MakeBoon(null, BoonRarity.Common),
                MakeBoon("", BoonRarity.Common),
                MakeBoon("boon_valid", BoonRarity.Common),
            };
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);

            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("boon_valid", offer[0].id);
        }

        [Test]
        public void Pick_PoolEntryWithMaxStacksZero_TreatedAsOfferableOnce()
        {
            // A mis-authored maxStacks (0) must degrade to "offerable once", not "silently never
            // offerable" (A1.2).
            var pool = new List<BoonDefinition> { MakeBoon("boon_a", BoonRarity.Common, maxStacks: 0) };
            var rng = new RunRng(1);

            var offerWhenUnheld = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);
            Assert.AreEqual(1, offerWhenUnheld.Count);

            var rng2 = new RunRng(1);
            var offerWhenHeldOnce = BoonOfferPicker.Pick(pool, _ => 1, RoomKind.Combat, ref rng2, count: 3);
            Assert.AreEqual(0, offerWhenHeldOnce.Count);
        }

        [Test]
        public void Pick_DoesNotMutateCallerPoolList()
        {
            var pool = MakeVariedPool();
            var poolCopy = new List<BoonDefinition>(pool);
            var rng = new RunRng(1);

            BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);

            CollectionAssert.AreEqual(poolCopy, pool);
        }

        [Test]
        public void Pick_DrawCountStability_RngStateAdvancesByExactlyResultCountDraws()
        {
            // Downstream per-node determinism depends on Pick consuming exactly one RunRng draw per
            // boon it actually offers (and zero draws when it gives up early on all-zero weights).
            var pool = MakeVariedPool();
            var rng = new RunRng(2026);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);

            var expectedRng = new RunRng(2026);
            for (int i = 0; i < offer.Count; i++) expectedRng.NextUInt();

            Assert.AreEqual(expectedRng.State, rng.State);
        }

        // ---- Max-stack exclusion ----

        [Test]
        public void Pick_BoonAtMaxStacks_IsExcludedFromOffer()
        {
            var maxed = MakeBoon("boon_maxed", BoonRarity.Common, maxStacks: 2);
            var available = MakeBoon("boon_available", BoonRarity.Common);
            var pool = new List<BoonDefinition> { maxed, available };
            var rng = new RunRng(42);

            var offer = BoonOfferPicker.Pick(pool, id => id == "boon_maxed" ? 2 : 0, RoomKind.Combat, ref rng, count: 3);

            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("boon_available", offer[0].id);
        }

        // ---- Distinctness ----

        [Test]
        public void Pick_LargePool_ReturnsRequestedCountAllDistinct()
        {
            var pool = MakeVariedPool();
            var rng = new RunRng(55);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);

            Assert.AreEqual(3, offer.Count);
            var ids = new HashSet<string>();
            foreach (var boon in offer) Assert.IsTrue(ids.Add(boon.id), "offer contained a duplicate id");
        }

        [Test]
        public void Pick_DuplicateIdsInPool_NeverOffersTheSameIdTwice()
        {
            var dupA = MakeBoon("boon_dup", BoonRarity.Common);
            var dupB = MakeBoon("boon_dup", BoonRarity.Common);
            var other = MakeBoon("boon_other", BoonRarity.Common);
            var pool = new List<BoonDefinition> { dupA, dupB, other };
            var rng = new RunRng(7);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);

            var ids = new HashSet<string>();
            foreach (var boon in offer) Assert.IsTrue(ids.Add(boon.id));
            Assert.AreEqual(2, offer.Count); // "boon_dup" once + "boon_other"
        }

        // ---- Determinism ----

        [Test]
        public void Pick_SameSeed_ProducesIdenticalOffer()
        {
            var pool = MakeVariedPool();
            var rngA = new RunRng(2026);
            var rngB = new RunRng(2026);

            var offerA = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Elite, ref rngA, count: 3);
            var offerB = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Elite, ref rngB, count: 3);

            Assert.AreEqual(offerA.Count, offerB.Count);
            for (int i = 0; i < offerA.Count; i++)
                Assert.AreEqual(offerA[i].id, offerB[i].id);
        }

        [Test]
        public void Pick_DifferentSeeds_CanProduceDifferentOffers()
        {
            // Not a strict guarantee for any two seeds, but across a spread of seeds against a varied
            // pool we should see at least one difference — otherwise the rng isn't actually driving picks.
            var pool = MakeVariedPool();
            string firstSignature = null;
            bool sawDifference = false;

            for (uint seed = 1; seed <= 20; seed++)
            {
                var rng = new RunRng(seed);
                var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng, count: 3);
                string signature = string.Join(",", offer.ConvertAll(b => b.id));
                if (firstSignature == null) firstSignature = signature;
                else if (signature != firstSignature) sawDifference = true;
            }

            Assert.IsTrue(sawDifference, "Expected at least one different offer across 20 seeds.");
        }

        // ---- Rarity weighting shape ----

        [Test]
        public void RarityWeight_CombatFavoursCommonOverEpic()
        {
            Assert.Greater(BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Combat),
                BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Combat));
        }

        [Test]
        public void RarityWeight_EliteShiftsTowardRareRelativeToCombat()
        {
            int combatRare = BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Combat);
            int eliteRare = BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Elite);

            Assert.Greater(eliteRare, combatRare);
        }

        [Test]
        public void RarityWeight_BossShiftsHardestTowardEpic()
        {
            int combatEpic = BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Combat);
            int eliteEpic = BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Elite);
            int bossEpic = BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Boss);

            Assert.Greater(eliteEpic, combatEpic);
            Assert.Greater(bossEpic, eliteEpic);
            Assert.Greater(bossEpic, BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Boss));
        }

        [Test]
        public void RarityWeight_TreasureExcludesCommonButAllowsRareAndEpic()
        {
            Assert.AreEqual(0, BoonOfferPicker.RarityWeight(BoonRarity.Common, RoomKind.Treasure));
            Assert.Greater(BoonOfferPicker.RarityWeight(BoonRarity.Rare, RoomKind.Treasure), 0);
            Assert.Greater(BoonOfferPicker.RarityWeight(BoonRarity.Epic, RoomKind.Treasure), 0);
        }

        [Test]
        public void Pick_TreasureKind_NeverOffersCommonRarity()
        {
            var pool = MakeVariedPool();
            var rng = new RunRng(999);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Treasure, ref rng, count: 3);

            foreach (var boon in offer)
                Assert.AreNotEqual(BoonRarity.Common, boon.rarity);
        }

        // ---- A1.6: offers may legitimately be smaller than `count` from a non-empty pool ----

        [Test]
        public void Pick_TreasureKind_CommonOnlyPool_ReturnsEmptyOffer()
        {
            // Common has weight 0 for Treasure, so an all-Common pool has nothing eligible to draw —
            // Pick must return 0 items rather than throwing or stalling.
            var pool = new List<BoonDefinition>
            {
                MakeBoon("boon_a", BoonRarity.Common),
                MakeBoon("boon_b", BoonRarity.Common),
            };
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Treasure, ref rng, count: 3);

            Assert.AreEqual(0, offer.Count);
        }

        [Test]
        public void Pick_TreasureKind_CommonAndRarePool_CountThree_ReturnsOne()
        {
            // Only the single Rare entry is eligible (Common excluded); Pick must return exactly that
            // one item rather than 3, since a second draw has nothing left with positive weight.
            var pool = new List<BoonDefinition>
            {
                MakeBoon("boon_common", BoonRarity.Common),
                MakeBoon("boon_rare", BoonRarity.Rare),
            };
            var rng = new RunRng(1);

            var offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Treasure, ref rng, count: 3);

            Assert.AreEqual(1, offer.Count);
            Assert.AreEqual("boon_rare", offer[0].id);
        }

        [Test]
        public void Pick_ForgeKind_DoesNotThrow()
        {
            // Forge nodes don't offer boons per the design doc, but RarityWeight must still degrade
            // gracefully (falls back to Combat's weights) rather than throwing for an unhandled kind.
            var pool = MakeVariedPool();
            var rng = new RunRng(3);

            List<BoonDefinition> offer = null;
            Assert.DoesNotThrow(() => offer = BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Forge, ref rng, count: 3));
            Assert.AreEqual(3, offer.Count);
        }
    }
}
