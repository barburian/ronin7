using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Marks an NPC with dialogue and metadata for story sequences.
    /// </summary>
    public class StoryNpc : MonoBehaviour
    {
        /// <summary>All enabled StoryNpcs, for cheap nearest-talkable lookups without a scene-wide
        /// FindObjectsByType every frame. Mirrors the Ronin7.Ship EnemyShip.Active/Asteroid.Active
        /// idiom.</summary>
        public static readonly List<StoryNpc> Active = new();

        [SerializeField] private string displayName;
        [SerializeField] private string role;
        [SerializeField] private DialoguePlayer dialogue;
        [SerializeField] private bool remote;
        [Tooltip("Floating arrow shown above this NPC until the player has talked to them.")]
        [SerializeField] private GameObject talkArrow;

        public string DisplayName => displayName;
        public DialoguePlayer Dialogue => dialogue;
        public bool Remote => remote;
        public bool Talked { get; private set; }

        /// <summary>Overrides the dialogue this NPC plays (used by <see cref="NpcGossipSelector"/> to
        /// pick a campaign-flag-gated variant at Awake, before <see cref="TalkInteractor"/> ever reads
        /// <see cref="Dialogue"/>). Ignores null so a misconfigured selector can't blank an NPC out.</summary>
        public void SetDialogue(DialoguePlayer d)
        {
            if (d != null) dialogue = d;
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        // Editor sessions with "Enter Play Mode (no domain reload)" keep static state across Play
        // cycles, which would leak stale entries from a previous run into the next (mirrors EventBus's
        // reset).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() => Active.Clear();

        /// <summary>Mark this NPC as talked-to: hides its arrow so it no longer reads as a target.</summary>
        public void MarkTalked()
        {
            Talked = true;
            if (talkArrow != null) talkArrow.SetActive(false);
        }
    }
}
