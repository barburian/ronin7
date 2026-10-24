using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>P6-03: pure math behind UI scale, the assist multipliers, the windup rim pulse and the Assisted tag.
    /// An unset (0 / NaN) multiplier reads as the default, so a zeroed <see cref="GameSettings"/> can never mean
    /// "invulnerable" or "zero-size panel".</summary>
    public static class AccessibilityOptions
    {
        public const float UiScaleMin = 1f, UiScaleMax = 1.5f;
        public const float DamageTakenMin = 0.5f;
        public const float CooldownScaleMax = 1.5f;

        public const float PulseMinHz = 2f, PulseMaxHz = 6f;
        public const float PulseMin = 0.35f;          // rim scale at the trough of the full pulse
        /// <summary>Reduce Flashes: constant, below the 3-per-second flash threshold.</summary>
        public const float ReducedPulseHz = 2f;
        public const float ReducedPulseMin = 0.65f;   // shallow smooth dip: readable, never strobes

        public static float ClampUiScale(float v) => v > 0f ? Mathf.Clamp(v, UiScaleMin, UiScaleMax) : 1f;
        public static float ClampDamageTaken(float v) => v > 0f ? Mathf.Clamp(v, DamageTakenMin, 1f) : 1f;
        public static float ClampCooldownScale(float v) => v > 0f ? Mathf.Clamp(v, 1f, CooldownScaleMax) : 1f;

        public static float DamageTaken(float amount, float multiplier) => amount * ClampDamageTaken(multiplier);
        public static float Cooldown(float seconds, float multiplier) => seconds * ClampCooldownScale(multiplier);

        /// <summary>True when either assist is on (D-06: assisted runs still bank Echoes).</summary>
        public static bool IsAssisted(GameSettings s) => ClampDamageTaken(s.DamageTaken) < 1f || ClampCooldownScale(s.EnemyCooldownScale) > 1f;

        /// <summary>Windup pulse rate: 2 Hz at the start of the telegraph rising to 6 Hz at the strike.</summary>
        public static float PulseHz(float progress) => Mathf.Lerp(PulseMinHz, PulseMaxHz, Mathf.Clamp01(progress));

        public static float PulseHzFor(float progress, bool reduceFlashes) => reduceFlashes ? ReducedPulseHz : PulseHz(progress);

        /// <summary>Accumulating the phase (not time x hz) keeps the pulse continuous as the rate rises.</summary>
        public static float AdvancePhase(float phase, float progress, float dt, bool reduceFlashes)
            => Mathf.Repeat(phase + PulseHzFor(progress, reduceFlashes) * dt, 1f);

        /// <summary>Rim multiplier 0..1 for a pulse phase (cycles): smooth sine, peak 1. Apply AFTER the
        /// Reduce Flashes rim cap so the modulation survives the cap.</summary>
        public static float TelegraphPulse(float phase, bool reduceFlashes)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(phase * 2f * Mathf.PI); // 0..1, smooth
            return Mathf.Lerp(reduceFlashes ? ReducedPulseMin : PulseMin, 1f, wave);
        }

        /// <summary>Weak-point mark beats per second (scale only). Reduce Flashes slows it under the 3/s threshold.</summary>
        public static float WeakPointBeatHz(bool reduceFlashes) => reduceFlashes ? 1.5f : 3f;
    }

    /// <summary>P6-03: a line of dialogue to show as a subtitle. Published by DialoguePlayer at line start.</summary>
    public readonly struct SubtitleLine
    {
        public readonly string Speaker, Text;
        public readonly float Seconds;
        public SubtitleLine(string speaker, string text, float seconds) { Speaker = speaker; Text = text; Seconds = seconds; }

        public static string Format(string speaker, string text)
            => string.IsNullOrEmpty(text) ? string.Empty : string.IsNullOrEmpty(speaker) ? text : speaker + ": " + text;
    }
}
