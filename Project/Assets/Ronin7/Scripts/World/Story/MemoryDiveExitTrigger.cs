using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Fires <see cref="MemoryDiveController.ExitDive"/> the moment this GameObject is enabled.
    /// MissionStepKind.Trigger only ever SetActive(true)s its target objects, so exiting a dive (which
    /// deactivates the dive root) needs its own inactive-until-triggered object, mirroring
    /// <see cref="MemoryDiveEntryTrigger"/>.
    /// </summary>
    public class MemoryDiveExitTrigger : MonoBehaviour
    {
        [SerializeField] private MemoryDiveController dive;

        private void OnEnable()
        {
            if (dive != null) dive.ExitDive();
        }
    }
}
