using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ronin7.Enemies;
using Ronin7.World;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Additively drops ambient filler crowds (the new rigged Tripo art under Characters3D/Diversity/,
    /// rigged by <see cref="Ronin7.Editor.Art.NpcAutoRigger"/>) into thematically-fitting shipped scenes
    /// and gives each an <see cref="NpcWander"/> so it free-roams (which in turn auto-adds
    /// <see cref="NpcWalkAnimator"/> for the leg swing). Crowd members are visual-only — no collider, so
    /// they never block the player.
    ///
    /// Spawn points are scattered around the scene's existing known-walkable character anchors (StoryNpc,
    /// else Enemy) so the crowd lands on real ground near the action rather than blind in geometry.
    /// Idempotent (skips a scene that already has a CrowdRoot). Like the enemy-art and voice wirers this
    /// opens each shipped scene directly and patches in place — the chapter Build menus destructively
    /// rebuild and must not be re-run against a shipped scene.
    /// </summary>
    public static class CrowdArtWirer
    {
        private const string DiversityFolder = "Assets/Ronin7/Art/Generated/Characters3D/Diversity/";
        private const string SceneFolder = "Assets/Ronin7/Scenes/";
        private const int PerType = 6; // crowd members placed per type per scene

        // Thematic scene -> crowd types (folder names under Diversity/).
        private static readonly (string scene, string[] types)[] Map =
        {
            ("Ch08_SilentGarden",     new[] { "Mourners" }),
            ("Ch06_IronDojo",         new[] { "Obsidian-Synod" }),
            ("Ch16_ThroneOfAshes",    new[] { "Obsidian-Synod" }),
            ("Ch07_ForgottenNames",   new[] { "Warren-folk" }),
            ("Ch09_PitAndTheDeep",    new[] { "Warren-folk" }),
            ("Ch05_DebtOfAshes",      new[] { "Ashkin", "Gravelkin" }),
            ("Ch02_Auction",          new[] { "Vesh", "Voll" }),
        };

        [MenuItem("Tools/Space Samurai/Chapters/Wire Filler Crowds Into Scenes")]
        public static void WireCrowdsIntoScenes()
        {
            var report = new StringBuilder();
            int grandTotal = 0;

            foreach (var (scene, types) in Map)
            {
                string scenePath = SceneFolder + scene + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    report.Append(scene).Append(": scene not found — skipped\n");
                    continue;
                }

                var openScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (GameObject.Find("CrowdRoot") != null)
                {
                    report.Append(scene).Append(": CrowdRoot already present — skipped\n");
                    continue;
                }

                var anchors = CollectAnchors();
                if (anchors.Count == 0)
                {
                    report.Append(scene).Append(": no walkable anchors found — skipped\n");
                    continue;
                }

                var crowdRoot = new GameObject("CrowdRoot");
                int placed = 0, idx = 0;
                foreach (var type in types)
                {
                    var variants = LoadVariants(type);
                    if (variants.Count == 0)
                    {
                        report.Append(scene).Append(": type '").Append(type).Append("' has no prefabs\n");
                        continue;
                    }
                    for (int n = 0; n < PerType; n++, idx++)
                    {
                        var prefab = variants[n % variants.Count];
                        Vector3 anchor = anchors[idx % anchors.Count];
                        // Golden-angle spiral scatter, 2–4 m out, so crowd rings the anchor without piling up.
                        float ang = idx * 2.399963f;
                        float rad = 2f + 1.6f * (n / (float)PerType);
                        Vector3 pos = anchor + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);

                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, crowdRoot.transform);
                        go.name = $"Crowd_{type}_{n:00}";
                        go.transform.position = new Vector3(pos.x, anchor.y, pos.z);
                        go.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
                        FitAndGround(go, anchor.y);
                        go.AddComponent<NpcWander>();
                        placed++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(openScene);
                EditorSceneManager.SaveScene(openScene);
                grandTotal += placed;
                report.Append(scene).Append(": placed=").Append(placed)
                      .Append(" (").Append(string.Join(", ", types)).Append(")\n");
            }

            Debug.Log($"[CrowdArtWirer] Done: {grandTotal} crowd NPCs placed.\n{report}");
        }

        /// <summary>Known-walkable anchor points: prefer peaceful StoryNpc spots, else enemy spawns.</summary>
        private static List<Vector3> CollectAnchors()
        {
            var npcs = Object.FindObjectsByType<StoryNpc>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var pts = npcs.Select(n => n.transform.position).ToList();
            if (pts.Count == 0)
                pts = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Select(e => e.transform.position).ToList();
            return pts;
        }

        private static List<GameObject> LoadVariants(string type)
        {
            string folder = DiversityFolder + type;
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(g => g != null)
                .ToList();
        }

        /// <summary>Scale the crowd member to ~1.8 m and drop its feet to the anchor's ground y.</summary>
        private static void FitAndGround(GameObject go, float groundY)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float h = b.size.y;
            if (h > 0.01f && (h < 1.2f || h > 2.4f))
                go.transform.localScale *= 1.8f / h;

            rends = go.GetComponentsInChildren<Renderer>();
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += Vector3.up * (groundY - b.min.y);
        }
    }
}
