using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Minimal duel loop: watches a single enemy and, when it dies, respawns it after a
    /// delay so the player can keep practicing. (Phase 4+ will grow this into proper
    /// zone-driven encounters / waves.)
    /// </summary>
    public class EncounterManager : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float respawnDelay = 2.5f;

        private Health enemyHealth;
        private float respawnAt = -1f;

        private void Awake()
        {
            if (enemy != null) enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth != null) enemyHealth.Died += OnEnemyDied;
        }

        private void OnDestroy()
        {
            if (enemyHealth != null) enemyHealth.Died -= OnEnemyDied;
        }

        private void OnEnemyDied()
        {
            respawnAt = Time.time + respawnDelay;
            Log.Info("[Encounter] Enemy down — respawning shortly.");
        }

        private void Update()
        {
            if (respawnAt > 0f && Time.time >= respawnAt)
            {
                respawnAt = -1f;
                Vector3 pos = spawnPoint != null ? spawnPoint.position : enemy.transform.position;
                enemy.Respawn(pos);
            }
        }
    }
}
