using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Selects and plays the appropriate briefing dialogue based on campaign milestone completion.
    /// Selection order: if planetScene7 is set and completed, plays postMission7; else if planetScene6
    /// is set and completed, plays postMission6; else if planetScene5 is set and completed, plays postMission5;
    /// else if planetScene4 is set and completed, plays postMission4; else if planetScene3 is set and completed,
    /// plays postMission3; else if planetScene2 is completed, plays postMission2; else if planetScene is completed,
    /// plays postMission; else plays preMission.
    /// </summary>
    public class BriefingSelector : MonoBehaviour
    {
        [SerializeField] private DialoguePlayer preMission;
        [SerializeField] private DialoguePlayer postMission;
        [SerializeField] private string planetScene;
        [SerializeField] private DialoguePlayer postMission2;
        [SerializeField] private string planetScene2;
        [SerializeField] private DialoguePlayer postMission3;
        [SerializeField] private string planetScene3;
        [SerializeField] private DialoguePlayer postMission4;
        [SerializeField] private string planetScene4;
        [SerializeField] private DialoguePlayer postMission5;
        [SerializeField] private string planetScene5;
        [SerializeField] private DialoguePlayer postMission6;
        [SerializeField] private string planetScene6;
        [SerializeField] private DialoguePlayer postMission7;
        [SerializeField] private string planetScene7;
        [SerializeField] private DialoguePlayer postMission8;
        [SerializeField] private string planetScene8;

        private void Start()
        {
            DialoguePlayer briefing = null;

            if (!string.IsNullOrEmpty(planetScene8) && CampaignState.IsCompleted(planetScene8))
            {
                briefing = postMission8;
            }
            else if (!string.IsNullOrEmpty(planetScene7) && CampaignState.IsCompleted(planetScene7))
            {
                briefing = postMission7;
            }
            else if (!string.IsNullOrEmpty(planetScene6) && CampaignState.IsCompleted(planetScene6))
            {
                briefing = postMission6;
            }
            else if (!string.IsNullOrEmpty(planetScene5) && CampaignState.IsCompleted(planetScene5))
            {
                briefing = postMission5;
            }
            else if (!string.IsNullOrEmpty(planetScene4) && CampaignState.IsCompleted(planetScene4))
            {
                briefing = postMission4;
            }
            else if (!string.IsNullOrEmpty(planetScene3) && CampaignState.IsCompleted(planetScene3))
            {
                briefing = postMission3;
            }
            else if (!string.IsNullOrEmpty(planetScene2) && CampaignState.IsCompleted(planetScene2))
            {
                briefing = postMission2;
            }
            else if (!string.IsNullOrEmpty(planetScene) && CampaignState.IsCompleted(planetScene))
            {
                briefing = postMission;
            }
            else if (!string.IsNullOrEmpty(planetScene))
            {
                briefing = preMission;
            }

            if (briefing != null)
            {
                briefing.Play();
            }
        }
    }
}
