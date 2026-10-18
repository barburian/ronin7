using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// BUG-39 first-encounter cue. The first time an enemy with a given <see cref="WeaknessKind"/> gets aggro
    /// in a save: its weak-point marker pulses for 1.5 s and the wrist HUD shows one short line. The first
    /// <see cref="WeaknessExploited"/> of that kind afterwards gets a 0.5x slow-mo for 0.4 s real time on
    /// <see cref="TimeScaleChannel.WeaknessTeach"/>, restored on the same paths as the deflect slow-mo
    /// (expiry, disable, app pause, mode change). Seen kinds persist in <see cref="MetaProgression"/>.
    /// Self-installs (persistent) like <see cref="RunPauseController"/>; does nothing outside a run.
    /// </summary>
    public class WeaknessTeach : MonoBehaviour
    {
        public const float SlowScale = 0.5f, SlowRealSeconds = 0.4f, PulseSeconds = 1.5f, HintSeconds = 4f;
        private const float ScanInterval = 0.25f;

        private static WeaknessTeach instance;

        // Kinds taught at aggro whose exploit slow-mo has not played yet (session only).
        private readonly HashSet<int> pendingSlowMo = new();
        private float slowMoEndUnscaled = -1f;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WeaknessTeach));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WeaknessTeach>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<WeaknessExploited>(OnExploited);
            EventBus.Subscribe<GameModeChanged>(OnModeChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<WeaknessExploited>(OnExploited);
            EventBus.Unsubscribe<GameModeChanged>(OnModeChanged);
            RestoreTime();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) RestoreTime();
        }

        private void OnModeChanged(GameModeChanged _) => RestoreTime();

        private void Update()
        {
            if (slowMoEndUnscaled > 0f && Time.unscaledTime >= slowMoEndUnscaled) RestoreTime();
            if (!RunState.InRun || Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanInterval;
            // Static registry, 4 Hz, no allocation: first aggro'd enemy of an untaught kind gets the cue.
            var all = Health.Active;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].TryGetComponent(out Enemy e) || !e.IsAggro || !all[i].IsAlive) continue;
                if (!TryTeach(e.Weakness)) continue;
                e.PulseWeakPoint(PulseSeconds);
                RunHudPanel.ShowHint(Hint(e.Weakness), HintSeconds);
                return; // one teach at a time
            }
        }

        /// <summary>Marks <paramref name="kind"/> seen and arms its exploit slow-mo. False when it was
        /// already taught in this save (or has no weakness to teach).</summary>
        internal bool TryTeach(WeaknessKind kind)
        {
            if (kind == WeaknessKind.None || MetaProgression.HasSeen((int)kind)) return false;
            MetaProgression.MarkSeen((int)kind);
            pendingSlowMo.Add((int)kind);
            return true;
        }

        internal void OnExploited(WeaknessExploited e)
        {
            if (!pendingSlowMo.Remove(e.Kind)) return;
            TimeScaleArbiter.SetRequest(TimeScaleChannel.WeaknessTeach, SlowScale);
            slowMoEndUnscaled = Time.unscaledTime + SlowRealSeconds;
        }

        private void RestoreTime()
        {
            TimeScaleArbiter.ClearRequest(TimeScaleChannel.WeaknessTeach);
            slowMoEndUnscaled = -1f;
        }

        /// <summary>Wrist-HUD teach line, five words or fewer and at most 18 chars (the HUD line width).</summary>
        public static string Hint(WeaknessKind k)
        {
            switch (k)
            {
                case WeaknessKind.Backstab: return "Strike its back";
                case WeaknessKind.HeavyBreak: return "Swing hard";
                case WeaknessKind.Recovery: return "Hit its recovery";
                case WeaknessKind.Alternating: return "Hit the other side";
                case WeaknessKind.Reflect: return "Deflect bolts back";
                case WeaknessKind.Linchpin: return "Kill the drone";
                case WeaknessKind.ParryGate: return "Parry, then strike";
                case WeaknessKind.CounterHit: return "Hit it mid-attack";
                case WeaknessKind.PostureOnly: return "Break its posture";
                case WeaknessKind.Blink: return "Hit after a blink";
                default: return string.Empty;
            }
        }
    }
}
