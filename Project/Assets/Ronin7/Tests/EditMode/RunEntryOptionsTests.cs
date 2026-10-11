using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    public class RunEntryOptionsTests
    {
        [Test]
        public void NoSnapshot_SingleNewRunControl()
        {
            Assert.AreEqual("ENTER THE FRACTURE", RunEntryOptions.PrimaryLabel(false));
            Assert.IsFalse(RunEntryOptions.ShowAbandon(false));
        }

        [Test]
        public void Snapshot_ResumeAndAbandon()
        {
            Assert.AreEqual("RESUME RUN", RunEntryOptions.PrimaryLabel(true));
            Assert.IsTrue(RunEntryOptions.ShowAbandon(true));
        }
    }
}
