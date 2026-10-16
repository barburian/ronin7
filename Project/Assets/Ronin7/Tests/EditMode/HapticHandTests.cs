using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine.XR;

namespace Ronin7.Tests.EditMode
{
    /// <summary>BUG-34 guard: the shared off-hand lookup follows GameSettings.LeftHanded.</summary>
    public class HapticHandTests
    {
        private GameSettings _saved;

        [SetUp] public void SetUp() => _saved = GameSettings.Active;
        [TearDown] public void TearDown() => GameSettings.Active = _saved;

        [Test]
        public void HapticHand_FollowsLeftHanded()
        {
            var s = GameSettings.Default;
            s.LeftHanded = false;
            GameSettings.Active = s;
            Assert.AreEqual(XRNode.LeftHand, HandednessRig.OffHand(GameSettings.Active.LeftHanded));

            s.LeftHanded = true;
            GameSettings.Active = s;
            Assert.AreEqual(XRNode.RightHand, HandednessRig.OffHand(GameSettings.Active.LeftHanded));
        }
    }
}
