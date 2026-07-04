using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    public class CampaignStatsDisplayFormatTests
    {
        [Test]
        public void Format_AllZero()
        {
            string text = CampaignStatsDisplay.Format(0, 0, 0, 0, 0, 0, 0);

            Assert.AreEqual("DEFEATED 0 / PERFECT PARRIES 0 / GUARD BREAKS 0 / BEST COMBO 0 / " +
                "BEST KILL STREAK 0 / BEST NEAR MISS STREAK 0 / BEST PARRY STREAK 0", text);
        }

        [Test]
        public void Format_MatchesExpectedLayout()
        {
            string text = CampaignStatsDisplay.Format(47, 12, 3, 4, 6, 7, 8);

            Assert.AreEqual("DEFEATED 47 / PERFECT PARRIES 12 / GUARD BREAKS 3 / BEST COMBO 4 / " +
                "BEST KILL STREAK 6 / BEST NEAR MISS STREAK 7 / BEST PARRY STREAK 8", text);
        }
    }
}
