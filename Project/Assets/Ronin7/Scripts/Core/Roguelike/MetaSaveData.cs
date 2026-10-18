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
        /// <summary>P2-08: first-run onboarding finished. Appended field: an old meta.json without it loads false.</summary>
        public bool tutorialDone = false;
        /// <summary>BUG-39: <c>(int)WeaknessKind</c> values whose first-encounter teach has run. Appended field
        /// (version stays 1): an old meta.json without it loads an empty list.</summary>
        public List<int> seenWeaknesses = new List<int>();
        /// <summary>P4-05: <see cref="BoonPackId"/>s bought (P4-10 Shrine). Appended field (version stays 1): an old
        /// meta.json without it loads an empty list, i.e. every archetype pack locked.</summary>
        public List<string> unlockedPacks = new List<string>();
        /// <summary>P4-08: <see cref="WeaponId"/>s unlocked (P4-10 Shrine). Appended field (version stays 1): an old
        /// meta.json without it loads an empty list, i.e. only the katana.</summary>
        public List<string> unlockedWeapons = new List<string>();
        /// <summary>P4-08: the LOADOUT rack pick. Appended field (version stays 1): missing / locked loads the katana.</summary>
        public string selectedLoadout = WeaponId.Katana;
        /// <summary>P4-09: the boot-menu Fracture Pressure pick, indexed by <c>(int)HeatRank</c>. Appended field
        /// (version stays 1): an old meta.json without it loads an empty list, i.e. no heat.</summary>
        public List<int> heatRanks = new List<int>();
    }
}
