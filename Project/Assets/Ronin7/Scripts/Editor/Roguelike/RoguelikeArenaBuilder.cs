using Ronin7.Combat;
using Ronin7.Flow;
using Ronin7.Player;
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
            light.intensity = 0.4f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.12f);
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
            var rig = BuildRig(refs, addLocomotion: true);
            AttachPlayerAbilities(rig, refs);
            if (rig.GetComponent<ParryFlowController>() == null) rig.AddComponent<ParryFlowController>();
            if (rig.GetComponent<ComboMomentumController>() == null) rig.AddComponent<ComboMomentumController>();
            BuildSword(new Vector3(0.3f, 1f, 1.6f), weapon);
            RogueUpscaleSword();

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
            if (halfExtentProp != null) halfExtentProp.floatValue = ArenaGeometryBuilder.DefaultHalfExtent;
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
                      $", halfExtent={ArenaGeometryBuilder.DefaultHalfExtent}. Room geometry/enemies build " +
                      "at runtime per node — no static walls authored here.");

            RogueEnsureRunDirectorInBootScene();
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
    }
}
