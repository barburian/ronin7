using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class GossipSelectorTests
    {
        [Test]
        public void SelectIndex_ReturnsFirstSatisfiedFlag_PriorityOrder()
        {
            var flags = new[] { "galaxy2_complete", "galaxy1_complete" };
            var set = new HashSet<string> { "galaxy1_complete", "galaxy2_complete" };

            int index = GossipSelector.SelectIndex(flags, set.Contains);

            Assert.AreEqual(0, index);
        }

        [Test]
        public void SelectIndex_SkipsUnsatisfiedFlags_ReturnsFirstSatisfied()
        {
            var flags = new[] { "galaxy2_complete", "galaxy1_complete" };
            var set = new HashSet<string> { "galaxy1_complete" };

            int index = GossipSelector.SelectIndex(flags, set.Contains);

            Assert.AreEqual(1, index);
        }

        [Test]
        public void SelectIndex_NullOrEmptyFlag_IsUniversalFallback()
        {
            var flags = new[] { "galaxy1_complete", "" };
            var set = new HashSet<string>();

            int index = GossipSelector.SelectIndex(flags, set.Contains);

            Assert.AreEqual(1, index);
        }

        [Test]
        public void SelectIndex_NoMatch_ReturnsNegativeOne()
        {
            var flags = new[] { "galaxy1_complete", "galaxy2_complete" };
            var set = new HashSet<string>();

            int index = GossipSelector.SelectIndex(flags, set.Contains);

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void SelectIndex_EmptyArray_ReturnsNegativeOne()
        {
            int index = GossipSelector.SelectIndex(new string[0], _ => true);

            Assert.AreEqual(-1, index);
        }

        [Test]
        public void SelectIndex_NullArray_ReturnsNegativeOne()
        {
            int index = GossipSelector.SelectIndex(null, _ => true);

            Assert.AreEqual(-1, index);
        }
    }
}
