using Ronin7.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Ronin7.Player
{
    /// <summary>
    /// P2-09 left-handed mode, applied to whatever rig the current scene built (called by the settings
    /// service on every Apply, so it runs live and on scene load). Stateless and idempotent:
    /// <list type="bullet">
    /// <item>the run wrist HUD moves to the off-hand controller;</item>
    /// <item>the scene's UI ray (built on the right controller) casts from the main hand and presses
    /// with that hand's Select — the component stays put, only its ray origin and press action swap;</item>
    /// <item>the katana holster mirrors to the other hip.</item>
    /// </list>
    /// Locomotion sticks are NOT swapped: move/teleport stays on the left stick, turn on the right.
    /// </summary>
    public static class HandednessRig
    {
        /// <summary>Name the roguelike entry-point patcher gives the RunArena wrist HUD.</summary>
        public const string WristHudName = "RunWristHud";
        private const string RayOriginName = "MainHandRayOrigin";

        /// <summary>The off hand: carries the wrist HUD and the off-hand haptics. Left for a
        /// right-handed player, right for a left-handed one.</summary>
        public static XRNode OffHand(bool leftHanded) => leftHanded ? XRNode.RightHand : XRNode.LeftHand;

        public static void Apply(bool leftHanded)
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.LeftHand == null || rig.RightHand == null) return;

            Transform offHand = OffHand(leftHanded) == XRNode.LeftHand ? rig.LeftHand : rig.RightHand;
            Transform mainHand = offHand == rig.LeftHand ? rig.RightHand : rig.LeftHand;

            // HUD → off hand (keeps its wrist-local pose, which is centred on x).
            Transform hud = rig.LeftHand.Find(WristHudName);
            if (hud == null) hud = rig.RightHand.Find(WristHudName);
            if (hud != null && hud.parent != offHand) hud.SetParent(offHand, false);

            var holster = rig.GetComponent<KatanaHolster>();
            if (holster != null) holster.SetMirrored(leftHanded);
            // P4-08 Twin Wakizashi: the left blade sits on the opposite hip.
            var offAnchor = rig.transform.Find(KatanaHolster.OffhandAnchorName);
            var offHolster = offAnchor != null ? offAnchor.GetComponent<KatanaHolster>() : null;
            if (offHolster != null) offHolster.SetMirrored(!leftHanded);

            var ray = rig.RightHand.GetComponent<XRRayInteractor>();
            if (ray == null) return;
            ray.interactionLayers &= ~TeleportRig.LayerMask; // the UI ray must never select the teleport floor
            ray.rayOriginTransform = EnsureChild(mainHand, RayOriginName);

            var press = ray.uiPressInput.inputActionReferencePerformed;
            if (press == null || press.asset == null) return;
            var action = press.asset.FindAction(mainHand == rig.LeftHand ? "Left Hand/Select" : "Right Hand/Select");
            if (action != null && press.action != action)
                ray.uiPressInput.inputActionReferencePerformed = InputActionReference.Create(action);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}
