using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Heat pocket: a trigger volume that sheds the player's <see cref="CryoChillController"/>
    /// chill while they stand inside it. Place over reactor vents, fires, or storm-shelter spots.
    /// Uses the inspector-assigned controller if set, otherwise finds it on whatever enters
    /// (the player rig carries the controller).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HeatVent : MonoBehaviour
    {
        [SerializeField] private CryoChillController controller;

        private void OnTriggerStay(Collider other)
        {
            var c = controller != null ? controller : other.GetComponentInParent<CryoChillController>();
            if (c != null) c.Warm(Time.deltaTime);
        }
    }
}
