using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>Surface a footstep thud is synthesized for — shapes tone (lowpass amount) and decay rate.</summary>
    public enum FootstepSurface { Soft, Hard }

    /// <summary>Themed ambience bed variant — shapes drone frequency/amount vs. noise amount/tone.</summary>
    public enum AmbienceTheme { HangarHum, GardenWind, DreadDrone }

    /// <summary>
    /// Pure DSP core for procedurally synthesizing placeholder footstep and ambience SFX. No
    /// UnityEngine.AudioClip or editor dependency — operates entirely on float[] sample arrays so it
    /// is usable from runtime code and unit-testable without an AudioClip round-trip. Deterministic:
    /// every generator takes an explicit seed and uses <see cref="System.Random"/> (never
    /// UnityEngine.Random or a time-based seed), so the same seed always produces the same samples.
    /// Callers (e.g. <see cref="Ronin7.Editor.Art.ProceduralAudioClipBuilder"/>) turn the output into
    /// real AudioClip assets.
    /// </summary>
    public static class ProceduralAudioSynth
    {
        /// <summary>
        /// Short filtered-noise burst with an attack/decay envelope — a footstep thud. The noise is
        /// pushed through a 1-pole lowpass (alpha set by <paramref name="surface"/>) for tone, then
        /// shaped by a fast linear attack and an exponential decay. Soft surfaces (carpet/grass) come
        /// out darker (lower lowpass alpha) and decay slower; hard surfaces (metal/stone) come out
        /// brighter and decay faster.
        /// </summary>
        public static float[] GenerateFootstepThud(int sampleRate, float durationSeconds, int seed, FootstepSurface surface)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            var rng = new System.Random(seed);
            var samples = new float[n];

            float lowpassAlpha = surface == FootstepSurface.Soft ? 0.12f : 0.5f;
            float decayRate = surface == FootstepSurface.Soft ? 18f : 30f;
            const float AttackSeconds = 0.003f;

            float filtered = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (noise - filtered) * lowpassAlpha;

                float attack = Mathf.Clamp01(t / AttackSeconds);
                float decay = Mathf.Exp(-decayRate * t);
                samples[i] = Mathf.Clamp(filtered * attack * decay, -1f, 1f);
            }
            return samples;
        }

        /// <summary>
        /// Loopable ambience bed: a low sine drone layered with filtered noise, weighted per
        /// <paramref name="theme"/> (more noise/less tone for wind, more tone/less noise for a dread
        /// drone, a balance for a hangar hum). The tail is crossfaded into the head so the returned
        /// buffer loops without a click — the first and last samples end up identical.
        /// </summary>
        public static float[] GenerateAmbienceBed(int sampleRate, float durationSeconds, int seed, AmbienceTheme theme)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(sampleRate * durationSeconds));
            var rng = new System.Random(seed);
            var samples = new float[n];

            float droneFreq, droneAmount, noiseAmount, noiseLowpassAlpha;
            switch (theme)
            {
                case AmbienceTheme.GardenWind:
                    droneFreq = 60f; droneAmount = 0.15f; noiseAmount = 0.5f; noiseLowpassAlpha = 0.05f;
                    break;
                case AmbienceTheme.DreadDrone:
                    droneFreq = 40f; droneAmount = 0.55f; noiseAmount = 0.12f; noiseLowpassAlpha = 0.02f;
                    break;
                default: // HangarHum
                    droneFreq = 80f; droneAmount = 0.35f; noiseAmount = 0.25f; noiseLowpassAlpha = 0.04f;
                    break;
            }

            float filtered = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float drone = Mathf.Sin(2f * Mathf.PI * droneFreq * t) * droneAmount;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                filtered += (noise - filtered) * noiseLowpassAlpha;

                samples[i] = Mathf.Clamp(drone + filtered * noiseAmount, -1f, 1f);
            }

            // Crossfade the tail into the head so index 0 and index n-1 land on the same value.
            int fadeLen = Mathf.Clamp(n / 4, 2, sampleRate / 2);
            for (int i = 0; i < fadeLen; i++)
            {
                float w = i / (float)(fadeLen - 1);
                int idx = n - fadeLen + i;
                samples[idx] = Mathf.Lerp(samples[idx], samples[fadeLen - 1 - i], w);
            }

            return samples;
        }
    }
}
