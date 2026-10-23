using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>One finished run, as a single JSON line in <c>runlog.jsonl</c>.</summary>
    [Serializable]
    public struct RunLogEntry
    {
        public string utc;
        public uint seed;
        public bool won;
        public int depth;
        public int kills;
        public int boons;
        public int echoes;
        public float seconds;
        public string[] boonIds;
        public string deathBy;
        public string build;
        // P4-00a / BUG-41 (append-only): old lines simply lack these. evt: "start" | "end" (empty on old lines).
        public string evt;
        public string outcome;      // "won" | "died" | "abandoned" (end lines)
        public int sector;          // sector of the node the run ended on (0-based)
        public int elites;          // elite spawns
        public int resistedHits;
        public int weaknessHits;
        public string[] spawnCounts; // "Archetype:n"
        public string[] killCounts;  // "Archetype:n"
        public string[] affixCounts; // "Volatile:n"
        public string[] boonOffers;  // one per boon pick screen: "offeredA,offeredB,offeredC>picked" (picked empty = none)
        public int[] path;           // P5-08 (append-only): node ids walked, start..end (end lines; the boot map table reads it)
    }

    /// <summary>P4-00a: per-run counters behind the extra <see cref="RunLogEntry"/> fields. Reset at run start.</summary>
    public static class RunTally
    {
        private static readonly Dictionary<string, int> spawns = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> kills = new Dictionary<string, int>();
        private static readonly List<string> offers = new List<string>();
        private static readonly Dictionary<string, int> affixes = new Dictionary<string, int>();
        public static int Elites, Resisted, Exploited;

        public static void Reset()
        {
            spawns.Clear(); kills.Clear(); affixes.Clear(); offers.Clear();
            Elites = Resisted = Exploited = 0;
        }

        public static void NoteSpawn(string archetype, bool elite = false)
        {
            Bump(spawns, archetype);
            if (elite) Elites++;
        }

        public static void NoteOffer(IEnumerable<string> offeredIds, string pickedId) =>
            offers.Add(string.Join(",", offeredIds) + ">" + pickedId);

        public static string[] Offers() => offers.ToArray();

        public static void NoteAffix(string affix) => Bump(affixes, affix);

        /// <summary>Counts only archetypes that were spawned this run, so the player, allies and props are ignored.</summary>
        public static void NoteKill(string archetype)
        {
            if (spawns.ContainsKey(archetype)) Bump(kills, archetype);
        }

        public static string[] Spawns() => Format(spawns);
        public static string[] Kills() => Format(kills);
        public static string[] Affixes() => Format(affixes);

        private static void Bump(Dictionary<string, int> d, string key)
        {
            d.TryGetValue(key, out int n);
            d[key] = n + 1;
        }

        private static string[] Format(Dictionary<string, int> d)
        {
            var list = new List<string>(d.Count);
            foreach (var kv in d) list.Add(kv.Key + ":" + kv.Value);
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }
    }

    /// <summary>
    /// P0-11: local run telemetry. Every finished run (all builds) appends one line to
    /// <c>persistentDataPath/runlog.jsonl</c>; nothing leaves the device. On Quest, pull it with
    /// <c>adb pull /sdcard/Android/data/&lt;package&gt;/files/runlog.jsonl</c>.
    /// </summary>
    public static class RunLog
    {
        public const string FileName = "runlog.jsonl";
        public const string AbandonedMarker = "abandoned";

        /// <summary>Pure: an abandon is told apart from a death by the deathBy marker AbandonRun sets.</summary>
        public static string Outcome(bool won, string deathBy) =>
            won ? "won" : deathBy == AbandonedMarker ? AbandonedMarker : "died";

        /// <summary>Null = <see cref="Application.persistentDataPath"/>. Tests point this at a temp dir.</summary>
        internal static string DirectoryOverride;

        public static string FilePath => Path.Combine(DirectoryOverride ?? Application.persistentDataPath, FileName);

        public static void Append(RunLogEntry entry)
        {
            try
            {
                File.AppendAllText(FilePath, ToLine(entry));
            }
            catch (Exception ex)
            {
                // Telemetry must never break a run's end.
                Debug.LogWarning($"[RunLog] Could not append to {FileName}: {ex.Message}");
            }
        }

        /// <summary>Compact JSON plus one newline. JsonUtility escapes any newline inside a string, so
        /// a line is always exactly one record. Pure. P0-11 test seam.</summary>
        internal static string ToLine(RunLogEntry entry) => JsonUtility.ToJson(entry) + "\n";
    }
}
