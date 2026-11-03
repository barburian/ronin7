using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>P8-02: strike direction. Serialized: append-only. P8-03 adds the horizontal slashes (Left = blade
    /// sweeps toward the body's left, Right = toward its right), Thrust (straight forward) and Low (knee-height sweep).</summary>
    public enum StrikeDirection { Overhead, Aim, Left, Right, Thrust, Low }

    /// <summary>P8-02: one strike of a pattern. Timings are multipliers of the body's own
    /// <see cref="EnemyDefinition"/> telegraph/active/recover times (1 = today's chop), so sector and Heat
    /// tuning on the body still apply.</summary>
    [System.Serializable]
    public struct Strike
    {
        public StrikeDirection direction;
        public float windup, active, recovery;
        /// <summary>P8-02c: ranged strikes fire a pooled bolt (m/s, m/s^2 down, hit radius m). Old assets read 0 = melee.</summary>
        public float boltSpeed, boltGravity, boltRadius;
        public bool Ranged => boltSpeed > 0f;
        /// <summary>P8-03: a DODGE strike - a blade parry does not interrupt it. Negative so old assets stay parryable.</summary>
        public bool unparryable;
        public bool Parryable => !Ranged && !unparryable;
        public static Strike Chop => new Strike { direction = StrikeDirection.Overhead, windup = 1f, active = 1f, recovery = 1f };
    }

    [System.Serializable]
    public class AttackPattern
    {
        public string name = "Chop";
        [Tooltip("P8-03 weighted picker: relative chance of this pattern.")]
        public int weight = 1;
        public List<Strike> strikes = new List<Strike> { Strike.Chop };
    }

    /// <summary>
    /// P8-02 (D-17): a weapon is its own prefab + this definition, composed onto a body's sockets at spawn
    /// (<see cref="Enemy.Equip"/>), never baked into the enemy prefab. The weapon owns its moveset (P8-03).
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin7/Enemy Weapon Definition")]
    public class EnemyWeaponDefinition : ScriptableObject
    {
        public const string MainSocket = "Socket_MainHand", OffSocket = "Socket_OffHand", TipName = "BladeTip", EdgeBaseName = "BladeEdgeBase";
        /// <summary>P8-02c: outer-left-forearm strap for <see cref="forearm"/> off-hands, scaled to a 0.25 m forearm.</summary>
        public const string ShieldSocket = "Socket_Shield";

        [Tooltip("Visual prefab, authored in the socket frame (+Z forward, +Y up at the arm's rest). Null = natural (claws): meshless.")]
        public GameObject prefab;
        [Tooltip("Blade tip in the weapon's local space: the parry-capsule end and swing-speed sample point.")]
        public Vector3 tip = new Vector3(0f, 0f, 0.5f);
        [Tooltip("Base of the cutting edge (local). P8-04 telegraph reads tip..edgeBase.")]
        public Vector3 edgeBase = new Vector3(0f, 0f, 0.1f);
        [Tooltip("Multiplies the body's attackRange.")]
        public float reach = 1f;
        [Tooltip("Off-hand only: straps to Socket_Shield on the forearm instead of the hand (shields).")]
        public bool forearm;
        [Tooltip("Multiplies the body's damage.")]
        public float damage = 1f;
        [Tooltip("Staff: casts the P3-05 bolts. Data only until P8-03 wires casting (RangedEnemy lives in Ship).")]
        public bool ranged;
        public List<AttackPattern> patterns = new List<AttackPattern> { new AttackPattern() };

        /// <summary>Applies this weapon's stat mods to the body's (runtime, already-scaled) definition.</summary>
        public void ApplyTo(EnemyDefinition body)
        {
            body.attackRange *= reach;
            body.damage *= damage;
        }

        /// <summary>Instantiates the weapon under <paramref name="socket"/> (an empty root when meshless) with
        /// a <see cref="TipName"/> child at <see cref="tip"/> and an <see cref="EdgeBaseName"/> child at <see cref="edgeBase"/>
        /// (P8-03: the parry capsule runs edgeBase..tip). Returns the weapon root.</summary>
        public Transform Attach(Transform socket)
        {
            Transform root = prefab != null ? Instantiate(prefab, socket, false).transform : new GameObject(name).transform;
            root.SetParent(socket, false);
            var t = new GameObject(TipName).transform;
            t.SetParent(root, false);
            t.localPosition = tip;
            var b = new GameObject(EdgeBaseName).transform;
            b.SetParent(root, false);
            b.localPosition = edgeBase;
            return root;
        }
    }

    /// <summary>
    /// P8-03: one enemy's cursor over its (merged main + off-hand) patterns. <see cref="Peek"/> is held until
    /// <see cref="Consume"/>, so chase/kite and the attack agree; a multi-strike pattern hands out its strikes in
    /// order across consecutive attacks, and a new pattern is picked (weighted, never the same twice in a row when
    /// 2+ exist) only once the current one is exhausted.
    /// </summary>
    public sealed class StrikeSequencer
    {
        private readonly System.Random rng;
        private int pattern = -1, next;

        public StrikeSequencer(List<AttackPattern> patterns, System.Random rng) { Patterns = patterns; this.rng = rng; }

        public List<AttackPattern> Patterns { get; }
        /// <summary>The pattern <see cref="Peek"/> draws from (picks one if the last is exhausted).</summary>
        public AttackPattern Current { get { Refill(); return pattern < 0 ? null : Patterns[pattern]; } }
        /// <summary>Index of <see cref="Peek"/> within its pattern (0 = opener). P8-04 combo-count hook.</summary>
        public int StrikeIndex { get { Refill(); return next; } }
        /// <summary>True between strikes of a multi-strike pattern (the next attack chains).</summary>
        public bool ComboPending => pattern >= 0 && next > 0 && next < Count(pattern);

        public Strike Peek { get { Refill(); return pattern < 0 || Count(pattern) == 0 ? Strike.Chop : Patterns[pattern].strikes[next]; } }
        public Strike Consume() { Strike s = Peek; next++; return s; }
        /// <summary>Drop the rest of the combo (parried/flinched); the no-repeat rule still sees this pattern.</summary>
        public void BreakCombo() { if (pattern >= 0) next = Count(pattern); }

        private int Count(int i) => Patterns[i].strikes != null ? Patterns[i].strikes.Count : 0;
        private void Refill() { if (pattern < 0 || next >= Count(pattern)) { pattern = Pick(Patterns, pattern, rng); next = 0; } }

        /// <summary>Pure: weighted pick by <see cref="AttackPattern.weight"/>, excluding <paramref name="last"/> when 2+
        /// patterns exist. -1 when there are none.</summary>
        public static int Pick(IList<AttackPattern> patterns, int last, System.Random rng)
        {
            int total = 0;
            for (int i = 0; i < patterns.Count; i++) if (i != last || patterns.Count == 1) total += System.Math.Max(0, patterns[i].weight);
            if (total == 0) return patterns.Count == 0 ? -1 : (last + 1) % patterns.Count; // all weights 0: rotate
            int roll = rng.Next(total);
            for (int i = 0; i < patterns.Count; i++)
            {
                if (i == last && patterns.Count > 1) continue;
                roll -= System.Math.Max(0, patterns[i].weight);
                if (roll < 0) return i;
            }
            return patterns.Count - 1; // unreachable
        }
    }
}
