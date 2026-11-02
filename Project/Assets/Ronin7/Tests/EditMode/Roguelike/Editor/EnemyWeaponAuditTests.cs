using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>P8-01 (BUG-54): every spawnable enemy that is not <c>naturalWeapon</c> holds a visible weapon
    /// with its blade tip wired; natural ones (beasts, wraiths, drones) are flagged on their definition.</summary>
    public class EnemyWeaponAuditTests
    {
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";

        private static void Check(GameObject prefab, EnemyDefinition def, System.Collections.Generic.List<string> bad)
        {
            if (prefab == null || def == null || def.naturalWeapon) return;
            var enemy = prefab.GetComponent<Enemy>();
            var so = new SerializedObject(enemy);
            var tip = so.FindProperty("bladeTip").objectReferenceValue as Transform;
            var weapon = so.FindProperty("weapon").objectReferenceValue as Transform;
            if (tip == null || weapon == null || EnemySwordVisual.FindBladeRenderer(weapon) == null)
                bad.Add($"{prefab.name} ({def.name}): tip={(tip != null)} visibleBlade={(weapon != null && EnemySwordVisual.FindBladeRenderer(weapon) != null)}");
        }

        [Test]
        public void EveryNonNaturalEnemy_HoldsAWeaponWithBladeTip()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            var bad = new System.Collections.Generic.List<string>();
            foreach (var e in table.trash) Check(e.prefab, e.definition, bad);
            foreach (var b in table.bosses) Check(b.prefab, b.definition, bad);
            Assert.IsEmpty(bad, "Unarmed humanoids:\n" + string.Join("\n", bad));
        }

        [Test]
        public void NaturalFlag_OnBeastsWraithsAndDrones_Only()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null) Assert.Ignore("Spawn table not built.");
            var natural = new System.Collections.Generic.List<string>();
            foreach (var e in table.trash) if (e.definition.naturalWeapon) natural.Add(e.prefab.name);
            foreach (var b in table.bosses) if (b.definition.naturalWeapon) natural.Add(b.prefab.name);
            CollectionAssert.AreEquivalent(new[] { "Hunter_Drone", "Dominion_Scan-Drone", "Redaction_Construct",
                "Spectral_Grave-Guardian", "Dream_Ghost_Manifestation", "Vane_Wraith-6" }, natural);
        }
    }
}
