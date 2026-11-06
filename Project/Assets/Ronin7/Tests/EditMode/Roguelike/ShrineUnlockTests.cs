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
    /// <summary>P4-10 Shrine variety unlocks: weapons, Art selector, Unforgiving Guard haptic seam.</summary>
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
        public void Shrine_Purchase_CostDeductionAndNoDoubleBuy(string id, int cost)
        {
            for (int i = 0; i < MetaProgression.WinsRequired(id); i++) MetaProgression.NoteRunEnded(15, true); // A15.12: the win gate
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
            for (int i = 0; i < MetaProgression.WinsRequired(WeaponId.TwinWakizashi); i++) MetaProgression.NoteRunEnded(15, true); // A15.12: the win gate
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
            for (int i = 0; i < MetaProgression.WinsRequired(WeaponId.PolearmShield); i++) MetaProgression.NoteRunEnded(15, true); // A15.12: the win gate
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

        // R9: both Art slots are free and default to Charged Slash / Kiai Palm; a chosen Art persists.
        [Test]
        public void Arts_DefaultFree_ChosenArtPersists_AppliedAtRunStart()
        {
            Assert.AreEqual(ArtId.ChargedSlash, MetaProgression.EquippedArt(true));
            Assert.AreEqual(ArtId.KiaiPalm, MetaProgression.EquippedArt(false));
            RunState.Begin(9u);
            RunDirector.ApplyMetaArts();
            Assert.AreEqual(ArtId.ChargedSlash, RunState.WeaponArtId);
            Assert.AreEqual(ArtId.KiaiPalm, RunState.OffhandArtId);

            Assert.IsTrue(MetaProgression.TryEquipArt(true, ArtId.BladeThrow));
            MetaProgression.Save();
            MetaProgression.Load();
            Assert.AreEqual(ArtId.BladeThrow, MetaProgression.EquippedArt(true), "a previously chosen art stays");
            Assert.AreEqual(ArtId.KiaiPalm, MetaProgression.EquippedArt(false));
        }

        [Test]
        public void Arts_OldSaveWithEmptyArts_GetsDefaults_AndArtSlotUpgradesCannotBeBought()
        {
            MetaProgression.ApplyFrom(JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":900,\"weaponArt\":\"\",\"offhandArt\":\"\"}"));
            Assert.AreEqual(ArtId.ChargedSlash, MetaProgression.EquippedArt(true));
            Assert.AreEqual(ArtId.KiaiPalm, MetaProgression.EquippedArt(false));
            Assert.IsFalse(MetaProgression.TryPurchase("meta_weapon_art_slot"), "R9: art_slot purchases are gone");
            Assert.AreEqual(900, MetaProgression.Echoes);
        }

        [Test]
        public void Shrine_ArtSelector_CyclesWithoutNone()
        {
            Assert.AreEqual(ArtId.ChainHook, MetaShopPanel.NextArt(MetaShopPanel.OffhandArts, ArtId.KiaiPalm));
            Assert.AreEqual(ArtId.KiaiPalm, MetaShopPanel.NextArt(MetaShopPanel.OffhandArts, ArtId.ElementImbue));
            Assert.AreEqual(ArtId.ChargedSlash, MetaShopPanel.NextArt(MetaShopPanel.WeaponArts, "bogus"));
            Assert.AreEqual(ArtId.BladeThrow, MetaShopPanel.NextArt(MetaShopPanel.WeaponArts, ArtId.ChargedSlash));
        }

        [TestCase(ArtId.ChargedSlash)]
        [TestCase(ArtId.BladeThrow)]
        [TestCase(ArtId.KiaiPalm)]
        [TestCase(ArtId.ChainHook)]
        [TestCase(ArtId.ElementImbue)]
        public void Arts_EachHasAHelpLineNamingItsTrigger(string id)
            => StringAssert.Contains("TRIGGER", MetaShopPanel.ArtHelp(id));

        [Test]
        public void MetaSave_V1WithoutNewFields_LoadsDefaults()
        {
            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":42}");
            Assert.AreEqual(1, data.version);
            MetaProgression.ApplyFrom(data);
            Assert.AreEqual(42, MetaProgression.Echoes);
            Assert.AreEqual(ArtId.ChargedSlash, MetaProgression.EquippedArt(true));
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
