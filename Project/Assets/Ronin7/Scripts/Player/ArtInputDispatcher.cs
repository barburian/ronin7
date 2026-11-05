using Ronin7.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ronin7.Player
{
    /// <summary>
    /// P4-03: main-hand trigger fires the Weapon Art (sword held), off-hand trigger the Off-hand Art (empty
    /// off hand; swapped when left-handed). Thin shell over <see cref="ArtSlots"/>; wired to Gameplay/WeaponArt and
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
            ArtSlots.WeaponHeld = ArtSlots.OffhandHeld = false;
        }

        private void Update()
        {
            // A15.3: left-handed swaps which trigger/grabber is the weapon (main) hand. The input asset is not rewired.
            bool lh = GameSettings.Active.LeftHanded;
            ArtSlots.Hands(lh, rightGrabber != null && rightGrabber.IsHolding, leftGrabber != null && leftGrabber.IsHolding,
                out bool main, out bool off);
            var weaponAct = (lh ? offhandArtAction : weaponArtAction)?.action;
            var offAct = (lh ? weaponArtAction : offhandArtAction)?.action;
            bool open = !ArtSlots.Blocked;
            ArtSlots.WeaponHeld = open && ArtSlots.CanFire(ArtSlot.Weapon, main, off)
                                  && weaponAct != null && weaponAct.IsPressed();
            ArtSlots.OffhandHeld = open && ArtSlots.CanFire(ArtSlot.Offhand, main, off)
                                   && offAct != null && offAct.IsPressed();
            if (weaponAct != null && weaponAct.WasPressedThisFrame())
                ArtSlots.TryFire(ArtSlot.Weapon, main, off);
            if (offAct != null && offAct.WasPressedThisFrame())
                ArtSlots.TryFire(ArtSlot.Offhand, main, off);
        }
    }
}
