using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Sets one or more persistent <see cref="CampaignState"/> story flags when <see cref="SetFlags"/>
    /// is invoked. Designed to be driven by a persistent UnityEvent listener wired at build time
    /// (e.g. a finale "return to hub" button), so the flags reliably set at runtime — unlike a
    /// build-time runtime listener, which is not serialized into the saved scene.
    /// </summary>
    public class CampaignFlagSetter : MonoBehaviour
    {
        [SerializeField] private string[] flags;

        /// <summary>Set every configured flag via CampaignState. Null/empty entries are ignored.</summary>
        public void SetFlags()
        {
            if (flags == null) return;
            foreach (var f in flags)
            {
                if (!string.IsNullOrEmpty(f)) CampaignState.SetFlag(f);
            }
        }
    }
}
