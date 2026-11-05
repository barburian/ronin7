#if RONIN7_DEV
using System.Diagnostics;
using System.Globalization;
using Ronin7.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

namespace Ronin7.Player
{
    /// <summary>
    /// A15.11 proof, dev builds only: one logcat line per 30 s window of a run (and one when the run or loadout ends) with the
    /// Whip's main-thread time, missed frames and GC. Runs on every loadout, so the Katana baseline is the same line with
    /// whipMs 0. whipMsHeld and checks divide by whipFrames, the frames the Whip was
    /// actually held and stepped; whipMs and the rest divide by every in-run frame. Allocates only when it writes a line; the frame after a line is left out of the GC count, because
    /// "GC Allocated In Frame" reads the frame before.
    /// </summary>
    public class WeaponPerfLog : MonoBehaviour
    {
        private const float WindowSeconds = 30f;
        private const double FallbackHz = 90.0;
        /// <summary>A frame longer than this many refresh intervals is a missed frame.</summary>
        private const double MissedFrameFactor = 1.5;

        private static long whipTicks;
        private static long whipChecks;
        private static int whipPoints;
        private static int whipFrames;
        private static readonly System.Collections.Generic.List<XRDisplaySubsystem> displays = new System.Collections.Generic.List<XRDisplaySubsystem>(2);

        private ProfilerRecorder gc;
        private string loadout;
        private double hz, missedLimit, seconds;
        private int frames, missed, gcFrames;
        private long gcBytes, gcMaxBytes;
        private bool skipGcReading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject(nameof(WeaponPerfLog));
            DontDestroyOnLoad(go);
            go.AddComponent<WeaponPerfLog>();
        }

        /// <summary>Called by <see cref="WhipController"/> once per simulated frame.</summary>
        internal static void AddWhip(long ticks, int checks, int points)
        {
            whipTicks += ticks;
            whipChecks += checks;
            whipPoints = points;
            whipFrames++;
        }

        private void OnEnable() => gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        private void OnDisable() => gc.Dispose();

        private void Update()
        {
            string now = RunState.InRun ? WeaponId.Effective(RunState.LoadoutId) : null;
            if (now != loadout)
            {
                if (loadout != null) Flush("end");
                loadout = now;
                StartWindow();
            }
            if (loadout == null) return;

            double dt = Time.unscaledDeltaTime;
            seconds += dt;
            frames++;
            if (dt > missedLimit) missed++;
            if (skipGcReading) skipGcReading = false;
            else if (gc.Valid)
            {
                long b = gc.LastValue;
                if (b > 0L)
                {
                    gcFrames++;
                    gcBytes += b;
                    if (b > gcMaxBytes) gcMaxBytes = b;
                }
            }
            if (seconds >= WindowSeconds) Flush("30s");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && loadout != null) Flush("pause"); // headset off: keep what was measured
        }

        private void Flush(string reason)
        {
            if (frames > 0)
            {
                double whipMs = whipTicks * 1000.0 / Stopwatch.Frequency / frames;
                double whipMsHeld = whipFrames > 0 ? whipTicks * 1000.0 / Stopwatch.Frequency / whipFrames : 0.0;
                UnityEngine.Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[WeaponPerf] reason={0} loadout={1} sec={2:F1} frames={3} whipMs={4:F3} whipFrames={5} whipMsHeld={6:F3} hz={7:F0} missed={8} missedPct={9:F2} gcFrames={10} gcBytes={11} gcMaxBytes={12} gcValid={13} points={14} checks={15:F1}",
                    reason, loadout, seconds, frames, whipMs, whipFrames, whipMsHeld, hz, missed, 100.0 * missed / frames,
                    gcFrames, gcBytes, gcMaxBytes, gc.Valid ? 1 : 0, whipPoints, whipFrames > 0 ? (double)whipChecks / whipFrames : 0.0));
            }
            StartWindow();
            skipGcReading = true;
        }

        private void StartWindow()
        {
            // Once per window: GetSubsystems fills the cached list. No XR display (editor, flat) falls back to 90.
            float rate = 0f;
            SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count && rate <= 0f; i++) displays[i].TryGetDisplayRefreshRate(out rate);
            displays.Clear();
            hz = rate > 0f ? rate : FallbackHz;
            missedLimit = MissedFrameFactor / hz;
            seconds = 0.0;
            frames = missed = gcFrames = 0;
            gcBytes = gcMaxBytes = 0L;
            whipTicks = whipChecks = 0L;
            whipPoints = whipFrames = 0;
        }
    }
}
#endif
