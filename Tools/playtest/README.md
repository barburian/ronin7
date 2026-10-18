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
