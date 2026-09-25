using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// Ordered campaign mission list — replaces galaxy-map planet selection. The ship hub asks this
    /// asset for the next mission the player hasn't completed yet, then launches it.
    ///
    /// A mission is one chapter: its <see cref="Mission.entryScene"/> is the on-foot scene the hub
    /// loads; the chapter then plays through its own internal LandingRequested hops and finishes when
    /// its terminal scene publishes ZoneCompleted, returning the player to the hub. Completion is keyed
    /// on the entry scene (held in CampaignState.LastPlanetScene from the hub launch through to that
    /// ZoneCompleted), so this list is the single source of truth for mission order.
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin 7/Campaign Director", fileName = "CampaignDirector")]
    public class CampaignDirector : ScriptableObject
    {
        [Serializable]
        public struct Mission
        {
            [Tooltip("Stable mission id, e.g. \"Ch02\". For reference/debugging only.")]
            public string id;

            [Tooltip("On-foot scene the hub loads to start this mission. Must be in the Build Profiles scene list.")]
            public string entryScene;
        }

        [Tooltip("Missions in play order (one per chapter). Empty entryScene = unfilled scaffold slot.")]
        [SerializeField] private Mission[] missions = Array.Empty<Mission>();

        public IReadOnlyList<Mission> Missions => missions;

        /// <summary>
        /// First mission whose entry scene is not yet completed, or null if every (filled) mission is
        /// done. The completion test is injected rather than calling CampaignState directly so the
        /// ordering logic stays pure and unit-testable. Missions with an empty entryScene are skipped
        /// (scaffold slots reserved for later galaxies).
        /// </summary>
        public Mission? NextIncomplete(Func<string, bool> isCompleted)
        {
            if (missions == null) return null;
            foreach (var m in missions)
            {
                if (string.IsNullOrEmpty(m.entryScene)) continue;
                if (!isCompleted(m.entryScene)) return m;
            }
            return null;
        }

        /// <summary>Build an in-memory director (no asset) for unit tests.</summary>
        internal static CampaignDirector CreateForTests(params Mission[] missions)
        {
            var director = CreateInstance<CampaignDirector>();
            director.missions = missions;
            return director;
        }
    }
}
