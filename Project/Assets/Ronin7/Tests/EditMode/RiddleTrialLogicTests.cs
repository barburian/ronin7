using NUnit.Framework;
using Ronin7.World.Story;

namespace Ronin7.Tests.EditMode
{
    public class RiddleTrialLogicTests
    {
        [Test]
        public void InitialState_NotPassed_NoWrongAttempts()
        {
            var logic = new RiddleTrialLogic(3, correctIndex: 1);
            Assert.IsFalse(logic.Passed);
            Assert.AreEqual(0, logic.WrongAttemptCount);
        }

        [Test]
        public void Submit_CorrectIndex_PassesAndReturnsCorrect()
        {
            var logic = new RiddleTrialLogic(3, correctIndex: 1);
            var result = logic.Submit(1);
            Assert.AreEqual(RiddleTrialResult.Correct, result);
            Assert.IsTrue(logic.Passed);
        }

        [Test]
        public void Submit_WrongInRangeIndex_FlagsGuardianSpawnAndDoesNotPass()
        {
            var logic = new RiddleTrialLogic(3, correctIndex: 1);
            var result = logic.Submit(0);
            Assert.AreEqual(RiddleTrialResult.Wrong, result);
            Assert.IsFalse(logic.Passed);
            Assert.AreEqual(1, logic.WrongAttemptCount);
        }

        [Test]
        public void Submit_SeveralWrongAnswers_EachCountsAndTrialStillOpen()
        {
            var logic = new RiddleTrialLogic(3, correctIndex: 2);
            Assert.AreEqual(RiddleTrialResult.Wrong, logic.Submit(0));
            Assert.AreEqual(RiddleTrialResult.Wrong, logic.Submit(1));
            Assert.AreEqual(2, logic.WrongAttemptCount);
            Assert.IsFalse(logic.Passed);

            Assert.AreEqual(RiddleTrialResult.Correct, logic.Submit(2));
            Assert.IsTrue(logic.Passed);
        }

        [Test]
        public void Submit_AfterPassed_IsIdempotent_CorrectAndWrongBothIgnored()
        {
            var logic = new RiddleTrialLogic(3, correctIndex: 1);
            Assert.AreEqual(RiddleTrialResult.Correct, logic.Submit(1));

            Assert.AreEqual(RiddleTrialResult.AlreadyPassed, logic.Submit(1)); // re-submitting the right pad
            Assert.AreEqual(RiddleTrialResult.AlreadyPassed, logic.Submit(0)); // stepping on a wrong pad afterward
            Assert.AreEqual(0, logic.WrongAttemptCount); // neither post-pass call touched the wrong count
        }

        [Test]
        public void Submit_OutOfRangeIndex_IsRejectedWithoutSideEffects()
        {
            var logic = new RiddleTrialLogic(2, correctIndex: 0);
            Assert.AreEqual(RiddleTrialResult.OutOfRange, logic.Submit(-1));
            Assert.AreEqual(RiddleTrialResult.OutOfRange, logic.Submit(2));
            Assert.IsFalse(logic.Passed);
            Assert.AreEqual(0, logic.WrongAttemptCount);
        }

        [Test]
        public void ZeroOptionCount_EveryIndexIsOutOfRange()
        {
            var logic = new RiddleTrialLogic(0, correctIndex: 0);
            Assert.AreEqual(RiddleTrialResult.OutOfRange, logic.Submit(0));
            Assert.IsFalse(logic.Passed);
        }

        [Test]
        public void NegativeOptionCount_TreatedAsZero()
        {
            var logic = new RiddleTrialLogic(-4, correctIndex: 0);
            Assert.AreEqual(RiddleTrialResult.OutOfRange, logic.Submit(0));
            Assert.IsFalse(logic.Passed);
        }
    }
}
