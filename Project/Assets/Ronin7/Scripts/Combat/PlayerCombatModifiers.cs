using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the player rig root. Holds the named multiplier slots <see cref="BladeDamager"/> reads
    /// (via <see cref="DamageMultiplier"/>) to scale the wielder's swing damage. Each slot defaults to
    /// 1 (no effect) so every existing scene/test that doesn't place this component — or an ability
    /// that hasn't unlocked yet — is unaffected. Ch7's weakpoint-sight (<c>Ronin7.Player.WeakpointSight</c>)
    /// drives <see cref="WeakpointMultiplier"/>; Sunder Beat's parry-flow streak
    /// (<c>Ronin7.Player.ParryFlowController</c>) drives <see cref="ParryFlowMultiplier"/>; Follow-
    /// Through's combo counter (<c>Ronin7.Player.ComboMomentumController</c>) drives
    /// <see cref="ComboMultiplier"/>. The coupling runs Player -> writes -> this component -> Combat
    /// reads, since Combat doesn't reference Player.
    ///
    /// LIFETIME: a plain instance living on the player rig GameObject in the scene — no static state.
    /// Every field/property here re-initializes to its default the moment a fresh instance is created,
    /// which happens on every scene load (including "Enter Play Mode without domain reload," since the
    /// rig itself is a scene object, not a persisted singleton). Unlike EventBus/Health.Active/
    /// CombatActivity, there is nothing here that would leak stale values across Play sessions, so no
    /// explicit Reset()/RuntimeInitializeOnLoadMethod is needed.
    /// </summary>
    public class PlayerCombatModifiers : MonoBehaviour
    {
        /// <summary>Hard cap on the combined <see cref="DamageMultiplier"/> product (see its doc).</summary>
        private const float MaxDamageMultiplier = 3f;

        /// <summary>Ch7 weakpoint-sight's multiplier slot.</summary>
        public float WeakpointMultiplier { get; set; } = 1f;

        /// <summary>Sunder Beat's parry-flow-streak multiplier slot.</summary>
        public float ParryFlowMultiplier { get; set; } = 1f;

        /// <summary>Follow-Through's combo-chain multiplier slot.</summary>
        public float ComboMultiplier { get; set; } = 1f;

        /// <summary>
        /// The multiplier <see cref="BladeDamager"/> actually applies: the product of every named slot
        /// above, hard-capped at <see cref="MaxDamageMultiplier"/> (3x). Multiple buffs can be active at
        /// once (weakpoint-sight + a parry-flow streak + a combo chain), and without a cap their product
        /// could compound into a one-shot-everything multiplier; the cap keeps stacking generous without
        /// making it game-breaking. Read-only — set the individual slots instead.
        /// </summary>
        public float DamageMultiplier =>
            Mathf.Min(WeakpointMultiplier * ParryFlowMultiplier * ComboMultiplier, MaxDamageMultiplier);
    }
}
