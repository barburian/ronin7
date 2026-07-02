using Ronin7.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ronin7.Flow
{
    /// <summary>
    /// Persistent owner of the runtime graphics tier. Drop one on the persistent boot object next to
    /// <c>AudioDirector</c>/<c>SettingsService</c>/<c>QualityBootstrap</c>. It listens for
    /// <see cref="SettingsChanged"/> on the <see cref="EventBus"/> and translates the chosen
    /// <see cref="GraphicsQuality"/> into the one place the look dials up or down:
    /// <list type="bullet">
    ///   <item>full-screen Bloom strength (the one heavy post effect on Quest),</item>
    ///   <item><see cref="GraphicsRuntime.ParticleScale"/> read by the combat VFX storm,</item>
    ///   <item><see cref="GraphicsRuntime.BladeLightsEnabled"/> read by the plasma-blade lights.</item>
    /// </list>
    ///
    /// It owns its OWN high-priority global Volume rather than mutating a shared profile asset, so the
    /// tier-gated bloom applies in every scene regardless of which profile URP treats as the per-camera
    /// default — and the static grade (color/tonemapping/split-toning) authored into the asset profiles
    /// still stacks through underneath, since this volume only overrides Bloom.
    /// </summary>
    [DisallowMultipleComponent]
    public class GraphicsDirector : MonoBehaviour
    {
        public static GraphicsDirector Instance { get; private set; }

        // --- Tier knobs. This is the single place the look dials up or down. ---
        private const float HighBloomIntensity = 0.9f;  // PCVR: full neon glow
        private const float LowBloomIntensity = 0.35f;  // Quest: keep the heavy effect cheap
        private const float BloomThreshold = 0.9f;      // only HDR emission blooms, lit surfaces don't wash out
        private const float BloomScatter = 0.7f;        // soft halo
        private const float HighParticleScale = 1f;
        private const float LowParticleScale = 0.4f;

        private Bloom bloom;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildRuntimeVolume();

            // Resolve a platform default immediately so the look is right before the settings service
            // publishes (mirrors SettingsService.Load's platform fallback). The authoritative value
            // arrives via SettingsChanged on the first Apply / scene load.
            SetQuality(Application.isMobilePlatform ? GraphicsQuality.Low : GraphicsQuality.High);
        }

        private void OnEnable() => EventBus.Subscribe<SettingsChanged>(OnSettingsChanged);
        private void OnDisable() => EventBus.Unsubscribe<SettingsChanged>(OnSettingsChanged);

        private void OnSettingsChanged(SettingsChanged e)
        {
            GraphicsRuntime.CombatBlood = e.Settings.CombatBlood;
            SetQuality(e.Settings.Quality);
        }

        private void BuildRuntimeVolume()
        {
            var go = new GameObject("GraphicsDirector Volume");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f; // above scene/default volumes so the tier owns Bloom
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(BloomThreshold);
            bloom.scatter.Override(BloomScatter);
        }

        /// <summary>Apply a tier: bloom strength + the <see cref="GraphicsRuntime"/> snapshot others read.</summary>
        public void SetQuality(GraphicsQuality tier)
        {
            bool high = tier == GraphicsQuality.High;
            GraphicsRuntime.Quality = tier;
            GraphicsRuntime.ParticleScale = high ? HighParticleScale : LowParticleScale;
            GraphicsRuntime.BladeLightsEnabled = high;
            if (bloom != null)
            {
                bloom.intensity.Override(high ? HighBloomIntensity : LowBloomIntensity);
                // High-quality (multi-pass) bloom filtering is bandwidth-heavy on Quest; PCVR keeps it.
                bloom.highQualityFiltering.Override(high);
            }
        }
    }
}
