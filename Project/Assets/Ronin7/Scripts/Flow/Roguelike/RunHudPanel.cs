using Ronin7.Core;
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
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RunStarted>(OnRunStarted);
            EventBus.Unsubscribe<RoomCleared>(OnRoomCleared);
            EventBus.Unsubscribe<BoonChosen>(OnBoonChosen);
            EventBus.Unsubscribe<RunEnded>(OnRunEnded);
        }

        private void OnRunStarted(RunStarted _) => Refresh();
        private void OnRoomCleared(RoomCleared _) => Refresh();
        private void OnBoonChosen(BoonChosen _) => Refresh();

        // A7.9: RunEnded is published before RunState.End() runs (RunState.InRun is only flipped once
        // the hub scene load completes — see RunDirector.SceneLoadRoutine), so a Refresh() here would
        // still see InRun == true and keep showing the dead run's stats for the rest of the hub
        // session. Clear directly instead of re-deriving from RunState.
        private void OnRunEnded(RunEnded _)
        {
            if (target != null) target.text = string.Empty;
        }

        private void Refresh()
        {
            if (target == null) return;
            target.text = RunState.InRun
                ? Format(RunState.NodeIndex, RunState.CurrentNode.Sector, RunState.Boons.Count, RunState.EchoesEarned)
                : string.Empty;
        }

        /// <summary>Pure formatter — "NODE x/15 / SECTOR s / BOONS n / ECHOES e" (1-indexed for
        /// display; <paramref name="nodeIndex"/>/<paramref name="sector"/> are the 0-indexed values
        /// RunState/RunNode use internally).</summary>
        public static string Format(int nodeIndex, int sector, int boonCount, int echoesEarned) =>
            $"NODE {nodeIndex + 1}/{RunMapGenerator.TotalNodes} / SECTOR {sector + 1} / BOONS {boonCount} / ECHOES {echoesEarned}";
    }
}
