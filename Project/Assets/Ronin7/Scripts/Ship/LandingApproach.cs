using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Ship
{
    /// <summary>
    /// Detects when the flying player has lined up a slow approach on a landable planet and lets
    /// them commit to landing by holding the grip. Publishes <see cref="LandingRequested"/>; the
    /// flow manager handles the comfort fade + scene load.
    ///
    /// Geometry: the rig never moves — the world (under <see cref="universe"/>) moves as the
    /// inverse of the virtual ship pose, so a planet at universe-local position P sits a distance
    /// |P - shipPos| from the player. We therefore measure proximity between
    /// <see cref="ShipController.ShipPosition"/> and each landable's universe-local position,
    /// sampled live each check because planets orbit (see <see cref="PlanetOrbit"/>).
    /// </summary>
    public class LandingApproach : MonoBehaviour
    {
        [System.Serializable]
        public class Landable
        {
            [Tooltip("The planet transform the player can land on (a child of the universe root).")]
            public Transform target;
            [Tooltip("How close (universe units) the ship must be to begin landing.")]
            public float approachRadius = 80f;
            [Tooltip("Scene to load on landing. Empty = the flow manager's default on-foot scene.")]
            public string destinationScene = "";
            [Tooltip("Campaign scene that must be completed before landing is allowed. Empty = always available.")]
            public string requiredCompletedScene = "";
        }

        [Header("Refs")]
        [SerializeField] private ShipController ship;
        [Tooltip("The moving-world root the landable planets live under (same one the ShipController drives).")]
        [SerializeField] private Transform universe;
        [SerializeField] private List<Landable> landables = new();

        public IReadOnlyList<Landable> Landables => landables;

        [Header("Commit input (reuse a grip / Select action)")]
        [SerializeField] private InputActionReference landAction;
        [SerializeField, Range(0.1f, 0.9f)] private float pressThreshold = 0.5f;

        [Header("Tuning")]
        [Tooltip("Ship speed must be at or below this (units/sec) for the approach to be considered stable.")]
        [SerializeField] private float maxLandingSpeed = 8f;
        [Tooltip("Seconds of stable slow approach inside the radius before landing arms.")]
        [SerializeField] private float dwellToArm = 0.6f;
        [Tooltip("Seconds the grip must be held, once armed, to commit to landing.")]
        [SerializeField] private float holdToLand = 1.0f;

        [Header("Prompt (optional)")]
        [SerializeField] private TextMesh promptText;

        [Header("Campaign gating (optional)")]
        [Tooltip("Destination scene that counts as the FIRST story planet: landing there sets " +
                 "Galaxy1Progress.FirstPlanetDeparted so enemy waves start once the player returns " +
                 "to space. Empty = no gating.")]
        [SerializeField] private string firstPlanetScene = "";

        private InputAction landResolved;
        private float armTimer;
        private float holdTimer;
        private bool fired;

        private void OnEnable()
        {
            landResolved = InputResolver.Resolve(landAction, string.Empty, string.Empty, "Landing");
        }

        private void OnDisable()
        {
            landResolved?.Disable();
            landResolved = null;
        }

        private void Start()
        {
            if (ship == null) Debug.LogError("[Landing] No ShipController assigned.", this);
            if (universe == null) Debug.LogError("[Landing] No universe transform assigned.", this);

            ShowPrompt(null);
        }

        private void Update()
        {
            if (fired || ship == null) return;

            Landable near = NearestInRange(out float dist);
            bool slow = Mathf.Abs(ship.CurrentSpeed) <= maxLandingSpeed;
            bool skiesClear = !HostilesPresent();

            // Completed story planets are view-only: the Corsair never enters the completed set,
            // so docking there stays available forever.
            if (near != null && CampaignState.IsCompleted(near.destinationScene))
            {
                armTimer = 0f;
                holdTimer = 0f;
                ShowPrompt("Planet already cleared");
                return;
            }

            // Check docking clearance: if a required scene is set but not completed, block landing.
            if (near != null && !ClearanceGranted(near.requiredCompletedScene))
            {
                armTimer = 0f;
                holdTimer = 0f;
                ShowPrompt("No docking clearance");
                return;
            }

            if (near == null || !slow)
            {
                armTimer = 0f;
                holdTimer = 0f;
                ShowPrompt(near != null && !slow ? "Slow down to land" : null);
                return;
            }

            if (!CanArm(near != null, slow, skiesClear))
            {
                // In range and slow, but enemies still alive: block landing with a clear message.
                armTimer = 0f;
                holdTimer = 0f;
                ShowPrompt("Clear hostiles to land");
                return;
            }

            // Inside the radius and flying slowly: build up the arm dwell.
            armTimer += Time.deltaTime;
            if (armTimer < dwellToArm)
            {
                ShowPrompt("Hold steady…");
                return;
            }

            // Armed — hold the grip to commit.
            bool pressed = landResolved != null && landResolved.ReadValue<float>() >= pressThreshold;
            if (pressed)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdToLand)
                {
                    fired = true;
                    ShowPrompt("Landing…");
                    if (!string.IsNullOrEmpty(firstPlanetScene) && near.destinationScene == firstPlanetScene)
                        Galaxy1Progress.FirstPlanetDeparted = true;
                    EventBus.Publish(new LandingRequested(near.destinationScene));
                    return;
                }
                ShowPrompt("Landing… keep holding");
            }
            else
            {
                holdTimer = 0f;
                ShowPrompt("Hold grip to land");
            }
        }

        /// <summary>Pure decision: landing may arm only when in range, slow enough, and no hostiles remain.</summary>
        public static bool CanArm(bool nearInRange, bool slowEnough, bool skiesClear)
            => nearInRange && slowEnough && skiesClear;

        /// <summary>Pure decision: docking clearance is granted if no required scene is set, or the required scene is completed.</summary>
        public static bool ClearanceGranted(string requiredCompletedScene)
            => string.IsNullOrEmpty(requiredCompletedScene) || CampaignState.IsCompleted(requiredCompletedScene);

        /// <summary>True if any registered <see cref="EnemyShip"/> is non-null and still alive.</summary>
        private bool HostilesPresent()
        {
            var list = EnemyShip.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && e.IsAlive) return true;
            }
            return false;
        }

        private Landable NearestInRange(out float bestDist)
        {
            Landable best = null;
            bestDist = float.MaxValue;
            Vector3 shipPos = ship.ShipPosition;
            foreach (var l in landables)
            {
                if (l?.target == null) continue;
                // Sample live: landables orbit (planets) or could move, so a cached position goes stale.
                Vector3 universePos = universe != null
                    ? universe.InverseTransformPoint(l.target.position)
                    : l.target.position;
                float d = Vector3.Distance(shipPos, universePos);
                if (d <= l.approachRadius && d < bestDist)
                {
                    bestDist = d;
                    best = l;
                }
            }
            return best;
        }

        private void ShowPrompt(string msg)
        {
            if (promptText == null) return;
            promptText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
            if (!string.IsNullOrEmpty(msg)) promptText.text = msg;
        }
    }
}
