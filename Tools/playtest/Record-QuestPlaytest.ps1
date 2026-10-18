<#
Record a Quest playtest over wifi: screen video + logcat (Unity) + runlog.jsonl.
Usage: .\Record-QuestPlaytest.ps1 [-Ip 192.168.1.50] [-Package id] [-Bitrate 8] [-Crop 1600:900:2017:510]
Output: playtests/<timestamp>/ at repo root. Ctrl+C to stop and pull files.
#>
param(
    [string]$Ip,
    [string]$Package = 'com.cookingwithbobistudios.ronin7',
    [int]$Port = 5555,
    [int]$Bitrate = 8,                      # Mbit/s
    [string]$Crop = '1600:900:2017:510',    # scrcpy single-eye crop of the Quest 2-eye view; '' = no crop
    [switch]$NoScrcpy                       # force screenrecord fallback
)
$ErrorActionPreference = 'Stop'

# 1. Find adb: PATH, then ANDROID_HOME, then newest Unity Hub editor SDK.
$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb -and $env:ANDROID_HOME) { $adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe' }
if (-not $adb -or -not (Test-Path $adb)) {
    $adb = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $adb) { throw 'adb.exe not found (PATH, ANDROID_HOME, Unity Hub AndroidPlayer SDK).' }
Write-Host "adb: $adb"

# 2. Connect.
function Get-Serials { (& $adb devices) | Select-Object -Skip 1 | Where-Object { $_ -match '\sdevice$' } | ForEach-Object { ($_ -split '\s+')[0] } }
if ($Ip) {
    & $adb connect "${Ip}:$Port" | Write-Host
} else {
    $all = @(Get-Serials)
    $usb = $all | Where-Object { $_ -notmatch ':' } | Select-Object -First 1
    if ($usb) {
        $addr = (& $adb -s $usb shell ip -f inet addr show wlan0) -join "`n"
        if ($addr -notmatch 'inet (\d+\.\d+\.\d+\.\d+)/') { throw 'Could not read wlan0 IP; is the headset on wifi?' }
        $Ip = $Matches[1]
        & $adb -s $usb tcpip $Port | Write-Host
        Start-Sleep 2
        & $adb connect "${Ip}:$Port" | Write-Host
        Write-Host "Headset wifi IP: $Ip (unplug USB now if you like)"
    } elseif ($all | Where-Object { $_ -match ':' }) {
        $Ip = (($all | Where-Object { $_ -match ':' } | Select-Object -First 1) -split ':')[0]
    } else { throw 'No device. Attach via USB once, or pass -Ip <headset ip>.' }
}
$dev = "${Ip}:$Port"
if (-not (Get-Serials | Where-Object { $_ -eq $dev })) { throw "$dev not connected (accept the debugging prompt in the headset?)." }

$root = Split-Path (Split-Path $PSScriptRoot)
$out = Join-Path $root ("playtests\" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $out | Out-Null
Write-Host "Output: $out"

# 4a. logcat in background.
& $adb -s $dev logcat -c
$log = Start-Process $adb -ArgumentList "-s $dev logcat -v time -s Unity" -RedirectStandardOutput (Join-Path $out 'logcat-unity.txt') -NoNewWindow -PassThru

# 3. Screen recording.
$scrcpy = $null
if (-not $NoScrcpy) {
    $scrcpy = (Get-Command scrcpy -ErrorAction SilentlyContinue).Source
    if (-not $scrcpy) { $scrcpy = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\scrcpy.exe'; if (-not (Test-Path $scrcpy)) { $scrcpy = $null } }
    if (-not $scrcpy) {
        $scrcpy = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\Genymobile.scrcpy_*\scrcpy-win64-*\scrcpy.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    $env:ADB = $adb   # make scrcpy use the same adb server/binary
}
$useScrcpy = [bool]$scrcpy
$rec = $null
try {
    if ($useScrcpy) {
        $a = @('-s', $dev, '--no-playback', '--no-audio', '--video-bit-rate', "${Bitrate}M", '--record', (Join-Path $out 'screen.mkv'))
        if ($Crop) { $a += @('--crop', $Crop) }
        Write-Host 'Recording with scrcpy. Ctrl+C to stop.'
        $rec = Start-Process $scrcpy -ArgumentList $a -NoNewWindow -PassThru
        $rec.WaitForExit()
    } else {
        Write-Host 'scrcpy not found: using adb screenrecord, 180 s chunks. Ctrl+C to stop.'
        Write-Host '(Quest-native alternative: in-headset recorder, Quest button > Camera/Sharing > Record.)'
        $i = 0
        while ($true) {
            & $adb -s $dev shell screenrecord --bit-rate ($Bitrate * 1000000) --time-limit 180 ("/sdcard/pt_{0:D3}.mp4" -f $i++)
        }
    }
} finally {
    Write-Host "`nStopping..."
    if (-not $useScrcpy) {
        & $adb -s $dev shell pkill -2 screenrecord   # SIGINT so the mp4 is finalized
        Start-Sleep 2
        foreach ($f in (& $adb -s $dev shell ls /sdcard/pt_*.mp4) ) {
            $f = $f.Trim()
            if ($f -like '/sdcard/pt_*') { & $adb -s $dev pull $f $out | Out-Null; & $adb -s $dev shell rm $f }
        }
    }
    if ($log -and -not $log.HasExited) { Stop-Process -Id $log.Id -Force }
    & $adb -s $dev pull "/sdcard/Android/data/$Package/files/runlog.jsonl" $out 2>&1 | Write-Host
    Write-Host "Done: $out"
}
