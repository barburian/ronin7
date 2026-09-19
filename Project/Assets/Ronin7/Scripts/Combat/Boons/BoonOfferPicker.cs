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
            int count = 3)
        {
            var result = new List<BoonDefinition>();
            if (pool == null || pool.Count == 0) return result;

            var eligible = new List<BoonDefinition>(pool.Count);
            var seenIds = new HashSet<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var boon = pool[i];
                if (boon == null || string.IsNullOrEmpty(boon.id)) continue;
                if (!seenIds.Add(boon.id)) continue; // dedupe: distinct ids only
                int held = stacksHeld != null ? stacksHeld(boon.id) : 0;
                // A1.2: maxStacks <= 0 treated as 1, matching BoonInventory.Add, so a mis-authored asset
                // degrades to "offerable once" rather than "silently never offerable".
                if (held >= Mathf.Max(1, boon.maxStacks)) continue; // already maxed out
                eligible.Add(boon);
            }

            for (int i = 0; i < count && eligible.Count > 0; i++)
            {
                var weights = new int[eligible.Count];
                for (int w = 0; w < eligible.Count; w++)
                    weights[w] = RarityWeight(eligible[w].rarity, kind);

                int pickIndex = rng.PickWeighted(weights);
                if (pickIndex < 0) break; // all-zero weights left; nothing more to offer

                result.Add(eligible[pickIndex]);
                eligible.RemoveAt(pickIndex);
            }

            return result;
        }

        /// <summary>
        /// Rarity weight for a single node kind. Combat favours Common, Elite shifts toward Rare, Boss
        /// shifts hardest toward Epic, and Treasure excludes Common entirely (guarantees at least Rare).
        /// Forge doesn't offer boons but falls back to Combat's weights rather than throwing.
        /// </summary>
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
