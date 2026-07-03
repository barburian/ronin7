using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Pure destination math for Phase-step (Ch10 "The Ledger of Rust" unlock — Sever/Ninja-2's freed
    /// blade-shadow, inherited by Echo): given the rig's current position and an aim direction, computes
    /// the candidate landing point up to <c>maxDistance</c> away. "Through matter" means the solver never
    /// raycasts or stops short at the first obstacle — it always returns the full-distance candidate in
    /// the given direction; the MonoBehaviour that drives it (<see cref="PhaseStepController"/>) is
    /// responsible for any ground/clamp validation against the live scene. Uses only the
    /// <see cref="Vector3"/> value type — no MonoBehaviour/scene dependency — mirroring
    /// <c>WeakpointSightMarkers</c>/<c>OverdriveLogic</c> so it is deterministic and unit-testable apart
    /// from the MonoBehaviour.
    /// </summary>
    public static class PhaseStepSolver
    {
        /// <summary>
        /// Computes the blink destination: <paramref name="origin"/> plus <paramref name="direction"/>
        /// normalized and scaled to <paramref name="maxDistance"/>. A near-zero direction is treated as
        /// "no blink" and returns <paramref name="origin"/> unchanged. <paramref name="maxDistance"/> is
        /// clamped to non-negative (a negative/misconfigured distance never blinks backward).
        /// </summary>
        public static Vector3 ComputeDestination(Vector3 origin, Vector3 direction, float maxDistance)
        {
            float distance = maxDistance < 0f ? 0f : maxDistance;
            if (direction.sqrMagnitude < 1e-6f) return origin;
            return origin + direction.normalized * distance;
        }
    }
}
