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
}
