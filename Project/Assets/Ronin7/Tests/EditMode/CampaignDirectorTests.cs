using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="CampaignDirector.NextIncomplete"/>, the pure ordering logic the ship hub uses
    /// to pick the next mission. Completion is supplied as a predicate so these tests never touch
    /// CampaignState or author an asset.
    /// </summary>
    public class CampaignDirectorTests
    {
        private static CampaignDirector.Mission M(string id, string entryScene) =>
            new CampaignDirector.Mission { id = id, entryScene = entryScene };

        private static System.Func<string, bool> CompletedSet(params string[] scenes)
        {
            var set = new HashSet<string>(scenes);
            return set.Contains;
        }

        [Test]
        public void NextIncomplete_NothingCompleted_ReturnsFirst()
        {
            var director = CampaignDirector.CreateForTests(
                M("Ch02", "Ch02_Prologue"),
                M("Ch03", "Ch03_Prologue"));

            var next = director.NextIncomplete(CompletedSet());

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Ch02_Prologue", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_SkipsCompleted_ReturnsFirstUncompleted()
        {
            var director = CampaignDirector.CreateForTests(
                M("Ch02", "Ch02_Prologue"),
                M("Ch03", "Ch03_Prologue"),
                M("Ch04", "Ch04_Prologue"));

            var next = director.NextIncomplete(CompletedSet("Ch02_Prologue"));

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Ch03_Prologue", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_AllCompleted_ReturnsNull()
        {
            var director = CampaignDirector.CreateForTests(
                M("Ch02", "Ch02_Prologue"),
                M("Ch03", "Ch03_Prologue"));

            var next = director.NextIncomplete(
                CompletedSet("Ch02_Prologue", "Ch03_Prologue"));

            Assert.IsNull(next);
        }

        [Test]
        public void NextIncomplete_SkipsEmptyScaffoldSlots()
        {
            // Unfilled scaffold slots (empty entryScene, reserved for later content) are not launchable.
            var director = CampaignDirector.CreateForTests(
                M("Ch02", "Ch02_Prologue"),
                M("Ch14", ""),
                M("Ch03", "Ch03_Prologue"));

            var next = director.NextIncomplete(CompletedSet("Ch02_Prologue"));

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Ch03_Prologue", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_EmptyList_ReturnsNull()
        {
            var director = CampaignDirector.CreateForTests();
            Assert.IsNull(director.NextIncomplete(CompletedSet()));
        }
    }
}
