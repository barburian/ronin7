using Ronin7.Core;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Activates objects (and optionally plays a dialogue) once the scene's space encounter is
    /// cleared (<see cref="SpaceEncounterCleared"/>). Lets a story scene gate its ending on a
    /// space fight without a MissionDirector step — e.g. EP07 Escape, where the return prompt
    /// and closing dialogue only appear after the Dominion interceptor is destroyed. If
    /// <see cref="clearedFlag"/> is already set (the fight was won on an earlier visit, so the
    /// gated GuardEncounter never respawns), fires immediately on start instead.
    /// </summary>
    public class EncounterClearedActivator : MonoBehaviour
    {
        [Tooltip("Objects switched on when the encounter clears (e.g. the transition prompt box).")]
        [SerializeField] private GameObject[] activateOnCleared;
        [Tooltip("Optional dialogue played once when the encounter clears.")]
        [SerializeField] private DialoguePlayer playOnCleared;
        [Tooltip("Mirror of the encounter's clearedFlag: if already set, fire on start.")]
        [SerializeField] private string clearedFlag = "";
        [Tooltip("Optional extra story flag to set when the encounter clears (e.g. galaxy1_complete for finale encounters).")]
        [SerializeField] private string extraFlag = "";

        private bool fired;

        private void OnEnable() => EventBus.Subscribe<SpaceEncounterCleared>(OnCleared);
        private void OnDisable() => EventBus.Unsubscribe<SpaceEncounterCleared>(OnCleared);

        private void Start()
        {
            if (!string.IsNullOrEmpty(clearedFlag) && CampaignState.HasFlag(clearedFlag))
                Fire();
        }

        private void OnCleared(SpaceEncounterCleared _) => Fire();

        private void Fire()
        {
            if (fired) return;
            fired = true;
            if (activateOnCleared != null)
            {
                foreach (GameObject go in activateOnCleared)
                {
                    if (go != null) go.SetActive(true);
                }
            }
            if (playOnCleared != null) playOnCleared.Play();
            if (!string.IsNullOrEmpty(extraFlag)) CampaignState.SetFlag(extraFlag);
        }
    }
}
