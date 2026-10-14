using NUnit.Framework;
using Ronin7.World;

namespace Ronin7.Tests.EditMode.Roguelike.World
{
    public class TutorialPromptTests
    {
        private const TutorialPrompt.Step Draw = TutorialPrompt.Step.Draw;

        [Test]
        public void Advance_HappyPath_WalksAllSteps()
        {
            var s = Draw;
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.Grabbed);
            Assert.AreEqual(TutorialPrompt.Step.Swing, s);
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.Impact);
            Assert.AreEqual(TutorialPrompt.Step.Parry, s);
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.Deflected);
            Assert.AreEqual(TutorialPrompt.Step.Choose, s);
            s = TutorialPrompt.Advance(s, TutorialPrompt.Trigger.BoonChosen);
            Assert.AreEqual(TutorialPrompt.Step.Done, s);
        }

        [Test]
        public void Advance_ClearBeforeParry_SkipsToChoose()
        {
            var s = TutorialPrompt.Advance(TutorialPrompt.Step.Parry, TutorialPrompt.Trigger.Cleared);
            Assert.AreEqual(TutorialPrompt.Step.Choose, s);
        }

        [Test]
        public void Advance_LateEventFromDraw_SkipsAhead_NeverRewinds()
        {
            // Sword already held at room start: first event is a hit, not a grab.
            Assert.AreEqual(TutorialPrompt.Step.Parry, TutorialPrompt.Advance(Draw, TutorialPrompt.Trigger.Impact));
            Assert.AreEqual(TutorialPrompt.Step.Choose, TutorialPrompt.Advance(TutorialPrompt.Step.Choose, TutorialPrompt.Trigger.Grabbed));
        }
    }
}
