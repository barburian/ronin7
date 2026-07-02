using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Recentres the player horizontally. <see cref="RecenterRig"/> is the reliable path: it moves
    /// the XR Origin root so the head sits over the seat and faces forward, which works on every
    /// runtime. <see cref="Recenter"/> is the XR-subsystem recenter — kept as a best-effort, but on
    /// Quest in Floor/Stage tracking it reports success yet moves nothing, which is why the menu
    /// button uses <see cref="RecenterRig"/> instead.
    /// </summary>
    public static class XRRecenterUtility
    {
        // Reused across calls to avoid per-press allocations.
        private static readonly List<XRInputSubsystem> Buffer = new List<XRInputSubsystem>(4);

        /// <summary>
        /// Manually recentres the rig horizontally by moving the XR Origin root so the player's head
        /// sits over the seat (world-origin XZ) and faces +Z, leaving head height untouched. Works on
        /// every runtime because it only moves a transform we own. Scenes author the seated rig at the
        /// world origin facing +Z (the menu/content sits ahead along +Z), so that is the recenter target.
        /// </summary>
        public static void RecenterRig()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.Origin == null || rig.Head == null)
            {
                Debug.LogWarning("[XRRecenter] No VRRig with origin/head — rig recenter skipped " +
                                 "(expected in Editor without a connected headset).");
                return;
            }

            Transform root = rig.Origin;
            Transform head = rig.Head;

            // 1) Yaw: rotate the rig around the head so the head's horizontal forward faces +Z.
            //    Rotating around the head's world point keeps the head's position fixed.
            Vector3 headForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (headForward.sqrMagnitude > 1e-4f)
            {
                float yaw = Vector3.SignedAngle(headForward, Vector3.forward, Vector3.up);
                root.RotateAround(head.position, Vector3.up, yaw);
            }

            // 2) Position: slide the rig horizontally so the head sits over the world-origin XZ.
            //    The delta has y = 0, so head (and rig) height is preserved.
            Vector3 headXZ = new Vector3(head.position.x, 0f, head.position.z);
            root.position -= headXZ;

            Debug.Log("[XRRecenter] Rig recentred (head moved to seat, facing +Z).");
        }

        public static bool Recenter()
        {
            Buffer.Clear();
            SubsystemManager.GetSubsystems(Buffer);

            if (Buffer.Count == 0)
            {
                Debug.LogWarning("[XRRecenter] No XRInputSubsystem found — recenter ignored " +
                                 "(expected in Editor without a connected headset).");
                return false;
            }

            bool any = false;
            foreach (var sub in Buffer)
            {
                if (sub != null && sub.TryRecenter()) any = true;
            }

            if (any) Debug.Log("[XRRecenter] Tracking origin recentred.");
            else Debug.LogWarning("[XRRecenter] No subsystem accepted TryRecenter().");
            return any;
        }
    }
}
