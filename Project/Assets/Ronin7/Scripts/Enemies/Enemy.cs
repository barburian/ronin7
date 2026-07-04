using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Ground-melee enemy. Approaches the player on flat ground (Chase state), then runs the
    /// shared Windup → Active → Recover chop from <see cref="MeleeAttacker"/>. Parry probe is
    /// a capsule between <c>weapon</c> and <c>bladeTip</c>; the parry window also covers the
    /// late part of Windup (≥ 0.75 of telegraphTime) so a brave swing can interrupt the chop.
    /// </summary>
    public class Enemy : MeleeAttacker
    {
        [Header("Refs (Enemy)")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private Transform bladeTip;

        [Header("Parry probe")]
        [SerializeField] private float parryRadius = 0.12f;

        [SerializeField] private bool debugLogs = false;

        private static readonly Collider[] _bladeHits = new Collider[4];

        private float groundY;
        private float nextAttackTime;

        protected override float TelegraphTime => definition.telegraphTime;
        protected override float ActiveTime => definition.activeTime;
        protected override float RecoverTime => definition.recoverTime;
        protected override float StaggerTime => definition.staggerTime;
        protected override float AttackDamage => definition.damage;

        // After Recover/Stagger, drop back into Chase rather than Idle — the Idle state's
        // 0.2s settle gate is meant for the very first spawn only.
        protected override State StateAfterRecover => State.Chase;
        protected override State StateAfterStagger => State.Chase;

        // Parry window includes late Windup so a player can pre-empt the chop.
        protected override bool IsInParryWindow()
            => state == State.Active
            || (state == State.Windup && timer >= TelegraphTime * 0.75f);

        // bladeTip is the point that actually sweeps through the chop (weapon is the static pivot) —
        // the same point already used for the parry capsule below, so it doubles as the G4 Blade
        // Clash swing-speed tracker's sample point.
        protected override Transform SwingTrackPoint => bladeTip != null ? bladeTip : weapon;

        protected override void Awake()
        {
            if (definition == null)
            {
                Debug.LogWarning("[Enemy] No EnemyDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            }
            base.Awake();
            health.Configure(definition.maxHealth);
            if (bladeTip == null)
                Debug.LogError("[Enemy] bladeTip not wired — parry detection disabled.", this);
            groundY = transform.position.y;
        }

        // FacePlayer every frame while engaging or telegraphing — NOT during Active/Recover/
        // Stagger, so the body locks orientation during the chop animation. Matches the
        // original per-case FacePlayer placement.
        protected override void BeforeStateTick()
        {
            if (state == State.Idle || state == State.Chase || state == State.Windup)
                FacePlayer();
        }

        protected override void OnIdleTick()
        {
            // Brief settle on spawn before chasing (original used a dedicated Idle→Chase
            // transition gated on timer > 0.2s; same effect here).
            if (timer > 0.2f) Enter(State.Chase);
        }

        protected override void OnChaseTick()
        {
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist > definition.attackRange)
            {
                Vector3 step = to.normalized * definition.moveSpeed * Time.deltaTime;
                Vector3 pos = transform.position + step;
                pos.y = groundY;
                transform.position = pos;
            }
            else if (Time.time >= nextAttackTime)
            {
                BeginAttack();
            }
        }

        protected override void TryDetectParry()
        {
            if (bladeTip == null || weapon == null) return;

            int n = Physics.OverlapCapsuleNonAlloc(weapon.position, bladeTip.position,
                parryRadius, _bladeHits, Layers.BladeMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var blade = _bladeHits[i].GetComponentInParent<BladeDamager>();
                if (blade == null) continue;
                Deflect(bladeTip.position, blade);
                return;
            }
        }

        // +0.4 slop so a player who's just slipped out of range during Active still eats the hit.
        protected override bool IsInLandHitRange()
            => target != null
            && (target.transform.position - transform.position).sqrMagnitude
               <= (definition.attackRange + 0.4f) * (definition.attackRange + 0.4f);

        protected override void OnActiveEnd() => nextAttackTime = Time.time + definition.attackCooldown;

        protected override void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[Enemy] {msg}", this);
        }

        /// <summary>Revive and reposition for a fresh duel.</summary>
        public void Respawn(Vector3 position)
        {
            if (definition != null) health.Configure(definition.maxHealth);
            transform.SetPositionAndRotation(position, Quaternion.identity);
            groundY = position.y;
            currentEuler = restEuler;
            if (weapon != null) weapon.localRotation = Quaternion.Euler(restEuler);
            Tint(IdleColor);
            deflectedThisSwing = false;
            nextAttackTime = 0f;
            Enter(State.Idle);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (weapon == null || bladeTip == null) return;
            bool lateWindup = state == State.Windup && definition != null
                && timer >= definition.telegraphTime * 0.75f;
            if (state != State.Active && !lateWindup) return;
            Gizmos.color = deflectedThisSwing ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(weapon.position, parryRadius);
            Gizmos.DrawWireSphere(bladeTip.position, parryRadius);
            Gizmos.DrawLine(weapon.position, bladeTip.position);
        }
    }
}
