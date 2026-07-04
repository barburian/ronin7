#!/usr/bin/env python3
"""
Space Samurai -- procedural scene SFX generator (ambient loops + footsteps).

Synthesizes placeholder-quality, scene-distinct ambient loops and footstep
one-shots and writes them as 16-bit PCM mono WAV files into
Assets/Ronin7/Art/Generated/Audio/SFX. Everything is built from oscillators
and filtered noise (same approach as generate_sounds.py in this folder), so
it is license-clean, deterministic, and re-runnable. Tweak the parameters in
any build_* function and re-run.

Requires numpy (checked at import time; this project's other SFX generator,
generate_sounds.py, already depends on it). If numpy is unavailable, install
it (`pip install numpy`) -- a pure-stdlib fallback is not implemented here
since the handful of seconds of audio this script produces make numpy the
simple, already-adopted choice for this folder.

Usage:
    python generate_sfx.py            # write WAVs to ../SFX
    python generate_sfx.py --out DIR  # write to a specific folder

Output -> scene:
    medbay_hum.wav      ambient loop, ~8s  -- soft airy medical-bay hum
    labcore_hum.wav     ambient loop, ~8s  -- sterile electrical lab hum
    throne_rumble.wav   ambient loop, ~10s -- deep ominous throne-room rumble
    dock_wind.wav       ambient loop, ~8s  -- thin hull/dock wind
    footstep_a.wav      one-shot, ~0.25s   -- footstep variant A
    footstep_b.wav      one-shot, ~0.25s   -- footstep variant B
    footstep_c.wav      one-shot, ~0.25s   -- footstep variant C
    footstep_d.wav      one-shot, ~0.25s   -- footstep variant D
"""

import argparse
import os
import wave

try:
    import numpy as np
except ImportError:
    print("[ERROR] numpy not installed. Install with: pip install numpy")
    raise SystemExit(1)

SR = 44100
RNG = np.random.default_rng(20260704)  # fixed seed -> reproducible ambience loops

LOOP_PEAK = 10 ** (-6.0 / 20.0)  # ~-6 dBFS target peak for ambient loops
STEP_PEAK = 10 ** (-3.0 / 20.0)  # ~-3 dBFS target peak for footstep one-shots


# ----------------------------------------------------------------------------- helpers
def t_axis(seconds):
    return np.linspace(0.0, seconds, int(SR * seconds), endpoint=False)


def env_ad(n, attack, decay, sustain_floor=0.0):
    """Attack-decay exponential envelope of length n samples."""
    a = max(1, int(attack * SR))
    out = np.empty(n)
    a = min(a, n)
    out[:a] = np.linspace(0.0, 1.0, a)
    rest = n - a
    if rest > 0:
        k = decay if decay > 0 else 1e-6
        out[a:] = sustain_floor + (1.0 - sustain_floor) * np.exp(-np.arange(rest) / (k * SR))
    return out


def one_pole_lowpass(x, cutoff_hz):
    """Cheap 1-pole low-pass; cutoff in Hz."""
    dt = 1.0 / SR
    rc = 1.0 / (2 * np.pi * cutoff_hz)
    alpha = dt / (rc + dt)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(x.size):
        acc += alpha * (x[i] - acc)
        y[i] = acc
    return y


def one_pole_highpass(x, cutoff_hz):
    return x - one_pole_lowpass(x, cutoff_hz)


def bandpass(x, low_hz, high_hz):
    return one_pole_highpass(one_pole_lowpass(x, high_hz), low_hz)


def time_varying_lowpass(x, cutoff_hz_array):
    """1-pole low-pass whose cutoff (Hz, one value per sample) changes over time --
    used to make a noise band slowly 'sweep' instead of sitting still."""
    dt = 1.0 / SR
    y = np.empty_like(x)
    acc = 0.0
    for i in range(x.size):
        rc = 1.0 / (2 * np.pi * cutoff_hz_array[i])
        a = dt / (rc + dt)
        acc += a * (x[i] - acc)
        y[i] = acc
    return y


def noise(n, rng=RNG):
    return rng.uniform(-1.0, 1.0, n)


def normalize(x, peak):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def soft_clip(x):
    return np.tanh(x)


