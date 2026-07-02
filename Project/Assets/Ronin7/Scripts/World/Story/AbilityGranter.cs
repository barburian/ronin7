using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Unlocks one persistent <see cref="CampaignState"/> ability when <see cref="Grant"/> runs.
    /// <see cref="Grant"/> is a public parameterless method (same contract as
    /// <see cref="CampaignFlagSetter.SetFlags"/>) so it can be wired as a persistent UnityEvent
    /// listener; it also fires from <see cref="OnEnable"/> so a MissionDirector Trigger step (which
    /// just SetActives its target objects) grants the ability on its own, mirroring how
    /// <c>ProximityDoor</c>/<c>ChapterOutro</c> come alive purely from being enabled.
    /// </summary>
    public class AbilityGranter : MonoBehaviour
    {
        [SerializeField] private string abilityId;

        /// <summary>Unlock the configured ability. Null/empty ids are ignored (CampaignState.UnlockAbility no-ops).</summary>
        public void Grant()
        {
            CampaignState.UnlockAbility(abilityId);
        }

        private void OnEnable() => Grant();
    }
}
