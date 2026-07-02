using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Marks an NPC with dialogue and metadata for story sequences.
    /// </summary>
    public class StoryNpc : MonoBehaviour
    {
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

        /// <summary>Mark this NPC as talked-to: hides its arrow so it no longer reads as a target.</summary>
        public void MarkTalked()
        {
            Talked = true;
            if (talkArrow != null) talkArrow.SetActive(false);
        }
    }
}
