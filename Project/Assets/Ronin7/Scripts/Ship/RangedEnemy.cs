using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// P3-05 ranged gunner (Hunter_Drone). Reuses Enemy's Windup/Active/Recover machine: the Windup is the
    /// charge telegraph, the bolt leaves at the end of Active, and there is no melee hit or parry. Kites
    /// to <see cref="KiteMin"/>..<see cref="KiteMax"/> metres. The bolt is reflectable (see Projectile).
    /// </summary>
    public class RangedEnemy : Enemy
    {
        public const float KiteMin = 5f, KiteMax = 7f;
        const float BoltSpeed = 4f, BoltLife = 4f, BoltRadius = 0.12f;

        ProjectilePool pool;

        protected override void Awake()
        {
            base.Awake();
            // ponytail: one lookup per spawn; the arena bakes a single shared pool, so no registry needed
            pool = FindAnyObjectByType<ProjectilePool>();
        }

        protected override bool IsInLandHitRange() => false;
        protected override void TryDetectParry() { }

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

        protected override void OnActiveEnd()
        {
            base.OnActiveEnd();
            if (pool == null) return;
            Vector3 muzzle = weapon != null ? weapon.position : transform.position + Vector3.up;
            var cam = Camera.main;
            Vector3 head = cam != null ? cam.transform.position : target.transform.position + Vector3.up * 1.6f;
            pool.Fire(gameObject, muzzle, Quaternion.LookRotation(head - muzzle), BoltSpeed, AttackDamage,
                BoltLife, BoltRadius, OpenColor, null); // BUG-39: gold = "send it back" (Reflect)
        }
    }
}
