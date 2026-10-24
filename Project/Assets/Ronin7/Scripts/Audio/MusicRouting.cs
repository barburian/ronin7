using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>P6-01: the nine run-music loops, index = sector * 3 + (0 explore, 1 combat, 2 boss).</summary>
    public enum MusicSlot
    {
        RustExplore, RustCombat, RustBoss,
        ProgramExplore, ProgramCombat, ProgramBoss,
        GardenExplore, GardenCombat, GardenBoss,
    }

    /// <summary>
    /// P6-01 music-duck envelope: -6 dB for 0.3 s after a trigger, then a short release ramp back to 0 dB.
    /// Pure (time is passed in) so the timing is unit-testable; the director feeds the result to the mixer.
    /// </summary>
    public struct MusicDuck
    {
        public const float Db = -6f, HoldSeconds = 0.3f, ReleaseSeconds = 0.25f;
        private float until;
        private bool armed;

        public void Trigger(float now) { until = now + HoldSeconds; armed = true; }

        public float DbAt(float now)
        {
            if (!armed || now < until) return armed ? Db : 0f;
            float r = (now - until) / ReleaseSeconds;
            return r >= 1f ? 0f : Db * (1f - r);
        }
    }
}
