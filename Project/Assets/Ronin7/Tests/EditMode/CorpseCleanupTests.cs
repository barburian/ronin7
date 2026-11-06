using NUnit.Framework;
using Ronin7.Enemies;

namespace Ronin7.Tests.EditMode
{
    /// <summary>Playtest 1: corpses sink during their last second, then are deactivated at CorpseSeconds.</summary>
    public class CorpseCleanupTests
    {
        [Test]
        public void Corpse_StaysPutThenSinksOverTheLastSecond()
        {
            Assert.AreEqual(0f, MeleeAttacker.CorpseSunkAt(0f), 1e-5f);
            Assert.AreEqual(0f, MeleeAttacker.CorpseSunkAt(MeleeAttacker.CorpseSeconds - 1f), 1e-5f);
            Assert.AreEqual(0.5f, MeleeAttacker.CorpseSunkAt(MeleeAttacker.CorpseSeconds - 0.5f), 1e-5f);
            Assert.AreEqual(1f, MeleeAttacker.CorpseSunkAt(MeleeAttacker.CorpseSeconds), 1e-5f);
            Assert.AreEqual(1f, MeleeAttacker.CorpseSunkAt(99f), 1e-5f);
        }
    }
}
