using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards <see cref="GameFlowManager.ShouldAutosaveOnArrival"/>, the pure gate that decides
    /// whether reaching a newly loaded scene autosaves. Every player scene arrival is a checkpoint;
    /// Boot arrivals (main menu) and the quick-boot hub load (no New/Load choice was made, so
    /// saving would clobber MostRecentSlot) must not save.
    /// </summary>
    public class GameFlowAutosaveTests
    {
        [Test]
        public void ShouldAutosaveOnArrival_PlayerScene_Saves()
        {
            Assert.IsTrue(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: false));
            Assert.IsTrue(GameFlowManager.ShouldAutosaveOnArrival(GameMode.SpaceFlight, quickBoot: false));
        }

        [Test]
        public void ShouldAutosaveOnArrival_BootScene_DoesNotSave()
        {
            // Return-to-menu and game over both land in Boot — no checkpoint there.
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.Boot, quickBoot: false));
        }

        [Test]
        public void ShouldAutosaveOnArrival_QuickBoot_DoesNotSave_RegardlessOfMode()
        {
            // The quick-boot hub load arrives in OnFoot but precedes any New/Load choice.
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: true));
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.Boot, quickBoot: true));
        }
    }
}
