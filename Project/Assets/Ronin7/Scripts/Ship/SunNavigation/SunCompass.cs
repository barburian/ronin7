using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Pillar 1 of the sun-navigation system (see <c>Docs/SunNavigation-Design.md</c>): the sun as a
    /// persistent compass. Pure geometry helpers — no Unity lifecycle, fully unit-tested — so HUD /
    /// objective-arrow / galaxy-map consumers can phrase headings relative to the always-bright sun.
    ///
    /// FRAME OF REFERENCE: pass directions in any single consistent frame. In the moving-universe model
    /// the ship nose is the rig's forward and the sun renders at its swept world position, so callers
    /// typically pass the rig forward and (sunWorldPos - rigPos) — both world-space, which is correct.
    /// </summary>
    public static class SunCompass
    {
        /// <summary>
        /// Signed heading (−180..180°) from <paramref name="forward"/> to <paramref name="toSun"/>,
        /// measured in the horizontal plane defined by <paramref name="up"/>: 0 = sun dead ahead,
        /// +90 = sun directly to starboard (right), ±180 = sun directly behind. Both vectors are
        /// flattened onto the up-plane first so elevation never contaminates the bearing.
        /// </summary>
        public static float BearingDeg(Vector3 forward, Vector3 toSun, Vector3 up)
        {
            Vector3 f = Vector3.ProjectOnPlane(forward, up);
            Vector3 s = Vector3.ProjectOnPlane(toSun, up);
            // Degenerate look/aim (parallel to up) has no horizontal heading — report dead-ahead.
            if (f.sqrMagnitude < 1e-8f || s.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.SignedAngle(f, s, up);
        }

        /// <summary>
        /// Elevation of the sun above the horizon plane (−90..90°): +90 = straight up,
        /// 0 = on the horizon, −90 = straight down, relative to <paramref name="up"/>.
        /// </summary>
        public static float ElevationDeg(Vector3 toSun, Vector3 up)
        {
            Vector3 s = toSun.normalized;
            Vector3 u = up.normalized;
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(s, u), -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
