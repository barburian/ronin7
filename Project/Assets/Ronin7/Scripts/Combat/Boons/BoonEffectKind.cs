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
    ///
    /// APPEND-ONLY (P4-01): authored BoonDefinition assets serialize this enum by its numeric value,
    /// and PlayerCombatModifiers indexes its boon-stat array by it. Never reorder, remove or insert —
    /// only add new members at the end. Pinned by BoonEffectKind_ExistingValues_ArePinned.
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
        GrantAbility,

        // ---- P4-02 stat sheet. Additive bonus over base; each consumer clamps to its cap. ----
        /// <summary>magnitude 0.1 => swing thresholds x0.9 (cap x0.70). Same max damage.</summary>
        EdgeSensitivity,
        /// <summary>magnitude 0.2 => posture gain x1.2 (cap x2.0).</summary>
        PostureDamage,
        /// <summary>magnitude 0.25 => precision hit x1.50 instead of x1.25 (cap x2.0).</summary>
        Precision,
        /// <summary>magnitude 0.1 => perfect-parry window +10% of 0.12 s (cap +0.04 s).</summary>
        ParryWindow,
        /// <summary>magnitude 0.25 => deflect stagger x1.25 (cap x1.5).</summary>
        StaggerDuration,
        /// <summary>magnitude 0.1 => 10% less incoming damage (cap 40%).</summary>
        DamageReduction,
        /// <summary>magnitude 0.1 => ability cooldowns x0.9 / Overdrive charge x1.1 (cap 40%).</summary>
        AbilityHaste,
        /// <summary>magnitude 1 => +1 Phase Step charge (base 1, cap 3).</summary>
        PhaseCharges,
        /// <summary>magnitude 0.1 => +10% echoes (cap x1.5).</summary>
        EchoGain,
        /// <summary>magnitude 1 => +1 luck point: +4 Rare / +2 Epic offer weight per point (cap 3).</summary>
        Luck
    }
}
