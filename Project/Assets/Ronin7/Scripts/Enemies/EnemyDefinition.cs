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
    }
}
