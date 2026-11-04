using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// P8-06 Duelist. Between its own attacks it holds its weapon up in a guard: a frontal melee hit is blocked
    /// (no damage, the resisted clang) and it ripostes after <see cref="RiposteDelay"/>. The guard drops while it
    /// attacks, recovers or staggers, so the openings are: parry its swing, punish its recovery (Recovery
    /// weakness in data), or get behind the blade. In range it sidesteps every few seconds. Melee kits only
    /// (EnemySpawnTable.Entry.meleeOnly). The guard reads by pose + sound, never colour.
    /// </summary>
    public class DuelistEnemy : Enemy
    {
        public const float RiposteDelay = 0.35f, SidestepEvery = 2.5f, SidestepMetres = 1.1f, SidestepSeconds = 0.3f;
        // Arm raised forward with the blade held across the chest (MeleeAttacker pose space: x swing, y yaw, z roll).
        // ponytail: one shared guard pose for every melee kit; tune per weapon if a kit reads wrong.
        private static readonly Vector3 GuardEuler = new Vector3(-60f, -40f, 60f);
        private const float GuardRaiseRate = 6f; // per second: ~0.17 s to full guard

        private float guardT;
        private float nextSidestep;
        internal bool RipostePending { get; private set; }
        internal float RiposteAt { get; set; }

        /// <summary>True while a frontal blade hit would be blocked (Idle/Chase).</summary>
        internal bool Guarding => state == State.Idle || state == State.Chase;

        public override bool IsPosingWeapon => base.IsPosingWeapon || (Guarding && guardT > 0f);

        protected override float FilterDamage(DamageInfo info)
        {
            if (Guarding && info.Type == DamageType.Melee && IsFrontal(transform.position, transform.forward, info.Point))
            {
                EventBus.Publish(new HitResisted(info.Point, gameObject)); // clang + spark + haptic: "blocked"
                if (!RipostePending) { RipostePending = true; RiposteAt = Time.time + RiposteDelay; }
                return 0f; // Health drops a zero hit: no damage, no flinch
            }
            return base.FilterDamage(info);
        }

        /// <summary>Pure: the hit point is in front of the body (the blade covers the front half).</summary>
        public static bool IsFrontal(Vector3 pos, Vector3 forward, Vector3 point)
        {
            Vector3 to = point - pos; to.y = 0f; forward.y = 0f;
            return Vector3.Dot(to, forward) > 0f;
        }

        protected override void BeforeStateTick()
        {
            base.BeforeStateTick();
            if (!Guarding) { guardT = 0f; return; }
            guardT = Mathf.MoveTowards(guardT, 1f, GuardRaiseRate * Time.deltaTime);
            PoseWeapon(restEuler, GuardEuler, guardT);
        }

        protected override void OnChaseTick()
        {
            if (RipostePending && Time.time >= RiposteAt)
            {
                RipostePending = false;
                BeginAttack();
                return;
            }
            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            if (to.magnitude <= Definition.attackRange + 1f && Time.time >= nextSidestep)
            {
                nextSidestep = Time.time + SidestepEvery;
                Vector3 side = Vector3.Cross(Vector3.up, to).normalized * (Random.value < 0.5f ? -1f : 1f);
                Displace(side * SidestepMetres, SidestepSeconds);
            }
            base.OnChaseTick();
        }

        /// <summary>Editor capture hook: hold the full guard pose.</summary>
        public void HoldGuard() { guardT = 1f; PoseWeapon(restEuler, GuardEuler, 1f); }

        public override void Respawn(Vector3 position)
        {
            base.Respawn(position);
            RipostePending = false;
            guardT = 0f;
        }
    }
}
