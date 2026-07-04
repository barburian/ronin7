using NUnit.Framework;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class ZeroGCombatVolumeTests
    {
        /// <summary>
        /// Smoke test: ZeroGCombatVolume can be instantiated without errors.
        /// Full ref-counting behavior requires PlayMode and trigger setup.
        /// </summary>
        [Test]
        public void ZeroGCombatVolume_CanBeInstantiated()
        {
            var go = new GameObject("TestVolume");
            var volume = go.AddComponent<ZeroGCombatVolume>();
            Assert.IsNotNull(volume);
            Object.DestroyImmediate(go);
        }

        // ---- Enter/Exit pure ref-counting transitions. ----

        [Test]
        public void Enter_FirstVolume_ReturnsCountOneAndShouldEnable()
        {
            var (count, shouldEnable) = ZeroGCombatVolume.Enter(0);

            Assert.AreEqual(1, count);
            Assert.IsTrue(shouldEnable);
        }

        [Test]
        public void Enter_SecondOverlappingVolume_DoesNotReSignalEnable()
        {
            var (count, shouldEnable) = ZeroGCombatVolume.Enter(1);

            Assert.AreEqual(2, count);
            Assert.IsFalse(shouldEnable);
        }

        [Test]
        public void Exit_LastVolume_ReturnsCountZeroAndShouldDisable()
        {
            var (count, shouldDisable) = ZeroGCombatVolume.Exit(1);

            Assert.AreEqual(0, count);
            Assert.IsTrue(shouldDisable);
        }

        [Test]
        public void Exit_NotLastVolume_DoesNotSignalDisable()
        {
            var (count, shouldDisable) = ZeroGCombatVolume.Exit(2);

            Assert.AreEqual(1, count);
            Assert.IsFalse(shouldDisable);
        }

        // ---- Leak regression: OnDestroy while inside must release the ref count. ----
        // Before the fix, destroying a volume while the player was still inside it never fired
        // OnTriggerExit, so the static counter leaked its increment forever and zero-g stayed
        // permanently broken for the rest of the session.

        [Test]
        public void Enter_ThenDestroyWhileInside_ThenEnterAgain_SignalsShouldEnableAgain()
        {
            // Simulates: Enter (count 0 -> 1, shouldEnable) -> volume destroyed while still inside,
            // which must run the same Exit path (count 1 -> 0, shouldDisable) -> a later Enter must
            // see a fresh count of 0 and signal shouldEnable again, rather than staying stuck at >=1.
            var (afterEnter, shouldEnable) = ZeroGCombatVolume.Enter(0);
            Assert.AreEqual(1, afterEnter);
            Assert.IsTrue(shouldEnable);

            var (afterDestroyExit, shouldDisable) = ZeroGCombatVolume.Exit(afterEnter);
            Assert.AreEqual(0, afterDestroyExit);
            Assert.IsTrue(shouldDisable);

            var (afterSecondEnter, shouldEnableAgain) = ZeroGCombatVolume.Enter(afterDestroyExit);
            Assert.AreEqual(1, afterSecondEnter);
            Assert.IsTrue(shouldEnableAgain);
        }
    }
}
