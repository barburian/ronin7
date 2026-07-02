using System;
using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// <c>Galaxy1_Ch1_Hub</c> is both the Ch1 mission and the persistent post-Ch1 hub: this switches
    /// the scene between the two modes on load, and (in hub mode) reveals each later chapter's room
    /// once its completion flag is set. No events, no polling — the mode is fixed for the scene's
    /// lifetime once <see cref="Awake"/> runs.
    /// </summary>
    public class HubStateController : MonoBehaviour
    {
        [Serializable]
        public class FlagGate
        {
            public string flag;
            public GameObject[] roots;
        }

        [Tooltip("The Ch1 MissionDirector hierarchy — active only before ch1_complete is set.")]
        [SerializeField] private GameObject missionModeRoot;
        [Tooltip("The hub console/briefing hierarchy — active only once ch1_complete is set.")]
        [SerializeField] private GameObject hubModeRoot;
        [Tooltip("Per-chapter room shells, each revealed once its flag is set (hub mode only).")]
        [SerializeField] private FlagGate[] roomGates;
        [Tooltip("Locked-door controllers the Ch1 mission would have unlocked via Trigger steps — " +
                 "forced active in hub mode so the hub stays traversable without the mission running.")]
        [SerializeField] private GameObject[] hubModeUnlocked;

        private void Awake()
        {
            bool hubMode = IsHubMode(CampaignState.HasFlag);
            if (missionModeRoot != null) missionModeRoot.SetActive(!hubMode);
            if (hubModeRoot != null) hubModeRoot.SetActive(hubMode);

            if (hubMode && hubModeUnlocked != null)
            {
                foreach (var go in hubModeUnlocked)
                {
                    if (go != null) go.SetActive(true);
                }
            }

            if (roomGates == null) return;
            foreach (var gate in roomGates)
            {
                bool enable = hubMode && ShouldEnableGate(gate?.flag, CampaignState.HasFlag);
                if (gate?.roots == null) continue;
                foreach (var root in gate.roots)
                {
                    if (root != null) root.SetActive(enable);
                }
            }
        }

        /// <summary>Whether the scene should present as the hub rather than the Ch1 mission.</summary>
        public static bool IsHubMode(Func<string, bool> hasFlag) => hasFlag("ch1_complete");

        /// <summary>Whether a gate's rooms should be active. A null/empty flag never gates open.</summary>
        public static bool ShouldEnableGate(string flag, Func<string, bool> hasFlag)
            => !string.IsNullOrEmpty(flag) && hasFlag(flag);
    }
}
