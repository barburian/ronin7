using System;
using System.Collections.Generic;

namespace Ronin7.Core
{
    /// <summary>
    /// JsonUtility-serializable snapshot of campaign progress (one save slot's contents).
    /// itemIds/abilityIds are forward-compat: empty today, reserved so future item/ability
    /// systems can persist without a schema break (JsonUtility leaves absent fields at their
    /// initializer values, so old files load with empty lists, never null).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2; // v2: chapter restructure — completed scenes renamed to prologue entries

        public int version = CurrentVersion;
        public long savedAtUtcTicks;
        public List<string> completedPlanetScenes = new List<string>(); // landable destinationScene names
        public string lastPlanetScene = "";  // "" = none -> ship spawns at universe origin
        public bool firstPlanetDeparted;     // mirrors Galaxy1Progress.FirstPlanetDeparted
        public int shipHullIndex;            // mirrors ShipSelection.SelectedHullIndex
        public List<string> itemIds = new List<string>();
        public List<string> abilityIds = new List<string>();
        public List<string> storyFlags = new List<string>();

        // Ronin7.Core.CampaignStats counters. Forward-compat: JsonUtility leaves absent int fields
        // at 0, so old save files load these as zero rather than throwing (same guarantee as the
        // itemIds/abilityIds lists above).
        public int statsEnemiesDefeated;
        public int statsPerfectParries;
        public int statsPostureBreaks;
        public int statsBestCombo;
        public int statsBestKillStreak;
        public int statsBestNearMissStreak;
        public int statsBestParryStreak;
        public int statsFlawlessEncounters;

        // Ronin7.Core.DrillBestScores ledger. Forward-compat: JsonUtility leaves an absent list at
        // its initializer value (empty), so old save files load with no drill scores instead of throwing
        // (same guarantee as itemIds/abilityIds above).
        public List<DrillScoreEntry> drillScores = new List<DrillScoreEntry>();
    }
}
