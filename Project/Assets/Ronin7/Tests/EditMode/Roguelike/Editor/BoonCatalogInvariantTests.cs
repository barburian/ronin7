using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEditor;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>
    /// A1.8: invariants over the SHIPPED <see cref="BoonCatalog"/> asset authored by
    /// <c>BoonCatalogBuilder</c> — a mis-authored field here costs the player a run-defining pick for
    /// nothing, so these are gates, not conventions. The catalog only exists after a human has run
    /// "Tools/Space Samurai/Roguelike/Build Boon Catalog" in a live editor (this class cannot run that
    /// menu item itself — no AssetDatabase write access from a fresh checkout's test gate), so every
    /// test skips gracefully via <see cref="Assert.Ignore"/> when the asset is absent rather than
    /// failing the 842-green baseline on a fresh checkout.
    /// </summary>
    public class BoonCatalogInvariantTests
    {
        private const string CatalogPath = "Assets/Ronin7/Data/Boons/BoonCatalog.asset";

        private static BoonCatalog LoadCatalogOrSkip()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoonCatalog>(CatalogPath);
            if (catalog == null || catalog.boons == null || catalog.boons.Length == 0)
            {
                Assert.Ignore($"{CatalogPath} not built yet — run 'Tools/Space Samurai/Roguelike/" +
                              "Build Boon Catalog' in a live editor first.");
            }
            return catalog;
        }

        private static IEnumerable<string> AllConstants(System.Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                    yield return (string)field.GetValue(null);
            }
        }

        [Test]
        public void EveryBoon_HasDistinctNonEmptyId()
        {
            var catalog = LoadCatalogOrSkip();
            var seen = new HashSet<string>();
            foreach (var boon in catalog.boons)
            {
                Assert.IsNotNull(boon, "BoonCatalog.boons contains a null entry.");
                Assert.IsFalse(string.IsNullOrEmpty(boon.id), $"'{boon.name}' has an empty id.");
                Assert.IsTrue(seen.Add(boon.id), $"Duplicate boon id: {boon.id}");
            }
        }

        [Test]
        public void EveryBoonIdConstant_AppearsExactlyOnceInTheCatalog()
        {
            var catalog = LoadCatalogOrSkip();
            var idCounts = new Dictionary<string, int>();
            foreach (var boon in catalog.boons)
            {
                if (boon == null || string.IsNullOrEmpty(boon.id)) continue;
                idCounts.TryGetValue(boon.id, out int count);
                idCounts[boon.id] = count + 1;
            }

            foreach (var idConst in AllConstants(typeof(BoonId)))
            {
                Assert.IsTrue(idCounts.TryGetValue(idConst, out int count),
                    $"BoonId constant '{idConst}' is missing from the catalog.");
                Assert.AreEqual(1, count, $"BoonId constant '{idConst}' appears {count} times, expected exactly 1.");
            }
        }

        [Test]
        public void EveryGrantAbilityOrReviveOnceBoon_HasMaxStacksOfOne()
        {
            var catalog = LoadCatalogOrSkip();
            foreach (var boon in catalog.boons)
            {
                if (boon == null) continue;
                if (boon.effect == BoonEffectKind.GrantAbility || boon.effect == BoonEffectKind.ReviveOnce)
                {
                    Assert.AreEqual(1, boon.maxStacks,
                        $"'{boon.id}' ({boon.effect}) must have maxStacks == 1.");
                }
            }
        }

        [Test]
        public void EveryGrantAbilityBoon_HasAbilityIdMatchingAnAbilityIdConstant()
        {
            var catalog = LoadCatalogOrSkip();
            var validAbilityIds = new HashSet<string>(AllConstants(typeof(AbilityId)));

            foreach (var boon in catalog.boons)
            {
                if (boon == null || boon.effect != BoonEffectKind.GrantAbility) continue;
                Assert.IsFalse(string.IsNullOrEmpty(boon.abilityId), $"'{boon.id}' is GrantAbility but abilityId is empty.");
                Assert.IsTrue(validAbilityIds.Contains(boon.abilityId),
                    $"'{boon.id}'.abilityId ('{boon.abilityId}') does not match any AbilityId constant.");
            }
        }

        [Test]
        public void Ironskin_HasTheDocumentedMetaCoupledMagnitudeAndCap()
        {
            var catalog = LoadCatalogOrSkip();
            var ironskin = catalog.Find(BoonId.Ironskin);
            if (ironskin == null) Assert.Ignore($"'{BoonId.Ironskin}' not present in the catalog.");

            // A6.3: MetaUpgradeId.StartingHealth promises "+10 max HP per level" and reaches level 5;
            // A7.6: maxStacks must clear that (plus in-run headroom) or the boon drops out of the offer
            // pool once the meta upgrade is bought out.
            Assert.AreEqual(10f, ironskin.magnitude, 0.0001f, "Ironskin.magnitude must be 10.");
            Assert.AreEqual(8, ironskin.maxStacks, "Ironskin.maxStacks must be 8.");
        }

        [Test]
        public void KeenEdge_HasTheDocumentedMetaCoupledMagnitudeAndCap()
        {
            var catalog = LoadCatalogOrSkip();
            var keenEdge = catalog.Find(BoonId.KeenEdge);
            if (keenEdge == null) Assert.Ignore($"'{BoonId.KeenEdge}' not present in the catalog.");

            // A6.3/A7.6: MetaUpgradeId.StartingDamage promises "+5% blade damage per level" to level 5.
            Assert.AreEqual(0.05f, keenEdge.magnitude, 0.0001f, "KeenEdge.magnitude must be 0.05.");
            Assert.AreEqual(8, keenEdge.maxStacks, "KeenEdge.maxStacks must be 8.");
        }
    }
}
