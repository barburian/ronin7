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

        [TestCase(-20.5f)]
        [TestCase(0.1f)]
        [TestCase(5f)]
        public void ImplausibleHeight_IsInvalid(float y) =>
            Assert.IsFalse(MainMenuController.IsHeadPoseValid(true, new Vector3(0.1f, y, 0.2f)));

        [Test]
        public void SeatedHeight_IsValid() =>
            Assert.IsTrue(MainMenuController.IsHeadPoseValid(true, new Vector3(0f, 1.05f, 0f)));

        [Test]
        public void StableCount_GrowsWhilePoseHolds_ResetsOnJump()
        {
            var p = new Vector3(0f, 1.6f, 0f);
            Assert.AreEqual(3, MainMenuController.NextStableFrames(2, p, p + new Vector3(0.001f, 0f, 0f)));
            Assert.AreEqual(0, MainMenuController.NextStableFrames(4, p, p + new Vector3(0f, 0.2f, 0f)));
            Assert.Greater(MainMenuController.RequiredStableFrames, 1);
        }

        [Test]
        public void Placement_StandingForward_IsInFrontAtEyeLevelMinusDrop()
        {
            var pos = MainMenuController.PlaceInFront(new Vector3(0.2f, 1.6f, 0.1f), Vector3.forward, out var rot);
            Assert.AreEqual(0.2f, pos.x, 1e-4f);
            Assert.AreEqual(1.5f, pos.y, 1e-4f);
            Assert.AreEqual(1.6f, pos.z, 1e-4f);
            Assert.AreEqual(0f, Vector3.Angle(rot * Vector3.forward, Vector3.forward), 1e-3f);
        }

        [Test]
        public void Placement_LookingDown_UsesFlatForward_NotTheFloor()
        {
            var fwd = new Vector3(1f, -1f, 0f).normalized; // 45 deg down, facing +X
            var pos = MainMenuController.PlaceInFront(new Vector3(0f, 1.6f, 0f), fwd, out var rot);
            Assert.AreEqual(1.5f, pos.x, 1e-3f);
            Assert.AreEqual(1.5f, pos.y, 1e-4f);
            Assert.AreEqual(0f, Vector3.Angle(rot * Vector3.forward, Vector3.right), 1e-3f);
        }

        [Test]
        public void Placement_StraightDown_FallsBackToPlusZ()
        {
            var pos = MainMenuController.PlaceInFront(new Vector3(0f, 1.6f, 0f), Vector3.down, out var rot);
            Assert.AreEqual(1.5f, pos.z, 1e-3f);
            Assert.AreEqual(0f, Vector3.Angle(rot * Vector3.forward, Vector3.forward), 1e-3f);
        }

        [Test]
        public void Placement_SeatedHeight_FollowsHead()
        {
            var pos = MainMenuController.PlaceInFront(new Vector3(0f, 1.05f, 0f), Vector3.forward, out _);
            Assert.AreEqual(0.95f, pos.y, 1e-4f);
        }
    }
}
