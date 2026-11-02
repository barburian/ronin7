using NUnit.Framework;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P7-03 (BUG-50): the boot menu recenters only on a real HMD pose.</summary>
    public class MenuRecenterTests
    {
        [Test]
        public void UntrackedHead_IsInvalid() =>
            Assert.IsFalse(MainMenuController.IsHeadPoseValid(false, new Vector3(0f, 1.6f, 0f)));

        [Test]
        public void TrackedButZeroPose_IsInvalid() =>
            Assert.IsFalse(MainMenuController.IsHeadPoseValid(true, Vector3.zero));

        [Test]
        public void TrackedWithPose_IsValid() =>
            Assert.IsTrue(MainMenuController.IsHeadPoseValid(true, new Vector3(0.1f, 1.5f, -0.2f)));
    }
}
