using System.Text;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Module 5a — authors the 45 <see cref="BoonDefinition"/> assets (15 + P4-05's 30) (final <c>BoonId</c> set, Amendment
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
            // P4-05
            public BoonTag Tags;
            public string[] RequiresTags;
            public bool HasSecondary;
            public BoonEffectKind SecondaryEffect;
            public float SecondaryMagnitude;
        }

        /// <summary>P4-05 compact spec for the 30 archetype boons (secondary optional).</summary>
        private static BoonSpec B(string asset, string id, string name, string desc, BoonRarity rarity, BoonTag tags,
            BoonEffectKind effect, float magnitude, int maxStacks, string abilityId = null,
            BoonEffectKind? secondary = null, float secondaryMagnitude = 0f, string[] requiresTags = null) => new BoonSpec
        {
            AssetName = asset, Id = id, DisplayName = name, Description = desc, Rarity = rarity, Tags = tags,
            Effect = effect, Magnitude = magnitude, MaxStacks = maxStacks, AbilityId = abilityId,
            HasSecondary = secondary.HasValue, SecondaryEffect = secondary ?? default,
            SecondaryMagnitude = secondaryMagnitude, RequiresTags = requiresTags,
        };

        // ---- Common (7) — modest, always-useful stat nudges. Weighted heaviest at Combat (A1.7: 55). ----
        private static readonly BoonSpec[] Specs =
        {
            new BoonSpec
            {
                AssetName = "KeenEdge", Id = BoonId.KeenEdge,
                DisplayName = "Keen Edge", Description = "+5% blade damage and +5% posture damage per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.BladeDamageMultiplier,
                // A6.3/A7.6: magnitude and maxStacks are load-bearing, not stylistic. MetaUpgradeId
                // .StartingDamage grants stacks of this boon 1:1 with its level (max level 5) — the
                // magnitude must equal the doc's "+5% blade damage per level" promise, and maxStacks
                // must clear 5 with in-run headroom or the boon silently drops out of the offer pool
                // once a player has fully bought the meta upgrade (A7.6).
                Magnitude = 0.05f, AbilityId = null, MaxStacks = 8,
                HasSecondary = true, SecondaryEffect = BoonEffectKind.PostureDamage, SecondaryMagnitude = 0.05f, // P4-05 retune
            },
            new BoonSpec
            {
                AssetName = "Whetstone", Id = BoonId.Whetstone,
                DisplayName = "Salvager's Whetstone", Description = "+8% blade damage per stack.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.BladeDamageMultiplier,
                // A plain in-run damage Common, not tied to any meta upgrade — normal 3-stack headroom
                // is enough, and a slightly higher per-stack rate than KeenEdge keeps it worth taking
                // even though KeenEdge can out-stack it over a long run.
                Magnitude = 0.08f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "Ironskin", Id = BoonId.Ironskin,
                DisplayName = "Ironskin", Description = "+10 max HP per stack; heals 10 when picked.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.MaxHealthAdd,
                // Same load-bearing coupling as KeenEdge above, mirrored for MetaUpgradeId.StartingHealth
                // ("+10 max HP per level", max level 5).
                Magnitude = 10f, AbilityId = null, MaxStacks = 8,
                HasSecondary = true, SecondaryEffect = BoonEffectKind.HealOnPick, SecondaryMagnitude = 10f, // P4-05 retune
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
                DisplayName = "Flow Initiate", Description = "+3% parry-flow bonus per stack; the parry streak lasts 3 s longer.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.ParryFlowBonus,
                // Small nudge to ParryFlowController's per-stack bonus (base 0.08 — see PerStackBonus
                // there); a Common should not out-value SunderBeat, the Rare version of this same effect.
                Magnitude = 0.03f, AbilityId = null, MaxStacks = 3, Tags = BoonTag.Parry,
                HasSecondary = true, SecondaryEffect = BoonEffectKind.ParryStreakTimeout, SecondaryMagnitude = 3f, // P4-05 retune
            },
            new BoonSpec
            {
                AssetName = "ComboInitiate", Id = BoonId.ComboInitiate,
                DisplayName = "Reversal", Description = "+3% combo bonus per stack; a reversed swing on the same target keeps the combo.",
                Rarity = BoonRarity.Common, Effect = BoonEffectKind.ComboBonus,
                // Mirrors FlowInitiate's reasoning for ComboMomentumController's per-stack bonus.
                // P4-05/BUG-20: renamed Reversal (id kept for save compatibility).
                Magnitude = 0.03f, AbilityId = null, MaxStacks = 3, Tags = BoonTag.Flow,
                HasSecondary = true, SecondaryEffect = BoonEffectKind.ComboReversal, SecondaryMagnitude = 1f,
            },

            // ---- Rare (3) — noticeably stronger than the Common tier. ----
            new BoonSpec
            {
                AssetName = "SunderBeat", Id = BoonId.SunderBeat,
                DisplayName = "Sunder Beat", Description = "+8% parry-flow bonus per stack.",
                Rarity = BoonRarity.Rare, Effect = BoonEffectKind.ParryFlowBonus, Tags = BoonTag.Parry,
                // Named for the shipped perfect-parry streak mechanic it amplifies (see
                // MeleeAttacker/ParryFlowController's own "Sunder Beat" comments) — roughly 2.5x
                // FlowInitiate's per-stack rate, appropriate for the Rare tier.
                Magnitude = 0.08f, AbilityId = null, MaxStacks = 3,
            },
            new BoonSpec
            {
                AssetName = "FollowThrough", Id = BoonId.FollowThrough,
                DisplayName = "Follow Through", Description = "+8% combo bonus per stack.",
                Rarity = BoonRarity.Rare, Effect = BoonEffectKind.ComboBonus, Tags = BoonTag.Flow,
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
                Magnitude = 0f, AbilityId = AbilityId.Overdrive, MaxStacks = 1, Tags = BoonTag.Ability,
            },
            new BoonSpec
            {
                AssetName = "GrantPhaseStep", Id = BoonId.GrantPhaseStep,
                DisplayName = "Phase Step", Description = "Unlocks Phase Step for this run.",
                Rarity = BoonRarity.Epic, Effect = BoonEffectKind.GrantAbility,
                Magnitude = 0f, AbilityId = AbilityId.PhaseStep, MaxStacks = 1, Tags = BoonTag.Ability,
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
                Magnitude = 0f, AbilityId = AbilityId.Mirror, MaxStacks = 1, Tags = BoonTag.Ability,
            },
            // ---- P4-05: 30 archetype boons (Roadmap P4-05). Bleed / Storm / Executioner tags are locked packs
            // (BoonPackId) until bought; elemental tags also own the S8 Imbue element (RunDirector.ElementsFor). ----

            // Kensei (parry)
            B("IronWall", BoonId.IronWall, "Iron Dojo Wall", "Perfect-parry window +0.02 s per stack.", BoonRarity.Common, BoonTag.Parry, BoonEffectKind.ParryWindow, 0.1667f, 2),
            B("LongStagger", BoonId.LongStagger, "Long Stagger", "+20% enemy stagger duration per stack.", BoonRarity.Common, BoonTag.Parry, BoonEffectKind.StaggerDuration, 0.2f, 2),
            B("Riposte", BoonId.Riposte, "Riposte", "Unlocks Riposte: the hit after a perfect parry deals bonus damage. Extra stacks +25%.", BoonRarity.Rare, BoonTag.Parry,
                BoonEffectKind.GrantAbility, 0f, 2, AbilityId.Riposte, BoonEffectKind.RiposteDamage, 0.25f),
            B("Counterweight", BoonId.Counterweight, "Counterweight", "Heal 4 HP on every perfect parry.", BoonRarity.Rare, BoonTag.Parry, BoonEffectKind.HealOnPerfectParry, 4f, 2),
            B("Zanshin", BoonId.Zanshin, "Zanshin", "Unlocks Zanshin for this run.", BoonRarity.Epic, BoonTag.Parry, BoonEffectKind.GrantAbility, 0f, 1, AbilityId.Zanshin),

            // Executioner (pack)
            B("QuickDraw", BoonId.QuickDraw, "Quick Draw", "Unlocks Quick Draw for this run.", BoonRarity.Rare, BoonTag.Executioner, BoonEffectKind.GrantAbility, 0f, 1, AbilityId.QuickDraw),
            B("HeavyHand", BoonId.HeavyHand, "Heavy Hand", "+25% posture damage per stack.", BoonRarity.Common, BoonTag.Executioner, BoonEffectKind.PostureDamage, 0.25f, 3),
            B("Guillotine", BoonId.Guillotine, "Guillotine", "Posture breaks deal triple bonus damage.", BoonRarity.Rare, BoonTag.Executioner, BoonEffectKind.PostureBreakBonus, 2f, 1),
            B("Headhunter", BoonId.Headhunter, "Drovis Bounty", "+25% precision-hit damage per stack.", BoonRarity.Common, BoonTag.Executioner, BoonEffectKind.Precision, 0.25f, 3),

            // Dancer (flow)
            B("MomentumKeeper", BoonId.MomentumKeeper, "Momentum Keeper", "Combo window +0.4 s per stack.", BoonRarity.Common, BoonTag.Flow, BoonEffectKind.ComboWindow, 0.4f, 2),
            B("Whirlwind", BoonId.Whirlwind, "Whirlwind", "Hitting a second enemy within 0.15 s deals +30% to both.", BoonRarity.Rare, BoonTag.Flow, BoonEffectKind.MultiHitBonus, 0.3f, 1),

            // Hemorrhage (Bleed pack)
            B("SerratedEdge", BoonId.SerratedEdge, "Serrated Edge", "Hits bleed for 15% of the damage over 3 s (+10% per stack).", BoonRarity.Common, BoonTag.Bleed, BoonEffectKind.Bleed, 0.1f, 3),
            B("OpenVeins", BoonId.OpenVeins, "Open Veins", "Hits on bleeding enemies deal +20%.", BoonRarity.Rare, BoonTag.Bleed, BoonEffectKind.BleedVulnerability, 0.2f, 1),
            B("Exsanguinate", BoonId.Exsanguinate, "Exsanguinate", "Heal 6 HP when a bleeding enemy dies.", BoonRarity.Rare, BoonTag.Bleed, BoonEffectKind.HealOnBleedKill, 6f, 1),

            // Storm (pack)
            B("StaticEdge", BoonId.StaticEdge, "Static Edge", "Every 4th hit arcs lightning for 12 damage per stack.", BoonRarity.Common, BoonTag.Storm, BoonEffectKind.StaticChain, 12f, 3),
            B("Conductor", BoonId.Conductor, "Conductor", "Lightning chains to one more enemy and reaches 1.5 m farther per stack.", BoonRarity.Rare, BoonTag.Storm, BoonEffectKind.ChainJumps, 1f, 2),
            B("Thunderclap", BoonId.Thunderclap, "Thunderclap", "Blade clashes shock enemies within 2.5 m for 20 damage and posture.", BoonRarity.Rare, BoonTag.Storm, BoonEffectKind.ClashShock, 20f, 1),

            // Shadow (ability)
            B("Quickened", BoonId.Quickened, "Quickened", "-12% ability cooldowns per stack.", BoonRarity.Common, BoonTag.Ability, BoonEffectKind.AbilityHaste, 0.12f, 3),
            B("Afterimage", BoonId.Afterimage, "Afterimage", "+1 Phase Step charge per stack.", BoonRarity.Rare, BoonTag.Ability, BoonEffectKind.PhaseCharges, 1f, 2, AbilityId.PhaseStep),
            B("Overclock", BoonId.Overclock, "Overclock", "Overdrive charges 50% faster; hits during Overdrive heal 1 HP.", BoonRarity.Rare, BoonTag.Ability,
                BoonEffectKind.OverdriveCharge, 0.5f, 1, AbilityId.Overdrive, BoonEffectKind.OverdriveLifesteal, 1f),
            B("EchoTwin", BoonId.EchoTwin, "Shadow Twin", "Mirror twin lasts 10 s longer.", BoonRarity.Rare, BoonTag.Ability, BoonEffectKind.MirrorDuration, 10f, 1, AbilityId.Mirror),

            // Bulwark
            B("LacqueredArmor", BoonId.LacqueredArmor, "Lacquered Armor", "-8% damage taken per stack.", BoonRarity.Common, BoonTag.Bulwark, BoonEffectKind.DamageReduction, 0.08f, 3),
            B("LastStand", BoonId.LastStand, "Last Stand", "Below 30% HP: +35% damage and -15% damage taken.", BoonRarity.Rare, BoonTag.Bulwark, BoonEffectKind.LastStand, 0.35f, 1),

            // Curses (a strong upside paid for by a negative secondary)
            B("GlassEdge", BoonId.GlassEdge, "Glass Edge", "+35% blade damage, but -25 max HP.", BoonRarity.Rare, BoonTag.Curse,
                BoonEffectKind.BladeDamageMultiplier, 0.35f, 1, null, BoonEffectKind.MaxHealthAdd, -25f),
            B("BloodPrice", BoonId.BloodPrice, "Coil Tithe", "Heal 8 HP on every kill, but the Forge can no longer mend.", BoonRarity.Rare, BoonTag.Curse,
                BoonEffectKind.HealOnKill, 8f, 1, null, BoonEffectKind.ForgeMend, -1f),
            B("RecklessFlow", BoonId.RecklessFlow, "Reckless Flow", "+12% parry-flow bonus, but any imperfect parry breaks the streak.", BoonRarity.Rare, BoonTag.Curse | BoonTag.Parry,
                BoonEffectKind.ParryFlowBonus, 0.12f, 1, null, BoonEffectKind.RecklessFlow, -1f),
            B("HollowOath", BoonId.HollowOath, "Hollow Oath", "Epic boons are twice as likely, but -20% max HP.", BoonRarity.Rare, BoonTag.Curse,
                BoonEffectKind.EpicWeight, 1f, 1, null, BoonEffectKind.MaxHealthPercent, -0.2f),

            // Duos (offered only once both archetypes are owned)
            B("ThunderRiposte", BoonId.ThunderRiposte, "Thunder Riposte", "Your riposte arcs to every enemy within 4 m for half its damage.", BoonRarity.Duo, BoonTag.Parry | BoonTag.Storm,
                BoonEffectKind.RiposteChain, 4f, 1, AbilityId.Riposte, requiresTags: new[] { "Parry", "Storm" }),
            B("CrimsonTide", BoonId.CrimsonTide, "Crimson Tide", "At combo 3+, bleeds are twice as strong.", BoonRarity.Duo, BoonTag.Bleed | BoonTag.Flow,
                BoonEffectKind.ComboBleed, 1f, 1, requiresTags: new[] { "Bleed", "Flow" }),
            B("GhostStep", BoonId.GhostStep, "Ghost Step", "Phase Step through a strike about to land: it counts as a perfect parry.", BoonRarity.Duo, BoonTag.Ability | BoonTag.Parry,
                BoonEffectKind.GhostStep, 0.25f, 1, AbilityId.PhaseStep, requiresTags: new[] { "Ability", "Parry" }),
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
            boon.tags = spec.Tags;
            boon.requiresTags = spec.RequiresTags;
            boon.hasSecondary = spec.HasSecondary;
            boon.secondaryEffect = spec.SecondaryEffect;
            boon.secondaryMagnitude = spec.SecondaryMagnitude;
            // P5-07: icon by asset name from the processed sprites (Build Boon Icons); keep the old one if missing.
            boon.icon = AssetDatabase.LoadAssetAtPath<Sprite>(
                Ronin7.Editor.Art.BoonIconBuilder.ProcessedFolder + "/Icon_" + spec.AssetName + ".png") ?? boon.icon;

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
