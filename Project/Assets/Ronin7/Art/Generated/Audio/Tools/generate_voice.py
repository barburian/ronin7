#!/usr/bin/env python3
"""
Space Samurai â€” voice-over generation pipeline.

Generates voice-over WAVs from the EP01 voice manifest using edge-tts.
Each speaker maps to a specific neural voice with customized rate/pitch.
Outputs 22050 Hz mono 16-bit WAV files (or MP3 if ffmpeg unavailable).

Usage:
    python generate_voice.py                                          # use defaults
    python generate_voice.py --manifest PATH                          # custom manifest
    python generate_voice.py --out DIR                                # custom output dir
    python generate_voice.py --force                                  # regenerate all
    python generate_voice.py --manifest PATH --out DIR --force        # combined

Defaults:
    --manifest:  ../Voice/ep01_voice_manifest.json (relative to script)
    --out:       ../Voice/ (relative to script)
"""

import argparse
import asyncio
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

try:
    import edge_tts
except ImportError:
    print("[ERROR] edge-tts not installed. Install with: pip install edge-tts")
    sys.exit(1)

# Speaker to voice mapping: (voice_name, rate, pitch)
SPEAKER_VOICES = {
    "Kessler": ("en-US-GuyNeural", "-5%", "+0Hz"),
    "Ronin-7": ("en-US-SteffanNeural", "-10%", "-10Hz"),
    "Khall": ("en-GB-RyanNeural", "-15%", "-15Hz"),
    "Empire Trooper 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Empire Trooper 2": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Dominion Trooper": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Dock Hand": ("en-AU-WilliamMultilingualNeural", "+0%", "+0Hz"),
    "Trader": ("en-GB-ThomasNeural", "+0%", "+0Hz"),
    "Ship computer": ("en-US-RogerNeural", "-10%", "-12Hz"),
    # Chapter 1 (Galaxy 1) speakers
    "Trooper 1": ("en-US-ChristopherNeural", "+22%", "-5Hz"),
    "Trooper 2": ("en-US-ChristopherNeural", "+24%", "-7Hz"),
    "Squad Leader (V.O.)": ("en-US-BrianNeural", "+15%", "-4Hz"),
    "Comm (V.O.)": ("en-GB-RyanNeural", "+12%", "-15Hz"),
    "Handler (Hologram)": ("en-GB-RyanNeural", "+10%", "-15Hz"),
    "Drone": ("en-US-RogerNeural", "+15%", "-12Hz"),
    # Chapter 2 (Galaxy 1) speakers
    "Resh": ("en-US-EricNeural", "+0%", "-5Hz"),
    "Broker": ("en-US-JasonNeural", "+18%", "+4Hz"),
    # Chapter 3 (Galaxy 1) speakers — the katana shadow-AI: "Shadow" pre-naming, "Echo" after.
    # Same voice both ways (it IS the same being), slightly warmer once named.
    "Shadow": ("en-US-AriaNeural", "-8%", "-6Hz"),
    "Echo": ("en-US-AriaNeural", "-4%", "-4Hz"),
    # Chapter 4 (Galaxy 1) speakers. Mera Voss and Khall reuse their EP08/base entries above.
    "Tessa Rin": ("en-GB-SoniaNeural", "-6%", "-5Hz"),
    "Kerrax": ("en-US-RogerNeural", "-10%", "-12Hz"),
    # EP02 speakers
    "Station Traffic Control": ("en-US-JennyNeural", "+0%", "+0Hz"),
    "Captain Resh": ("en-GB-SoniaNeural", "-5%", "-5Hz"),
    "Saffron Veil Enforcer 1": ("en-GB-LibbyNeural", "+0%", "+0Hz"),
    "Saffron Veil Enforcer 2": ("en-GB-ThomasNeural", "+2%", "-3Hz"),
    "Saffron Veil Enforcer 3": ("en-GB-ThomasNeural", "+5%", "-5Hz"),
    "Saffron Veil Guard 1": ("en-GB-ThomasNeural", "+0%", "+0Hz"),
    "Saffron Veil Guard 2": ("en-GB-ThomasNeural", "+3%", "-5Hz"),
    "Saffron Veil Guard 3": ("en-GB-ThomasNeural", "+5%", "+0Hz"),
    "Saffron Veil Guard 4": ("en-GB-ThomasNeural", "+2%", "-3Hz"),
    "Iris": ("en-US-AnaNeural", "+0%", "+0Hz"),
    "Samurai-4": ("en-GB-LibbyNeural", "-10%", "-8Hz"),
    "Gilded Maw Handler 1": ("en-US-EricNeural", "-5%", "-5Hz"),
    "Veil Escort 1": ("en-GB-ThomasNeural", "-2%", "-5Hz"),
    "Gilded Maw Handler 2": ("en-US-EricNeural", "-5%", "-5Hz"),
    "Dominion Enforcer 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Dominion Enforcer 2": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    # EP03 speakers
    "Dominion Enforcer 3": ("en-US-ChristopherNeural", "+8%", "-3Hz"),
    "Mira": ("en-US-AnaNeural", "+0%", "+10Hz"),
    "Senna": ("en-US-AnaNeural", "-8%", "-6Hz"),
    "Dominion Hunter": ("en-US-MichelleNeural", "-8%", "-8Hz"),
    "Hollow Kings Pilot": ("en-US-EricNeural", "-12%", "-20Hz"),
    "Crimson Lotus Enforcer 1": ("en-GB-RyanNeural", "+8%", "-5Hz"),
    "Resistance Doctor": ("en-US-JennyNeural", "-6%", "-4Hz"),
    "Rustfang Raider": ("en-AU-WilliamMultilingualNeural", "+8%", "-8Hz"),
    # EP04 speakers
    "Katana": ("en-US-AvaNeural", "-12%", "-4Hz"),
    "Archive Ghost Signal": ("en-US-JennyNeural", "-10%", "-10Hz"),
    "Archive Terminal": ("en-US-JennyNeural", "-6%", "-14Hz"),
    "Rustfang Scout 1": ("en-AU-WilliamMultilingualNeural", "+6%", "-10Hz"),
    "Rustfang Scout 2": ("en-AU-WilliamMultilingualNeural", "+10%", "-6Hz"),
    "Rustfang Scout 3": ("en-AU-WilliamMultilingualNeural", "+12%", "-12Hz"),
    "Rustfang Scout 4": ("en-AU-WilliamMultilingualNeural", "+8%", "-4Hz"),
    "Drone 1": ("en-US-EmmaNeural", "+8%", "+12Hz"),
    "Drone 2": ("en-US-EmmaNeural", "+10%", "+8Hz"),
    "Drone 3": ("en-US-EmmaNeural", "+12%", "+10Hz"),
    "Sentry 1": ("en-US-ChristopherNeural", "-10%", "-15Hz"),
    "Sentry 2": ("en-US-ChristopherNeural", "-12%", "-12Hz"),
    "Sentry 3": ("en-US-ChristopherNeural", "-14%", "-15Hz"),
    "Enforcer": ("en-US-SteffanNeural", "-6%", "-6Hz"),
    "Interceptor 1": ("en-US-ChristopherNeural", "+10%", "-10Hz"),
    # EP05 speakers
    "Ronin-9": ("en-US-AvaNeural", "-10%", "-6Hz"),
    "Station Comms": ("en-GB-ThomasNeural", "-5%", "+0Hz"),
    "Enforcer 1": ("en-US-ChristopherNeural", "+6%", "-8Hz"),
    "Enforcer 2": ("en-US-ChristopherNeural", "+8%", "-5Hz"),
    "Enforcer 3": ("en-US-ChristopherNeural", "+10%", "-3Hz"),
    "Hunter 1": ("en-US-EricNeural", "-8%", "-10Hz"),
    "Hunter 2": ("en-US-EricNeural", "-6%", "-7Hz"),
    "Silencer 1": ("en-US-ChristopherNeural", "-8%", "-12Hz"),
    "Silencer 2": ("en-US-ChristopherNeural", "-10%", "-10Hz"),
    "Dominion Officer": ("en-US-GuyNeural", "+0%", "-2Hz"),
    "Dominion Boarder 1": ("en-US-ChristopherNeural", "+4%", "-6Hz"),
    "Dominion Boarder 2": ("en-US-ChristopherNeural", "+6%", "-4Hz"),
    "Elite Operative 1": ("en-US-EricNeural", "-4%", "-8Hz"),
    "Elite Operative 2": ("en-US-EricNeural", "-2%", "-6Hz"),
    "Dominion Fighter 1": ("en-US-GuyNeural", "+2%", "-4Hz"),
    "Dominion Fighter 2": ("en-US-GuyNeural", "+4%", "-2Hz"),
    # EP06 speakers
    "Hollow Kings Operative 1": ("en-US-EricNeural", "-12%", "-20Hz"),
    "Hollow Kings Operative 2": ("en-US-EricNeural", "-10%", "-18Hz"),
    "Hollow Kings Operative 3": ("en-US-EricNeural", "-14%", "-22Hz"),
    "Hollow Kings Operative 4": ("en-US-EricNeural", "-11%", "-19Hz"),
    "Hollow Kings Operative 5": ("en-US-ChristopherNeural", "-8%", "-15Hz"),
    "Hollow Kings Operative 6": ("en-US-ChristopherNeural", "-10%", "-18Hz"),
    "Hollow Kings Operative 7": ("en-US-ChristopherNeural", "-12%", "-20Hz"),
    "Hollow Kings Operative 8": ("en-US-EricNeural", "-8%", "-15Hz"),
    "Dominion Trooper 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Dominion Trooper 2": ("en-US-ChristopherNeural", "+7%", "-7Hz"),
    "Dominion Trooper 3": ("en-US-ChristopherNeural", "+4%", "-3Hz"),
    "Dominion Trooper 4": ("en-US-ChristopherNeural", "+6%", "-6Hz"),
    "Dominion Trooper 5": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Dominion Trooper 6": ("en-US-GuyNeural", "+3%", "-5Hz"),
    "Dominion Trooper 7": ("en-US-GuyNeural", "+5%", "-8Hz"),
    "Dominion Trooper 8": ("en-US-GuyNeural", "+4%", "-6Hz"),
    "Dominion Elite 1": ("en-US-SteffanNeural", "-20%", "-20Hz"),
    "Dominion Elite 2": ("en-US-SteffanNeural", "-20%", "-20Hz"),
    # EP07 speakers
    "Cipher": ("en-US-SteffanNeural", "-10%", "-10Hz"),
    "Tessa": ("en-GB-SoniaNeural", "-6%", "-5Hz"),
    "Yard Enforcer 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Yard Enforcer 2": ("en-US-ChristopherNeural", "+7%", "-7Hz"),
    "Yard Enforcer 3": ("en-US-ChristopherNeural", "+8%", "-8Hz"),
    "Dominion Servitor": ("en-US-EricNeural", "-15%", "-30Hz"),
    "Cipher (recorded)": ("en-US-SteffanNeural", "-12%", "-15Hz"),
    "Ronin-7 (recorded)": ("en-US-SteffanNeural", "-12%", "-15Hz"),
    "Khall (recorded)": ("en-GB-RyanNeural", "-18%", "-18Hz"),
    "Dominion Guard 1": ("en-US-SteffanNeural", "-8%", "-10Hz"),
    "Dominion Guard 2": ("en-US-SteffanNeural", "-6%", "-8Hz"),
    "Dominion Interceptor Pilot": ("en-US-ChristopherNeural", "+10%", "-3Hz"),
    # EP08 speakers
    # Distinct from Echo's AriaNeural so the Ch4 Beat5 scene reads as two people.
    "Mera Voss": ("en-US-MichelleNeural", "-12%", "-8Hz"),
    "Reaper Unit": ("en-US-RogerNeural", "-15%", "-22Hz"),
    "Lotus Drone 1": ("en-US-EricNeural", "-14%", "-28Hz"),
    "Lotus Drone 2": ("en-US-EricNeural", "-16%", "-26Hz"),
    "Lotus Enforcer 1": ("en-GB-RyanNeural", "+6%", "-6Hz"),
    "Lotus Enforcer 3": ("en-GB-RyanNeural", "+12%", "-8Hz"),
    "Dominion Scout 1": ("en-US-ChristopherNeural", "+6%", "-6Hz"),
    # EP09 speakers
    "Vera Dusk": ("en-GB-LibbyNeural", "-4%", "-2Hz"),
    "Barkeep": ("en-AU-WilliamMultilingualNeural", "-6%", "-6Hz"),
    "Salvager Patron 1": ("en-GB-ThomasNeural", "-3%", "-6Hz"),
    "Salvager Patron 2": ("en-GB-ThomasNeural", "+4%", "-2Hz"),
    "Syndicate Runner 1": ("en-US-EricNeural", "+6%", "-4Hz"),
    "Syndicate Runner 2": ("en-US-EricNeural", "+9%", "-2Hz"),
    "Dominion Scout 2": ("en-US-ChristopherNeural", "+8%", "-8Hz"),
    "Dominion Scout 3": ("en-US-ChristopherNeural", "+4%", "-4Hz"),
    "Child Echo 1": ("en-US-AnaNeural", "-5%", "+4Hz"),
    "Child Echo 2": ("en-US-AnaNeural", "-2%", "+8Hz"),
    # EP10 speakers
    "Sister Meredith": ("en-GB-SoniaNeural", "-15%", "-12Hz"),
    "Teenager 1": ("en-US-AnaNeural", "+2%", "-2Hz"),
    "Teenager 2": ("en-US-AnaNeural", "-4%", "+2Hz"),
    # EP11 speakers
    "Master Kaelen": ("en-US-RogerNeural", "-10%", "-8Hz"),
    # Chapter 6 (Galaxy 1) speakers. Master Kaelen/Morrigan reuse their EP11/EP12 entries above.
    "Matron Hespa": ("en-GB-SoniaNeural", "-8%", "+2Hz"),
    "Drillmaster Caradoc": ("en-US-ChristopherNeural", "-18%", "-16Hz"),
    # EP12 speakers
    "Morrigan": ("en-US-MichelleNeural", "-8%", "-6Hz"),
    "Dominion Purifier Lead": ("en-US-SteffanNeural", "-3%", "-8Hz"),
    "Dominion Purifier 2": ("en-US-EricNeural", "-2%", "-6Hz"),
    # EP13 speakers
    "Lyris": ("en-US-AvaNeural", "-2%", "-2Hz"),
    "Coral Vex": ("en-CA-ClaraNeural", "-6%", "-6Hz"),
    "Yask": ("en-US-BrianNeural", "-8%", "-12Hz"),
    "Memory Grinder Lead": ("en-US-ChristopherNeural", "-6%", "-10Hz"),
    "Extraction Guard Captain": ("en-GB-LibbyNeural", "-2%", "-4Hz"),
    "Dominion Sentry 3": ("en-US-ChristopherNeural", "-10%", "-8Hz"),
    "Ronin-10": ("en-US-SteffanNeural", "-14%", "-14Hz"),
    # EP14 speakers
    "PROTOCOL-VERITY": ("en-US-AvaNeural", "-12%", "-12Hz"),
    "Automated Defense System": ("en-US-RogerNeural", "-15%", "-15Hz"),
    "Hunter Drone Lead": ("en-US-AndrewNeural", "-12%", "-12Hz"),
    # New speakers (from dialogue batch)
    "Automated Beacon": ("en-US-BrianNeural", "-15%", "-20Hz"),
    "Gilded Maw Operative": ("en-GB-MaisieNeural", "+0%", "+0Hz"),
    "Mirs": ("en-CA-LiamNeural", "-3%", "-2Hz"),
    "Emberhand Enforcer Lead": ("en-US-AndrewMultilingualNeural", "-5%", "-8Hz"),
    "Pale Choir Assassin Lead": ("en-IE-EmilyNeural", "-2%", "+2Hz"),
    "Dominion Squad Lead": ("en-US-BrianMultilingualNeural", "-8%", "-6Hz"),
    "Syndicate Hunter Lead": ("en-AU-NatashaNeural", "+5%", "-5Hz"),
    "Dreadnought Commander": ("en-NZ-MitchellNeural", "-12%", "-15Hz"),
    # EP16 speakers
    "Sela": ("en-AU-NatashaNeural", "-8%", "-8Hz"),  # EP16 Emberborn monk (renamed from "Vess" to free the name for EP20's ally)
    "Vess": ("en-IN-NeerjaNeural", "-2%", "-2Hz"),   # EP20 Emberborn ally #8 (Vendor of Ghosts)
    "Morrow": ("en-IE-ConnorNeural", "-8%", "-8Hz"),
    "Salvage Pirate 1": ("en-NG-AbeoNeural", "+10%", "-5Hz"),
    "Salvage Pirate 2": ("en-PH-JamesNeural", "+15%", "-8Hz"),
    "Dominion Soldier 1": ("en-SG-WayneNeural", "+6%", "-4Hz"),
    "Dominion Soldier 2": ("en-ZA-LukeNeural", "+8%", "-6Hz"),
    # EP17 speakers (Galaxy 3 — The Pit and the Pattern). Voices reused from proven-valid entries above.
    "Gryph": ("en-US-RogerNeural", "-12%", "-18Hz"),          # Pitmaw pit-lord, gravelly/deep
    "Tisane": ("en-GB-LibbyNeural", "+2%", "+2Hz"),           # female mid-20s, sharp survivor
    "Vesper": ("en-US-MichelleNeural", "-5%", "-6Hz"),        # female mid-30s, precise/military
    "Rustfang Breaker 1": ("en-AU-WilliamMultilingualNeural", "+5%", "-8Hz"),
    "Rustfang Breaker 2": ("en-AU-WilliamMultilingualNeural", "+9%", "-5Hz"),
    "Rustfang Corporal": ("en-US-ChristopherNeural", "+4%", "-6Hz"),  # clinical military
    "Pit Master": ("en-NZ-MitchellNeural", "-10%", "-12Hz"),  # deep arena announcer
    # EP18 speakers (Galaxy 3 — The Drowning Deep). Voices reused from proven-valid entries above.
    "Takeshi": ("en-US-AndrewNeural", "-6%", "-6Hz"),
    "Sable Dross": ("en-IE-EmilyNeural", "-2%", "+2Hz"),
    "Commodore": ("en-GB-SoniaNeural", "-12%", "-10Hz"),
    "Bartender": ("en-AU-WilliamMultilingualNeural", "-6%", "-6Hz"),
    "Tide Baron Enforcer": ("en-US-EricNeural", "-8%", "-10Hz"),
    # "Enforcer 1" already defined in the EP05 block above — reused as-is.
    "Researcher 1": ("en-US-MichelleNeural", "-6%", "-8Hz"),
    "Defense System (Overhead)": ("en-US-RogerNeural", "-15%", "-15Hz"),
    "Prisoner-Worker": ("en-US-AnaNeural", "-8%", "-6Hz"),
    # EP19 speakers (Galaxy 3 — The Ledger of Rust). Voices reused from proven-valid entries above.
    "Tara": ("en-US-AriaNeural", "-8%", "-6Hz"),                      # female late-20s, precise military, hollow edge
    "Cassie-04": ("en-US-AnaNeural", "-4%", "+2Hz"),                 # female early-20s, conditioning breaking
    "Operative": ("en-US-AnaNeural", "-6%", "-2Hz"),                # same actor pre-name (Beats 8-9)
    "Vault Operative": ("en-US-AnaNeural", "-4%", "-2Hz"),         # the "Seven?" line (Beat 4)
    "Vault Operative Commander": ("en-US-MichelleNeural", "-6%", "-8Hz"),
    "Patrol Commander": ("en-GB-RyanNeural", "-6%", "-6Hz"),       # geometric accountant-officer
    "Pursuit Fighter 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),
    "Pursuit Fighter 2": ("en-US-ChristopherNeural", "+8%", "-7Hz"),
    "Bank Guard 1": ("en-US-EricNeural", "+4%", "-6Hz"),
    "Bank Guard 2": ("en-US-EricNeural", "+7%", "-8Hz"),
    "Dominion Signal": ("en-US-AnaNeural", "-8%", "-6Hz"),         # degrading automated distress (same actor as Operative)
    # EP20 speakers (Galaxy 4)
    "Kess": ("en-US-BrianNeural", "+5%", "+5Hz"),
    "Tide Baron Squad Lead": ("en-US-GuyNeural", "+3%", "-4Hz"),
    "Rustfang Enforcer": ("en-AU-WilliamMultilingualNeural", "+10%", "-8Hz"),
    "Ninefold Bank Commander": ("en-US-ChristopherNeural", "+2%", "-4Hz"),
    "Crimson Lotus Commander": ("en-GB-RyanNeural", "+2%", "-6Hz"),
    "Crimson Lotus Enforcer 2": ("en-GB-RyanNeural", "+10%", "-6Hz"),
    "Crimson Lotus Enforcer 3": ("en-GB-RyanNeural", "+6%", "-4Hz"),
    "Dominion Drone Network": ("en-US-EricNeural", "-15%", "-30Hz"),
    "Dominion Drone 1": ("en-US-EmmaNeural", "+8%", "+12Hz"),
    "Dominion Drone 2": ("en-US-EmmaNeural", "+10%", "+8Hz"),
    "Tide Baron Squadron Leader": ("en-US-GuyNeural", "+5%", "-6Hz"),
    "Rustfang Pit Commander": ("en-AU-WilliamMultilingualNeural", "+8%", "-10Hz"),
    "Hollow Kings Infiltrator": ("en-US-MichelleNeural", "-6%", "-12Hz"),
    "Dominion Elite 3": ("en-US-SteffanNeural", "-18%", "-18Hz"),
    # EP21 speakers (Galaxy 3 — The Crimson Sleep). Voices reused from proven-valid entries above.
    "Kade": ("en-US-RogerNeural", "-8%", "-10Hz"),                       # male mid-50s, hollow intelligence officer, confession-weary
    "Ronin-8": ("en-US-AvaNeural", "-4%", "+2Hz"),                      # female early-20s, raw, newly woken from dreamstate
    "Girl (Age Nine)": ("en-US-AnaNeural", "+0%", "+8Hz"),             # young female orphan, clear, too-old-for-innocence
    "Lotus Warrior": ("en-US-SteffanNeural", "-14%", "-18Hz"),         # dream self-copy: Cipher's voice twisted/chanting
    "Lotus Enforcer 2": ("en-GB-RyanNeural", "+10%", "-6Hz"),         # Keth'vor enforcer (1 and 3 already defined above)
    "Lotus Commander": ("en-GB-RyanNeural", "-2%", "-8Hz"),           # Keth'vor ritual command precision
    "Lotus Guard 1": ("en-GB-RyanNeural", "+6%", "-4Hz"),
    "Dominion Elite Operative 1": ("en-US-MichelleNeural", "-6%", "-8Hz"),  # female late-20s, clipped/emotionless
    "Dominion Elite Operative 2": ("en-US-ChristopherNeural", "+4%", "-6Hz"),  # male mid-20s, cold efficiency
    "Dominion Elite Operative 3": ("en-US-EricNeural", "-2%", "-4Hz"),  # male early-20s, programming fracturing
    # EP22 speakers (Galaxy 3 — The Void Inheritors). Voices reused from proven-valid entries above.
    "Irene Sols": ("en-GB-SoniaNeural", "-8%", "-8Hz"),
    "Lira": ("en-US-AnaNeural", "+0%", "+8Hz"),
    "Ronin-1": ("en-US-AndrewNeural", "-10%", "-10Hz"),
    "Ronin-12": ("en-US-MichelleNeural", "-6%", "-6Hz"),
    "Security Mech 1": ("en-US-EricNeural", "-14%", "-28Hz"),
    "Security Mech 2": ("en-US-EricNeural", "-16%", "-26Hz"),
    "Vault Security": ("en-US-RogerNeural", "-15%", "-15Hz"),
    "Farm Overseer": ("en-US-GuyNeural", "+0%", "-2Hz"),
    "Repair Drone 1": ("en-US-EmmaNeural", "+8%", "+12Hz"),
    "Repair Drone 2": ("en-US-EmmaNeural", "+10%", "+8Hz"),
    "Dominion Carrier Commander": ("en-US-ChristopherNeural", "+2%", "-4Hz"),
    "Boarding Team Leader": ("en-US-BrianNeural", "+0%", "-4Hz"),
    # EP23 speakers (Galaxy 3 — Thermopause). Voices reused from proven-valid entries above.
    "Vale": ("en-US-MichelleNeural", "-12%", "-10Hz"),              # Commander Vale, female 50s, commanding/precise
    "Empire Drone": ("en-US-EricNeural", "-15%", "-30Hz"),         # corrupted salvage-drone AI, deeply wrong
    "Sentinel Platform 1": ("en-US-RogerNeural", "-15%", "-15Hz"), # automated ice-world sentinel, emotionless synth
    "Sentinel Platform 2": ("en-US-RogerNeural", "-12%", "-18Hz"),
    "Cryo-Interface": ("en-US-AnaNeural", "-8%", "-6Hz"),          # looping fragment, human struggling under synthesis
    "Cryo-Animate 1": ("en-US-AvaNeural", "-12%", "-16Hz"),        # half-revived operative, ice-distorted female
    "Cryo-Animate 2": ("en-US-AvaNeural", "-14%", "-14Hz"),
    "Station AI": ("en-US-JennyNeural", "-6%", "-14Hz"),           # clinical archive log voice
    "Defense Drone 3": ("en-US-EmmaNeural", "+10%", "+8Hz"),       # vault immune-protocol drone
    "Defense Drone 4": ("en-US-EmmaNeural", "+12%", "+10Hz"),
    "Dominion Destroyer Commander": ("en-US-ChristopherNeural", "+0%", "-4Hz"),  # boarding-assault commander
    # EP24 speakers (Galaxy 3 FINALE — The Fracture Protocol). Clones are copies of Cipher (en-US-SteffanNeural),
    # so the male clone voices share Cipher's voice with slight pitch/rate variance. Voices reused from proven-valid entries above.
    "Seven-Prime": ("en-US-AvaNeural", "-4%", "+0Hz"),               # individuating clone, female, fractured/afraid then defiant
    "Cache": ("en-US-RogerNeural", "-8%", "-10Hz"),                  # archivist clone, older male, philosophy from raw data
    "Clone Authority 1": ("en-US-SteffanNeural", "-8%", "-8Hz"),     # male port-authority clone, Cipher's voice, signal fracturing
    "Clone Authority 2": ("en-US-JennyNeural", "-6%", "-8Hz"),       # female port-authority clone, command authority contradicting
    "Clone Child": ("en-US-AnaNeural", "+0%", "-4Hz"),              # child-variant clone, consciousness as pain
    "Clone Soldier 1": ("en-US-SteffanNeural", "-12%", "-12Hz"),    # male vault clone, Cipher's voice cracking into anguish
    "Dominion Operative 1": ("en-US-ChristopherNeural", "+0%", "-4Hz"),  # Khall's elite strike unit, flawless/emotionless
    "Dying Clone": ("en-US-MichelleNeural", "-8%", "-6Hz"),         # female clone, Dominion accent cracking into something human
    # EP25 speakers (Galaxy 4 launch — The Sterile Reckoning). Cipher/Kessler/Khall reuse their existing entries above.
    "Harrow": ("en-GB-ThomasNeural", "+0%", "-2Hz"),                # Aureling station facilitator, clipped/oily, sells miracles then betrays
    "Dr. Heris": ("en-GB-SoniaNeural", "-8%", "-8Hz"),             # Aurexian surgeon, late 40s, ice with sorrow underneath — she built Cipher
    "Dominion Commando 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),  # black-armor hunter, professional protocol, no malice
    "Dominion Commando 2": ("en-US-ChristopherNeural", "+8%", "-3Hz"),  # hunter, frustration cracking through the conditioning
    # EP26 speakers (Galaxy 4 — The Requiem Protocol)
    "Mortis": ("en-GB-RyanNeural", "-12%", "-12Hz"),
    "Mortis (Voice Recording)": ("en-GB-RyanNeural", "-15%", "-16Hz"),
    "Sallow": ("en-IE-EmilyNeural", "-6%", "-4Hz"),
    "Varrik": ("en-AU-WilliamMultilingualNeural", "+4%", "-8Hz"),
    "Katana (Interior VO)": ("en-US-AvaNeural", "-12%", "-4Hz"),
    # EP27 speakers (Galaxy 4 — The Hollow Choir). Voices reused from proven-valid entries above.
    "Drek": ("en-US-RogerNeural", "-6%", "-8Hz"),                      # dockmaster, male 50s, weathered/incurious
    "Meren": ("en-US-MichelleNeural", "-4%", "-4Hz"),                  # scavenger matriarch, female 40s, layered/practical
    "Choir Cultist 1": ("en-IE-EmilyNeural", "-4%", "+0Hz"),          # ethereal Harmonian, female, soft with steel
    "Dying Cultist": ("en-AU-WilliamMultilingualNeural", "-8%", "-6Hz"), # Harmonian male, breathless/breaking
    "Bloodied Elder": ("en-GB-SoniaNeural", "-10%", "-8Hz"),          # Harmonian elder, female 50s, resolute through pain
    "Khall (Memory)": ("en-US-ChristopherNeural", "-6%", "-8Hz"),     # Khall heard in memory — same casting as Khall, slightly degraded
    "Dominion Assault Commander": ("en-US-ChristopherNeural", "+5%", "-4Hz"), # sharp, clipped, professional under stress
    "Dominion Weapons Officer": ("en-US-BrianNeural", "+2%", "-2Hz"), # younger, professional but uncertain
    # EP28 speakers (Galaxy 4 — The Harvest Moon Festival). Voices reused from proven-valid entries above.
    "Maya Selene": ("en-US-MichelleNeural", "-4%", "+2Hz"),                 # festival administrator, practical warm female, fearless
    "Dominion Scan Agent 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),    # security protocol, professional detachment
    "Dominion Scan Agent 2": ("en-US-ChristopherNeural", "+8%", "-3Hz"),    # security protocol variant, higher tempo
    "Dominion Commander": ("en-US-SteffanNeural", "-6%", "-8Hz"),           # operations commander, cold precise authority
    # EP30 speakers (Galaxy 4 — The Vault Within). Cipher/Kessler/Mera Voss/Khall/Katana (Interior VO) reuse entries above.
    "Soren": ("en-US-SteffanNeural", "-10%", "-10Hz"),                       # reclaimed birth name — identical to Cipher for vocal continuity
    "Cipher (Recording - Younger)": ("en-US-SteffanNeural", "-6%", "+0Hz"), # younger archived recording — softer, slightly higher
    "Khall (Hologram)": ("en-GB-RyanNeural", "-18%", "-18Hz"),              # preloaded hologram — matches Khall (recorded) degradation
    "Station Control": ("en-US-BrianNeural", "+8%", "-2Hz"),                # panicked relay-station operator
    "Broker (Hollow Kings)": ("en-US-RogerNeural", "-8%", "-10Hz"),         # smooth cultured syndicate broker
    # EP31 speakers (Galaxy 4 — The Eternal Cycle). Soren/Kessler reuse entries above. All voices proven-valid.
    "Dr. Lyssa Chen": ("en-US-MichelleNeural", "-8%", "-4Hz"),              # looped station commander, female mid-40s, level/precise, fear used up
    "Dr. Heris (Echo)": ("en-GB-SoniaNeural", "-10%", "-12Hz"),            # Heris time-echo — her cast (line above) degraded/stuttering across loop boundaries
    "Corrupted Echo of Soren": ("en-US-SteffanNeural", "-18%", "-22Hz"),   # Soren's own voice corrupted — same actor, stretched thin/glitching
    "Dr. Marcus Renn": ("en-US-RogerNeural", "-12%", "-14Hz"),             # project director, male 60s, gravelly with centuries of regret
    # EP32 speakers (Galaxy 4 FINALE arc — The Throne of Ashes). Soren/Kessler/Khall/Iris reuse entries above. All voices proven-valid elsewhere in this file.
    "Commander Vale": ("en-US-MichelleNeural", "-12%", "-10Hz"),           # Soren's old squad-lead, female 50s — same casting as "Vale" (EP23) for continuity
    "Khall (Recording)": ("en-GB-RyanNeural", "-18%", "-16Hz"),           # Khall's archived vault confession — his cast, exhausted/degraded
    "Dominion Pilot 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),       # Obsidian Rank interceptor, clipped/synchronized
    "Dominion Pilot 2": ("en-US-ChristopherNeural", "+8%", "-3Hz"),       # interceptor variant, interchangeable
    "Automated Fortress Voice": ("en-US-RogerNeural", "-15%", "-20Hz"),   # synthesized fortress dock control, all menace no welcome
    "Dominion Soldier": ("en-US-ChristopherNeural", "+4%", "-5Hz"),       # hangar Obsidian Rank, networked organism
    "Revenant Threne": ("en-IE-ConnorNeural", "-6%", "-12Hz"),            # Pale Choir High Priest, ethereal Harmonian, beautiful/wrong
    "Choir Operative 1": ("en-IE-EmilyNeural", "-2%", "+2Hz"),            # Harmonian ascetic, female, soft with steel
    "Choir Operative 2": ("en-US-AndrewMultilingualNeural", "-4%", "-8Hz"),  # Harmonian male, layered harmonics
    "Rogue Officer 1": ("en-US-SteffanNeural", "-8%", "-10Hz"),           # Beacon-networked throne guard, flat precision
    "Privateer Captain": ("en-US-EricNeural", "-12%", "-20Hz"),          # Hollow Kings blockade, Null-touched glitch/erasure
    # EP33 speakers (Galaxy 4 SERIES FINALE — The Tenfold Pact). Soren/Kessler/Khall/Maelgorn-adjacent and the ten allies
    # (Mera Voss/Captain Resh/Gryph/Vess/Coral Vex/Morrigan/Dr. Heris/Sallow/Sable Dross/Cassie-04) plus Samurai-4 reuse
    # entries above. Only EP33-introduced/uncast speakers are added here; all voices proven-valid elsewhere in this file.
    "Maelgorn": ("en-GB-ThomasNeural", "-14%", "-14Hz"),                 # First Overseer — ancient, refined, never-questioned certainty
    "Dominion Strike Leader": ("en-US-ChristopherNeural", "+3%", "-6Hz"),# Lantern strike-team lead, clipped never-questioned obedience
    "Dominion Trooper 1": ("en-US-ChristopherNeural", "+5%", "-5Hz"),    # black-ops trooper, aggressive focus
    "Dominion Trooper 2": ("en-US-ChristopherNeural", "+8%", "-3Hz"),    # trooper variant, panicked when formation breaks
    "Obsidian Trooper 1": ("en-US-ChristopherNeural", "+2%", "-7Hz"),    # Synod fortress trooper, emotionless Beacon-command
    "Dominion Guard 1": ("en-US-ChristopherNeural", "+6%", "-4Hz"),      # Crucible medical-level guard, precise
    "Privateers Commander": ("en-US-EricNeural", "-6%", "-10Hz"),        # power-vacuum opportunist hitting the refugee convoy
}

