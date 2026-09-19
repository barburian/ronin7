using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Pure aggregation of the boons a run currently holds into the numbers the rest of combat reads.
    /// A plain class (not a MonoBehaviour) so it can live on RunState/RunDirector and be unit-tested
    /// without a scene. Multiplier-shaped effects (currently just BladeDamageMultiplier) stack as
    /// (1+magnitude)^stacks per boon id, multiplied together across all boons of that effect kind;
    /// MaxHealthAdd/HealOnKill/ParryFlowBonus/ComboBonus are flat sums.
    /// </summary>
    public class BoonInventory
    {
        private readonly Dictionary<string, int> _stacks = new Dictionary<string, int>();
        private readonly Dictionary<string, BoonDefinition> _definitions = new Dictionary<string, BoonDefinition>();
        private readonly List<string> _grantedAbilities = new List<string>();
        private int _grantedRevives;
        private int _spentRevives;

        /// <summary>Adds one stack of the given boon. A null definition or a definition with no id is
        /// ignored. A1.2: <c>maxStacks</c> is enforced here too (not just by BoonOfferPicker) — starting
        /// boons, Treasure grants and debug grants all bypass the picker's own guard, so this is the one
        /// place every acquisition path funnels through.</summary>
        public void Add(BoonDefinition boon)
        {
            if (boon == null || string.IsNullOrEmpty(boon.id)) return;
            if (Stacks(boon.id) >= Mathf.Max(1, boon.maxStacks)) return;

            _stacks.TryGetValue(boon.id, out int current);
            _stacks[boon.id] = current + 1;
            _definitions[boon.id] = boon;

            if (boon.effect == BoonEffectKind.ReviveOnce) _grantedRevives++;
            if (boon.effect == BoonEffectKind.GrantAbility && !string.IsNullOrEmpty(boon.abilityId)
                && !_grantedAbilities.Contains(boon.abilityId))
            {
                _grantedAbilities.Add(boon.abilityId);
            }
        }

        /// <summary>Number of stacks held of the given boon id. Unknown/null ids return 0.</summary>
        public int Stacks(string boonId) =>
            !string.IsNullOrEmpty(boonId) && _stacks.TryGetValue(boonId, out int stacks) ? stacks : 0;

        public float BladeDamageMultiplier => MultiplierProduct(BoonEffectKind.BladeDamageMultiplier);
        public float MaxHealthAdd => AdditiveSum(BoonEffectKind.MaxHealthAdd);
        public float HealOnKill => AdditiveSum(BoonEffectKind.HealOnKill);
        public float ParryFlowBonus => AdditiveSum(BoonEffectKind.ParryFlowBonus);
        public float ComboBonus => AdditiveSum(BoonEffectKind.ComboBonus);

        /// <summary>A1.1: granted revives minus revives already spent. Spent count is set from the
        /// durable <see cref="RunState.RevivesSpent"/> via <see cref="SetRevivesSpent"/> — this instance
        /// gets rebuilt every arena-scene reload, so a transient counter alone would resurrect a used
        /// Second Wind on the next node.</summary>
        public bool HasRevive => _grantedRevives - _spentRevives > 0;

        /// <summary>A1.1: syncs the spent-revive count from the durable <see cref="RunState.RevivesSpent"/>
        /// after this inventory is rebuilt (e.g. on an arena-scene reload). The caller (RunDirector) owns
        /// one inventory for the whole run and calls this after replaying <see cref="Add"/> for every held
        /// boon.</summary>
        public void SetRevivesSpent(int spent) => _spentRevives = spent;

        /// <summary>Consumes one revive if any remain, notifying <see cref="RunState"/> so the spend is
        /// durable across the next rebuild. Returns false when none remain.</summary>
        public bool ConsumeRevive()
        {
            if (!HasRevive) return false;
            _spentRevives++;
            RunState.NoteReviveSpent();
            return true;
        }

        public IReadOnlyList<string> GrantedAbilities => _grantedAbilities;

        /// <summary>Resets the inventory to empty, as at the start of a new run.</summary>
        public void Clear()
        {
            _stacks.Clear();
            _definitions.Clear();
            _grantedAbilities.Clear();
            _grantedRevives = 0;
            _spentRevives = 0;
        }

        private float MultiplierProduct(BoonEffectKind kind)
        {
            float product = 1f;
            foreach (var pair in _definitions)
            {
                if (pair.Value.effect != kind) continue;
                product *= Mathf.Pow(1f + pair.Value.magnitude, Stacks(pair.Key));
            }
            return product;
        }

        private float AdditiveSum(BoonEffectKind kind)
        {
            float sum = 0f;
            foreach (var pair in _definitions)
            {
                if (pair.Value.effect != kind) continue;
                sum += pair.Value.magnitude * Stacks(pair.Key);
            }
            return sum;
        }
    }
}
