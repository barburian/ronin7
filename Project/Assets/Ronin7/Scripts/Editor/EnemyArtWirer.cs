using System.Collections.Generic;
using System.Text;
using Ronin7.Combat;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Additively swaps the greybox capsule visual on every shipped-scene combat enemy for the new
    /// rigged Tripo enemy art (Characters3D/Enemies/, rigged by <see cref="Ronin7.Editor.Art.NpcAutoRigger"/>)
    /// and ensures each one carries a <see cref="NpcWalkAnimator"/> so its legs swing while it chases.
    ///
    /// Purely cosmetic + locomotion: the old body GameObject, its collider, the ArmR/Sword/Blade/BladeTip
    /// weapon rig, Health, faction, spawn-active state and all <see cref="Enemy"/>/<see cref="FactionCombatant"/>
    /// serialized wiring are left untouched — only the old MeshRenderer is disabled and a "Visual_Enemy"
    /// child prefab instance is added. Idempotent (skips anything that already has a Visual_Enemy child or a
    /// SkinnedMeshRenderer, e.g. the Ch9/Ch11 Named-mesh bosses, which only get the walk animator).
    ///
    /// The project's chapter Build menu items destructively NewScene-rebuild, so — like
    /// <see cref="ChapterVoiceWirer"/> and the enemy-variety wirer — this opens each shipped scene directly
    /// and patches it in place. Re-run this after any (never-recommended) chapter rebuild to restore the art.
    /// </summary>
    public static class EnemyArtWirer
    {
        private const string EnemyFolder = "Assets/Ronin7/Art/Generated/Characters3D/Enemies/";
        private const string SceneFolder = "Assets/Ronin7/Scenes/";

        // Thematic scene -> (primary enemy, secondary enemy) mapping. Secondary is used for
        // FactionCombatant faction 1 and for odd-indexed Enemy instances so a scene fields two
        // enemy types; null means "primary only". Together these place all 11 enemy prefabs.
        private static readonly (string scene, string primary, string secondary)[] Map =
        {
            ("Galaxy1_Ch1_Hub",       "Dominion_Trooper",        null),
            ("Ch02_Auction",          "Coil_Syndicate_Ganger",   null),
            ("Ch04_OverseersHunt",    "Dominion_Trooper",        "Dominion_Scan-Drone"),
            ("Ch06_IronDojo",         "Iron_Dojo_Warden-Cadre",  null),
            ("Ch07_ForgottenNames",   "Ash-World_Scavenger",     "Hunter_Drone"),
            ("Ch08_SilentGarden",     "Spectral_Grave-Guardian", null),
            ("Ch09_PitAndTheDeep",    "Ash-World_Scavenger",     null),
            ("Ch10_LedgerOfRust",     "Coil_Syndicate_Ganger",   "Ash-World_Scavenger"),
            ("Ch11_GhostsAndOrigins", "Dream_Ghost_Manifestation", null),
            ("Ch12_TheFracture",      "Dominion_Trooper",        "Redaction_Construct"),
            ("Ch13_SterileReckoning", "Redaction_Construct",     "Humanoid_Automaton"),
            ("Ch16_ThroneOfAshes",    "Program_Operative_Grunt", null),
        };

        [MenuItem("Tools/Space Samurai/Chapters/Wire Enemy Art Into Scenes")]
        public static void WireEnemyArtIntoScenes()
        {
            var report = new StringBuilder();
            int totalSwapped = 0, totalBossWalk = 0, totalSkipped = 0;

            foreach (var (scene, primaryName, secondaryName) in Map)
            {
                string scenePath = SceneFolder + scene + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    report.Append(scene).Append(": scene not found — skipped\n");
                    continue;
                }

                var primary = LoadEnemy(primaryName);
                var secondary = secondaryName != null ? LoadEnemy(secondaryName) : null;
                if (primary == null)
                {
                    report.Append(scene).Append(": primary prefab '").Append(primaryName).Append("' not found — skipped\n");
                    continue;
                }

                var openScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int swapped = 0, bossWalk = 0, skipped = 0;

                var enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < enemies.Length; i++)
                {
                    var e = enemies[i];
                    GameObject art = (secondary != null && (i % 2 == 1)) ? secondary : primary;
                    switch (Process(e.gameObject, art, selfIsVisual: false))
                    {
                        case Result.Swapped: swapped++; break;
                        case Result.BossWalk: bossWalk++; break;
                        default: skipped++; break;
                    }
                }

                var facs = Object.FindObjectsByType<FactionCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var f in facs)
                {
                    GameObject art = (secondary != null && f.FactionId == 1) ? secondary : primary;
                    switch (Process(f.gameObject, art, selfIsVisual: true))
                    {
                        case Result.Swapped: swapped++; break;
                        case Result.BossWalk: bossWalk++; break;
                        default: skipped++; break;
                    }
                }

                if (swapped > 0 || bossWalk > 0)
                {
                    EditorSceneManager.MarkSceneDirty(openScene);
                    EditorSceneManager.SaveScene(openScene);
                }
                totalSwapped += swapped; totalBossWalk += bossWalk; totalSkipped += skipped;
                report.Append(scene).Append(": swapped=").Append(swapped)
                      .Append(" bossWalk=").Append(bossWalk).Append(" skipped=").Append(skipped)
                      .Append(" (primary=").Append(primaryName)
                      .Append(secondaryName != null ? ", secondary=" + secondaryName : "").Append(")\n");
            }

            Debug.Log($"[EnemyArtWirer] Done: {totalSwapped} swapped, {totalBossWalk} boss-walk, {totalSkipped} skipped.\n{report}");
        }

        private enum Result { Swapped, BossWalk, Skipped }

        private static GameObject LoadEnemy(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + name + ".prefab");

        private static Result Process(GameObject root, GameObject artPrefab, bool selfIsVisual)
        {
            // Already processed by a prior run.
            if (root.transform.Find("Visual_Enemy") != null)
                return Result.Skipped;

            // Named-mesh bosses (Ch9 Vane, Ch11 Aldric) already carry rigged art — just make them walk.
            if (root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                NpcWalkAnimator.EnsureOn(root);
                return Result.BossWalk;
            }

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(artPrefab, root.transform);
            vis.name = "Visual_Enemy";
            vis.transform.localRotation = Quaternion.identity;
            Vector3 s = root.transform.localScale;
            vis.transform.localScale = new Vector3(SafeInv(s.x), SafeInv(s.y), SafeInv(s.z));
            vis.transform.localPosition = Vector3.zero;

            FitAndGround(vis, root.transform.position.y);

            // Hide the old greybox visual (keep the GameObject, collider and weapon rig intact).
            if (selfIsVisual)
            {
                var mr = root.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
            else
            {
                var body = root.transform.Find("Body");
                if (body != null)
                    foreach (var mr in body.GetComponents<MeshRenderer>()) mr.enabled = false;
            }

            // Re-point bodyRenderer to the new skinned mesh so combat hit-flash shows on the visible art.
            var smr = vis.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null)
            {
                var comp = (Component)root.GetComponent<Enemy>() ?? root.GetComponent<FactionCombatant>();
                if (comp != null)
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("bodyRenderer");
                    if (prop != null) { prop.objectReferenceValue = smr; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }

            NpcWalkAnimator.EnsureOn(root);
            return Result.Swapped;
        }

        private static float SafeInv(float v) => Mathf.Abs(v) < 1e-4f ? 1f : 1f / v;

        /// <summary>Scale the visual to ~1.8 m tall and drop its feet to the enemy root's own ground y.</summary>
        private static void FitAndGround(GameObject vis, float rootGroundY)
        {
            var rends = vis.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            if (h > 0.01f && (h < 1.2f || h > 2.4f))
                vis.transform.localScale *= 1.8f / h;

            // Re-measure after scaling and shift so the feet sit at the root's ground plane.
            rends = vis.GetComponentsInChildren<Renderer>();
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            vis.transform.position += Vector3.up * (rootGroundY - b.min.y);
        }
    }
}
