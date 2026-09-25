using System.Text;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Module 5a — authors the 15 <see cref="BoonDefinition"/> assets (final <c>BoonId</c> set, Amendment
    /// A1.7) plus the <see cref="BoonCatalog"/> that references them, at
    /// <c>Assets/Ronin7/Data/Boons/</c>. Idempotent: re-running finds each existing asset by path and
    /// overwrites its fields in place (matches every other builder's <c>Ensure*</c> convention), so it's
    /// safe to re-run after a design tweak here without leaving orphan assets or breaking existing
    /// references to them (e.g. <c>RunDirector.boonCatalog</c>).
    ///
    /// Magnitudes below are gated by <c>BoonCatalogInvariantTests</c> where the design doc pins an exact
    /// number (Ironskin/KeenEdge — A6.3, A7.6) and by <c>maxStacks == 1</c> for every GrantAbility/
    /// ReviveOnce boon (A1.8); the rest are this builder's own call, documented per-entry below.
    /// </summary>
    public static class BoonCatalogBuilder
    {
        private const string BoonsFolder = "Assets/Ronin7/Data/Boons";
        private const string CatalogPath = BoonsFolder + "/BoonCatalog.asset";

        private struct BoonSpec
        {
            public string AssetName;
            public string Id;
            public string DisplayName;
            public string Description;
            public BoonRarity Rarity;
            public BoonEffectKind Effect;
            public float Magnitude;
            public string AbilityId;
            public int MaxStacks;
        }

        // ---- Common (7) — modest, always-useful stat nudges. Weighted heaviest at Combat (A1.7: 55). ----
        private static readonly BoonSpec[] Specs =
        {
            new BoonSpec
            {
                AssetName = "KeenEdge", Id = BoonId.KeenEdge,
                DisplayName = "Keen Edge", Description = "+5% blade damage per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.BladeDamageMultiplier,
                // A6.3/A7.6: magnitude and maxStacks are load-bearing, not stylistic. MetaUpgradeId
                // .StartingDamage grants stacks of this boon 1:1 with its level (max level 5) — the
                // magnitude must equal the doc's "+5% blade damage per level" promise, and maxStacks
                // must clear 5 with in-run headroom or the boon silently drops out of the offer pool
                // once a player has fully bought the meta upgrade (A7.6).
                Magnitude = 0.05f, AbilityId = null, MaxStacks = 8,
            },
            new BoonSpec
            {
                AssetName = "Whetstone", Id = BoonId.Whetstone,
                DisplayName = "Whetstone", Description = "+8% blade damage per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.BladeDamageMultiplier,
                // A plain in-run damage Common, not tied to any meta upgrade — normal 3-stack headroom
                // is enough, and a slightly higher per-stack rate than KeenEdge keeps it worth taking
                // even though KeenEdge can out-stack it over a long run.
                Magnitude = 0.08f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "Ironskin", Id = BoonId.Ironskin,
                DisplayName = "Ironskin", Description = "+10 max HP per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.MaxHealthAdd,
                // Same load-bearing coupling as KeenEdge above, mirrored for MetaUpgradeId.StartingHealth
                // ("+10 max HP per level", max level 5).
                Magnitude = 10f, AbilityId = null, MaxStacks = 8,
            },
            new BoonSpec
            {
                AssetName = "SecondSkin", Id = BoonId.SecondSkin,
                DisplayName = "Second Skin", Description = "+15 max HP per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.MaxHealthAdd,
                // Plain in-run health Common (not meta-coupled): a bigger flat chunk per stack than
                // Ironskin, normal 3-stack headroom.
                Magnitude = 15f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "Bloodletter", Id = BoonId.Bloodletter,
                DisplayName = "Bloodletter", Description = "Heal 3 HP on every kill.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.HealOnKill,
                // 3 HP/kill matches the original contract's own worked example (§4 Module 2 doc
                // comment). 3 stacks = 9 HP/kill at the cap, a meaningful but not run-trivialising heal
                // against depth-14 trash HP pools.
                Magnitude = 3f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "FlowInitiate", Id = BoonId.FlowInitiate,
                DisplayName = "Flow Initiate", Description = "+3% parry-flow bonus per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.ParryFlowBonus,
                // Small nudge to ParryFlowController's per-stack bonus (base 0.08 — see PerStackBonus
                // there); a Common should not out-value SunderBeat, the Rare version of this same effect.
                Magnitude = 0.03f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "ComboInitiate", Id = BoonId.ComboInitiate,
                DisplayName = "Combo Initiate", Description = "+3% combo bonus per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.ComboBonus,
                // Mirrors FlowInitiate's reasoning for ComboMomentumController's per-stack bonus.
                Magnitude = 0.03f, AbilityId = null, MaxStacks = 3,
            },

            // ---- Rare (3) — noticeably stronger than the Common tier. ----
            new BoonSpec
            {
                AssetName = "SunderBeat", Id = BoonId.SunderBeat,
                DisplayName = "Sunder Beat", Description = "+8% parry-flow bonus per stack.",
                Rarity = BoonRarity.Rare, Effect = BoonEffectKind.ParryFlowBonus,
                // Named for the shipped perfect-parry streak mechanic it amplifies (see
                // MeleeAttacker/ParryFlowController's own "Sunder Beat" comments) — roughly 2.5x
                // FlowInitiate's per-stack rate, appropriate for the Rare tier.
                Magnitude = 0.08f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "FollowThrough", Id = BoonId.FollowThrough,
                DisplayName = "Follow Through", Description = "+8% combo bonus per stack.",
                Rarity = BoonRarity.Rare, Effect = BoonEffectKind.ComboBonus,
                // Rare-tier combo counterpart to SunderBeat, same reasoning.
                Magnitude = 0.08f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "SecondWind", Id = BoonId.SecondWind,
                DisplayName = "Second Wind", Description = "Survive one lethal blow this run.",
                Rarity = BoonRarity.Rare, Effect = BoonEffectKind.ReviveOnce,
                // A1.8/task spec: every ReviveOnce boon is maxStacks 1. Magnitude is unused by
                // BoonInventory for this effect kind (HasRevive/ConsumeRevive are stack-counted, not
                // magnitude-scaled) — RunDirector separately sets Health.ReviveFraction (A6.2) while
                // this boon is held, so 0 here is correct, not a placeholder.
                Magnitude = 0f, AbilityId = null, MaxStacks = 1,
            },

            // ---- Epic (5) — grant the five already-built abilities (§2 reuse mandate). ----
            new BoonSpec
            {
                AssetName = "GrantWeakpointSight", Id = BoonId.GrantWeakpointSight,
                DisplayName = "Weakpoint Sight", Description = "Unlocks Weakpoint Sight for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.WeakpointSight, MaxStacks = 1,
            },
            new BoonSpec
            {
                AssetName = "GrantOverdrive", Id = BoonId.GrantOverdrive,
                DisplayName = "Overdrive", Description = "Unlocks Overdrive for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.Overdrive, MaxStacks = 1,
            },
            new BoonSpec
            {
                AssetName = "GrantPhaseStep", Id = BoonId.GrantPhaseStep,
                DisplayName = "Phase Step", Description = "Unlocks Phase Step for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.PhaseStep, MaxStacks = 1,
            },
            new BoonSpec
            {
                AssetName = "GrantUnbroken", Id = BoonId.GrantUnbroken,
                DisplayName = "Unbroken", Description = "Unlocks Unbroken for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.Unbroken, MaxStacks = 1,
            },
            new BoonSpec
            {
                AssetName = "GrantMirror", Id = BoonId.GrantMirror,
                DisplayName = "Mirror", Description = "Unlocks Mirror for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.Mirror, MaxStacks = 1,
            },
        };

        [MenuItem("Tools/Space Samurai/Roguelike/Build Boon Catalog")]
        public static void BuildBoonCatalog()
        {
            EnsureFolder(BoonsFolder);

            var boons = new BoonDefinition[Specs.Length];
            var report = new StringBuilder();
            for (int i = 0; i < Specs.Length; i++)
            {
                boons[i] = EnsureBoon(Specs[i]);
                report.Append(Specs[i].AssetName).Append(" [").Append(Specs[i].Rarity).Append("] ")
                      .Append(Specs[i].Effect).Append(" magnitude=").Append(Specs[i].Magnitude)
                      .Append(" maxStacks=").Append(Specs[i].MaxStacks).Append('\n');
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BoonCatalog>(CatalogPath);
            bool catalogIsNew = catalog == null;
            if (catalogIsNew) catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = boons;
            if (catalogIsNew) AssetDatabase.CreateAsset(catalog, CatalogPath);
            else EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            Debug.Log($"[BoonCatalogBuilder] Built {boons.Length} boon(s) and the catalog at {CatalogPath}.\n{report}");
        }

        private static BoonDefinition EnsureBoon(BoonSpec spec)
        {
            string path = BoonsFolder + "/" + spec.AssetName + ".asset";
            var boon = AssetDatabase.LoadAssetAtPath<BoonDefinition>(path);
            bool isNew = boon == null;
            if (isNew) boon = ScriptableObject.CreateInstance<BoonDefinition>();

            boon.id = spec.Id;
            boon.displayName = spec.DisplayName;
            boon.description = spec.Description;
            boon.rarity = spec.Rarity;
            boon.effect = spec.Effect;
            boon.magnitude = spec.Magnitude;
            boon.abilityId = spec.AbilityId;
            boon.maxStacks = spec.MaxStacks;

            if (isNew) AssetDatabase.CreateAsset(boon, path);
            else EditorUtility.SetDirty(boon);
            return boon;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
