using NUnit.Framework;
using Ronin7.Core;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    public class SaveSystemTests
    {
        private string testDirectory;
        private string previousPrefsValue;

        [SetUp]
        public void SetUp()
        {
            // Create a fresh temp directory for this test
            testDirectory = Path.Combine(Path.GetTempPath(), "ss_savetests_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(testDirectory);
            SaveSystem.DirectoryOverride = testDirectory;

            // Save the previous MostRecentSlot value to restore later
            previousPrefsValue = PlayerPrefs.GetInt("ss.lastSaveSlot", 1).ToString();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the test directory
            try
            {
                if (System.IO.Directory.Exists(testDirectory))
                {
                    System.IO.Directory.Delete(testDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to delete test directory {testDirectory}: {ex.Message}");
            }

            // Restore DirectoryOverride
            SaveSystem.DirectoryOverride = null;

            // Restore previous PlayerPrefs value
            if (previousPrefsValue != null && int.TryParse(previousPrefsValue, out int priorValue))
            {
                PlayerPrefs.SetInt("ss.lastSaveSlot", priorValue);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void Save_ThenLoad_RoundTripsData()
        {
            // Arrange
            SaveData original = new SaveData();
            original.completedPlanetScenes.Add("Planet_A");
            original.completedPlanetScenes.Add("Planet_B");
            original.lastPlanetScene = "Planet_B";
            original.firstPlanetDeparted = true;
            original.shipHullIndex = 2;

            // Act
            SaveSystem.Save(1, original);
            SaveData loaded = SaveSystem.Load(1);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.completedPlanetScenes.Count);
            Assert.Contains("Planet_A", loaded.completedPlanetScenes);
            Assert.Contains("Planet_B", loaded.completedPlanetScenes);
            Assert.AreEqual("Planet_B", loaded.lastPlanetScene);
            Assert.IsTrue(loaded.firstPlanetDeparted);
            Assert.AreEqual(2, loaded.shipHullIndex);
        }

        [Test]
        public void Load_NeverSavedSlot_ReturnsNull()
        {
            // Act
            SaveData loaded = SaveSystem.Load(1);
            bool exists = SaveSystem.SlotExists(1);

            // Assert
            Assert.IsNull(loaded);
            Assert.IsFalse(exists);
        }

        [Test]
        public void Load_CorruptFile_ReturnsNull()
        {
            // Arrange
            string slotPath = Path.Combine(testDirectory, "slot1.json");
            File.WriteAllText(slotPath, "{ this is not valid json ][");

            // Act & Assert - should not throw, just return null. Error-log count varies by
            // Unity version (the JSON parser may log before throwing), so ignore log failures
            // for this test rather than expecting an exact message.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                SaveData loaded = SaveSystem.Load(1);
                Assert.IsNull(loaded);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void Load_FutureVersion_ReturnsNull()
        {
            // Arrange
            SaveData futureData = new SaveData();
            futureData.version = SaveData.CurrentVersion + 1;
            futureData.lastPlanetScene = "Planet_A";

            string json = JsonUtility.ToJson(futureData, true);
            string slotPath = Path.Combine(testDirectory, "slot2.json");
            File.WriteAllText(slotPath, json);

            // Act & Assert
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*newer than CurrentVersion.*"));
            SaveData loaded = SaveSystem.Load(2);
            Assert.IsNull(loaded);
        }

        [Test]
        public void Load_OlderVersion_MigratesToCurrentVersionWithoutDataLoss()
        {
            // Arrange - hand-write a save tagged with a version below current.
            string olderJson = @"{
  ""version"": 0,
  ""lastPlanetScene"": ""Planet_A"",
  ""completedPlanetScenes"": [""Planet_A""],
  ""shipHullIndex"": 3,
  ""firstPlanetDeparted"": true
}";
            string slotPath = Path.Combine(testDirectory, "slot1.json");
            File.WriteAllText(slotPath, olderJson);

            // Act
            SaveData loaded = SaveSystem.Load(1);

            // Assert - upgraded to current version, no data dropped.
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveData.CurrentVersion, loaded.version);
            Assert.AreEqual("Planet_A", loaded.lastPlanetScene);
            Assert.AreEqual(1, loaded.completedPlanetScenes.Count);
            Assert.Contains("Planet_A", loaded.completedPlanetScenes);
            Assert.AreEqual(3, loaded.shipHullIndex);
            Assert.IsTrue(loaded.firstPlanetDeparted);
        }

        [Test]
        public void Save_LeftoverTempFile_DoesNotCorruptPriorSaveAndIsCleaned()
        {
            // Arrange - a valid prior save, plus a stray temp from a simulated interrupted write.
            SaveData original = new SaveData();
            original.lastPlanetScene = "Planet_A";
            original.completedPlanetScenes.Add("Planet_A");
            SaveSystem.Save(1, original);

            string tempPath = Path.Combine(testDirectory, "slot1.json.tmp");
            File.WriteAllText(tempPath, "{ half-written garbage ][");

            // Act
            SaveData loaded = SaveSystem.Load(1);

            // Assert - prior valid save still loads intact, and the stray temp is cleaned up.
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Planet_A", loaded.lastPlanetScene);
            Assert.AreEqual(1, loaded.completedPlanetScenes.Count);
            Assert.IsFalse(File.Exists(tempPath), "stray temp file should be removed on load");
        }

        [Test]
        public void Save_NormalWrite_LeavesNoTempFileBehind()
        {
            // Arrange
            SaveData data = new SaveData();
            data.lastPlanetScene = "Planet_A";

            // Act
            SaveSystem.Save(1, data);

            // Assert - the atomic swap consumes the temp file.
            string tempPath = Path.Combine(testDirectory, "slot1.json.tmp");
            Assert.IsFalse(File.Exists(tempPath));
            Assert.IsTrue(File.Exists(Path.Combine(testDirectory, "slot1.json")));
        }

        [Test]
        public void Save_OverwriteExistingSlot_RoundTripsNewData()
        {
            // Arrange - first save establishes an existing file so the swap takes the File.Replace path.
            SaveData first = new SaveData();
            first.lastPlanetScene = "Planet_A";
            SaveSystem.Save(1, first);

            // Act - second save must replace the existing slot atomically.
            SaveData second = new SaveData();
            second.lastPlanetScene = "Planet_B";
            second.completedPlanetScenes.Add("Planet_A");
            SaveSystem.Save(1, second);
            SaveData loaded = SaveSystem.Load(1);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Planet_B", loaded.lastPlanetScene);
            Assert.AreEqual(1, loaded.completedPlanetScenes.Count);
        }

        [Test]
        public void Load_MissingItemIdsAbilityIds_LoadsWithEmptyLists()
        {
            // Arrange - hand-write JSON with only version and lastPlanetScene
            string minimalJson = @"{
  ""version"": 1,
  ""lastPlanetScene"": ""Planet_A""
}";
            string slotPath = Path.Combine(testDirectory, "slot1.json");
            File.WriteAllText(slotPath, minimalJson);

            // Act
            SaveData loaded = SaveSystem.Load(1);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Planet_A", loaded.lastPlanetScene);
            Assert.IsNotNull(loaded.itemIds);
            Assert.IsNotNull(loaded.abilityIds);
            Assert.AreEqual(0, loaded.itemIds.Count);
            Assert.AreEqual(0, loaded.abilityIds.Count);
        }

        [Test]
        public void Load_MissingStatsFields_DefaultsToZero()
        {
            // Arrange - hand-write JSON with only version and lastPlanetScene, no stats fields
            string minimalJson = @"{
  ""version"": 1,
  ""lastPlanetScene"": ""Planet_A""
}";
            string slotPath = Path.Combine(testDirectory, "slot1.json");
            File.WriteAllText(slotPath, minimalJson);

            // Act
            SaveData loaded = SaveSystem.Load(1);

            // Assert - JsonUtility leaves absent int fields at 0, so old saves load with zeroed stats.
            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.statsEnemiesDefeated);
            Assert.AreEqual(0, loaded.statsPerfectParries);
            Assert.AreEqual(0, loaded.statsPostureBreaks);
            Assert.AreEqual(0, loaded.statsBestCombo);
            Assert.AreEqual(0, loaded.statsBestKillStreak);
            Assert.AreEqual(0, loaded.statsBestNearMissStreak);
            Assert.AreEqual(0, loaded.statsBestParryStreak);
            Assert.AreEqual(0, loaded.statsFlawlessEncounters);
        }

        [Test]
        public void SlotSummary_EmptySlot_ReturnsEmpty()
        {
            // Act
            string summary = SaveSystem.SlotSummary(1);

            // Assert
            Assert.AreEqual("Empty", summary);
        }

        [Test]
        public void SlotSummary_OnePlanetCleared_ReturnsSingular()
        {
            // Arrange
            SaveData data = new SaveData();
            data.completedPlanetScenes.Add("Planet_A");
            SaveSystem.Save(1, data);

            // Act
            string summary = SaveSystem.SlotSummary(1);

            // Assert
            Assert.AreEqual("1 planet cleared", summary);
        }

        [Test]
        public void SlotSummary_TwoPlanetsCleared_ReturnsPlural()
        {
            // Arrange
            SaveData data = new SaveData();
            data.completedPlanetScenes.Add("Planet_A");
            data.completedPlanetScenes.Add("Planet_B");
            SaveSystem.Save(1, data);

            // Act
            string summary = SaveSystem.SlotSummary(1);

            // Assert
            Assert.AreEqual("2 planets cleared", summary);
        }

        [Test]
        public void Save_OutOfRangeSlot_LogsErrorAndReturnsGracefully()
        {
            // Arrange
            SaveData data = new SaveData();

            // Act & Assert - slot 0 is out of range
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*out of range.*"));
            SaveSystem.Save(0, data);
            Assert.IsFalse(SaveSystem.SlotExists(0));

            // Act & Assert - slot > SlotCount is out of range
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*out of range.*"));
            SaveSystem.Save(SaveSystem.SlotCount + 1, data);
            Assert.IsFalse(SaveSystem.SlotExists(SaveSystem.SlotCount + 1));
        }

        [Test]
        public void Load_OutOfRangeSlot_ReturnsNull()
        {
            // Act
            SaveData loaded0 = SaveSystem.Load(0);
            SaveData loadedHigh = SaveSystem.Load(SaveSystem.SlotCount + 1);

            // Assert
            Assert.IsNull(loaded0);
            Assert.IsNull(loadedHigh);
        }

        [Test]
        public void SlotExists_OutOfRangeSlot_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(SaveSystem.SlotExists(0));
            Assert.IsFalse(SaveSystem.SlotExists(SaveSystem.SlotCount + 1));
        }

        [Test]
        public void MostRecentSlot_GetAndSet_RoundTrips()
        {
            // Act
            SaveSystem.MostRecentSlot = 2;
            int retrieved = SaveSystem.MostRecentSlot;

            // Assert
            Assert.AreEqual(2, retrieved);
        }

        [Test]
        public void MostRecentSlot_SetOutOfRange_Clamps()
        {
            // Act
            SaveSystem.MostRecentSlot = 0;           // clamps to 1
            int lowClamped = SaveSystem.MostRecentSlot;

            SaveSystem.MostRecentSlot = SaveSystem.SlotCount + 5;  // clamps to SlotCount
            int highClamped = SaveSystem.MostRecentSlot;

            // Assert
            Assert.AreEqual(1, lowClamped);
            Assert.AreEqual(SaveSystem.SlotCount, highClamped);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsStoryFlags()
        {
            // Arrange
            SaveData original = new SaveData();
            original.storyFlags.Add("guard_cleared_1");
            original.storyFlags.Add("boss_defeated");

            // Act
            SaveSystem.Save(1, original);
            SaveData loaded = SaveSystem.Load(1);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(2, loaded.storyFlags.Count);
            Assert.Contains("guard_cleared_1", loaded.storyFlags);
            Assert.Contains("boss_defeated", loaded.storyFlags);
        }

        [Test]
        public void Load_MissingStoryFlags_LoadsWithEmptyList()
        {
            // Arrange - hand-write JSON with version and lastPlanetScene, but no storyFlags
            string minimalJson = @"{
  ""version"": 1,
  ""lastPlanetScene"": ""Planet_A""
}";
            string slotPath = Path.Combine(testDirectory, "slot1.json");
            File.WriteAllText(slotPath, minimalJson);

            // Act
            SaveData loaded = SaveSystem.Load(1);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.storyFlags);
            Assert.AreEqual(0, loaded.storyFlags.Count);
        }
    }
}
