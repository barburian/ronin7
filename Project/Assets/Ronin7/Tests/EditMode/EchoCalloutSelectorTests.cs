using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.World.Story;

namespace Ronin7.Tests.EditMode
{
    public class EchoCalloutSelectorTests
    {
        private static IReadOnlyDictionary<string, string[]> TwoLinePools() =>
            new Dictionary<string, string[]>
            {
                { "enemy_killed", new[] { "Down.", "Clean." } },
            };

        [Test]
        public void TryPick_FirstCall_ReturnsFirstLine()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 20f);

            bool picked = selector.TryPick("enemy_killed", 0f, out string line);

            Assert.IsTrue(picked);
            Assert.AreEqual("Down.", line);
        }

        [Test]
        public void TryPick_WithinCooldown_ReturnsFalse()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 20f);
            selector.TryPick("enemy_killed", 0f, out _);

            bool picked = selector.TryPick("enemy_killed", 5f, out string line);

            Assert.IsFalse(picked);
            Assert.IsNull(line);
        }

        [Test]
        public void TryPick_AfterCooldown_Allowed()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 20f);
            selector.TryPick("enemy_killed", 0f, out _);

            bool picked = selector.TryPick("enemy_killed", 20f, out string line);

            Assert.IsTrue(picked);
            Assert.IsNotNull(line);
        }

        [Test]
        public void TryPick_NoImmediateRepeat()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 0f);
            selector.TryPick("enemy_killed", 0f, out string first);

            selector.TryPick("enemy_killed", 1f, out string second);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void TryPick_DeterministicRoundRobin()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 0f);

            selector.TryPick("enemy_killed", 0f, out string first);
            selector.TryPick("enemy_killed", 1f, out string second);
            selector.TryPick("enemy_killed", 2f, out string third);

            Assert.AreEqual("Down.", first);
            Assert.AreEqual("Clean.", second);
            Assert.AreEqual("Down.", third);
        }

        [Test]
        public void TryPick_EmptyPool_ReturnsFalse()
        {
            var pools = new Dictionary<string, string[]> { { "idle_hint", new string[0] } };
            var selector = new EchoCalloutSelector(pools, cooldownSeconds: 0f);

            bool picked = selector.TryPick("idle_hint", 0f, out string line);

            Assert.IsFalse(picked);
            Assert.IsNull(line);
        }

        [Test]
        public void TryPick_UnknownKind_ReturnsFalse()
        {
            var selector = new EchoCalloutSelector(TwoLinePools(), cooldownSeconds: 0f);

            bool picked = selector.TryPick("nonexistent_kind", 0f, out string line);

            Assert.IsFalse(picked);
            Assert.IsNull(line);
        }
    }
}
