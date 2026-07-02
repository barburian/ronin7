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
                M("EP02", "Galaxy1_EP02_Docking"),
                M("EP03", "Galaxy1_EP03_Hauler"));

            var next = director.NextIncomplete(CompletedSet());

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Galaxy1_EP02_Docking", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_SkipsCompleted_ReturnsFirstUncompleted()
        {
            var director = CampaignDirector.CreateForTests(
                M("EP02", "Galaxy1_EP02_Docking"),
                M("EP03", "Galaxy1_EP03_Hauler"),
                M("EP04", "Galaxy1_EP04_JungleMoon"));

            var next = director.NextIncomplete(CompletedSet("Galaxy1_EP02_Docking"));

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Galaxy1_EP03_Hauler", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_AllCompleted_ReturnsNull()
        {
            var director = CampaignDirector.CreateForTests(
                M("EP02", "Galaxy1_EP02_Docking"),
                M("EP03", "Galaxy1_EP03_Hauler"));

            var next = director.NextIncomplete(
                CompletedSet("Galaxy1_EP02_Docking", "Galaxy1_EP03_Hauler"));

            Assert.IsNull(next);
        }

        [Test]
        public void NextIncomplete_SkipsEmptyScaffoldSlots()
        {
            // Unfilled scaffold slots (empty entryScene, reserved for later galaxies) are not launchable.
            var director = CampaignDirector.CreateForTests(
                M("EP02", "Galaxy1_EP02_Docking"),
                M("EP09", ""),
                M("EP03", "Galaxy1_EP03_Hauler"));

            var next = director.NextIncomplete(CompletedSet("Galaxy1_EP02_Docking"));

            Assert.IsTrue(next.HasValue);
            Assert.AreEqual("Galaxy1_EP03_Hauler", next.Value.entryScene);
        }

        [Test]
        public void NextIncomplete_EmptyList_ReturnsNull()
        {
            var director = CampaignDirector.CreateForTests();
            Assert.IsNull(director.NextIncomplete(CompletedSet()));
        }
    }
}
