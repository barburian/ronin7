using System.Collections;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Walks a <see cref="target"/> transform (e.g. Kessler) through a list of waypoints once,
    /// starting the moment this GameObject is enabled. Used for the scripted "Kessler leads the way
    /// to the command room" beat: the walker GameObject starts inactive and a
    /// <c>MissionStepKind.Trigger</c> step activates it.
    ///
    /// Movement is plain transform interpolation (the NPC has no collider/rigidbody), which keeps it
    /// cheap and predictable on Quest.
    /// </summary>
    public class NpcWalker : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float moveSpeed = 1.6f;

        [Tooltip("Rotate the NPC to face its direction of travel.")]
        [SerializeField] private bool faceTravel = true;

        [SerializeField] private float arriveThreshold = 0.15f;

        private bool hasWalked;

        private void OnEnable()
        {
            if (hasWalked) return;
            hasWalked = true;

            // Disable StoryNpcWander if the target has one, so scripted walking takes priority.
            if (target != null)
            {
                StoryNpcWander wander = target.GetComponent<StoryNpcWander>();
                if (wander != null)
                {
                    wander.enabled = false;
                }
                NpcWalkAnimator.EnsureOn(target.gameObject);
            }

            StartCoroutine(WalkRoutine());
        }

        private IEnumerator WalkRoutine()
        {
            if (target == null || waypoints == null)
            {
                yield break;
            }

            foreach (Transform wp in waypoints)
            {
                if (wp == null) continue;

                while (Vector3.Distance(target.position, wp.position) > arriveThreshold)
                {
                    Vector3 to = wp.position - target.position;
                    if (faceTravel && to.sqrMagnitude > 0.0001f)
                    {
                        Vector3 flat = new Vector3(to.x, 0f, to.z);
                        if (flat.sqrMagnitude > 0.0001f)
                        {
                            Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                            target.rotation = Quaternion.RotateTowards(target.rotation, look, 360f * Time.deltaTime);
                        }
                    }

                    target.position = Vector3.MoveTowards(target.position, wp.position, moveSpeed * Time.deltaTime);
                    yield return null;
                }
            }
        }
    }
}
