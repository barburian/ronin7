using NUnit.Framework;
using Ronin7.Editor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class PerfBudgetTests
    {
        private static PerfBudget.Sample Ok() => new PerfBudget.Sample
            { Draws = 100, SetPass = 30, Triangles = 100_000, Particles = 50, MeanLuminance = 0.2f };

        [Test]
        public void Verdict_InsideQuest3_Passes() => Assert.AreEqual("PASS", PerfBudget.Verdict(Ok(), PerfBudget.Quest3));

        [Test]
        public void Violations_EachLimitOver_IsReported()
        {
            var s = Ok();
            s.Draws = 176; s.SetPass = 51; s.Triangles = 800_001; s.Particles = 201; s.MeanLuminance = 0.01f; s.MagentaRatio = 0.5f;
            Assert.AreEqual(6, PerfBudget.Violations(s, PerfBudget.Quest3).Count);
        }

        [Test]
        public void ImageStats_BlackAndMagenta()
        {
            var px = new[] { new Color32(0, 0, 0, 255), new Color32(255, 0, 255, 255) };
            PerfBudget.ImageStats(px, out float luma, out float magenta);
            Assert.AreEqual(0.5f, magenta, 1e-6f);
            Assert.AreEqual((0.2126f + 0.0722f) / 2f, luma, 1e-4f);
        }

        [Test]
        public void ImageStats_White_IsFullLuminance()
        {
            PerfBudget.ImageStats(new[] { new Color32(255, 255, 255, 255) }, out float luma, out float magenta);
            Assert.AreEqual(1f, luma, 1e-4f);
            Assert.AreEqual(0f, magenta);
        }
    }
}
