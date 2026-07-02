using UnityEngine;

namespace Ronin7.World
{
    /// <summary>Designer-tunable description of a landable planet zone (a bounded search area).</summary>
    [CreateAssetMenu(menuName = "Space Samurai/Zone Definition", fileName = "ZoneDefinition")]
    public class ZoneDefinition : ScriptableObject
    {
        [Tooltip("Radius of the explorable area; the player is leashed inside it.")]
        public float radius = 8f;
        public int enemyCount = 3;
        public int relicCount = 3;

        [TextArea]
        public string objective = "Defeat the enemies and collect the relics, then return to extraction.";
    }
}
