using Ronin7.Editor.Art;
using Ronin7.Player;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Parkour level work (cycle 5): the Ch06 Iron Dojo summit route — an additive patch that makes
    /// the dojo climbable "all the way to the top" with the WallClimbLocomotion kit. Terrace decks,
    /// terrace rocks and the bell tower become Climbable (the mountain face turns into a legitimate
    /// climbing shortcut beside the ramps), and a new hold ladder up the Morrigan Spine's yard-facing
    /// wall reaches a summit deck ABOVE the dojo roofline (y≈15.8, the previous ceiling was the
    /// unreachable 15.6 wall crest) crowned with a summit bell.
    ///
    /// Two-track per the destructive-rebuild rule: the MenuItem patches the SHIPPED scene additively
    /// (idempotent — re-running skips existing pieces), and Chapter6Builder's rebuild path calls the
    /// same <see cref="AddDojoSummitRoute"/> so a future rebuild stays correct.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string SummitRouteName = "SummitRoute";

        [MenuItem("Tools/Space Samurai/Chapters/Patch Ch06 Summit Route (additive)", priority = 120)]
        public static void PatchCh06SummitRoute()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Ronin7/Scenes/Ch06_IronDojo.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var dojo = GameObject.Find("IronDojo");
            if (dojo == null) { Debug.LogError("[ParkourLevel] IronDojo root not found."); return; }

            AddDojoSummitRoute(dojo.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[ParkourLevel] Ch06 summit route patched + saved.");
        }

        /// <summary>Idempotent: safe on a scene that already has the route (or on a fresh rebuild).</summary>
        internal static void AddDojoSummitRoute(Transform dojoRoot)
        {
            // 1) The mountain face becomes climbable: terrace decks, their rocks, the bell tower.
            // Deep search: the ascent lives under intermediate nodes (Ascent/UpperCitadel), not as
            // direct children of the dojo root.
            foreach (var t in dojoRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool climbTarget = n == "BellTower"
                    || (n.StartsWith("Terrace") && !n.Contains("_")) // Terrace0..4 decks
                    || (n.StartsWith("Terrace") && n.Contains("_Rock"));
                if (climbTarget && t.GetComponent<Climbable>() == null && t.GetComponent<Collider>() != null)
                    t.gameObject.AddComponent<Climbable>();
            }

            // 2) Summit hold ladder + deck + bell.
            if (dojoRoot.Find(SummitRouteName) != null) return; // already patched

            var route = new GameObject(SummitRouteName);
            route.transform.SetParent(dojoRoot, false);

            var holdColor = new Color(0.55f, 0.42f, 0.28f); // worn wood pegs against the dark wall
            // Ladder up MorriganSpine_WallN_A's yard face (wall plane z=100, gate opening spans
            // x -1.55..1.55 — the ladder stays on the solid west segment).
            Vector3[] holds =
            {
                new Vector3(-3.5f, 12.70f, 100.22f),
                new Vector3(-2.7f, 13.35f, 100.22f),
                new Vector3(-3.5f, 14.00f, 100.22f),
                new Vector3(-2.7f, 14.65f, 100.22f),
                new Vector3(-3.5f, 15.30f, 100.22f),
            };
            for (int i = 0; i < holds.Length; i++)
                BuildClimbProp(route.transform, $"SummitHold{i}", holds[i], new Vector3(0.4f, 0.22f, 0.25f), holdColor);

            // Deck above the Morrigan Spine roofline — THE top of the dojo. Climbable so the final
            // move is grab-edge-and-mantle from the last hold.
            BuildClimbProp(route.transform, "SummitDeck", new Vector3(-3.1f, 15.7f, 98.0f),
                new Vector3(5.0f, 0.2f, 4.0f), new Color(0.32f, 0.26f, 0.2f));

            // Summit bell: the reward read at the top (visual only, primitive-collider safe).
            var bell = BuildClimbProp(route.transform, "SummitBell", new Vector3(-3.1f, 16.15f, 97.0f),
                new Vector3(0.55f, 0.4f, 0.55f), new Color(0.75f, 0.6f, 0.25f), PrimitiveType.Cylinder, climbable: false);
            BuildClimbProp(route.transform, "SummitBellPost", new Vector3(-3.1f, 16.6f, 97.0f),
                new Vector3(0.08f, 0.5f, 0.08f), new Color(0.3f, 0.28f, 0.26f), climbable: false);
        }

        /// <summary>Primitive prop with its collider kept (climbing needs it) and an optional
        /// <see cref="Climbable"/> marker. Mirrors the chapter builders' BuildProp styling.</summary>
        private static GameObject BuildClimbProp(Transform parent, string name, Vector3 pos, Vector3 scale,
            Color color, PrimitiveType type = PrimitiveType.Cube, bool climbable = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            TintShared(go.GetComponent<Renderer>(), color);
            if (climbable) go.AddComponent<Climbable>();
            return go;
        }

        // ==================== Parkour Grounds (dedicated training annex scene) ====================

        private const string ParkourScenePath = SceneFolder + "/ParkourGrounds.unity";
        private static readonly Color TierEasy = new Color(0.35f, 0.7f, 0.4f);
        private static readonly Color TierMedium = new Color(0.85f, 0.65f, 0.3f);
        private static readonly Color TierHard = new Color(0.8f, 0.32f, 0.3f);
        private static readonly Color Structure = new Color(0.34f, 0.36f, 0.4f);

        /// <summary>
        /// Builds the dedicated parkour training scene: three tiered courses (easy jumps + low
        /// climb, medium tower + sky bridge + wall run, hard wall-jump chimney + transfer chain +
        /// 16m summit spire) on one large slab. Reached from a hub console; both legs travel via
        /// StoryTransition.LoadOnFootScene (LandingRequested) — NEVER ZoneCompleted, which would
        /// credit the annex as a completed campaign mission (see GameFlowManager/CampaignState).
        /// Greybox in the house style; primitive colliders only.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Chapters/Build Parkour Grounds", priority = 121)]
        public static void BuildParkourGrounds()
        {
            if (!TryLoadInputRefs(out var refs)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Lighting: cool pre-dawn training light, tier accents do the wayfinding. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.8f, 0.95f);
            light.intensity = 1.0f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.16f, 0.18f, 0.24f);
            RenderSettings.fogDensity = 0.012f;
            BuildAccentPointLight("EasyLight", new Vector3(-28f, 4f, 40f), TierEasy, 1.4f, 18f);
            BuildAccentPointLight("MediumLight", new Vector3(0f, 10f, 45f), TierMedium, 1.4f, 20f);
            BuildAccentPointLight("HardLight", new Vector3(28f, 14f, 60f), TierHard, 1.4f, 22f);

            var worldGo = new GameObject("ParkourGrounds");
            var world = worldGo.transform;

            // ---- Ground slab: everything happens above one safe floor (no kill pits). ----
            BuildClimbProp(world, "Ground", new Vector3(0f, -0.2f, 60f), new Vector3(90f, 0.4f, 150f),
                new Color(0.2f, 0.21f, 0.25f), climbable: false);

            BuildEasyCourse(world);
            BuildMediumCourse(world);
            BuildHardCourse(world);

            // ---- Return console at the spawn plaza (LandingRequested back to the hub). ----
            var returnGo = BuildTransitionBox("ReturnConsole", new Vector3(-4f, 1.2f, 6f), "RETURN TO THE CAIRN",
                out var returnBtn, out var returnTransition);
            returnGo.transform.SetParent(world, true);
            var rtSo = new SerializedObject(returnTransition);
            rtSo.FindProperty("onFootScene").stringValue = "Galaxy1_Ch1_Hub";
            rtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(returnBtn.onClick,
                new UnityEngine.Events.UnityAction(returnTransition.LoadOnFootScene));

            // ---- Game root + rig + bounds (mission-scene scaffold minus MissionDirector). ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<Ronin7.Core.GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            BuildRig(refs, addLocomotion: true);
            var boundsGo = new GameObject("ZoneBounds");
            var bounds = boundsGo.AddComponent<Ronin7.World.ZoneBounds>();
            var boundsSo = new SerializedObject(bounds);
            var centerProp = boundsSo.FindProperty("center");
            if (centerProp != null) centerProp.vector3Value = new Vector3(0f, 8f, 60f);
            var radiusProp = boundsSo.FindProperty("radius");
            if (radiusProp != null) radiusProp.floatValue = 130f;
            boundsSo.ApplyModifiedPropertiesWithoutUndo();

            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            BuildAmbienceLayer("ParkourWindAmbience", new Vector3(0f, 6f, 60f), 20f, 90f, 0.35f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, ParkourScenePath);
            EnsureScenesInBuild(ParkourScenePath);
            Debug.Log($"[Space Samurai] Parkour Grounds built at {ParkourScenePath}: easy jumps/climb, " +
                      "medium tower + sky bridge + wall run, hard chimney + transfer + 16m summit spire.");
        }

        /// <summary>Tier 1 (green, x≈-28): stepped jump line, a 4m hold wall, a ground-level wall run.</summary>
        private static void BuildEasyCourse(Transform world)
        {
            var root = new GameObject("EasyCourse");
            root.transform.SetParent(world, false);
            var t = root.transform;

            // Jump line: five pads rising then falling, 1.2m gaps — pure stick-jump practice.
            float[] padY = { 0.6f, 1.0f, 1.4f, 1.0f, 0.6f };
            for (int i = 0; i < padY.Length; i++)
                BuildClimbProp(t, $"JumpPad{i}", new Vector3(-28f, padY[i] - 0.2f, 18f + i * 3.2f),
                    new Vector3(2f, 0.4f, 2f), TierEasy, climbable: false);

            // Low climb wall: 4m face with five fat holds, topped by a walk-off deck.
            BuildClimbProp(t, "ClimbWall", new Vector3(-28f, 2f, 38f), new Vector3(6f, 4f, 0.4f), Structure, climbable: false);
            for (int i = 0; i < 5; i++)
                BuildClimbProp(t, $"WallHold{i}", new Vector3(-28f + (i % 2 == 0 ? -0.6f : 0.6f), 0.7f + i * 0.65f, 37.7f),
                    new Vector3(0.5f, 0.25f, 0.3f), TierEasy);
            BuildClimbProp(t, "TopDeck", new Vector3(-28f, 3.8f, 40.6f), new Vector3(6f, 0.4f, 4f), Structure);

            // Wall run: hop pad → 12m wall on the left → landing pad. Ground below stays safe.
            BuildClimbProp(t, "RunStart", new Vector3(-28f, 0.6f, 46f), new Vector3(2f, 0.4f, 2f), TierEasy, climbable: false);
            BuildClimbProp(t, "RunWall", new Vector3(-29.6f, 1.8f, 52.5f), new Vector3(0.3f, 2.6f, 12f), Structure, climbable: false);
            BuildClimbProp(t, "RunEnd", new Vector3(-28f, 0.6f, 59f), new Vector3(2f, 0.4f, 2f), TierEasy, climbable: false);
            BuildClimbProp(t, "FinishPost", new Vector3(-28f, 1.4f, 64f), new Vector3(0.25f, 2.8f, 0.25f), TierEasy, climbable: false);
        }

        /// <summary>Tier 2 (amber, x≈0): a 9m hold tower, a gap-jump sky bridge, a high wall run.</summary>
        private static void BuildMediumCourse(Transform world)
        {
            var root = new GameObject("MediumCourse");
            root.transform.SetParent(world, false);
            var t = root.transform;

            // Hold tower: 9m spire, ladder of 11 holds on the south face, deck on top.
            BuildClimbProp(t, "Tower", new Vector3(0f, 4.5f, 30f), new Vector3(3f, 9f, 3f), Structure, climbable: false);
            for (int i = 0; i < 11; i++)
                BuildClimbProp(t, $"TowerHold{i}", new Vector3(i % 2 == 0 ? -0.6f : 0.6f, 0.8f + i * 0.72f, 28.3f),
                    new Vector3(0.45f, 0.22f, 0.3f), TierMedium);
            BuildClimbProp(t, "TowerDeck", new Vector3(0f, 9.2f, 30f), new Vector3(4f, 0.4f, 4f), Structure);

            // Sky bridge: three floating pads with ~1.8m gaps at 9m — jumps with real exposure.
            for (int i = 0; i < 3; i++)
                BuildClimbProp(t, $"SkyPad{i}", new Vector3(0f, 8.8f, 35.5f + i * 4.3f), new Vector3(2.5f, 0.4f, 2.5f), TierMedium);

            // High wall run: 14m wall bridges the long gap to the landing deck.
            BuildClimbProp(t, "HighRunWall", new Vector3(2.2f, 10.3f, 52f), new Vector3(0.3f, 3f, 14f), Structure, climbable: false);
            BuildClimbProp(t, "LandingDeck", new Vector3(0f, 8.4f, 60.5f), new Vector3(4f, 0.4f, 4f), Structure);

            // Descent stack back to ground + finish.
            for (int i = 0; i < 3; i++)
                BuildClimbProp(t, $"DescentPad{i}", new Vector3(0f, 6f - i * 2.6f, 64f + i * 3f), new Vector3(2.5f, 0.4f, 2.5f), TierMedium, climbable: false);
            BuildClimbProp(t, "FinishPost", new Vector3(0f, 1.4f, 74f), new Vector3(0.25f, 2.8f, 0.25f), TierMedium, climbable: false);
        }

        /// <summary>Tier 3 (red, x≈+28): wall-jump chimney, exposed ridge, wall-run transfer, 16m summit.</summary>
        private static void BuildHardCourse(Transform world)
        {
            var root = new GameObject("HardCourse");
            root.transform.SetParent(world, false);
            var t = root.transform;

            // Chimney: two facing walls 1.6m apart; sparse alternating holds force hand-over-hand
            // switching between faces all the way to 11m.
            BuildClimbProp(t, "ChimneyWallW", new Vector3(26.4f, 6f, 22f), new Vector3(0.3f, 12f, 5f), Structure, climbable: false);
            BuildClimbProp(t, "ChimneyWallE", new Vector3(29.6f, 6f, 22f), new Vector3(0.3f, 12f, 5f), Structure, climbable: false);
            for (int i = 0; i < 11; i++)
            {
                bool west = i % 2 == 0;
                BuildClimbProp(t, $"ChimneyHold{i}", new Vector3(west ? 26.75f : 29.25f, 0.9f + i * 1.0f, 21f + (i % 3) * 0.9f),
                    new Vector3(0.3f, 0.2f, 0.35f), TierHard);
            }
            BuildClimbProp(t, "ChimneyExit", new Vector3(28f, 11.6f, 25.5f), new Vector3(3f, 0.4f, 3f), Structure);

            // Exposed ridge: small pads, 2.2m gaps, at 11.6m — run-up jumps under pressure.
            for (int i = 0; i < 3; i++)
                BuildClimbProp(t, $"RidgePad{i}", new Vector3(28f, 11.6f, 30f + i * 3.7f), new Vector3(1.5f, 0.4f, 1.5f), TierHard);

            // Wall-run transfer: right-side wall, then a LEFT-side wall offset across the line —
            // the player must wall-jump off the first to reach the second, then land on the deck.
            BuildClimbProp(t, "TransferWallA", new Vector3(30.2f, 12.6f, 46f), new Vector3(0.3f, 3f, 10f), Structure, climbable: false);
            BuildClimbProp(t, "TransferWallB", new Vector3(25.8f, 12.6f, 58f), new Vector3(0.3f, 3f, 10f), Structure, climbable: false);
            BuildClimbProp(t, "TransferDeck", new Vector3(28f, 11.2f, 66f), new Vector3(3.5f, 0.4f, 3.5f), Structure);

            // Summit spire: sparse 0.85m holds with an overhang shelf to negotiate, topping out at 16m.
            BuildClimbProp(t, "Spire", new Vector3(28f, 13.5f, 76f), new Vector3(2.5f, 9f, 2.5f), Structure, climbable: false);
            for (int i = 0; i < 6; i++)
                BuildClimbProp(t, $"SpireHold{i}", new Vector3(28f + (i % 2 == 0 ? -0.5f : 0.5f), 11.7f + i * 0.85f, 74.6f),
                    new Vector3(0.35f, 0.2f, 0.3f), TierHard);
            BuildClimbProp(t, "OverhangShelf", new Vector3(28f, 15.6f, 75.2f), new Vector3(2f, 0.3f, 1.6f), TierHard);
            BuildClimbProp(t, "SummitPlatform", new Vector3(28f, 16.2f, 76f), new Vector3(3f, 0.4f, 3f), Structure);
            BuildClimbProp(t, "SummitBeacon", new Vector3(28f, 17.2f, 76f), new Vector3(0.3f, 1.6f, 0.3f), TierHard, climbable: false);
        }

        /// <summary>
        /// Additive hub patch: a "PARKOUR GROUNDS" console beside the mission console, gated under
        /// HubRoot (hub mode only). Travels via StoryTransition.LoadOnFootScene — see
        /// <see cref="BuildParkourGrounds"/> for why the annex never uses ZoneCompleted.
        /// </summary>
        [MenuItem("Tools/Space Samurai/Chapters/Patch Hub Parkour Console (additive)", priority = 122)]
        public static void PatchHubParkourConsole()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/Ronin7/Scenes/Galaxy1_Ch1_Hub.unity", OpenSceneMode.Single);
            // HubRoot is serialized INACTIVE (HubStateController flips it at runtime), so
            // GameObject.Find can't see it — walk the scene roots instead.
            var hubRoot = FindInactiveRoot("HubRoot");
            if (hubRoot == null) { Debug.LogError("[ParkourLevel] HubRoot not found in hub scene."); return; }
            if (hubRoot.transform.Find("ParkourConsole") != null)
            {
                Debug.Log("[ParkourLevel] Hub parkour console already present — nothing to do.");
                return;
            }

            AddHubParkourConsole(hubRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ParkourLevel] Hub parkour console patched + saved.");
        }

        /// <summary>Shared by the additive hub patch and (for future rebuilds) BuildHubMode.</summary>
        internal static void AddHubParkourConsole(GameObject hubRoot)
        {
            var consoleGo = BuildTransitionBox("ParkourConsole", new Vector3(9f, 1.2f, 30f), "PARKOUR GROUNDS",
                out var btn, out var transition);
            consoleGo.transform.SetParent(hubRoot.transform, true);
            var so = new SerializedObject(transition);
            so.FindProperty("onFootScene").stringValue = "ParkourGrounds";
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(btn.onClick,
                new UnityEngine.Events.UnityAction(transition.LoadOnFootScene));
        }

        private static GameObject FindInactiveRoot(string name)
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name) return go;
                var child = go.transform.Find(name);
                if (child != null) return child.gameObject;
            }
            return null;
        }
    }
}
