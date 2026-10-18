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
        /// snapshot with any other version is discarded rather than resumed — its Echoes banked first
        /// (P0-09). v2 (P2-04): branching map; nodeIndex is a node id, plus path/depth/maxHealthBonus.
        /// v3 (P4-11): Shards split from Echoes, the deferred P4-03..P4-09 run fields, and the BUG-35
        /// <see cref="RunRng.ForNode"/> seed mix (a v2 run would resume with different node contents).</summary>
        public const int CurrentVersion = 3;

        public int version; // no initializer: a file missing the field reads as 0 → discarded
        public uint seed;
        public int nodeIndex;
        /// <summary>v1/v2 only: the run's unbanked Echoes (banked on discard). v3 writes 0; see <see cref="shards"/>.</summary>
        public int echoesEarned;
        public int rerollTokens;
        public int revivesSpent;
        public float playerHealth = -1f;
        public List<string> boons = new List<string>(); // one entry per stack
        public List<string> grantedAbilities = new List<string>();

        /// <summary>This run's kills so far, so the run summary's kill delta survives a resume.</summary>
        public int runKills;

        /// <summary>P1-07: Unbroken already spent this run. APPENDED field (no version bump): a legacy
        /// run.json without it deserializes to false.</summary>
        public bool unbrokenUsed;

        /// <summary>v2: node ids walked this run, start first; <see cref="nodeIndex"/> is the last.</summary>
        public List<int> path = new List<int>();

        /// <summary>v2: rows cleared before the current node (0..14). v1 files have no field; their
        /// <see cref="nodeIndex"/> already was the depth (linear map) — see <see cref="DepthOf"/>.</summary>
        public int depth;

        /// <summary>v2: event max-HP change for the run (Blood Pact).</summary>
        public float maxHealthBonus;

        /// <summary>v3 (P4-11): the run's Shards. Never banked: a discarded v3+ run banks its loss Echo result.</summary>
        public int shards;

        /// <summary>v3: equipped Weapon / Off-hand Art ids ("" = none) (P4-03/P4-10).</summary>
        public string weaponArtId = "";
        public string offhandArtId = "";

        /// <summary>v3: one boon id per Forge temper level (P4-07).</summary>
        public List<string> temperLevels = new List<string>();

        /// <summary>v3: the run's weapon (WeaponId) (P4-08).</summary>
        public string loadoutId = "";

        /// <summary>v3: the run's Fracture Pressure ranks, indexed by HeatRank (P4-09).</summary>
        public List<int> heatRanks = new List<int>();

        /// <summary>Depth reached by a saved run, for banking it as a loss: v1 (linear map) stored the
        /// depth in nodeIndex; v2+ store it in <see cref="depth"/>.</summary>
        public static int DepthOf(RunSnapshot s) => s.version <= 1 ? s.nodeIndex : s.depth;

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

        /// <summary>Echoes a discarded snapshot banks: v1/v2 stored unbanked Echoes (bank them, BUG-25); v3+ hold
        /// Shards, which are never banked, so bank the normal LOSS result for the depth reached (P4-11).</summary>
        public static int DiscardEchoes(RunSnapshot s) =>
            s.version <= 2 ? s.echoesEarned : RunScaling.EchoResult(DepthOf(s), false);

        /// <summary>The saved snapshot, or null if there is none, it can't be parsed, or it is from
        /// another version. A discarded snapshot first banks <see cref="DiscardEchoes"/> as a lost run
        /// (BUG-25: they used to vanish), then is deleted so it never prompts again.</summary>
        public static RunSnapshot Read()
        {
            RunSnapshot snapshot = SaveSystem.ReadJson<RunSnapshot>(SaveSystem.RunSnapshotPath);
            if (snapshot == null || snapshot.version == CurrentVersion) return snapshot;

            int echoes = DiscardEchoes(snapshot);
            Debug.LogWarning($"RunSnapshot: discarding version {snapshot.version} snapshot (current {CurrentVersion}); banking its {echoes} Echoes.");
            MetaProgression.EnsureLoaded();
            MetaProgression.AddEchoes(echoes);
            MetaProgression.NoteRunEnded(DepthOf(snapshot), false);
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
