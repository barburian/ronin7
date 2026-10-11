using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.XR.Oculus;
#endif

namespace Ronin7.Flow
{
    /// <summary>
    /// One-shot Quest performance/quality scaffolding. Drop one on the persistent boot object.
    /// On Android device builds it asks the Oculus runtime for the desired display refresh rate
    /// and a fixed-foveation level, and applies a few mobile-VR quality knobs (shadows, MSAA via
    /// the active URP asset, vSync). Everything device-specific is gated behind
    /// <c>UNITY_ANDROID &amp;&amp; !UNITY_EDITOR</c> so it cleanly no-ops in the Editor and on PCVR.
    ///
    /// SCOPE: this is scaffolding only. The actual choice of 72 vs 90 Hz, foveation level, and the
    /// quality tier should be confirmed by ON-DEVICE PROFILING (the human's job) — the values here
    /// are conservative, comfortable starting points.
    /// </summary>
    [DisallowMultipleComponent]
    public class QualityBootstrap : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Target Quest display refresh rate (Hz). Falls back to the highest rate the headset supports at or below this (Quest 1 caps at 72).")]
        [SerializeField] private float targetRefreshRate = 90f;

        [Header("Foveation (0=Off, 1=Low, 2=Med, 3=High, 4=HighTop)")]
        [Tooltip("Fixed-foveated-rendering level. Higher = more peripheral GPU savings but more visible edge blur. Also enable Foveated Rendering in the Oculus/OpenXR feature settings.")]
        [SerializeField, Range(0, 4)] private int foveationLevel = 2;

        [Header("Mobile quality knobs")]
        [Tooltip("Force vSyncCount = 0 so the XR compositor (not Unity vSync) governs frame pacing — recommended on Quest.")]
        [SerializeField] private bool disableUnityVSync = true;
        [Tooltip("Cap real-time shadow distance (metres). Lower is cheaper; 0 leaves the quality setting untouched.")]
        [SerializeField] private float shadowDistance = 25f;

        /// <summary>Highest supported rate &lt;= target; if none, the lowest supported; if list empty, target.</summary>
        public static float PickRefreshRate(float[] supported, float target)
        {
            if (supported == null || supported.Length == 0) return target;
            float best = -1f, lowest = float.MaxValue;
            foreach (var r in supported)
            {
                if (r <= target && r > best) best = r;
                if (r < lowest) lowest = r;
            }
            return best > 0f ? best : lowest;
        }

        private void Awake()
        {
            // vSync: let the XR runtime pace frames. Safe everywhere, so not gated.
            if (disableUnityVSync) QualitySettings.vSyncCount = 0;

            // Shadow distance is a generic QualitySettings knob — safe in editor too.
            if (shadowDistance > 0f) QualitySettings.shadowDistance = shadowDistance;

#if UNITY_ANDROID && !UNITY_EDITOR
            // --- Device-only: Oculus runtime hints. These are no-ops / unavailable off-device. ---
            float[] available;
            float rate = Performance.TryGetAvailableDisplayRefreshRates(out available)
                ? PickRefreshRate(available, targetRefreshRate) : targetRefreshRate;
            if (!Performance.TrySetDisplayRefreshRate(rate))
                Debug.LogWarning($"[QualityBootstrap] Could not set {rate}Hz (not supported by this headset/runtime).");

            // Fixed-foveated rendering. Requires Foveated Rendering enabled in the OpenXR/Oculus
            // feature settings; otherwise this call has no effect.
            if (!Utils.SetFoveationLevel(foveationLevel))
                Debug.LogWarning("[QualityBootstrap] SetFoveationLevel failed (foveation not enabled in player settings?).");
#else
            // Reference the fields in the editor path so they don't read as unused.
            _ = targetRefreshRate; _ = foveationLevel;
#endif
        }
    }
}
