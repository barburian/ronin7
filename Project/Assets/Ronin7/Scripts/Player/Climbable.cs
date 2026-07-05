using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Marker for climbable geometry: any collider on (or under) a GameObject carrying this can be
    /// gripped by <see cref="WallClimbLocomotion"/>. Presence-is-the-switch, mirroring
    /// <see cref="ZeroGHandle"/> — level design opts surfaces in (climbing walls, pipes, ledges)
    /// and everything else stays ungrabbable.
    /// </summary>
    public class Climbable : MonoBehaviour
    {
    }
}
