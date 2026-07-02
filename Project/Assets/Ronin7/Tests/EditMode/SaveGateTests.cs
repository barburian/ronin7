using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class SaveGateTests
    {
        // Truth table: CanSave returns true only for calm SpaceFlight or calm OnFoot.
        // All other modes (Boot, GalaxyMap, Landing) always return false.

        [TestCase(GameMode.SpaceFlight, false, false, true)]
        [TestCase(GameMode.SpaceFlight, true, false, false)]
        [TestCase(GameMode.SpaceFlight, false, true, true)] // on-foot aggro is irrelevant in space
        [TestCase(GameMode.SpaceFlight, true, true, false)]

        [TestCase(GameMode.OnFoot, false, false, true)]
        [TestCase(GameMode.OnFoot, true, false, true)]
        [TestCase(GameMode.OnFoot, false, true, false)]
        [TestCase(GameMode.OnFoot, true, true, false)]

        [TestCase(GameMode.Boot, false, false, false)]
        [TestCase(GameMode.Boot, true, false, false)]
        [TestCase(GameMode.Boot, false, true, false)]
        [TestCase(GameMode.Boot, true, true, false)]

        [TestCase(GameMode.GalaxyMap, false, false, false)]
        [TestCase(GameMode.GalaxyMap, true, false, false)]
        [TestCase(GameMode.GalaxyMap, false, true, false)]
        [TestCase(GameMode.GalaxyMap, true, true, false)]

        [TestCase(GameMode.Landing, false, false, false)]
        [TestCase(GameMode.Landing, true, false, false)]
        [TestCase(GameMode.Landing, false, true, false)]
        [TestCase(GameMode.Landing, true, true, false)]

        public void CanSave_TruthTable(GameMode mode, bool spaceHostiles, bool onFootAggro, bool expected)
        {
            // Act
            bool result = SaveGate.CanSave(mode, spaceHostiles, onFootAggro);

            // Assert
            Assert.AreEqual(expected, result);
        }
    }
}
