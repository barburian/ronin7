using System.Text;
using Ronin7.Combat;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Module 5a — bakes the 11 trash-mob art prefabs (<c>Art/Generated/Characters3D/Enemies/</c>)
    /// plus the 3 sector-boss Named prefabs (Appendix A of Roguelike-Design.md) into combat-ready,
    /// spawnable prefabs at <c>Assets/Ronin7/Prefabs/Roguelike/Enemies/</c>. The roguelike's
    /// <c>EnemySpawnTable</c> (see <see cref="RoguelikeDataBuilder"/>) points at these exact paths —
    /// another agent's runtime spawner depends on them, so do not rename or relocate.
    ///
    /// Component recipe mirrors <c>XRRigBuilder.BuildEnemy</c> and <see cref="EnemyArtWirer"/>:
    /// <see cref="Health"/> + <see cref="Enemy"/> on the root, the weapon rig bound at
    /// <c>Rig_ArmR/Sword/Blade/BladeTip</c> (built if the source art prefab doesn't already carry
    /// it), <c>bodyRenderer</c> pointed at <c>Visual_Skinned</c>'s renderer, a <see cref="NpcWalkAnimator"/>,
    /// and a capsule collider on the root (never a MeshCollider — CLAUDE.md forbids introducing one).
    /// <c>target</c> and <c>definition</c> are left null on purpose: <c>MeleeAttacker.Awake</c>'s
    /// <c>FindPlayer()</c> resolves <c>target</c> at spawn, and the roguelike spawner calls
    /// <c>Enemy.Configure(def)</c> before activation to set <c>definition</c>.
    ///
    /// <para><b>CRITICAL (A3.1) — every baked prefab's ROOT GAMEOBJECT IS SAVED INACTIVE.</b> Unity does
    /// not run <c>Awake</c> on a component added to (or already present on) an inactive GameObject, and
    /// it does not run <c>Awake</c> on an instantiated copy of one either — that inactive window is what
    /// lets the roguelike spawner call <c>Instantiate(prefab)</c> → <c>Enemy.Configure(scaledDef)</c> →
    /// position → <c>SetActive(true)</c>, so <c>Enemy.Awake</c> (which one-time-configures both
    /// <c>Health</c> and <c>PostureMeter</c> from <c>definition</c>) runs exactly once, after the scaled
    /// definition is in place. If this prefab is ever "fixed" by re-enabling the root, difficulty
    /// scaling and posture-break thresholds silently revert to the unscaled base stats at every depth —
    /// do not re-enable it.</para>
    ///
    /// Idempotent: re-running overwrites the baked prefab cleanly (matches every other builder in this
    /// project — <c>PrefabUtility.SaveAsPrefabAsset</c> replaces the asset at the same path in place).
    /// This class cannot be exercised without a live editor (AssetDatabase, PrefabUtility); it fails
    /// loudly per-prefab (log + skip) rather than throwing, so one missing source asset never aborts
    /// the whole bake.
    /// </summary>
    public static class EnemyPrefabBaker
    {
        private const string EnemySourceFolder = "Assets/Ronin7/Art/Generated/Characters3D/Enemies";
        private const string BossSourceFolder = "Assets/Ronin7/Art/Generated/Characters3D/Named";
        private const string OutputFolder = "Assets/Ronin7/Prefabs/Roguelike/Enemies";

        /// <summary>Adult human height (metres) every spawnable enemy is normalised to. 1 unit = 1 m
        /// is a hard VR rule: an enemy that is 0.4 m short reads as a child at room scale.</summary>
        private const float TargetEnemyHeight = 1.8f;

        /// <summary>Uniformly scales <paramref name="go"/> so its combined renderer bounds are
        /// <paramref name="targetHeight"/> tall, and drops its art so the feet sit on the root's own
        /// ground plane — leaving the ROOT pivot itself untouched. Returns the scale factor applied
        /// (1 when the mesh has no renderers or zero height).</summary>
        private static float NormaliseHeightTo(GameObject go, float targetHeight)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return 1f;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y <= 0.01f) return 1f;

            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Re-measure after scaling and ground the feet, so spawn positions (which assume y=0 is
            // the floor) put the enemy on the floor rather than sunk into or floating above it.
            // The Tripo meshes pivot at their bounds CENTRE, so this lift is ~half the body height.
            // It must land on the CHILDREN, never on the root: RunArenaController assigns the root's
            // localPosition wholesale at spawn (arena-local, y=0), which discards any root-level
            // offset and leaves the enemy half-sunk with its capsule floating above the body. Root
            // pivot = feet is the convention everywhere else (EnemyArtWirer.FitAndGround,
            // XRRigBuilder.BuildEnemy), and the capsule baked below assumes it too.
            rends = go.GetComponentsInChildren<Renderer>(true);
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 lift = go.transform.InverseTransformVector(
                Vector3.up * (go.transform.position.y - b.min.y));
            foreach (Transform child in go.transform) child.localPosition += lift;
            return scale;
        }

        // The 11 trash-mob prefabs (Appendix A).
        private static readonly string[] TrashMobNames =
        {
            "Ash-World_Scavenger",
            "Coil_Syndicate_Ganger",
            "Dominion_Trooper",
            "Program_Operative_Grunt",
            "Hunter_Drone",
            "Dominion_Scan-Drone",
            "Humanoid_Automaton",
            "Iron_Dojo_Warden-Cadre",
            "Spectral_Grave-Guardian",
            "Redaction_Construct",
            "Dream_Ghost_Manifestation",
        };

        // The 3 sector-boss Named prefabs (Appendix A) — spare/alternate bosses (Kerrax, Samurai-4,
        // Sever_Ninja-2) are not baked; they're not referenced by the shipped EnemySpawnTable.
        private static readonly string[] BossNames =
        {
            "The-Warden",
            "Vane_Wraith-6",
            "Maelgorn",
        };

        [MenuItem("Tools/Space Samurai/Roguelike/Bake Enemy Prefabs")]
        public static void BakeEnemyPrefabs()
        {
            EnsureFolder(OutputFolder);
            var report = new StringBuilder();
            int baked = 0, failed = 0;

            foreach (var name in TrashMobNames)
            {
                if (BakeOne(EnemySourceFolder, name, report)) baked++; else failed++;
            }
            foreach (var name in BossNames)
            {
                if (BakeOne(BossSourceFolder, name, report)) baked++; else failed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyPrefabBaker] Baked {baked} prefab(s) to {OutputFolder}, {failed} failed.\n{report}");
        }

        private static bool BakeOne(string sourceFolder, string name, StringBuilder report)
        {
            string sourcePath = sourceFolder + "/" + name + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"[EnemyPrefabBaker] Source prefab not found: {sourcePath} — skipped.");
                report.Append(name).Append(": MISSING SOURCE (").Append(sourcePath).Append(")\n");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            // Inactive from the moment it exists: no Awake fires on any component added below, and
            // none will fire on a future Instantiate() of the saved prefab either — see the class doc.
            instance.SetActive(false);

            var armR = FindDeep(instance.transform, "Rig_ArmR");
            if (armR == null)
            {
                Debug.LogError($"[EnemyPrefabBaker] '{name}': no Rig_ArmR bone found in the source rig — " +
                               "cannot wire the weapon; skipped.");
                report.Append(name).Append(": NO Rig_ArmR\n");
                Object.DestroyImmediate(instance);
                return false;
            }

            var bladeTip = armR.Find("Sword/Blade/BladeTip");
            if (bladeTip == null)
            {
                // Matches XRRigBuilder.BuildEnemy's greybox convention (Sword/Blade/BladeTip, tip offset
                // 0.5m along local +Z) so the parry-capsule geometry behaves identically to shipped enemies.
                var sword = new GameObject("Sword").transform;
                sword.SetParent(armR, false);
                var blade = new GameObject("Blade").transform;
                blade.SetParent(sword, false);
                var tip = new GameObject("BladeTip").transform;
                tip.SetParent(blade, false);
                tip.localPosition = new Vector3(0f, 0f, 0.5f);
                bladeTip = tip;
            }

            var visual = FindDeep(instance.transform, "Visual_Skinned");
            var bodyRenderer = visual != null ? visual.GetComponent<Renderer>() : null;
            if (bodyRenderer == null)
            {
                bodyRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (bodyRenderer == null)
                    Debug.LogWarning($"[EnemyPrefabBaker] '{name}': no Visual_Skinned renderer found — " +
                                      "combat hit-flash tint will be a no-op for this enemy.");
            }

            instance.AddComponent<Health>();
            var enemy = instance.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "weapon", armR);
            SetObjectRef(so, "bladeTip", bladeTip);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            // "target" and "definition" are deliberately left null — see the class doc.
            so.ApplyModifiedPropertiesWithoutUndo();

            NpcWalkAnimator.EnsureOn(instance);

            // Normalise to adult human height. The Tripo source meshes measure ~1.40 m, which reads as
            // child-sized next to a 1.6 m player in VR — confirmed in-headset. EnemyArtWirer.FitAndGround
            // only rescales outside [1.2, 2.4] m, so 1.40 m slips through it untouched; here we always
            // normalise, because at room scale a 0.4 m height error is glaring.
            float scale = NormaliseHeightTo(instance, TargetEnemyHeight);

            // Collider is authored in world metres, so divide by the root scale we just applied or it
            // would be scaled a second time and end up ~2.3 m tall around a 1.8 m body.
            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f / scale;
            capsule.radius = 0.35f / scale;
            capsule.center = new Vector3(0f, 0.9f / scale, 0f);

            string outputPath = OutputFolder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);

            report.Append(name).Append(": OK (x").Append(scale.ToString("F2"))
                  .Append(" -> ").Append(TargetEnemyHeight.ToString("F2")).Append("m) -> ")
                  .Append(outputPath).Append('\n');
            return true;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetObjectRef(SerializedObject so, string property, Object value)
        {
            var prop = so.FindProperty(property);
            if (prop != null) prop.objectReferenceValue = value;
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
