using UnityEngine;

namespace Ronin7.Player
{
    /// <summary>
    /// Pure stateful low-health heartbeat cadence: decides how often, and how strongly, to pulse as
    /// health falls below <c>lowHealthFraction</c>. No UnityEngine.Time dependency (mirrors
    /// <c>EchoCalloutSelector</c>'s "caller supplies now" idiom) so it's deterministic and unit-testable.
    /// </summary>
    public class HeartbeatPulser
    {
        private readonly float lowHealthFraction;
        private readonly float minInterval;
        private readonly float maxInterval;
        private readonly float minAmplitude;
        private readonly float maxAmplitude;

        private float nextBeatAt = float.NegativeInfinity;

        public HeartbeatPulser(float lowHealthFraction, float minInterval, float maxInterval,
            float minAmplitude, float maxAmplitude)
        {
            this.lowHealthFraction = lowHealthFraction;
            this.minInterval = minInterval;
            this.maxInterval = maxInterval;
            this.minAmplitude = minAmplitude;
            this.maxAmplitude = maxAmplitude;
        }

        /// <summary>
        /// Attempt a beat at <paramref name="nowSeconds"/> for the given <paramref name="healthFraction"/>
        /// (Current/Max, in [0,1]). Returns false for dead or at-or-above-threshold health, which also
        /// resets the cadence so a later drop below threshold beats immediately. Otherwise returns false
        /// until the urgency-scaled interval since the last beat has elapsed.
        /// </summary>
        public bool TryBeat(float healthFraction, float nowSeconds, out float amplitude)
        {
            amplitude = 0f;

            if (healthFraction <= 0f || healthFraction >= lowHealthFraction)
            {
                nextBeatAt = float.NegativeInfinity;
                return false;
            }

            if (nowSeconds < nextBeatAt) return false;

            float urgency = 1f - Mathf.Clamp01(healthFraction / lowHealthFraction);
            amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, urgency);
            nextBeatAt = nowSeconds + Mathf.Lerp(maxInterval, minInterval, urgency);
            return true;
        }
    }
}
