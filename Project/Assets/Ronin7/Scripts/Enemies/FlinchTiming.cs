using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>Pure flinch rules for <see cref="MeleeAttacker"/> (no Unity state; EditMode-testable).</summary>
    public static class FlinchTiming
    {
        /// <summary>
        /// Whether a hit should trigger a flinch. Never when dead, already staggered (deflect/posture
        /// stagger wins), mid-Active strike (committed, keeps parry timing honest), or inside the
        /// re-flinch immunity window. A hit during Windup flinches (cancelling the attack) only when
        /// <paramref name="cancelsWindup"/>.
        /// </summary>
        public static bool ShouldFlinch(bool alive, bool inStagger, bool inActive, bool inWindup,
            bool cancelsWindup, float now, float immuneUntil)
            => alive && !inStagger && !inActive && (!inWindup || cancelsWindup) && now >= immuneUntil;

        /// <summary>0 -> 1 -> 0 lean envelope over normalised flinch time.</summary>
        public static float TiltEnvelope(float t) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
    }
}
