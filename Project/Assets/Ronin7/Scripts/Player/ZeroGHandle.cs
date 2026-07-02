using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Marker component for zero-gravity grab handles. Attach to objects with a trigger collider
    /// that the player can grab and pull on while in zero-g combat volumes. The collider trigger
    /// is detected by <see cref="ZeroGGrabLocomotion"/> to anchor grabs.
    /// </summary>
    public class ZeroGHandle : MonoBehaviour
    {
    }
}
