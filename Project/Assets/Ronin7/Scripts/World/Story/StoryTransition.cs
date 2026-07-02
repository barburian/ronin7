using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Scene-transition glue for the story "boxes" (the prompt panels the player walks up to and
    /// presses). Buttons wire their onClick to one of these methods. Reuses the existing flow events
    /// so no new <see cref="Ronin7.Flow.GameFlowManager"/> wiring is needed:
    ///   <see cref="LandingRequested"/> -> load an on-foot scene (market -> hideout);
    ///   <see cref="ZoneCompleted"/>    -> return to space combat (hideout -> space).
    /// </summary>
    public class StoryTransition : MonoBehaviour
    {
        [Tooltip("On-foot scene to load from LoadOnFootScene(). Must be in the Build Profiles scene list.")]
        [SerializeField] private string onFootScene = "";

        public void LoadOnFootScene()
        {
            EventBus.Publish(new LandingRequested(onFootScene));
        }

        public void ReturnToSpace()
        {
            EventBus.Publish(new ZoneCompleted());
        }
    }
}