def make_seamless(x, cross_sec=0.4):
    """Equal-power circular crossfade so the loop has no click/pop at the wrap point."""
    c = int(cross_sec * SR)
    c = min(c, x.size // 2)
    if c <= 0:
        return x
    head = x[:c].copy()
    tail = x[-c:].copy()
    fade = np.linspace(0.0, 1.0, c)
    blended = head * np.sqrt(fade) + tail * np.sqrt(1.0 - fade)
    x = x[:-c]
    x[:c] = blended
    return x


def write_wav(path, samples):
    samples = np.clip(samples, -1.0, 1.0)
    pcm = (samples * 32767.0).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    peak = np.max(np.abs(samples))
    dbfs = 20 * np.log10(peak) if peak > 0 else float("-inf")
    print(f"  wrote {os.path.basename(path):18} {samples.size / SR:5.2f}s  peak {dbfs:5.1f} dBFS")


# ----------------------------------------------------------------------------- ambience loops
def build_medbay_hum():
    """Soft airy medical-bay hum: a close sine pair (110/111 Hz) that beats slowly
    against itself, a light filtered noise floor for 'air', and a gentle 0.25 Hz
    amplitude breathing on top. Seamless."""
    dur = 8.0
    t = t_axis(dur)
    n = t.size
    beat = np.sin(2 * np.pi * 110.0 * t) + np.sin(2 * np.pi * 111.0 * t)
    air = one_pole_lowpass(noise(n), 500) * 0.12
    breathing = 0.85 + 0.15 * np.sin(2 * np.pi * 0.25 * t)
    sig = (0.5 * beat + air) * breathing
    sig = soft_clip(sig)
    sig = make_seamless(sig, 0.5)
    return normalize(sig, LOOP_PEAK)


def build_labcore_hum():
    """Sterile electrical lab hum: mains-style 60 Hz fundamental with 120/240 Hz
    harmonics at lower level, a faint high shimmer noise band, and a slight
    periodic flutter riding the amplitude. Seamless."""
    dur = 8.0
    t = t_axis(dur)
    n = t.size
    hum = np.sin(2 * np.pi * 60.0 * t) + 0.35 * np.sin(2 * np.pi * 120.0 * t) + 0.15 * np.sin(2 * np.pi * 240.0 * t)
    shimmer = bandpass(noise(n), 3000, 9000) * 0.08
    flutter = 1.0 + 0.04 * np.sin(2 * np.pi * 3.3 * t)
    sig = (0.5 * hum + shimmer) * flutter
    sig = soft_clip(sig)
    sig = make_seamless(sig, 0.4)
    return normalize(sig, LOOP_PEAK)


def build_throne_rumble():
    """Deep ominous throne-room rumble: brown-ish noise cascaded through two
    1-pole low-pass stages for a hard sub-120 Hz roll-off, a faint 35 Hz sub-sine,
    and a slow 0.1 Hz swell. Seamless."""
    dur = 10.0
    t = t_axis(dur)
    n = t.size
    brown = one_pole_lowpass(noise(n), 70.0)
    brown = one_pole_lowpass(brown, 70.0)  # second stage -> steeper, harder roll-off
    sub = np.sin(2 * np.pi * 35.0 * t) * 0.3
    swell = 0.7 + 0.3 * np.sin(2 * np.pi * 0.1 * t)
    sig = (0.9 * brown + sub) * swell
    sig = soft_clip(sig)
    sig = make_seamless(sig, 0.6)
    return normalize(sig, LOOP_PEAK)


def build_dock_wind():
    """Thin hull/dock wind: pink-ish noise (high end rolled off, low end trimmed)
    whose passband center slowly sweeps, plus a couple of slow gust LFOs on
    amplitude. Seamless."""
    dur = 8.0
    t = t_axis(dur)
    n = t.size
    nz = noise(n)
    sweep_cutoff = 700.0 + 500.0 * np.sin(2 * np.pi * 0.05 * t)
    swept = time_varying_lowpass(nz, sweep_cutoff)
    wind = one_pole_highpass(swept, 150.0)
    gust = 0.6 + 0.25 * np.sin(2 * np.pi * 0.033 * t + 0.7) + 0.15 * np.sin(2 * np.pi * 0.017 * t)
    sig = wind * np.clip(gust, 0.2, 1.0)
    sig = soft_clip(sig)
    sig = make_seamless(sig, 0.5)
    return normalize(sig, LOOP_PEAK)


# ----------------------------------------------------------------------------- footstep one-shots
def build_footstep(seed, knock_freq):
    """Short low thud: an exponentially-decaying filtered noise burst (the
    contact scuff) plus a decaying low sine 'knock' (the body weight). Edges are
    linearly faded so the one-shot never clicks. seed/knock_freq vary per
    variant for scene-to-scene distinctness."""
    dur = 0.25
    t = t_axis(dur)
    n = t.size
    rng = np.random.default_rng(seed)
    burst = bandpass(noise(n, rng), 150.0, 2500.0) * env_ad(n, 0.001, 0.045)
    knock = np.sin(2 * np.pi * knock_freq * t) * env_ad(n, 0.0008, 0.05)
    sig = soft_clip(0.8 * burst + 0.9 * knock)

    fade = int(0.003 * SR)
    sig[:fade] *= np.linspace(0.0, 1.0, fade)
    sig[-fade:] *= np.linspace(1.0, 0.0, fade)
    return normalize(sig, STEP_PEAK)


# ----------------------------------------------------------------------------- driver
CLIPS = {
    "medbay_hum": build_medbay_hum,
    "labcore_hum": build_labcore_hum,
    "throne_rumble": build_throne_rumble,
    "dock_wind": build_dock_wind,
    "footstep_a": lambda: build_footstep(1, 58.0),
    "footstep_b": lambda: build_footstep(2, 64.0),
    "footstep_c": lambda: build_footstep(3, 70.0),
    "footstep_d": lambda: build_footstep(4, 75.0),
}


def main():
    default_out = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "SFX"))
    ap = argparse.ArgumentParser(description="Generate Space Samurai ambient loops and footstep SFX as WAV files.")
    ap.add_argument("--out", default=default_out, help="output folder (default: ../SFX relative to this script)")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    print(f"Generating {len(CLIPS)} clips into {args.out}")
    for name, fn in CLIPS.items():
        write_wav(os.path.join(args.out, name + ".wav"), fn())
    print("Done.")


if __name__ == "__main__":
    main()
