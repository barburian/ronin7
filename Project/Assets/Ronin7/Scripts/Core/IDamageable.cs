using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>Describes a single instance of damage being dealt.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;
        public readonly DamageType Type;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageType type = DamageType.Melee)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Type = type;
        }
    }

    public enum DamageType
    {
        Melee,
        Projectile,
        Collision
    }

    /// <summary>Anything that can receive damage — players, enemies, ships, props.</summary>
    public interface IDamageable
    {
        void ApplyDamage(in DamageInfo info);
        bool IsAlive { get; }
    }
}
