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

            // P4-05: what the run already holds, derived from the pool itself (no extra parameter).
            BoonTag owned = OwnedTags(pool, stacksHeld);
            float epicBonus = HeldSum(pool, stacksHeld, BoonEffectKind.EpicWeight);

            var eligible = new List<BoonDefinition>(pool.Count);
            var seenIds = new HashSet<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var boon = pool[i];
                if (!IsOfferable(boon, stacksHeld, owned)) continue;
                if (!seenIds.Add(boon.id)) continue; // dedupe: distinct ids only
                eligible.Add(boon);
            }

            // P1-08 (BUG-22): tier first (node weights, independent of how many boons each tier holds),
            // then within the tier. Two RNG draws per offered boon. P4-05: the Duo tier only has weight
            // while a Duo is eligible (a gated Duo tier must not leak its weight into Epic), and Hollow
            // Oath scales Epic.
            var tierWeights = new int[TierCount];
            for (int t = 0; t < TierCount; t++) tierWeights[t] = RarityWeight((BoonRarity)t, kind, luck);
            tierWeights[(int)BoonRarity.Epic] = Mathf.RoundToInt(tierWeights[(int)BoonRarity.Epic] * (1f + Mathf.Max(0f, epicBonus)));
            if (!HasTier(eligible, BoonRarity.Duo)) tierWeights[(int)BoonRarity.Duo] = 0;

            var inTier = new List<BoonDefinition>();
            var affinity = new List<int>();
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
                affinity.Clear();
                bool anyAffinity = false;
                for (int e = 0; e < eligible.Count; e++)
                {
                    if ((int)eligible[e].rarity != use) continue;
                    inTier.Add(eligible[e]);
                    int w = TagWeight(eligible[e], owned);
                    anyAffinity |= w != BaseWeight;
                    affinity.Add(w);
                }

                // P4-05: x1.5 for a tag shared with an owned boon. With no affinity in the tier this stays
                // the old uniform draw, so seeded offers for tagless pools are unchanged.
                var picked = anyAffinity ? inTier[rng.PickWeighted(affinity.ToArray())] : inTier[rng.NextInt(inTier.Count)];
                result.Add(picked);
                eligible.Remove(picked);
            }

            return result;
        }

        private const int TierCount = 4; // Common, Rare, Epic, Duo
        private const int BaseWeight = 2, AffinityWeight = 3; // x1.5

        /// <summary>P4-05: within-tier weight (2 = no shared tag, 3 = shares a tag with an owned boon: x1.5).</summary>
        public static int TagWeight(BoonDefinition boon, BoonTag owned) =>
            boon != null && (boon.tags & owned) != 0 ? AffinityWeight : BaseWeight;

        /// <summary>Union of the tags of every pool boon held at least once.</summary>
        public static BoonTag OwnedTags(IReadOnlyList<BoonDefinition> pool, Func<string, int> stacksHeld)
        {
            BoonTag owned = BoonTag.None;
            if (pool == null || stacksHeld == null) return owned;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && !string.IsNullOrEmpty(pool[i].id) && stacksHeld(pool[i].id) > 0) owned |= pool[i].tags;
            return owned;
        }

        private static float HeldSum(IReadOnlyList<BoonDefinition> pool, Func<string, int> stacksHeld, BoonEffectKind kind)
        {
            float sum = 0f;
            if (stacksHeld == null) return sum;
            var seen = new HashSet<string>();
            for (int i = 0; i < pool.Count; i++)
            {
                var b = pool[i];
                if (b == null || string.IsNullOrEmpty(b.id) || !seen.Add(b.id)) continue;
                sum += BoonInventory.Magnitude(b, kind) * stacksHeld(b.id);
            }
            return sum;
        }

        private static bool HasTier(List<BoonDefinition> eligible, BoonRarity rarity)
        {
            for (int e = 0; e < eligible.Count; e++) if (eligible[e].rarity == rarity) return true;
            return false;
        }

        /// <summary>P4-05 USER DECISION: a boon tagged Bleed / Storm / Executioner belongs to that archetype
        /// pack and is never offered (nor granted as a starting boon) until the pack is unlocked.</summary>
        public static bool IsPackUnlocked(BoonDefinition boon)
        {
            if (boon == null) return false;
            if ((boon.tags & BoonTag.Bleed) != 0 && !MetaProgression.IsPackUnlocked(BoonPackId.Bleed)) return false;
            if ((boon.tags & BoonTag.Storm) != 0 && !MetaProgression.IsPackUnlocked(BoonPackId.Storm)) return false;
            if ((boon.tags & BoonTag.Executioner) != 0 && !MetaProgression.IsPackUnlocked(BoonPackId.Executioner)) return false;
            return true;
        }

        /// <summary>Not null, has an id, not at max stacks, not an already-granted single-stack ability, its
        /// pack unlocked, the ability it modifies held (a non-GrantAbility boon naming an abilityId), and for a
        /// Duo every required tag owned.</summary>
        private static bool IsOfferable(BoonDefinition boon, Func<string, int> stacksHeld, BoonTag owned)
        {
            if (boon == null || string.IsNullOrEmpty(boon.id)) return false;
            int held = stacksHeld != null ? stacksHeld(boon.id) : 0;
            // A1.2: maxStacks <= 0 treated as 1, matching BoonInventory.Add, so a mis-authored asset
            // degrades to "offerable once" rather than "silently never offerable".
            int max = Mathf.Max(1, boon.maxStacks);
            if (held >= max) return false; // already maxed out
            if (!IsPackUnlocked(boon)) return false;
            if (boon.rarity == BoonRarity.Duo)
            {
                if (!boon.TryGetRequiredTags(out BoonTag required) || required == BoonTag.None) return false;
                if ((owned & required) != required) return false;
            }
            bool hasAbility = !string.IsNullOrEmpty(boon.abilityId) && RunState.HasGrantedAbility(boon.abilityId);
            // P4-05: a modifier of an ability (Afterimage, Overclock, Echo Twin, Ghost Step, Thunder Riposte)
            // would be a dead pick without it.
            if (boon.effect != BoonEffectKind.GrantAbility) return string.IsNullOrEmpty(boon.abilityId) || hasAbility;
            // P1-08 (BUG-14): an ability already granted this run would be a dead pick, unless the boon has
            // stacks left to give (P4-05 Riposte's extra-stack damage).
            return !(hasAbility && max <= 1);
        }

        /// <summary>P2-04 events: one offerable boon of exactly <paramref name="rarity"/>, uniform; null if
        /// none is left (the caller then falls back to a Treasure-weighted <see cref="Pick"/>).</summary>
        public static BoonDefinition PickOfRarity(IReadOnlyList<BoonDefinition> pool, Func<string, int> stacksHeld, BoonRarity rarity, ref RunRng rng)
        {
            if (pool == null) return null;
            BoonTag owned = OwnedTags(pool, stacksHeld);
            var matches = new List<BoonDefinition>();
            for (int i = 0; i < pool.Count; i++)
                if (IsOfferable(pool[i], stacksHeld, owned) && pool[i].rarity == rarity && !matches.Contains(pool[i])) matches.Add(pool[i]);
            return matches.Count == 0 ? null : matches[rng.NextInt(matches.Count)];
        }

        private static int FirstNonEmptyTier(List<BoonDefinition> eligible, int[] tierWeights, int from, int step)
        {
            for (int t = from; t >= 0 && t < TierCount; t += step)
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
            int bonus = rarity == BoonRarity.Rare ? 4 : rarity == BoonRarity.Epic ? 2 : 0; // Duo: none
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
                        BoonRarity.Duo => 15,
                        _ => 0
                    };
                case RoomKind.Elite:
                    return rarity switch
                    {
                        BoonRarity.Common => 40,
                        BoonRarity.Rare => 45,
                        BoonRarity.Epic => 15,
                        BoonRarity.Duo => 10,
                        _ => 0
                    };
                case RoomKind.Boss:
                    return rarity switch
                    {
                        BoonRarity.Common => 15,
                        BoonRarity.Rare => 35,
                        BoonRarity.Epic => 50,
                        BoonRarity.Duo => 15,
                        _ => 0
                    };
                default: // Combat, Forge
                    return rarity switch
                    {
                        BoonRarity.Common => 55,
                        BoonRarity.Rare => 38,
                        BoonRarity.Epic => 7,
                        BoonRarity.Duo => 6,
                        _ => 0
                    };
            }
        }
    }
}
