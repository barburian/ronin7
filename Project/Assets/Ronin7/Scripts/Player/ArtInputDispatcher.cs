using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// P4-03: right trigger fires the Weapon Art (sword held), left trigger the Off-hand Art (empty
    /// left hand). Thin shell over <see cref="ArtSlots"/>; wired to Gameplay/WeaponArt and
    /// Gameplay/OffhandArt by XRRigBuilder.
    /// </summary>
    public class ArtInputDispatcher : MonoBehaviour
    {
        [SerializeField] private InputActionReference weaponArtAction;
        [SerializeField] private InputActionReference offhandArtAction;
        [SerializeField] private Grabber rightGrabber;
        [SerializeField] private Grabber leftGrabber;

        private void OnEnable()
        {
            weaponArtAction?.action?.Enable();
            offhandArtAction?.action?.Enable();
        }

        private void OnDisable()
        {
            weaponArtAction?.action?.Disable();
            offhandArtAction?.action?.Disable();
        }

        private void Update()
        {
            bool right = rightGrabber != null && rightGrabber.IsHolding;
            bool left = leftGrabber != null && leftGrabber.IsHolding;
            if (weaponArtAction?.action != null && weaponArtAction.action.WasPressedThisFrame())
                ArtSlots.TryFire(ArtSlot.Weapon, right, left);
            if (offhandArtAction?.action != null && offhandArtAction.action.WasPressedThisFrame())
                ArtSlots.TryFire(ArtSlot.Offhand, right, left);
        }
    }
}
