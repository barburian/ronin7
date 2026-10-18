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

        // BUG-39: one short teach line (<= 5 words) under the run stats, shown for a few real seconds.
        private static string hint;
        private static float hintUntil;
        private string lastHint;

        /// <summary>BUG-39: show <paramref name="text"/> on the wrist HUD for <paramref name="seconds"/> real time
        /// (picked up by the 0.25 s poll; static so the teach needs no reference to the wrist object).</summary>
        public static void ShowHint(string text, float seconds)
        {
            hint = text;
            hintUntil = Time.unscaledTime + seconds;
        }
        private (int hp, int hpMax, int node, int sector, int foes, int boons, int echoes) lastKey;
        private static readonly System.Collections.Generic.List<RoomKind> nextKinds = new(3);

        private void Refresh()
        {
            if (target == null || ended) return;
            if (!RunState.InRun) { if (target.text.Length != 0) target.text = string.Empty; shown = false; return; }
            var health = VRRig.Instance != null ? VRRig.Instance.GetComponent<Health>() : null;
            var key = (health != null ? Mathf.CeilToInt(health.Current) : 0, health != null ? Mathf.CeilToInt(health.Max) : 0,
                RunState.NodeIndex, RunState.CurrentNode.Sector, foes, RunState.Boons.Count, RunState.EchoesEarned);
            string h = Time.unscaledTime < hintUntil ? hint : null;
            if (shown && key == lastKey && ReferenceEquals(h, lastHint)) return; // no per-frame/poll string allocation when nothing changed
            lastKey = key;
            lastHint = h;
            shown = true;
            // key.node is the node id (changes on every advance); display the depth (P2-04).
            nextKinds.Clear();
            foreach (int id in RunState.NextChoices) nextKinds.Add(RunState.Map[id].Kind);
            target.text = Format(key.Item1, key.Item2, RunState.Depth, key.Item4, key.Item5, key.Item6, key.Item7, FormatNext(nextKinds));
            if (h != null) target.text += "\n" + h;
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

        /// <summary>Pure formatter — five lines, each at most 18 chars (1-indexed for display;
        /// <paramref name="depth"/>/<paramref name="sector"/> are the 0-indexed RunState values).
        /// P2-04 adds the NEXT line (<see cref="FormatNext"/>).</summary>
        public static string Format(int hp, int hpMax, int depth, int sector, int foes, int boonCount, int echoes, string next) =>
            $"HP {hp}/{hpMax}\nNODE {depth + 1}/{RunMapGenerator.TotalNodes} · S{sector + 1}\nFOES {foes} LEFT\nBOONS {boonCount} · {EchoGlyph} {echoes}\n{next}";

        /// <summary>P2-04 "NEXT:" line: one path shows its name ("NEXT: BOSS"), several show 3-letter
        /// codes ("NEXT: ELT/EVT/SHP", 17 chars max); the final node shows "NEXT: EXIT".</summary>
        public static string FormatNext(System.Collections.Generic.IReadOnlyList<RoomKind> kinds)
        {
            if (kinds == null || kinds.Count == 0) return "NEXT: EXIT";
            if (kinds.Count == 1) return "NEXT: " + RoomKindText.Name(kinds[0]);
            var sb = new System.Text.StringBuilder("NEXT: ");
            for (int i = 0; i < kinds.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(RoomKindText.Code(kinds[i]));
            }
            return sb.ToString();
        }
    }
}
