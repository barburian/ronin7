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
        /// <summary>Blade speed (m/s) behind this hit; 0 when not a blade hit.</summary>
        public readonly float Speed;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageType type = DamageType.Melee, float speed = 0f)
        {
            Speed = speed;
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
            Type = type;
        }

        public DamageInfo WithAmount(float a) => new DamageInfo(a, Point, Direction, Source, Type, Speed);
    }

    public enum DamageType
    {
        Melee,
        Projectile,
        Collision,
        /// <summary>P4-05 boon procs (bleed ticks, storm chains): never flinches, never builds posture,
        /// never re-procs. APPEND-ONLY.</summary>
        Status
    }

    /// <summary>Anything that can receive damage — players, enemies, ships, props.</summary>
    public interface IDamageable
    {
        void ApplyDamage(in DamageInfo info);
        bool IsAlive { get; }
    }
}
