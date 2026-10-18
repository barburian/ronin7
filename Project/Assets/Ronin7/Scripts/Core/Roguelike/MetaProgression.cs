using System;
using System.Collections.Generic;
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

        /// <summary>BUG-25: true once <see cref="Load"/> has run this session. <see cref="Save"/> before
        /// it would write the zeroed defaults over the player's banked meta.json.</summary>
        private static bool loaded;

        /// <summary>BUG-25: the loaded meta.json came from a newer build. This build cannot represent
        /// all of it, so it must never write it back (it would silently downgrade the file).</summary>
        private static bool saveBlocked;

        public static bool IsLoaded => loaded;

        public static int Echoes => echoes;
        public static int BestDepth => bestDepth;
        public static int RunsCompleted => runsCompleted;
        public static int RunsWon => runsWon;

        private static bool tutorialDone;

        // BUG-39: (int)WeaknessKind values already taught (Core cannot see the Enemies enum).
        private static readonly List<int> seenWeaknesses = new();

        /// <summary>BUG-39: true once the first-encounter teach for this weakness kind has run in this save.</summary>
        public static bool HasSeen(int weaknessKind) => seenWeaknesses.Contains(weaknessKind);

        /// <summary>BUG-39: records a taught weakness kind. In memory only: persisted by the next Save
        /// (run end / purchase), so no file IO lands mid-fight.</summary>
        public static void MarkSeen(int weaknessKind)
        {
            if (!seenWeaknesses.Contains(weaknessKind)) seenWeaknesses.Add(weaknessKind);
        }

        /// <summary>P2-08: first-run onboarding finished (or skipped because the player has completed runs).</summary>
        public static bool TutorialDone => tutorialDone;

        // P4-05: archetype packs (BoonPackId) the player has unlocked. Boons from a locked pack are never offered.
        private static readonly List<string> unlockedPacks = new();

        /// <summary>P4-05 dev path (RONIN7_DEV / editor smoke): every pack reads unlocked. In memory only, never
        /// saved, and not cleared by <see cref="Reset"/> (a Load must not re-lock a dev session).</summary>
        public static bool DevUnlockAllPacks { get; set; }

        public static bool IsPackUnlocked(string packId) =>
            DevUnlockAllPacks || (!string.IsNullOrEmpty(packId) && unlockedPacks.Contains(packId));

        /// <summary>Marks a pack unlocked in memory; the caller (P4-10 Shrine purchase) saves.</summary>
        public static void UnlockPack(string packId)
        {
            if (!string.IsNullOrEmpty(packId) && !unlockedPacks.Contains(packId)) unlockedPacks.Add(packId);
        }

        // P4-08: weapons unlocked (P4-10 Shrine sells them) and the LOADOUT rack pick.
        private static readonly List<string> unlockedWeapons = new();
        private static string selectedLoadout = WeaponId.Katana;

        /// <summary>P4-08 dev path (RONIN7_DEV / editor smoke): every weapon reads unlocked. In memory only, never saved.</summary>
        public static bool DevUnlockAllWeapons { get; set; }

        /// <summary>P4-08: the katana is always unlocked; the rest need a P4-10 Shrine purchase.</summary>
        public static bool IsWeaponUnlocked(string weaponId) =>
            weaponId == WeaponId.Katana || DevUnlockAllWeapons || (!string.IsNullOrEmpty(weaponId) && unlockedWeapons.Contains(weaponId));

        /// <summary>Marks a weapon unlocked in memory; the caller (P4-10 Shrine purchase) saves.</summary>
        public static void UnlockWeapon(string weaponId)
        {
            if (!string.IsNullOrEmpty(weaponId) && !unlockedWeapons.Contains(weaponId)) unlockedWeapons.Add(weaponId);
        }

        /// <summary>P4-08: the loadout the next run starts with. A locked pick (e.g. a dev unlock that ended) reads as the katana.</summary>
        public static string SelectedLoadout => IsWeaponUnlocked(selectedLoadout) ? selectedLoadout : WeaponId.Katana;

        /// <summary>P4-08 LOADOUT rack: selects <paramref name="weaponId"/> if unlocked (false, no change, if locked). Caller saves.</summary>
        public static bool TrySelectLoadout(string weaponId)
        {
            if (!IsWeaponUnlocked(weaponId)) return false;
            selectedLoadout = weaponId;
            return true;
        }

        // P4-09: the Fracture Pressure pick (indexed by HeatRank). Only read while heat is unlocked.
        private static readonly int[] heatRanks = new int[Heat.Count];

        /// <summary>P4-09: Fracture Pressure unlocks after the first win.</summary>
        public static bool HeatUnlocked => Heat.Unlocked(runsWon);

        /// <summary>P4-09: picked ranks of <paramref name="r"/> (0 while heat is locked).</summary>
        public static int HeatRankOf(HeatRank r) => HeatUnlocked && (uint)r < Heat.Count ? heatRanks[(int)r] : 0;

        /// <summary>P4-09 heat selector: sets a rank (clamped to its max). False, no change, while locked. Caller saves.</summary>
        public static bool TrySetHeatRank(HeatRank r, int ranks)
        {
            if (!HeatUnlocked || (uint)r >= Heat.Count) return false;
            heatRanks[(int)r] = Mathf.Clamp(ranks, 0, Heat.Max(r));
            return true;
        }

        // P4-10: Shrine Art picks (stored as ArtId strings; Core cannot see the Player constants).
        private static string weaponArt = "", offhandArt = "";

        /// <summary>P4-10: the Art slot has been bought at the Shrine.</summary>
        public static bool HasArtSlot(bool weapon) =>
            UpgradeLevel(weapon ? MetaUpgradeId.WeaponArtSlot : MetaUpgradeId.OffhandArtSlot) >= 1;

        /// <summary>P4-10: the Art the next run starts with in this slot ("" while the slot is unbought).</summary>
        public static string EquippedArt(bool weapon) => !HasArtSlot(weapon) ? "" : weapon ? weaponArt : offhandArt;

        /// <summary>P4-10 Shrine Art selector ("" clears). False, no change, while the slot is unbought. Caller saves.</summary>
        public static bool TryEquipArt(bool weapon, string artId)
        {
            if (!HasArtSlot(weapon)) return false;
            if (weapon) weaponArt = artId ?? ""; else offhandArt = artId ?? "";
            return true;
        }

        // P4-10 Keepsakes: Epics picked in past runs, the ones bought, and the one the next run starts with.
        public const int KeepsakeCost = 200;
        private static readonly List<string> pastEpics = new();
        private static readonly List<string> keepsakes = new();
        private static string selectedKeepsake = "";

        public static IReadOnlyList<string> PastEpics => pastEpics;
        public static bool HasKeepsake(string epicId) => !string.IsNullOrEmpty(epicId) && keepsakes.Contains(epicId);

        /// <summary>P4-10: records an Epic pick. In memory only; persisted by the run-end Save.</summary>
        public static void NoteEpicPicked(string epicId)
        {
            if (!string.IsNullOrEmpty(epicId) && !pastEpics.Contains(epicId)) pastEpics.Add(epicId);
        }

        /// <summary>P4-10: buys the Keepsake for a past Epic and persists. False if never picked, owned or unaffordable.</summary>
        public static bool TryBuyKeepsake(string epicId)
        {
            if (string.IsNullOrEmpty(epicId) || !pastEpics.Contains(epicId) || keepsakes.Contains(epicId)) return false;
            if (!TrySpend(KeepsakeCost)) return false;
            keepsakes.Add(epicId);
            Save();
            return true;
        }

        /// <summary>P4-10: the Keepsake Epic the next run starts holding ("" = none).</summary>
        public static string SelectedKeepsake => HasKeepsake(selectedKeepsake) ? selectedKeepsake : "";

        /// <summary>P4-10: picks an owned Keepsake ("" clears). False, no change, if not owned. Caller saves.</summary>
        public static bool TrySelectKeepsake(string epicId)
        {
            if (!string.IsNullOrEmpty(epicId) && !HasKeepsake(epicId)) return false;
            selectedKeepsake = epicId ?? "";
            return true;
        }

        /// <summary>P2-08: pure load rule. A player who already finished runs before this field existed
        /// has no use for the tutorial, so runsCompleted &gt; 0 counts as done.</summary>
        internal static bool ResolveTutorialDone(MetaSaveData data) =>
            data != null && (data.tutorialDone || data.metaRunsCompleted > 0);

        /// <summary>P2-08: marks the tutorial done and persists (no-op once done).</summary>
        public static void CompleteTutorial()
        {
            if (tutorialDone) return;
            tutorialDone = true;
            Save();
        }

        /// <summary>BUG-32 dev reset (only called from DevRunTools): in-memory only, never saved, so the
        /// P2-08 prompts show again; the next Save would persist it, which is fine for a dev build.</summary>
        public static void ResetTutorialInMemory()
        {
            runsCompleted = 0;
            tutorialDone = false;
            seenWeaknesses.Clear(); // BUG-39: the weakness teaches replay too
        }

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
                case MetaUpgradeId.WeaponArtSlot:
                case MetaUpgradeId.OffhandArtSlot: return 1;
                default: return 0;
            }
        }

        // P2-01 price tables, indexed by (nextLevel - 1).
        private static readonly int[] PowerCosts = { 40, 60, 80, 100, 120 };
        private static readonly int[] BoonCosts = { 80, 150, 250 };
        private static readonly int[] RerollCosts = { 50, 100, 150 };
        // P4-10 Shrine variety unlocks (one-time; weapons and packs are "level 1" of their own id).
        private static readonly int[] ArtSlotCosts = { 250 };
        private static readonly int[] BladeCosts = { 150 };
        private static readonly int[] TwinCosts = { 300 };
        private static readonly int[] PackCosts = { 120 };

        private static bool IsWeaponItem(string id) => id == WeaponId.Nodachi || id == WeaponId.Tanto || id == WeaponId.TwinWakizashi;
        private static bool IsPackItem(string id) => id == BoonPackId.Bleed || id == BoonPackId.Storm || id == BoonPackId.Executioner;

        /// <summary>Echo price of buying <paramref name="nextLevel"/> (1-based) of an upgrade; 0 if out of range.</summary>
        public static int UpgradeCost(string id, int nextLevel)
        {
            int[] table;
            switch (id)
            {
                case MetaUpgradeId.StartingHealth:
                case MetaUpgradeId.StartingDamage: table = PowerCosts; break;
                case MetaUpgradeId.StartingBoon: table = BoonCosts; break;
                case MetaUpgradeId.RerollTokens: table = RerollCosts; break;
                case MetaUpgradeId.WeaponArtSlot:
                case MetaUpgradeId.OffhandArtSlot: table = ArtSlotCosts; break;
                case WeaponId.Nodachi:
                case WeaponId.Tanto: table = BladeCosts; break;
                case WeaponId.TwinWakizashi: table = TwinCosts; break;
                case BoonPackId.Bleed:
                case BoonPackId.Storm:
                case BoonPackId.Executioner: table = PackCosts; break;
                default: return 0;
            }
            return nextLevel >= 1 && nextLevel <= table.Length ? table[nextLevel - 1] : 0;
        }

        /// <summary>Buys the next level of an upgrade and persists. False (no change) if maxed or unaffordable.</summary>
        public static bool TryPurchase(string id)
        {
            if (IsWeaponItem(id) || IsPackItem(id)) // P4-10: one-time unlocks
            {
                if (unlockedWeapons.Contains(id) || unlockedPacks.Contains(id)) return false;
                if (!TrySpend(UpgradeCost(id, 1))) return false;
                if (IsWeaponItem(id)) UnlockWeapon(id); else UnlockPack(id);
                Save();
                return true;
            }
            int level = UpgradeLevel(id);
            if (level >= MaxLevel(id)) return false;
            if (!TrySpend(UpgradeCost(id, level + 1))) return false;
            SetUpgradeLevel(id, level + 1);
            Save();
            return true;
        }

        /// <summary>Records a finished run (win or permadeath) into the lifetime counters.
        /// <paramref name="depthReached"/> is rooms CLEARED (TotalNodes on a win), shown as n/15.</summary>
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
            tutorialDone = ResolveTutorialDone(data);
            seenWeaknesses.Clear();
            if (data.seenWeaknesses != null)
                foreach (int k in data.seenWeaknesses) MarkSeen(k);
            unlockedPacks.Clear();
            if (data.unlockedPacks != null)
                foreach (string id in data.unlockedPacks) UnlockPack(id);
            unlockedWeapons.Clear();
            if (data.unlockedWeapons != null)
                foreach (string id in data.unlockedWeapons) UnlockWeapon(id);
            selectedLoadout = string.IsNullOrEmpty(data.selectedLoadout) ? WeaponId.Katana : data.selectedLoadout;
            System.Array.Clear(heatRanks, 0, Heat.Count);
            if (data.heatRanks != null)
                for (int i = 0; i < Heat.Count && i < data.heatRanks.Count; i++)
                    heatRanks[i] = Mathf.Clamp(data.heatRanks[i], 0, Heat.Max((HeatRank)i));
            pastEpics.Clear();
            if (data.pastEpics != null)
                foreach (string id in data.pastEpics) NoteEpicPicked(id);
            keepsakes.Clear();
            if (data.keepsakes != null)
                foreach (string id in data.keepsakes)
                    if (!string.IsNullOrEmpty(id) && !keepsakes.Contains(id)) keepsakes.Add(id);
            selectedKeepsake = data.selectedKeepsake ?? "";
            weaponArt = data.weaponArt ?? "";
            offhandArt = data.offhandArt ?? "";

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
            data.tutorialDone = tutorialDone;
            data.seenWeaknesses = new List<int>(seenWeaknesses);
            data.seenWeaknesses.Sort();
            data.unlockedPacks = new List<string>(unlockedPacks);
            data.unlockedPacks.Sort(System.StringComparer.Ordinal);
            data.unlockedWeapons = new List<string>(unlockedWeapons);
            data.unlockedWeapons.Sort(System.StringComparer.Ordinal);
            data.selectedLoadout = SelectedLoadout;
            data.heatRanks = new List<int>(heatRanks);
            data.pastEpics = new List<string>(pastEpics);
            data.pastEpics.Sort(System.StringComparer.Ordinal);
            data.keepsakes = new List<string>(keepsakes);
            data.keepsakes.Sort(System.StringComparer.Ordinal);
            data.selectedKeepsake = SelectedKeepsake;
            data.weaponArt = weaponArt;
            data.offhandArt = offhandArt;

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
        /// Called once at boot. A corrupt file falls back to meta.json.bak (see
        /// <see cref="SaveSystem.ReadJson{T}"/>); if neither loads (incl. first-ever launch) this is a
        /// fresh zeroed meta and never throws — meta progress shouldn't be able to brick a boot.
        /// </summary>
        public static void Load()
        {
            Reset();
            MetaSaveData data = SaveSystem.ReadJson<MetaSaveData>(SaveSystem.MetaPath);
            if (data != null) ApplyFrom(data);
            saveBlocked = IsNewerThanThisBuild(data);
            if (saveBlocked)
                Debug.LogWarning($"MetaProgression: meta.json is version {data.version}, newer than {MetaSaveData.CurrentVersion}; it will not be overwritten.");
            loaded = true;
        }

        /// <summary>Loads meta.json unless this session already has. For callers that must bank
        /// Echoes before the normal boot load may have run (e.g. discarding a stale run.json).</summary>
        public static void EnsureLoaded()
        {
            if (!loaded) Load();
        }

        /// <summary>Pure. BUG-25 test seam.</summary>
        internal static bool IsNewerThanThisBuild(MetaSaveData data) =>
            data != null && data.version > MetaSaveData.CurrentVersion;

        /// <summary>
        /// Persists current meta state to <c>meta.json</c>, reusing <see cref="SaveSystem.WriteAtomic"/>
        /// (the same write-temp-then-move helper the numbered slots use) rather than a second write
        /// path. Called when a run ends and when an upgrade is purchased.
        /// </summary>
        public static void Save()
        {
            if (!loaded)
            {
                Debug.LogError("MetaProgression.Save called before Load; refusing to overwrite meta.json with defaults.");
                return;
            }
            if (saveBlocked)
            {
                Debug.LogWarning("MetaProgression.Save skipped: meta.json is from a newer build.");
                return;
            }
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

        /// <summary>Test-only: also forgets that Load ran (Reset deliberately keeps it, since Load
        /// itself starts with Reset).</summary>
        internal static void ResetForTests()
        {
            Reset();
            loaded = false;
            saveBlocked = false;
            DevUnlockAllPacks = false;
            DevUnlockAllWeapons = false;
        }

        public static void Reset()
        {
            echoes = 0;
            bestDepth = 0;
            runsCompleted = 0;
            runsWon = 0;
            tutorialDone = false;
            seenWeaknesses.Clear();
            unlockedPacks.Clear();
            unlockedWeapons.Clear();
            selectedLoadout = WeaponId.Katana;
            System.Array.Clear(heatRanks, 0, Heat.Count);
            pastEpics.Clear();
            keepsakes.Clear();
            selectedKeepsake = "";
            weaponArt = offhandArt = "";
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
