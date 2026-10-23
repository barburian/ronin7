using System;
using System.IO;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// P5-08: read-only view of the last finished run in <c>runlog.jsonl</c> (the boot holo map table).
    /// Never writes. Pure except <see cref="TryReadLast"/>.
    /// </summary>
    public static class LastRunReader
    {
        /// <summary>The last finished-run line: an "end" line, or a pre-P4-00a line (no evt). False if none.</summary>
        public static bool TryParseLast(string text, out RunLogEntry entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(text)) return false;
            string[] lines = text.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] != '{') continue;
                RunLogEntry e;
                try { e = JsonUtility.FromJson<RunLogEntry>(line); }
                catch (Exception) { continue; } // a torn last line must not hide the run before it
                if (e.evt == "start") continue;
                entry = e;
                return true;
            }
            return false;
        }

        /// <summary>Missing or unreadable file = false (the table shows "No runs yet").</summary>
        public static bool TryReadLast(string filePath, out RunLogEntry entry)
        {
            entry = default;
            try
            {
                if (!File.Exists(filePath)) return false;
                return TryParseLast(File.ReadAllText(filePath), out entry);
            }
            catch (Exception) { return false; }
        }

        public static string Caption(RunLogEntry e)
        {
            string outcome = string.IsNullOrEmpty(e.outcome) ? RunLog.Outcome(e.won, e.deathBy) : e.outcome;
            return $"LAST RUN\nSEED {e.seed}   DEPTH {e.depth + 1}/{RunMapGenerator.TotalNodes}   {outcome.ToUpperInvariant()}";
        }

        /// <summary>Per node: 2 = on the logged path, 1 = in a row the run reached (old lines without a path), 0 = not reached.
        /// Ids outside the map are ignored.</summary>
        public static int[] NodeStates(RunMap map, RunLogEntry e)
        {
            var states = new int[map.Nodes.Length];
            if (e.path != null && e.path.Length > 0)
            {
                foreach (int id in e.path)
                    if (id >= 0 && id < states.Length) states[id] = 2;
                return states;
            }
            for (int i = 0; i < states.Length; i++)
                if (map.Nodes[i].Depth <= e.depth) states[i] = 1;
            return states;
        }
    }
}
