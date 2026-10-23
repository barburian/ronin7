using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Editor
{
    /// <summary>P5-00 budget table (Roguelike-Roadmap.md) and the render-check image stats. Pure.</summary>
    internal static class PerfBudget
    {
        public struct Limits { public long Draws, SetPass, Triangles; public int Particles; }

        public struct Sample
        {
            public long Draws, SetPass, Triangles, GcBytesPerFrame, GcMaxBytes;
            public int Particles;
            public float TextureMb, MeanLuminance, MagentaRatio;
        }

        public static readonly Limits Quest3 = new Limits { Draws = 175, SetPass = 50, Triangles = 800_000, Particles = 200 };
        public static readonly Limits Pcvr = new Limits { Draws = 600, SetPass = 150, Triangles = 3_000_000, Particles = 800 };

        /// <summary>Render check: a frame darker than this reads as "all black" on device.</summary>
        public const float MinLuminance = 0.03f;
        /// <summary>Render check: more magenta than this means a missing/broken shader.</summary>
        public const float MaxMagentaRatio = 0.001f;

        public static List<string> Violations(Sample s, Limits l)
        {
            var v = new List<string>();
            if (s.Draws > l.Draws) v.Add($"draws {s.Draws}>{l.Draws}");
            if (s.SetPass > l.SetPass) v.Add($"SetPass {s.SetPass}>{l.SetPass}");
            if (s.Triangles > l.Triangles) v.Add($"tris {s.Triangles}>{l.Triangles}");
            if (s.Particles > l.Particles) v.Add($"particles {s.Particles}>{l.Particles}");
            if (s.MeanLuminance < MinLuminance) v.Add($"luma {s.MeanLuminance:0.000}<{MinLuminance}");
            if (s.MagentaRatio > MaxMagentaRatio) v.Add($"magenta {s.MagentaRatio:0.0000}>{MaxMagentaRatio}");
            return v;
        }

        public static string Verdict(Sample s, Limits l)
        {
            var v = Violations(s, l);
            return v.Count == 0 ? "PASS" : "FAIL: " + string.Join(", ", v);
        }

        /// <summary>Rec.709 mean luminance (0..1) and the fraction of pure-magenta pixels.</summary>
        public static void ImageStats(Color32[] px, out float meanLuminance, out float magentaRatio)
        {
            meanLuminance = magentaRatio = 0f;
            if (px == null || px.Length == 0) return;
            double sum = 0;
            int magenta = 0;
            foreach (var c in px)
            {
                sum += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0;
                if (c.r > 200 && c.b > 200 && c.g < 60) magenta++;
            }
            meanLuminance = (float)(sum / px.Length);
            magentaRatio = (float)magenta / px.Length;
        }
    }
}
