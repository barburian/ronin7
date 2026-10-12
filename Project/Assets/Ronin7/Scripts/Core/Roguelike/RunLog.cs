using System;
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
    }

    /// <summary>
    /// P0-11: local run telemetry. Every finished run (all builds) appends one line to
    /// <c>persistentDataPath/runlog.jsonl</c>; nothing leaves the device. On Quest, pull it with
    /// <c>adb pull /sdcard/Android/data/&lt;package&gt;/files/runlog.jsonl</c>.
    /// </summary>
    public static class RunLog
    {
        public const string FileName = "runlog.jsonl";

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
