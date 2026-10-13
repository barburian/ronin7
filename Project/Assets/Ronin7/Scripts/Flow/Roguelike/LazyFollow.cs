using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// P1-13 (BUG-16): "lazy follow" for worldspace menu panels. The panel stays put while the player
    /// looks around; only when it drifts more than <see cref="RecenterAngle"/> degrees out of the
    /// (yaw-only) view direction — or the player walks/crouches away from it — does it ease to a fresh
    /// pose over <see cref="Duration"/> seconds, at head height minus <see cref="HeightDrop"/>, facing the head.
    /// Never snaps mid-play, so it cannot induce the sickness a continuously re-facing panel would.
    /// </summary>
    public sealed class LazyFollow
    {
        public const float RecenterAngle = 35f;
        public const float Duration = 0.25f;
        public const float HeightDrop = 0.1f;
        const float DriftMeters = 0.5f;

        readonly float distance;
        readonly float yawOffsetDeg;
        Vector3 lastFwd = Vector3.forward;
        bool moving;
        float t;
        Vector3 fromPos, toPos;
        Quaternion fromRot, toRot;

        /// <param name="yawOffsetDeg">Where the panel sits relative to the view direction (the summary
        /// panel parks beside the menu rather than dead ahead).</param>
        public LazyFollow(float distance, float yawOffsetDeg = 0f)
        {
            this.distance = distance;
            this.yawOffsetDeg = yawOffsetDeg;
        }

        /// <summary>View direction flattened onto the horizontal plane. When the head looks straight
        /// up/down that degenerates, so fall back to the head's up axis (which points forward-of-body
        /// when looking down, backward when looking up), then to <paramref name="lastGood"/>.</summary>
        public static Vector3 FlatForward(Vector3 camForward, Vector3 camUp, Vector3 lastGood)
        {
            var f = Vector3.ProjectOnPlane(camForward, Vector3.up);
            if (f.sqrMagnitude > 1e-4f) return f.normalized;
            f = Vector3.ProjectOnPlane(camForward.y > 0f ? -camUp : camUp, Vector3.up);
            return f.sqrMagnitude > 1e-4f ? f.normalized : lastGood;
        }

        /// <summary>True when the panel is more than <paramref name="thresholdDeg"/> off the desired
        /// view direction. Both vectors are horizontal; a zero vector never triggers.</summary>
        public static bool ShouldRecenter(Vector3 desiredFlat, Vector3 toPanelFlat, float thresholdDeg = RecenterAngle) =>
            desiredFlat.sqrMagnitude > 1e-6f && toPanelFlat.sqrMagnitude > 1e-6f
            && Vector3.Angle(desiredFlat, toPanelFlat) > thresholdDeg;

        /// <summary>Target pose: <paramref name="distance"/> out along the yaw-offset flat forward, at
        /// head height minus <see cref="HeightDrop"/>, looking away from the head.</summary>
        public static void FollowPose(Vector3 head, Vector3 flatForward, float yawOffsetDeg, float distance,
            out Vector3 position, out Quaternion rotation)
        {
            var dir = Quaternion.AngleAxis(yawOffsetDeg, Vector3.up) * flatForward;
            position = head + dir * distance;
            position.y = head.y - HeightDrop;
            rotation = Quaternion.LookRotation(position - head, Vector3.up);
        }

        /// <summary>Place the panel at its rest pose immediately (factory build).</summary>
        public void Snap(Transform panel, Transform head)
        {
            lastFwd = FlatForward(head.forward, head.up, lastFwd);
            FollowPose(head.position, lastFwd, yawOffsetDeg, distance, out var p, out var r);
            panel.SetPositionAndRotation(p, r);
            moving = false;
        }

        public void Tick(Transform panel, Transform head, float dt)
        {
            lastFwd = FlatForward(head.forward, head.up, lastFwd);
            if (moving)
            {
                t += dt;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Duration));
                panel.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, s), Quaternion.Slerp(fromRot, toRot, s));
                if (t >= Duration) moving = false;
                return;
            }

            var offset = panel.position - head.position;
            var toPanelFlat = Vector3.ProjectOnPlane(offset, Vector3.up);
            var desired = Quaternion.AngleAxis(yawOffsetDeg, Vector3.up) * lastFwd;
            bool drifted = Mathf.Abs(toPanelFlat.magnitude - Mathf.Abs(distance)) > DriftMeters
                           || Mathf.Abs(offset.y + HeightDrop) > 0.2f;
            if (!drifted && !ShouldRecenter(desired, toPanelFlat)) return;

            fromPos = panel.position; fromRot = panel.rotation;
            FollowPose(head.position, lastFwd, yawOffsetDeg, distance, out toPos, out toRot);
            t = 0f;
            moving = true;
        }
    }
}
