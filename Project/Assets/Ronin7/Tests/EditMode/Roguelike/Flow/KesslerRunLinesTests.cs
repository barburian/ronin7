using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P6-04: Kessler run-end line bank, boss intro/phase lines, outcome keying and the no-repeat rule.</summary>
    public class KesslerRunLinesTests
    {
        private static readonly KesslerOutcome[] All =
            (KesslerOutcome[])System.Enum.GetValues(typeof(KesslerOutcome));

        [Test]
        public void EveryOutcome_HasThreeOrMoreNonEmptyDistinctVariants()
        {
            foreach (var o in All)
            {
                var lines = KesslerRunLines.Variants(o);
                Assert.GreaterOrEqual(lines.Length, 3, o.ToString());
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var l in lines)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(l.Text), o.ToString());
                    Assert.AreEqual("Kessler", l.Speaker);
                    Assert.IsTrue(seen.Add(l.Clip), "duplicate clip " + l.Clip);
                }
            }
        }

        [Test]
        public void EveryBoss_HasIntro_AndAtLeastOnePhaseLine_MaelgornTwo()
        {
            for (int s = 0; s < 3; s++)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(KesslerRunLines.BossIntro(s).Text), "intro " + s);
                Assert.IsNotNull(KesslerRunLines.BossPhase(s, 1), "phase 1 of sector " + s);
            }
            Assert.IsNotNull(KesslerRunLines.BossPhase(2, 2));
            Assert.IsNull(KesslerRunLines.BossPhase(0, 2));
            Assert.IsNull(KesslerRunLines.BossPhase(1, 0));
            Assert.IsNull(KesslerRunLines.BossPhase(5, 1));
        }

        [Test]
        public void AllClipNames_AreUnique()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var l in KesslerRunLines.AllLines()) Assert.IsTrue(seen.Add(l.Clip), l.Clip);
        }

        [TestCase(true, 14, false, 2, KesslerOutcome.WonHeat)]
        [TestCase(true, 14, true, 0, KesslerOutcome.Won)]
        [TestCase(false, 3, true, 0, KesslerOutcome.FirstRun)]
        [TestCase(false, 3, false, 0, KesslerOutcome.DiedRust)]
        [TestCase(false, 4, false, 3, KesslerOutcome.DiedRust)]
        [TestCase(false, 5, false, 0, KesslerOutcome.DiedProgram)]
        [TestCase(false, 9, false, 0, KesslerOutcome.DiedProgram)]
        [TestCase(false, 10, false, 0, KesslerOutcome.DiedGarden)]
        [TestCase(false, 99, false, 0, KesslerOutcome.DiedGarden)]
        [TestCase(false, -2, false, 0, KesslerOutcome.DiedRust)]
        public void Outcome_KeysBySectorWinAndHeat(bool won, int depth, bool first, int heat, KesslerOutcome expected)
        {
            Assert.AreEqual(expected, KesslerRunLines.Outcome(won, depth, first, heat));
        }

        [Test]
        public void NextVariant_NeverRepeatsLast_AndStaysInRange()
        {
            for (int count = 2; count <= 5; count++)
                for (int last = -1; last < count; last++)
                    for (int r = 0; r < 40; r++)
                    {
                        int v = KesslerRunLines.NextVariant(count, last, r);
                        Assert.That(v, Is.InRange(0, count - 1));
                        if (last >= 0) Assert.AreNotEqual(last, v);
                    }
        }

        [Test]
        public void NextVariant_CoversEveryOtherVariant()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int r = 0; r < 20; r++) seen.Add(KesslerRunLines.NextVariant(4, 2, r));
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3 }, seen);
        }

        [Test]
        public void NextVariant_SingleVariant_ReturnsZero() =>
            Assert.AreEqual(0, KesslerRunLines.NextVariant(1, 0, 7));
    }
}
