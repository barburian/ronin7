using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Shared obstacle probe for the transform-interpolation NPC movers (<see cref="NpcWander"/>,
    /// <see cref="StoryNpcWander"/>, <see cref="NpcWalker"/>). Those scripts deliberately move without
    /// colliders/rigidbodies (Quest-cheap), which let NPCs walk straight through props and walls; this
    /// adds the missing awareness with a single chest-height sphere cast per query instead of a
    /// NavMesh — no shipped scene has baked nav data, and baking would mean touching all 14 scenes
    /// (see the destructive-rebuild caveat in IMPROVEMENT-SUMMARY). Trigger volumes (talk zones,
    /// story triggers, door sensors) are ignored; hits inside the moving NPC's own hierarchy are
    /// skipped so an NPC collider can never block itself. Solid hits on other characters DO block —
    /// an NPC stopping short of the player is the wanted behavior.
    /// </summary>
    public static class NpcSteering
    {
        /// <summary>Chest height (m) the probe travels at — above floors/thresholds, below hanging props.</summary>
        public const float ProbeHeight = 0.6f;

        /// <summary>Probe radius (m) — roughly an NPC torso half-width.</summary>
        public const float ProbeRadius = 0.25f;

        /// <summary>How far ahead (m) the per-frame walk guard looks while an NPC is moving.</summary>
        public const float Lookahead = 0.45f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>
        /// True when solid geometry blocks the flat line from <paramref name="npc"/>'s position toward
        /// <paramref name="dest"/>. Checks the whole leg when <paramref name="maxDistance"/> is
        /// negative (destination validation), or just the next stretch when it is set (per-frame
        /// guard with <see cref="Lookahead"/>).
        /// </summary>
        public static bool PathBlocked(Transform npc, Vector3 dest, float maxDistance = -1f)
        {
            Vector3 flat = dest - npc.position;
            flat.y = 0f;
            float legLength = flat.magnitude;
            if (legLength < 0.001f) return false;

            float distance = maxDistance < 0f ? legLength : Mathf.Min(maxDistance, legLength);
            Vector3 origin = npc.position + Vector3.up * ProbeHeight;

            int count = Physics.SphereCastNonAlloc(origin, ProbeRadius, flat / legLength, Hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].transform;
                if (hit != null && !hit.IsChildOf(npc)) return true;
            }
            return false;
        }
    }
}
