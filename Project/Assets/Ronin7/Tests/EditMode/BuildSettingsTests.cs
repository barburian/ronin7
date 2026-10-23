using System.Linq;
using NUnit.Framework;
using Ronin7.Flow;
using UnityEditor;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P0-10 (D-02): the roguelike build ships the boot menu (index 0) and RunArena only;
    /// the campaign scenes stay in the list, disabled.</summary>
    public class BuildSettingsTests
    {
        private const string Boot = "Assets/Ronin7/Scenes/Phase6_Boot.unity";
        private const string Arena = "Assets/Ronin7/Scenes/RunArena.unity";

        [Test]
        public void RoguelikeScenesPresent_AndBootIsIndex0()
        {
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(enabled.Length, Is.GreaterThan(0), "No scenes enabled in the build.");
            Assert.AreEqual(Boot, enabled[0], "The boot menu must be build index 0.");
            // P5-05: baked room shells (Scenes/Rooms/) load additively into RunArena, so they ship too.
            CollectionAssert.AreEquivalent(new[] { Boot, Arena }, enabled.Where(p => !p.StartsWith("Assets/Ronin7/Scenes/Rooms/")),
                "Only the boot menu, RunArena and the room shells ship in the roguelike build (D-02).");
        }

        [TestCase(nameof(MainMenuController.OnStartNewGameClicked), true)]
        [TestCase(nameof(MainMenuController.OnContinueClicked), true)]
        [TestCase(nameof(MainMenuController.OnStartCampaignClicked), true)]
        [TestCase(nameof(MainMenuController.OnLoadSlotClicked), true)]
        [TestCase(nameof(MainMenuController.OnStartRunClicked), false)]
        [TestCase(nameof(MainMenuController.OnAbandonRunClicked), false)]
        [TestCase(nameof(MainMenuController.OnRecalibrateClicked), false)]
        [TestCase(nameof(MainMenuController.OnExitGameClicked), false)]
        public void CampaignEntries_AreHiddenWithoutTheCampaign(string method, bool isCampaign)
        {
            Assert.AreEqual(isCampaign, MainMenuController.IsCampaignEntry(method));
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void Transition_RefusesScenesOutsideTheBuild(bool inBuild, bool expected)
        {
            Assert.AreEqual(expected, GameFlowManager.CanLoadScene(inBuild));
        }
    }
}
