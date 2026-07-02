using UnityEngine;

namespace Ronin7.World.Story
{
    /// <summary>
    /// A single line of dialogue with speaker, text, duration, and optional audio.
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea]
        public string text;
        public float seconds = 3f;
        public AudioClip clip;
    }
}
