using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Designer-tunable stats for a hostile space fighter. Mirrors the melee
    /// <see cref="Ronin7.Enemies.EnemyDefinition"/> style but for the dogfight loop:
    /// the ship approaches the player's virtual position, strafes at a preferred range, fires
    /// pooled bolts on a cadence, and occasionally peels off to evade. All distances/speeds are
    /// expressed in <b>universe-local units</b> because enemy ships live under the moving
    /// <c>universe</c> transform and chase <see cref="ShipController.ShipPosition"/> (see
    /// <see cref="EnemyShip"/> for the full frame-of-reference rationale).
    /// </summary>
    [CreateAssetMenu(menuName = "Space Samurai/Enemy Ship Definition", fileName = "EnemyShipDefinition")]
    public class EnemyShipDefinition : ScriptableObject
    {
        [Header("Body")]
        public float maxHealth = 50f;

        [Header("Movement (universe units)")]
        [Tooltip("Cruise speed when approaching or repositioning.")]
        public float moveSpeed = 24f;
        [Tooltip("Turn rate (deg/sec) used to swing the nose toward its move/aim target. Keeps motion readable.")]
        public float turnSpeed = 90f;
        [Tooltip("Distance the ship tries to hold from the player while strafing. The 'comfortable' dogfight range.")]
        public float preferredRange = 120f;
        [Tooltip("Tolerance band around the preferred range; inside it the ship strafes rather than closing/backing off.")]
        public float rangeTolerance = 25f;

        [Header("Strafe")]
        [Tooltip("Lateral orbit speed (deg/sec around the player) while holding range and firing.")]
        public float strafeSpeed = 30f;
        [Tooltip("Seconds before the ship reverses its strafe direction, so it weaves instead of circling predictably.")]
        public float strafeFlipInterval = 2.5f;

        [Header("Fire cadence (seconds)")]
        [Tooltip("Pause after reaching strafe range before opening fire (a readable 'lining up' beat).")]
        public float aimTime = 0.6f;
        [Tooltip("How long a firing burst lasts before the ship re-evaluates / evades.")]
        public float burstDuration = 1.6f;
        [Tooltip("Shots per second during a burst.")]
        [Range(0.2f, 8f)] public float fireRate = 1.5f;
        [Tooltip("Cone half-angle (deg) the nose must be within for the ship to actually pull the trigger.")]
        [Range(1f, 45f)] public float fireConeAngle = 12f;

        [Header("Evade")]
        [Tooltip("Seconds spent jinking away after a burst before resuming the approach.")]
        public float evadeTime = 1.4f;
        [Tooltip("Speed multiplier applied to moveSpeed during the evade dash.")]
        [Range(1f, 3f)] public float evadeSpeedMultiplier = 1.5f;

        [Header("Projectile")]
        [Tooltip("Speed of enemy bolts (universe units/sec). Enemy bolts live under universe and fly toward the player's virtual position.")]
        public float projectileSpeed = 90f;
        public float projectileDamage = 8f;
        [Tooltip("Seconds before an enemy bolt expires and returns to the pool.")]
        public float projectileLifetime = 3.5f;
        [Tooltip("Visual radius of an enemy bolt.")]
        [Range(0.05f, 1.5f)] public float projectileRadius = 0.4f;
        [Tooltip("Tint for enemy bolts so they read distinctly from the player's own fire.")]
        public Color tracerColor = new Color(1f, 0.45f, 0.25f);

        /// <summary>Seconds between shots during a burst, derived from <see cref="fireRate"/>.</summary>
        public float SecondsPerShot => fireRate > 0f ? 1f / fireRate : float.MaxValue;
    }
}
