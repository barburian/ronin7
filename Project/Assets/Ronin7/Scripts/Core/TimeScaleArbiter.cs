using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Sole owner of <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c> writes in the codebase. Effects
    /// that want to slow time (the Overdrive burst, the deflect slow-mo) register a scale request keyed
    /// by a <see cref="TimeScaleChannel"/> instead of snapshotting "the original" globals and writing
    /// them directly — two independent snapshot/restore cycles corrupt each other the moment the effects
    /// overlap: a short effect ending mid-burst snaps timeScale back to 1 early, or a second effect
    /// activating mid-slow-mo snapshots the already-scaled fixedDeltaTime as its "original" and later
    /// restores that corrupted value permanently (it is a global static, so the corruption bleeds across
    /// scenes).
    ///
    /// The effective scale applied to Unity is always <see cref="TimeScaleComposition.EffectiveScale"/>
    /// (MIN of active requests), and <c>Time.fixedDeltaTime</c> is always <c>baseline * effective</c> —
    /// derived from a baseline captured exactly once on first use, never re-derived from the (possibly
    /// already scaled) current value.
    /// </summary>
    public static class TimeScaleArbiter
    {
        private static readonly TimeScaleComposition composition = new TimeScaleComposition();
        private static float? baselineFixedDeltaTime;

        /// <summary>The scale currently applied to <c>Time.timeScale</c> (1f when no requests are active).</summary>
        public static float EffectiveScale => composition.EffectiveScale;

        /// <summary>
        /// Registers (or replaces — see <see cref="TimeScaleComposition.SetRequest"/>) <paramref name="channel"/>'s
        /// requested scale and immediately re-applies the composed result to <c>Time.*</c>.
        /// </summary>
        public static void SetRequest(TimeScaleChannel channel, float scale)
        {
            CaptureBaselineIfNeeded();
            composition.SetRequest(channel, scale);
            Apply();
        }

        /// <summary>
        /// Releases <paramref name="channel"/>'s request, if any, and re-applies the remaining
        /// composition (falling back to 1f / baseline once no requests remain).
        /// </summary>
        public static void ClearRequest(TimeScaleChannel channel)
        {
            composition.ClearRequest(channel);
            Apply();
        }

        private static void CaptureBaselineIfNeeded()
        {
            // Lazy first-touch capture rather than [RuntimeInitializeOnLoadMethod]: EditMode tests never
            // run that callback, and the first SetRequest is always the earliest safe point to read
            // Time.fixedDeltaTime before any effect has scaled it.
            if (!baselineFixedDeltaTime.HasValue) baselineFixedDeltaTime = Time.fixedDeltaTime;
        }

        private static void Apply()
        {
            float scale = composition.EffectiveScale;
            Time.timeScale = scale;
            if (baselineFixedDeltaTime.HasValue) Time.fixedDeltaTime = baselineFixedDeltaTime.Value * scale;
        }

        /// <summary>Test-only: forgets every active request and the captured baseline, so the next
        /// <see cref="SetRequest"/> re-captures <c>Time.fixedDeltaTime</c> fresh. Mirrors this codebase's
        /// static-registry teardown idiom (e.g. <c>EventBus.Clear()</c>).</summary>
        internal static void ResetForTests()
        {
            composition.Clear();
            baselineFixedDeltaTime = null;
        }
    }
}
