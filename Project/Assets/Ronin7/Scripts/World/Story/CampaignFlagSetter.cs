using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Sets one or more persistent <see cref="CampaignState"/> story flags when <see cref="SetFlags"/>
    /// is invoked. Designed to be driven by a persistent UnityEvent listener wired at build time
    /// (e.g. a finale "return to hub" button), so the flags reliably set at runtime — unlike a
    /// build-time runtime listener, which is not serialized into the saved scene.
    ///
    /// When <see cref="setOnEnable"/> is true, also fires from OnEnable (mirrors <c>AbilityGranter</c>)
    /// so a MissionDirector Trigger step, which just SetActives its target objects, can set a flag
    /// mid-chapter on its own — used for Ch3's "ch3_echo_named" naming beat. It defaults to false
    /// because several finale scenes carry this component on a return box that is ACTIVE from scene
    /// start; those must keep firing only from the wired button press.
    /// </summary>
    public class CampaignFlagSetter : MonoBehaviour
    {
        [SerializeField] private string[] flags;
        [Tooltip("Fire SetFlags when this GameObject is enabled (for Trigger-step activation). Leave " +
                 "false for objects that are active at scene start or wired to an explicit event.")]
        [SerializeField] private bool setOnEnable;

        /// <summary>Set every configured flag via CampaignState. Null/empty entries are ignored.</summary>
        public void SetFlags()
        {
            if (flags == null) return;
            foreach (var f in flags)
            {
                if (!string.IsNullOrEmpty(f)) CampaignState.SetFlag(f);
            }
        }

        private void OnEnable()
        {
            if (setOnEnable) SetFlags();
        }
    }
}
