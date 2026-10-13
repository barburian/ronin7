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
        private const string SurfaceMaterialPath = RoguelikeDataFolder + "/ArenaSurface.mat";
        private const string RoguelikeEnemyFolder = RoguelikeDataFolder + "/Enemies";

        private struct TrashSpec
        {
            public string PrefabName;
            public string DefinitionName; // story source asset (cloned into RL_<RlName>)
            public string RlName;
            public int Weight;
            public int MinDepth;
            public bool EliteOnly;
        }

        private struct BossSpec
        {
            public string PrefabName;
            public string DefinitionName; // story source asset (cloned into RL_<RlName>)
            public string RlName;
            public int Sector;
        }

        // Appendix A's exact prefab -> definition -> minDepth mapping. Weight favors the lighter/
        // earlier tiers so easy trash stays the common case at low depth while heavy/elite mobs stay
        // rare even once they become eligible (matches the suggested tier column: light > medium/fast
        // > heavy > elite).
        private static readonly TrashSpec[] TrashSpecs =
        {
            new TrashSpec { PrefabName = "Ash-World_Scavenger",       DefinitionName = "Ch7Scavenger", RlName = "AshScavenger",          Weight = 15, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Coil_Syndicate_Ganger",     DefinitionName = "Ch9CoilRaider", RlName = "CoilGanger",         Weight = 15, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Dominion_Trooper",          DefinitionName = "Ch10SyndicateGuard", RlName = "DominionTrooper",    Weight = 12, MinDepth = 0, EliteOnly = false },
            new TrashSpec { PrefabName = "Program_Operative_Grunt",   DefinitionName = "Bandit", RlName = "ProgramGrunt",                Weight = 12, MinDepth = 2, EliteOnly = false },
            new TrashSpec { PrefabName = "Hunter_Drone",              DefinitionName = "Ch11GhostManifestation", RlName = "HunterDrone", Weight = 10, MinDepth = 3, EliteOnly = false },
            new TrashSpec { PrefabName = "Dominion_Scan-Drone",       DefinitionName = "Ch13LabSecurity", RlName = "ScanDrone",       Weight = 10, MinDepth = 3, EliteOnly = false },
            new TrashSpec { PrefabName = "Humanoid_Automaton",        DefinitionName = "Ch10MineAutomaton", RlName = "HumanoidAutomaton",     Weight = 8,  MinDepth = 5, EliteOnly = false },
            new TrashSpec { PrefabName = "Iron_Dojo_Warden-Cadre",    DefinitionName = "Ch6Caradoc", RlName = "IronWarden",            Weight = 8,  MinDepth = 5, EliteOnly = false },
            new TrashSpec { PrefabName = "Spectral_Grave-Guardian",   DefinitionName = "Ch8Buried", RlName = "GraveGuardian", Weight = 6,  MinDepth = 6, EliteOnly = true },
            new TrashSpec { PrefabName = "Redaction_Construct",       DefinitionName = "Ch13Redactor", RlName = "RedactionConstruct",          Weight = 6,  MinDepth = 7, EliteOnly = true },
            new TrashSpec { PrefabName = "Dream_Ghost_Manifestation", DefinitionName = "Ch11GhostManifestation", RlName = "DreamGhost", Weight = 6,  MinDepth = 8, EliteOnly = true },
        };

        // Appendix A's boss table — the 3 sector-capping bosses (spares Kerrax/Samurai-4/Sever_Ninja-2
        // are not wired here; nothing in the contract calls for them).
        private static readonly BossSpec[] BossSpecs =
        {
            new BossSpec { PrefabName = "The-Warden",    DefinitionName = "Ch8Warden", RlName = "Warden",          Sector = 0 },
            new BossSpec { PrefabName = "Vane_Wraith-6",  DefinitionName = "Ch9Vane", RlName = "VaneWraith",            Sector = 1 },
            new BossSpec { PrefabName = "Maelgorn",       DefinitionName = "Ch16MaelgornPhase1", RlName = "Maelgorn", Sector = 2 },
        };

        [MenuItem("Tools/Space Samurai/Roguelike/Build Spawn Table + Room Library")]
        public static void BuildSpawnTableAndRoomLibrary()
        {
            EnsureFolder(RoguelikeDataFolder);
            BuildRoguelikeDefinitions();
            BuildSpawnTable();
            BuildRoomLibrary();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// P1-02: every spawn-table enemy gets its own RL_ clone of its story definition, so the
        /// roguelike can tune without touching chapter assets. BUG-08: the Dream Ghost and Grave
        /// Guardian sources are not real fighters (Ch11DreamingArchive-style stubs), so they carry
        /// overrides. Re-running re-derives each clone from its source and re-applies the overrides.
        /// </summary>
        private static void BuildRoguelikeDefinitions()
        {
            EnsureFolder(RoguelikeEnemyFolder);
            foreach (var t in TrashSpecs) EnsureClone(t.DefinitionName, "RL_" + t.RlName, Overrides(t.RlName));
            foreach (var b in BossSpecs) EnsureClone(b.DefinitionName, "RL_" + b.RlName, Overrides(b.RlName));
        }

        private static System.Action<EnemyDefinition> Overrides(string rlName)
        {
            switch (rlName)
            {
                case "DreamGhost":
                    return d => { d.maxHealth = 90f; d.damage = 12f; d.moveSpeed = 1.4f; d.attackCooldown = 1.0f; };
                case "GraveGuardian":
                    return d => d.maxHealth = 120f;
                // BUG-11: heavy types keep their windup when hit.
                case "HumanoidAutomaton":
                case "IronWarden":
                case "Warden":
                case "VaneWraith":
                case "Maelgorn":
                    return d => d.superArmor = true;
                default:
                    return d => { };
            }
        }

        private static void EnsureClone(string sourceName, string cloneName, System.Action<EnemyDefinition> tune)
        {
            var source = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DataFolder + "/" + sourceName + ".asset");
            if (source == null)
            {
                Debug.LogError($"[RoguelikeDataBuilder] Source definition '{sourceName}' missing; cannot build {cloneName}.");
                return;
            }
            string path = RoguelikeEnemyFolder + "/" + cloneName + ".asset";
            var clone = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (clone == null)
            {
                clone = Object.Instantiate(source);
                tune(clone);
                AssetDatabase.CreateAsset(clone, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, clone);
                tune(clone);
                EditorUtility.SetDirty(clone);
            }
            clone.name = cloneName;
        }

        private static void BuildSpawnTable()
        {
            var report = new StringBuilder();

            var trash = new List<EnemySpawnTable.Entry>();
            foreach (var spec in TrashSpecs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/" + spec.PrefabName + ".prefab");
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RoguelikeEnemyFolder + "/RL_" + spec.RlName + ".asset");
                if (prefab == null || def == null)
                {
                    Debug.LogError($"[RoguelikeDataBuilder] Trash entry '{spec.PrefabName}' skipped — " +
                        (prefab == null ? "baked prefab missing (run Bake Enemy Prefabs first). " : "") +
                        (def == null ? $"EnemyDefinition 'RL_{spec.RlName}' missing." : ""));
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
                report.Append(spec.PrefabName).Append(" -> RL_").Append(spec.RlName)
                      .Append(" weight=").Append(spec.Weight).Append(" minDepth=").Append(spec.MinDepth)
                      .Append(spec.EliteOnly ? " eliteOnly" : "").Append('\n');
            }

            var bosses = new List<EnemySpawnTable.BossEntry>();
            foreach (var spec in BossSpecs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabFolder + "/" + spec.PrefabName + ".prefab");
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RoguelikeEnemyFolder + "/RL_" + spec.RlName + ".asset");
                if (prefab == null || def == null)
                {
                    Debug.LogError($"[RoguelikeDataBuilder] Boss entry '{spec.PrefabName}' (sector {spec.Sector}) skipped — " +
                        (prefab == null ? "baked prefab missing (run Bake Enemy Prefabs first). " : "") +
                        (def == null ? $"EnemyDefinition 'RL_{spec.RlName}' missing." : ""));
                    continue;
                }
                bosses.Add(new EnemySpawnTable.BossEntry { prefab = prefab, definition = def, sector = spec.Sector });
                report.Append("Boss sector ").Append(spec.Sector).Append(": ").Append(spec.PrefabName)
                      .Append(" -> RL_").Append(spec.RlName).Append('\n');
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
            library.surfaceMaterial = EnsureSurfaceMaterial();

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

        /// <summary>Shipped URP material for the runtime-built room (a player build has no default
        /// primitive material -> magenta). Editor-time Shader.Find is fine; the asset keeps the shader
        /// referenced in the build. Idempotent: an existing asset is reused untouched.</summary>
        private static Material EnsureSurfaceMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
            {
                Debug.LogError("[RoguelikeDataBuilder] 'Universal Render Pipeline/Simple Lit' shader not found; ArenaSurface.mat not created.");
                return null;
            }
            mat = new Material(shader) { name = "ArenaSurface" };
            mat.SetColor("_BaseColor", Color.white); // RendererTint MPB overrides per renderer
            AssetDatabase.CreateAsset(mat, SurfaceMaterialPath);
            return mat;
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
