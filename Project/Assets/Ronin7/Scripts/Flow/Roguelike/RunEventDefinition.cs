using System;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Flow
{
    /// <summary>P2-04: what one event option does. Append-only (serialized in Data/Roguelike/Events).
    /// MaxHealthAdd mirrors the boon effect of the same name; the rest are the HP/shard/reroll deltas.</summary>
    public enum RunEventEffectKind
    {
        /// <summary>amount = flat max-HP change for the rest of the run (negative allowed).</summary>
        MaxHealthAdd,
        /// <summary>amount = fraction of max HP healed (0.3 = 30%).</summary>
        HealFraction,
        /// <summary>amount = flat damage; an event never kills (leaves at least 1 HP).</summary>
        Damage,
        /// <summary>Grant one boon of <see cref="RunEventEffect.rarity"/>.</summary>
        GrantBoon,
        /// <summary>amount = reroll tokens added.</summary>
        AddReroll,
        /// <summary>amount = this run's Shards spent (P4-11; enum value unchanged, assets keep their int); the option is disabled when short.</summary>
        SpendShards,
    }

    [Serializable]
    public class RunEventEffect
    {
        public RunEventEffectKind kind;
        public float amount;
        public BoonRarity rarity;
    }

    [Serializable]
    public class RunEventOption
    {
        public string label;
        [Tooltip("One line under the label, e.g. \"-15 max HP, gain a Rare boon\".")]
        public string hint;
        [Range(0f, 1f), Tooltip("Chance the effects apply; otherwise failEffects apply. Rolled on a RunRng substream.")]
        public float chance = 1f;
        public RunEventEffect[] effects = Array.Empty<RunEventEffect>();
        public RunEventEffect[] failEffects = Array.Empty<RunEventEffect>();
    }

    /// <summary>P2-04: a pure-data Event node — title, body and two options.</summary>
    [CreateAssetMenu(menuName = "Ronin 7/Run Event", fileName = "RunEvent")]
    public class RunEventDefinition : ScriptableObject
    {
        public string title;
        [TextArea] public string body;
        public RunEventOption optionA = new RunEventOption();
        public RunEventOption optionB = new RunEventOption();
    }

    /// <summary>The summed result of resolving one option.</summary>
    internal struct RunEventOutcome
    {
        public bool Success;
        public float MaxHealthDelta;
        public float HealFraction;
        public float Damage;
        public int Rerolls;
        public int ShardsSpent;
        public List<BoonRarity> Boons;
    }

    /// <summary>Pure event maths (EditMode-testable). RunDirector applies the outcome to the run and rig.</summary>
    internal static class RunEventResolver
    {
        /// <summary>Shards the option charges up front (its success-branch SpendShards).</summary>
        internal static int ShardCost(RunEventOption option)
        {
            int cost = 0;
            if (option?.effects != null)
                foreach (var e in option.effects)
                    if (e != null && e.kind == RunEventEffectKind.SpendShards) cost += Mathf.RoundToInt(e.amount);
            return cost;
        }

        internal static bool CanAfford(RunEventOption option, int runShards) => runShards >= ShardCost(option);

        /// <summary>Rolls <see cref="RunEventOption.chance"/> (one draw only when 0 &lt; chance &lt; 1) and sums
        /// the applied branch.</summary>
        internal static RunEventOutcome Resolve(RunEventOption option, ref RunRng rng)
        {
            var o = new RunEventOutcome { Boons = new List<BoonRarity>() };
            if (option == null) return o;
            o.Success = option.chance >= 1f || (option.chance > 0f && rng.NextFloat() < option.chance);
            var list = o.Success ? option.effects : option.failEffects;
            if (list == null) return o;
            foreach (var e in list)
            {
                if (e == null) continue;
                switch (e.kind)
                {
                    case RunEventEffectKind.MaxHealthAdd: o.MaxHealthDelta += e.amount; break;
                    case RunEventEffectKind.HealFraction: o.HealFraction += e.amount; break;
                    case RunEventEffectKind.Damage: o.Damage += e.amount; break;
                    case RunEventEffectKind.GrantBoon: o.Boons.Add(e.rarity); break;
                    case RunEventEffectKind.AddReroll: o.Rerolls += Mathf.RoundToInt(e.amount); break;
                    case RunEventEffectKind.SpendShards: o.ShardsSpent += Mathf.RoundToInt(e.amount); break;
                }
            }
            return o;
        }

        /// <summary>New (max, current) HP: max change first (current never above the new max), then the
        /// heal (fraction of the new max), then damage — floored at 1 HP, so an event never kills.</summary>
        internal static void ApplyHealth(float current, float max, in RunEventOutcome o, out float newMax, out float newCurrent)
        {
            newMax = Mathf.Max(1f, max + o.MaxHealthDelta);
            newCurrent = Mathf.Min(current, newMax);
            if (o.HealFraction > 0f) newCurrent = Mathf.Min(newMax, newCurrent + o.HealFraction * newMax);
            if (o.Damage > 0f) newCurrent = Mathf.Max(1f, newCurrent - o.Damage);
        }
    }
}
