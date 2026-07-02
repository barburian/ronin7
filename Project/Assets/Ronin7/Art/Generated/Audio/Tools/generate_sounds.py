#!/usr/bin/env python3
"""
Space Samurai — procedural SFX generator ("the audio agent").

Synthesizes every clip the AudioDirector expects and writes them as 16-bit PCM
WAV files into Assets/Ronin7/Audio. No samples are downloaded — each sound
is built from oscillators + filtered noise, so it is license-clean, deterministic,
and re-runnable. Tweak the parameters in any build_* function and re-run.

Usage:
    python generate_sounds.py            # write WAVs next to ../ (the Audio folder)
    python generate_sounds.py --out DIR  # write to a specific folder

Clip -> AudioDirector field:
    SwordDeflect    -> swordDeflect    (metallic clang / parry)
    SwordImpact     -> swordImpact     (flesh/armor cut)
    PlayerHit       -> playerHit       (grunt / damage thud)
    ShipGunFire     -> shipGunFire     (laser-rifle blaster 'pew')
    BoltImpact      -> boltImpact      (laser hitting metal / molten burst)
    EnemyExplosion  -> enemyExplosion  (ship blows up — boom + debris)
    Landing         -> landing         (whoosh / thruster-down)
    Extraction      -> extraction      (success sting)
    FlightAmbience  -> flightAmbience  (engine/space hum loop, seamless)
    OnFootAmbience  -> onFootAmbience  (planet wind loop, seamless)
    EngineLoop      -> engineLoop      (Star-Wars ion-engine drone, seamless)
"""

import argparse
import os
import wave

import numpy as np

SR = 44100
RNG = np.random.default_rng(20260522)  # fixed seed -> reproducible output


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


def noise(n):
    return RNG.uniform(-1.0, 1.0, n)


def normalize(x, peak=0.92):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def soft_clip(x):
    return np.tanh(x)