DEFAULT_VOICE = ("en-US-AriaNeural", "+0%", "+0Hz")


async def generate_voiceover(file_name, speaker, text, out_dir, force):
    """
    Generate a single voice-over line.
    Returns (status, file_name, error_msg) where status is 'generated', 'skipped', or 'failed'.
    """
    # Determine output file extension based on ffmpeg availability
    has_ffmpeg = shutil.which("ffmpeg") is not None
    ext = ".wav" if has_ffmpeg else ".mp3"
    out_path = os.path.join(out_dir, f"{file_name}{ext}")

    # Skip if a prior output already exists (either extension â€” Ep01Builder loads .wav then .mp3,
    # so a previous run's .mp3 satisfies the contract just as a .wav does) and not force.
    if not force:
        for prior_ext in (".wav", ".mp3"):
            if os.path.exists(os.path.join(out_dir, f"{file_name}{prior_ext}")):
                return ("skipped", file_name, None)

    # Get voice parameters
    if speaker in SPEAKER_VOICES:
        voice, rate, pitch = SPEAKER_VOICES[speaker]
    else:
        voice, rate, pitch = DEFAULT_VOICE
        print(f"[WARN] Unknown speaker '{speaker}', using default voice {voice}")

    # Retry up to 3 times on network error
    for attempt in range(1, 4):
        try:
            # Generate audio with edge-tts
            communicate = edge_tts.Communicate(text, voice, rate=rate, pitch=pitch)

            if has_ffmpeg:
                # Save to temp MP3, then convert to WAV
                with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as tmp:
                    tmp_path = tmp.name

                # Stream audio to temp file
                async for chunk in communicate.stream():
                    if chunk["type"] == "audio":
                        with open(tmp_path, "ab") as f:
                            f.write(chunk["data"])

                # Convert to WAV using ffmpeg
                try:
                    subprocess.run(
                        [
                            "ffmpeg",
                            "-y",
                            "-i",
                            tmp_path,
                            "-ar",
                            "22050",
                            "-ac",
                            "1",
                            "-acodec",
                            "pcm_s16le",
                            "-q:a",
                            "9",
                            out_path,
                        ],
                        check=True,
                        stdout=subprocess.DEVNULL,
                        stderr=subprocess.DEVNULL,
                    )
                    os.unlink(tmp_path)
                except subprocess.CalledProcessError as e:
                    if os.path.exists(tmp_path):
                        os.unlink(tmp_path)
                    raise e
            else:
                # Save directly to MP3 (truncate any prior output so a regen replaces, not appends)
                if os.path.exists(out_path):
                    os.remove(out_path)
                async for chunk in communicate.stream():
                    if chunk["type"] == "audio":
                        with open(out_path, "ab") as f:
                            f.write(chunk["data"])

            return ("generated", file_name, None)

        except Exception as e:
            if attempt < 3:
                # Exponential backoff: 2s, 4s, then fail
                await asyncio.sleep(2 ** attempt)
            else:
                return ("failed", file_name, str(e))

    return ("failed", file_name, "Max retries exceeded")


