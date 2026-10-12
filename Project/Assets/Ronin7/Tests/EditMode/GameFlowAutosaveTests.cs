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
            Assert.IsTrue(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: false, inRun: false));
            Assert.IsTrue(GameFlowManager.ShouldAutosaveOnArrival(GameMode.SpaceFlight, quickBoot: false, inRun: false));
        }

        [Test]
        public void ShouldAutosaveOnArrival_BootScene_DoesNotSave()
        {
            // Return-to-menu and game over both land in Boot — no checkpoint there.
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.Boot, quickBoot: false, inRun: false));
        }

        [Test]
        public void ShouldAutosaveOnArrival_QuickBoot_DoesNotSave_RegardlessOfMode()
        {
            // The quick-boot hub load arrives in OnFoot but precedes any New/Load choice.
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: true, inRun: false));
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.Boot, quickBoot: true, inRun: false));
        }

        [Test]
        public void ShouldAutosaveOnArrival_InRun_ReturnsFalse()
        {
            // BUG-01: run nodes arrive as OnFoot, but CampaignState is blank during a run, so an
            // autosave would overwrite the campaign slot (and roll the .bak to blank) 15x per run.
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: false, inRun: true));
            Assert.IsFalse(GameFlowManager.ShouldAutosaveOnArrival(GameMode.SpaceFlight, quickBoot: false, inRun: true));
        }

        [Test]
        public void ShouldAutosaveOnArrival_NotInRun_OnFoot_ReturnsTrue()
        {
            Assert.IsTrue(GameFlowManager.ShouldAutosaveOnArrival(GameMode.OnFoot, quickBoot: false, inRun: false));
        }
    }
}
