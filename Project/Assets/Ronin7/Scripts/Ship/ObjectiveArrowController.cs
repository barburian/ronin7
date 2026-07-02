using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Drives the cockpit waypoint arrow's target priority: while any enemy ship is alive the arrow
    /// tracks the NEAREST live hostile (re-targeting as ships die), and once the sky is clear it
    /// falls back to the current mission objective (a planet or dock). Listens for
    /// <see cref="EnemyShipDestroyed"/> for instant re-targets and rescans on a slow tick to catch
    /// spawns and relative-distance changes.
    /// </summary>
    public class ObjectiveArrowController : MonoBehaviour
    {
        [SerializeField] private PlanetTargetMarker marker;
        [Tooltip("Where the arrow points when no hostiles are alive (the mission objective).")]
        [SerializeField] private Transform defaultObjective;
        [Tooltip("Seconds between target rescans (the destroy event also triggers an immediate one).")]
        [SerializeField] private float retargetInterval = 0.5f;

        private Transform current;
        private float nextScanTime;

        private void OnEnable() => EventBus.Subscribe<EnemyShipDestroyed>(OnEnemyDestroyed);
        private void OnDisable() => EventBus.Unsubscribe<EnemyShipDestroyed>(OnEnemyDestroyed);

        private void Update()
        {
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + retargetInterval;
            Retarget();
        }

        private void OnEnemyDestroyed(EnemyShipDestroyed _) => Retarget();

        /// <summary>Swap the fallback objective (e.g. when the mission advances to the next planet).</summary>
        public void SetObjective(Transform objective)
        {
            defaultObjective = objective;
            Retarget();
        }

        private void Retarget()
        {
            if (marker == null) return;

            Transform next = NearestLiveEnemy();
            if (next == null) next = defaultObjective;
            if (next == current) return;

            current = next;
            marker.SetTarget(next);
        }

        private static Transform NearestLiveEnemy()
        {
            Vector3 shipPos = ShipController.Instance != null ? ShipController.Instance.ShipPosition : Vector3.zero;
            EnemyShip best = null;
            float bestSqr = float.MaxValue;
            var list = EnemyShip.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive) continue;
                float d = (e.UniversePosition - shipPos).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = e;
                }
            }
            return best != null ? best.transform : null;
        }
    }
}
