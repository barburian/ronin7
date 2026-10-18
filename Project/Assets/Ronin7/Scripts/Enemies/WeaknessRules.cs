using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    // WeaknessKind lives in EnemyDefinition.cs (serialized, APPEND-ONLY: never reorder or remove values).

    /// <summary>Outcome of a weakness check for one hit.</summary>
    public enum WeaknessResult { Neutral, Resisted, Exploited }

    /// <summary>Snapshot of a hit and the victim's state. Built by Enemy.FilterDamage (P3-02).</summary>
    public readonly struct WeaknessContext
    {
        public readonly Vector3 EnemyPos, EnemyForward, SourcePos, HitPoint;
        public readonly float Speed;
        public readonly DamageType Type;
        public readonly bool SourceIsPlayer, SourceIsAlly, SightActive;
        public readonly bool Staggered, Attacking, Recovering, RecentlyBlinked;
        /// <summary>+1 / -1 (see <see cref="WeaknessRules.Side"/>); LastHitSide 0 = no previous hit.</summary>
        public readonly int HitSide, LastHitSide;
        public readonly float SinceLastHit;

        public WeaknessContext(Vector3 enemyPos, Vector3 enemyForward, Vector3 sourcePos, Vector3 hitPoint,
            float speed, DamageType type, bool sourceIsPlayer, bool sourceIsAlly, bool sightActive,
            bool staggered, bool attacking, bool recovering, bool recentlyBlinked,
            int hitSide, int lastHitSide, float sinceLastHit)
        {
            EnemyPos = enemyPos; EnemyForward = enemyForward; SourcePos = sourcePos; HitPoint = hitPoint;
            Speed = speed; Type = type;
            SourceIsPlayer = sourceIsPlayer; SourceIsAlly = sourceIsAlly; SightActive = sightActive;
            Staggered = staggered; Attacking = attacking; Recovering = recovering; RecentlyBlinked = recentlyBlinked;
            HitSide = hitSide; LastHitSide = lastHitSide; SinceLastHit = sinceLastHit;
        }
    }

    /// <summary>Pure weakness rule table (P3-01). No Unity state; Enemy wiring is P3-02.</summary>
    public static class WeaknessRules
    {
        public const float AlternatingWindow = 4f;
        /// <summary>Wiring sets <see cref="WeaknessContext.RecentlyBlinked"/> using this window.</summary>
        public const float BlinkWindow = 1f;
        /// <summary>PostureOnly: HP is always Resisted (x0.2) but posture gain is x5 - its ability-free path.</summary>
        public const float PostureOnlyHpMultiplier = 0.2f;
        public const float PostureOnlyPostureMultiplier = 5f;

        public static WeaknessResult Evaluate(WeaknessKind k, in WeaknessContext c, float heavySwingSpeed)
        {
            switch (k)
            {
                case WeaknessKind.Backstab:
                    return c.Staggered || IsBehind(c.EnemyPos, c.EnemyForward, c.SourcePos)
                        ? WeaknessResult.Exploited : WeaknessResult.Resisted;

                case WeaknessKind.HeavyBreak:
                    if (IsBehind(c.EnemyPos, c.EnemyForward, c.SourcePos)) return WeaknessResult.Neutral;
                    return c.Speed >= heavySwingSpeed ? WeaknessResult.Exploited : WeaknessResult.Resisted;

                case WeaknessKind.Recovery:
                    if (c.Staggered || c.Recovering) return WeaknessResult.Exploited;
                    return c.Attacking ? WeaknessResult.Resisted : WeaknessResult.Neutral;

                case WeaknessKind.Alternating:
                    if (c.LastHitSide == 0 || c.SinceLastHit >= AlternatingWindow) return WeaknessResult.Neutral;
                    return c.HitSide == c.LastHitSide ? WeaknessResult.Resisted : WeaknessResult.Exploited;

                case WeaknessKind.Reflect:
                    return c.Type == DamageType.Projectile && c.SourceIsPlayer
                        ? WeaknessResult.Exploited : WeaknessResult.Neutral;

                case WeaknessKind.ParryGate:
                    return c.Staggered ? WeaknessResult.Exploited : WeaknessResult.Resisted;

                case WeaknessKind.CounterHit:
                    return c.Attacking || c.Staggered || c.SightActive || c.SourceIsAlly
                        ? WeaknessResult.Exploited : WeaknessResult.Resisted;

                case WeaknessKind.PostureOnly:
                    return WeaknessResult.Resisted;

                case WeaknessKind.Blink:
                    return c.RecentlyBlinked ? WeaknessResult.Exploited : WeaknessResult.Neutral;

                default: // None, Linchpin (its death effect is wired elsewhere)
                    return WeaknessResult.Neutral;
            }
        }

        /// <summary>BUG-39: true while the enemy's state-based weakness is open right now, i.e. the body rim
        /// shows gold ("hit now"). Only the kinds whose window is a state: the positional / swing-speed / side
        /// / projectile kinds (Backstab plate, HeavyBreak slab, Alternating shoulders, Reflect bolts) carry
        /// their own marker instead, so they are false here except Backstab's stagger window.</summary>
        public static bool IsOpen(WeaknessKind k, bool staggered, bool attacking, bool recovering, bool recentlyBlinked)
        {
            switch (k)
            {
                case WeaknessKind.Recovery: return staggered || recovering;
                case WeaknessKind.Backstab:
                case WeaknessKind.ParryGate: return staggered;
                case WeaknessKind.CounterHit: return attacking || staggered;
                case WeaknessKind.Blink: return recentlyBlinked;
                default: return false; // None, HeavyBreak, Alternating, Reflect, Linchpin, PostureOnly
            }
        }

        /// <summary>BUG-39: kinds whose rim rests blue-grey ("guarded") whenever <see cref="IsOpen"/> is false.</summary>
        public static bool RestsGuarded(WeaknessKind k) =>
            k == WeaknessKind.ParryGate || k == WeaknessKind.CounterHit || k == WeaknessKind.PostureOnly;

        public static float Multiplier(WeaknessResult r, float resist, float exploit) =>
            r == WeaknessResult.Resisted ? resist : r == WeaknessResult.Exploited ? exploit : 1f;

        /// <summary>True when src is at or beyond minAngleDeg off the (flattened) forward.</summary>
        internal static bool IsBehind(Vector3 pos, Vector3 fwd, Vector3 src, float minAngleDeg = 110f)
        {
            fwd.y = 0f;
            Vector3 to = src - pos;
            to.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return false;
            return Vector3.Angle(fwd, to) >= minAngleDeg;
        }

        /// <summary>+1 if hit is on the enemy's right, -1 on its left. Same math as EchoHunter
        /// (Mathf.Sign of dot(right, hit - pos)); a zero offset counts as +1.</summary>
        internal static int Side(Vector3 pos, Vector3 fwd, Vector3 hit) =>
            (int)Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, fwd), hit - pos));
    }
}
