using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Eye-height calibration for the seated/standing rig. The rig tracks the floor
    /// (<see cref="Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor"/>), so a raw recenter
    /// never changes how high the player's viewpoint sits. This shifts the rig's dedicated
    /// <see cref="VRRig.EyeHeightRoot"/> Y (which XROrigin does not manage, unlike the Camera
    /// Offset) so the player's current head maps to a target eye height.
    ///
    /// <see cref="Calibrate"/> only reads the rig and publishes the computed offset on the
    /// <see cref="EventBus"/>; the settings service (Audio assembly) owns persistence and is the
    /// single caller of <see cref="ApplyOffset"/>. This keeps Player free of an Audio reference
    /// (no assembly cycle), mirroring how <see cref="XRRecenterUtility"/> stays dependency-light.
    /// </summary>
    public static class XREyeHeightCalibrator
    {
        /// <summary>Pure offset math: how far to raise the rig so a head at <paramref name="trackedHeadHeight"/>
        /// reads as <paramref name="targetEyeHeight"/> above the floor.</summary>
        public static float ComputeOffset(float trackedHeadHeight, float targetEyeHeight)
            => targetEyeHeight - trackedHeadHeight;

        /// <summary>Writes the calibrated Y onto the live rig's Camera Offset. Sole transform writer.
        /// Silent no-op when there is no rig yet (this runs on every scene load, before some scenes
        /// have a rig) — the no-headset case is reported by <see cref="Calibrate"/> instead.</summary>
        public static void ApplyOffset(float offsetY)
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.EyeHeightRoot == null) return;

            var p = rig.EyeHeightRoot.localPosition;
            p.y = offsetY;
            rig.EyeHeightRoot.localPosition = p;
        }

        /// <summary>
        /// Reads the current head pose, computes the eye-height-root Y that places the viewpoint at
        /// <paramref name="targetEyeHeight"/>, and publishes <see cref="EyeHeightCalibrated"/>.
        /// Idempotent: the current offset is removed before measuring, so repeated presses converge.
        /// Returns false (with a warning) when there is no rig/head — expected in the Editor without a headset.
        /// </summary>
        public static bool Calibrate(float targetEyeHeight)
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.Head == null || rig.Origin == null || rig.EyeHeightRoot == null)
            {
                Debug.LogWarning("[EyeHeight] No VRRig with head/origin/eye-height root — eye-height calibration skipped " +
                                 "(expected in Editor without a connected headset).");
                return false;
            }

            // Device-tracked head height above the floor, with any previously-applied offset removed.
            float currentOffsetY = rig.EyeHeightRoot.localPosition.y;
            float trackedHeadHeight = rig.Head.position.y - rig.Origin.position.y - currentOffsetY;

            float newOffsetY = ComputeOffset(trackedHeadHeight, targetEyeHeight);
            EventBus.Publish(new EyeHeightCalibrated(newOffsetY));
            Debug.Log($"[EyeHeight] Calibrated to {targetEyeHeight:0.00}m (offset {newOffsetY:+0.00;-0.00}m).");
            return true;
        }
    }
}
