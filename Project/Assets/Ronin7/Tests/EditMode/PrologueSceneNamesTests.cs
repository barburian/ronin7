using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the chapter-restructure save migration (v1→v2): completed planet-scene names must
    /// rename to their ship-prologue entries so legacy saves keep their campaign progress, while
    /// every non-chapter scene name passes through untouched.
    /// </summary>
    public class PrologueSceneNamesTests
    {
        [TestCase("Ch02_Auction", "Ch02_Prologue")]
        [TestCase("Ch06_IronDojo", "Ch06_Prologue")]
        [TestCase("Ch13_SterileReckoning", "Ch13_Prologue")]
        [TestCase("Ch16_ThroneOfAshes", "Ch16_Prologue")]
        public void ChapterPlanetScenes_RenameToPrologueEntries(string oldName, string expected)
        {
            Assert.AreEqual(expected, PrologueSceneNames.Rename(oldName));
        }

        [TestCase("Ch02_Prologue")]
        [TestCase("Galaxy1_Ch1_Hub")]
        [TestCase("Phase6_Boot")]
        [TestCase("ParkourGrounds")]
        [TestCase("")]
        [TestCase("Chapter_NotANumber")]
        public void NonChapterAndAlreadyMigratedNames_PassThroughUnchanged(string name)
        {
            Assert.AreEqual(name, PrologueSceneNames.Rename(name));
        }

        [Test]
        public void EveryCampaignChapterId_HasAWellFormedPrologueName()
        {
            // The rule must produce ChNN_Prologue for every shipped chapter number (02..13, 16).
            foreach (var n in new[] { "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "16" })
            {
                Assert.AreEqual($"Ch{n}_Prologue", PrologueSceneNames.Rename($"Ch{n}_Whatever"));
            }
        }
    }
}
