using UnityEngine;

namespace Ronin7.Enemies
{
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

        [Header("Attack timing (seconds)")]
        public float telegraphTime = 0.8f;
        public float activeTime = 0.8f;     // the parry window
        public float recoverTime = 0.6f;
        public float staggerTime = 1.2f;
        [Tooltip("Pause between attacks while repositioning.")]
        public float attackCooldown = 0.8f;

        [Header("Damage")]
        public float damage = 12f;

        [Header("Progression scaling (opt-in)")]
        [Tooltip("Extra max health per galaxy the player has finished (CampaignState.GalaxiesCompleted), " +
                 "mirroring SpaceEncounterManager's per-galaxy ramp. 0 = no scaling (default).")]
        public float healthAddedPerGalaxy = 0f;
        [Tooltip("Extra attack damage per galaxy completed. 0 = no scaling (default).")]
        public float damageAddedPerGalaxy = 0f;

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

        /// <summary>Attack damage scaled by story progress via <see cref="damageAddedPerGalaxy"/>.</summary>
        public float ScaledDamage(int galaxiesCompleted)
            => ScaledByGalaxy(damage, damageAddedPerGalaxy, galaxiesCompleted);
    }
}
