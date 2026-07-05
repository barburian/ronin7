using System.Collections.Generic;

namespace Ronin7.Core
{
    /// <summary>
    /// Pure composition of active <see cref="TimeScaleChannel"/> requests: the effective scale is
    /// MIN(active requests), or 1f when none are active. No UnityEngine dependency — mirrors this
    /// codebase's Logic-class idiom (e.g. <c>OverdriveLogic</c>, <c>HeatLogic</c>) so composition is
    /// deterministic and unit-testable apart from <see cref="TimeScaleArbiter"/>, the thin applier that
    /// pushes the result to <c>Time.timeScale</c>/<c>Time.fixedDeltaTime</c>.
    /// </summary>
    internal sealed class TimeScaleComposition
    {
        private readonly Dictionary<TimeScaleChannel, float> requests = new Dictionary<TimeScaleChannel, float>();

        /// <summary>MIN of all active requests; 1f (no slow-mo) when none are active.</summary>
        public float EffectiveScale
        {
            get
            {
                if (requests.Count == 0) return 1f;
                float min = float.PositiveInfinity;
                foreach (float scale in requests.Values)
                {
                    if (scale < min) min = scale;
                }
                return min;
            }
        }

        /// <summary>Registers <paramref name="channel"/>'s requested scale. Re-entrant: a second call
        /// for the same channel replaces its value rather than stacking a second entry.</summary>
        public void SetRequest(TimeScaleChannel channel, float scale) => requests[channel] = scale;

        /// <summary>Releases <paramref name="channel"/>'s request, if any. No-op if it wasn't active.</summary>
        public void ClearRequest(TimeScaleChannel channel) => requests.Remove(channel);

        /// <summary>Releases every active request (test teardown / arbiter reset).</summary>
        public void Clear() => requests.Clear();
    }
}
