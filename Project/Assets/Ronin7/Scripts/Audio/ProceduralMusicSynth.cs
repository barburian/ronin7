using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>Which character a run-music loop has. Index matches <see cref="MusicSlot"/> % 3.</summary>
    public enum MusicIntensity { Explore, Combat, Boss }

    /// <summary>
    /// P6-01 pure DSP for the placeholder run music and the tonal cue stings (no AudioClip or editor dependency,
    /// deterministic per seed). Companion to <see cref="ProceduralAudioSynth"/>, which owns the noise/metal
    /// primitives that are reused here. Music loops are built bar by bar into a ring buffer: every note is
    /// added with modulo wrap, so a tail that runs past the end lands on the start and the loop is seamless by
    /// construction (no crossfade, no click). Tonal and rhythmic on purpose, not noise.
    /// Sector 0 Rust: industrial, metallic. 1 Program: cold digital pulse. 2 Garden: organic, airy.
    /// </summary>
    public static class ProceduralMusicSynth
    {
        public const int MusicRate = 22050;
        public const int CueRate = 44100;
        private const int Bars = 16;

        private enum Wave { Sine, Saw, Square, Tri }

        // ---- per-sector tuning: bpm (explore/combat, boss), key root (MIDI), chord roots (semitones, 2 bars each) ----
        private static readonly float[] BpmNormal = { 100f, 120f, 80f };
        private static readonly float[] BpmBoss = { 110f, 128f, 88f };
        private static readonly int[] KeyMidi = { 38, 45, 50 };
        private static readonly int[][] Roots = { new[] { 0, 1, 0, -2 }, new[] { 0, 8, 3, 10 }, new[] { 0, 7, 9, 5 } };
        private static readonly int[] Minor = { 0, 3, 7, 10 }, Major = { 0, 4, 7, 11 };
        private static readonly int[] PentaMinor = { 0, 3, 5, 7, 10 }, PentaMajor = { 0, 2, 4, 7, 9 };

        public static float LoopSeconds(int sector, MusicIntensity i) =>
            Bars * 4 * 60f / (i == MusicIntensity.Boss ? BpmBoss[Wrap(sector)] : BpmNormal[Wrap(sector)]);

        private static int Wrap(int sector) => ((sector % 3) + 3) % 3;
        private static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

        public static float[] GenerateTrack(int sector, MusicIntensity inten)
        {
            sector = Wrap(sector);
            bool boss = inten == MusicIntensity.Boss;
            float bpm = boss ? BpmBoss[sector] : BpmNormal[sector];
            float beat = 60f / bpm, bar = beat * 4f, six = beat / 4f;
            var b = new float[Mathf.RoundToInt(Bars * bar * MusicRate)];
            var rng = new System.Random(7000 + sector * 10 + (int)inten);
            int key = KeyMidi[sector] - (boss ? 5 : 0);
            var roots = Roots[sector];
            // Program/Rust chords: minor flavour except Program's F/C/G (major) and Garden's maj7 colour on the first chord.
            for (int bi = 0; bi < Bars; bi++)
            {
                int ci = (bi / 2) % 4;
                int root = key + roots[ci];
                bool major = sector == 1 ? ci != 0 : sector == 2 && !boss && ci != 2;
                var chord = major ? Major : Minor;
                float t0 = bi * bar;
                bool firstBar = bi % 2 == 0;
                if (sector == 0) RustBar(b, rng, t0, bar, beat, six, root, chord, inten, firstBar);
                else if (sector == 1) ProgramBar(b, rng, t0, bar, beat, six, root, chord, inten, firstBar);
                else GardenBar(b, rng, t0, bar, beat, six, root, chord, inten, firstBar, bi);
            }
            return Finish(b, boss ? 0.9f : 0.8f);
        }

        // ---------------------------------------------------------------- Rust: industrial, metallic
        private static void RustBar(float[] b, System.Random r, float t0, float bar, float beat, float six, int root, int[] chord, MusicIntensity k, bool first)
        {
            bool combat = k != MusicIntensity.Explore, boss = k == MusicIntensity.Boss;
            if (first) // low detuned saw drone on root + fifth, whole two bars
            {
                Note(b, MusicRate, t0, bar * 2f - 0.05f, Hz(root + 12), 0.10f, Wave.Saw, 0.4f, 0.6f, 0f, 0.0035f, 0f, 0.05f);
                Note(b, MusicRate, t0, bar * 2f - 0.05f, Hz(root + 19), 0.07f, Wave.Saw, 0.4f, 0.6f, 0f, -0.003f, 0f, 0.05f);
            }
            for (int s = 0; s < 16; s++)
            {
                float t = t0 + s * six;
                bool onBeat = s % 4 == 0, eighth = s % 2 == 0;
                if (onBeat && (combat || s == 0)) Kick(b, MusicRate, t, combat ? 0.8f : 0.45f, 42f, 130f);
                if (boss && s == 10) Kick(b, MusicRate, t, 0.7f, 42f, 130f);
                if (combat && (s == 4 || s == 12)) Snare(b, MusicRate, t, r, 0.45f);
                if (combat && eighth) Hat(b, MusicRate, t, r, 0.1f, s % 4 == 2);
                if (boss && !eighth) Hat(b, MusicRate, t, r, 0.06f, false);
                // metallic clank (inharmonic strike) off the beat
                if ((combat && (s == 6 || s == 14)) || (!combat && first && s == 10))
                    AddBuffer(b, t, ProceduralAudioSynth.GenerateMetalStrike(MusicRate, 0.5f, 17 + s, Hz(root + (s == 6 ? 31 : 36)), 0f, 9f), combat ? 0.32f : 0.22f);
                // driven bass: explore hits the root on the 1 and the "and" of 3; combat/boss pulse eighths
                if (combat ? eighth : (s == 0 || s == 10))
                    Note(b, MusicRate, t, six * (combat ? 1.7f : 3f), Hz(root + ((boss && s % 8 == 4) ? 12 : 0)), boss ? 0.42f : 0.34f, Wave.Saw, 0.004f, 0.08f, boss ? 6f : 3f);
                // minor-pentatonic stab line: sparse in explore, 16th-gated in combat, brass chord hits in boss
                if (!combat && first && (s == 6 || s == 14))
                    Note(b, MusicRate, t, beat * 0.9f, Hz(root + 24 + PentaMinor[(s + (int)(t0 / bar)) % 5]), 0.12f, Wave.Square, 0.01f, 0.4f, 0f, 0f, 0f, 0.3f);
                if (boss && onBeat && s % 8 == 0)
                    for (int ci = 0; ci < 3; ci++) Note(b, MusicRate, t, beat * 1.5f, Hz(root + 12 + chord[ci]), 0.14f, Wave.Saw, 0.03f, 0.25f, 2f, 0.004f, 0.2f, 0.35f);
            }
        }

        // ---------------------------------------------------------------- Program: cold digital pulse
        private static void ProgramBar(float[] b, System.Random r, float t0, float bar, float beat, float six, int root, int[] chord, MusicIntensity k, bool first)
        {
            bool combat = k != MusicIntensity.Explore, boss = k == MusicIntensity.Boss;
            if (first) // thin triangle pad, two octaves up
                for (int i = 0; i < 3; i++) Note(b, MusicRate, t0, bar * 2f - 0.05f, Hz(root + 24 + chord[i]), 0.07f, Wave.Tri, 0.35f, 0.5f, 0f, 0.002f);
            for (int s = 0; s < 16; s++)
            {
                float t = t0 + s * six;
                bool onBeat = s % 4 == 0, eighth = s % 2 == 0;
                if (combat && onBeat) Kick(b, MusicRate, t, boss ? 0.85f : 0.7f, 50f, 160f);
                else if (!combat && s == 0) Kick(b, MusicRate, t, 0.35f, 50f, 160f);
                if (combat && (s == 4 || s == 12)) Snare(b, MusicRate, t, r, 0.3f); // clap
                if (!combat ? s % 4 == 2 : (boss || eighth)) Hat(b, MusicRate, t, r, combat ? 0.12f : 0.08f, false);
                // sidechain-style bass pulse: staccato eighths on the root in every state
                if (eighth && (combat || s % 8 == 0))
                    Note(b, MusicRate, t, six * 1.3f, Hz(root + (s % 8 == 6 ? 7 : 0)), combat ? 0.38f : 0.3f, boss ? Wave.Square : Wave.Sine, 0.003f, 0.05f, boss ? 2f : 0f, 0f, 0f, 0.35f);
                // gated square arpeggio through the chord: 8ths in explore, 16ths in combat/boss
                if (combat || eighth)
                {
                    int step = (combat ? s : s / 2) % 4;
                    int note = chord[step % chord.Length] + (step == 3 ? 12 : 0) + (boss && s >= 8 ? 12 : 0);
                    Note(b, MusicRate, t, six * 0.8f, Hz(root + 24 + note), combat ? 0.13f : 0.09f, Wave.Square, 0.002f, 0.05f, 0f, 0f, 0f, 0.28f);
                }
                if (first && s == 0 && !combat) Bell(b, MusicRate, t, Hz(root + 36 + chord[2]), 0.14f, 1.6f); // digital bell
            }
        }

        // ---------------------------------------------------------------- Garden: organic, airy
        private static void GardenBar(float[] b, System.Random r, float t0, float bar, float beat, float six, int root, int[] chord, MusicIntensity k, bool first, int bi)
        {
            bool combat = k != MusicIntensity.Explore, boss = k == MusicIntensity.Boss;
            var penta = boss ? PentaMinor : PentaMajor;
            if (first) // slow detuned sine chord pad; boss adds a clashing minor second
            {
                for (int i = 0; i < 4; i++)
                {
                    Note(b, MusicRate, t0, bar * 2f - 0.1f, Hz(root + 12 + chord[i]), 0.07f, Wave.Sine, 1.0f, 1.2f, 0f, 0.0025f);
                    Note(b, MusicRate, t0, bar * 2f - 0.1f, Hz(root + 12 + chord[i]), 0.05f, Wave.Sine, 1.2f, 1.2f, 0f, -0.0025f);
                }
                if (boss) Note(b, MusicRate, t0, bar * 2f - 0.1f, Hz(root + 13), 0.06f, Wave.Sine, 1.0f, 1.2f);
                Note(b, MusicRate, t0, bar * 2f - 0.1f, Hz(root), combat ? 0.34f : 0.26f, Wave.Sine, 0.2f, 0.8f); // soft root
            }
            for (int s = 0; s < 16; s++)
            {
                float t = t0 + s * six;
                bool onBeat = s % 4 == 0, eighth = s % 2 == 0;
                // hand-drum / taiko toms
                if (combat && ((onBeat && (boss || s % 8 == 0)) || (boss && s == 10) || s == 14 && bi % 2 == 1))
                    Kick(b, MusicRate, t, boss ? 0.8f : 0.5f, boss ? 48f : 78f, boss ? 120f : 190f);
                if (combat && eighth && !boss) Hat(b, MusicRate, t, r, 0.09f, false); // shaker
                if (boss && s % 4 == 2) Hat(b, MusicRate, t, r, 0.09f, false);
                // plucked pentatonic line (harmonic stack with fast decay)
                int pick = (s * 3 + bi * 2 + (int)(t0 * 3f)) % 5;
                if (combat ? eighth : (s % 4 == 0 && first) || s == 10)
                    Pluck(b, MusicRate, t, Hz(root + 24 + penta[pick]), combat ? 0.17f : 0.13f, beat * 1.2f);
                // sparse vibrato "flute" lead in explore
                if (!combat && first && s == 4)
                    Note(b, MusicRate, t, beat * 2.5f, Hz(root + 36 + penta[(bi / 2 + 2) % 5]), 0.1f, Wave.Sine, 0.12f, 0.6f, 0f, 0.0015f, 0.45f);
            }
        }

        // ---------------------------------------------------------------- voices (all ring-buffer wrapped)
        private static void Add(float[] b, int i, float v)
        {
            int n = b.Length;
            i %= n; if (i < 0) i += n;
            b[i] += v;
        }

        private static void AddBuffer(float[] b, float t, float[] src, float amp)
        {
            int s0 = Mathf.RoundToInt(t * MusicRate);
            for (int i = 0; i < src.Length; i++) Add(b, s0 + i, src[i] * amp);
        }

        private static float Osc(Wave w, float ph)
        {
            ph -= Mathf.Floor(ph);
            switch (w)
            {
                case Wave.Saw: return 2f * ph - 1f;
                case Wave.Square: return ph < 0.5f ? 1f : -1f;
                case Wave.Tri: return 4f * Mathf.Abs(ph - 0.5f) - 1f;
                default: return Mathf.Sin(ph * 2f * Mathf.PI);
            }
        }

        /// <summary>One sustained note: attack, hold to <paramref name="dur"/>, exponential release. <paramref name="drive"/>
        /// saturates (tanh), <paramref name="detune"/> adds a second voice (fraction of the frequency), <paramref name="vib"/> is
        /// a vibrato depth in semitones at 5.5 Hz, <paramref name="lp"/> a one-pole lowpass alpha (0 = none).</summary>
        private static void Note(float[] b, int sr, float t0, float dur, float hz, float amp, Wave w, float atk, float rel,
            float drive = 0f, float detune = 0f, float vib = 0f, float lp = 0f)
        {
            int s0 = Mathf.RoundToInt(t0 * sr);
            int len = Mathf.RoundToInt((dur + rel * 2f) * sr);
            float p1 = 0f, p2 = 0f, y = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Min(1f, t / Mathf.Max(atk, 0.001f));
                if (t > dur) env *= Mathf.Exp(-(t - dur) * 5f / Mathf.Max(rel, 0.01f));
                if (env < 0.0005f && t > dur) break;
                float f = hz * (vib > 0f ? Mathf.Pow(2f, vib * Mathf.Sin(2f * Mathf.PI * 5.5f * t) / 12f) : 1f);
                p1 += f / sr;
                float v = Osc(w, p1);
                if (detune != 0f) { p2 += f * (1f + detune) / sr; v = (v + Osc(w, p2)) * 0.5f; }
                if (drive > 0f) v = (float)System.Math.Tanh(v * (1f + drive)) / (float)System.Math.Tanh(1f + drive);
                if (lp > 0f) { y += (v - y) * lp; v = y; }
                Add(b, s0 + i, v * env * amp);
            }
        }

        private static void Kick(float[] b, int sr, float t0, float amp, float lowHz, float hiHz)
        {
            int s0 = Mathf.RoundToInt(t0 * sr), len = (int)(0.32f * sr);
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                ph += (lowHz + (hiHz - lowHz) * Mathf.Exp(-t * 40f)) / sr;
                Add(b, s0 + i, Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Exp(-t * 11f) * Mathf.Min(1f, t / 0.002f) * amp);
            }
        }

        private static void Snare(float[] b, int sr, float t0, System.Random r, float amp)
        {
            int s0 = Mathf.RoundToInt(t0 * sr), len = (int)(0.22f * sr);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float noise = (float)(r.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 22f);
                float tone = Mathf.Sin(2f * Mathf.PI * 185f * t) * Mathf.Exp(-t * 30f) * 0.6f;
                Add(b, s0 + i, (noise * 0.7f + tone) * amp);
            }
        }

        private static void Hat(float[] b, int sr, float t0, System.Random r, float amp, bool open)
        {
            int s0 = Mathf.RoundToInt(t0 * sr), len = (int)((open ? 0.16f : 0.05f) * sr);
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float n = (float)(r.NextDouble() * 2.0 - 1.0);
                float hp = n - prev; prev = n; // first difference = crude highpass
                Add(b, s0 + i, hp * 0.5f * Mathf.Exp(-t * (open ? 22f : 70f)) * amp);
            }
        }

        private static void Pluck(float[] b, int sr, float t0, float hz, float amp, float dur)
        {
            int s0 = Mathf.RoundToInt(t0 * sr), len = (int)(dur * 2f * sr);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float v = 0f;
                for (int h = 1; h <= 4; h++) v += Mathf.Sin(2f * Mathf.PI * hz * h * t) * Mathf.Exp(-t * (4f + h * 5f)) / h;
                Add(b, s0 + i, v * Mathf.Min(1f, t / 0.003f) * amp);
            }
        }

        private static void Bell(float[] b, int sr, float t0, float hz, float amp, float dur)
        {
            int s0 = Mathf.RoundToInt(t0 * sr), len = (int)(dur * sr);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float mod = Mathf.Sin(2f * Mathf.PI * hz * 3.5f * t) * 2.2f * Mathf.Exp(-t * 3f);
                Add(b, s0 + i, Mathf.Sin(2f * Mathf.PI * hz * t + mod) * Mathf.Exp(-t * 2.5f) * amp);
            }
        }

        /// <summary>Soft-clip, then scale the peak to <paramref name="peak"/> so levels are comparable across tracks.</summary>
        private static float[] Finish(float[] b, float peak)
        {
            float max = 0f;
            for (int i = 0; i < b.Length; i++) { b[i] = (float)System.Math.Tanh(b[i] * 1.2f); max = Mathf.Max(max, Mathf.Abs(b[i])); }
            if (max > 0f) { float g = peak / max; for (int i = 0; i < b.Length; i++) b[i] *= g; }
            return b;
        }

        // ================================================================ cue stings and loops (44.1 kHz)
        public enum Cue { RunStarted, RoomCleared, BoonOffered, BoonChosen, RunLost, RunWon, EnemySpawning, BossPhase,
                          WindupMelee, WindupCharger, WindupBlink, WindupDrone, WindupRanged, LowHealthLoop, MenuAmbience }

        public static float[] GenerateCue(Cue cue)
        {
            const int sr = CueRate;
            var r = new System.Random(9000 + (int)cue);
            float[] b;
            switch (cue)
            {
                case Cue.RunStarted: // rising open fifth arpeggio with a bright tail
                    b = new float[(int)(1.4f * sr)];
                    for (int i = 0; i < 4; i++) Note(b, sr, i * 0.14f, 0.12f, Hz(50 + new[] { 0, 7, 12, 19 }[i]), 0.35f, Wave.Saw, 0.004f, 0.35f, 1f, 0.004f, 0f, 0.3f);
                    Bell(b, sr, 0.56f, Hz(86), 0.3f, 0.8f);
                    break;
                case Cue.RoomCleared: // major triad chime, FM bells stacked
                    b = new float[(int)(1.3f * sr)];
                    for (int i = 0; i < 3; i++) Bell(b, sr, i * 0.07f, Hz(72 + new[] { 0, 4, 7 }[i]), 0.3f, 1.2f);
                    break;
                case Cue.BoonOffered: // shimmering high pair with tremolo
                    b = new float[(int)(1.0f * sr)];
                    Bell(b, sr, 0f, Hz(88), 0.28f, 0.95f); Bell(b, sr, 0.12f, Hz(95), 0.24f, 0.85f);
                    for (int i = 0; i < b.Length; i++) b[i] *= 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * i / sr);
                    break;
                case Cue.BoonChosen: // quick confirm: two notes up a fifth
                    b = new float[(int)(0.55f * sr)];
                    Note(b, sr, 0f, 0.1f, Hz(72), 0.35f, Wave.Tri, 0.003f, 0.12f);
                    Note(b, sr, 0.11f, 0.2f, Hz(79), 0.4f, Wave.Tri, 0.003f, 0.2f);
                    break;
                case Cue.RunLost: // slow descending minor sag, low and heavy
                    b = new float[(int)(3.2f * sr)];
                    for (int i = 0; i < 3; i++) Note(b, sr, i * 0.55f, 0.5f, Hz(57 - new[] { 0, 2, 5 }[i]), 0.4f, Wave.Saw, 0.02f, 0.6f, 0.5f, 0.004f, 0f, 0.1f);
                    Note(b, sr, 0f, 2.0f, Hz(33), 0.4f, Wave.Sine, 0.1f, 0.5f);
                    break;
                case Cue.RunWon: // major fanfare: ascending run, held triad
                    b = new float[(int)(3.0f * sr)];
                    for (int i = 0; i < 4; i++) Note(b, sr, i * 0.16f, 0.14f, Hz(60 + new[] { 0, 4, 7, 12 }[i]), 0.3f, Wave.Saw, 0.005f, 0.2f, 0.5f, 0.003f, 0f, 0.25f);
                    for (int i = 0; i < 3; i++) Note(b, sr, 0.66f, 1.0f, Hz(60 + new[] { 0, 4, 7 }[i]), 0.22f, Wave.Saw, 0.02f, 0.7f, 0.5f, 0.003f, 0f, 0.2f);
                    Bell(b, sr, 0.66f, Hz(84), 0.2f, 1.6f);
                    break;
                case Cue.EnemySpawning: // rising portal swell: sweeping sine + shimmering octave, tonal
                    b = new float[(int)(0.8f * sr)];
                    SweepInto(b, sr, 180f, 900f, 0.75f, 0.4f, 7f);
                    SweepInto(b, sr, 360f, 1800f, 0.75f, 0.15f, 11f);
                    break;
                case Cue.BossPhase: // heavy impact with a tritone swell
                    b = new float[(int)(2.0f * sr)];
                    Kick(b, sr, 0f, 0.9f, 38f, 110f);
                    Note(b, sr, 0f, 1.0f, Hz(38), 0.3f, Wave.Saw, 0.02f, 0.5f, 3f, 0.004f, 0f, 0.1f);
                    Note(b, sr, 0.05f, 1.0f, Hz(44), 0.25f, Wave.Saw, 0.15f, 0.5f, 3f, 0.004f, 0f, 0.1f);
                    break;
                case Cue.WindupMelee: // mid, bright two-tone tick (square), short
                    b = new float[(int)(0.3f * sr)];
                    Note(b, sr, 0f, 0.07f, Hz(76), 0.4f, Wave.Square, 0.002f, 0.06f, 0f, 0f, 0f, 0.4f);
                    Note(b, sr, 0.09f, 0.1f, Hz(83), 0.4f, Wave.Square, 0.002f, 0.08f, 0f, 0f, 0f, 0.4f);
                    break;
                case Cue.WindupCharger: // low growling rise, saw with 18 Hz tremolo
                    b = new float[(int)(0.6f * sr)];
                    SweepInto(b, sr, 95f, 240f, 0.55f, 0.55f, 18f, Wave.Saw);
                    break;
                case Cue.WindupBlink: // glassy high ping pair, falling
                    b = new float[(int)(0.5f * sr)];
                    Bell(b, sr, 0f, Hz(96), 0.35f, 0.3f); Bell(b, sr, 0.17f, Hz(89), 0.35f, 0.3f);
                    break;
                case Cue.WindupDrone: // electronic double beep, mid-high square
                    b = new float[(int)(0.4f * sr)];
                    Note(b, sr, 0f, 0.07f, 1250f, 0.35f, Wave.Square, 0.002f, 0.03f, 0f, 0f, 0f, 0.3f);
                    Note(b, sr, 0.13f, 0.07f, 1250f, 0.35f, Wave.Square, 0.002f, 0.03f, 0f, 0f, 0f, 0.3f);
                    break;
                case Cue.WindupRanged: // rising charge whine, triangle
                    b = new float[(int)(0.6f * sr)];
                    SweepInto(b, sr, 500f, 1500f, 0.55f, 0.4f, 0f, Wave.Tri);
                    break;
                case Cue.LowHealthLoop: // lub-dub heartbeat, exactly 1.0 s (60 bpm) so the loop is seamless
                    b = new float[sr];
                    Kick(b, sr, 0f, 0.9f, 48f, 95f); Kick(b, sr, 0.26f, 0.6f, 52f, 100f);
                    break;
                default: // MenuAmbience: 24 s pad on a sus chord; every partial has a whole number of cycles in the loop
                    return MenuAmbience(r);
            }
            return Finish(b, cue == Cue.LowHealthLoop ? 0.9f : 0.75f);
        }

        private static void SweepInto(float[] b, int sr, float f0, float f1, float dur, float amp, float trem, Wave w = Wave.Sine)
        {
            float ph = 0f;
            int len = Mathf.Min(b.Length, (int)(dur * sr));
            for (int i = 0; i < len; i++)
            {
                float u = i / (float)len;
                ph += Mathf.Lerp(f0, f1, u * u) / sr;
                float env = Mathf.Min(1f, i / (0.02f * sr)) * (u < 0.85f ? 1f : (1f - u) / 0.15f);
                float tr = trem > 0f ? 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * trem * i / sr) : 1f;
                b[i] += Osc(w, ph) * env * tr * amp;
            }
        }

        private static float[] MenuAmbience(System.Random r)
        {
            const float L = 24f;
            int n = (int)(L * CueRate);
            var b = new float[n];
            float[] midis = { 38, 45, 50, 57, 62, 64 }; // D sus: root, fifth, octave, fifth, ninth/sus tones
            for (int k = 0; k < midis.Length; k++)
            {
                float f = Mathf.Round(Hz(midis[k]) * L) / L, f2 = Mathf.Round(Hz(midis[k]) * 1.004f * L) / L; // detuned pair beats slowly
                float lfo = Mathf.Round((0.05f + 0.02f * k) * L) / L;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)CueRate;
                    float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * lfo * t + k);
                    b[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * (k < 2 ? 0.16f : 0.07f) * swell;
                }
            }
            // airy noise: lowpassed, slowly swelling, tail crossfaded into the head
            float lpv = 0f; var air = new float[n];
            for (int i = 0; i < n; i++) { lpv += ((float)(r.NextDouble() * 2.0 - 1.0) - lpv) * 0.03f; air[i] = lpv * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * i / n)); }
            int fade = CueRate;
            for (int i = 0; i < fade; i++) { float w = i / (float)(fade - 1); int idx = n - fade + i; air[idx] = Mathf.Lerp(air[idx], air[fade - 1 - i], w); }
            for (int i = 0; i < n; i++) b[i] += air[i] * 0.35f;
            return Finish(b, 0.6f);
        }
    }
}
