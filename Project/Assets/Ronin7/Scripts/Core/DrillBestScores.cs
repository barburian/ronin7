using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>One drill's persisted best score. JsonUtility-serializable (see <see cref="SaveData.drillScores"/>).</summary>
    [Serializable]
    public struct DrillScoreEntry
    {
        public string id;
        public int best;
    }

    /// <summary>
    /// Session-lifetime best-score ledger for dojo drills, keyed by drill id (mirrors
    /// <see cref="CampaignStats"/>'s static-class pattern). <see cref="Ronin7.Player.DojoDrillController"/>
    /// reports a completed run via <see cref="RecordIfBetter"/>; <see cref="CampaignState"/> carries the
    /// ledger across saves via <see cref="ToSaveList"/>/<see cref="ApplyFrom"/>.
    /// </summary>
    public static class DrillBestScores
    {
        private static readonly Dictionary<string, int> bests = new();

        /// <summary>Best recorded score for the given drill id. 0 for an unknown/null/empty id.</summary>
        public static int BestFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            return bests.TryGetValue(id, out int best) ? best : 0;
        }

        /// <summary>Records score for id if it beats the current best; never lowers it. Returns the
        /// stored best after the call. No-op (returns 0) for a null/empty id.</summary>
        public static int RecordIfBetter(string id, int score)
        {
            if (string.IsNullOrEmpty(id)) return 0;

            int current = BestFor(id);
            if (score > current)
            {
                bests[id] = score;
                return score;
            }
            return current;
        }

        /// <summary>Snapshot as a sorted-by-id list for persistence (deterministic files, mirrors
        /// CampaignState's sorted-list save pattern).</summary>
        public static List<DrillScoreEntry> ToSaveList()
        {
            List<string> ids = new List<string>(bests.Keys);
            ids.Sort();

            List<DrillScoreEntry> result = new List<DrillScoreEntry>(ids.Count);
            foreach (string id in ids)
            {
                result.Add(new DrillScoreEntry { id = id, best = bests[id] });
            }
            return result;
        }

        /// <summary>Loads the ledger from a saved snapshot. Clears current state first. Ignores
        /// null/empty ids (defensive, mirrors CampaignState.ApplyFrom).</summary>
        public static void ApplyFrom(List<DrillScoreEntry> entries)
        {
            bests.Clear();
            if (entries == null) return;

            foreach (DrillScoreEntry entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.id))
                {
                    bests[entry.id] = entry.best;
                }
            }
        }

        public static void Reset() => bests.Clear();

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak best scores from a previous run into the next (mirrors
        // CampaignStats/EventBus's reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Reset();
    }
}
