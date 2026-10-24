using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>Published when a node's fight/reward is resolved. Lets Flow/World coordinate the
    /// post-clear flow (boon offer, advance, arena teardown) without a new assembly edge.</summary>
    public readonly struct RoomCleared
    {
        public readonly int NodeIndex;
        public RoomCleared(int nodeIndex) { NodeIndex = nodeIndex; }
    }

    /// <summary>Published by <see cref="RunState.Begin"/>'s caller when a run starts.</summary>
    public readonly struct RunStarted
    {
        public readonly uint Seed;
        public RunStarted(uint seed) { Seed = seed; }
    }

    /// <summary>Published when a run ends, whether by boss kill (won) or permadeath.</summary>
    public readonly struct RunEnded
    {
        public readonly int DepthReached;
        public readonly bool Won;
        public RunEnded(int depthReached, bool won) { DepthReached = depthReached; Won = won; }
    }

    /// <summary>P6-06: the onboarding prompts were shown because Replay Tutorial was on; the settings service clears the toggle (one-shot).</summary>
    public readonly struct TutorialReplayShown { }

    /// <summary>P2-08: a <see cref="Grabbable"/> was picked up (the sword draw, for the tutorial prompt).</summary>
    public readonly struct ObjectGrabbed
    {
        public readonly Transform Hand;
        public ObjectGrabbed(Transform hand) { Hand = hand; }
    }

    /// <summary>P6-01: the boon offer panel was shown (audio chime; no payload).</summary>
    public readonly struct BoonOffered { }

    /// <summary>Published when the player picks a boon from an offer.</summary>
    public readonly struct BoonChosen
    {
        public readonly string BoonId;
        public BoonChosen(string boonId) { BoonId = boonId; }
    }

    /// <summary>Published once a run node's arena has been built and is ready to play (see
    /// <see cref="Ronin7.World.RunArenaController.Start"/>). Lets listeners react to the sector/kind of
    /// the node the player just entered — e.g. per-sector ambience — without a hard reference into
    /// Ronin7.World.</summary>
    public readonly struct RunNodeEntered
    {
        public readonly int Sector;
        public readonly RoomKind Kind;
        public RunNodeEntered(int sector, RoomKind kind) { Sector = sector; Kind = kind; }
    }

    /// <summary>P1-13: published by the arena on room activate and after each enemy death — enemies
    /// still to beat (alive + queued). Feeds the HUD's FOES line.</summary>
    public readonly struct RoomProgress
    {
        public readonly int Remaining;
        public RoomProgress(int remaining) { Remaining = remaining; }
    }

    /// <summary>P2-06: an enemy is about to materialise at <see cref="Point"/> (world space) after the
    /// spawn telegraph delay. Lets audio play the cue without a hard reference into Ronin7.World.</summary>
    public readonly struct EnemySpawning
    {
        public readonly Vector3 Point;
        public EnemySpawning(Vector3 point) { Point = point; }
    }

    /// <summary>P2-04: the cleared node's reward is done; the player now picks the next node from
    /// <see cref="Choices"/> (node ids). The arena raises exit gates for them; RunDirector may also show
    /// the CHOOSE YOUR PATH panel. Published once per node.</summary>
    public readonly struct PathChoiceOffered
    {
        public readonly int[] Choices;
        public PathChoiceOffered(int[] choices) { Choices = choices; }
    }

    /// <summary>P2-04: the player picked node <see cref="NodeId"/> (exit gate walked into, or panel button).</summary>
    public readonly struct PathChosen
    {
        public readonly int NodeId;
        public PathChosen(int nodeId) { NodeId = nodeId; }
    }

    /// <summary>P2-02: the player took the Treasure boon at <see cref="Point"/> (world space);
    /// <see cref="Color"/> is the boon's rarity colour. Lets the pooled VFX burst without a Flow reference.</summary>
    public readonly struct TreasureOpened
    {
        public readonly Vector3 Point;
        public readonly Color Color;
        public TreasureOpened(Vector3 point, Color color) { Point = point; Color = color; }
    }
}
