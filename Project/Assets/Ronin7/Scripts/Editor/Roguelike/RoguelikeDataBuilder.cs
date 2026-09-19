using System.Collections.Generic;
using System.Text;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Module 5a — authors the <see cref="EnemySpawnTable"/> (Appendix A's exact prefab -> definition
    /// mapping) and the <see cref="ArenaRoomLibrary"/> (3 biomes: rust/program/garden) at
    /// <c>Assets/Ronin7/Data/Roguelike/</c>. Points at the prefabs <see cref="EnemyPrefabBaker"/> bakes —
    /// run that menu item first, or every entry referencing a missing prefab is skipped with a loud
    /// error rather than shipping a null reference (A5.1 treats a null prefab/definition as ineligible
    /// anyway, so a partially-baked run degrades gracefully instead of crashing this builder).
    ///
    /// Idempotent: re-running finds the existing assets by path and overwrites their fields in place.
    /// </summary>
    public static class RoguelikeDataBuilder
    {
        private const string EnemyPrefabFolder = "Assets/Ronin7/Prefabs/Roguelike/Enemies";
        private const string DataFolder = "Assets/Ronin7/Data";
        private const string RoguelikeDataFolder = DataFolder + "/Roguelike";
        private const string SpawnTablePath = RoguelikeDataFolder + "/EnemySpawnTable.asset";
        private const string RoomLibraryPath = RoguelikeDataFolder + "/ArenaRoomLibrary.asset";

        private struct TrashSpec
        {
            public string PrefabName;
            public string DefinitionName;
            public int Weight;
            public int MinDepth;
            public bool EliteOnly;
        }

        private struct BossSpec
        {
            public string PrefabName;
            public string DefinitionName;
            public int Sector;
        }

        // Appendix A's exact prefab -> definition -> minDepth mapping. Weight favors the lighter/
        // earlier tiers so easy trash stays the common case at low depth while heavy/elite mobs stay
        // rare even once they become eligible (matches the suggested tier column: light > medium/fast
        // > heavy > elite).
        private static readonly TrashSpec[] TrashSpecs =
        {
            new TrashSpec { PrefabName = "Ash-World_Scavenger",       DefinitionName = "Ch7Scavenger",          Weight = 15, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Coil_Syndicate_Ganger",     DefinitionName = "Ch9CoilRaider",         Weight = 15, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Dominion_Trooper",          DefinitionName = "Ch10SyndicateGuard",    Weight = 12, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Program_Operative_Grunt",   DefinitionName = "Bandit",                Weight = 12, MinDepth = 2, EliteOnly = false },
            new TrashSpec { PrefabName = "Hunter_Drone",              DefinitionName = "Ch11GhostManifestation", Weight = 10, MinDepth = 3, EliteOnly = false },
            new TrashSpec { PrefabName = "Dominion_Scan-Drone",       DefinitionName = "Ch13LabSecurity",       Weight = 10, MinDepth = 3, EliteOnly = false },
            new TrashSpec { PrefabName = "Humanoid_Automaton",        DefinitionName = "Ch10MineAutomaton",     Weight = 8,  MinDepth = 5, EliteOnly = false },
            new TrashSpec { PrefabName = "Iron_Dojo_Warden-Cadre",    DefinitionName = "Ch6Caradoc",            Weight = 8,  MinDepth = 5, EliteOnly = false },
            new TrashSpec { PrefabName = "Spectral_Grave-Guardian",   DefinitionName = "Ch8Buried",             Weight = 6,  MinDepth = 6, EliteOnly = true },
            new TrashSpec { PrefabName = "Redaction_Construct",       DefinitionName = "Ch13Redactor",          Weight = 6,  MinDepth = 7, EliteOnly = true },
            new TrashSpec { PrefabName = "Dream_Ghost_Manifestation", DefinitionName = "Ch11DreamingArchive",   Weight = 6,  MinDepth = 8, EliteOnly = true },
        };

        // Appendix A's boss table — the 3 sector-capping bosses (spares Kerrax/Samurai-4/Sever_Ninja-2
        // are not wired here; nothing in the contract calls for them).
        private static readonly BossSpec[] BossSpecs =
        {
            new BossSpec { PrefabName = "The-Warden",    DefinitionName = "Ch8Warden",          Sector = 0 },
            new BossSpec { PrefabName = "Vane_Wraith-6",  DefinitionName = "Ch9Vane",            Sector = 1 },
            new BossSpec { PrefabName = "Maelgorn",       DefinitionName = "Ch16MaelgornPhase1", Sector = 2 },
        };

        [MenuItem("Tools/Space Samurai/Roguelike/Build Spawn Table + Room Library")]
        public static void BuildSpawnTableAndRoomLibrary()
        {
            EnsureFolder(RoguelikeDataFolder);
            BuildSpawnTable();
            BuildRoomLibrary();
            AssetDatabase.SaveAssets();
        }

        private static void BuildSpawnTable()
        {
            var report = new StringBuilder();

            var trash = new List<EnemySpawnTable.Entry>();
            foreach (var spec in TrashSpecs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/" + spec.PrefabName + ".prefab");
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataFolder + "/" + spec.DefinitionName + ".asset");
                if (prefab == null || def == null)
                {
                    Debug.LogError($"[RoguelikeDataBuilder] Trash entry '{spec.PrefabName}' skipped — " +
                        (prefab == null ? "baked prefab missing (run Bake Enemy Prefabs first). " : "") +
                        (def == null ? $"EnemyDefinition '{spec.DefinitionName}' missing." : ""));
                    continue;
                }
                trash.Add(new EnemySpawnTable.Entry
                {
                    prefab = prefab,
                    definition = def,
                    weight = spec.Weight,
                    minDepth = spec.MinDepth,
                    eliteOnly = spec.EliteOnly,
                });
                report.Append(spec.PrefabName).Append(" -> ").Append(spec.DefinitionName)
                      .Append(" weight=").Append(spec.Weight).Append(" minDepth=").Append(spec.MinDepth)
                      .Append(spec.EliteOnly ? " eliteOnly" : "").Append('\n');
            }

            var bosses = new List<EnemySpawnTable.BossEntry>();
            foreach (var spec in BossSpecs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/" + spec.PrefabName + ".prefab");
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataFolder + "/" + spec.DefinitionName + ".asset");
                if (prefab == null || def == null)
                {
                    Debug.LogError($"[RoguelikeDataBuilder] Boss entry '{spec.PrefabName}' (sector {spec.Sector}) skipped — " +
                        (prefab == null ? "baked prefab missing (run Bake Enemy Prefabs first). " : "") +
                        (def == null ? $"EnemyDefinition '{spec.DefinitionName}' missing." : ""));
                    continue;
                }
                bosses.Add(new EnemySpawnTable.BossEntry { prefab = prefab, definition = def, sector = spec.Sector });
                report.Append("Boss sector ").Append(spec.Sector).Append(": ").Append(spec.PrefabName)
                      .Append(" -> ").Append(spec.DefinitionName).Append('\n');
            }

            var table = AssetDatabase.LoadAssetAtPath<EnemySpawnTable>(SpawnTablePath);
            bool isNew = table == null;
            if (isNew) table = ScriptableObject.CreateInstance<EnemySpawnTable>();
            table.trash = trash.ToArray();
            table.bosses = bosses.ToArray();
            if (isNew) AssetDatabase.CreateAsset(table, SpawnTablePath);
            else EditorUtility.SetDirty(table);

            Debug.Log($"[RoguelikeDataBuilder] Spawn table: {trash.Count}/{TrashSpecs.Length} trash entrie(s), " +
                      $"{bosses.Count}/{BossSpecs.Length} boss entrie(s) at {SpawnTablePath}.\n{report}");
        }

        private static void BuildRoomLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ArenaRoomLibrary>(RoomLibraryPath);
            bool isNew = library == null;
            if (isNew) library = ScriptableObject.CreateInstance<ArenaRoomLibrary>();

            // Distinct floor/ceiling/accent per biome; propPrefabs deliberately left empty — the
            // runtime greyboxes when empty (ArenaRoomLibrary.Biome doc), and A5.7 strips MeshColliders
            // from any prop instantiated there regardless, so there's nothing this builder needs to add.
            library.biomes = new[]
            {
                new ArenaRoomLibrary.Biome
                {
                    id = "rust",
                    floorColor = new Color(0.32f, 0.19f, 0.12f),
                    ceilColor = new Color(0.14f, 0.08f, 0.05f),
                    accentColor = new Color(0.95f, 0.45f, 0.15f),
                    propPrefabs = null,
                },
                new ArenaRoomLibrary.Biome
                {
                    id = "program",
                    floorColor = new Color(0.10f, 0.13f, 0.20f),
                    ceilColor = new Color(0.04f, 0.05f, 0.09f),
                    accentColor = new Color(0.25f, 0.75f, 0.95f),
                    propPrefabs = null,
                },
                new ArenaRoomLibrary.Biome
                {
                    id = "garden",
                    floorColor = new Color(0.12f, 0.22f, 0.14f),
                    ceilColor = new Color(0.05f, 0.10f, 0.07f),
                    accentColor = new Color(0.55f, 0.90f, 0.55f),
                    propPrefabs = null,
                },
            };

            if (isNew) AssetDatabase.CreateAsset(library, RoomLibraryPath);
            else EditorUtility.SetDirty(library);

            Debug.Log($"[RoguelikeDataBuilder] Room library: 3 biomes (rust/program/garden) at {RoomLibraryPath}.");
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
