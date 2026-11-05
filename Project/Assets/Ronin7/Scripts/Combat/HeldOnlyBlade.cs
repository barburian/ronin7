using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// P9-02c: keeps a blade inert while its Grabbable is not held. A holstered polearm moves with the player's
    /// walking (BladeDamager speed is world-space) and a holstered shield must not parry or absorb bolts, so the
    /// trigger collider and the damager (which also owns the swept casts) follow <see cref="Grabbable.IsHeld"/>.
    /// Only the polearm and shield carry this; the katanas keep their always-armed behaviour.
    /// </summary>
    [RequireComponent(typeof(BladeDamager), typeof(Collider))]
    public class HeldOnlyBlade : MonoBehaviour
    {
        private Grabbable grabbable;

        private void OnEnable()
        {
            grabbable = GetComponentInParent<Grabbable>();
            if (grabbable != null)
            {
                grabbable.Grabbed += OnGrabbed;
                grabbable.Released += OnReleased;
            }
            Apply(grabbable != null && grabbable.IsHeld);
        }

        private void OnDisable()
        {
            if (grabbable == null) return;
            grabbable.Grabbed -= OnGrabbed;
            grabbable.Released -= OnReleased;
        }

        private void OnGrabbed(Transform hand) => Apply(true);
        private void OnReleased() => Apply(false);

        internal void Apply(bool held)
        {
            GetComponent<Collider>().enabled = held;
            GetComponent<BladeDamager>().enabled = held;
        }
    }
}
