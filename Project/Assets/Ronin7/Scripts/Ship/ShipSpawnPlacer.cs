using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Placed in the space scene by the scene builder. Positions the ship at the last-visited planet
    /// on spawn. Runs in Start() after PlanetOrbit has positioned planets in Awake, and ShipController
    /// spawns at origin by default. If no planet is recorded or the last-visited planet is not in the
    /// landing list, origin spawn is the default.
    /// </summary>
    public class ShipSpawnPlacer : MonoBehaviour
    {
        [SerializeField] private ShipController ship;
        [SerializeField] private Transform universe;
        [SerializeField] private LandingApproach landing;
        [Tooltip("Spawn distance = approachRadius * this; >1 keeps the ship outside the landing radius.")]
        [SerializeField] private float standoffMultiplier = 1.5f;

        private void Start()
        {
            if (ship == null || universe == null || landing == null || string.IsNullOrEmpty(CampaignState.LastPlanetScene))
            {
                return;
            }

            LandingApproach.Landable found = null;
            foreach (var l in landing.Landables)
            {
                if (l != null && l.target != null && l.destinationScene == CampaignState.LastPlanetScene)
                {
                    found = l;
                    break;
                }
            }

            if (found == null)
            {
                return;
            }

            // Sample the planet's LIVE universe-local position (planets orbit).
            Vector3 planetPos = universe.InverseTransformPoint(found.target.position);
            float standoff = found.approachRadius * standoffMultiplier;
            ComputeSpawnPose(planetPos, standoff, out Vector3 spawnPos, out Quaternion spawnRot);
            ship.Teleport(spawnPos, spawnRot);
        }

        /// <summary>Position standoff units from the planet on its origin-facing side, looking at the planet.</summary>
        public static void ComputeSpawnPose(Vector3 planetPos, float standoff, out Vector3 position, out Quaternion rotation)
        {
            Vector3 toOrigin = planetPos.sqrMagnitude < 0.0001f ? Vector3.back : (-planetPos).normalized;
            position = planetPos + toOrigin * standoff;
            rotation = Quaternion.LookRotation((planetPos - position).normalized, Vector3.up);
        }
    }
}
