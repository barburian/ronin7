using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using Ronin7.Player;
using Ronin7.Ship;
using Ronin7.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Module 5 editor builder for the roguelike run: menu <c>Tools/Space Samurai/Roguelike/Build Run
    /// Arena Scene</c> authors the single reusable <c>Assets/Ronin7/Scenes/RunArena.unity</c> scene that
    /// every node loads, and (per the design doc's Module 5 note) additively wires a persistent
    /// <see cref="RunDirector"/> into the boot scene so <c>MainMenuController.OnStartRunClicked</c> has
    /// something to call.
    ///
    /// Declared as another partial of <see cref="XRRigBuilder"/> per the design doc's Appendix A note —
    /// <c>BuildRig</c>, <c>AttachPlayerAbilities</c>, <c>BuildSword</c>, <c>EnsureWeaponDefinition</c>,
    /// <c>EnsureFolder</c>, <c>EnsureScenesInBuild</c>, <c>TryLoadInputRefs</c>,
    /// <c>EnsureXRUIEventSystemMenu</c>, <c>SetObjectRef</c> and <c>BootScenePath</c> are all private
    /// static members over there and must not be duplicated. All helpers local to this file are
    /// prefixed <c>Rogue</c>.
    ///
    /// NO STATIC ROOM GEOMETRY: unlike a chapter scene, <see cref="RunArenaController"/> builds the
    /// floor/walls/ceiling/accent-lights/props itself at runtime via <c>ArenaGeometryBuilder</c> (once
    /// per node, under the black fade — see its own class doc), so this builder only needs to author the
    /// rig, the arena root, an EventSystem, and cheap ambient lighting for the brief window before that
    /// runs.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string RogueScenePath = SceneFolder + "/RunArena.unity";
        private const string RogueArenaRoomLibraryPath = "Assets/Ronin7/Data/Roguelike/ArenaRoomLibrary.asset";
        private const string RogueEnemySpawnTablePath = "Assets/Ronin7/Data/Roguelike/EnemySpawnTable.asset";
        private const string RogueBoonCatalogPath = "Assets/Ronin7/Data/Boons/BoonCatalog.asset";

        // Matches BuildRig's own on-foot spawn nudge (addLocomotion rigs spawn at z=2 to clear a front
        // wall/floor lip) — this transform is only a fallback aim point for RunArenaController (used
        // when Camera.main is null), but keeping it at the rig's actual spawn position is the honest
        // value to author rather than an arbitrary one.
        private const float RoguePlayerSpawnZ = 2f;

        [MenuItem("Tools/Space Samurai/Roguelike/Build Run Arena Scene", priority = 500)]
        public static void BuildRunArenaScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The input-action refs must ALSO be loaded after NewScene, for exactly the reason the
            // comment below gives for definition assets. Loading them first (as the chapter builders
            // do) makes every FindRef lookup miss once NewScene has unloaded them, and the rig
            // silently ships with unwired Head/Hand position, rotation and select bindings — i.e. no
            // tracking and no input at all in the headset, reported only as editor-time log errors.
            if (!TryLoadInputRefs(out var refs)) return;

            // Definition assets loaded AFTER NewScene (scene creation unloads unused assets, so refs
            // held across it go fake-null and serialize as {fileID: 0} — see Chapter9Builder). These
            // three are authored by the other Module 5 builders; missing ones log a warning and stay
            // null rather than failing this build (the other agent's baking order isn't this menu's
            // problem to enforce).
            var weapon = EnsureWeaponDefinition();
            var roomLibrary = RogueLoadOptional<ArenaRoomLibrary>(RogueArenaRoomLibraryPath, "ArenaRoomLibrary");
            var spawnTable = RogueLoadOptional<EnemySpawnTable>(RogueEnemySpawnTablePath, "EnemySpawnTable");

            // ---- Lighting: cheap ambient + a single directional light. ArenaGeometryBuilder adds its
            // own 2 accent point lights per node at runtime (A5.7's Quest per-object light budget), so
            // this scene deliberately adds none of its own on top. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.6f, 0.62f, 0.7f);
            light.intensity = 1.3f; // 0.4 + 0.08 ambient rendered black once the post grade went live (P0-06)
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
            RenderSettings.fogDensity = 0.015f;

            // ---- Player rig: locomotion + every component the boon system drives (A7.5). BuildRig
            // alone omits all of this; AttachPlayerAbilities covers PlayerCombatModifiers and the five
            // ability controllers (WeakpointSight/OverdriveController/PhaseStepController/UnbrokenWard/
            // MirrorSummonController, each self-gating on AbilityAccess.Has), but ParryFlowController and
            // ComboMomentumController are added by no builder anywhere — they were hand-wired into
            // chapter scenes post-build — so FlowInitiate/ComboInitiate stay dead without them here. ----
            var rig = BuildRig(refs, addLocomotion: true, farClip: 300f);
            AttachPlayerAbilities(rig, refs);
            if (rig.GetComponent<ParryFlowController>() == null) rig.AddComponent<ParryFlowController>();
            if (rig.GetComponent<ComboMomentumController>() == null) rig.AddComponent<ComboMomentumController>();
            EnsureBoonProcs(rig); // P4-05
            BuildSword(new Vector3(0.3f, 1f, 1.6f), weapon);
            RogueUpscaleSword();
            EnsureOffhandSword(); // P4-08 Twin Wakizashi left blade (inactive until equipped)
            EnsureSwordTrailDrivers(); // P5-03 (after the offhand copy: an existing copy is not re-made)

            // ---- Combat feedback (P0-05/BUG-05): hit haptics, the deflect slow-mo and the hit-flash
            // vignette (the only off-screen hit cue), the low-HP heartbeat and the blade-clash pulse.
            // Same scene-level placement as Chapter16Builder's CombatFeedbackController. ----
            new GameObject("Feedback").AddComponent<CombatFeedbackController>();
            if (rig.GetComponent<HeartbeatHaptics>() == null) rig.AddComponent<HeartbeatHaptics>();
            if (rig.GetComponent<BladeClashFeedback>() == null) rig.AddComponent<BladeClashFeedback>();

            // ---- XR UI plumbing (A7.2): without an EventSystem + XRUIInputModule in this scene, the
            // runtime-built boon-offer panel's TrackedDeviceGraphicRaycaster has nothing to route clicks
            // through and the run soft-locks at the first offer. The helper already exists; it was
            // previously called only from the main-menu builder. ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();

            // An EventSystem alone is NOT enough: something has to actually point at the panel. Without
            // a ray interactor on the rig the boon offer renders, highlights nothing, and cannot be
            // clicked — the run stalls at the first reward. Confirmed in-headset ("I cannot select a
            // boon") and by measurement (rayInteractors=0 in this scene). Same helper the boot menu uses.
            WireRightHandRayInteractorMenu();

            // ---- In-run settings (P0-05/BUG-05): the existing settings canvas, opened from the pause
            // panel's SETTINGS. Its toggle must NOT listen to the Menu button here — in a run that
            // button pauses (RunPauseController), and the pause panel calls Show()/Hide() instead.
            // SettingsService itself persists from the boot scene. ----
            var settingsPanel = SettingsPanelBuilder.BuildCanvas();
            SettingsPanelBuilder.EnsureMenuToggle(settingsPanel.gameObject);
            settingsPanel.gameObject.SetActive(false);
            var settingsToggleSo = new SerializedObject(Object.FindAnyObjectByType<SettingsMenuToggle>());
            settingsToggleSo.FindProperty("listenForMenuButton").boolValue = false;
            settingsToggleSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- P3-05: one shared bolt pool (capacity 32) for ranged enemies (RangedEnemy finds it once in Awake). ----
            EnsureProjectilePool();

            // ---- Player spawn point: a plain Transform RunArenaController falls back to for its
            // no-spawn exclusion centre when Camera.main is unavailable. ----
            var spawnGo = new GameObject("PlayerSpawn");
            spawnGo.transform.position = new Vector3(0f, 0f, RoguePlayerSpawnZ);

            // ---- Arena root: MUST sit at world identity — RunArenaController composes room geometry and
            // enemy spawn positions in its own local space, and that only lines up with the world-space
            // playerSpawn/Camera.main positions it also reads while the root is at world identity (A5.3).
            var arenaGo = new GameObject("Arena");
            arenaGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var arenaController = arenaGo.AddComponent<RunArenaController>();
            var arenaSo = new SerializedObject(arenaController);
            SetObjectRef(arenaSo, "roomLibrary", roomLibrary);
            SetObjectRef(arenaSo, "spawnTable", spawnTable);
            var halfExtentProp = arenaSo.FindProperty("arenaHalfExtent");
            if (halfExtentProp != null) halfExtentProp.floatValue = 0f; // P2-05: 0 = per-node-kind size (RunScaling.ArenaHalfExtent)
            SetObjectRef(arenaSo, "playerSpawn", spawnGo.transform);
            arenaSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            EditorSceneManager.SaveScene(scene, RogueScenePath);
            EnsureScenesInBuild(RogueScenePath);

            Debug.Log($"[Space Samurai] RunArena built at {RogueScenePath}. Rig: locomotion + " +
                      "PlayerCombatModifiers + all 5 ability controllers (AttachPlayerAbilities) + " +
                      "ParryFlowController + ComboMomentumController + katana. EventSystem/XRUIInputModule " +
                      "present for the boon-offer panel. Arena root 'Arena' at world identity carries " +
                      "RunArenaController " +
                      (roomLibrary != null ? "with ArenaRoomLibrary wired" : "— ArenaRoomLibrary MISSING, bake it first") + ", " +
                      (spawnTable != null ? "EnemySpawnTable wired" : "EnemySpawnTable MISSING, bake it first") +
                      ", halfExtent per node kind (RunScaling.ArenaHalfExtent). Room geometry/enemies build " +
                      "at runtime per node — no static walls authored here.");

            RogueEnsureRunDirectorInBootScene();
        }

        /// <summary>P3-05: adds the shared ProjectilePool (capacity 32) if the open scene has none. Idempotent, additive.</summary>
        internal static void EnsureProjectilePool()
        {
            if (Object.FindAnyObjectByType<ProjectilePool>() != null) return;
            var so = new SerializedObject(new GameObject("ProjectilePool").AddComponent<ProjectilePool>());
            so.FindProperty("capacity").intValue = 32;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Placing <see cref="RunDirector"/> only in RunArena.unity would leave it null until the arena
        /// scene loaded once — too late, since <c>MainMenuController.OnStartRunClicked</c> calls
        /// <c>RunDirector.Instance</c> from the boot scene, before any run has started. So it must live
        /// alongside <see cref="GameFlowManager"/> instead: same "Game" GameObject, added after it
        /// (component order is the sane default per A7.1, even though the EndingRun latch makes ordering
        /// safe either way). Opens the boot scene additively rather than rebuilding it — per CLAUDE.md,
        /// shipped scenes are patched, never rebuilt.
        /// </summary>
        /// <summary>
        /// Scales the katana up to a real sword length. The art prefab measures ~0.74 m overall
        /// (0.62 m blade + 0.12 m handle); a katana is ~1.0 m (blade ~0.70, tsuka ~0.25), and at room
        /// scale in VR the stock size reads as a toy — confirmed in-headset ("the sword is too small").
        /// Scales the visual only, so <c>BladeTip</c>'s world position (the parry/damage sample point)
        /// moves with the blade rather than being left behind at the old tip.
        /// </summary>
        /// <summary>P5-03: every holstered blade's trail gets <see cref="BladeTrailDriver"/> like Sword_Katana.prefab
        /// (the unpacked offhand copy does not inherit it). Idempotent; also the additive patch for a built RunArena.</summary>
        public static void EnsureSwordTrailDrivers()
        {
            foreach (var holster in Object.FindObjectsByType<KatanaHolster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (holster.Sword == null) continue;
                foreach (var trail in holster.Sword.GetComponentsInChildren<TrailRenderer>(true))
                    Ronin7.Editor.Art.ArtPrefabBuilder.EnsureTrailDriver(trail.transform);
            }
        }

        private static void RogueUpscaleSword()
        {
            const float SwordScale = 1.35f; // 0.74 m -> ~1.0 m overall

            var sword = GameObject.Find("Sword");
            if (sword == null)
            {
                Debug.LogWarning("[RogueEntry] No 'Sword' in the arena scene — skipped the katana upscale.");
                return;
            }

            var visual = sword.transform.Find("Sword_Katana");
            if (visual == null)
            {
                Debug.LogWarning("[RogueEntry] Sword has no 'Sword_Katana' visual child — skipped the upscale.");
                return;
            }

            visual.localScale *= SwordScale;
            Debug.Log($"[RogueEntry] Katana visual scaled x{SwordScale} (~0.74m -> ~1.0m overall).");
        }

        /// <summary>P4-08: every loadout definition (Katana + the roadmap's Nodachi / Tanto / Twin Wakizashi),
        /// creating any missing asset beside Katana.asset. Existing assets are returned untouched (tuning survives).</summary>
        public static WeaponDefinition[] EnsureLoadoutDefinitions() => new[]
        {
            WithBladeLength(EnsureWeaponDefinition(), Ronin7.Editor.Art.ArtPrefabBuilder.KatanaBladeLength),
            EnsureLoadoutDefinition("Nodachi", WeaponId.Nodachi, 2f, 7f, 14f, 60f, 0.35f, Ronin7.Editor.Art.ArtPrefabBuilder.NodachiBladeLength),
            EnsureLoadoutDefinition("Tanto", WeaponId.Tanto, 1f, 4.5f, 6f, 28f, 0.18f, Ronin7.Editor.Art.ArtPrefabBuilder.TantoBladeLength),
            // User decision (Amendment 12): 0.6x katana damage per blade, 1.25x swing speed (katana thresholds
            // and cooldown / 1.25). Supersedes the roadmap's 6-26. A15.5: Double Katana blades are full katana
            // length (0.837 m, the Sword_Katana mesh) and a normal parry staggers x0.7.
            EnsureLoadoutDefinition("TwinWakizashi", WeaponId.TwinWakizashi, 1.2f, 4.8f, 4.8f, 24f, 0.2f,
                Ronin7.Editor.Art.ArtPrefabBuilder.KatanaBladeLength, 0.7f),
            // A15.6 Polearm: swing 6-20 full at 7 m/s, 0.3 s cooldown; minSwingSpeed 1.5 is the default (A15 gives none).
            // bladeLength 0: the damager pivot sits at the grip and its box is the front 1.1 m, not a base-fixed blade.
            RoguePolearmRules(EnsureLoadoutDefinition("Polearm", WeaponId.PolearmShield, 1.5f, 7f, 6f, 20f, 0.3f, 0f)),
            // P9-02d: the Whip HANDLE's definition: no damage, no parry, no thrust. The chain's damage is WhipController's.
            RogueWhipHandleRules(EnsureLoadoutDefinition("WhipHandle", WeaponId.Whip, 1.5f, 6f, 0f, 0f, 0.25f, 0f)),
        };

        private const string PlayerShieldPath = DataFolder + "/PlayerShield.asset";

        /// <summary>A15.6 / R1: the Polearm cannot parry and thrusts for a flat 30. Spec-driven, so it is re-applied to an existing asset.</summary>
        private static WeaponDefinition RoguePolearmRules(WeaponDefinition def)
        {
            if (!def.canParry && Mathf.Approximately(def.thrustDamage, 30f)) return def;
            def.canParry = false;
            def.thrustDamage = 30f;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static WeaponDefinition RogueWhipHandleRules(WeaponDefinition def)
        {
            if (!def.canParry) return def;
            def.canParry = false;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        /// <summary>A15.7: the player's shield definition: 0 damage, parries, normal parry stagger x0.25. Its id is not a
        /// loadout id and it never goes in the roster or RunDirector.weapons.</summary>
        public static WeaponDefinition EnsurePlayerShieldDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PlayerShieldPath);
            if (existing != null) return existing;
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.id = "player_shield";
            def.minSwingSpeed = 1.5f;
            def.referenceSwingSpeed = 6f;
            def.minDamage = 0f;
            def.maxDamage = 0f;
            def.canParry = true;
            def.parryStagger = 0.25f;
            AssetDatabase.CreateAsset(def, PlayerShieldPath);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static WeaponDefinition WithBladeLength(WeaponDefinition def, float length)
        {
            if (Mathf.Approximately(def.bladeLength, length)) return def;
            def.bladeLength = length;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static WeaponDefinition EnsureLoadoutDefinition(string file, string id, float minSpeed, float refSpeed,
            float minDamage, float maxDamage, float cooldown, float bladeLength, float? parryStagger = null)
        {
            string path = DataFolder + "/" + file + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (existing != null)
            {
                // A13.5 / A15.5: only the length (and a given parry stagger) is spec-driven; tuned stats stay
                if (parryStagger.HasValue && !Mathf.Approximately(existing.parryStagger, parryStagger.Value))
                {
                    existing.parryStagger = parryStagger.Value;
                    EditorUtility.SetDirty(existing);
                }
                return WithBladeLength(existing, bladeLength);
            }
            var def = ScriptableObject.CreateInstance<WeaponDefinition>();
            def.id = id;
            def.minSwingSpeed = minSpeed;
            def.referenceSwingSpeed = refSpeed;
            def.minDamage = minDamage;
            def.maxDamage = maxDamage;
            def.hitCooldown = cooldown;
            def.bladeLength = bladeLength;
            if (parryStagger.HasValue) def.parryStagger = parryStagger.Value;
            AssetDatabase.CreateAsset(def, path);
            AssetDatabase.SaveAssets();
            return def;
        }

        private static void SetObjectRefArray(SerializedObject so, string property, Object[] values)
        {
            var prop = so.FindProperty(property);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>
        /// P4-08 Twin Wakizashi: a second instance of the scene's sword (same Grabbable / Sword / BladeDamager
        /// pattern) on its own <see cref="KatanaHolster"/>, which sits on the rig's
        /// <see cref="KatanaHolster.OffhandAnchorName"/> child (the left hip, mirroring HipAnchor). Inactive until
        /// RunDirector.ApplyLoadout equips the Twin, so other loadouts pay nothing for it. Idempotent and
        /// additive: safe to run on the shipped RunArena (edit mode only).
        /// </summary>
        public static void EnsureOffhandSword()
        {
            var vrRig = Object.FindAnyObjectByType<VRRig>(FindObjectsInactive.Include);
            var mainHolster = vrRig != null ? vrRig.GetComponent<KatanaHolster>() : null;
            if (mainHolster == null || mainHolster.Sword == null)
            {
                Debug.LogWarning("[RogueEntry] No VRRig KatanaHolster with a sword — skipped the Twin Wakizashi left blade.");
                return;
            }
            RogueEnsurePolearmShield(vrRig.transform); // P9-02c: a rebuild keeps them too
            RogueEnsureWhip(vrRig.transform); // P9-02d
            if (vrRig.transform.Find(KatanaHolster.OffhandAnchorName) != null) return;

            var anchor = new GameObject(KatanaHolster.OffhandAnchorName).transform;
            anchor.SetParent(vrRig.transform, false);
            anchor.localPosition = new Vector3(-0.15f, 0.9f, 0.05f); // HipAnchor mirrored to the left hip

            var left = Object.Instantiate(mainHolster.Sword.gameObject);
            left.name = "SwordLeft";
            left.transform.position += new Vector3(-0.6f, 0f, 0f);
            left.SetActive(false);
            RoguePaintOffhandBlade(left.transform);

            var holster = anchor.gameObject.AddComponent<KatanaHolster>();
            var so = new SerializedObject(holster);
            SetObjectRef(so, "sword", left.GetComponent<Grabbable>());
            SetObjectRef(so, "hipAnchor", anchor);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(vrRig.gameObject.scene);
        }

        private const string RogueCrimsonBladePath = "Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade_Crimson.mat";
        private const string RogueBladeSourcePath = "Assets/Ronin7/Art/Materials/HeroVariants/CyberSword_Blade.mat";

        /// <summary>A15.5: the Double Katana's second blade is deep crimson through its OWN material (a copy of the
        /// katana blade material, same shader), not a runtime tint: ElementImbueController resets the blade from
        /// the material colour. Crimson (blue over green, hue ~350 deg), not the orange-red of enemy telegraphs.</summary>
        private static Material RogueEnsureCrimsonBladeMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(RogueCrimsonBladePath);
            if (mat != null) return mat;
            if (!AssetDatabase.CopyAsset(RogueBladeSourcePath, RogueCrimsonBladePath)) return null;
            mat = AssetDatabase.LoadAssetAtPath<Material>(RogueCrimsonBladePath);
            mat.SetColor("_BaseColor", new Color(0.55f, 0.02f, 0.07f, 1f));
            mat.SetColor("_EmissionColor", new Color(2.4f, 0.05f, 0.2f, 1f)); // HDR, same ~2.5 peak as the cyan original
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>Swaps blade slot 0 (the glowing blade; slot 1 is the shared outline) on <paramref name="sword"/>'s
        /// "Blade" renderer, leaving hilt, guard and kashira alone. Returns true if anything changed.</summary>
        private static bool RoguePaintOffhandBlade(Transform sword)
        {
            var crimson = RogueEnsureCrimsonBladeMaterial();
            var blade = crimson != null ? sword.Find("Sword_Katana/Blade") : null;
            var r = blade != null ? blade.GetComponent<Renderer>() : null;
            if (r == null || r.sharedMaterial == crimson) return false;
            var mats = r.sharedMaterials;
            mats[0] = crimson;
            r.sharedMaterials = mats;
            return true;
        }

        /// <summary>A15.5: gives the shipped RunArena's off-hand sword (SwordLeft) its crimson blade. Additive and
        /// idempotent: opens the scene, swaps one material slot, saves; never rebuilds the scene.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Offhand Crimson Blade (A15.5)", priority = 515)]
        public static void RoguePatchOffhandCrimson()
        {
            var scene = EditorSceneManager.OpenScene(RogueScenePath, OpenSceneMode.Single);
            var anchor = Object.FindAnyObjectByType<VRRig>(FindObjectsInactive.Include)?.transform.Find(KatanaHolster.OffhandAnchorName);
            var holster = anchor != null ? anchor.GetComponent<KatanaHolster>() : null;
            if (holster == null || holster.Sword == null) { Debug.LogWarning("[RogueEntry] No OffhandHolster sword in RunArena."); return; }
            if (!RoguePaintOffhandBlade(holster.Sword.transform)) { Debug.Log("[RogueEntry] Offhand blade already crimson."); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RogueEntry] Offhand blade set to crimson.");
        }

        private const string RogueDarkMatPath = "Assets/Ronin7/Art/Materials/HeroVariants/Gritty_15_15_18_s.mat";
        private const string RogueGreyMatPath = "Assets/Ronin7/Art/Materials/HeroVariants/Gritty_107_117_133_m.mat";

        private static void RoguePrimitive(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // visual only: colliders live on Blade / GripCollider
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Anchor + inactive-to-be Grabbable root with its AttachPoint and GripCollider (the only grabbable volume).</summary>
        private static Grabbable RogueNewGear(Transform rig, string anchorName, Vector3 anchorPos, Vector3 anchorEuler, string gearName,
            Vector3 gripSize, out Transform anchor)
        {
            anchor = new GameObject(anchorName).transform;
            anchor.SetParent(rig, false);
            anchor.localPosition = anchorPos;
            anchor.localEulerAngles = anchorEuler;

            var root = new GameObject(gearName);
            root.transform.SetParent(anchor, false);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.isKinematic = true;
            var attach = new GameObject("AttachPoint");
            attach.transform.SetParent(root.transform, false);
            var grip = new GameObject("GripCollider", typeof(BoxCollider));
            grip.transform.SetParent(root.transform, false);
            var gripBox = grip.GetComponent<BoxCollider>();
            gripBox.isTrigger = true;
            gripBox.size = gripSize;
            var grab = root.AddComponent<Grabbable>();
            var so = new SerializedObject(grab);
            SetObjectRef(so, "attachPoint", attach.transform);
            so.ApplyModifiedPropertiesWithoutUndo();
            return grab;
        }

        private static void RogueAddDamager(Transform parent, string name, Vector3 boxCenter, Vector3 boxSize, WeaponDefinition def)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = boxCenter;
            box.size = boxSize;
            var damager = go.AddComponent<BladeDamager>();
            var so = new SerializedObject(damager);
            SetObjectRef(so, "definitionOverride", def);
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<HeldOnlyBlade>();
        }

        /// <summary>P9-02c / A15.6, A15.7, A15.14: the Polearm and Shield on the rig, placeholder primitives (1 unit = 1 m), both
        /// inactive on their own hip anchors until RunDirector.ApplyLoadout equips them. Polearm: grip at the damager pivot (so
        /// BladeDamager speed is hand speed), box = the front 1.1 m (0.3 to 1.4 m ahead of the hand), the rear is visual.
        /// Shield: its own object (not EW_Shield) with a zero-damage BladeDamager. Idempotent; true if it built anything.</summary>
        internal static bool RogueEnsurePolearmShield(Transform rig)
        {
            var gear = rig.GetComponent<PolearmShieldGear>();
            if (gear != null && gear.Complete) return false;
            if (gear == null) gear = rig.gameObject.AddComponent<PolearmShieldGear>();
            var dark = AssetDatabase.LoadAssetAtPath<Material>(RogueDarkMatPath);
            var grey = AssetDatabase.LoadAssetAtPath<Material>(RogueGreyMatPath);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(RogueBladeSourcePath);
            var polearmDef = EnsureLoadoutDefinitions()[4];
            var shieldDef = EnsurePlayerShieldDefinition();

            // Anchors on the right-handed baseline; SetMirrored flips position and rotation for left-handed.
            // Pole diagonal with +z (the head) up and behind the shoulder; shield flat on the off hip, face outward.
            var polearm = RogueNewGear(rig, "PolearmAnchor", new Vector3(0.15f, 0.9f, 0.05f), new Vector3(-135f, 10f, 0f),
                "Polearm", new Vector3(0.1f, 0.1f, 0.2f), out var polearmAnchor);
            var pv = new GameObject("PolearmVisual").transform;
            pv.SetParent(polearm.transform, false);
            RoguePrimitive(pv, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.425f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.775f, 0.03f), dark);
            RoguePrimitive(pv, "Butt", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.35f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), grey);
            RoguePrimitive(pv, "Head", PrimitiveType.Cube, new Vector3(0f, 0f, 1.29f), Vector3.zero, new Vector3(0.07f, 0.012f, 0.22f), glow);
            RogueAddDamager(polearm.transform, "Blade", new Vector3(0f, 0f, 0.85f), new Vector3(0.06f, 0.06f, 1.1f), polearmDef);
            polearm.gameObject.AddComponent<Sword>().Definition = polearmDef;
            polearm.gameObject.SetActive(false);

            var shield = RogueNewGear(rig, "ShieldAnchor", new Vector3(-0.2f, 0.9f, 0f), new Vector3(0f, -90f, 0f),
                "PlayerShield", new Vector3(0.1f, 0.1f, 0.1f), out var shieldAnchor);
            var sv = new GameObject("ShieldVisual").transform;
            sv.SetParent(shield.transform, false);
            RoguePrimitive(sv, "Disc", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.12f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, 0.015f, 0.5f), grey);
            RoguePrimitive(sv, "Boss", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.14f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.06f), glow);
            RogueAddDamager(shield.transform, "ShieldFace", new Vector3(0f, 0f, 0.12f), new Vector3(0.5f, 0.5f, 0.04f), shieldDef);
            shield.gameObject.SetActive(false);

            var so = new SerializedObject(gear);
            SetObjectRef(so, "polearm", polearm);
            SetObjectRef(so, "polearmAnchor", polearmAnchor);
            SetObjectRef(so, "shield", shield);
            SetObjectRef(so, "shieldAnchor", shieldAnchor);
            so.ApplyModifiedPropertiesWithoutUndo();
            WeaponMeshFit.ApplyTo(gear); // P9-04: Tripo visuals
            return true;
        }

        /// <summary>P9-02d / A15.8, A15.14: the Whip on the rig, placeholder primitives (1 unit = 1 m), inactive on the main-hand hip
        /// until RunDirector.ApplyLoadout equips it. The handle's BladeDamager (0 damage, no parry) only gives Element Imbue its sweep
        /// and bolts a collider to pass through. The chain is one LineRenderer on the shared chain material, with no collider.
        /// Idempotent; true if it built anything.</summary>
        internal static bool RogueEnsureWhip(Transform rig)
        {
            var gear = rig.GetComponent<PolearmShieldGear>();
            if (gear != null && gear.WhipComplete) return false;
            if (gear == null) gear = rig.gameObject.AddComponent<PolearmShieldGear>();
            var dark = AssetDatabase.LoadAssetAtPath<Material>(RogueDarkMatPath);
            var grey = AssetDatabase.LoadAssetAtPath<Material>(RogueGreyMatPath);
            var glow = AssetDatabase.LoadAssetAtPath<Material>(RogueBladeSourcePath);
            var def = EnsureLoadoutDefinitions()[5];

            // Same hip as the katana, ring end forward and down. Handle runs -0.1 to 0.2 m along +z, ring at its front.
            var whip = RogueNewGear(rig, "WhipAnchor", new Vector3(0.15f, 0.9f, 0.05f), new Vector3(60f, 0f, 0f),
                "Whip", new Vector3(0.1f, 0.1f, 0.2f), out var whipAnchor);
            var hv = new GameObject("WhipVisual").transform;
            hv.SetParent(whip.transform, false);
            RoguePrimitive(hv, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.05f), new Vector3(90f, 0f, 0f), new Vector3(0.035f, 0.15f, 0.035f), dark);
            var ring = new GameObject("ChainRing").transform;
            ring.SetParent(whip.transform, false);
            ring.localPosition = new Vector3(0f, 0f, 0.22f);
            RoguePrimitive(ring, "Ring", PrimitiveType.Sphere, Vector3.zero, Vector3.zero, new Vector3(0.05f, 0.05f, 0.015f), glow);
            RoguePrimitive(hv, "Coil", PrimitiveType.Cylinder, new Vector3(0f, -0.06f, 0.1f), Vector3.zero, new Vector3(0.18f, 0.03f, 0.18f), grey);
            RogueAddDamager(whip.transform, "Blade", new Vector3(0f, 0f, 0.05f), new Vector3(0.04f, 0.04f, 0.3f), def);
            whip.gameObject.AddComponent<Sword>().Definition = def;

            var lineGo = new GameObject("ChainLine", typeof(LineRenderer));
            lineGo.transform.SetParent(whip.transform, false);
            var lr = lineGo.GetComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = VerletChain.DefaultPoints;
            lr.startWidth = 0.025f;
            lr.endWidth = 0.012f;
            lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ChainLineMaterialPath);
            lr.startColor = lr.endColor = new Color(0.4f, 0.9f, 1f, 1f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.enabled = false;

            var controller = whip.gameObject.AddComponent<WhipController>();
            var cso = new SerializedObject(controller);
            SetObjectRef(cso, "chainRing", ring);
            SetObjectRef(cso, "chainLine", lr);
            SetObjectRef(cso, "coil", hv.Find("Coil").gameObject);
            cso.ApplyModifiedPropertiesWithoutUndo();
            whip.gameObject.SetActive(false);

            var so = new SerializedObject(gear);
            SetObjectRef(so, "whip", whip);
            SetObjectRef(so, "whipAnchor", whipAnchor);
            so.ApplyModifiedPropertiesWithoutUndo();
            WeaponMeshFit.ApplyTo(gear); // P9-04: Tripo visuals
            return true;
        }

        /// <summary>P9-02d: additive, idempotent. RunArena gets the Whip rig objects; Phase6_Boot's RunDirector.weapons gets
        /// WhipHandle. Never rebuilds a scene. Ends with Phase6_Boot open.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Whip (P9-02d)", priority = 517)]
        public static void RoguePatchWhip()
        {
            var scene = EditorSceneManager.OpenScene(RogueScenePath, OpenSceneMode.Single);
            var rig = Object.FindAnyObjectByType<VRRig>(FindObjectsInactive.Include);
            if (rig == null) { Debug.LogWarning("[RogueEntry] No VRRig in RunArena."); return; }
            if (RogueEnsureWhip(rig.transform))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[RogueEntry] Whip added to RunArena.");
            }
            else Debug.Log("[RogueEntry] RunArena already has the Whip.");
            RoguePatchBootWeapons();
        }

        /// <summary>P9-02c: additive, idempotent patch of the shipped scenes. RunArena gets the Polearm and Shield rig objects;
        /// Phase6_Boot's RunDirector.weapons gets the Polearm definition. Never rebuilds a scene. Ends with Phase6_Boot open.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Polearm + Shield (P9-02c)", priority = 516)]
        public static void RoguePatchPolearmShield()
        {
            var scene = EditorSceneManager.OpenScene(RogueScenePath, OpenSceneMode.Single);
            var rig = Object.FindAnyObjectByType<VRRig>(FindObjectsInactive.Include);
            if (rig == null) { Debug.LogWarning("[RogueEntry] No VRRig in RunArena."); return; }
            if (RogueEnsurePolearmShield(rig.transform))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[RogueEntry] Polearm + Shield added to RunArena.");
            }
            else Debug.Log("[RogueEntry] RunArena already has the Polearm + Shield.");
            RoguePatchBootWeapons();
        }

        /// <summary>P9-04 (A15.16): additive, idempotent. Swaps the Polearm, Shield and Whip placeholder VISUALS in RunArena for the
        /// Tripo meshes (gameplay transforms and colliders untouched). Never rebuilds a scene. Ends with Phase6_Boot open.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Weapon Meshes (P9-04)", priority = 518)]
        public static void RoguePatchWeaponMeshes()
        {
            var scene = EditorSceneManager.OpenScene(RogueScenePath, OpenSceneMode.Single);
            var gear = Object.FindAnyObjectByType<PolearmShieldGear>(FindObjectsInactive.Include);
            if (gear == null) { Debug.LogWarning("[RogueEntry] No PolearmShieldGear in RunArena."); return; }
            if (WeaponMeshFit.ApplyTo(gear))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[RogueEntry] Tripo weapon meshes applied to RunArena.");
            }
            else Debug.Log("[RogueEntry] RunArena already has the Tripo weapon meshes.");
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
        }

        private static void RoguePatchBootWeapons()
        {
            var boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<RunDirector>(FindObjectsInactive.Include);
            if (director == null) { Debug.LogWarning("[RogueEntry] No RunDirector in the boot scene."); return; }
            var so = new SerializedObject(director);
            var prop = so.FindProperty("weapons");
            var defs = EnsureLoadoutDefinitions();
            bool same = prop.arraySize == defs.Length;
            for (int i = 0; same && i < defs.Length; i++) same = prop.GetArrayElementAtIndex(i).objectReferenceValue == defs[i];
            if (same) { Debug.Log("[RogueEntry] RunDirector.weapons is already current."); return; }
            SetObjectRefArray(so, "weapons", defs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);
            Debug.Log("[RogueEntry] RunDirector.weapons updated.");
        }

        private static void RogueEnsureRunDirectorInBootScene()
        {
            var bootScene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            if (!bootScene.IsValid())
            {
                Debug.LogError($"[RoguelikeArenaBuilder] Could not open boot scene '{BootScenePath}' to " +
                                "place RunDirector. Build it via Tools/Space Samurai/Build Phase 6 Boot " +
                                "Scene first, then re-run this menu.");
                return;
            }

            var gameGo = GameObject.Find("Game");
            if (gameGo == null || gameGo.GetComponent<GameFlowManager>() == null)
            {
                Debug.LogError("[RoguelikeArenaBuilder] Boot scene has no 'Game' GameObject with a " +
                                "GameFlowManager — cannot place RunDirector. Build the boot scene first.");
                return;
            }

            var runDirector = gameGo.GetComponent<RunDirector>();
            if (runDirector == null) runDirector = gameGo.AddComponent<RunDirector>(); // appends after GameFlowManager

            var catalog = RogueLoadOptional<BoonCatalog>(RogueBoonCatalogPath, "BoonCatalog");
            var runDirectorSo = new SerializedObject(runDirector);
            SetObjectRef(runDirectorSo, "boonCatalog", catalog);
            SetObjectRefArray(runDirectorSo, "weapons", EnsureLoadoutDefinitions()); // P4-08
            // P2-04: Event-node pool (authored by RunEventBuilder).
            var eventGuids = AssetDatabase.FindAssets("t:RunEventDefinition", new[] { RunEventBuilder.EventsFolder });
            var eventsProp = runDirectorSo.FindProperty("runEvents");
            eventsProp.arraySize = eventGuids.Length;
            for (int i = 0; i < eventGuids.Length; i++)
                eventsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<RunEventDefinition>(AssetDatabase.GUIDToAssetPath(eventGuids[i]));
            runDirectorSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(bootScene);
            EditorSceneManager.SaveScene(bootScene);

            Debug.Log($"[RoguelikeArenaBuilder] RunDirector wired into '{BootScenePath}' on 'Game' " +
                      (catalog != null
                          ? "with the BoonCatalog assigned."
                          : "— BoonCatalog MISSING; bake it via its Module 5 builder menu, then re-run " +
                            "this menu so runs actually offer boons."));
        }

        /// <summary>Loads an authored Module 5 asset by its fixed path; logs a clear warning and returns
        /// null (never throws) when it hasn't been baked yet, so a partial build order never aborts the
        /// whole scene.</summary>
        private static T RogueLoadOptional<T>(string path, string label) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                Debug.LogWarning($"[RoguelikeArenaBuilder] No {label} found at '{path}' — leaving the " +
                                  "reference null. Bake it via its Module 5 builder menu item, then re-run this menu.");
            return asset;
        }

        /// <summary>P4-05: the boon proc component (bleed / storm / parry procs) plus its pooled chain line
        /// on the rig root. Idempotent and additive, like <see cref="EnsureSkills"/>.</summary>
        internal static void EnsureBoonProcs(GameObject rig)
        {
            var procs = EnsureComponent<RunBoonProcs>(rig);
            var lineT = rig.transform.Find("BoonProcLine");
            if (lineT == null) { lineT = new GameObject("BoonProcLine").transform; lineT.SetParent(rig.transform, false); }
            var lr = lineT.GetComponent<LineRenderer>();
            if (lr == null) lr = lineT.gameObject.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.02f;
            lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ChainLineMaterialPath);
            lr.startColor = lr.endColor = new Color(0.55f, 0.75f, 1f, 1f);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.enabled = false;
            var so = new SerializedObject(procs);
            SetObjectRef(so, "procLine", lr);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(procs);
        }
    }
}
