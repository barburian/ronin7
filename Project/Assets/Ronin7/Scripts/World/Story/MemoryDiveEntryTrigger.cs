using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Fires <see cref="MemoryDiveController.EnterDive"/> the moment this GameObject is enabled — the
    /// same "inactive until a MissionStepKind.Trigger step activates it" idiom <c>NpcWalker</c> and
    /// <c>AbilityGranter</c> use, so a Trigger step can start a memory dive without MissionDirector
    /// itself knowing about MemoryDiveController.
    /// </summary>
    public class MemoryDiveEntryTrigger : MonoBehaviour
    {
        [SerializeField] private MemoryDiveController dive;

        private void OnEnable()
        {
            if (dive != null) dive.EnterDive();
        }
    }
}
