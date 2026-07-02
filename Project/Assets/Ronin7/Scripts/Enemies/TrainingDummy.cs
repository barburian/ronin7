using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Stationary practice target. Skips Chase entirely — waits in Idle for the player to
    /// step into range, then runs the shared Windup → Active → Recover chop from
    /// <see cref="MeleeAttacker"/>. Parry zone is a forgiving sphere in front of the body;
    /// the window is the Active phase only (no late-Windup parry, unlike Enemy).
    /// </summary>
    public class TrainingDummy : MeleeAttacker
    {
        [Header("Timing (seconds) — inline; TrainingDummy has no SO definition")]
        [SerializeField] private float idleTime = 1.2f;
        [SerializeField] private float telegraphTime = 0.85f;
        [SerializeField] private float activeTime = 0.55f;
        [SerializeField] private float recoverTime = 0.7f;
        [SerializeField] private float staggerTime = 1.4f;

        [Header("Combat")]
        [SerializeField] private float damage = 15f;
        [SerializeField] private float attackRange = 2.5f;

        [Header("Parry guard zone (forgiving deflect detector)")]
        [Tooltip("How far in front of the dummy the guard zone sits.")]
        [SerializeField] private float guardForward = 0.55f;
        [SerializeField] private float guardHeight = 1.3f;
        [SerializeField] private float guardRadius = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        private static readonly Collider[] _bladeHits = new Collider[4];

        protected override float TelegraphTime => telegraphTime;
        protected override float ActiveTime => activeTime;
        protected override float RecoverTime => recoverTime;
        protected override float StaggerTime => staggerTime;
        protected override float AttackDamage => damage;

        // Slower track than Enemy — feels less twitchy for a stationary target.
        protected override float FaceTurnRate => 5f;

        // The dummy faces the player continuously (every state but Dead, which is handled
        // by the base's early-return). This preserves the original Update's "FacePlayer at
        // the top of Update before the switch" pattern.
        protected override void BeforeStateTick() => FacePlayer();

        protected override void OnIdleTick()
        {
            if (timer >= idleTime && PlayerInRange())
            {
                Log("telegraph (winding up)");
                BeginAttack();
            }
        }

        // Snap the body to face the player exactly at the moment the chop starts, so the
        // strike always points at where they were at telegraph-start.
        protected override void OnBeginAttack() => SnapFacePlayer();

        protected override void TryDetectParry()
        {
            Vector3 guard = GuardPoint();
            int n = Physics.OverlapSphereNonAlloc(guard, guardRadius, _bladeHits,
                Layers.BladeMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (_bladeHits[i].GetComponentInParent<BladeDamager>() == null) continue;
                Deflect(guard);
                return;
            }
        }

        protected override bool IsInLandHitRange() => PlayerInRange();

        protected override void OnDeathPose()
        {
            if (weapon != null) weapon.localRotation = Quaternion.Euler(strikeEuler);
        }

        protected override void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[TrainingDummy] {msg}", this);
        }

        private Vector3 GuardPoint() =>
            transform.position + transform.forward * guardForward + Vector3.up * guardHeight;

        private bool PlayerInRange()
        {
            if (target == null) return false;
            return (target.transform.position - transform.position).sqrMagnitude
                <= attackRange * attackRange;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || state != State.Active) return;
            Gizmos.color = deflectedThisSwing ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(GuardPoint(), guardRadius);
        }
    }
}
