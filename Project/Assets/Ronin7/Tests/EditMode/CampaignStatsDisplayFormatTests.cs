using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    public class CampaignStatsDisplayFormatTests
    {
        [Test]
        public void Format_AllZero()
        {
            string text = CampaignStatsDisplay.Format(0, 0, 0, 0);

            Assert.AreEqual("DEFEATED 0 / PERFECT PARRIES 0 / GUARD BREAKS 0 / BEST COMBO 0", text);
        }

        [Test]
        public void Format_MatchesExpectedLayout()
        {
            string text = CampaignStatsDisplay.Format(47, 12, 3, 4);

            Assert.AreEqual("DEFEATED 47 / PERFECT PARRIES 12 / GUARD BREAKS 3 / BEST COMBO 4", text);
        }
    }
}
