using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Coverage for <see cref="RunHudPanel.Format"/> (P1-13: four short lines, ≤18 chars each,
    /// so the wrist HUD stays readable at arm reach).</summary>
    public class RunHudPanelFormatTests
    {
        [Test]
        public void Format_MatchesSpecLayout()
        {
            Assert.AreEqual("HP 72/110\nNODE 6/15 · S2\nFOES 3 LEFT\nBOONS 4 · " + RunHudPanel.EchoGlyph + " 85",
                RunHudPanel.Format(72, 110, 5, 1, 3, 4, 85));
        }

        [Test]
        public void FourLines_MaxWidth18()
        {
            // Worst case per field: HP 999/999, NODE 15/15 · S3, FOES 99 LEFT, BOONS 99 · ◈ 9999.
            string s = RunHudPanel.Format(999, 999, 14, 2, 99, 99, 9999);
            var lines = s.Split('\n');
            Assert.AreEqual(4, lines.Length);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 18, l);
        }
    }
}
