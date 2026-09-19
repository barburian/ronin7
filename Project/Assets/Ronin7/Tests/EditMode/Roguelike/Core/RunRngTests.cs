using System;
using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    public class RunRngTests
    {
        [Test]
        public void NextUInt_SameSeed_ProducesIdenticalSequence()
        {
            RunRng a = new RunRng(12345);
            RunRng b = new RunRng(12345);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), $"sequence diverged at step {i}");
            }
        }

        [Test]
        public void NextUInt_DifferentSeeds_ProduceDifferentSequences()
        {
            RunRng a = new RunRng(1);
            RunRng b = new RunRng(2);

            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
            {
                if (a.NextUInt() != b.NextUInt()) anyDifferent = true;
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void Constructor_SeedZero_IsRemappedAndDoesNotStickAtZero()
        {
            RunRng rng = new RunRng(0);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreNotEqual(0u, rng.NextUInt());
            }
        }

        [Test]
        public void NextInt_MaxExclusiveZeroOrNegative_ReturnsZero()
        {
            RunRng rng = new RunRng(7);

            Assert.AreEqual(0, rng.NextInt(0));
            Assert.AreEqual(0, rng.NextInt(-5));
        }

        [Test]
        public void NextInt_MaxExclusive_StaysInRange()
        {
            RunRng rng = new RunRng(99);

            for (int i = 0; i < 500; i++)
            {
                int value = rng.NextInt(10);
                Assert.GreaterOrEqual(value, 0);
                Assert.Less(value, 10);
            }
        }

        [Test]
        public void NextInt_MinMax_StaysInRange()
        {
            RunRng rng = new RunRng(321);

            for (int i = 0; i < 500; i++)
            {
                int value = rng.NextInt(5, 15);
                Assert.GreaterOrEqual(value, 5);
                Assert.Less(value, 15);
            }
        }

        [Test]
        public void NextInt_MaxNotGreaterThanMin_ReturnsMin()
        {
            RunRng rng = new RunRng(321);

            Assert.AreEqual(5, rng.NextInt(5, 5));
            Assert.AreEqual(5, rng.NextInt(5, 2));
        }

        [Test]
        public void NextFloat_StaysInZeroOneRange()
        {
            RunRng rng = new RunRng(55);

            for (int i = 0; i < 500; i++)
            {
                float value = rng.NextFloat();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void Chance_ZeroProbability_NeverTrue()
        {
            RunRng rng = new RunRng(8);

            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(rng.Chance(0f));
            }
        }

        [Test]
        public void Chance_OneProbability_AlwaysTrue()
        {
            RunRng rng = new RunRng(8);

            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(rng.Chance(1f));
            }
        }

        [Test]
        public void PickWeighted_EmptySpan_ReturnsMinusOne()
        {
            RunRng rng = new RunRng(1);

            Assert.AreEqual(-1, rng.PickWeighted(ReadOnlySpan<int>.Empty));
        }

        [Test]
        public void PickWeighted_NullSpan_ReturnsMinusOne()
        {
            RunRng rng = new RunRng(1);

            Assert.AreEqual(-1, rng.PickWeighted(null));
        }

        [Test]
        public void PickWeighted_AllZeroWeights_ReturnsMinusOne()
        {
            RunRng rng = new RunRng(1);
            Span<int> weights = stackalloc int[] { 0, 0, 0 };

            Assert.AreEqual(-1, rng.PickWeighted(weights));
        }

        [Test]
        public void PickWeighted_AllNegativeWeights_ReturnsMinusOne()
        {
            RunRng rng = new RunRng(1);
            Span<int> weights = stackalloc int[] { -1, -2 };

            Assert.AreEqual(-1, rng.PickWeighted(weights));
        }

        [Test]
        public void PickWeighted_SingleZeroWeight_NeverPicksIt()
        {
            RunRng rng = new RunRng(42);
            Span<int> weights = stackalloc int[] { 10, 0, 10 };

            for (int i = 0; i < 200; i++)
            {
                int pick = rng.PickWeighted(weights);
                Assert.AreNotEqual(1, pick);
                Assert.IsTrue(pick == 0 || pick == 2);
            }
        }

        [Test]
        public void PickWeighted_OnlyOnePositiveWeight_AlwaysPicksIt()
        {
            RunRng rng = new RunRng(3);
            Span<int> weights = stackalloc int[] { 0, 5, 0 };

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(1, rng.PickWeighted(weights));
            }
        }

        [Test]
        public void ForNode_SameRunSeedAndNodeIndex_ProducesIdenticalSequence()
        {
            RunRng a = RunRng.ForNode(999, 4);
            RunRng b = RunRng.ForNode(999, 4);

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void ForNode_DifferentNodeIndices_ProduceDifferentSubstreams()
        {
            RunRng a = RunRng.ForNode(999, 0);
            RunRng b = RunRng.ForNode(999, 1);

            Assert.AreNotEqual(a.State, b.State);
        }
    }
}
