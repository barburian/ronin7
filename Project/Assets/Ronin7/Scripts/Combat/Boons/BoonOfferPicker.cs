using System;
using System.Collections.Generic;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Combat
{
    /// <summary>
    /// Pure boon-offer selection: which boons a cleared node shows the player. No Unity API beyond the
    /// BoonDefinition ScriptableObject references already in hand, and no state of its own — determinism
    /// comes entirely from the <see cref="RunRng"/> the caller passes in.
    /// </summary>
    public static class BoonOfferPicker
    {
        /// <summary>
        /// Picks up to <paramref name="count"/> distinct boons from <paramref name="pool"/>, weighted by
        /// rarity for <paramref name="kind"/>. Boons already at their max stack count (per
        /// <paramref name="stacksHeld"/>) are never offered. Degrades gracefully: a null/empty pool
        /// returns an empty list, and a pool with fewer eligible boons than <paramref name="count"/>
        /// returns only what's available.
        ///
        /// A1.6: this can legitimately return **0 or 1 items from a non-empty pool** — e.g. a Treasure
        /// node whose eligible entries are all Common, and Common has weight 0 for Treasure (see
        /// <see cref="RarityWeight"/>), so every remaining draw sees all-zero weights and stops. Callers
        /// (the offer UI) MUST auto-advance on a 0-item result and may present a 1- or 2-item choice —
        /// never block waiting for a choice that cannot be made, which is a run-ending soft-lock in a
        /// headset.
        /// </summary>
        public static List<BoonDefinition> Pick(
            IReadOnlyList<BoonDefinition> pool,
            Func<string, int> stacksHeld,
            RoomKind kind,
            ref RunRng rng,
            int count = 3,
            float luck = 0f)
        {
            var result = new List<BoonDefinition>();
            if (pool == null || pool.Count == 0) return result;

            var eligible = new List<BoonDefinition>(pool.Count);
            var seenIds = new HashSet<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var boon = pool[i];
                if (!IsOfferable(boon, stacksHeld)) continue;
                if (!seenIds.Add(boon.id)) continue; // dedupe: distinct ids only
                eligible.Add(boon);
            }

            // P1-08 (BUG-22): tier first (node weights, independent of how many boons each tier holds),
            // then uniform within the tier. Two RNG draws per offered boon.
            var tierWeights = new int[3];
            for (int t = 0; t < 3; t++) tierWeights[t] = RarityWeight((BoonRarity)t, kind, luck);

            var inTier = new List<BoonDefinition>();
            for (int i = 0; i < count && eligible.Count > 0; i++)
            {
                int tier = rng.PickWeighted(tierWeights);
                if (tier < 0) break; // all tier weights zero

                // Empty tier falls to the next tier down (only tiers with weight > 0). If nothing is
                // below, look up so a non-empty pool isn't starved by an unlucky tier roll.
                int use = FirstNonEmptyTier(eligible, tierWeights, tier, -1);
                if (use < 0) use = FirstNonEmptyTier(eligible, tierWeights, tier + 1, +1);
                if (use < 0) break; // nothing with positive weight left; A1.6 short offer

                inTier.Clear();
                for (int e = 0; e < eligible.Count; e++)
                    if ((int)eligible[e].rarity == use) inTier.Add(eligible[e]);

                var picked = inTier[rng.NextInt(inTier.Count)];
                result.Add(picked);
                eligible.Remove(picked);
            }

            return result;
        }

        /// <summary>Not null, has an id, not at max stacks, and not an already-granted ability.</summary>
        private static bool IsOfferable(BoonDefinition boon, Func<string, int> stacksHeld)
        {
            if (boon == null || string.IsNullOrEmpty(boon.id)) return false;
            int held = stacksHeld != null ? stacksHeld(boon.id) : 0;
            // A1.2: maxStacks <= 0 treated as 1, matching BoonInventory.Add, so a mis-authored asset
            // degrades to "offerable once" rather than "silently never offerable".
            if (held >= Mathf.Max(1, boon.maxStacks)) return false; // already maxed out
            // P1-08 (BUG-14): an ability already granted this run would be a dead pick.
            return !(boon.effect == BoonEffectKind.GrantAbility && RunState.HasGrantedAbility(boon.abilityId));
        }

        /// <summary>P2-04 events: one offerable boon of exactly <paramref name="rarity"/>, uniform; null if
        /// none is left (the caller then falls back to a Treasure-weighted <see cref="Pick"/>).</summary>
        public static BoonDefinition PickOfRarity(IReadOnlyList<BoonDefinition> pool, Func<string, int> stacksHeld, BoonRarity rarity, ref RunRng rng)
        {
            if (pool == null) return null;
            var matches = new List<BoonDefinition>();
            for (int i = 0; i < pool.Count; i++)
                if (IsOfferable(pool[i], stacksHeld) && pool[i].rarity == rarity && !matches.Contains(pool[i])) matches.Add(pool[i]);
            return matches.Count == 0 ? null : matches[rng.NextInt(matches.Count)];
        }

        private static int FirstNonEmptyTier(List<BoonDefinition> eligible, int[] tierWeights, int from, int step)
        {
            for (int t = from; t >= 0 && t < 3; t += step)
            {
                if (tierWeights[t] <= 0) continue;
                for (int e = 0; e < eligible.Count; e++)
                    if ((int)eligible[e].rarity == t) return t;
            }
            return -1;
        }

        /// <summary>
        /// Rarity weight for a single node kind. Combat favours Common, Elite shifts toward Rare, Boss
        /// shifts hardest toward Epic, and Treasure excludes Common entirely (guarantees at least Rare).
        /// Forge doesn't offer boons but falls back to Combat's weights rather than throwing.
        /// </summary>
        /// <summary>P4-02 Luck: <see cref="RarityWeight(BoonRarity, RoomKind)"/> plus +4 Rare / +2 Epic per
        /// whole luck point (cap 3). Common is never changed, so Treasure still excludes it.</summary>
        public static int RarityWeight(BoonRarity rarity, RoomKind kind, float luck)
        {
            int points = Mathf.Clamp(Mathf.FloorToInt(luck + 1e-4f), 0, 3);
            int bonus = rarity == BoonRarity.Rare ? 4 : rarity == BoonRarity.Epic ? 2 : 0;
            return RarityWeight(rarity, kind) + bonus * points;
        }

        public static int RarityWeight(BoonRarity rarity, RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Treasure:
                    return rarity switch
                    {
                        BoonRarity.Common => 0,
                        BoonRarity.Rare => 70,
                        BoonRarity.Epic => 30,
                        _ => 0
                    };
                case RoomKind.Elite:
                    return rarity switch
                    {
                        BoonRarity.Common => 40,
                        BoonRarity.Rare => 45,
                        BoonRarity.Epic => 15,
                        _ => 0
                    };
                case RoomKind.Boss:
                    return rarity switch
                    {
                        BoonRarity.Common => 15,
                        BoonRarity.Rare => 35,
                        BoonRarity.Epic => 50,
                        _ => 0
                    };
                default: // Combat, Forge
                    return rarity switch
                    {
                        BoonRarity.Common => 55,
                        BoonRarity.Rare => 38,
                        BoonRarity.Epic => 7,
                        _ => 0
                    };
            }
        }
    }
}
