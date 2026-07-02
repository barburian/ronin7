using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>Player's blade intercepted an incoming enemy attack within the parry window.</summary>
    public readonly struct SwordDeflected
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public SwordDeflected(Vector3 point, GameObject attacker) { Point = point; Attacker = attacker; }
    }

    /// <summary>An enemy attack landed on the player.</summary>
    public readonly struct PlayerHit
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public PlayerHit(float amount, Vector3 point) { Amount = amount; Point = point; }
    }

    /// <summary>The player's blade struck something damageable (for sparks / SFX / haptics).</summary>
    public readonly struct SwordImpact
    {
        public readonly Vector3 Point;
        public readonly float Speed;
        public readonly GameObject Victim;
        public SwordImpact(Vector3 point, float speed, GameObject victim) { Point = point; Speed = speed; Victim = victim; }
    }
}