async def main():
    parser = argparse.ArgumentParser(
        description="Generate voice-over WAVs from EP01 voice manifest using edge-tts"
    )
    parser.add_argument(
        "--manifest",
        default=None,
        help="Path to voice manifest JSON (default: ../Voice/ep01_voice_manifest.json relative to script)",
    )
    parser.add_argument(
        "--out",
        default=None,
        help="Output directory (default: ../Voice/ relative to script)",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate all files, overwriting existing",
    )

    args = parser.parse_args()

    # Set defaults relative to script location
    script_dir = Path(__file__).parent
    manifest_path = args.manifest or str(script_dir.parent / "Voice" / "ep01_voice_manifest.json")
    out_dir = args.out or str(script_dir.parent / "Voice")

    # Ensure output directory exists
    Path(out_dir).mkdir(parents=True, exist_ok=True)

    # Load manifest
    if not os.path.exists(manifest_path):
        print(f"[ERROR] Manifest not found: {manifest_path}")
        sys.exit(1)

    try:
        with open(manifest_path) as f:
            manifest = json.load(f)
    except json.JSONDecodeError as e:
        print(f"[ERROR] Failed to parse manifest: {e}")
        sys.exit(1)

    lines = manifest.get("lines", [])
    if not lines:
        print("[WARN] No lines found in manifest")
        sys.exit(0)

    print(f"[Info] Loaded {len(lines)} lines from {manifest_path}")
    print(f"[Info] Output directory: {out_dir}")
    if args.force:
        print("[Info] Force regenerate enabled")
    print()

    # Generate all lines concurrently
    tasks = [
        generate_voiceover(
            line_data["file"],
            line_data["speaker"],
            line_data["text"],
            out_dir,
            args.force,
        )
        for line_data in lines
    ]

    results = await asyncio.gather(*tasks)

    # Count results
    generated = sum(1 for r in results if r[0] == "generated")
    skipped = sum(1 for r in results if r[0] == "skipped")
    failed = sum(1 for r in results if r[0] == "failed")

    # Print summary
    print(f"\n[Summary] Generated: {generated}, Skipped: {skipped}, Failed: {failed}")

    # Print failed lines
    if failed > 0:
        print("\n[Failed lines]:")
        for status, file_name, error in results:
            if status == "failed":
                print(f"  {file_name}: {error}")
        sys.exit(1)

    print()
    sys.exit(0)


if __name__ == "__main__":
    asyncio.run(main())
