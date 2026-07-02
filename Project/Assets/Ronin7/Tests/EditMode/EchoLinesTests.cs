using NUnit.Framework;
using Ronin7.World.Story;

namespace Ronin7.Tests.EditMode
{
    public class EchoLinesTests
    {
        [Test]
        public void PoolsFor_PreCh3_AllPoolsEmpty()
        {
            var pools = EchoLines.PoolsFor(f => false);

            Assert.AreEqual(0, pools[EchoLines.EnemyKilled].Length);
            Assert.AreEqual(0, pools[EchoLines.PlayerHurt].Length);
            Assert.AreEqual(0, pools[EchoLines.AbilityActivated].Length);
            Assert.AreEqual(0, pools[EchoLines.IdleHint].Length);
        }

        [Test]
        public void PoolsFor_PostCh3_PoolsNonEmpty()
        {
            var pools = EchoLines.PoolsFor(f => f == "ch3_complete");

            Assert.Greater(pools[EchoLines.EnemyKilled].Length, 0);
            Assert.Greater(pools[EchoLines.PlayerHurt].Length, 0);
            Assert.Greater(pools[EchoLines.AbilityActivated].Length, 0);
            Assert.Greater(pools[EchoLines.IdleHint].Length, 0);
        }

        [Test]
        public void PoolsFor_PostCh9_SupersetsPostCh3()
        {
            var post3 = EchoLines.PoolsFor(f => f == "ch3_complete");
            var post9 = EchoLines.PoolsFor(f => true);

            foreach (var kind in new[] { EchoLines.EnemyKilled, EchoLines.PlayerHurt, EchoLines.AbilityActivated, EchoLines.IdleHint })
            {
                foreach (var line in post3[kind])
                {
                    CollectionAssert.Contains(post9[kind], line);
                }
            }
        }

        [Test]
        public void PoolsFor_NoLineContainsEmDash()
        {
            var pools = EchoLines.PoolsFor(f => true);

            foreach (var kind in pools.Keys)
            {
                foreach (var line in pools[kind])
                {
                    Assert.IsFalse(line.Contains("—"), $"'{line}' ({kind}) contains an em-dash.");
                }
            }
        }

        [Test]
        public void PoolsFor_EveryLineNonEmptyAndTrimmed()
        {
            var pools = EchoLines.PoolsFor(f => true);

            foreach (var kind in pools.Keys)
            {
                foreach (var line in pools[kind])
                {
                    Assert.IsFalse(string.IsNullOrEmpty(line), $"Empty line in '{kind}'.");
                    Assert.AreEqual(line.Trim(), line, $"'{line}' ({kind}) has leading/trailing whitespace.");
                }
            }
        }

        [Test]
        public void PoolsFor_NullHasFlag_TreatedAsPreCh3()
        {
            var pools = EchoLines.PoolsFor(null);

            Assert.AreEqual(0, pools[EchoLines.EnemyKilled].Length);
        }
    }
}
