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
        Luck,

        // ---- P4-05 archetype boons. Read through PlayerCombatModifiers.Boon(kind) (stacks summed). ----
        /// <summary>magnitude 4 => a perfect parry heals 4 (Counterweight).</summary>
        HealOnPerfectParry,
        /// <summary>magnitude 0.4 => combo window +0.4 s (Momentum Keeper).</summary>
        ComboWindow,
        /// <summary>magnitude 0.3 => a swing hitting 2+ enemies does +30% to each (Whirlwind).</summary>
        MultiHitBonus,
        /// <summary>magnitude 0.1 per stack => bleed 0.05 + bonus of the hit over 3 s (Serrated Edge: 15/25/35%).</summary>
        Bleed,
        /// <summary>magnitude 0.2 => bleeding targets take +20% blade damage (Open Veins).</summary>
        BleedVulnerability,
        /// <summary>magnitude 6 => killing a bleeding enemy heals 6 (Exsanguinate).</summary>
        HealOnBleedKill,
        /// <summary>magnitude 12 => every 4th hit chains for 12 x BoonMultiplier (Static Edge).</summary>
        StaticChain,
        /// <summary>magnitude 1 => +1 chain jump and +1.5 m chain range (Conductor).</summary>
        ChainJumps,
        /// <summary>magnitude 20 => a blade clash shocks within 2.5 m: 20 damage + 25 posture (Thunderclap).</summary>
        ClashShock,
        /// <summary>magnitude 2 => posture-break bonus damage x3 (Guillotine).</summary>
        PostureBreakBonus,
        /// <summary>magnitude 0.5 => Overdrive charge per hit +50% (Overclock).</summary>
        OverdriveCharge,
        /// <summary>magnitude 1 => heal 1 per blade hit while Overdrive is active (Overclock secondary).</summary>
        OverdriveLifesteal,
        /// <summary>magnitude 10 => Mirror lasts +10 s (Echo Twin).</summary>
        MirrorDuration,
        /// <summary>magnitude 0.35 => below 30% HP: +35% damage and -15% incoming (Last Stand).</summary>
        LastStand,
        /// <summary>Negative => the Forge's MEND card is disabled (Blood Price curse).</summary>
        ForgeMend,
        /// <summary>Negative => a non-perfect deflect breaks the parry streak (Reckless Flow curse).</summary>
        RecklessFlow,
        /// <summary>magnitude 1 => Epic offer-tier weight x2 (Hollow Oath).</summary>
        EpicWeight,
        /// <summary>magnitude -0.2 => max HP x0.8 (Hollow Oath curse).</summary>
        MaxHealthPercent,
        /// <summary>magnitude 4 => riposte hits chain to every enemy within 4 m (Thunder Riposte).</summary>
        RiposteChain,
        /// <summary>magnitude 1 => at combo 3+, bleed x2 (Crimson Tide).</summary>
        ComboBleed,
        /// <summary>magnitude 0.25 => a Phase Step up to 0.25 s before an enemy's strike counts as a perfect parry (Ghost Step).</summary>
        GhostStep,
        /// <summary>magnitude 1 => a swing-direction reversal on the same target advances the combo (Reversal).</summary>
        ComboReversal,
        /// <summary>magnitude 3 => parry-streak idle timeout +3 s (Flow Initiate secondary).</summary>
        ParryStreakTimeout,
        /// <summary>magnitude 10 => heal 10 when picked (Ironskin secondary).</summary>
        HealOnPick,
        /// <summary>magnitude 0.25 per stack => Riposte damage 1.5 + max(0.25, bonus) (Riposte secondary).</summary>
        RiposteDamage
    }
}
