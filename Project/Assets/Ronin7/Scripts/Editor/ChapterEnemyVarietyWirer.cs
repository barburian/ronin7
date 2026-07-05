using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Applies the Ch7 gang-war pocket and the Ch12 Sentinel-Duelist/HiveCascade squad additively to the
    /// already-shipped Chapter 7 and Chapter 12 scenes. Both scenes were built before these mechanics were
    /// wired in, and the project's chapter Build menu items destructively NewScene-rebuild — never to be
    /// re-run against a shipped scene — so this utility opens each scene directly and adds the new content
    /// in place instead (mirrors <see cref="ChapterVoiceWirer"/>'s "open -> modify -> save" idiom).
    ///
    /// Both ChapterNBuilder sources already carry the identical construction
    /// (<c>Ch7BuildGangWarPocket</c> / <c>Ch12UpgradeToSentinelDuelist</c> / <c>Ch12BuildHiveCascade</c>),
    /// which this utility calls directly (it lives in the same <see cref="XRRigBuilder"/> partial class),
    /// so a future rebuild of either scene reproduces this exactly.
    /// </summary>
    public static partial class XRRigBuilder
    {
        [MenuItem("Tools/Space Samurai/Chapters/Wire Enemy Variety Into Ch7 + Ch12 Scenes")]
        public static void WireEnemyVarietyIntoScenes()
        {
            WireCh7GangWarIntoScene();
            WireCh12SentinelSquadIntoScene();
        }

        /// <summary>Second variety wave (cycle 9): Ch10 tier-4 gang war + Ch13 sterile hive cascade,
        /// same additive open→build→save idiom as the Ch7/Ch12 pass above.</summary>
        [MenuItem("Tools/Space Samurai/Chapters/Wire Enemy Variety Into Ch10 + Ch13 Scenes")]
        public static void WireEnemyVarietyIntoCh10Ch13Scenes()
        {
            WireCh10GangWarIntoScene();
            WireCh13HiveCascadeIntoScene();
        }

        private static void WireCh10GangWarIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(Ch10ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("Tier4GangWarSpawner") != null)
            {
                Debug.Log("[ChapterEnemyVarietyWirer] Ch10: gang war already wired — skipping.");
                return;
            }
            Ch10BuildGangWarPocket();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ChapterEnemyVarietyWirer] Ch10: tier-4 gang war wired (6 brawlers + proximity spawner).");
        }

        private static void WireCh13HiveCascadeIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(Ch13ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("SterileHiveCascade") != null)
            {
                Debug.Log("[ChapterEnemyVarietyWirer] Ch13: hive cascade already wired — skipping.");
                return;
            }

            // The lab-security trio's authored positions (Chapter13Builder.labSecurityPositions).
            Vector3[] squadPositions = { new Vector3(-3f, 0f, 22f), new Vector3(3f, 0f, 22f), new Vector3(0f, 0f, 27f) };
            var squad = new List<MeleeAttacker>();
            foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include))
            {
                foreach (var pos in squadPositions)
                {
                    if ((enemy.transform.position - pos).sqrMagnitude < 0.01f)
                    {
                        squad.Add(enemy);
                        break;
                    }
                }
            }
            if (squad.Count != 3)
            {
                Debug.LogError($"[ChapterEnemyVarietyWirer] Ch13: expected 3 lab-security enemies at the known positions, found {squad.Count} — aborting.");
                return;
            }

            Ch13BuildHiveCascade(squad);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ChapterEnemyVarietyWirer] Ch13: sterile hive cascade wired over the lab-security trio.");
        }

        private static void WireCh7GangWarIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(Ch7ScenePath, OpenSceneMode.Single);

            var spawnerGo = GameObject.Find("OuterStacksWaveSpawner");
            if (spawnerGo == null)
            {
                Debug.LogError("[ChapterEnemyVarietyWirer] Ch7: 'OuterStacksWaveSpawner' not found — aborting.");
                return;
            }
            var spawner = spawnerGo.GetComponent<EnemyWaveSpawner>();
            var so = new SerializedObject(spawner);
            var wavesProp = so.FindProperty("waves");
            if (wavesProp.arraySize >= 3)
            {
                Debug.Log("[ChapterEnemyVarietyWirer] Ch7: gang-war wave already wired — skipping.");
            }
            else
            {
                var gangWarHealths = Ch7BuildGangWarPocket();
                wavesProp.arraySize = 3;
                var enemiesProp = wavesProp.GetArrayElementAtIndex(2).FindPropertyRelative("enemies");
                enemiesProp.arraySize = gangWarHealths.Count;
                for (int i = 0; i < gangWarHealths.Count; i++)
                    enemiesProp.GetArrayElementAtIndex(i).objectReferenceValue = gangWarHealths[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[ChapterEnemyVarietyWirer] Ch7: gang-war pocket wired ({gangWarHealths.Count} combatants).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void WireCh12SentinelSquadIntoScene()
        {
            var scene = EditorSceneManager.OpenScene(Ch12ScenePath, OpenSceneMode.Single);

            if (GameObject.Find("CradleHiveCascade") != null)
            {
                Debug.Log("[ChapterEnemyVarietyWirer] Ch12: hive cascade already wired — skipping.");
                EditorSceneManager.SaveScene(scene);
                return;
            }

            var sentinelDuelistDef = Ch12EnsureSentinelDuelistDefinition();

            // Same tier center + offsets BuildChapter12TheFracture uses for the Tier-2 skirmish trio.
            Vector3 tier2Center = new Vector3(-2f, -3f, 42f);
            Vector3[] squadOffsets = { new Vector3(-3f, 0f, 4f), new Vector3(3f, 0f, 4f), new Vector3(0f, 0f, -3f) };
            Vector3 sentinelPos = tier2Center + squadOffsets[squadOffsets.Length - 1];

            var squad = new List<MeleeAttacker>();
            Enemy sentinelEnemy = null;
            foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include))
            {
                foreach (var offset in squadOffsets)
                {
                    if ((enemy.transform.position - (tier2Center + offset)).sqrMagnitude < 0.01f)
                    {
                        squad.Add(enemy);
                        if ((enemy.transform.position - sentinelPos).sqrMagnitude < 0.01f)
                            sentinelEnemy = enemy;
                        break;
                    }
                }
            }

            if (squad.Count != 3 || sentinelEnemy == null)
            {
                Debug.LogError($"[ChapterEnemyVarietyWirer] Ch12: expected 3 Tier-2 skirmish enemies at the known " +
                                $"positions, found {squad.Count} — aborting.");
                return;
            }

            Ch12UpgradeToSentinelDuelist(sentinelEnemy, sentinelDuelistDef);
            Ch12BuildHiveCascade(squad);
            Debug.Log("[ChapterEnemyVarietyWirer] Ch12: Sentinel Duelist + HiveCascade squad wired.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
