using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// Wrist- or hub-mounted HUD for the active run: depth ("node x/15"), sector, boon count, and
    /// echoes earned so far. Deliberately event-driven (<see cref="RunStarted"/>,
    /// <see cref="RoomCleared"/>, <see cref="BoonChosen"/>, <see cref="RunEnded"/>) rather than
    /// polled — unlike e.g. <c>CampaignStatsDisplay</c>'s throttled-Update idiom, this refreshes only
    /// on the handful of events that can actually change these numbers, so it costs nothing out of
    /// the 11.11 ms VR frame budget on the vastly more common frame where nothing changed.
    /// </summary>
    public class RunHudPanel : MonoBehaviour
    {
        [SerializeField] private TextMesh target;

        private void OnEnable()
        {
            EventBus.Subscribe<RunStarted>(OnRunStarted);
            EventBus.Subscribe<RoomCleared>(OnRoomCleared);
            EventBus.Subscribe<BoonChosen>(OnBoonChosen);
            EventBus.Subscribe<RunEnded>(OnRunEnded);
            EventBus.Subscribe<RoomProgress>(OnRoomProgress);
            EventBus.Subscribe<RunNodeEntered>(OnRunNodeEntered);
            Refresh();
            InvokeRepeating(nameof(Refresh), 0.25f, 0.25f); // HP (damage and heals); only rebuilds text on change
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RunStarted>(OnRunStarted);
            EventBus.Unsubscribe<RoomCleared>(OnRoomCleared);
            EventBus.Unsubscribe<BoonChosen>(OnBoonChosen);
            EventBus.Unsubscribe<RunEnded>(OnRunEnded);
            EventBus.Unsubscribe<RoomProgress>(OnRoomProgress);
            EventBus.Unsubscribe<RunNodeEntered>(OnRunNodeEntered);
            CancelInvoke(nameof(Refresh));
        }

        private void OnRunStarted(RunStarted _) { ended = false; Refresh(); }
        private void OnRoomCleared(RoomCleared _) => Refresh();
        private void OnBoonChosen(BoonChosen _) => Refresh();
        private void OnRunNodeEntered(RunNodeEntered _) => Refresh();
        private void OnRoomProgress(RoomProgress p) { foes = p.Remaining; Refresh(); }

        // A7.9: RunEnded is published before RunState.End() runs (RunState.InRun is only flipped once
        // the hub scene load completes — see RunDirector.SceneLoadRoutine), so a Refresh() here would
        // still see InRun == true and keep showing the dead run's stats for the rest of the hub
        // session. Clear directly instead of re-deriving from RunState.
        private void OnRunEnded(RunEnded _)
        {
            if (target != null) target.text = string.Empty;
            shown = false;
            ended = true; // InRun stays true until the hub load; the 0.25 s poll must not resurrect the stats
        }

        private int foes;
        private bool shown, ended;
        private (int hp, int hpMax, int node, int sector, int foes, int boons, int echoes) lastKey;

        private void Refresh()
        {
            if (target == null || ended) return;
            if (!RunState.InRun) { if (target.text.Length != 0) target.text = string.Empty; shown = false; return; }
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            var key = (health != null ? Mathf.CeilToInt(health.Current) : 0, health != null ? Mathf.CeilToInt(health.Max) : 0,
                RunState.NodeIndex, RunState.CurrentNode.Sector, foes, RunState.Boons.Count, RunState.EchoesEarned);
            if (shown && key == lastKey) return; // no per-frame/poll string allocation when nothing changed
            lastKey = key;
            shown = true;
            target.text = Format(key.Item1, key.Item2, key.Item3, key.Item4, key.Item5, key.Item6, key.Item7);
#if RONIN7_DEV
            baseText = target.text;
#endif
        }

#if RONIN7_DEV
        // P0-11 dev readout: smoothed frame time under the run line, 4 updates a second.
        private string baseText = string.Empty;
        private float smoothedMs;
        private float nextDevUpdate;

        private void Update()
        {
            if (target == null || !RunState.InRun) return;
            smoothedMs = Mathf.Lerp(smoothedMs, Time.unscaledDeltaTime * 1000f, 0.1f);
            if (Time.unscaledTime < nextDevUpdate) return;
            nextDevUpdate = Time.unscaledTime + 0.25f;
            target.text = $"{baseText}\n{smoothedMs:0.0} ms";
        }
#endif

        /// <summary>Glyph before the echo count; verified present in the LegacyRuntime font atlas.</summary>
        public const string EchoGlyph = "◈";

        /// <summary>Pure formatter — four lines, each at most 18 chars (1-indexed for display;
        /// <paramref name="nodeIndex"/>/<paramref name="sector"/> are the 0-indexed RunState values).</summary>
        public static string Format(int hp, int hpMax, int nodeIndex, int sector, int foes, int boonCount, int echoes) =>
            $"HP {hp}/{hpMax}\nNODE {nodeIndex + 1}/{RunMapGenerator.TotalNodes} · S{sector + 1}\nFOES {foes} LEFT\nBOONS {boonCount} · {EchoGlyph} {echoes}";
    }
}
