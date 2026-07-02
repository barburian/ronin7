using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// Programmatic techno-noir post-processing grade — the Sairento "neon over dark" frame. Run
    /// <c>Tools/Space Samurai/Art/Apply Techno-Noir Post-FX</c> to stamp the grade onto the URP volume
    /// profiles reproducibly (like the rest of this pipeline), instead of hand-tuning in the Inspector.
    /// Grade values are sourced from <see cref="ArtDirectionSpec"/> to maintain a single source of truth.
    ///
    /// Both profiles are graded the same so the result is deterministic regardless of which one URP
    /// treats as the per-camera default: <c>SampleSceneProfile</c> is referenced by both RP assets,
    /// <c>DefaultVolumeProfile</c> by the URP global settings. Bloom is set here to the High baseline so
    /// the editor preview glows; at runtime <c>GraphicsDirector</c> owns a higher-priority volume that
    /// re-overrides bloom strength per quality tier (Low dims it for Quest).
    /// </summary>
    public static class NeonPostFx
    {
        private const string SampleProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        private const string DefaultProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        [MenuItem("Tools/Space Samurai/Art/Apply Techno-Noir Post-FX", priority = 1)]
        public static void ApplyTechnoNoirPostFx()
        {
            int graded = 0;
            graded += GradeProfile(SampleProfilePath) ? 1 : 0;
            graded += GradeProfile(DefaultProfilePath) ? 1 : 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"[NeonPostFx] Graded {graded} volume profile(s) to techno-noir. " +
                      "Bloom is the High baseline; GraphicsDirector dims it on the Low (Quest) tier at runtime.");
        }

        private static bool GradeProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                Debug.LogWarning($"[NeonPostFx] No VolumeProfile at {path}; skipped.");
                return false;
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(ArtDirectionSpec.BloomIntensity);
            bloom.threshold.Override(ArtDirectionSpec.BloomThreshold);
            bloom.scatter.Override(ArtDirectionSpec.BloomScatter);

            var color = GetOrAdd<ColorAdjustments>(profile);
            color.active = true;
            color.contrast.Override(ArtDirectionSpec.Contrast);
            color.saturation.Override(ArtDirectionSpec.Saturation);
            color.colorFilter.Override(ArtDirectionSpec.CoolFilter);

            var split = GetOrAdd<SplitToning>(profile);
            split.active = true;
            split.shadows.Override(ArtDirectionSpec.SplitShadows);
            split.highlights.Override(ArtDirectionSpec.SplitHighlights);
            split.balance.Override(ArtDirectionSpec.SplitBalance);

            // ACES gives neon highlights a richer rolloff than the neutral/none default.
            var tone = GetOrAdd<Tonemapping>(profile);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(ArtDirectionSpec.Vignette);

            EditorUtility.SetDirty(profile);
            return true;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet<T>(out var component) ? component : profile.Add<T>(true);
        }
    }
}
