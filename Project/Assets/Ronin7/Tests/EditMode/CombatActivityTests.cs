using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class CombatActivityTests
    {
        [SetUp]
        public void SetUp()
        {
            CombatActivity.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CombatActivity.Reset();
        }

        [Test]
        public void Add_IncrementsOnFootAggro()
        {
            // Arrange
            Assert.IsFalse(CombatActivity.OnFootAggro);

            // Act
            CombatActivity.Add();

            // Assert
            Assert.IsTrue(CombatActivity.OnFootAggro);
            Assert.AreEqual(1, CombatActivity.OnFootAggroCount);
        }

        [Test]
        public void Remove_DecrementsOnFootAggro()
        {
            // Arrange
            CombatActivity.Add();
            Assert.IsTrue(CombatActivity.OnFootAggro);

            // Act
            CombatActivity.Remove();

            // Assert
            Assert.IsFalse(CombatActivity.OnFootAggro);
            Assert.AreEqual(0, CombatActivity.OnFootAggroCount);
        }

        [Test]
        public void Remove_AtZero_StaysAtZero()
        {
            // Arrange
            Assert.AreEqual(0, CombatActivity.OnFootAggroCount);

            // Act
            CombatActivity.Remove();
            CombatActivity.Remove();

            // Assert
            Assert.AreEqual(0, CombatActivity.OnFootAggroCount);
            Assert.IsFalse(CombatActivity.OnFootAggro);
        }

        [Test]
        public void Add_AfterRemoveAtZero_WorksCorrectly()
        {
            // Arrange
            CombatActivity.Add();
            CombatActivity.Remove();
            Assert.IsFalse(CombatActivity.OnFootAggro);

            // Act
            CombatActivity.Remove();  // attempted underflow
            CombatActivity.Remove();  // attempted underflow again
            CombatActivity.Add();      // should increment from 0

            // Assert
            Assert.IsTrue(CombatActivity.OnFootAggro);
            Assert.AreEqual(1, CombatActivity.OnFootAggroCount);
        }

        [Test]
        public void MultipleAdds_CountIncrementsCorrectly()
        {
            // Act
            CombatActivity.Add();
            CombatActivity.Add();
            CombatActivity.Add();

            // Assert
            Assert.IsTrue(CombatActivity.OnFootAggro);
            Assert.AreEqual(3, CombatActivity.OnFootAggroCount);
        }

        [Test]
        public void MultipleRemoves_CountDecrementsCorrectly()
        {
            // Arrange
            CombatActivity.Add();
            CombatActivity.Add();
            CombatActivity.Add();

            // Act
            CombatActivity.Remove();
            CombatActivity.Remove();

            // Assert
            Assert.IsTrue(CombatActivity.OnFootAggro);
            Assert.AreEqual(1, CombatActivity.OnFootAggroCount);
        }
    }
}
