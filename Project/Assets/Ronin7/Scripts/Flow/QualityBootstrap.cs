using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
#if UNITY_ANDROID || UNITY_EDITOR
// Unity.XR.MetaOpenXR only exists for Android (and the editor): a Windows player must not see it.
using UnityEngine.XR.OpenXR.Features.Meta;
#endif

namespace Ronin7.Flow
{
    /// <summary>
    /// One-shot Quest performance setup. Drop one on the persistent boot object.
    ///
    /// BUG-07: this used to call the <c>Unity.XR.Oculus</c> Performance/Utils APIs, which are no-ops
    /// while only the OpenXR loader is active — Quest stayed at its default refresh rate with no
    /// foveation. It now talks to the active loader's <see cref="XRDisplaySubsystem"/>: the refresh rate
    /// through the Meta OpenXR display extensions (DisplayUtilities feature), and fixed foveation through
    /// <see cref="XRDisplaySubsystem.foveatedRenderingLevel"/> (OpenXR Foveated Rendering feature, SRP
    /// Foveation API). Shadow distance is owned by the URP assets (Mobile_RPAsset: 0, PC_RPAsset: 20),
    /// not <c>QualitySettings</c>, which URP ignores.
    ///
    /// The code compiles on every platform (so the editor catches API breaks) but only acts on Android.
    /// The 90 Hz choice and the foveation levels are confirmed by on-device profiling (G6).
    /// </summary>
    [DisallowMultipleComponent]
    public class QualityBootstrap : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Target Quest display refresh rate (Hz). Falls back to the highest supported rate at or below this.")]
        [SerializeField] private float targetRefreshRate = 90f;

        [Header("Mobile quality knobs")]
        [Tooltip("Force vSyncCount = 0 so the XR compositor (not Unity vSync) governs frame pacing — recommended on Quest.")]
        [SerializeField] private bool disableUnityVSync = true;

        /// <summary>Frames to wait for the XR display subsystem to start before giving up.</summary>
        private const int MaxWaitFrames = 300;

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

        /// <summary>Fixed-foveation strength (0..1) per headset: Quest 3/3S/Pro have the GPU headroom
        /// to blur less of the periphery than Quest 2 needs to. Pure. P0-07 test seam.</summary>
        internal static float FoveationFor(GraphicsDirector.DeviceTier tier) =>
            tier == GraphicsDirector.DeviceTier.Quest3 ? 0.75f : 0.5f;

        /// <summary>P5-01: eye-texture scale per headset. Mobile_RPAsset renders at 1.0 (Quest 3 gets foveation +
        /// dynamic resolution); Quest 2 drops to 0.85 to stay in budget. Pure. P5-01 test seam.</summary>
        internal static float EyeResolutionScaleFor(GraphicsDirector.DeviceTier tier) =>
            tier == GraphicsDirector.DeviceTier.Quest2 ? 0.85f : 1f;

        private void Awake()
        {
            // vSync: let the XR runtime pace frames. Safe everywhere, so not gated.
            if (disableUnityVSync) QualitySettings.vSyncCount = 0;
            if (Application.platform == RuntimePlatform.Android) StartCoroutine(ConfigureQuestDisplay());
        }

        private IEnumerator ConfigureQuestDisplay()
        {
            XRDisplaySubsystem display = null;
            for (int i = 0; i < MaxWaitFrames && (display == null || !display.running); i++)
            {
                display = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
                if (display == null || !display.running) yield return null;
            }
            if (display == null || !display.running)
            {
                Debug.LogWarning("[QualityBootstrap] No running XR display subsystem; refresh rate and foveation left at defaults.");
                yield break;
            }

            float rate = targetRefreshRate;
#if UNITY_ANDROID || UNITY_EDITOR
            if (display.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out NativeArray<float> supported))
            {
                rate = PickRefreshRate(supported.ToArray(), targetRefreshRate);
                supported.Dispose();
            }
            if (!display.TryRequestDisplayRefreshRate(rate))
                Debug.LogWarning($"[QualityBootstrap] Refresh rate request for {rate} Hz was refused.");
#endif

            var tier = GraphicsDirector.DetectTier(Application.isMobilePlatform, SystemInfo.deviceModel);
            display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            display.foveatedRenderingLevel = FoveationFor(tier);
            XRSettings.eyeTextureResolutionScale = EyeResolutionScaleFor(tier);

            // The runtime applies the rate asynchronously; report what actually took effect.
            yield return null;
            display.TryGetDisplayRefreshRate(out float actual);
            Debug.Log($"[QualityBootstrap] {tier}: display {actual} Hz (requested {rate}), foveation {display.foveatedRenderingLevel:0.00}.");
        }
    }
}
