using System.Collections;
using UnityEngine;
using Ronin7.Combat;
using Ronin7.Enemies;

namespace Ronin7.World
{
    /// <summary>
    /// Story NPC wandering with pause and enemy-aware behavior.
    /// Wanders within wanderRadius of an anchor point (captured at spawn).
    /// When Paused is set true or enemies are nearby, stops wandering, walks back to the anchor,
    /// and faces the camera. This is used during conversations and when threats approach.
    /// </summary>
    public class StoryNpcWander : MonoBehaviour
    {
        [Tooltip("Max distance from the spawn point the NPC will wander.")]
        [SerializeField] private float wanderRadius = 2.5f;

        [SerializeField] private float moveSpeed = 1.2f;

        [Tooltip("Rotate the NPC to face its direction of travel.")]
        [SerializeField] private bool faceTravel = true;

        [SerializeField] private float arriveThreshold = 0.15f;

        [Tooltip("Seconds to wait at each destination before picking the next one.")]
        [SerializeField] private Vector2 pauseRange = new Vector2(0.5f, 2.5f);

        [Tooltip("Radius within which nearby live enemies will pause the NPC automatically.")]
        [SerializeField] private float enemyPauseRadius = 12f;

        public bool Paused { get; set; }

        private Vector3 anchor;
        private bool enemiesNearby;
        private Coroutine wanderRoutine;
        private Coroutine enemyPauseRoutine;

        private static readonly WaitForSeconds EnemyPollInterval = new WaitForSeconds(0.5f); // ~2 Hz

        // Shared across every StoryNpcWander instance so a scene with N story NPCs doesn't run N
        // separate FindObjectsByType<Enemy> scans at 2Hz — the first instance to poll past the
        // interval rescans, the rest reuse its result.
        private static Enemy[] cachedEnemies = System.Array.Empty<Enemy>();
        private static float lastEnemyScanTime = -1f;
        private const float EnemyScanInterval = 0.5f;

        // Enter Play Mode (no domain reload) sessions keep static state across Play cycles, which
        // would carry a stale scan/timestamp into the next run (mirrors Health.Active's reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEnemyScanCache()
        {
            cachedEnemies = System.Array.Empty<Enemy>();
            lastEnemyScanTime = -1f;
        }

        private static Enemy[] GetCachedEnemies()
        {
            float now = Time.time;
            if (lastEnemyScanTime < 0f || now - lastEnemyScanTime >= EnemyScanInterval)
            {
                cachedEnemies = FindObjectsByType<Enemy>();
                lastEnemyScanTime = now;
            }
            return cachedEnemies;
        }

        private void Awake()
        {
            anchor = transform.position;
        }

        private void OnEnable()
        {
            NpcWalkAnimator.EnsureOn(gameObject);
            wanderRoutine = StartCoroutine(WanderRoutine());
            enemyPauseRoutine = StartCoroutine(EnemyPauseRoutine());
        }

        private void OnDisable()
        {
            if (wanderRoutine != null)
            {
                StopCoroutine(wanderRoutine);
                wanderRoutine = null;
            }
            if (enemyPauseRoutine != null)
            {
                StopCoroutine(enemyPauseRoutine);
                enemyPauseRoutine = null;
            }
        }

        private IEnumerator WanderRoutine()
        {
            while (true)
            {
                // If paused or enemies are nearby, walk back to anchor and face camera.
                bool shouldPause = Paused || enemiesNearby;
                if (shouldPause)
                {
                    yield return ReturnToAnchor();
                    yield return FaceCameraRoutine();
                }
                else
                {
                    // Pick a random destination within wanderRadius of the anchor.
                    Vector2 offset = Random.insideUnitCircle * wanderRadius;
                    Vector3 dest = anchor + new Vector3(offset.x, 0f, offset.y);

                    // Obstacle awareness (NpcSteering): skip legs that start blocked, abandon legs
                    // that become blocked — the outer loop pauses and picks a different spot.
                    bool legBlocked = NpcSteering.PathBlocked(transform, dest);

                    // Walk to destination.
                    while (!legBlocked && Vector3.Distance(transform.position, dest) > arriveThreshold)
                    {
                        // Check pause condition each frame.
                        if (Paused || enemiesNearby)
                        {
                            break;
                        }

                        if (NpcSteering.PathBlocked(transform, dest, NpcSteering.Lookahead))
                        {
                            break;
                        }

                        Vector3 to = dest - transform.position;
                        if (faceTravel)
                        {
                            Vector3 flat = new Vector3(to.x, 0f, to.z);
                            if (flat.sqrMagnitude > 0.0001f)
                            {
                                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 360f * Time.deltaTime);
                            }
                        }

                        transform.position = Vector3.MoveTowards(transform.position, dest, moveSpeed * Time.deltaTime);
                        yield return null;
                    }

                    // Pause at destination.
                    if (!Paused && !enemiesNearby)
                    {
                        float pauseTimer = 0f;
                        float pauseDuration = Random.Range(pauseRange.x, pauseRange.y);
                        while (pauseTimer < pauseDuration)
                        {
                            pauseTimer += Time.deltaTime;
                            yield return null;
                        }
                    }
                }

                yield return null;
            }
        }

        private IEnumerator ReturnToAnchor()
        {
            while (Vector3.Distance(transform.position, anchor) > arriveThreshold)
            {
                // Blocked on the way home — usually the approaching PLAYER's own capsule tripping
                // the probe: wait in place and resume when the path clears. (An early yield-break
                // here permanently abandoned the return, so a story beat could play with the NPC
                // stranded up to wanderRadius from its scripted spot.)
                if (NpcSteering.PathBlocked(transform, anchor, NpcSteering.Lookahead))
                {
                    yield return null;
                    continue;
                }

                Vector3 to = anchor - transform.position;
                if (faceTravel)
                {
                    Vector3 flat = new Vector3(to.x, 0f, to.z);
                    if (flat.sqrMagnitude > 0.0001f)
                    {
                        Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 360f * Time.deltaTime);
                    }
                }

                transform.position = Vector3.MoveTowards(transform.position, anchor, moveSpeed * Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator FaceCameraRoutine()
        {
            while (Paused || enemiesNearby)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    // Face the camera (yaw only).
                    Vector3 dirToCamera = cam.transform.position - transform.position;
                    Vector3 flatDir = new Vector3(dirToCamera.x, 0f, dirToCamera.z);
                    if (flatDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetLook = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetLook, 360f * Time.deltaTime);
                    }
                }
                yield return null;
            }
        }

        private IEnumerator EnemyPauseRoutine()
        {
            while (true)
            {
                // Slow ~2 Hz poll: cache the result so the per-frame movement loops never call
                // FindObjectsByType (which allocates). Independent of the user-set Paused flag.
                enemiesNearby = AreEnemiesNearby();
                yield return EnemyPollInterval;
            }
        }

        private bool AreEnemiesNearby()
        {
            var cam = Camera.main;
            if (cam == null) return false;

            var enemies = GetCachedEnemies();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeSelf) continue;

                Health health = enemy.GetComponent<Health>();
                if (health == null || !health.IsAlive) continue;

                Vector3 toEnemy = enemy.transform.position - cam.transform.position;
                toEnemy.y = 0f;
                if (toEnemy.magnitude <= enemyPauseRadius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
