using NUnit.Framework;
using Ronin7.Core;

namespace Ronin7.Tests.EditMode
{
    public class CampaignStateAbilityTests
    {
        private bool savedFirstPlanetDeparted;
        private int savedShipHullIndex;

        [SetUp]
        public void SetUp()
        {
            // ApplyFrom also writes Galaxy1Progress/ShipSelection; save/restore so ApplyFrom tests here
            // don't leak state into other test classes (mirrors CampaignStateTests.cs).
            savedFirstPlanetDeparted = Galaxy1Progress.FirstPlanetDeparted;
            savedShipHullIndex = ShipSelection.SelectedHullIndex;
            CampaignState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignState.Reset();
            Galaxy1Progress.FirstPlanetDeparted = savedFirstPlanetDeparted;
            ShipSelection.SelectedHullIndex = savedShipHullIndex;
        }

        [Test]
        public void UnlockAbility_ThenHasAbility_ReturnsTrue()
        {
            CampaignState.UnlockAbility(AbilityId.Overdrive);

            Assert.IsTrue(CampaignState.HasAbility(AbilityId.Overdrive));
        }

        [Test]
        public void HasAbility_UnsetAbility_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasAbility(AbilityId.Overdrive));
        }

        [Test]
        public void UnlockAbility_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CampaignState.UnlockAbility(null));
            Assert.IsFalse(CampaignState.HasAbility(null));
        }

        [Test]
        public void UnlockAbility_EmptyString_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => CampaignState.UnlockAbility(""));
            Assert.IsFalse(CampaignState.HasAbility(""));
        }

        [Test]
        public void HasAbility_Null_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasAbility(null));
        }

        [Test]
        public void HasAbility_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(CampaignState.HasAbility(""));
        }

        [Test]
        public void ToSaveData_SortsAbilityIds()
        {
            CampaignState.UnlockAbility(AbilityId.Mirror);
            CampaignState.UnlockAbility(AbilityId.Overdrive);
            CampaignState.UnlockAbility(AbilityId.PhaseStep);

            SaveData save = CampaignState.ToSaveData();

            Assert.AreEqual(3, save.abilityIds.Count);
            Assert.AreEqual(AbilityId.Mirror, save.abilityIds[0]);
            Assert.AreEqual(AbilityId.Overdrive, save.abilityIds[1]);
            Assert.AreEqual(AbilityId.PhaseStep, save.abilityIds[2]);
        }

        [Test]
        public void ToSaveData_AndApplyFrom_RoundTripsAbilities()
        {
            CampaignState.UnlockAbility(AbilityId.WeakpointSight);
            CampaignState.UnlockAbility(AbilityId.Unbroken);

            SaveData save = CampaignState.ToSaveData();
            CampaignState.Reset();
            CampaignState.ApplyFrom(save);

            Assert.IsTrue(CampaignState.HasAbility(AbilityId.WeakpointSight));
            Assert.IsTrue(CampaignState.HasAbility(AbilityId.Unbroken));
            Assert.IsFalse(CampaignState.HasAbility(AbilityId.Mirror));
        }

        [Test]
        public void ApplyFrom_NullAbilityIds_HandlesGracefully()
        {
            SaveData save = new SaveData();
            save.abilityIds = null;

            Assert.DoesNotThrow(() => CampaignState.ApplyFrom(save));
            Assert.IsFalse(CampaignState.HasAbility(AbilityId.Overdrive));
        }

        [Test]
        public void ApplyFrom_EmptyStringAbilityIdsIgnored()
        {
            SaveData save = new SaveData();
            save.abilityIds.Add("");
            save.abilityIds.Add(AbilityId.PhaseStep);

            CampaignState.ApplyFrom(save);

            Assert.IsTrue(CampaignState.HasAbility(AbilityId.PhaseStep));
            Assert.IsFalse(CampaignState.HasAbility(""));
        }

        [Test]
        public void Reset_ClearsAbilities()
        {
            CampaignState.UnlockAbility(AbilityId.Mirror);

            CampaignState.Reset();

            Assert.IsFalse(CampaignState.HasAbility(AbilityId.Mirror));
        }
    }
}
