using NUnit.Framework;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Guards the pure publish-decision seam on <see cref="ChapterOutro"/>.</summary>
    public class ChapterOutroTests
    {
        [Test]
        public void ShouldPublish_FlagEnabled_ReturnsTrue()
        {
            Assert.IsTrue(ChapterOutro.ShouldPublish(true));
        }

        [Test]
        public void ShouldPublish_FlagDisabled_ReturnsFalse()
        {
            Assert.IsFalse(ChapterOutro.ShouldPublish(false));
        }
    }
}
