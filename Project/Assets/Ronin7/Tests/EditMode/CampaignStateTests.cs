using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class CampaignStateTests
    {
        private bool savedFirstPlanetDeparted;
        private int savedShipHullIndex;

        [SetUp]
        public void SetUp()
        {
            // Save current state of Galaxy1Progress and ShipSelection
            savedFirstPlanetDeparted = Galaxy1Progress.FirstPlanetDeparted;
            savedShipHullIndex = ShipSelection.SelectedHullIndex;

            // Reset campaign state to clean slate
            CampaignState.Reset();
            CampaignStats.Reset();
            DrillBestScores.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            // Reset state again for cleanliness
            CampaignState.Reset();
            CampaignStats.Reset();
            DrillBestScores.Reset();

            // Restore Galaxy1Progress and ShipSelection
            Galaxy1Progress.FirstPlanetDeparted = savedFirstPlanetDeparted;
            ShipSelection.SelectedHullIndex = savedShipHullIndex;
        }

        [Test]
        public void NoteLanding_FromSpace_SetsLastPlanetScene()
        {
            // Act
            CampaignState.NoteLanding("PlanetA", fromSpace: true);

            // Assert
            Assert.AreEqual("PlanetA", CampaignState.LastPlanetScene);
        }

        [Test]
        public void NoteLanding_InteriorHop_DoesNotChangeLastPlanetScene()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);

            // Act
            CampaignState.NoteLanding("Market", fromSpace: false);

            // Assert
            Assert.AreEqual("PlanetA", CampaignState.LastPlanetScene);
        }

        [Test]
        public void NoteLanding_EmptySceneFromSpace_DoesNotUpdateLastPlanetScene()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);

            // Act
            CampaignState.NoteLanding("", fromSpace: true);

            // Assert
            Assert.AreEqual("PlanetA", CampaignState.LastPlanetScene);
        }

        [Test]
        public void NoteLanding_EmptySceneNotFromSpace_DoesNotUpdateLastPlanetScene()
        {
            // Arrange - empty scene with fromSpace:false on fresh state
            // Act
            CampaignState.NoteLanding("", fromSpace: false);

            // Assert
            Assert.AreEqual("", CampaignState.LastPlanetScene);
        }

        [Test]
        public void NoteZoneCompleted_AfterLandingFromSpace_ReturnsAndCompletesPlanet()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);

            // Act
            string cleared = CampaignState.NoteZoneCompleted("Corsair");
            bool isCompleted = CampaignState.IsCompleted("PlanetA");

            // Assert
            Assert.AreEqual("PlanetA", cleared);
            Assert.IsTrue(isCompleted);
        }

        [Test]
        public void NoteZoneCompleted_OnCorsair_ReturnsNullAndCompletesNothing()
        {
            // Arrange
            CampaignState.NoteLanding("Corsair", fromSpace: true);

            // Act
            string cleared = CampaignState.NoteZoneCompleted("Corsair");
            bool corsairCompleted = CampaignState.IsCompleted("Corsair");

            // Assert
            Assert.IsNull(cleared);
            Assert.IsFalse(corsairCompleted);
        }

        [Test]
        public void NoteZoneCompleted_EmptyLastPlanetScene_ReturnsNull()
        {
            // Arrange - LastPlanetScene is empty by default

            // Act
            string cleared = CampaignState.NoteZoneCompleted("Corsair");

            // Assert
            Assert.IsNull(cleared);
        }

        [Test]
        public void ToSaveData_AndApplyFrom_RoundTripsState()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);
            CampaignState.NoteZoneCompleted("Corsair");
            CampaignState.NoteLanding("PlanetB", fromSpace: true);
            CampaignState.NoteZoneCompleted("Hideout");
            Galaxy1Progress.FirstPlanetDeparted = true;
            ShipSelection.SelectedHullIndex = 3;

            // Act
            SaveData save = CampaignState.ToSaveData();
            CampaignState.Reset();
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.IsTrue(CampaignState.IsCompleted("PlanetA"));
            Assert.IsTrue(CampaignState.IsCompleted("PlanetB"));
            Assert.AreEqual("PlanetB", CampaignState.LastPlanetScene);
            Assert.IsTrue(Galaxy1Progress.FirstPlanetDeparted);
            Assert.AreEqual(3, ShipSelection.SelectedHullIndex);
        }

        [Test]
        public void ApplyFrom_Null_DoesNotThrowAndChangesNothing()
        {
            // Arrange
            CampaignState.NoteLanding("PlanetA", fromSpace: true);
            string originalScene = CampaignState.LastPlanetScene;

            // Act & Assert
            Assert.DoesNotThrow(() => CampaignState.ApplyFrom(null));
            Assert.AreEqual(originalScene, CampaignState.LastPlanetScene);
        }

        [Test]
        public void SetFlag_SetsTheFlag()
        {
            // Act
            CampaignState.SetFlag("guard_encounter_cleared");

            // Assert
            Assert.IsTrue(CampaignState.HasFlag("guard_encounter_cleared"));
        }

        [Test]
        public void SetFlag_Null_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => CampaignState.SetFlag(null));
            Assert.IsFalse(CampaignState.HasFlag(null));
        }

        [Test]
        public void SetFlag_EmptyString_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => CampaignState.SetFlag(""));
            Assert.IsFalse(CampaignState.HasFlag(""));
        }

        [Test]
        public void HasFlag_UnsetFlag_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasFlag("nonexistent_flag"));
        }

        [Test]
        public void HasFlag_Null_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasFlag(null));
        }

        [Test]
        public void HasFlag_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasFlag(""));
        }

        [Test]
        public void ToSaveData_AndApplyFrom_RoundTripsFlags()
        {
            // Arrange
            CampaignState.SetFlag("flag_a");
            CampaignState.SetFlag("flag_b");

            // Act
            SaveData save = CampaignState.ToSaveData();
            CampaignState.Reset();
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.IsTrue(CampaignState.HasFlag("flag_a"));
            Assert.IsTrue(CampaignState.HasFlag("flag_b"));
            // Verify sorted order in SaveData
            Assert.AreEqual(2, save.storyFlags.Count);
            Assert.AreEqual("flag_a", save.storyFlags[0]);
            Assert.AreEqual("flag_b", save.storyFlags[1]);
        }

        [Test]
        public void ApplyFrom_NullStoryFlags_HandlesGracefully()
        {
            // Arrange
            SaveData save = new SaveData();
            save.storyFlags = null;

            // Act & Assert
            Assert.DoesNotThrow(() => CampaignState.ApplyFrom(save));
            Assert.IsFalse(CampaignState.HasFlag("any_flag"));
        }

        [Test]
        public void ApplyFrom_EmptyStringFlagsIgnored()
        {
            // Arrange
            SaveData save = new SaveData();
            save.storyFlags.Add("");
            save.storyFlags.Add("valid_flag");

            // Act
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.IsTrue(CampaignState.HasFlag("valid_flag"));
            Assert.IsFalse(CampaignState.HasFlag(""));
        }

        [Test]
        public void GalaxiesCompleted_NoFlags_IsZero()
        {
            Assert.AreEqual(0, CampaignState.GalaxiesCompleted);
        }

        [Test]
        public void GalaxiesCompleted_CountsGalaxyCompleteFlags()
        {
            // Arrange
            CampaignState.SetFlag("galaxy1_complete");
            CampaignState.SetFlag("galaxy2_complete");
            CampaignState.SetFlag("unrelated_flag");

            // Assert
            Assert.AreEqual(2, CampaignState.GalaxiesCompleted);
        }

        [Test]
        public void GalaxiesCompleted_SurvivesSaveRoundTrip()
        {
            // Arrange
            CampaignState.SetFlag("galaxy1_complete");

            // Act
            SaveData save = CampaignState.ToSaveData();
            CampaignState.Reset();
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.AreEqual(1, CampaignState.GalaxiesCompleted);
        }

        [Test]
        public void ToSaveData_AndApplyFrom_RoundTripsStats()
        {
            // Arrange
            CampaignStats.RecordEnemyDefeated();
            CampaignStats.RecordEnemyDefeated();
            CampaignStats.RecordPerfectParry();
            CampaignStats.RecordPostureBreak();
            CampaignStats.RecordCombo(3);
            CampaignStats.RecordKillStreak(4);
            CampaignStats.RecordNearMissStreak(5);

            // Act
            SaveData save = CampaignState.ToSaveData();
            CampaignStats.Reset();
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.AreEqual(2, CampaignStats.EnemiesDefeated);
            Assert.AreEqual(1, CampaignStats.PerfectParries);
            Assert.AreEqual(1, CampaignStats.PostureBreaks);
            Assert.AreEqual(3, CampaignStats.BestCombo);
            Assert.AreEqual(4, CampaignStats.BestKillStreak);
            Assert.AreEqual(5, CampaignStats.BestNearMissStreak);
        }

        [Test]
        public void ToSaveData_AndApplyFrom_RoundTripsDrillScores()
        {
            // Arrange
            DrillBestScores.RecordIfBetter("hub_dojo", 180);

            // Act
            SaveData save = CampaignState.ToSaveData();
            DrillBestScores.Reset();
            CampaignState.ApplyFrom(save);

            // Assert
            Assert.AreEqual(180, DrillBestScores.BestFor("hub_dojo"));
        }

        [Test]
        public void Reset_ClearsStoryFlags()
        {
            // Arrange
            CampaignState.SetFlag("test_flag");

            // Act
            CampaignState.Reset();

            // Assert
            Assert.IsFalse(CampaignState.HasFlag("test_flag"));
        }
    }
}
