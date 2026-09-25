using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Coverage for <see cref="RunHudPanel.Format"/>, mirroring CampaignStatsDisplayFormatTests'
    /// pure-formatter style — no scene/MonoBehaviour needed.</summary>
    public class RunHudPanelFormatTests
    {
        [Test]
        public void Format_FirstNodeFirstSectorNoBoonsNoEchoes()
        {
            Assert.AreEqual("NODE 1/15 / SECTOR 1 / BOONS 0 / ECHOES 0", RunHudPanel.Format(0, 0, 0, 0));
        }

        [Test]
        public void Format_MidRun_MatchesExpectedLayout()
        {
            Assert.AreEqual("NODE 8/15 / SECTOR 2 / BOONS 3 / ECHOES 240", RunHudPanel.Format(7, 1, 3, 240));
        }

        [Test]
        public void Format_FinalNode()
        {
            Assert.AreEqual("NODE 15/15 / SECTOR 3 / BOONS 5 / ECHOES 900", RunHudPanel.Format(14, 2, 5, 900));
        }
    }
}
