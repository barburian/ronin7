using NUnit.Framework;
using Ronin7.World.Story;

namespace Ronin7.Tests.EditMode
{
    public class MultiObjectiveLogicTests
    {
        [Test]
        public void InitialState_RemainingEqualsCount_NotAllComplete()
        {
            var logic = new MultiObjectiveLogic(3);
            Assert.AreEqual(3, logic.RemainingCount);
            Assert.IsFalse(logic.AllComplete);
        }

        [Test]
        public void Complete_DecrementsRemainingCount()
        {
            var logic = new MultiObjectiveLogic(3);
            logic.Complete(0);
            Assert.AreEqual(2, logic.RemainingCount);
        }

        [Test]
        public void Complete_LastOutstanding_ReturnsTrueAndSetsAllComplete()
        {
            var logic = new MultiObjectiveLogic(3);
            Assert.IsFalse(logic.Complete(0));
            Assert.IsFalse(logic.Complete(1));
            Assert.IsTrue(logic.Complete(2));
            Assert.IsTrue(logic.AllComplete);
            Assert.AreEqual(0, logic.RemainingCount);
        }

        [Test]
        public void Complete_AnyOrder_StillCompletesOnTheThirdCall()
        {
            var logic = new MultiObjectiveLogic(3);
            Assert.IsFalse(logic.Complete(2));
            Assert.IsFalse(logic.Complete(0));
            Assert.IsTrue(logic.Complete(1));
            Assert.IsTrue(logic.AllComplete);
        }

        [Test]
        public void Complete_SameIndexTwice_IsIdempotent_SecondCallReturnsFalse()
        {
            var logic = new MultiObjectiveLogic(2);
            Assert.IsFalse(logic.Complete(0));
            Assert.IsFalse(logic.Complete(0)); // already complete: no-op, no double-decrement
            Assert.AreEqual(1, logic.RemainingCount);
            Assert.IsFalse(logic.AllComplete);
        }

        [Test]
        public void Complete_OutOfRangeIndex_IsIgnored()
        {
            var logic = new MultiObjectiveLogic(2);
            Assert.IsFalse(logic.Complete(-1));
            Assert.IsFalse(logic.Complete(2));
            Assert.AreEqual(2, logic.RemainingCount);
        }

        [Test]
        public void ZeroCount_IsAlreadyAllComplete()
        {
            var logic = new MultiObjectiveLogic(0);
            Assert.IsTrue(logic.AllComplete);
            Assert.AreEqual(0, logic.RemainingCount);
        }

        [Test]
        public void NegativeCount_TreatedAsZero()
        {
            var logic = new MultiObjectiveLogic(-5);
            Assert.IsTrue(logic.AllComplete);
            Assert.AreEqual(0, logic.RemainingCount);
        }
    }
}
