using UnityEngine;
using Ronin7.Core;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Picks one of several dialogue variants for a <see cref="StoryNpc"/> at Awake, gated by campaign
    /// story flags (see <see cref="GossipSelector"/>) — e.g. an NPC who reacts differently once
    /// "galaxy1_complete" is set. <see cref="variants"/>/<see cref="requiredFlags"/> are parallel
    /// arrays; a slot with an empty/null required flag is the universal fallback.
    /// </summary>
    [RequireComponent(typeof(StoryNpc))]
    public class NpcGossipSelector : MonoBehaviour
    {
        [SerializeField] private DialoguePlayer[] variants;
        [SerializeField] private string[] requiredFlags;

        private void Awake()
        {
            if (variants == null || requiredFlags == null) return;

            int idx = GossipSelector.SelectIndex(requiredFlags, CampaignState.HasFlag);
            if (idx < 0 || idx >= variants.Length) return;

            DialoguePlayer chosen = variants[idx];
            if (chosen == null) return;

            StoryNpc npc = GetComponent<StoryNpc>();
            if (npc != null) npc.SetDialogue(chosen);
        }
    }
}
