using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Friendly combatant NPC that hunts and attacks live enemies on the ground plane.
    /// Has no Health component and cannot be damaged by design — builders must not add Health to ally NPCs.
    ///
    /// Target identification mirrors the enemy's perspective but inverted: enemies are GameObjects
    /// with Health + MeleeAttacker component, excluding the player (identified by CharacterController).
    ///
    /// For tests: call TickCombat(deltaTime) to drive per-frame decisions deterministically.
    /// </summary>
    public class AllyCombatant : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private float moveSpeed = 1.4f;
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float damagePerHit = 8f;
        [SerializeField] private float attackInterval = 1.4f;
        [SerializeField] private float retargetInterval = 1f;

        [Header("Refs")]
        [SerializeField] private Renderer bodyRenderer;

        private Health currentTarget;
        private float groundY;
        private float timeSinceLastAttack;
        private float timeSinceLastRetarget;

        /// <summary>The current target's Health, or null if idle.</summary>
        public Health CurrentTarget => currentTarget;

        private void Awake()
        {
            groundY = transform.position.y;
        }

        private void Update()
        {
            TickCombat(Time.deltaTime);
        }

        /// <summary>Main combat loop: retarget, move, attack. Call from Update or tests.</summary>
        public void TickCombat(float deltaTime)
        {
            timeSinceLastRetarget += deltaTime;
            if (timeSinceLastRetarget >= retargetInterval)
            {
                timeSinceLastRetarget = 0f;
                RetargetNearestEnemy();
            }

            // No target or target died
            if (currentTarget == null || !currentTarget.IsAlive)
            {
                currentTarget = null;
                return;
            }

            Vector3 to = currentTarget.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // Face the target (copy of MeleeAttacker.FacePlayer pattern)
            if (to.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(to), 8f * deltaTime);
            }

            timeSinceLastAttack += deltaTime;

            if (dist > attackRange)
            {
                // Chase: move toward target, maintain ground Y (copy of Enemy.OnChaseTick pattern)
                Vector3 step = to.normalized * moveSpeed * deltaTime;
                Vector3 pos = transform.position + step;
                pos.y = groundY;
                transform.position = pos;
            }
            else if (timeSinceLastAttack >= attackInterval)
            {
                // In range: attack
                timeSinceLastAttack = 0f;
                AttackTarget();
            }
        }

        /// <summary>Find and lock onto the nearest live enemy (Health + MeleeAttacker, excluding player).</summary>
        private void RetargetNearestEnemy()
        {
            currentTarget = null;
            float bestDistSq = float.MaxValue;

            var active = Health.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var h = active[i];
                // Must exist and be alive
                if (h == null || !h.IsAlive) continue;

                // Skip player (has CharacterController)
                if (h.GetComponent<CharacterController>() != null) continue;

                // Must be an enemy (has MeleeAttacker component, which includes Enemy)
                if (h.GetComponent<MeleeAttacker>() == null) continue;

                float distSq = (h.transform.position - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    currentTarget = h;
                }
            }
        }

        /// <summary>Deal damage to the current target (copy of MeleeAttacker.LandHit pattern).</summary>
        private void AttackTarget()
        {
            if (currentTarget == null || !currentTarget.IsAlive) return;

            Vector3 dir = (currentTarget.transform.position - transform.position).normalized;
            currentTarget.ApplyDamage(new DamageInfo(damagePerHit, currentTarget.transform.position, dir, gameObject));
        }
    }
}
