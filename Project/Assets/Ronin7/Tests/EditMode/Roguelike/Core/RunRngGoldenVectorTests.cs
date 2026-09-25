using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>
    /// Pins the EXACT RNG sequence and map layout for known seeds.
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

        [Test]
        public void Generate_Seed2026_MatchesGoldenLayout()
        {
            RoomKind[] expected =
            {
                RoomKind.Combat, RoomKind.Combat,   RoomKind.Elite,  RoomKind.Forge, RoomKind.Boss,
                RoomKind.Combat, RoomKind.Treasure, RoomKind.Combat, RoomKind.Forge, RoomKind.Boss,
                RoomKind.Combat, RoomKind.Elite,    RoomKind.Combat, RoomKind.Forge, RoomKind.Boss,
            };

            AssertLayout(2026u, expected);
        }

        [Test]
        public void Generate_Seed1_MatchesGoldenLayout()
        {
            RoomKind[] expected =
            {
                RoomKind.Combat, RoomKind.Elite,  RoomKind.Treasure, RoomKind.Forge, RoomKind.Boss,
                RoomKind.Combat, RoomKind.Elite,  RoomKind.Treasure, RoomKind.Forge, RoomKind.Boss,
                RoomKind.Combat, RoomKind.Combat, RoomKind.Combat,   RoomKind.Forge, RoomKind.Boss,
            };

            AssertLayout(1u, expected);
        }

        private static void AssertLayout(uint seed, RoomKind[] expected)
        {
            RunNode[] nodes = RunMapGenerator.Generate(seed);

            Assert.AreEqual(RunMapGenerator.TotalNodes, nodes.Length, "map length changed");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], nodes[i].Kind,
                    $"seed {seed} node {i} changed from {expected[i]} to {nodes[i].Kind}");
            }
        }
    }
}
