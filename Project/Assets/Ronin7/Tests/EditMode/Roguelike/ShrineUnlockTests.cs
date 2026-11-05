using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P4-10 Shrine variety unlocks: weapons, Art slots + selector, packs, Keepsakes, Unforgiving Guard haptic seam.</summary>
    public class ShrineUnlockTests
    {
        private string dir;
        private readonly List<UnityEngine.Object> created = new();

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            dir = Path.Combine(Path.GetTempPath(), "ss_shrine_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            MetaProgression.Load(); // Save() refuses before Load (BUG-25)
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            MetaProgression.ResetForTests();
            RunState.Reset();
            SaveSystem.DirectoryOverride = null;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        [TestCase(WeaponId.TwinWakizashi, 300)]
        [TestCase(WeaponId.PolearmShield, 450)] // P9-02c provisional; the 2-win requirement comes in P9-03
        [TestCase(MetaUpgradeId.WeaponArtSlot, 250)]
        [TestCase(MetaUpgradeId.OffhandArtSlot, 250)]
        [TestCase(BoonPackId.Bleed, 120)]
        [TestCase(BoonPackId.Storm, 120)]
        [TestCase(BoonPackId.Executioner, 120)]
        public void Shrine_Purchase_CostDeductionAndNoDoubleBuy(string id, int cost)
        {
            Assert.AreEqual(cost, MetaProgression.UpgradeCost(id, 1));
            MetaProgression.AddEchoes(cost - 1);
            Assert.IsFalse(MetaProgression.TryPurchase(id), "insufficient echoes refused");
            Assert.AreEqual(cost - 1, MetaProgression.Echoes);

            MetaProgression.AddEchoes(1 + 500);
            Assert.IsTrue(MetaProgression.TryPurchase(id));
            Assert.AreEqual(500, MetaProgression.Echoes);
            Assert.IsFalse(MetaProgression.TryPurchase(id), "no double-buy");
            Assert.AreEqual(500, MetaProgression.Echoes);
        }

        [Test]
        public void Shrine_WeaponPurchase_UnlocksAndPersists()
        {
            MetaProgression.AddEchoes(300);
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi));
            Assert.IsTrue(MetaProgression.TryPurchase(WeaponId.TwinWakizashi));
            MetaProgression.Load();
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi));
            Assert.AreEqual(0, MetaProgression.Echoes);
        }

        [Test]
        public void Shrine_PolearmPurchase_UnlocksPersistsAndIsSelectable()
        {
            MetaProgression.AddEchoes(450);
            Assert.IsTrue(MetaProgression.TryPurchase(WeaponId.PolearmShield));
            MetaProgression.Load();
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.PolearmShield));
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.PolearmShield));
            Assert.AreEqual(WeaponId.PolearmShield, MetaProgression.SelectedLoadout);
            Assert.AreEqual(0, MetaProgression.Echoes);
        }

        [TestCase(WeaponId.Nodachi)]
        [TestCase(WeaponId.Tanto)]
        public void Shrine_OffRosterWeapon_RefusedAndNotCharged(string id)
        {
            MetaProgression.AddEchoes(1000);
            Assert.IsFalse(MetaProgression.TryPurchase(id));
            Assert.AreEqual(1000, MetaProgression.Echoes);
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(id));
        }

        [Test]
        public void Shrine_PackPurchase_MakesBoonsOfferable()
        {
            var bleed = ScriptableObject.CreateInstance<BoonDefinition>();
            created.Add(bleed);
            bleed.id = "test_bleed";
            bleed.rarity = BoonRarity.Common;
            bleed.tags = BoonTag.Bleed;
            bleed.maxStacks = 1;
            var pool = new List<BoonDefinition> { bleed };

            var rng = RunRng.ForNode(1u, 0);
            Assert.AreEqual(0, BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng).Count);

            MetaProgression.AddEchoes(120);
            Assert.IsTrue(MetaProgression.TryPurchase(BoonPackId.Bleed));
            rng = RunRng.ForNode(1u, 0);
            CollectionAssert.Contains(BoonOfferPicker.Pick(pool, _ => 0, RoomKind.Combat, ref rng), bleed);
        }

        [Test]
        public void Shrine_ArtSlot_EnablesEquip_AppliedAtRunStart()
        {
            Assert.IsFalse(MetaProgression.TryEquipArt(true, ArtId.BladeThrow), "slot not bought");
            MetaProgression.AddEchoes(250);
            Assert.IsTrue(MetaProgression.TryPurchase(MetaUpgradeId.WeaponArtSlot));
            Assert.IsTrue(MetaProgression.TryEquipArt(true, ArtId.BladeThrow));
            Assert.AreEqual("", MetaProgression.EquippedArt(false), "off-hand slot still unbought");

            MetaProgression.Save();
            MetaProgression.Load();
            RunState.Begin(9u);
            RunDirector.ApplyMetaArts();
            Assert.AreEqual(ArtId.BladeThrow, RunState.WeaponArtId);
            Assert.AreEqual("", RunState.OffhandArtId);
        }

        // P7-02 (BUG-49): buying the slot used to leave it on NONE until a second click, so the run started without an Art.
        [Test]
        public void ShrineArtSlot_Buy_EquipsFirstArt()
        {
            MetaProgression.AddEchoes(500);
            Assert.IsTrue(MetaShopPanel.BuyArtSlot(true));
            Assert.IsTrue(MetaShopPanel.BuyArtSlot(false));
            Assert.IsFalse(MetaShopPanel.BuyArtSlot(true), "no double-buy");

            MetaProgression.Load(); // the equip was saved
            Assert.AreEqual(ArtId.ChargedSlash, MetaProgression.EquippedArt(true));
            Assert.AreEqual(ArtId.KiaiPalm, MetaProgression.EquippedArt(false));
            RunState.Begin(9u);
            RunDirector.ApplyMetaArts();
            Assert.AreEqual(ArtId.ChargedSlash, RunState.WeaponArtId);
            Assert.AreEqual(ArtId.KiaiPalm, RunState.OffhandArtId);
        }

        [Test]
        public void Shrine_ArtSelector_CyclesThroughNone()
        {
            Assert.AreEqual(ArtId.KiaiPalm, MetaShopPanel.NextArt(MetaShopPanel.OffhandArts, ""));
            Assert.AreEqual("", MetaShopPanel.NextArt(MetaShopPanel.OffhandArts, ArtId.ElementImbue));
            Assert.AreEqual("", MetaShopPanel.NextArt(MetaShopPanel.WeaponArts, "bogus"), "unknown id restarts at NONE");
            Assert.AreEqual(ArtId.ChargedSlash, MetaShopPanel.NextArt(MetaShopPanel.WeaponArts, ""));
        }

        [Test]
        public void Keepsake_RecordBuyAndUse()
        {
            var epic = ScriptableObject.CreateInstance<BoonDefinition>();
            created.Add(epic);
            epic.id = BoonId.GrantOverdrive;
            epic.rarity = BoonRarity.Epic;
            epic.effect = BoonEffectKind.GrantAbility;
            epic.abilityId = AbilityId.Overdrive;
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            created.Add(catalog);
            catalog.boons = new[] { epic };

            MetaProgression.AddEchoes(1000);
            Assert.IsFalse(MetaProgression.TryBuyKeepsake(epic.id), "never picked");
            MetaProgression.NoteEpicPicked(epic.id);
            MetaProgression.NoteEpicPicked(epic.id);
            Assert.AreEqual(1, MetaProgression.PastEpics.Count);

            Assert.IsTrue(MetaProgression.TryBuyKeepsake(epic.id));
            Assert.AreEqual(800, MetaProgression.Echoes);
            Assert.IsFalse(MetaProgression.TryBuyKeepsake(epic.id), "no double-buy");
            Assert.AreEqual(800, MetaProgression.Echoes);
            Assert.IsTrue(MetaProgression.TrySelectKeepsake(epic.id));
            MetaProgression.Save();
            MetaProgression.Load();
            Assert.AreEqual(epic.id, MetaProgression.SelectedKeepsake);

            RunState.Begin(3u);
            RunDirector.ApplyKeepsake(catalog);
            Assert.AreEqual(1, RunState.BoonCount(epic.id));
            Assert.IsTrue(RunState.HasGrantedAbility(AbilityId.Overdrive));
        }

        [Test]
        public void Keepsake_InsufficientEchoes_Refused()
        {
            MetaProgression.NoteEpicPicked(BoonId.GrantMirror);
            MetaProgression.AddEchoes(MetaProgression.KeepsakeCost - 1);
            Assert.IsFalse(MetaProgression.TryBuyKeepsake(BoonId.GrantMirror));
            Assert.IsFalse(MetaProgression.TrySelectKeepsake(BoonId.GrantMirror), "unowned cannot be selected");
        }

        [Test]
        public void MetaSave_V1WithoutNewFields_LoadsDefaults()
        {
            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":42}");
            Assert.AreEqual(1, data.version);
            MetaProgression.ApplyFrom(data);
            Assert.AreEqual(42, MetaProgression.Echoes);
            Assert.AreEqual(0, MetaProgression.PastEpics.Count);
            Assert.AreEqual("", MetaProgression.SelectedKeepsake);
            Assert.IsFalse(MetaProgression.HasArtSlot(true));
            Assert.IsFalse(MetaProgression.HasArtSlot(false));
            Assert.AreEqual("", MetaProgression.EquippedArt(true));
            Assert.IsFalse(MetaProgression.IsPackUnlocked(BoonPackId.Storm));
            Assert.IsFalse(MetaProgression.IsWeaponUnlocked(WeaponId.Nodachi));
        }

        [Test]
        public void UnforgivingGuard_Chip_PublishesGuardChipped_NotPlayerHit()
        {
            var heat = new List<int>(new int[Heat.Count]);
            heat[(int)HeatRank.UnforgivingGuard] = 1;
            MetaProgression.ApplyFrom(new MetaSaveData { metaRunsWon = 1, heatRanks = heat });
            RunState.Begin(5u);

            var go = new GameObject("Player");
            created.Add(go);
            var hp = go.AddComponent<Health>();
            hp.Configure(100f);
            hp.SetCurrent(100f);

            int chips = 0, hits = 0;
            Action<GuardChipped> onChip = _ => chips++;
            Action<PlayerHit> onHit = _ => hits++;
            EventBus.Subscribe(onChip);
            EventBus.Subscribe(onHit);
            try
            {
                Assert.IsTrue(MeleeAttacker.TryGuardChip(hp, 20f, null));
                Assert.AreEqual(1, chips);
                Assert.AreEqual(0, hits, "chip must not reset the parry streak");
                Assert.AreEqual(100f - 20f * Heat.ChipFraction, hp.Current, 1e-4f);

                RunState.Reset();
                Assert.IsFalse(MeleeAttacker.TryGuardChip(hp, 20f, null), "no heat, no chip");
                Assert.AreEqual(1, chips);
            }
            finally
            {
                EventBus.Unsubscribe(onChip);
                EventBus.Unsubscribe(onHit);
            }
        }
    }
}
