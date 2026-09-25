using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Persistent, cross-run meta-currency and upgrades (mirrors <see cref="CampaignState"/>'s
    /// static-class pattern). Unlike <see cref="RunState"/>, this survives a run ending — and
    /// survives <see cref="CampaignState.Reset"/> too, since meta progress outlives any one campaign.
    /// Persisted to its own <c>meta.json</c> file beside the numbered save slots (see
    /// <see cref="MetaSaveData"/> and <see cref="Save"/>/<see cref="Load"/>) rather than inside
    /// <see cref="SaveData"/> — Roguelike-Design.md Amendment 2 (A2.1): meta progress is account-wide,
    /// not per-slot, so storing it in a slot caused real data loss and a cross-profile leak.
    /// </summary>
    public static class MetaProgression
    {
        private static int echoes;
        private static int bestDepth;
        private static int runsCompleted;
        private static int runsWon;
        private static readonly Dictionary<string, int> upgradeLevels = new();

        public static int Echoes => echoes;
        public static int BestDepth => bestDepth;
        public static int RunsCompleted => runsCompleted;
        public static int RunsWon => runsWon;

        /// <summary>Clamps negatives to 0 rather than letting Echoes go below zero.</summary>
        public static void AddEchoes(int amount)
        {
            echoes = Mathf.Max(0, echoes + amount);
        }

        /// <summary>Spends Echoes on an upgrade purchase. False (no-op) if amount is negative or
        /// exceeds the current balance.</summary>
        public static bool TrySpend(int amount)
        {
            if (amount < 0 || amount > echoes) return false;
            echoes -= amount;
            return true;
        }

        public static int UpgradeLevel(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return 0;
            return upgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        /// <summary>Clamps to [0, MaxLevel(upgradeId)]. An unrecognized upgradeId has a max of 0 and
        /// is never inserted — early-return rather than storing a zeroed key, so a typo'd id doesn't
        /// linger in <see cref="upgradeLevels"/> and pollute every future <see cref="WriteTo"/>.</summary>
        public static void SetUpgradeLevel(string upgradeId, int level)
        {
            if (string.IsNullOrEmpty(upgradeId)) return;
            int max = MaxLevel(upgradeId);
            if (max == 0) return;
            upgradeLevels[upgradeId] = Mathf.Clamp(level, 0, max);
        }

        /// <summary>Per-upgrade level cap. Single source of truth so other modules/tests don't
        /// hardcode the 5/5/3/3 maxima documented on <see cref="MetaUpgradeId"/>.</summary>
        public static int MaxLevel(string upgradeId)
        {
            switch (upgradeId)
            {
                case MetaUpgradeId.StartingHealth: return 5;
                case MetaUpgradeId.StartingDamage: return 5;
                case MetaUpgradeId.StartingBoon: return 3;
                case MetaUpgradeId.RerollTokens: return 3;
                default: return 0;
            }
        }

        /// <summary>Records a finished run (win or permadeath) into the lifetime counters.</summary>
        public static void NoteRunEnded(int depthReached, bool won)
        {
            runsCompleted++;
            if (won) runsWon++;
            bestDepth = Mathf.Max(bestDepth, depthReached);
        }

        /// <summary>Loads meta state from a saved snapshot. Clears current state and populates from data.</summary>
        public static void ApplyFrom(MetaSaveData data)
        {
            if (data == null) return;

            echoes = data.metaEchoes;
            bestDepth = data.metaBestDepth;
            runsCompleted = data.metaRunsCompleted;
            runsWon = data.metaRunsWon;

            upgradeLevels.Clear();
            if (data.metaUpgrades != null)
            {
                foreach (MetaUpgradeEntry entry in data.metaUpgrades)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.id))
                    {
                        upgradeLevels[entry.id] = Mathf.Clamp(entry.level, 0, MaxLevel(entry.id));
                    }
                }
            }
        }

        /// <summary>Writes current meta state into a MetaSaveData for persistence. Upgrades are
        /// sorted by id for deterministic files (mirrors CampaignState's sorted-list save pattern).
        /// A2.6: entries at level 0 are skipped rather than written — an upgrade that was set back
        /// down to 0 (or never inserted, see <see cref="SetUpgradeLevel"/>) shouldn't linger in the
        /// file as dead weight.</summary>
        public static void WriteTo(MetaSaveData data)
        {
            if (data == null) return;

            data.metaEchoes = echoes;
            data.metaBestDepth = bestDepth;
            data.metaRunsCompleted = runsCompleted;
            data.metaRunsWon = runsWon;

            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, int> kvp in upgradeLevels)
            {
                if (kvp.Value != 0) ids.Add(kvp.Key);
            }
            ids.Sort();

            List<MetaUpgradeEntry> entries = new List<MetaUpgradeEntry>(ids.Count);
            foreach (string id in ids)
            {
                entries.Add(new MetaUpgradeEntry { id = id, level = upgradeLevels[id] });
            }
            data.metaUpgrades = entries;
        }

        /// <summary>
        /// Loads persistent meta state from <c>meta.json</c> (see <see cref="SaveSystem.MetaPath"/>).
        /// Called once at boot. A missing file (first-ever launch) or a corrupt one both load as a
        /// fresh zeroed meta and never throw — meta progress is a bonus, not something that should be
        /// able to brick a boot.
        /// </summary>
        public static void Load()
        {
            Reset();

            string path = SaveSystem.MetaPath;
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                MetaSaveData data = JsonUtility.FromJson<MetaSaveData>(json);
                if (data != null) ApplyFrom(data);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MetaProgression.Load: failed to load meta.json, starting fresh: {ex.Message}");
                Reset();
            }
        }

        /// <summary>
        /// Persists current meta state to <c>meta.json</c>, reusing <see cref="SaveSystem.WriteAtomic"/>
        /// (the same write-temp-then-move helper the numbered slots use) rather than a second write
        /// path. Called when a run ends and when an upgrade is purchased.
        /// </summary>
        public static void Save()
        {
            try
            {
                MetaSaveData data = new MetaSaveData();
                WriteTo(data);
                string json = JsonUtility.ToJson(data, true);
                SaveSystem.WriteAtomic(SaveSystem.MetaPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"MetaProgression.Save: IO error: {ex.Message}");
            }
        }

        public static void Reset()
        {
            echoes = 0;
            bestDepth = 0;
            runsCompleted = 0;
            runsWon = 0;
            upgradeLevels.Clear();
        }

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak meta progress from a previous Play session into the next (mirrors
        // CampaignStats/EventBus's reset). A real player boots fresh and loads via ApplyFrom, so this
        // only matters for the editor's domain-reload-free Play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Reset();
    }
}
