using System;
using System.Collections.Generic;
using Ronin7.Core;

namespace Ronin7.Player
{
    public enum ArtSlot { Weapon, Offhand }

    /// <summary>
    /// P4-03: which Art is equipped per slot (ids live in <see cref="RunState"/> so they snapshot) and
    /// the id -> action registry the input dispatcher fires. Skills (P4-04) call <see cref="Register"/>.
    /// </summary>
    public static class ArtSlots
    {
        private static readonly Dictionary<string, Action> skills = new();

        /// <summary>True while Twin Wakizashi is equipped (P4-08 sets it); disables the Off-hand slot.</summary>
        public static bool OffhandBlocked;

        public static void Register(string id, Action activate) => skills[id] = activate;
        public static void Unregister(string id) => skills.Remove(id);

        public static string Equipped(ArtSlot slot) =>
            slot == ArtSlot.Weapon ? RunState.WeaponArtId : RunState.OffhandArtId;

        /// <summary>Equips <paramref name="id"/> in <paramref name="slot"/>, replacing the old one ("" clears).</summary>
        public static void Equip(ArtSlot slot, string id)
        {
            if (slot == ArtSlot.Weapon) RunState.SetWeaponArt(id);
            else RunState.SetOffhandArt(id);
        }

        /// <summary>Weapon Art needs the sword in the right hand; Off-hand Art needs an empty left hand
        /// and no Twin Wakizashi. Pure.</summary>
        public static bool CanFire(ArtSlot slot, bool rightHolding, bool leftHolding) =>
            slot == ArtSlot.Weapon ? rightHolding : !leftHolding && !OffhandBlocked;

        /// <summary>Runs the slot's equipped skill if the hand gate allows and one is registered.</summary>
        public static bool TryFire(ArtSlot slot, bool rightHolding, bool leftHolding)
        {
            if (!CanFire(slot, rightHolding, leftHolding)) return false;
            string id = Equipped(slot);
            if (string.IsNullOrEmpty(id) || !skills.TryGetValue(id, out var activate)) return false;
            activate();
            return true;
        }
    }
}
