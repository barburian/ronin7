using UnityEngine;
using Ronin7.Combat;

namespace Ronin7.World.Story
{
    /// <summary>
    /// Defines the kind of objective in a mission step.
    /// </summary>
    public enum MissionStepKind
    {
        Dialogue,
        SpawnAndDefeat,
        DefeatEnemies,
        ReachTrigger,
        Prompt,
        ReachExtraction,
        Trigger,
        TalkToNpcs,
        Hack,
        DefeatWaves
    }

    /// <summary>
    /// A single mission objective with type-specific parameters.
    /// </summary>
    [System.Serializable]
    public class MissionStep
    {
        public MissionStepKind kind;
        public string label;

        // Dialogue:
        public DialoguePlayer dialogue;

        // DefeatEnemies (pre-placed) AND SpawnAndDefeat:
        public Health[] enemies;
        public GameObject enemyPrefab;
        public Transform[] spawnPoints;

        // ReachTrigger:
        public Transform reachPoint;
        public float reachRadius = 2.5f;

        // Prompt:
        public GameObject promptObject;

        // ReachExtraction:
        public ExtractionZone extraction;

        // Trigger: GameObjects to activate, then advance immediately.
        public GameObject[] triggerObjects;

        // TalkToNpcs: advance once the player has talked to every NPC in this list.
        public StoryNpc[] npcs;

        // Hack: advance once this terminal has been hacked.
        public HackTerminal hackTerminal;

        // DefeatWaves: advance once all waves have been defeated.
        public EnemyWaveSpawner waveSpawner;
    }
}
