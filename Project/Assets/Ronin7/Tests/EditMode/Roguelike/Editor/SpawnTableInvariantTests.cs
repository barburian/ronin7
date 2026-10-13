using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Editor
{
    /// <summary>
    /// Invariants over the SHIPPED <see cref="EnemySpawnTable"/> asset and the baked enemy prefabs it
    /// (and <c>EnemyPrefabBaker</c>) produce. Both are authored by menu items that need a live editor
    /// ("Tools/Space Samurai/Roguelike/Bake Enemy Prefabs" then ".../Build Spawn Table + Room Library"),
    /// so every test here skips gracefully via <see cref="Assert.Ignore"/> when the relevant asset(s)
    /// don't exist yet rather than failing the 842-green baseline on a fresh checkout.
    /// </summary>
    public class SpawnTableInvariantTests
    {
        private const string SpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";
        private const string BakedEnemyFolder = "Assets/Ronin7/Prefabs/Roguelike/Enemies";

        private static EnemySpawnTable LoadTableOrSkip()
        {
            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            if (table == null || table.trash == null || table.trash.Length == 0)
            {
                Assert.Ignore($"{SpawnTablePath} not built yet — run 'Tools/Space Samurai/Roguelike/" +
                              "Build Spawn Table + Room Library' in a live editor first.");
            }
            return table;
        }

        [Test]
        public void EveryTrashEntry_HasNonNullPrefabAndDefinitionAndPositiveWeight()
        {
            var table = LoadTableOrSkip();
            foreach (var entry in table.trash)
            {
                Assert.IsNotNull(entry, "EnemySpawnTable.trash contains a null entry.");
                Assert.IsNotNull(entry.prefab, "A trash entry has a null prefab.");
                Assert.IsNotNull(entry.definition, $"Trash entry '{entry.prefab.name}' has a null definition.");
                Assert.Greater(entry.weight, 0, $"Trash entry '{entry.prefab.name}' has weight <= 0.");
            }
        }

        [Test]
        public void EveryBossEntry_HasNonNullPrefabAndDefinition()
        {
            var table = LoadTableOrSkip();
            if (table.bosses == null || table.bosses.Length == 0)
                Assert.Ignore($"{SpawnTablePath} has no boss entries yet.");

            foreach (var entry in table.bosses)
            {
                Assert.IsNotNull(entry, "EnemySpawnTable.bosses contains a null entry.");
                Assert.IsNotNull(entry.prefab, $"Boss entry for sector {entry?.sector} has a null prefab.");
                Assert.IsNotNull(entry.definition, $"Boss entry '{entry.prefab.name}' has a null definition.");
            }
        }

        [Test]
        public void EverySectorZeroToTwo_HasAtLeastOneBoss()
        {
            var table = LoadTableOrSkip();
            if (table.bosses == null || table.bosses.Length == 0)
                Assert.Ignore($"{SpawnTablePath} has no boss entries yet.");

            var sectorsPresent = new HashSet<int>();
            foreach (var entry in table.bosses)
            {
                if (entry != null && entry.prefab != null) sectorsPresent.Add(entry.sector);
            }

            for (int sector = 0; sector <= 2; sector++)
            {
                Assert.IsTrue(sectorsPresent.Contains(sector), $"Sector {sector} has no usable boss entry.");
            }
        }

        [Test]
        public void EveryReferencedPrefab_HasHealthAndEnemyComponents()
        {
            var table = LoadTableOrSkip();
            var checked_ = new HashSet<GameObject>();

            foreach (var entry in table.trash)
            {
                if (entry?.prefab == null || !checked_.Add(entry.prefab)) continue;
                AssertHasHealthAndEnemy(entry.prefab);
            }

            if (table.bosses != null)
            {
                foreach (var entry in table.bosses)
                {
                    if (entry?.prefab == null || !checked_.Add(entry.prefab)) continue;
                    AssertHasHealthAndEnemy(entry.prefab);
                }
            }
        }

        private static void AssertHasHealthAndEnemy(GameObject prefab)
        {
            Assert.IsNotNull(prefab.GetComponent<Ronin7.Combat.Health>(), $"'{prefab.name}' is missing a Health component.");
            Assert.IsNotNull(prefab.GetComponent<Enemy>(), $"'{prefab.name}' is missing an Enemy component.");
        }

        [Test]
        public void EveryBakedEnemyPrefab_RootIsInactive()
        {
            if (!AssetDatabase.IsValidFolder(BakedEnemyFolder))
            {
                Assert.Ignore($"{BakedEnemyFolder} not built yet — run 'Tools/Space Samurai/Roguelike/" +
                              "Bake Enemy Prefabs' in a live editor first.");
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { BakedEnemyFolder });
            if (guids.Length == 0)
                Assert.Ignore($"{BakedEnemyFolder} contains no baked prefabs yet.");

            // A3.1, load-bearing: Unity skips Awake on an instantiated inactive root, which is the
            // window the roguelike spawner needs to call Enemy.Configure(def) before Awake reads it.
            // A root saved active would silently revert difficulty scaling and posture thresholds to
            // unscaled base stats at every depth — assert it, don't just document it.
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Could not load prefab at {path}.");
                Assert.IsFalse(prefab.activeSelf, $"'{prefab.name}' root must be saved INACTIVE (A3.1) but is active.");
            }
        }

        [Test]
        public void EveryBakedEnemyPrefab_StandsOnItsOwnPivot_AtHumanHeight()
        {
            if (!AssetDatabase.IsValidFolder(BakedEnemyFolder))
            {
                Assert.Ignore($"{BakedEnemyFolder} not built yet — run 'Tools/Space Samurai/Roguelike/" +
                              "Bake Enemy Prefabs' in a live editor first.");
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { BakedEnemyFolder });
            if (guids.Length == 0)
                Assert.Ignore($"{BakedEnemyFolder} contains no baked prefabs yet.");

            // The regression this guards: EnemyPrefabBaker used to ground the art by lifting the
            // prefab ROOT (~half the body height, because the Tripo meshes pivot at their bounds
            // centre). RunArenaController assigns the root's localPosition wholesale at spawn
            // (arena-local, y=0), so that lift was discarded and every enemy stood half-sunk through
            // the floor with its capsule floating above the visible body. Root pivot = feet, and the
            // body is TargetEnemyHeight (1.8 m) tall — the capsule baked at centre 0.9 assumes both.
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Could not load prefab at {path}.");
                Assert.AreEqual(0f, prefab.transform.localPosition.y, 0.001f,
                    $"'{prefab.name}' root is offset in y — the spawner overwrites that, so the " +
                    "grounding offset belongs on the children, not the root.");

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    instance.SetActive(true); // baked inactive (A3.1); renderers need to be live to measure
                    instance.transform.position = Vector3.zero;

                    var rends = instance.GetComponentsInChildren<Renderer>(true);
                    Assert.Greater(rends.Length, 0, $"'{prefab.name}' has no renderers to measure.");
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                    Assert.AreEqual(0f, b.min.y, 0.02f,
                        $"'{prefab.name}' feet must sit on the root's pivot (y=0), but bounds.min.y is {b.min.y:F3}.");
                    Assert.AreEqual(1.8f, b.size.y, 0.05f,
                        $"'{prefab.name}' must be normalised to adult human height, but is {b.size.y:F3} m tall.");
                }
                finally
                {
                    Object.DestroyImmediate(instance); // A5.2: Destroy is illegal in edit mode
                }
            }
        }

        // ---- BUG-08: every spawnable definition must actually be able to fight ----

        [Test]
        public void EveryDefinition_IsCombatViable()
        {
            var table = LoadTableOrSkip();
            var definitions = new List<(string name, EnemyDefinition def)>();
            foreach (var e in table.trash) if (e != null) definitions.Add((e.prefab != null ? e.prefab.name : "?", e.definition));
            if (table.bosses != null)
                foreach (var b in table.bosses) if (b != null) definitions.Add((b.prefab != null ? b.prefab.name : "?", b.definition));

            foreach (var (name, def) in definitions)
            {
                Assert.IsNotNull(def, $"'{name}' has no definition.");
                Assert.Greater(def.damage, 0f, $"'{name}' ({def.name}) deals no damage.");
                Assert.Greater(def.moveSpeed, 0f, $"'{name}' ({def.name}) cannot move.");
                Assert.GreaterOrEqual(def.maxHealth, 20f, $"'{name}' ({def.name}) is a free kill.");
                Assert.Less(def.attackCooldown, 5f, $"'{name}' ({def.name}) effectively never attacks.");
            }
        }

        [Test]
        public void EveryEliteOnlyEntry_IsTougherThanDepth0Trash()
        {
            var table = LoadTableOrSkip();
            float toughestDepth0 = 0f;
            foreach (var e in table.trash)
                if (e != null && e.definition != null && !e.eliteOnly && e.minDepth == 0)
                    toughestDepth0 = Mathf.Max(toughestDepth0, e.definition.maxHealth);
            if (toughestDepth0 <= 0f) Assert.Ignore("No depth-0 trash in the table.");

            foreach (var e in table.trash)
            {
                if (e == null || e.definition == null || !e.eliteOnly) continue;
                Assert.Greater(e.definition.maxHealth, toughestDepth0,
                    $"Elite-only '{e.prefab.name}' ({e.definition.name}, {e.definition.maxHealth} HP) is no tougher than depth-0 trash ({toughestDepth0} HP).");
            }
        }

        // ---- BUG-10 / P1-10: boss total HP and damage must rise by sector ----

        [Test]
        public void BossTotalHealth_IsMonotonicBySector()
        {
            var table = LoadTableOrSkip();
            if (table.bosses == null || table.bosses.Length < 3) Assert.Ignore("Spawn table lacks 3 boss entries.");

            float prevHp = 0f, prevDmg = 0f;
            for (int sector = 0; sector <= 2; sector++)
            {
                EnemySpawnTable.BossEntry boss = System.Array.Find(table.bosses, b => b != null && b.sector == sector);
                Assert.IsNotNull(boss, $"No boss for sector {sector}.");
                // Boss node = last node of its sector, exactly as RunMapGenerator lays it out.
                int depth = sector * Ronin7.Core.RunMapGenerator.NodesPerSector + Ronin7.Core.RunMapGenerator.NodesPerSector - 1;
                RunArenaController.ScaledStats(boss.definition.maxHealth, boss.definition.damage, depth,
                    Ronin7.Core.RoomKind.Boss, sector, out float hp, out float dmg);

                Assert.Greater(hp, prevHp, $"Boss HP not increasing: sector {sector} ({boss.definition.name}) = {hp:F0} <= previous {prevHp:F0}.");
                Assert.Greater(dmg, prevDmg, $"Boss damage not increasing: sector {sector} ({boss.definition.name}) = {dmg:F1} <= previous {prevDmg:F1}.");
                prevHp = hp;
                prevDmg = dmg;
            }
        }

        private static IEnumerable<EnemyDefinition> TableDefinitions(EnemySpawnTable table)
        {
            foreach (var e in table.trash) yield return e.definition;
            if (table.bosses != null) foreach (var b in table.bosses) yield return b.definition;
        }

        [Test]
        public void EveryDefinition_TelegraphAtLeast055()
        {
            foreach (var d in TableDefinitions(LoadTableOrSkip()))
                Assert.GreaterOrEqual(d.telegraphTime, 0.55f, $"{d.name} telegraph is unreadable in VR.");
        }

        [Test]
        public void RL_Definitions_DoNotReferenceStoryAssets()
        {
            const string Folder = "Assets/Ronin7/Data/Roguelike/Enemies";
            var table = LoadTableOrSkip();
            int count = 0;
            foreach (var d in TableDefinitions(table))
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.IsTrue(path.StartsWith(Folder + "/RL_"), $"Spawn table points at non-RL asset {path}.");
                count++;
            }
            Assert.AreEqual(14, count, "Expected 11 trash + 3 boss definitions.");

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var dep in AssetDatabase.GetDependencies(path, true))
                    Assert.IsFalse(System.IO.Path.GetFileName(dep).StartsWith("Ch"), $"{path} depends on story asset {dep}.");
            }
        }
    }
}
