using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// P3-05 ranged gunner (Hunter_Drone). Reuses Enemy's Windup/Active/Recover machine: the Windup is the
    /// charge telegraph and the bolt leaves at the end of Active through the shared <see cref="Enemy.FireProjectile"/>
    /// path (P8-02c). No melee hit or parry. Kites to <see cref="KiteMin"/>..<see cref="KiteMax"/> metres.
    /// The bolt is reflectable (see Projectile).
    /// </summary>
    public class RangedEnemy : Enemy
    {
        public new const float KiteMin = Enemy.KiteMin, KiteMax = Enemy.KiteMax;
        private static readonly Strike Bolt = new Strike
        {
            direction = StrikeDirection.Overhead, windup = 1f, active = 1f, recovery = 1f, boltSpeed = 4f, boltRadius = 0.12f,
        };

        protected override Strike NextStrike() => Bolt;

        protected override void OnChaseTick()
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            // Step toward the middle of the band (away when too close); Hover eases it (P3-09).
            bool outOfBand = dist > KiteMax || dist < KiteMin;
            Hover(outOfBand ? to / Mathf.Max(dist, 1e-3f) * (dist > KiteMax ? 1f : -1f) : Vector3.zero);
            if (dist <= KiteMax && Time.time >= nextAttackTime) BeginAttack();
        }
    }
}
