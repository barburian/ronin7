using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class MetaProgressionTests
    {
        private bool savedFirstPlanetDeparted;
        private int savedShipHullIndex;
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            // CampaignState.ApplyFrom also writes Galaxy1Progress/ShipSelection; save/restore so the
            // ApplyFrom test below doesn't leak state into other test classes (mirrors
            // CampaignStateAbilityTests.cs).
            savedFirstPlanetDeparted = Galaxy1Progress.FirstPlanetDeparted;
            savedShipHullIndex = ShipSelection.SelectedHullIndex;
            MetaProgression.Reset();
            // A2.4: this class mutates the same global CampaignState every sibling test class shares
            // (indirectly, via CampaignState.Reset()/ApplyFrom below) — reset it too, matching
            // CampaignStateAbilityTests' idiom exactly, or a later test class inherits stray state.
            CampaignState.Reset();

            testDirectory = Path.Combine(Path.GetTempPath(), "ss_metatests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveSystem.DirectoryOverride = testDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            MetaProgression.Reset();
            CampaignState.Reset();
            Galaxy1Progress.FirstPlanetDeparted = savedFirstPlanetDeparted;
            ShipSelection.SelectedHullIndex = savedShipHullIndex;

            SaveSystem.DirectoryOverride = null;
            try
            {
                if (Directory.Exists(testDirectory))
                    Directory.Delete(testDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to delete test directory {testDirectory}: {ex.Message}");
            }
        }

        [Test]
        public void InitialState_IsZeroed()
        {
            Assert.AreEqual(0, MetaProgression.Echoes);
            Assert.AreEqual(0, MetaProgression.BestDepth);
            Assert.AreEqual(0, MetaProgression.RunsCompleted);
            Assert.AreEqual(0, MetaProgression.RunsWon);
        }

        [Test]
        public void AddEchoes_Accumulates()
        {
            MetaProgression.AddEchoes(50);
            MetaProgression.AddEchoes(25);

            Assert.AreEqual(75, MetaProgression.Echoes);
        }

        [Test]
        public void AddEchoes_ClampsBelowZeroToZero()
        {
            MetaProgression.AddEchoes(10);
            MetaProgression.AddEchoes(-100);

            Assert.AreEqual(0, MetaProgression.Echoes);
        }

        [Test]
        public void TrySpend_SufficientBalance_Succeeds()
        {
            MetaProgression.AddEchoes(100);

            bool spent = MetaProgression.TrySpend(40);

            Assert.IsTrue(spent);
            Assert.AreEqual(60, MetaProgression.Echoes);
        }

        [Test]
        public void TrySpend_InsufficientBalance_Fails_DoesNotChangeBalance()
        {
            MetaProgression.AddEchoes(10);

            bool spent = MetaProgression.TrySpend(40);

            Assert.IsFalse(spent);
            Assert.AreEqual(10, MetaProgression.Echoes);
        }

        [Test]
        public void TrySpend_NegativeAmount_Fails()
        {
            MetaProgression.AddEchoes(100);

            Assert.IsFalse(MetaProgression.TrySpend(-1));
            Assert.AreEqual(100, MetaProgression.Echoes);
        }

        [Test]
        public void UpgradeLevel_Unset_ReturnsZero()
        {
            Assert.AreEqual(0, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void UpgradeLevel_UnknownId_ReturnsZero()
        {
            Assert.AreEqual(0, MetaProgression.UpgradeLevel("not_a_real_upgrade"));
        }

        [TestCase(MetaUpgradeId.StartingHealth, 5)]
        [TestCase(MetaUpgradeId.StartingDamage, 5)]
        [TestCase(MetaUpgradeId.StartingBoon, 3)]
        [TestCase(MetaUpgradeId.RerollTokens, 3)]
        public void MaxLevel_MatchesDocumentedMaxima(string upgradeId, int expectedMax)
        {
            Assert.AreEqual(expectedMax, MetaProgression.MaxLevel(upgradeId));
        }

        [Test]
        public void MaxLevel_UnknownId_IsZero()
        {
            Assert.AreEqual(0, MetaProgression.MaxLevel("not_a_real_upgrade"));
        }

        [TestCase(MetaUpgradeId.StartingHealth, 5)]
        [TestCase(MetaUpgradeId.StartingDamage, 5)]
        [TestCase(MetaUpgradeId.StartingBoon, 3)]
        [TestCase(MetaUpgradeId.RerollTokens, 3)]
        public void SetUpgradeLevel_ClampsToPerUpgradeMax(string upgradeId, int max)
        {
            MetaProgression.SetUpgradeLevel(upgradeId, max + 10);

            Assert.AreEqual(max, MetaProgression.UpgradeLevel(upgradeId));
        }

        [Test]
        public void SetUpgradeLevel_ClampsNegativeToZero()
        {
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, -5);

            Assert.AreEqual(0, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void SetUpgradeLevel_UnknownId_AlwaysClampsToZero()
        {
            MetaProgression.SetUpgradeLevel("not_a_real_upgrade", 3);

            Assert.AreEqual(0, MetaProgression.UpgradeLevel("not_a_real_upgrade"));
        }

        [Test]
        public void SetUpgradeLevel_UnknownId_DoesNotInsertAKey()
        {
            // A2.6: an unrecognized id must early-return, not insert a zeroed key that would
            // otherwise pollute every future WriteTo() with a typo'd entry.
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 2); // one real entry present
            MetaProgression.SetUpgradeLevel("typo_id", 3);

            MetaSaveData save = new MetaSaveData();
            MetaProgression.WriteTo(save);

            Assert.AreEqual(1, save.metaUpgrades.Count);
            Assert.AreEqual(MetaUpgradeId.StartingHealth, save.metaUpgrades[0].id);
        }

        [Test]
        public void SetUpgradeLevel_NullOrEmptyId_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MetaProgression.SetUpgradeLevel(null, 3));
            Assert.DoesNotThrow(() => MetaProgression.SetUpgradeLevel("", 3));
        }

        [Test]
        public void NoteRunEnded_IncrementsRunsCompleted_RegardlessOfOutcome()
        {
            MetaProgression.NoteRunEnded(5, false);
            MetaProgression.NoteRunEnded(8, true);

            Assert.AreEqual(2, MetaProgression.RunsCompleted);
        }

        [Test]
        public void NoteRunEnded_OnlyCountsWinsWhenWon()
        {
            MetaProgression.NoteRunEnded(5, false);
            MetaProgression.NoteRunEnded(8, true);
            MetaProgression.NoteRunEnded(3, false);

            Assert.AreEqual(1, MetaProgression.RunsWon);
        }

        [Test]
        public void NoteRunEnded_BestDepth_OnlyEverClimbs()
        {
            MetaProgression.NoteRunEnded(5, false);
            MetaProgression.NoteRunEnded(3, false);
            MetaProgression.NoteRunEnded(9, true);
            MetaProgression.NoteRunEnded(7, false);

            Assert.AreEqual(9, MetaProgression.BestDepth);
        }

        [Test]
        public void WriteTo_ThenApplyFrom_RoundTripsThroughMetaSaveData()
        {
            MetaProgression.AddEchoes(120);
            MetaProgression.NoteRunEnded(6, false);
            MetaProgression.NoteRunEnded(11, true);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 3);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.RerollTokens, 2);

            MetaSaveData save = new MetaSaveData();
            MetaProgression.WriteTo(save);

            MetaProgression.Reset();
            MetaProgression.ApplyFrom(save);

            Assert.AreEqual(120, MetaProgression.Echoes);
            Assert.AreEqual(11, MetaProgression.BestDepth);
            Assert.AreEqual(2, MetaProgression.RunsCompleted);
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
            Assert.AreEqual(2, MetaProgression.UpgradeLevel(MetaUpgradeId.RerollTokens));
        }

        [Test]
        public void WriteTo_SortsUpgradesById()
        {
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 1); // "meta_starting_health"
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.RerollTokens, 1);   // "meta_reroll_tokens"

            MetaSaveData save = new MetaSaveData();
            MetaProgression.WriteTo(save);

            Assert.AreEqual(2, save.metaUpgrades.Count);
            Assert.AreEqual(MetaUpgradeId.RerollTokens, save.metaUpgrades[0].id);
            Assert.AreEqual(MetaUpgradeId.StartingHealth, save.metaUpgrades[1].id);
        }

        [Test]
        public void WriteTo_SkipsZeroLevelEntries()
        {
            // A2.6: an upgrade set back down to 0 must not linger in the written file as dead weight.
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 2);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 0);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.RerollTokens, 1);

            MetaSaveData save = new MetaSaveData();
            MetaProgression.WriteTo(save);

            Assert.AreEqual(1, save.metaUpgrades.Count);
            Assert.AreEqual(MetaUpgradeId.RerollTokens, save.metaUpgrades[0].id);
        }

        [Test]
        public void ApplyFrom_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MetaProgression.ApplyFrom(null));
        }

        [Test]
        public void ApplyFrom_NullMetaUpgradesList_DoesNotThrow_LeavesUpgradesEmpty()
        {
            MetaSaveData save = new MetaSaveData();
            save.metaUpgrades = null;

            Assert.DoesNotThrow(() => MetaProgression.ApplyFrom(save));
            Assert.AreEqual(0, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void ApplyFrom_EntryWithOutOfRangeLevel_Clamps()
        {
            MetaSaveData save = new MetaSaveData();
            save.metaUpgrades.Add(new MetaUpgradeEntry { id = MetaUpgradeId.StartingBoon, level = 999 });

            MetaProgression.ApplyFrom(save);

            Assert.AreEqual(3, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingBoon));
        }

        [Test]
        public void ApplyFrom_EntryWithEmptyId_IsIgnored()
        {
            MetaSaveData save = new MetaSaveData();
            save.metaUpgrades.Add(new MetaUpgradeEntry { id = "", level = 5 });

            Assert.DoesNotThrow(() => MetaProgression.ApplyFrom(save));
        }

        [Test]
        public void CampaignState_Reset_DoesNotClearMetaProgression()
        {
            MetaProgression.AddEchoes(50);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 2);

            CampaignState.Reset();

            Assert.AreEqual(50, MetaProgression.Echoes);
            Assert.AreEqual(2, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void CampaignState_ToSaveData_DoesNotIncludeMetaProgression()
        {
            // A2.1: meta lives in its own meta.json now, not in the per-slot SaveData — confirms
            // CampaignState.ToSaveData() has no coupling left to MetaProgression at all.
            MetaProgression.AddEchoes(77);

            SaveData save = CampaignState.ToSaveData();

            Assert.IsFalse(HasField(save, "metaEchoes"));
        }

        [Test]
        public void CampaignState_ApplyFrom_DoesNotTouchMetaProgression()
        {
            MetaProgression.AddEchoes(33);

            CampaignState.ApplyFrom(new SaveData());

            Assert.AreEqual(33, MetaProgression.Echoes);
        }

        private static bool HasField(object obj, string fieldName)
        {
            return obj.GetType().GetField(fieldName) != null;
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            MetaProgression.AddEchoes(50);
            MetaProgression.NoteRunEnded(5, true);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 2);

            MetaProgression.Reset();

            Assert.AreEqual(0, MetaProgression.Echoes);
            Assert.AreEqual(0, MetaProgression.BestDepth);
            Assert.AreEqual(0, MetaProgression.RunsCompleted);
            Assert.AreEqual(0, MetaProgression.RunsWon);
            Assert.AreEqual(0, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        // ---- meta.json persistence (A2.1) ----

        [Test]
        public void Save_ThenLoad_RoundTripsThroughMetaJson()
        {
            MetaProgression.AddEchoes(500);
            MetaProgression.NoteRunEnded(9, true);
            MetaProgression.SetUpgradeLevel(MetaUpgradeId.StartingHealth, 4);

            MetaProgression.Save();
            MetaProgression.Reset();
            MetaProgression.Load();

            Assert.AreEqual(500, MetaProgression.Echoes);
            Assert.AreEqual(9, MetaProgression.BestDepth);
            Assert.AreEqual(1, MetaProgression.RunsCompleted);
            Assert.AreEqual(1, MetaProgression.RunsWon);
            Assert.AreEqual(4, MetaProgression.UpgradeLevel(MetaUpgradeId.StartingHealth));
        }

        [Test]
        public void Load_MissingMetaJson_LoadsZeroed_DoesNotThrow()
        {
            Assert.IsFalse(File.Exists(SaveSystemMetaPathForTest()));

            Assert.DoesNotThrow(() => MetaProgression.Load());

            Assert.AreEqual(0, MetaProgression.Echoes);
            Assert.AreEqual(0, MetaProgression.BestDepth);
        }

        [Test]
        public void Load_CorruptMetaJson_LoadsZeroed_DoesNotThrow()
        {
            File.WriteAllText(SaveSystemMetaPathForTest(), "{ not valid json ][");

            Assert.DoesNotThrow(() => MetaProgression.Load());

            Assert.AreEqual(0, MetaProgression.Echoes);
            Assert.AreEqual(0, MetaProgression.BestDepth);
            Assert.AreEqual(0, MetaProgression.RunsCompleted);
            Assert.AreEqual(0, MetaProgression.RunsWon);
        }

        /// <summary>Reconstructs SaveSystem.MetaPath's value without exposing the internal — mirrors
        /// how the slot tests know their own file layout.</summary>
        private string SaveSystemMetaPathForTest() => Path.Combine(testDirectory, "meta.json");

        [Test]
        public void NewGameAfterLoad_DoesNotWipeOrLeakMetaAcrossProfiles()
        {
            // Regression for the exact bug in Roguelike-Design.md A2.1: player banks 500 Echoes,
            // meta.json holds them. Next session boots (MetaProgression.Load()), then the player
            // hits "Start New Game" WITHOUT loading a slot first. That must not zero the banked
            // Echoes, and the resulting per-slot autosave must carry no meta fields to leak into
            // (or wipe) any other slot's profile.
            MetaProgression.AddEchoes(500);
            MetaProgression.Save();

            // Simulate the next boot loading meta once, globally.
            MetaProgression.Reset();
            MetaProgression.Load();
            Assert.AreEqual(500, MetaProgression.Echoes, "meta.json round trip failed to set up the regression scenario");

            // "Start New Game" (no prior slot Load): CampaignState.Reset() runs, MetaProgression is
            // untouched by design (A2.1's whole point).
            CampaignState.Reset();
            Assert.AreEqual(500, MetaProgression.Echoes, "Start New Game must not wipe banked Echoes");

            // Arrival autosave to slot 1 — a fresh SaveData has no meta fields left to write zeros
            // into another profile with, because SaveData no longer carries meta at all.
            SaveData arrivalSave = CampaignState.ToSaveData();
            SaveSystem.Save(1, arrivalSave);

            // A different profile (slot 2) reads the SAME meta.json — there is no per-slot copy to
            // leak from or into.
            MetaProgression.Reset();
            MetaProgression.Load();
            Assert.AreEqual(500, MetaProgression.Echoes, "meta must survive a New Game + autosave round trip");
        }
    }
}
