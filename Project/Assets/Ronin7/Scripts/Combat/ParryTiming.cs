using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Pure-logic helpers behind Sunder Beat: the perfect-parry timing window, streak progression, and
    /// the resulting flow-state damage multiplier.
    ///
    /// DEVIATION: kept public rather than internal, unlike <see cref="BladeDamager"/>'s internal-static
    /// pattern. Production callers are <c>Ronin7.Enemies.MeleeAttacker.Deflect</c> and
    /// <c>Ronin7.Player.ParryFlowController</c> — different assemblies than Ronin7.Combat — and every
    /// <c>InternalsVisibleTo</c> in this project only grants access to Ronin7.Tests.EditMode, never to a
    /// sibling gameplay assembly. Internal here would make this uncallable from where it's needed.
    /// </summary>
    public static class ParryTiming
    {
        /// <summary>
        /// 1 at a frame-zero deflect, ramping linearly down to 0 at (or past) the edge of
        /// <paramref name="perfectWindow"/> seconds since entering the parry-active state. A
        /// non-positive window always yields 0 (nothing is ever "perfect").
        /// </summary>
        public static float ParryQuality(float timeInActiveState, float perfectWindow)
            => perfectWindow <= 0f ? 0f : Mathf.Clamp01(1f - timeInActiveState / perfectWindow);

        /// <summary>
        /// Advances the Sunder Beat streak: a quality-positive (on-time) parry extends the streak by
        /// one, capped at <paramref name="maxStreak"/>. A quality-zero (late/outside-window) parry
        /// leaves the streak unchanged rather than resetting it — flow only breaks on taking a hit or on
        /// idling out, not on a parry that merely missed the "perfect" window.
        /// </summary>
        public static int NextStreak(int currentStreak, float quality, int maxStreak)
            => quality > 0f ? Mathf.Min(currentStreak + 1, maxStreak) : currentStreak;

        /// <summary>Damage multiplier from the current streak: +<paramref name="perStackBonus"/> per stack.</summary>
        public static float FlowMultiplier(int streak, float perStackBonus) => 1f + streak * perStackBonus;

        /// <summary>
        /// G4 "Blade Clash": true only when BOTH the player's blade and the enemy's blade were moving
        /// at or above <paramref name="threshold"/> at the moment of the deflect — a genuine mutual
        /// clash, not just a well-timed parry against a slow/telegraphed swing. Pure/stateless,
        /// mirroring the rest of this class.
        /// </summary>
        public static bool IsClash(float playerBladeSpeed, float enemySwingSpeed, float threshold)
            => playerBladeSpeed >= threshold && enemySwingSpeed >= threshold;
    }
}