def make_seamless(x, cross_sec=0.3):
    """Equal-power circular crossfade so the loop has no click at the wrap point."""
    c = int(cross_sec * SR)
    c = min(c, x.size // 2)
    if c <= 0:
        return x
    head = x[:c].copy()
    tail = x[-c:].copy()
    fade = np.linspace(0.0, 1.0, c)
    # blend the tail into the head with equal-power weights, then drop the tail
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
    print(f"  wrote {os.path.basename(path):18} {samples.size / SR:5.2f}s")


# ----------------------------------------------------------------------------- one-shots
def build_sword_deflect():
    """Lightsaber clash: an electric crackle/zap at contact riding a warbling plasma
    hum, with a bright sparking burst and a short ringing buzz tail."""
    dur = 0.7
    t = t_axis(dur)
    n = t.size

    # Plasma hum — a buzzy harmonic tone (saw-like) with vibrato (the saber 'warble').
    vibrato = 1.0 + 0.012 * np.sin(2 * np.pi * 6.0 * t)
    base = 130.0 * vibrato
    phase = 2 * np.pi * np.cumsum(base) / SR
    hum = np.zeros(n)
    for k in range(1, 9):  # stacked harmonics -> rich electric buzz
        hum += (1.0 / k) * np.sin(k * phase)
    hum *= env_ad(n, 0.006, 0.22)

    # Contact zap — bright noise burst ring-modulated by a high tone for that
    # electric/metallic 'kzzt', sweeping downward as it discharges.
    zap_nz = bandpass(noise(n), 2500, 11000)
    ringmod = np.sin(2 * np.pi * (3200.0 * np.exp(-t * 6.0) + 800.0) * t)
    zap = zap_nz * (0.6 + 0.4 * ringmod) * env_ad(n, 0.0006, 0.05)

    # Spark crackle — sparse high-frequency pops in the first moments.
    crackle = noise(n) * (RNG.uniform(0, 1, n) > 0.985)
    crackle = one_pole_highpass(crackle, 4000) * env_ad(n, 0.0005, 0.09) * 2.0

    sig = soft_clip(0.55 * hum + 0.9 * zap + 0.5 * crackle)
    return normalize(sig)


def build_sword_impact():
    """Cutting 'shing-thunk': airy high swipe over a short low body thud."""
    dur = 0.4
    t = t_axis(dur)
    n = t.size
    swipe = bandpass(noise(n), 1800, 7000) * env_ad(n, 0.004, 0.08)
    thud_f = 150.0 * np.exp(-t * 9.0) + 60.0
    thud = np.sin(2 * np.pi * np.cumsum(thud_f) / SR) * env_ad(n, 0.001, 0.10)
    sig = soft_clip(0.7 * swipe + 0.9 * thud)
    return normalize(sig)


def build_player_hit():
    """Pained grunt approximation: low body thud + filtered vocal-ish formant noise."""
    dur = 0.45
    t = t_axis(dur)
    n = t.size
    # body thud
    thud_f = 110.0 * np.exp(-t * 7.0) + 55.0
    thud = np.sin(2 * np.pi * np.cumsum(thud_f) / SR) * env_ad(n, 0.002, 0.12)
    # "ugh" — buzzy tone near vocal range with a falling pitch, formant-shaped noise
    voice_f = 230.0 * np.exp(-t * 4.0) + 95.0
    voice = (np.sign(np.sin(2 * np.pi * np.cumsum(voice_f) / SR)) * 0.4
             + np.sin(2 * np.pi * np.cumsum(voice_f) / SR))
    voice = bandpass(voice, 200, 1200) * env_ad(n, 0.01, 0.13)
    breath = bandpass(noise(n), 600, 2500) * env_ad(n, 0.02, 0.06) * 0.3
    sig = soft_clip(0.9 * thud + 0.7 * voice + breath)
    return normalize(sig)


def build_ship_gun_fire():
    """Laser-rifle 'pew': a fast exponential downward pitch sweep with a metallic twang
    overtone and a bright onset zap — the classic blaster bolt."""
    dur = 0.28
    t = t_axis(dur)
    n = t.size
    # Fast downward sweep — the blaster 'kchew'.
    f = 1800.0 * np.exp(-t * 22.0) + 180.0
    phase = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(phase) + 0.5 * np.sin(2 * phase) + 0.25 * np.sin(3 * phase)
    body *= env_ad(n, 0.001, 0.055)
    # Metallic twang — a high resonant ring that decays fast (the 'cable' overtone).
    twang = np.sin(2 * np.pi * 2400.0 * t) * env_ad(n, 0.0005, 0.02) * 0.4
    # Onset zap noise for the sharp transient.
    zap = one_pole_highpass(noise(n), 3000) * env_ad(n, 0.0003, 0.012) * 0.5
    sig = soft_clip(0.9 * body + twang + zap)
    return normalize(sig)


def build_bolt_impact():
    """Laser bolt hitting metal: a sharp sizzling blast + inharmonic metallic clang ring
    over a short low thump — a small molten explosion on the hull."""
    dur = 0.45
    t = t_axis(dur)
    n = t.size
    # Sizzle/blast — bright noise burst that decays quickly.
    blast = bandpass(noise(n), 1200, 9000) * env_ad(n, 0.0005, 0.07)
    # Metallic ring — two inharmonic partials for the 'clang'.
    clang = np.sin(2 * np.pi * 1730.0 * t) + 0.6 * np.sin(2 * np.pi * 2940.0 * t)
    clang *= env_ad(n, 0.0008, 0.10) * 0.5
    # Low thump body for the impact weight.
    thud_f = 130.0 * np.exp(-t * 10.0) + 55.0
    thud = np.sin(2 * np.pi * np.cumsum(thud_f) / SR) * env_ad(n, 0.001, 0.09)
    sig = soft_clip(0.9 * blast + clang + 0.8 * thud)
    return normalize(sig)


def build_enemy_explosion():
    """Ship explosion: a punchy low-passed noise blast over a deep falling boom, with a
    sparse crackling debris tail."""
    dur = 1.4
    t = t_axis(dur)
    n = t.size
    # Initial blast — broadband noise, low-passed and decaying.
    blast = one_pole_lowpass(noise(n), 3000) * env_ad(n, 0.002, 0.25)
    # Deep boom — sub sine with falling pitch.
    boom_f = 120.0 * np.exp(-t * 3.0) + 35.0
    boom = np.sin(2 * np.pi * np.cumsum(boom_f) / SR) * env_ad(n, 0.004, 0.35)
    # Debris crackle — sparse pops through the tail.
    crackle = noise(n) * (RNG.uniform(0, 1, n) > 0.992)
    crackle = bandpass(crackle, 800, 5000) * env_ad(n, 0.05, 0.6) * 1.5
    sig = soft_clip(0.9 * blast + boom + 0.5 * crackle)
    return normalize(sig)


def build_landing():
    """Thruster-down: descending filtered-noise whoosh over a settling low rumble."""
    dur = 1.6
    t = t_axis(dur)
    n = t.size
    # whoosh: noise whose band sweeps downward
    nz = noise(n)
    cutoff = 6000.0 * np.exp(-t * 2.2) + 400.0
    whoosh = np.empty(n)
    acc = 0.0
    for i in range(n):  # time-varying 1-pole LP for the descending sweep
        a = (1.0 / SR) / (1.0 / (2 * np.pi * cutoff[i]) + 1.0 / SR)
        acc += a * (nz[i] - acc)
        whoosh[i] = acc
    whoosh = one_pole_highpass(whoosh, 120) * (env_ad(n, 0.05, 0.7) * 0.9 + 0.1)
    # low thruster rumble that powers down
    rumble_f = 70.0 * np.exp(-t * 0.8) + 35.0
    rumble = np.sin(2 * np.pi * np.cumsum(rumble_f) / SR) * env_ad(n, 0.08, 0.5)
    # touchdown thump near the end
    td = np.zeros(n)
    start = int(0.95 * SR)
    if start < n:
        tt = t[: n - start]
        td[start:] = np.sin(2 * np.pi * (90 * np.exp(-tt * 12) + 45) * tt) * np.exp(-tt * 10)
    sig = soft_clip(0.7 * whoosh + 0.8 * rumble + 0.9 * td)
    return normalize(sig)


def build_extraction():
    """Success sting: bright major arpeggio resolving to a shimmering chord."""
    dur = 1.3
    t = t_axis(dur)
    n = t.size
    sig = np.zeros(n)
    # C major triad arpeggio up, then sustained shimmer
    notes = [523.25, 659.25, 783.99, 1046.50]  # C5 E5 G5 C6
    step = 0.09
    for i, f in enumerate(notes):
        start = int(i * step * SR)
        seg = n - start
        if seg <= 0:
            continue
        tt = t[:seg]
        e = env_ad(seg, 0.005, 0.9)
        tone = (np.sin(2 * np.pi * f * tt)
                + 0.35 * np.sin(2 * np.pi * 2 * f * tt)
                + 0.18 * np.sin(2 * np.pi * 3 * f * tt))
        sig[start:] += e * tone * 0.4
    # gentle shimmer / sparkle tail
    sparkle = bandpass(noise(n), 4000, 10000) * env_ad(n, 0.2, 0.5) * 0.12
    sig = soft_clip(sig + sparkle)
    return normalize(sig, peak=0.85)


def build_door_slide():
    """Pneumatic sci-fi door: filtered noise whoosh with a soft servo hum and a gentle
    thunk at the end."""
    dur = 0.6
    t = t_axis(dur)
    n = t.size
    # Whoosh: filtered noise that fades out
    whoosh = bandpass(noise(n), 800, 4000) * env_ad(n, 0.05, 0.35)
    # Servo hum: gentle mid-range tone like a small motor
    servo = np.sin(2 * np.pi * 800.0 * t) * env_ad(n, 0.1, 0.3) * 0.35
    # Gentle thunk near the end
    thunk = np.zeros(n)
    thunk_start = int(0.48 * SR)
    if thunk_start < n:
        tt = t[:n - thunk_start]
        thunk_f = 200.0 * np.exp(-tt * 15.0) + 50.0
        thunk[thunk_start:] = (np.sin(2 * np.pi * np.cumsum(thunk_f) / SR)
                               * np.exp(-tt * 8.0) * 0.5)
    sig = soft_clip(0.75 * whoosh + 0.5 * servo + thunk)
    return normalize(sig)


def build_hack_loop():
    """Data-stream chatter: band-passed noise blips + soft digital ticking. Seamless."""
    dur = 1.5
    t = t_axis(dur)
    n = t.size
    # Blips: sparse filtered noise bursts
    blip_nz = bandpass(noise(n), 1500, 5000)
    blips = blip_nz * (RNG.uniform(0, 1, n) > 0.88)
    blips *= env_ad(n, 0.002, 0.04) * 0.8
    # Digital ticking: steady mid-range tone with flutter
    tick_f = 1200.0
    tick = np.sin(2 * np.pi * tick_f * t)
    tick *= 0.6 + 0.4 * np.sin(2 * np.pi * 30.0 * t)  # 30Hz flutter
    tick *= env_ad(n, 0.01, 0.15) * 0.25
    sig = soft_clip(blips + tick)
    sig = make_seamless(sig, 0.3)
    return normalize(sig, peak=0.8)


def build_hack_success():
    """Affirmative chime: two-note rising arpeggio (perfect fifth) with soft sine bell
    tones and quick decay."""
    dur = 0.8
    t = t_axis(dur)
    n = t.size
    sig = np.zeros(n)
    # First note (low)
    f1 = 440.0
    start1 = 0
    seg1 = n
    tt1 = t[:seg1]
    e1 = env_ad(seg1, 0.01, 0.35)
    tone1 = np.sin(2 * np.pi * f1 * tt1) + 0.3 * np.sin(2 * np.pi * 2 * f1 * tt1)
    sig[start1:] += e1 * tone1 * 0.5
    # Second note (perfect fifth = 1.5x) — starts at ~0.2s
    f2 = 660.0
    start2 = int(0.2 * SR)
    if start2 < n:
        seg2 = n - start2
        tt2 = t[:seg2]
        e2 = env_ad(seg2, 0.01, 0.4)
        tone2 = np.sin(2 * np.pi * f2 * tt2) + 0.3 * np.sin(2 * np.pi * 2 * f2 * tt2)
        sig[start2:] += e2 * tone2 * 0.5
    sig = soft_clip(sig)
    return normalize(sig, peak=0.85)


def build_hologram_on():
    """Hologram materialize: rising filtered shimmer (high sine partials + amplitude
    flutter ~30Hz) ending in a steady soft hum fadeout."""
    dur = 0.7
    t = t_axis(dur)
    n = t.size
    # Rising shimmer: multiple high-frequency partials
    shimmer = np.zeros(n)
    for f_mult in [2.0, 3.5, 5.2]:
        f = 440.0 * f_mult
        shimmer += np.sin(2 * np.pi * f * t)
    # Amplitude flutter at ~30Hz
    flutter = 0.5 + 0.5 * np.sin(2 * np.pi * 30.0 * t)
    shimmer *= flutter * env_ad(n, 0.05, 0.5) * 0.4
    # Steady soft hum fadeout
    hum_f = 220.0
    hum = np.sin(2 * np.pi * hum_f * t) * env_ad(n, 0.3, 0.4) * 0.25
    sig = soft_clip(shimmer + hum)
    return normalize(sig, peak=0.8)


def build_ui_click():
    """Soft UI confirm tick: short filtered click with a tiny pitch drop."""
    dur = 0.12
    t = t_axis(dur)
    n = t.size
    # Filtered click: high-passed noise with quick decay
    click_nz = one_pole_highpass(noise(n), 3000)
    click = click_nz * env_ad(n, 0.001, 0.015) * 0.7
    # Pitch drop: quick frequency sweep downward
    f = 4000.0 * np.exp(-t * 20.0) + 1000.0
    pitch_sweep = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(n, 0.0005, 0.025) * 0.3
    sig = soft_clip(click + pitch_sweep)
    return normalize(sig)


def build_wave_alarm():
    """Enemy-wave alert sting: short low brass-like sawtooth swell with a dissonant
    minor-second overlay, threatening not painful."""
    dur = 1.0
    t = t_axis(dur)
    n = t.size
    # Low brass body: sawtooth-ish tone from harmonics
    f_base = 110.0
    brass = np.zeros(n)
    for k in range(1, 6):
        brass += (1.0 / k) * np.sign(np.sin(2 * np.pi * k * f_base * t))
    brass *= env_ad(n, 0.05, 0.6) * 0.7
    # Dissonant minor-second overlay (semitone below base)
    f_dissonant = 103.8
    dissonant = np.sin(2 * np.pi * f_dissonant * t) * env_ad(n, 0.08, 0.5) * 0.4
    sig = soft_clip(brass + dissonant)
    return normalize(sig)


# ----------------------------------------------------------------------------- ambience loops
def build_flight_ambience():
    """Engine/space hum: layered detuned low drones + slow filtered-noise air. Seamless."""
    dur = 8.0
    t = t_axis(dur)
    n = t.size
    drone = np.zeros(n)
    for f, a in [(55.0, 0.5), (55.3, 0.4), (110.0, 0.25), (164.5, 0.12), (82.4, 0.18)]:
        lfo = 1.0 + 0.04 * np.sin(2 * np.pi * (0.07 + a) * t)  # slow chorus shimmer
        drone += a * np.sin(2 * np.pi * f * t) * lfo
    air = one_pole_lowpass(noise(n), 900) * 0.25
    air *= 0.6 + 0.4 * np.sin(2 * np.pi * 0.05 * t)  # slow swell
    sig = soft_clip(0.8 * drone + air) * 0.9
    sig = make_seamless(sig, 0.4)
    return normalize(sig, peak=0.8)


def build_onfoot_ambience():
    """Planet wind: band-passed noise with slowly modulated cutoff + gusts. Seamless."""
    dur = 10.0
    t = t_axis(dur)
    n = t.size
    nz = noise(n)
    wind = bandpass(nz, 250, 2200)
    # gusting amplitude from summed slow LFOs
    gust = (0.5
            + 0.25 * np.sin(2 * np.pi * 0.07 * t + 0.3)
            + 0.15 * np.sin(2 * np.pi * 0.13 * t + 1.1)
            + 0.10 * np.sin(2 * np.pi * 0.031 * t))
    wind *= np.clip(gust, 0.1, 1.0)
    # faint low atmosphere bed
    bed = one_pole_lowpass(noise(n), 200) * 0.15
    sig = soft_clip(wind * 0.9 + bed)
    sig = make_seamless(sig, 0.6)
    return normalize(sig, peak=0.75)


def build_engine_loop():
    """Star-Wars-style ion-engine drone: a detuned low growl with a buzzy edge plus a
    breathy mid 'howl' that slowly swells. Seamless loop; in-game its volume + pitch
    track ship throttle, so this is the idle bed."""
    dur = 6.0
    t = t_axis(dur)
    n = t.size
    # Detuned low growl — the engine body.
    growl = np.zeros(n)
    for f, a in [(70.0, 0.5), (70.6, 0.4), (140.0, 0.22), (105.0, 0.18)]:
        growl += a * np.sin(2 * np.pi * f * t)
    growl += 0.15 * np.sign(np.sin(2 * np.pi * 70.0 * t))  # buzzy edge
    # Mid 'howl' — band-passed noise with a slow swell (the TIE scream).
    howl = bandpass(noise(n), 600, 2600)
    howl *= 0.5 + 0.5 * np.sin(2 * np.pi * 0.15 * t)
    howl *= 0.35
    sig = soft_clip(0.8 * growl + howl) * 0.9
    sig = make_seamless(sig, 0.4)
    return normalize(sig, peak=0.8)


# ----------------------------------------------------------------------------- driver
CLIPS = {
    "SwordDeflect": build_sword_deflect,
    "SwordImpact": build_sword_impact,
    "PlayerHit": build_player_hit,
    "ShipGunFire": build_ship_gun_fire,
    "BoltImpact": build_bolt_impact,
    "EnemyExplosion": build_enemy_explosion,
    "Landing": build_landing,
    "Extraction": build_extraction,
    "DoorSlide": build_door_slide,
    "HackLoop": build_hack_loop,
    "HackSuccess": build_hack_success,
    "HologramOn": build_hologram_on,
    "UIClick": build_ui_click,
    "WaveAlarm": build_wave_alarm,
    "FlightAmbience": build_flight_ambience,
    "OnFootAmbience": build_onfoot_ambience,
    "EngineLoop": build_engine_loop,
}


def main():
    default_out = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    ap = argparse.ArgumentParser(description="Generate Space Samurai SFX as WAV files.")
    ap.add_argument("--out", default=default_out, help="output folder (default: the Audio folder)")
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    print(f"Generating {len(CLIPS)} clips into {args.out}")
    for name, fn in CLIPS.items():
        write_wav(os.path.join(args.out, name + ".wav"), fn())
    print("Done. Drop these into the AudioDirector fields in the Inspector.")


if __name__ == "__main__":
    main()
