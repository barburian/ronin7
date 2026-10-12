using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Mid-run autosave (run.json beside meta.json). Written on entering each node so a crash or a
    /// Quest app kill resumes the run at the start of that node instead of losing it and its
    /// unbanked Echoes. The map is NOT stored: it is rebuilt from <see cref="seed"/>
    /// (<see cref="RunMapGenerator"/> is deterministic). See <see cref="RunState.ToSnapshot"/> /
    /// <see cref="RunState.Restore"/>.
    /// </summary>
    [Serializable]
    public class RunSnapshot
    {
        /// <summary>Bump whenever the fields below or the seed→map generation change meaning; a
        /// snapshot with any other version is discarded rather than resumed.</summary>
        public const int CurrentVersion = 1;

        public int version; // no initializer: a file missing the field reads as 0 → discarded
        public uint seed;
        public int nodeIndex;
        public int echoesEarned;
        public int rerollTokens;
        public int revivesSpent;
        public float playerHealth = -1f;
        public List<string> boons = new List<string>(); // one entry per stack
        public List<string> grantedAbilities = new List<string>();

        /// <summary>This run's kills so far, so the run summary's kill delta survives a resume.</summary>
        public int runKills;

        public static void Write(RunSnapshot snapshot)
        {
            try
            {
                SaveSystem.WriteAtomic(SaveSystem.RunSnapshotPath, JsonUtility.ToJson(snapshot));
            }
            catch (Exception ex)
            {
                Debug.LogError($"RunSnapshot.Write: IO error: {ex.Message}");
            }
        }

        /// <summary>The saved snapshot, or null if there is none, it can't be parsed, or it is from
        /// another version. A discarded snapshot first banks its unbanked Echoes as a lost run
        /// (BUG-25: they used to vanish), then is deleted so it never prompts again.</summary>
        public static RunSnapshot Read()
        {
            RunSnapshot snapshot = SaveSystem.ReadJson<RunSnapshot>(SaveSystem.RunSnapshotPath);
            if (snapshot == null || snapshot.version == CurrentVersion) return snapshot;

            Debug.LogWarning($"RunSnapshot: discarding version {snapshot.version} snapshot (current {CurrentVersion}); banking its {snapshot.echoesEarned} Echoes.");
            MetaProgression.EnsureLoaded();
            MetaProgression.AddEchoes(snapshot.echoesEarned);
            MetaProgression.NoteRunEnded(snapshot.nodeIndex, false);
            MetaProgression.Save();
            Delete();
            return null;
        }

        public static void Delete()
        {
            try
            {
                SaveSystem.DeleteWithBackup(SaveSystem.RunSnapshotPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"RunSnapshot.Delete: IO error: {ex.Message}");
            }
        }
    }
}
