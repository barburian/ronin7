using System.Text;
using Ronin7.Combat;
using Ronin7.Enemies;
using Ronin7.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

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
        private const string MaterialFolder = OutputFolder + "/Materials";
        private const string BodyShaderName = "Ronin7/GrittyCyber";
        private const string EdgeMaterialPath = "Assets/Ronin7/Art/Materials/VfxNeon_Additive.mat"; // shared URP Particles/Unlit additive (already used by shipped VFX)
        private const string EdgeObjectName = "BladeEdge";
        private const string BlackMaskPath = "Assets/Ronin7/Art/Textures/Black4.png";

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

            // BUG-17: glTFast's material reads baseColorFactor, so RendererTint's _BaseColor/_EmissionColor
            // MPB (the windup telegraph) did nothing. Swap onto one shared GrittyCyber material per type.
            if (bodyRenderer != null) SwapToEnemyMaterial(instance, bodyRenderer, name, sourceFolder);

            instance.AddComponent<Health>();
            // Posture/stagger: Enemy.Awake configures it from definition.postureMaxFraction when present
            // (0 = keep PostureMeter's serialized postureMax). Without it, only a deflect ever staggers.
            instance.AddComponent<PostureMeter>();
            var enemy = instance.AddComponent<Enemy>();
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "weapon", armR);
            SetObjectRef(so, "bladeTip", bladeTip);
            SetObjectRef(so, "bodyRenderer", bodyRenderer);
            // "target" and "definition" are deliberately left null — see the class doc.
            so.ApplyModifiedPropertiesWithoutUndo();
            EnsureBladeEdge(instance);

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
            // P1-12: drop the variant link to the source art prefab, or its glTFast material (and the
            // 2048 px embedded textures) stays a build dependency of every enemy.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(instance, outputPath);
            Object.DestroyImmediate(instance);

            report.Append(name).Append(": OK (x").Append(scale.ToString("F2"))
                  .Append(" -> ").Append(TargetEnemyHeight.ToString("F2")).Append("m) -> ")
                  .Append(outputPath).Append('\n');
            return true;
        }

        /// <summary>Creates/updates <c>&lt;name&gt;_Enemy.mat</c> on GrittyCyber from the body's glTF material
        /// and puts it on every body slot plus any other glTFast slot on the enemy. Emission is rim-only
        /// (black mask) and black at rest; MeleeAttacker drives the P1-11 _RimColor/_FlashAmount per renderer
        /// via MPB. The glTF textures are extracted by <see cref="ExtractTextures"/> (P1-12).</summary>
        private static void SwapToEnemyMaterial(GameObject instance, Renderer bodyRenderer, string name, string sourceFolder)
        {
            var shader = Shader.Find(BodyShaderName);
            if (shader == null)
            {
                Debug.LogError($"[EnemyPrefabBaker] '{name}': shader {BodyShaderName} not found — body material left as-is.");
                return;
            }

            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + "_Enemy.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = shader;

            // Source = the body's original glTF material (a re-bake starts from the source art prefab).
            var src = bodyRenderer.sharedMaterial;
            if (src != null && src.shader != shader)
            {
                if (src.HasProperty("baseColorTexture")) mat.SetTexture("_BaseMap", src.GetTexture("baseColorTexture"));
                if (src.HasProperty("normalTexture")) mat.SetTexture("_BumpMap", src.GetTexture("normalTexture"));
                if (src.HasProperty("roughnessFactor")) mat.SetFloat("_Smoothness", 1f - src.GetFloat("roughnessFactor"));
            }
            // glTF metallicFactor only scales the ORM map's B channel, and every Tripo export ships
            // factor 1. GrittyCyber can't read ORM (it wants R=metal, A=smooth), so copying the bare
            // factor made every enemy full metal: no diffuse, and with no reflection probe on Quest, black.
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_MetallicGlossMap", null); // shader default white: scalars above drive it
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BlackMaskPath));
            mat.SetFloat("_EdgeFresnel", 2.5f);
            mat.SetFloat("_RimPower", 2.5f);
            if (src != null && src.shader != shader) ExtractTextures(sourceFolder, name, mat, src);
            EditorUtility.SetDirty(mat);

            var bodySlots = bodyRenderer.sharedMaterials;
            for (int i = 0; i < bodySlots.Length; i++) bodySlots[i] = mat;
            bodyRenderer.sharedMaterials = bodySlots;

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r == bodyRenderer) continue;
                var slots = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || !slots[i].shader.name.Contains("glTF")) continue;
                    slots[i] = mat;
                    changed = true;
                }
                if (changed) r.sharedMaterials = slots;
            }
        }


        /// <summary>P1-14: idempotently adds the 2-point blade-edge LineRenderer (shared additive material, 3 cm,
        /// no shadows / probes, world space) and wires <c>MeleeAttacker.bladeEdge</c>. MeleeAttacker enables it
        /// only in Windup/Active and writes the endpoints, so the positions here are placeholders.</summary>
        private static void EnsureBladeEdge(GameObject root)
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null) return;
            var child = root.transform.Find(EdgeObjectName);
            if (child == null)
            {
                child = new GameObject(EdgeObjectName).transform;
                child.SetParent(root.transform, false);
            }
            var lr = child.GetComponent<LineRenderer>();
            if (lr == null) lr = child.gameObject.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.03f;
            lr.numCapVertices = 0;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.enabled = false;
            var so = new SerializedObject(enemy);
            SetObjectRef(so, "bladeEdge", lr);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>P1-14: additive entry point. Adds the blade edge to every existing baked enemy prefab
        /// without re-baking (post-bake edits survive).</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Add Enemy Blade Edge (additive)")]
        public static void AddBladeEdgeToBaked()
        {
            foreach (var name in System.Linq.Enumerable.Concat(TrashMobNames, BossNames))
            {
                string path = OutputFolder + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[EnemyPrefabBaker] missing " + path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                EnsureBladeEdge(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyPrefabBaker] Added blade edge to baked enemies.");
        }

        // P3-04: baked prefab name -> archetype class, plus hover bottom height (0 = grounded).
        private const float DroneArtHeight = 0.8f, DroneHoverBottom = 1.4f; // centre ~1.8 m, inside the 1.6-2.0 m band

        /// <summary>P3-04 (additive, idempotent): swaps <c>Enemy</c> for the archetype subclass on the baked
        /// Charger / Hunter / Scan-Drone / Teleporter prefabs (serialized fields survive via m_Script),
        /// wires their optional streak / beam / glyph, and rebakes the two drones as ~0.8 m hover art:
        /// Visual_Skinned and Rig_Root lifted by the hover height, root pivot untouched at y=0.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Archetype Prefabs (additive)")]
        public static void PatchArchetypePrefabs()
        {
            PatchOne("Coil_Syndicate_Ganger", typeof(ChargerEnemy), false);
            PatchOne("Hunter_Drone", typeof(Ronin7.Ship.RangedEnemy), true);
            PatchOne("Dominion_Scan-Drone", typeof(SupportDrone), true);
            PatchOne("Dream_Ghost_Manifestation", typeof(BlinkEnemy), false);
            PatchOne("Vane_Wraith-6", typeof(BlinkEnemy), false); // P3-07: blinks only in its Blink-weakness phase 2
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyPrefabBaker] Archetype prefabs patched.");
        }

        private static void PatchOne(string name, System.Type type, bool hover)
        {
            string path = OutputFolder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning("[EnemyPrefabBaker] missing " + path); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var enemy = root.GetComponent<Enemy>();
                if (enemy.GetType() != type)
                {
                    var script = System.Array.Find(
                        System.Array.ConvertAll(AssetDatabase.FindAssets("t:MonoScript " + type.Name), g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g))),
                        m => m != null && m.GetClass() == type);
                    var so = new SerializedObject(enemy);
                    so.FindProperty("m_Script").objectReferenceValue = script;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    enemy = root.GetComponent<Enemy>();
                }
                var eso = new SerializedObject(enemy);
                if (type == typeof(ChargerEnemy)) SetObjectRef(eso, "streak", EnsureLine(root, "ChargeStreak", 0.12f));
                else if (type == typeof(SupportDrone)) SetObjectRef(eso, "beam", EnsureLine(root, "BuffBeam", 0.05f));
                else if (type == typeof(BlinkEnemy)) SetObjectRef(eso, "glyph", EnsureGlyph(root));
                eso.ApplyModifiedPropertiesWithoutUndo();
                if (hover) BakeHover(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static LineRenderer EnsureLine(GameObject root, string childName, float width)
        {
            var child = root.transform.Find(childName);
            if (child == null) { child = new GameObject(childName).transform; child.SetParent(root.transform, false); }
            var lr = child.GetComponent<LineRenderer>();
            if (lr == null) lr = child.gameObject.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.SetPositions(new[] { Vector3.up * (width * 0.5f), Vector3.up * (width * 0.5f) }); // placeholder bounds stay above the floor
            lr.alignment = LineAlignment.TransformZ; // streak lies flat on the floor; beam is thin enough not to care
            lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.enabled = false;
            return lr;
        }

        /// <summary>1 m floor quad (shared additive material, no collider) the Teleporter parks on its destination.
        /// World-sized: the root scale is divided out.</summary>
        private static Transform EnsureGlyph(GameObject root)
        {
            var t = root.transform.Find("BlinkGlyph");
            if (t == null)
            {
                t = new GameObject("BlinkGlyph").transform;
                t.SetParent(root.transform, false);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var mr = t.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EdgeMaterialPath);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.localPosition = new Vector3(0f, 0.02f / root.transform.localScale.y, 0f);
            t.localScale = Vector3.one / root.transform.localScale.y;
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>Idempotent: rescales the root so the art is DroneArtHeight tall, lifts Visual_Skinned and
        /// Rig_Root so it floats DroneHoverBottom above the root pivot, and refits the capsule to it.</summary>
        private static void BakeHover(GameObject root)
        {
            Bounds Measure()
            {
                var rends = root.GetComponentsInChildren<Renderer>(true);
                Bounds b = default; bool first = true;
                foreach (var r in rends)
                {
                    if (r is LineRenderer || r.gameObject.name == "BlinkGlyph") continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                return b;
            }
            Bounds m = Measure();
            if (m.size.y > 0.01f) root.transform.localScale *= DroneArtHeight / m.size.y;
            m = Measure();
            float s = root.transform.localScale.y;
            Vector3 lift = root.transform.InverseTransformVector(Vector3.up * (root.transform.position.y + DroneHoverBottom - m.min.y));
            foreach (var n in new[] { "Visual_Skinned", "Rig_Root" })
            {
                var c = FindDeep(root.transform, n);
                if (c != null) c.localPosition += lift;
            }
            var cap = root.GetComponent<CapsuleCollider>();
            cap.height = DroneArtHeight / s;
            cap.radius = 0.3f / s;
            cap.center = new Vector3(0f, (DroneHoverBottom + DroneArtHeight * 0.5f) / s, 0f);
        }

        /// <summary>P1-12 (BUG-23): additive entry point. Re-extracts the textures from each existing baked
        /// enemy's source art material onto its existing <c>_Enemy.mat</c>, without touching the prefabs.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Extract Enemy Textures (additive)")]
        public static void ExtractAllEnemyTextures()
        {
            var report = new StringBuilder();
            foreach (var (folder, names) in new[] { (EnemySourceFolder, TrashMobNames), (BossSourceFolder, BossNames) })
            foreach (var name in names)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + name + ".prefab");
                var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + "_Enemy.mat");
                var r = source != null ? FindDeep(source.transform, "Visual_Skinned")?.GetComponent<Renderer>() : null;
                var src = r != null ? r.sharedMaterial : null;
                if (mat == null || src == null) { report.Append(name).Append(": SKIPPED (missing mat/source)\n"); continue; }
                ExtractTextures(folder, name, mat, src);
                EditorUtility.SetDirty(mat);
                UnpackBaked(name);
                report.Append(name).Append(": OK\n");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyPrefabBaker] ExtractEnemyTextures\n" + report);
        }

        /// <summary>Unpacks an already-baked enemy prefab (no-op if already unpacked) so the source art
        /// prefab's glTFast material stops being a build dependency. Root stays inactive (A3.1).</summary>
        private static void UnpackBaked(string name)
        {
            string path = OutputFolder + "/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(root))
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>Writes the glTFast albedo/MR/normal sub-asset textures of <paramref name="src"/> out as
        /// readable PNGs (capped at 1024) under <c>&lt;folder&gt;/&lt;name&gt;_Tex/</c>, imports them with Quest
        /// ASTC settings and assigns them to <paramref name="mat"/> with <c>_MRGltf = 1</c>.</summary>
        public static void ExtractTextures(string folder, string name, Material mat, Material src)
        {
            string dir = folder + "/" + name + "_Tex";
            EnsureFolder(dir);
            var albedo = ExtractOne(src, "baseColorTexture", dir + "/Albedo.png", true, false);
            var mr = ExtractOne(src, "metallicRoughnessTexture", dir + "/MR.png", false, false);
            var normal = ExtractOne(src, "normalTexture", dir + "/Normal.png", false, true);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null) mat.SetTexture("_BumpMap", normal);
            if (mr != null)
            {
                mat.SetTexture("_MetallicGlossMap", mr);
                mat.SetFloat("_MRGltf", 1f);
                // Capped at 0.2 (EnemyBodyMaterialTests): full metal has no diffuse and no reflection
                // probe on Quest renders black. The B channel still varies it per texel.
                mat.SetFloat("_Metallic", 0.2f);
                mat.SetFloat("_Smoothness", 1f);
            }
        }

        private static Texture2D ExtractOne(Material src, string prop, string path, bool srgb, bool normal)
        {
            var tex = src.HasProperty(prop) ? src.GetTexture(prop) as Texture2D : null;
            if (tex == null) { Debug.LogWarning($"[EnemyPrefabBaker] '{src.name}' has no {prop} - {path} not written."); return null; }
            if (GraphicsFormatUtility.IsSRGBFormat(tex.graphicsFormat) != srgb)
                Debug.LogWarning($"[EnemyPrefabBaker] {path}: source sRGB={!srgb}, expected {srgb}; bytes copied as-is.");

            int w = Mathf.Min(tex.width, 1024), h = Mathf.Min(tex.height, 1024);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            System.IO.File.WriteAllBytes(path, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (!normal) imp.sRGBTexture = srgb;
            imp.mipmapEnabled = true;
            imp.maxTextureSize = 1024;
            imp.alphaSource = TextureImporterAlphaSource.None;
            var a = imp.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = 1024;
            a.format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            imp.SetPlatformTextureSettings(a);
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
