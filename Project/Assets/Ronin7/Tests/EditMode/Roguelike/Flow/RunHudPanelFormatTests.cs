using NUnit.Framework;
using Ronin7.Core;
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
            Assert.AreEqual("HP 72/110\nNODE 6/15 · S2\nFOES 3 LEFT\nBOONS 4 · " + RunHudPanel.EchoGlyph + " 85\nNEXT: BOSS",
                RunHudPanel.Format(72, 110, 5, 1, 3, 4, 85, "NEXT: BOSS"));
        }

        [Test]
        public void FiveLines_MaxWidth18()
        {
            // Worst case per field: HP 999/999, NODE 15/15 · S3, FOES 99 LEFT, BOONS 99 · ◈ 9999, three paths.
            string next = RunHudPanel.FormatNext(new[] { RoomKind.Treasure, RoomKind.Event, RoomKind.Shop });
            string s = RunHudPanel.Format(999, 999, 14, 2, 99, 99, 9999, next);
            var lines = s.Split('\n');
            Assert.AreEqual(5, lines.Length);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 18, l);
            foreach (RoomKind k in System.Enum.GetValues(typeof(RoomKind)))
                Assert.LessOrEqual(RunHudPanel.FormatNext(new[] { k }).Length, 18, k.ToString());
        }

        [Test]
        public void FormatNext_OneNamesIt_ManyUseCodes_NoneIsExit()
        {
            Assert.AreEqual("NEXT: BOSS", RunHudPanel.FormatNext(new[] { RoomKind.Boss }));
            Assert.AreEqual("NEXT: ELT/EVT/SHP", RunHudPanel.FormatNext(new[] { RoomKind.Elite, RoomKind.Event, RoomKind.Shop }));
            Assert.AreEqual("NEXT: EXIT", RunHudPanel.FormatNext(new RoomKind[0]));
        }
    }
}
