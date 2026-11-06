using NUnit.Framework;
using Ronin7.Flow;

namespace Ronin7.Tests.EditMode
{
    public class RunEntryOptionsTests
    {
        [Test]
        public void NoSnapshot_NewRunOnly_GoesStraightToWeaponSelect()
        {
            Assert.IsFalse(RunEntryOptions.ShowContinue(false));
            Assert.AreEqual(RunEntryOptions.NewRunStep.OpenWeaponSelect, RunEntryOptions.NewRunClick(false, false, 0f));
            Assert.AreEqual("NEW RUN", RunEntryOptions.NewRunButtonLabel(false));
        }

        [Test]
        public void Snapshot_ShowsContinue_AndNewRunNeedsConfirmation()
        {
            Assert.IsTrue(RunEntryOptions.ShowContinue(true));
            Assert.AreEqual(RunEntryOptions.NewRunStep.ArmAbandon, RunEntryOptions.NewRunClick(true, false, 0f));
            Assert.AreEqual("ABANDON SAVED RUN?", RunEntryOptions.NewRunButtonLabel(true));
        }

        [Test]
        public void ArmedClick_AbandonsOnlyAfterTheDelay()
        {
            Assert.AreEqual(RunEntryOptions.NewRunStep.ArmAbandon, RunEntryOptions.NewRunClick(true, true, RunEntryOptions.ConfirmDelay - 0.1f));
            Assert.AreEqual(RunEntryOptions.NewRunStep.AbandonThenWeaponSelect, RunEntryOptions.NewRunClick(true, true, RunEntryOptions.ConfirmDelay));
        }
    }
}
