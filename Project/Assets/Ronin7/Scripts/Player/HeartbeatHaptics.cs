using Ronin7.Combat;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// Low-health heartbeat: pulses both controllers on a <see cref="HeartbeatPulser"/> cadence that
    /// quickens and strengthens as health drops below <see cref="lowHealthFraction"/>. Purely a
    /// health-driven haptic layer — no VFX/audio, no camera shake (VR-safe).
    /// </summary>
    public class HeartbeatHaptics : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;

        [SerializeField] private float lowHealthFraction = 0.35f;
        [SerializeField] private float minInterval = 0.35f;
        [SerializeField] private float maxInterval = 1.0f;
        [SerializeField] private float minAmplitude = 0.15f;
        [SerializeField] private float maxAmplitude = 0.6f;
        [SerializeField] private float pulseDuration = 0.06f;

        private HeartbeatPulser pulser;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponent<Health>();
            pulser = new HeartbeatPulser(lowHealthFraction, minInterval, maxInterval, minAmplitude, maxAmplitude);
        }

        private void Update()
        {
            if (playerHealth == null || playerHealth.Max <= 0f) return;

            if (pulser.TryBeat(playerHealth.Current / playerHealth.Max, Time.unscaledTime, out float amplitude))
                PulseBoth(amplitude, pulseDuration);
        }

        private static void PulseBoth(float amplitude, float duration)
        {
            Haptics.Pulse(XRNode.LeftHand, amplitude, duration);
            Haptics.Pulse(XRNode.RightHand, amplitude, duration);
        }
    }
}
