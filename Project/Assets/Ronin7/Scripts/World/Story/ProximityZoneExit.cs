using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Publishes <see cref="ZoneCompleted"/> once when the player walks within
    /// <see cref="radius"/> of this transform — a walk-onto "launch pad" exit, in contrast to
    /// <see cref="StoryTransition"/> whose methods are wired to UI button clicks. Proximity is
    /// measured against the head camera, the same check <see cref="MissionDirector"/>'s
    /// ReachTrigger step uses.
    /// </summary>
    public class ProximityZoneExit : MonoBehaviour
    {
        [Tooltip("How close (meters) the player's head must be to trigger the exit.")]
        [SerializeField] private float radius = 1.2f;

        private bool fired;

        private void Update()
        {
            if (fired || Camera.main == null) return;

            if (Vector3.Distance(Camera.main.transform.position, transform.position) <= radius)
            {
                fired = true;
                EventBus.Publish(new ZoneCompleted());
            }
        }
    }
}
