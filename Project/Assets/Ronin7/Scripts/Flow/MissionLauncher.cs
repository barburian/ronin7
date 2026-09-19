using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>
    /// Hub-side launcher for the next campaign mission. Lives on Kessler's ship (the hub) and is
    /// invoked by an interactable — a console button or a Kessler talk prompt. Asks the
    /// <see cref="CampaignDirector"/> for the next mission the player hasn't completed and publishes
    /// <see cref="LandingRequested"/> so <see cref="GameFlowManager"/> loads it on-foot. Which Kessler
    /// briefing plays in the hub is handled separately by the existing BriefingSelector.
    /// </summary>
    public class MissionLauncher : MonoBehaviour
    {
        [Tooltip("Ordered campaign mission list (one entry per chapter).")]
        [SerializeField] private CampaignDirector director;

        /// <summary>Launch the next incomplete mission. No-op (logs) when the campaign is complete.</summary>
        public void LaunchNext()
        {
            if (director == null)
            {
                Debug.LogError("[MissionLauncher] No CampaignDirector assigned.");
                return;
            }

            var next = director.NextIncomplete(CampaignState.IsCompleted);
            if (next == null)
            {
                Debug.Log("[MissionLauncher] Campaign complete — no further missions to launch.");
                return;
            }

            EventBus.Publish(new LandingRequested(next.Value.entryScene));
        }
    }
}
