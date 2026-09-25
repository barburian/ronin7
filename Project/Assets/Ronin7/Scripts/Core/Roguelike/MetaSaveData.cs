using System;
using System.Collections.Generic;

namespace Ronin7.Core
{
    /// <summary>
    /// One meta-upgrade's persisted level. JsonUtility-serializable (see
    /// <see cref="MetaSaveData.metaUpgrades"/>). Was previously nested inside <see cref="SaveData"/>
    /// (its own file, MetaUpgradeEntry.cs) before Roguelike-Design.md Amendment 2 (A2.1) moved
    /// meta-progression out of the per-slot save; kept here next to the type that now owns it.
    /// </summary>
    [Serializable]
    public class MetaUpgradeEntry
    {
        public string id;
        public int level;
    }

    /// <summary>
    /// JsonUtility-serializable snapshot of <see cref="MetaProgression"/> — persistent, cross-run
    /// meta-currency and upgrades. Per Roguelike-Design.md Amendment 2 (A2.1), this lives in its own
    /// file (meta.json, beside the numbered slot files) instead of inside <see cref="SaveData"/>:
    /// meta progress is account-wide, not per-slot, so storing it in a slot caused real data loss
    /// (Start New Game without a prior Load left meta at its boot value of 0, and the next autosave
    /// overwrote the slot's banked Echoes with it) and a cross-profile leak (loading one slot's meta
    /// bleeding into a save made from a different slot). Its own <see cref="version"/> so this file's
    /// schema can evolve independently of <see cref="SaveData.CurrentVersion"/>.
    /// </summary>
    [Serializable]
    public class MetaSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int metaEchoes;
        public int metaBestDepth;
        public int metaRunsCompleted;
        public int metaRunsWon;
        public List<MetaUpgradeEntry> metaUpgrades = new List<MetaUpgradeEntry>();
    }
}
