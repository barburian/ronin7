using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>P8-02: strike direction. Serialized: append-only.</summary>
    public enum StrikeDirection { Overhead }

    /// <summary>P8-02: one strike of a pattern. Timings are multipliers of the body's own
    /// <see cref="EnemyDefinition"/> telegraph/active/recover times (1 = today's chop), so sector and Heat
    /// tuning on the body still apply.</summary>
    [System.Serializable]
    public struct Strike
    {
        public StrikeDirection direction;
        public float windup, active, recovery;
        public static Strike Chop => new Strike { direction = StrikeDirection.Overhead, windup = 1f, active = 1f, recovery = 1f };
    }

    [System.Serializable]
    public class AttackPattern
    {
        public string name = "Chop";
        public List<Strike> strikes = new List<Strike> { Strike.Chop };
    }

    /// <summary>
    /// P8-02 (D-17): a weapon is its own prefab + this definition, composed onto a body's sockets at spawn
    /// (<see cref="Enemy.Equip"/>), never baked into the enemy prefab. The weapon owns its moveset (P8-03).
    /// </summary>
    [CreateAssetMenu(menuName = "Ronin7/Enemy Weapon Definition")]
    public class EnemyWeaponDefinition : ScriptableObject
    {
        public const string MainSocket = "Socket_MainHand", OffSocket = "Socket_OffHand", TipName = "BladeTip";
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

        /// <summary>The strike the next attack uses. ponytail: always pattern 0 / strike 0; the weighted
        /// no-repeat picker and combos are P8-03.</summary>
        public Strike NextStrike() =>
            patterns != null && patterns.Count > 0 && patterns[0].strikes != null && patterns[0].strikes.Count > 0
                ? patterns[0].strikes[0] : Strike.Chop;

        /// <summary>Applies this weapon's stat mods to the body's (runtime, already-scaled) definition.</summary>
        public void ApplyTo(EnemyDefinition body)
        {
            body.attackRange *= reach;
            body.damage *= damage;
        }

        /// <summary>Instantiates the weapon under <paramref name="socket"/> (an empty root when meshless) with
        /// a <see cref="TipName"/> child at <see cref="tip"/>. Returns the weapon root.</summary>
        public Transform Attach(Transform socket)
        {
            Transform root = prefab != null ? Instantiate(prefab, socket, false).transform : new GameObject(name).transform;
            root.SetParent(socket, false);
            var t = new GameObject(TipName).transform;
            t.SetParent(root, false);
            t.localPosition = tip;
            return root;
        }
    }
}
