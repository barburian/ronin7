using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// Published when the player's ship takes projectile damage. UI/HUD/audio can listen without
    /// referencing the ship directly (same decoupling pattern as
    /// <see cref="Ronin7.Combat.EntityDamaged"/>).
    /// </summary>
    public readonly struct PlayerShipDamaged
    {
        public readonly float Amount;
        public readonly Vector3 WorldPoint;
        public readonly float Current;
        public readonly float Max;
        public PlayerShipDamaged(float amount, Vector3 worldPoint, float current, float max)
        {
            Amount = amount; WorldPoint = worldPoint; Current = current; Max = max;
        }
    }

    /// <summary>
    /// Published when the player's ship Health reaches zero — the dogfight's lose condition. Kept a
    /// decoupled event (like every other space-combat signal) so a HUD/flow/audio system can react
    /// to defeat without the ship referencing them directly. Nothing in Ship reacts to it; wiring a
    /// game-over/restart response is a flow/scene decision left to the human (see report).
    /// </summary>
    public readonly struct PlayerShipDestroyed
    {
        public readonly GameObject Ship;
        public readonly Vector3 WorldPoint;
        public PlayerShipDestroyed(GameObject ship, Vector3 worldPoint)
        {
            Ship = ship; WorldPoint = worldPoint;
        }
    }

    /// <summary>Published when an enemy ship is destroyed. Carries its universe-local position for FX/scoring.</summary>
    public readonly struct EnemyShipDestroyed
    {
        public readonly GameObject Ship;
        /// <summary>The kill location in universe-local space (the frame enemy ships live in).</summary>
        public readonly Vector3 UniversePosition;
        public EnemyShipDestroyed(GameObject ship, Vector3 universePosition)
        {
            Ship = ship; UniversePosition = universePosition;
        }
    }

    /// <summary>Published when an enemy ship is non-lethally disabled (drifts, not destroyed).</summary>
    public readonly struct EnemyShipDisabled
    {
        public readonly GameObject Ship;
        public readonly Vector3 UniversePosition;
        public EnemyShipDisabled(GameObject ship, Vector3 universePosition)
        {
            Ship = ship; UniversePosition = universePosition;
        }
    }

    /// <summary>Published each time the player's ship guns fire a bolt. For muzzle/fire SFX + VFX.</summary>
    public readonly struct ShipWeaponFired
    {
        public readonly Vector3 WorldPoint;
        public ShipWeaponFired(Vector3 worldPoint) => WorldPoint = worldPoint;
    }

    /// <summary>Published when any bolt (player or enemy) strikes a damageable. For impact SFX + VFX.</summary>
    public readonly struct ProjectileImpact
    {
        public readonly Vector3 WorldPoint;
        public ProjectileImpact(Vector3 worldPoint) => WorldPoint = worldPoint;
    }

    /// <summary>Published when a space encounter (wave) begins.</summary>
    public readonly struct SpaceEncounterStarted
    {
        public readonly int WaveIndex;
        public readonly int EnemyCount;
        public SpaceEncounterStarted(int waveIndex, int enemyCount)
        {
            WaveIndex = waveIndex; EnemyCount = enemyCount;
        }
    }

    /// <summary>Published when all enemies in the current encounter/wave have been destroyed.</summary>
    public readonly struct SpaceEncounterCleared
    {
        public readonly int WaveIndex;
        public SpaceEncounterCleared(int waveIndex) => WaveIndex = waveIndex;
    }

    /// <summary>Published when the player's ship jumps through a wormhole. Carries the universe-local
    /// landing position (the frame space objects live in) for FX/audio/HUD.</summary>
    public readonly struct WormholeTraversed
    {
        public readonly Vector3 UniversePosition;
        public WormholeTraversed(Vector3 universePosition) => UniversePosition = universePosition;
    }
}
