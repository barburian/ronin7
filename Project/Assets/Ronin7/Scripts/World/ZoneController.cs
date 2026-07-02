using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Tracks the search-and-clear objective for a zone: defeat all enemies and collect all
    /// relics, then the extraction pad activates. Reaching it completes the loop.
    /// </summary>
    public class ZoneController : MonoBehaviour
    {
        [SerializeField] private ZoneDefinition definition;
        [SerializeField] private List<Enemy> enemies = new();
        [SerializeField] private List<Pickup> relics = new();
        [SerializeField] private ExtractionZone extraction;

        private int enemiesAlive;
        private int relicsRemaining;

        private void Start()
        {
            foreach (var e in enemies)
            {
                if (e == null) continue;
                var h = e.GetComponent<Health>();
                if (h == null) continue;
                enemiesAlive++;
                h.Died += OnEnemyDied;
            }

            foreach (var p in relics)
            {
                if (p == null) continue;
                relicsRemaining++;
                p.Collected += OnRelicCollected;
            }

            if (extraction != null) extraction.Extracted += OnExtracted;

            Report();
            CheckComplete();
        }

        private void OnEnemyDied()
        {
            enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
            Report();
            CheckComplete();
        }

        private void OnRelicCollected(Pickup p)
        {
            relicsRemaining = Mathf.Max(0, relicsRemaining - 1);
            Report();
            CheckComplete();
        }

        private void CheckComplete()
        {
            if (enemiesAlive == 0 && relicsRemaining == 0 && extraction != null && !extraction.Active)
            {
                Log.Info("[Zone] Objective complete — return to the extraction pad (now green).");
                extraction.Activate();
            }
        }

        private void OnExtracted()
        {
            Log.Info("[Zone] Extracted! Zone cleared.");
            EventBus.Publish(new ZoneCompleted());
        }

        private void Report()
        {
            Log.Info($"[Zone] Enemies left: {enemiesAlive} | Relics left: {relicsRemaining}");
            EventBus.Publish(new ObjectiveUpdated(enemiesAlive, relicsRemaining));
        }
    }
}
