using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World.Story
{
    /// <summary>
    /// The playback's stealth threat inside a <see cref="MemoryDiveController"/> dive: a patrolling
    /// censor-shape that pushes the witness back out of the recording if it sees him. Ping-pongs
    /// between two waypoints on its own — <c>NpcWalker</c> only ever walks a target through its
    /// waypoints once on activation (a scripted one-shot relocation), which doesn't fit a repeating
    /// patrol, so this drives its own minimal Update-based movement instead.
    ///
    /// Detection is a pure static FOV + range check (<see cref="CanSee"/>) so it's unit-testable
    /// without a running scene. No raycast/occlusion — the playback's greybox geometry doesn't need it.
    /// On a sighting, invokes <see cref="OnSpotted"/> (wired by the builder to
    /// <see cref="MemoryDiveController.ResetToEntry"/>) with a short cooldown so the reset teleport
    /// can't cause an immediate re-fire before the player is clear of the sentinel's cone.
    /// </summary>
    public class RedactionSentinel : MonoBehaviour
    {
        [Header("Patrol")]
        [SerializeField] private Transform waypointA;
        [SerializeField] private Transform waypointB;
        [SerializeField] private float moveSpeed = 1.2f;

        [Header("Detection")]
        [Tooltip("The rig's head/camera transform — what the sentinel is trying to spot.")]
        [SerializeField] private Transform target;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float halfAngleDeg = 45f;
        [Tooltip("Seconds after firing onSpotted before it can fire again (covers the reset teleport).")]
        [SerializeField] private float spottedCooldown = 3f;

        [SerializeField] private UnityEvent onSpotted = new UnityEvent();

        /// <summary>Build-time hook: wire MemoryDiveController.ResetToEntry here.</summary>
        public UnityEvent OnSpotted => onSpotted;

        private Transform currentDestination;
        private float cooldownUntil;

        private void Awake()
        {
            currentDestination = waypointB != null ? waypointB : waypointA;
        }

        private void Update()
        {
            Patrol();
            CheckDetection();
        }

        private void Patrol()
        {
            if (waypointA == null || waypointB == null || currentDestination == null) return;

            transform.position = Vector3.MoveTowards(transform.position, currentDestination.position, moveSpeed * Time.deltaTime);

            Vector3 to = currentDestination.position - transform.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

            if (Vector3.Distance(transform.position, currentDestination.position) < 0.1f)
                currentDestination = currentDestination == waypointA ? waypointB : waypointA;
        }

        private void CheckDetection()
        {
            if (target == null || Time.time < cooldownUntil) return;
            if (!CanSee(transform.position, transform.forward, target.position, detectionRange, halfAngleDeg)) return;

            cooldownUntil = Time.time + spottedCooldown;
            onSpotted?.Invoke();
        }

        /// <summary>
        /// Pure FOV + range check: true if <paramref name="targetPos"/> is within <paramref name="range"/>
        /// of <paramref name="sentinelPos"/> and inside the forward cone of half-angle
        /// <paramref name="halfAngleDeg"/> around <paramref name="sentinelForward"/>. Dot-product test,
        /// no raycast/occlusion (greybox-simple, matches the playback's abstract geometry).
        /// </summary>
        public static bool CanSee(Vector3 sentinelPos, Vector3 sentinelForward, Vector3 targetPos, float range, float halfAngleDeg)
        {
            Vector3 to = targetPos - sentinelPos;
            float dist = to.magnitude;
            if (dist > range || dist < 0.0001f) return false;

            float cosHalfAngle = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
            float dot = Vector3.Dot(sentinelForward.normalized, to / dist);
            return dot >= cosHalfAngle;
        }
    }
}
