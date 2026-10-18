using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>
    /// Pins the EXACT RNG sequence for known seeds (map goldens: RunMapGraphTests).
    ///
    /// Every other determinism test in this suite compares two calls made in the same process, so it
    /// passes trivially and would KEEP passing if someone reordered the xorshift steps, changed the
    /// zero-seed remap constant, altered a room weight, or swapped the roll order in the map
    /// generator. Each of those silently changes what every existing seed produces, which breaks the
    /// replayable-run and shared-seed guarantees the mode is sold on (design doc §1, A2.5).
    ///
    /// These vectors were produced by two independent simulations of the exact bit operations and
    /// agreed. If one of these fails, the RNG changed: that is a deliberate decision to make, not a
    /// test to "fix" by pasting in the new numbers — every previously shared seed becomes a different
    /// run the moment you do.
    /// </summary>
    public class RunRngGoldenVectorTests
    {
        [Test]
        public void NextUInt_Seed1_MatchesGoldenVector()
        {
            RunRng rng = new RunRng(1);
            uint[] expected = { 270369u, 67634689u, 2647435461u, 307599695u, 2398689233u };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt(), $"RNG sequence changed at draw {i}");
            }
        }

        /// <summary>Also pins the zero-seed remap: xorshift32 is undefined at state 0, so seed 0 is
        /// remapped to a fixed nonzero constant. Changing that constant changes seed 0's whole run.</summary>
        [Test]
        public void NextUInt_SeedZero_MatchesGoldenVector()
        {
            RunRng rng = new RunRng(0);
            uint[] expected = { 1359758873u, 3761132862u, 2075758394u };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt(), $"zero-seed remap changed at draw {i}");
            }
        }

        /// <summary>Pins <see cref="RunRng.ForNode"/>. New golden (P4-11, RunSnapshot v3): the BUG-35 murmur3 fmix32
        /// seed mix deliberately changed every per-node stream (events, shop stock, offers, arena, affixes).</summary>
        [Test]
        public void ForNode_Seed2026Node5_MatchesGoldenVector()
        {
            RunRng rng = RunRng.ForNode(2026u, 5);
            uint[] expected = { 2833071292u, 3860715577u, 4265607220u };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt(), $"ForNode stream changed at draw {i}");
            }
        }

        /// <summary>BUG-35: before the seed mix, seeds 1..64 at one node all drew the same first coin flip (0/64 under 0.5).</summary>
        [Test]
        public void ForNode_LowSeeds_VaryFirstDraw()
        {
            int hits = 0;
            for (uint seed = 1; seed <= 64; seed++)
                if (RunRng.ForNode(seed, 3).NextFloat() < 0.5f) hits++;
            Assert.That(hits, Is.InRange(16, 48), $"first draw not spread across low seeds ({hits}/64 under 0.5)");
        }

        // Map-layout goldens moved to RunMapGraphTests (P2-04 branching map).
    }
}
