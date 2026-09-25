namespace Ronin7.Combat
{
    /// <summary>
    /// What a <see cref="BoonDefinition"/> does. <see cref="BoonInventory"/> aggregates same-kind boons
    /// held at once into the numbers the rest of Combat reads; see each member for how its magnitude
    /// is interpreted.
    ///
    /// Amendment 1 (A1.3) removed MoveSpeedMultiplier and ParryWindowMultiplier: neither had a
    /// possible consumer (ContinuousLocomotion exposes no speed hook and its comfort vignette ignores
    /// linear speed — a shippable speed boon is a VR-comfort hazard, not just dead code; and
    /// PerfectParryWindow is a private const that can't be scaled at runtime).
    /// </summary>
    public enum BoonEffectKind
    {
        /// <summary>magnitude 0.15 => x1.15, stacks multiplicatively as (1+magnitude)^stacks.</summary>
        BladeDamageMultiplier,
        /// <summary>magnitude 20 => +20 max HP, additive.</summary>
        MaxHealthAdd,
        /// <summary>magnitude 3 => 3 HP per kill, additive.</summary>
        HealOnKill,
        /// <summary>Adds to PlayerCombatModifiers.ParryFlowMultiplier headroom. Read via BoonInventory.Stacks
        /// and the definition's magnitude by the Player-side controller that owns that headroom.</summary>
        ParryFlowBonus,
        /// <summary>Combo-chain bonus. Read via BoonInventory.Stacks and the definition's magnitude by the
        /// Player-side combo controller.</summary>
        ComboBonus,
        /// <summary>Grants one extra life for the run. maxStacks is normally 1; BoonInventory.ConsumeRevive
        /// returns true at most once per stack granted.</summary>
        ReviveOnce,
        /// <summary>Grants an already-built ability (see AbilityId) via the abilityId field. maxStacks is
        /// normally 1.</summary>
        GrantAbility
    }
}
