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

    /// <summary>
    /// Sunder Beat: the player's deflect landed inside the perfect-timing window (see
    /// <see cref="ParryTiming.ParryQuality"/>). <see cref="Quality"/> is in (0, 1] — 1 at a frame-zero
    /// deflect, tapering toward 0 at the edge of the window. Only published when quality is greater
    /// than zero; a deflect outside the window still publishes <see cref="SwordDeflected"/> but not this.
    /// </summary>
    public readonly struct PerfectParry
    {
        public readonly Vector3 Point;
        public readonly GameObject Attacker;
        public readonly float Quality;
        public PerfectParry(Vector3 point, GameObject attacker, float quality) { Point = point; Attacker = attacker; Quality = quality; }
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

    /// <summary>An entity's posture meter (see <c>Ronin7.Enemies.PostureMeter</c>) broke: bonus damage
    /// was applied and a forced stagger was triggered.</summary>
    public readonly struct PostureBroken
    {
        public readonly GameObject Entity;
        public PostureBroken(GameObject entity) => Entity = entity;
    }
}
