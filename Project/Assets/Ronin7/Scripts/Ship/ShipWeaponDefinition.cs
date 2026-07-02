using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Designer-tunable stats for the player ship's fixed forward guns. Unlike the melee
    /// <see cref="Ronin7.Combat.WeaponDefinition"/> (where damage scales with swing speed),
    /// ship guns fire pooled projectiles at a fixed cadence and each bolt carries a flat damage
    /// value. Kept data-driven so balancing is an Inspector chore, not a recompile.
    /// </summary>
    [CreateAssetMenu(menuName = "Space Samurai/Ship Weapon Definition", fileName = "ShipWeaponDefinition")]
    public class ShipWeaponDefinition : ScriptableObject
    {
        [Header("Cadence")]
        [Tooltip("Shots per second while the trigger is held. Conservative on Quest so the pool stays small.")]
        [Range(0.5f, 20f)] public float fireRate = 5f;

        [Header("Projectile")]
        [Tooltip("Travel speed of each bolt (m/s) in real/camera space. Player bolts fly straight out of the cockpit.")]
        public float projectileSpeed = 120f;
        [Tooltip("Flat damage applied to the first IDamageable a bolt strikes.")]
        public float damage = 18f;
        [Tooltip("Seconds before an un-hit bolt expires and returns to the pool. Keeps the pool from starving.")]
        public float projectileLifetime = 2.5f;
        [Tooltip("Visual radius of the bolt's collider/scale. Small so it reads as a bolt, not a ball.")]
        [Range(0.02f, 0.5f)] public float projectileRadius = 0.08f;

        [Header("Feel")]
        [Tooltip("Tint applied to the bolt's grey-box visual so player fire reads distinctly from enemy fire.")]
        public Color tracerColor = new Color(0.4f, 0.9f, 1f);

        /// <summary>Seconds between shots, derived from <see cref="fireRate"/>.</summary>
        public float SecondsPerShot => fireRate > 0f ? 1f / fireRate : float.MaxValue;
    }
}
