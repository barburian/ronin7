using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Faction-aligned combatant that hunts and attacks units from rival factions.
    /// Unlike AllyCombatant, this unit has its own Health and can be damaged by the player and other factions.
    ///
    /// Target identification: a valid target is any live Health that is either:
    /// (a) the player (identified by CharacterController), always hostile to all factions, OR
    /// (b) another FactionCombatant with a different factionId.
    ///
    /// For tests: call TickCombat(deltaTime) to drive per-frame decisions deterministically.
    /// </summary>
    public class FactionCombatant : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private float moveSpeed = 1.4f;
        [SerializeField] private float attackRange = 1.6f;
        [SerializeField] private float damagePerHit = 8f;
        [SerializeField] private float attackInterval = 1.4f;
        [SerializeField] private float retargetInterval = 1f;

        [Header("Refs")]
        [SerializeField] private Renderer bodyRenderer;

        [Header("Faction")]
        [SerializeField] private int factionId = 0;

        private Health currentTarget;
        private float groundY;
        private float timeSinceLastAttack;
        private float timeSinceLastRetarget;

        /// <summary>The faction ID for this unit.</summary>
        public int FactionId => factionId;

        /// <summary>The current target's Health, or null if idle.</summary>
        public Health CurrentTarget => currentTarget;

        /// <summary>Set the faction ID. Called by builders after AddComponent.</summary>
        public void SetFaction(int id)
        {
            factionId = id;
        }

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

        /// <summary>Find and lock onto the nearest live hostile target (player or rival faction).</summary>
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

                // Skip self
                if (h.gameObject == gameObject) continue;

                // Check if it's the player (has CharacterController) — always a valid target
                if (h.GetComponent<CharacterController>() != null)
                {
                    float distSq = (h.transform.position - transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        currentTarget = h;
                    }
                    continue;
                }

                // Check if it's a rival faction unit (has FactionCombatant with different faction)
                var otherCombatant = h.GetComponent<FactionCombatant>();
                if (otherCombatant != null && ShouldTargetFaction(factionId, otherCombatant.FactionId))
                {
                    float distSq = (h.transform.position - transform.position).sqrMagnitude;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        currentTarget = h;
                    }
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

        /// <summary>Static predicate: returns true if selfFaction and otherFaction are different (hostile).</summary>
        public static bool ShouldTargetFaction(int selfFaction, int otherFaction)
        {
            return selfFaction != otherFaction;
        }
    }
}
