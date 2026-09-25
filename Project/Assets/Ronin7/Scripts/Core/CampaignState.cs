using System;
using System.Collections.Generic;

namespace Ronin7.Core
{
    /// <summary>
    /// In-session campaign progress (mirrors Galaxy1Progress pattern).
    /// Tracks which planets have been cleared and the player's current landing zone.
    /// Survives scene reloads but is reset when a new campaign begins.
    /// </summary>
    public static class CampaignState
    {
        private static readonly HashSet<string> completed = new HashSet<string>();
        private static readonly HashSet<string> storyFlags = new HashSet<string>();
        private static readonly HashSet<string> abilities = new HashSet<string>();
        public static string LastPlanetScene { get; private set; } = "";

        /// <summary>
        /// Record that the player has landed on a planet/zone.
        /// Only updates LastPlanetScene if fromSpace=true and scene is non-empty.
        /// (Interior hops like market→hideout republish LandingRequested while already on foot;
        /// they must NOT repoint LastPlanetScene away from the planet.)
        /// </summary>
        public static void NoteLanding(string destinationScene, bool fromSpace)
        {
            if (fromSpace && !string.IsNullOrEmpty(destinationScene))
            {
                LastPlanetScene = destinationScene;
            }
        }

        /// <summary>
        /// Record that a zone (corsairScene) has been completed.
        /// Returns the cleared planet scene name, or null if the zone does not count.
        /// (Empty LastPlanetScene or corsairScene == LastPlanetScene → null.)
        /// </summary>
        public static string NoteZoneCompleted(string corsairScene)
        {
            if (string.IsNullOrEmpty(LastPlanetScene) || LastPlanetScene == corsairScene)
            {
                return null;
            }

            completed.Add(LastPlanetScene);
            return LastPlanetScene;
        }

        /// <summary>
        /// Returns true if the given scene has been marked completed.
        /// </summary>
        public static bool IsCompleted(string scene)
        {
            if (string.IsNullOrEmpty(scene))
            {
                return false;
            }

            return completed.Contains(scene);
        }

        /// <summary>
        /// Record a story flag (e.g. a guard encounter cleared, a cutscene played). Ignores null/empty.
        /// </summary>
        public static void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag))
            {
                storyFlags.Add(flag);
            }
        }

        /// <summary>
        /// Return true if a story flag has been set.
        /// </summary>
        public static bool HasFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return false;
            }

            return storyFlags.Contains(flag);
        }

        /// <summary>
        /// Record that a permanent ability (see <see cref="AbilityId"/>) has been unlocked. Ignores null/empty.
        /// </summary>
        public static void UnlockAbility(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                abilities.Add(id);
            }
        }

        /// <summary>
        /// Return true if the given ability has been unlocked.
        /// </summary>
        public static bool HasAbility(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return abilities.Contains(id);
        }

        /// <summary>
        /// Number of galaxies the player has finished, derived from the "galaxyN_complete" story
        /// flags (set by each galaxy's finale mission). Persists via the normal storyFlags save.
        /// Used by SpaceEncounterManager to scale ambient hostile counts with story progress.
        /// </summary>
        public static int GalaxiesCompleted
        {
            get
            {
                int count = 0;
                for (int galaxy = 1; galaxy <= 4; galaxy++)
                {
                    if (storyFlags.Contains("galaxy" + galaxy + "_complete"))
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Read-only view of completed planet scenes.
        /// </summary>
        public static IReadOnlyCollection<string> Completed => completed;

        /// <summary>
        /// Snapshot current state as a SaveData for persistence.
        /// </summary>
        public static SaveData ToSaveData()
        {
            SaveData save = new SaveData();

            // Sort for deterministic files
            List<string> sortedCompleted = new List<string>(completed);
            sortedCompleted.Sort();
            save.completedPlanetScenes = sortedCompleted;

            List<string> sortedFlags = new List<string>(storyFlags);
            sortedFlags.Sort();
            save.storyFlags = sortedFlags;

            List<string> sortedAbilities = new List<string>(abilities);
            sortedAbilities.Sort();
            save.abilityIds = sortedAbilities;

            save.lastPlanetScene = LastPlanetScene;
            save.firstPlanetDeparted = Galaxy1Progress.FirstPlanetDeparted;
            save.shipHullIndex = ShipSelection.SelectedHullIndex;

            save.statsEnemiesDefeated = CampaignStats.EnemiesDefeated;
            save.statsPerfectParries = CampaignStats.PerfectParries;
            save.statsPostureBreaks = CampaignStats.PostureBreaks;
            save.statsBestCombo = CampaignStats.BestCombo;
            save.statsBestKillStreak = CampaignStats.BestKillStreak;
            save.statsBestNearMissStreak = CampaignStats.BestNearMissStreak;
            save.statsBestParryStreak = CampaignStats.BestParryStreak;
            save.statsFlawlessEncounters = CampaignStats.FlawlessEncounters;

            save.drillScores = DrillBestScores.ToSaveList();

            return save;
        }

        /// <summary>
        /// Load campaign state from a saved snapshot.
        /// Clears current state and populates from the given data.
        /// </summary>
        public static void ApplyFrom(SaveData data)
        {
            if (data == null)
            {
                return;
            }

            completed.Clear();
            if (data.completedPlanetScenes != null)
            {
                foreach (string scene in data.completedPlanetScenes)
                {
                    if (!string.IsNullOrEmpty(scene))
                    {
                        completed.Add(scene);
                    }
                }
            }

            storyFlags.Clear();
            if (data.storyFlags != null)
            {
                foreach (string flag in data.storyFlags)
                {
                    if (!string.IsNullOrEmpty(flag))
                    {
                        storyFlags.Add(flag);
                    }
                }
            }

            abilities.Clear();
            if (data.abilityIds != null)
            {
                foreach (string id in data.abilityIds)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        abilities.Add(id);
                    }
                }
            }

            LastPlanetScene = data.lastPlanetScene ?? "";
            Galaxy1Progress.FirstPlanetDeparted = data.firstPlanetDeparted;
            ShipSelection.SelectedHullIndex = data.shipHullIndex;

            CampaignStats.LoadFrom(data.statsEnemiesDefeated, data.statsPerfectParries, data.statsPostureBreaks, data.statsBestCombo,
                data.statsBestKillStreak, data.statsBestNearMissStreak, data.statsBestParryStreak, data.statsFlawlessEncounters);

            DrillBestScores.ApplyFrom(data.drillScores);
        }

        /// <summary>
        /// Reset to a fresh campaign state. Does NOT clear <see cref="MetaProgression"/> — meta
        /// progress (Echoes, upgrades) is persistent and survives a new campaign by definition.
        /// </summary>
        public static void Reset()
        {
            completed.Clear();
            storyFlags.Clear();
            abilities.Clear();
            LastPlanetScene = "";
        }
    }
}
