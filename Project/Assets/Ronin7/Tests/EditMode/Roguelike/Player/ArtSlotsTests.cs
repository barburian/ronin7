using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Player;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P4-03: Weapon Art / Off-hand Art slot model, dispatch gates and snapshot fields.</summary>
    public class ArtSlotsTests
    {
        private int fired;

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            fired = 0;
            ArtSlots.Register("test.art", () => fired++);
        }

        [TearDown]
        public void TearDown()
        {
            ArtSlots.Unregister("test.art");
            RunState.Reset();
        }

        [Test]
        public void NothingEquipped_DoesNotFire()
        {
            Assert.IsFalse(ArtSlots.TryFire(ArtSlot.Weapon, true, false));
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void EquipReplacesPerSlot_AndSlotsAreIndependent()
        {
            ArtSlots.Equip(ArtSlot.Weapon, "a");
            ArtSlots.Equip(ArtSlot.Weapon, "test.art");
            ArtSlots.Equip(ArtSlot.Offhand, "b");
            Assert.AreEqual("test.art", ArtSlots.Equipped(ArtSlot.Weapon));
            Assert.AreEqual("b", ArtSlots.Equipped(ArtSlot.Offhand));
        }

        [Test]
        public void WeaponArt_NeedsSwordInRightHand()
        {
            ArtSlots.Equip(ArtSlot.Weapon, "test.art");
            Assert.IsFalse(ArtSlots.TryFire(ArtSlot.Weapon, false, false));
            Assert.IsTrue(ArtSlots.TryFire(ArtSlot.Weapon, true, false));
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void OffhandArt_NeedsEmptyLeftHand()
        {
            ArtSlots.Equip(ArtSlot.Offhand, "test.art");
            Assert.IsFalse(ArtSlots.TryFire(ArtSlot.Offhand, true, true));
            Assert.IsTrue(ArtSlots.TryFire(ArtSlot.Offhand, true, false));
            Assert.AreEqual(1, fired);
        }

        [TestCase("katana", true)]
        [TestCase("twin_wakizashi", true)]
        [TestCase("polearm_shield", false)]
        [TestCase("whip", false)]
        public void WeaponArt_OnlyOnKatanaAndDoubleKatana_OffhandArtOnAnyWeapon(string loadout, bool weaponArtOk)
        {
            MetaProgression.DevUnlockAllWeapons = true;
            try
            {
                Assert.IsTrue(MetaProgression.TrySelectLoadout(loadout));
                RunState.Begin(1u);
                Assert.AreEqual(loadout, RunState.LoadoutId);
                Assert.AreEqual(weaponArtOk, ArtSlots.CanFire(ArtSlot.Weapon, true, false));
                Assert.IsTrue(ArtSlots.CanFire(ArtSlot.Offhand, true, false));
            }
            finally { MetaProgression.DevUnlockAllWeapons = false; MetaProgression.ResetForTests(); }
        }

        [Test]
        public void LeftHanded_SwapsMainAndOffHand()
        {
            ArtSlots.Hands(false, rightGrab: true, leftGrab: false, out bool main, out bool off);
            Assert.IsTrue(main); Assert.IsFalse(off);
            ArtSlots.Hands(true, rightGrab: true, leftGrab: false, out main, out off);
            Assert.IsFalse(main); Assert.IsTrue(off);
        }

        [Test]
        public void UnregisteredId_DoesNotFire()
        {
            ArtSlots.Equip(ArtSlot.Weapon, "missing");
            Assert.IsFalse(ArtSlots.TryFire(ArtSlot.Weapon, true, false));
        }

        [Test]
        public void ArtIds_ClearOnEnd()
        {
            RunState.Begin(7u);
            ArtSlots.Equip(ArtSlot.Weapon, "w");
            ArtSlots.Equip(ArtSlot.Offhand, "o");
            RunState.End();
            Assert.AreEqual("", RunState.WeaponArtId);
            Assert.AreEqual("", RunState.OffhandArtId);
        }
    }
}
