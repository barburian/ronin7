# Record-QuestPlaytest

Wifi playtest capture for the Quest: screen video, `logcat -s Unity`, and `runlog.jsonl`.
Output goes to `playtests/<yyyyMMdd-HHmmss>/` (git-ignored).

```powershell
# First time: headset on USB, wifi on. Script switches adb to wifi and records.
powershell -ExecutionPolicy Bypass -File Tools\playtest\Record-QuestPlaytest.ps1

# Later (already paired): reconnect directly
powershell -ExecutionPolicy Bypass -File Tools\playtest\Record-QuestPlaytest.ps1 -Ip 192.168.1.50
```

Ctrl+C stops recording, pulls files, and fetches `/sdcard/Android/data/<pkg>/files/runlog.jsonl`.

Options: `-Package` (default `com.cookingwithbobistudios.ronin7`), `-Bitrate` Mbit/s (8), `-Crop` scrcpy
crop (`1600:900:2017:510`, single eye; `''` for none), `-NoScrcpy` to force the fallback.

- adb: PATH, `ANDROID_HOME`, else the Unity Hub Android SDK.
- scrcpy (preferred; found on PATH, WinGet Links, or WinGet Packages; flags verified on 4.1) writes `screen.mkv`. Without it, `adb shell screenrecord` runs in 180 s chunks
  (`pt_000.mp4`, ...), pulled on stop.
- The crop values are the usual Quest 2 numbers; tune if the framing is off.
- Quest-native alternative: the in-headset recorder (Quest button > Camera/Sharing > Record).

## analyze_runlog.py

Per-boon offered/picked counts and pick rate (flags `< 5%` / `> 40%`, the P4-06 target), per-archetype
spawn/kill counts, resisted vs weakness hits, and outcomes (won/died/abandoned). Python 3 stdlib only.
Reads only `evt == "end"` lines; identical lines across pulls are counted once (each pull copies the
device's whole cumulative log).

```powershell
python Tools\playtest\analyze_runlog.py                      # every runlog.jsonl under playtests/
python Tools\playtest\analyze_runlog.py playtests\20261018-101500 other\runlog.jsonl
python -I Tools\playtest\samples\test_analyze_runlog.py      # self-check on the synthetic sample
```

The EditMode `BoonOfferRateSimulationTests` print the simulated offer rates for comparison; only this
script's numbers (real players) decide boon magnitude changes in `BoonCatalogBuilder`.
