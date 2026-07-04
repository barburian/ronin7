using System.Collections;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Ambient free-roam for filler crowd NPCs: on enable, remembers its spawn spot and then
    /// forever picks a random point within <see cref="wanderRadius"/> on the XZ plane, walks to it,
    /// pauses, and repeats. Unlike <see cref="NpcWalker"/> (a scripted one-shot waypoint walker that
    /// needs per-scene transforms wired in the Inspector), this needs no wiring, so it can be baked
    /// straight into a prefab and dropped into any scene.
    ///
    /// Movement is plain transform interpolation (no collider/rigidbody) to stay cheap on Quest when
    /// many of these roam at once.
    /// </summary>
    public class NpcWander : MonoBehaviour
    {
        [Tooltip("Max distance from the spawn point the NPC will wander.")]
        [SerializeField] private float wanderRadius = 4f;

        [SerializeField] private float moveSpeed = 1.2f;

        [Tooltip("Rotate the NPC to face its direction of travel.")]
        [SerializeField] private bool faceTravel = true;

        [SerializeField] private float arriveThreshold = 0.15f;

        [Tooltip("Seconds to wait at each destination before picking the next one.")]
        [SerializeField] private Vector2 pauseRange = new Vector2(0.5f, 2.5f);

        private Vector3 home;

        private void OnEnable()
        {
            home = transform.position;
            NpcWalkAnimator.EnsureOn(gameObject);
            StartCoroutine(WanderRoutine());
        }

        private IEnumerator WanderRoutine()
        {
            while (true)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 dest = home + new Vector3(offset.x, 0f, offset.y);

                while (Vector3.Distance(transform.position, dest) > arriveThreshold)
                {
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

                float pauseTimer = 0f;
                float pauseDuration = Random.Range(pauseRange.x, pauseRange.y);
                while (pauseTimer < pauseDuration)
                {
                    pauseTimer += Time.deltaTime;
                    yield return null;
                }
            }
        }
    }
}
