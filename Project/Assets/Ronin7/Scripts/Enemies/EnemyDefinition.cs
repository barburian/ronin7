using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>Per-enemy exploit rule (P3). Serialized: append-only.</summary>
    public enum WeaknessKind { None, Backstab, HeavyBreak, Recovery, Alternating, Reflect,
                               Linchpin, ParryGate, CounterHit, PostureOnly, Blink }

    /// <summary>Body material class, drives hit VFX + audio. Serialized: append-only.</summary>
    public enum MaterialClass { Organic, Mech, Spectral, Armored }

    /// <summary>Designer-tunable stats for a melee enemy.</summary>
    [CreateAssetMenu(menuName = "Space Samurai/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Body")]
        public float maxHealth = 60f;

        [Header("Movement")]
        public float moveSpeed = 1.4f;
        [Tooltip("Distance at which the enemy stops and starts an attack.")]
        public float attackRange = 1.7f;
        [Tooltip("P2-11: max body turn rate, degrees/s. Small corrections still ease in; this caps big swings.")]
        public float turnRateDegrees = 240f;
        [Tooltip("P2-11: walk speed ramp-up, m/s^2.")]
        public float acceleration = 4f;
        [Tooltip("P2-11: braking, m/s^2. Also sets how early the enemy slows before attackRange.")]
        public float deceleration = 6f;

        [Header("Attack timing (seconds)")]
        public float telegraphTime = 0.8f;
        public float activeTime = 0.8f;     // the parry window
        public float recoverTime = 0.6f;
        public float staggerTime = 1.2f;
        [Tooltip("Pause between attacks while repositioning.")]
        public float attackCooldown = 0.8f;

        [Header("Attack pose (P1-15, procedural on the 6-bone rig)")]
        [Tooltip("Multiplier on the weapon-arm arc away from rest (1 = full overhead chop; small for armless drones).")]
        public float poseArmScale = 1f;
        [Tooltip("Torso yaw at full draw-back, degrees (right shoulder pulls back; strike twists through).")]
        public float poseTwistDegrees = 30f;
        [Tooltip("Torso backward lean at full draw-back, degrees (strike lunges forward past rest).")]
        public float poseLeanDegrees = 12f;
        [Tooltip("Pose the whole rig (Rig_Root) instead of the torso: drones/rigid hulls lunge as one piece.")]
        public bool poseWholeBody;

        [Header("Damage")]
        public float damage = 12f;

        [Header("Progression scaling (opt-in)")]
        [Tooltip("Extra max health per galaxy the player has finished (CampaignState.GalaxiesCompleted), " +
                 "mirroring SpaceEncounterManager's per-galaxy ramp. 0 = no scaling (default).")]
        public float healthAddedPerGalaxy = 0f;
        [Tooltip("Extra attack damage per galaxy completed. 0 = no scaling (default).")]
        public float damageAddedPerGalaxy = 0f;

        [Header("Poise")]
        public bool superArmor;
        public float postureGainMultiplier = 1f;

        [Header("Weakness")]
        public WeaknessKind weakness;
        public float resistMultiplier = 0.15f, exploitMultiplier = 2f, heavySwingSpeed = 5.5f;
        public MaterialClass materialClass; // VFX + audio

        [Header("Posture (opt-in)")]
        [Tooltip("Fraction of maxHealth used as this enemy's posture-break threshold, so tougher tiers " +
                 "take longer to stagger (see PostureMeter.Configure). 0 = disabled — PostureMeter keeps " +
                 "its own serialized postureMax unchanged (default).")]
        public float postureMaxFraction = 0f;

        /// <summary>Pure scaling helper: base value plus a flat per-galaxy bonus. Negative
        /// galaxiesCompleted is clamped to 0 (can't happen via CampaignState, but keeps this safe
        /// standalone). Mirrors SpaceEncounterManager.ComposeWave's per-galaxy ramp.</summary>
        public static float ScaledByGalaxy(float baseValue, float perGalaxy, int galaxiesCompleted)
            => baseValue + perGalaxy * Mathf.Max(0, galaxiesCompleted);

        /// <summary>Max health scaled by story progress via <see cref="healthAddedPerGalaxy"/>.</summary>
        public float ScaledMaxHealth(int galaxiesCompleted)
            => ScaledByGalaxy(maxHealth, healthAddedPerGalaxy, galaxiesCompleted);

        /// <summary>Posture-break threshold: scaled max health * postureMaxFraction (0 = disabled).</summary>
        public float PostureMax(int galaxiesCompleted) => ScaledMaxHealth(galaxiesCompleted) * postureMaxFraction;

        /// <summary>Attack damage scaled by story progress via <see cref="damageAddedPerGalaxy"/>.</summary>
        public float ScaledDamage(int galaxiesCompleted)
            => ScaledByGalaxy(damage, damageAddedPerGalaxy, galaxiesCompleted);
    }
}
