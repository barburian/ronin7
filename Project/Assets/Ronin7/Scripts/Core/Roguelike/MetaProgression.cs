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
        private static int bestHits = -1; // P9-03: fewest PlayerHits on any won run; -1 = never won
        private static int difficulty = Difficulty.Unset; // R13: select-screen pick; Unset until chosen
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

        /// <summary>A15.12: a won run with at most this many PlayerHits unlocks the Whip.</summary>
        public const int WhipMaxHits = 3;

        /// <summary>P9-03: fewest PlayerHits taken on any won run, any weapon; -1 = never won.</summary>
        public static int BestHits => bestHits;

        /// <summary>R13: the select-screen mode. Unset resolves to Easy for a save with no runs, Normal otherwise.</summary>
        public static DifficultyMode SelectedDifficulty =>
            difficulty == Difficulty.Unset ? Difficulty.DefaultFor(runsCompleted) : Difficulty.From(difficulty);

        public static void SelectDifficulty(DifficultyMode mode) => difficulty = (int)mode;

        /// <summary>A15.13: "BEST: 7", or "BEST: --" before the first win. Shared by the Armory and the select cards.</summary>
        public static string BestHitsText(int best) => best >= 0 ? "BEST: " + best : "BEST: --";

        /// <summary>A15.12: run wins needed before the Shrine sells this weapon (0 = none).</summary>
        public static int WinsRequired(string id) => id == WeaponId.TwinWakizashi ? 1 : id == WeaponId.PolearmShield ? 2 : 0;

        /// <summary>Locked-weapon requirement line: "300 ECHOES + 1 WIN", or the Whip challenge with the best score.</summary>
        public static string RequirementText(string id, int best)
        {
            if (id == WeaponId.Whip) return "WIN ON NORMAL+ WITH " + WhipMaxHits + " HITS MAX / " + BestHitsText(best);
            int cost = UpgradeCost(id, 1), wins = WinsRequired(id);
            string text = cost > 0 ? cost + " ECHOES" : "";
            if (wins > 0) text += (text.Length > 0 ? " + " : "") + wins + (wins == 1 ? " WIN" : " WINS");
            return text.Length > 0 ? text : "LOCKED";
        }

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
        public static string SelectedLoadout => IsWeaponUnlocked(selectedLoadout) ? WeaponId.Effective(selectedLoadout) : WeaponId.Katana;

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

        // P4-10 / R9: the Art picks (stored as ArtId strings; Core cannot see the Player constants).
        // Slots are free from the start and default to Charged Slash / Kiai Palm (a save that chose nothing).
        private const string DefaultWeaponArt = "art.charged_slash", DefaultOffhandArt = "art.kiai_palm";
        private static string weaponArt = DefaultWeaponArt, offhandArt = DefaultOffhandArt;

        /// <summary>The Art the next run starts with in this slot.</summary>
        public static string EquippedArt(bool weapon) => weapon ? weaponArt : offhandArt;

        /// <summary>Armory Art selector. An empty id restores the slot default. Caller saves.</summary>
        public static bool TryEquipArt(bool weapon, string artId)
        {
            if (weapon) weaponArt = string.IsNullOrEmpty(artId) ? DefaultWeaponArt : artId;
            else offhandArt = string.IsNullOrEmpty(artId) ? DefaultOffhandArt : artId;
            return true;
        }

        /// <summary>P2-08: pure load rule. A player who already finished runs before this field existed
        /// has no use for the tutorial, so runsCompleted &gt; 0 counts as done.</summary>
        internal static bool ResolveTutorialDone(MetaSaveData data) =>
            data != null && (data.tutorialDone || data.metaRunsCompleted > 0);

        /// <summary>P6-06: show the prompts on a first run, or on any run while the Replay Tutorial toggle is on.
        /// Never when no save was loaded. Does not touch runsCompleted.</summary>
        public static bool ShouldShowTutorial(bool loaded, bool tutorialDone, bool replay) => loaded && (!tutorialDone || replay);

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
                default: return 0;
            }
        }

        // P2-01 price tables, indexed by (nextLevel - 1).
        private static readonly int[] PowerCosts = { 40, 60, 80, 100, 120 };
        private static readonly int[] BoonCosts = { 80, 150, 250 };
        private static readonly int[] RerollCosts = { 50, 100, 150 };
        // P4-10 Shrine variety unlocks (one-time; weapons and packs are "level 1" of their own id).
        private static readonly int[] BladeCosts = { 150 };
        private static readonly int[] TwinCosts = { 300 };
        private static readonly int[] PolearmCosts = { 450 }; // A15.12: also needs 2 wins, see WinsRequired

        private static bool IsWeaponItem(string id) => id == WeaponId.Nodachi || id == WeaponId.Tanto || id == WeaponId.TwinWakizashi || id == WeaponId.PolearmShield;

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
                case WeaponId.Nodachi:
                case WeaponId.Tanto: table = BladeCosts; break;
                case WeaponId.TwinWakizashi: table = TwinCosts; break;
                case WeaponId.PolearmShield: table = PolearmCosts; break;
                default: return 0;
            }
            return nextLevel >= 1 && nextLevel <= table.Length ? table[nextLevel - 1] : 0;
        }

        /// <summary>Buys the next level of an upgrade and persists. False (no change) if maxed or unaffordable.</summary>
        public static bool TryPurchase(string id)
        {
            if (id == WeaponId.Whip) return false; // A15.12: never for sale, NoteRunEnded unlocks it
            if (IsWeaponItem(id)) // P4-10: one-time unlocks
            {
                if (unlockedWeapons.Contains(id)) return false;
                if (IsWeaponItem(id) && !WeaponId.OnRoster(id)) return false; // A15.1
                if (runsWon < WinsRequired(id)) return false; // A15.12: checked before any Echoes are spent
                if (!TrySpend(UpgradeCost(id, 1))) return false;
                UnlockWeapon(id);
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
        public static void NoteRunEnded(int depthReached, bool won, int hitsTaken = -1, DifficultyMode mode = DifficultyMode.Normal)
        {
            if (difficulty == Difficulty.Unset) difficulty = (int)SelectedDifficulty; // pin the default before runsCompleted flips it
            runsCompleted++;
            if (won)
            {
                runsWon++;
                if (hitsTaken >= 0 && !DevUnlockAllWeapons && Difficulty.CountsForWhip(mode)) // R13: Easy wins count for runsWon only; -1 = no count (abandoned run); a dev-flag win must not persist an unlock or BEST
                {
                    if (bestHits < 0 || hitsTaken < bestHits) bestHits = hitsTaken;
                    if (hitsTaken <= WhipMaxHits) UnlockWeapon(WeaponId.Whip); // A15.12; the caller saves
                }
            }
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
            bestHits = data.metaBestHits;
            difficulty = data.difficulty < 0 ? Difficulty.Unset : Mathf.Clamp(data.difficulty, 0, Difficulty.Count - 1);
            tutorialDone = ResolveTutorialDone(data);
            seenWeaknesses.Clear();
            if (data.seenWeaknesses != null)
                foreach (int k in data.seenWeaknesses) MarkSeen(k);
            unlockedWeapons.Clear();
            if (data.unlockedWeapons != null)
                foreach (string id in data.unlockedWeapons) UnlockWeapon(id);
            selectedLoadout = string.IsNullOrEmpty(data.selectedLoadout) ? WeaponId.Katana : data.selectedLoadout;
            System.Array.Clear(heatRanks, 0, Heat.Count);
            if (data.heatRanks != null)
                for (int i = 0; i < Heat.Count && i < data.heatRanks.Count; i++)
                    heatRanks[i] = Mathf.Clamp(data.heatRanks[i], 0, Heat.Max((HeatRank)i));
            weaponArt = string.IsNullOrEmpty(data.weaponArt) ? DefaultWeaponArt : data.weaponArt;
            offhandArt = string.IsNullOrEmpty(data.offhandArt) ? DefaultOffhandArt : data.offhandArt;

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
            data.metaBestHits = bestHits;
            data.difficulty = difficulty;
            data.tutorialDone = tutorialDone;
            data.seenWeaknesses = new List<int>(seenWeaknesses);
            data.seenWeaknesses.Sort();
            data.unlockedWeapons = new List<string>(unlockedWeapons);
            data.unlockedWeapons.Sort(System.StringComparer.Ordinal);
            data.selectedLoadout = SelectedLoadout;
            data.heatRanks = new List<int>(heatRanks);
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
            DevUnlockAllWeapons = false;
        }

        public static void Reset()
        {
            echoes = 0;
            bestDepth = 0;
            runsCompleted = 0;
            runsWon = 0;
            bestHits = -1;
            difficulty = Difficulty.Unset;
            tutorialDone = false;
            seenWeaknesses.Clear();
            unlockedWeapons.Clear();
            selectedLoadout = WeaponId.Katana;
            System.Array.Clear(heatRanks, 0, Heat.Count);
            weaponArt = DefaultWeaponArt;
            offhandArt = DefaultOffhandArt;
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
