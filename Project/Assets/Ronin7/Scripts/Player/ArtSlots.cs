using System;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    public enum ArtSlot { Weapon, Offhand }

    /// <summary>P4-04 stable Art ids (snapshotted in RunState; never rename).</summary>
    public static class ArtId
    {
        public const string ChargedSlash = "art.charged_slash"; // S4, Weapon
        public const string BladeThrow = "art.blade_throw";     // S5, Weapon
        public const string KiaiPalm = "art.kiai_palm";         // S6, Off-hand
        public const string ChainHook = "art.chain_hook";       // S7, Off-hand
        public const string ElementImbue = "art.element_imbue"; // S8, Off-hand
    }

    /// <summary>
    /// P4-03: which Art is equipped per slot (ids live in <see cref="RunState"/> so they snapshot) and
    /// the id -> action registry the input dispatcher fires. Skills (P4-04) call <see cref="Register"/>.
    /// </summary>
    public static class ArtSlots
    {
        private static readonly Dictionary<string, Action> skills = new();

        /// <summary>True while Twin Wakizashi is equipped (P4-08 sets it); disables the Off-hand slot.</summary>
        public static bool OffhandBlocked;

        /// <summary>P4-04 modal gate: true while a boon/choice panel, the pause menu or the run summary is up.
        /// Set by RunDirector (Flow), which owns those panels. Null = never blocked.</summary>
        public static Func<bool> ModalOpen;

        public static bool Blocked => ModalOpen != null && ModalOpen();

        /// <summary>P4-04: the slot's trigger is held this frame and its gates pass (written by
        /// <see cref="ArtInputDispatcher"/>). Hold/release Arts poll this.</summary>
        public static bool WeaponHeld, OffhandHeld;

        public static bool Held(ArtSlot slot) => slot == ArtSlot.Weapon ? WeaponHeld : OffhandHeld;

        /// <summary>True when <paramref name="id"/> is the Art equipped in <paramref name="slot"/>.</summary>
        public static bool IsEquipped(ArtSlot slot, string id) => Equipped(slot) == id;

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
            if (Blocked || !CanFire(slot, rightHolding, leftHolding)) return false;
            string id = Equipped(slot);
            if (string.IsNullOrEmpty(id) || !skills.TryGetValue(id, out var activate)) return false;
            activate();
            return true;
        }
    }

    /// <summary>P4-04: small shared lookups for the skill components on the rig root.</summary>
    internal static class SkillRig
    {
        /// <summary>AbilityHaste-scaled cooldown factor for <paramref name="mods"/> (1 with no mods).</summary>
        public static float CooldownScale(PlayerCombatModifiers mods) =>
            PlayerCombatModifiers.CooldownScale(mods != null ? mods.Boon(BoonEffectKind.AbilityHaste) : 0f);

        /// <summary>Hand velocity in real m/s. HandVelocityTracker divides by the (slow-mo scaled)
        /// fixedDeltaTime, so it reads 1/timeScale too fast under slow-mo; 0 while paused.</summary>
        public static Vector3 RealVelocity(HandVelocityTracker t) =>
            t != null ? t.LinearVelocity * Mathf.Max(0f, Time.timeScale) : Vector3.zero;
    }
}
