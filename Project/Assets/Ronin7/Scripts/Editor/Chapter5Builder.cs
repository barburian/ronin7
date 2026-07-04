using Ronin7.Core;
using Ronin7.Player;
using Ronin7.World;
using Ronin7.World.Story;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Chapter 5 ("The Debt of Ashes") scene builder. Opens Act II. One scene, a single ash-world run on
    /// a +Z line: the landing site (ash palette, ember accents) -> the settlement ruins (collapsed wall
    /// shells, scavenger set-dressing, no combat) -> the mass grave (Vera Dusk's accusation, Kira named)
    /// -> a <see cref="MemoryDiveController"/> dive into the katana's recording of the massacre (offset
    /// far up +Z, still ghost silhouettes only, NO sentinels and NO violence depicted, resolving on the
    /// hesitation beat) -> back at the grave for the "Kael Vor" reveal (Iris/Mera over comm, Ladder C
    /// rung 1) and the reckoning (Vera refuses his life, passes a harder sentence, releases him). No
    /// roster boss; the antagonist is grief, matching Ch3's no-combat precedent. Vera Dusk has no
    /// real mesh yet (PlaceholderCharacterBuilder's ash-grey greybox at Named/Vera-Dusk.prefab); Kira
    /// Dusk's real mesh (Named/Kira-Dusk.prefab) appears only inside the dive, ghost-tinted.
    ///
    /// Lives in the same <see cref="XRRigBuilder"/> partial class as <c>ChapterSharedBuilders</c> so it
    /// reuses their geometry/dialogue/mission-step helpers directly. All chapter-local helpers are
    /// prefixed <c>Ch5</c>.
    ///
    /// CREW-PRESENCE DECISION: canon splits the landing party (Kessler, Resh, Mira go down; Iris and
    /// Mera Voss stay aboard the Cairn on an open comm-line). Rather than build a separate ship interior
    /// for a two-line presence, Iris and Mera Voss are voice-only here (DialoguePlayer speaker labels,
    /// no physical NPC) — the same "no body in the scene" convention Echo already uses. Kessler, Resh,
    /// and Mira get physical StoryNpc placement at the landing site as the landing party.
    ///
    /// SCAVENGERS: canon's Game Design section calls scavengers "light, optional, atmospheric... texture,
    /// not enemy" and explicitly not what the chapter is about. No Enemy/DefeatWaves step is built for
    /// them — they are set-dressing capsule silhouettes only in <see cref="Ch5BuildSettlementRuins"/>,
    /// matching Ch3's precedent of zero combat in a story-heavy chapter.
    ///
    /// ASH PARTICLES: no reusable "drifting ash" particle helper exists in ChapterSharedBuilders or
    /// Editor/Art (VfxPrefabBuilder only generates combat-hit VFX prefabs) — skipped rather than adding
    /// new particle infrastructure for one chapter.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string Ch5ScenePath = SceneFolder + "/Ch05_DebtOfAshes.unity";
        private const string Ch5VoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        // Named-cast prefabs (Tripo image->3D pipeline, grounded via FitNamedCharacter — same
        // convention Chapter3/Chapter4 use). Vera Dusk resolves to her PlaceholderCharacterBuilder
        // greybox stub; Kira Dusk has a real baked mesh but appears only inside the dive.
        private const string Ch5KesslerPrefab  = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab";
        private const string Ch5ReshPrefab     = "Assets/Ronin7/Art/Generated/Characters3D/Named/Resh.prefab";
        private const string Ch5MiraPrefab     = "Assets/Ronin7/Art/Generated/Characters3D/Named/Mira.prefab";
        private const string Ch5VeraDuskPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Vera-Dusk.prefab";
        private const string Ch5KiraDuskPrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Kira-Dusk.prefab";
        private const string Ch5EchoBladePrefab = "Assets/Ronin7/Art/Generated/Characters3D/Named/Echo.prefab";

        [MenuItem("Tools/Space Samurai/Chapters/Build Chapter 05 — The Debt of Ashes", priority = 205)]
        public static void BuildChapter5DebtOfAshes()
        {
            if (!TryLoadInputRefs(out var refs)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Definition assets must be loaded AFTER NewScene: scene creation unloads unused assets,
            // so references held across it go fake-null and serialize as {fileID: 0}.
            var weapon = EnsureWeaponDefinition();

            // ---- Lighting: an overcast ash-world sky. Dim grey-white key, low grey ambient, warm
            // ember accents standing in for the only heat left in the place. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.74f, 0.72f);
            light.intensity = 0.6f;
            lightGo.transform.rotation = Quaternion.Euler(60f, -40f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.15f, 0.15f);

            // Pale ash-gray overcast: a dense, sourceless pall standing in for open sky over the
            // whole exterior run (landing site, ruins, mass grave).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.5f, 0.48f, 0.46f);
            RenderSettings.fogDensity = 0.042f;

            BuildAccentPointLight("LandingEmber0", new Vector3(-4f, 1.6f, 6f), new Color(1f, 0.45f, 0.2f), 1.2f, 10f);
            BuildAccentPointLight("LandingEmber1", new Vector3(4f, 1.6f, 10f), new Color(1f, 0.5f, 0.25f), 1.2f, 10f);
            BuildAccentPointLight("RuinsEmber0", new Vector3(-5f, 1.8f, 26f), new Color(0.9f, 0.4f, 0.15f), 1.4f, 12f);
            BuildAccentPointLight("RuinsEmber1", new Vector3(5f, 1.8f, 36f), new Color(0.9f, 0.4f, 0.15f), 1.4f, 12f);
            BuildAccentPointLight("GraveEmber0", new Vector3(-4f, 1.6f, 58f), new Color(0.6f, 0.55f, 0.6f), 1f, 12f);
            BuildAccentPointLight("GraveEmber1", new Vector3(4f, 1.6f, 68f), new Color(0.6f, 0.55f, 0.6f), 1f, 12f);

            // ---- The ash-world: one open exterior ground plane (mirrors the EP04 jungle / Ch4
            // Deepworks pattern — no walls, no ceiling), threaded z[0,90]: landing site -> settlement
            // ruins -> mass grave. ----
            var ashWorldGo = new GameObject("AshWorld");
            var ashWorld = ashWorldGo.transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "AshGround";
            ground.transform.SetParent(ashWorld, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 45f);
            ground.transform.localScale = new Vector3(40f, 1f, 100f);
            TintShared(ground.GetComponent<Renderer>(), new Color(0.12f, 0.11f, 0.11f));

            Ch5BuildSettlementRuins(ashWorld);
            Ch5BuildMassGrave(ashWorld);

            // ---- The memory-dive: the settlement as it was, ghost-tinted, offset far up +Z (z 150-200)
            // so it never overlaps the real ash-world. Inactive until the dive Trigger step. ----
            var diveRootGo = new GameObject("MemoryDive_Massacre");
            var diveRoot = diveRootGo.transform;
            var flashback = diveRootGo.AddComponent<MemoryFlashbackController>(); // fog/desaturation on activation
            Ch5BuildMemoryDive(diveRoot, out var diveEntryPointGo);
            diveRootGo.SetActive(false);

            // ---- Game root. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<GameState>();
            gameGo.AddComponent<CombatFeedbackController>();

            // ---- Player rig (head + hands), locomotion, bounds, EchoPresence. ----
            var rig = BuildRig(refs, addLocomotion: true);
            rig.AddComponent<EchoPresence>();
            var bounds = rig.AddComponent<ZoneBounds>();
            bounds.center = new Vector3(0f, 0f, 100f); // covers the ash-world AND the dive island on one line
            bounds.radius = 115f;

            // The katana rides at the landing site from the start (this chapter opens already armed —
            // no rack-wake beat this time, matching the "cost, not initiation" tone of Act II opening).
            BuildSword(new Vector3(2f, 1f, 1f), Quaternion.Euler(-90f, 0f, 0f), weapon, Ch5EchoBladePrefab);

            // ---- The landing party: Kessler, Resh, Mira. Iris and Mera Voss stay voice-only (see the
            // CREW-PRESENCE DECISION note above the menu item). ----
            Ch5PlaceStoryNpc(Ch5KesslerPrefab, new Vector3(-1.5f, 0f, 3f), "Kessler", wanderRadius: 0.6f);
            Ch5PlaceStoryNpc(Ch5ReshPrefab, new Vector3(1.5f, 0f, 3f), "Resh", wanderRadius: 0.6f);
            Ch5PlaceStoryNpc(Ch5MiraPrefab, new Vector3(0f, 0f, 4.5f), "Mira", wanderRadius: 0.5f);

            // ---- Vera Dusk: waiting at the grave from the start (same convention as Tessa Rin in
            // Ch4), fixed in place, no wander. ----
            Ch5PlaceStoryNpc(Ch5VeraDuskPrefab, new Vector3(0f, 0f, 66f), "Vera Dusk", wanderRadius: 0f);

            // ---- Memory dive controller + entry/exit trigger objects (Trigger steps SetActive them;
            // their OnEnable drives EnterDive/ExitDive — MissionDirector never learns about dives). ----
            var diveGo = new GameObject("MemoryDive");
            var dive = diveGo.AddComponent<MemoryDiveController>();
            // Exit anchor: back at the grave, facing Vera (+Z).
            var exitPointGo = new GameObject("DiveExitPoint");
            exitPointGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, 60f), Quaternion.identity);
            var diveSo = new SerializedObject(dive);
            SetObjectRef(diveSo, "diveRoot", diveRootGo);
            SetObjectRef(diveSo, "diveEntryPoint", diveEntryPointGo.transform);
            SetObjectRef(diveSo, "diveExitPoint", exitPointGo.transform);
            SetObjectRef(diveSo, "rigRoot", rig.transform);
            SetObjectRef(diveSo, "flashback", flashback);
            diveSo.ApplyModifiedPropertiesWithoutUndo();

            var enterDiveGo = new GameObject("EnterDiveTrigger");
            var enterTrigger = enterDiveGo.AddComponent<MemoryDiveEntryTrigger>();
            var enterSo = new SerializedObject(enterTrigger);
            SetObjectRef(enterSo, "dive", dive);
            enterSo.ApplyModifiedPropertiesWithoutUndo();
            enterDiveGo.SetActive(false);

            var exitDiveGo = new GameObject("ExitDiveTrigger");
            var exitTrigger = exitDiveGo.AddComponent<MemoryDiveExitTrigger>();
            var exitSo = new SerializedObject(exitTrigger);
            SetObjectRef(exitSo, "dive", dive);
            exitSo.ApplyModifiedPropertiesWithoutUndo();
            exitDiveGo.SetActive(false);

            // ---- Reach points. ----
            var settlementReachGo = new GameObject("SettlementReachPoint");
            settlementReachGo.transform.position = new Vector3(0f, 1f, 20f);
            var graveReachGo = new GameObject("GraveReachPoint");
            graveReachGo.transform.position = new Vector3(0f, 1f, 58f);
            var diveLaneReachGo = new GameObject("DiveLaneReachPoint");
            diveLaneReachGo.transform.position = new Vector3(0f, 1f, 165f);
            var diveNearGirlReachGo = new GameObject("DiveNearGirlReachPoint");
            diveNearGirlReachGo.transform.position = new Vector3(0f, 1f, 188f);

            // ---- Dialogue players (Y / Left-Hand Talk advances each line). ----
            var talkRef = FindRef(refs, "Left Hand", "Talk");
            var dlgBriefing = Ch5BuildDialogue("Dialogue_Beat0_Briefing", new Vector3(0f, 1f, 3f), "ch5_beat0_briefing", talkRef);
            var dlgApproach = Ch5BuildDialogue("Dialogue_Beat1_Approach", new Vector3(0f, 1f, 24f), "ch5_beat1_approach", talkRef);
            var dlgAccusation = Ch5BuildDialogue("Dialogue_Beat1_Accusation", new Vector3(0f, 1f, 60f), "ch5_beat1_accusation", talkRef);
            var dlgKiraNamed = Ch5BuildDialogue("Dialogue_Beat1_KiraNamed", new Vector3(0f, 1f, 62f), "ch5_beat1_kira_named", talkRef);
            var dlgDiveIntro = Ch5BuildDialogue("Dialogue_Beat2_DiveIntro", new Vector3(0f, 1f, 157f), "ch5_beat2_dive_intro", talkRef);
            var dlgCountingStock = Ch5BuildDialogue("Dialogue_Beat2_CountingStock", new Vector3(0f, 1f, 170f), "ch5_beat2_counting_stock", talkRef);
            var dlgHesitation = Ch5BuildDialogue("Dialogue_Beat2_Hesitation", new Vector3(0f, 1f, 188f), "ch5_beat2_hesitation", talkRef);
            var dlgLedger = Ch5BuildDialogue("Dialogue_Beat3_Ledger", new Vector3(0f, 1f, 61f), "ch5_beat3_ledger", talkRef);
            var dlgDemand = Ch5BuildDialogue("Dialogue_Beat4_Demand", new Vector3(0f, 1f, 63f), "ch5_beat4_demand", talkRef);
            var dlgVerdict = Ch5BuildDialogue("Dialogue_Beat4_Verdict", new Vector3(0f, 1f, 64f), "ch5_beat4_verdict", talkRef);
            var dlgRelease = Ch5BuildDialogue("Dialogue_Beat4_Release", new Vector3(0f, 1f, 65f), "ch5_beat4_release", talkRef);
            var dlgHook = Ch5BuildDialogue("Dialogue_Beat4_Hook", new Vector3(0f, 1f, 66f), "ch5_beat4_hook", talkRef);

            // ---- Chapter-complete canvas (worldspace) + outro driver. ----
            var completeCanvasGo = Ch5BuildCompleteCanvas(new Vector3(0f, 1.4f, 70f));
            var outroGo = new GameObject("ChapterOutro");
            outroGo.transform.position = new Vector3(0f, 1f, 68f);
            var flagSetter = outroGo.AddComponent<CampaignFlagSetter>();
            var flagSo = new SerializedObject(flagSetter);
            var flagsProp = flagSo.FindProperty("flags");
            flagsProp.arraySize = 1;
            flagsProp.GetArrayElementAtIndex(0).stringValue = "ch5_complete";
            flagSo.ApplyModifiedPropertiesWithoutUndo();
            var outro = outroGo.AddComponent<ChapterOutro>();
            var outroSo = new SerializedObject(outro);
            SetObjectRef(outroSo, "completeCanvas", completeCanvasGo);
            outroSo.ApplyModifiedPropertiesWithoutUndo();
            // publishZoneCompleted defaults to true on ChapterOutro — scene-keyed completion, same as
            // every chapter finale.
            UnityEventTools.AddPersistentListener(outro.OnActivated,
                new UnityEngine.Events.UnityAction(flagSetter.SetFlags));
            outroGo.SetActive(false);

            // ---- Mission Director: the canonical Chapter 5 beat sequence. ----
            var missionGo = new GameObject("Mission");
            var missionDirector = missionGo.AddComponent<MissionDirector>();

            var mdSo = new SerializedObject(missionDirector);
            var steps = mdSo.FindProperty("steps");
            int n = 0;
            steps.arraySize = 19;

            AuthorDialogueStep(steps, n++, "Beat0: The Cairn (the briefing)", dlgBriefing);
            AuthorReachStep(steps, n++, "ReachTrigger: The Settlement", settlementReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Approach (Echo flags Vera)", dlgApproach);
            AuthorReachStep(steps, n++, "ReachTrigger: The Mass Grave", graveReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat1: The Accusation", dlgAccusation);
            AuthorDialogueStep(steps, n++, "Beat1: Kira Named (the sword remembers)", dlgKiraNamed);
            AuthorTriggerStep(steps, n++, "Trigger: Enter the Memory-Dive (the massacre)", enterDiveGo);
            AuthorDialogueStep(steps, n++, "Beat2: Walking the Dead (dive opens)", dlgDiveIntro);
            AuthorReachStep(steps, n++, "ReachTrigger: Dive — The Lane", diveLaneReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: Counting Stock", dlgCountingStock);
            AuthorReachStep(steps, n++, "ReachTrigger: Dive — Nearing the Girl", diveNearGirlReachGo.transform, 5f);
            AuthorDialogueStep(steps, n++, "Beat2: The Hesitation", dlgHesitation);
            AuthorTriggerStep(steps, n++, "Trigger: Exit the Memory-Dive (surface)", exitDiveGo);
            AuthorDialogueStep(steps, n++, "Beat3: The Ledger — 'Kael Vor' Exposed", dlgLedger);
            AuthorDialogueStep(steps, n++, "Beat4: The Demand (a life offered)", dlgDemand);
            AuthorDialogueStep(steps, n++, "Beat4: The Verdict (a harder sentence)", dlgVerdict);
            AuthorDialogueStep(steps, n++, "Beat4: The Release (Vera stays, he walks)", dlgRelease);
            AuthorDialogueStep(steps, n++, "Beat4: The Hook (the identity hunt opens)", dlgHook);
            AuthorTriggerStep(steps, n++, "Trigger: Chapter Outro (flag + fade + canvas)", outroGo);

            mdSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- XR UI infrastructure (SettingsPanelBuilder needs a ray interactor + event system). ----
            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            // ---- Save + register. ----
            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            XRRigBuilder.RewireOpenScene();
            EditorSceneManager.SaveScene(scene, Ch5ScenePath);
            EnsureScenesInBuild(Ch5ScenePath);

            Debug.Log($"[Space Samurai] Chapter 5 built at {Ch5ScenePath}. " +
                      "Landing site -> settlement ruins (ash palette, scavenger set-dressing, no combat) " +
                      "-> the mass grave (Vera Dusk's accusation, Kira named) -> a MemoryDiveController " +
                      "dive into the massacre (ghost silhouettes only, no sentinels, no violence depicted) " +
                      "-> the 'Kael Vor' reveal over comm (Ladder C rung 1) -> the reckoning (Vera refuses " +
                      "his life, releases him under a harder sentence). No roster boss. 19 mission steps. " +
                      "Opens Act II; sets ch5_complete.");
        }

        // ---- Dialogue: build via the shared helper, then wire ch5 voice clips ourselves. ----

        private static DialoguePlayer Ch5BuildDialogue(string name, Vector3 pos, string setId, InputActionReference advanceRef)
        {
            var lines = Chapter5Lines.Get(setId);
            var dp = BuildDialoguePlayer(name, pos, lines, advanceRef, clipSetId: null);
            int resolved = Ch5WireVoiceClips(dp, setId, lines);
            if (resolved < lines.Length)
                Debug.LogWarning($"[Chapter5] {name}: only {resolved}/{lines.Length} voice clips resolved for set '{setId}'.");
            return dp;
        }

        private static int Ch5WireVoiceClips(DialoguePlayer dp, string setId, DialogueLine[] lines)
        {
            var so = new SerializedObject(dp);
            var linesProp = so.FindProperty("lines");
            int resolved = 0;
            for (int i = 0; i < lines.Length && i < linesProp.arraySize; i++)
            {
                string clipName = Chapter5Lines.ClipName(setId, i, lines[i].speaker);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch5VoiceFolder}/{clipName}.mp3");
                if (clip == null)
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ch5VoiceFolder}/{clipName}.wav");
                if (clip != null)
                {
                    linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue = clip;
                    resolved++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return resolved;
        }

        // ---- Cast placement (mirrors Chapter3/Chapter4's StoryNpc + optional wander wiring). ----

        private static GameObject Ch5PlaceStoryNpc(string prefabPath, Vector3 pos, string displayName, float wanderRadius)
        {
            var go = InstantiateNpc(prefabPath, pos, displayName);
            if (go == null) return null;
            FitNamedCharacter(go);

            var npc = go.AddComponent<StoryNpc>();
            var npcSo = new SerializedObject(npc);
            npcSo.FindProperty("displayName").stringValue = displayName;
            npcSo.ApplyModifiedPropertiesWithoutUndo();

            if (wanderRadius > 0f)
            {
                var wander = go.AddComponent<StoryNpcWander>();
                var wanderSo = new SerializedObject(wander);
                wanderSo.FindProperty("wanderRadius").floatValue = wanderRadius;
                wanderSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return go;
        }

        // ---- The ash-world: settlement ruins and the mass grave. ----

        /// <summary>Collapsed wall-shell fragments and burned props scattered z[16,48]; a couple of
        /// non-interactive scavenger silhouettes for set-dressing only (no Enemy component, no AI —
        /// canon calls them "texture, not enemy").</summary>
        private static void Ch5BuildSettlementRuins(Transform parent)
        {
            var burned = new Color(0.15f, 0.13f, 0.12f);
            var charred = new Color(0.08f, 0.07f, 0.07f);
            var scavenger = new Color(0.2f, 0.18f, 0.17f);

            // Collapsed wall-shell fragments: short, broken, no full rooms — a settlement scoured, not
            // a level.
            Vector3[] shellPositions =
            {
                new Vector3(-6f, 0.6f, 18f), new Vector3(6f, 0.5f, 22f),
                new Vector3(-5f, 0.8f, 30f), new Vector3(5f, 0.4f, 34f),
                new Vector3(-6f, 0.5f, 42f), new Vector3(6f, 0.7f, 46f),
            };
            foreach (var pos in shellPositions)
                BuildProp(parent, "CollapsedWallShell", pos, new Vector3(2.2f, pos.y * 2f, 0.4f), burned);

            // Burned market-stall debris.
            BuildProp(parent, "BurnedStall0", new Vector3(-3f, 0.3f, 26f), new Vector3(1.2f, 0.5f, 0.8f), charred);
            BuildProp(parent, "BurnedStall1", new Vector3(3f, 0.3f, 28f), new Vector3(1.2f, 0.5f, 0.8f), charred);
            BuildProp(parent, "BurnedStall2", new Vector3(-2.5f, 0.25f, 38f), new Vector3(1f, 0.4f, 0.7f), charred);

            // Scavenger silhouettes: static, decorative, no components beyond a renderer — they scatter
            // in the fiction, not the sim.
            BuildProp(parent, "Scavenger_Silhouette0", new Vector3(-7f, 0.9f, 20f), new Vector3(0.4f, 1.8f, 0.3f), scavenger);
            BuildProp(parent, "Scavenger_Silhouette1", new Vector3(7f, 0.9f, 44f), new Vector3(0.4f, 1.8f, 0.3f), scavenger);
        }

        /// <summary>The mass grave: a marker-stone grid over the scar in the ground, a taller cairn of
        /// scrap and chits at its head, and a small bright ID-chit prop labeled KIRA. Returns the cairn
        /// GameObject (not otherwise referenced, but kept for parity with other chapters' return-a-focal-
        /// point builders).</summary>
        private static GameObject Ch5BuildMassGrave(Transform parent)
        {
            var stone = new Color(0.3f, 0.29f, 0.28f);
            var cairnColor = new Color(0.35f, 0.32f, 0.28f);

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    var pos = new Vector3(-4.5f + col * 3f, 0.15f, 54f + row * 4f);
                    BuildProp(parent, $"GraveMarker_{row}_{col}", pos, new Vector3(0.3f, 0.3f, 0.15f), stone);
                }
            }

            var cairnGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cairnGo.name = "Cairn";
            cairnGo.transform.SetParent(parent, false);
            cairnGo.transform.localPosition = new Vector3(0f, 0.4f, 64f);
            cairnGo.transform.localScale = new Vector3(1f, 0.8f, 1f);
            TintShared(cairnGo.GetComponent<Renderer>(), cairnColor);

            // The ID-chit: a small bright accent so it reads against the ash, with a floating label.
            var chitGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chitGo.name = "IDChit_Kira";
            chitGo.transform.SetParent(parent, false);
            chitGo.transform.localPosition = new Vector3(0f, 0.85f, 64f);
            chitGo.transform.localScale = new Vector3(0.15f, 0.02f, 0.1f);
            Object.DestroyImmediate(chitGo.GetComponent<Collider>());
            chitGo.GetComponent<Renderer>().sharedMaterial = MakeUnlitMaterial(new Color(0.95f, 0.9f, 0.75f));

            var labelGo = new GameObject("IDChit_Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.1f, 64f);
            labelGo.transform.localScale = Vector3.one * 0.01f;
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = "KIRA";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.color = new Color(0.9f, 0.88f, 0.8f);

            return cairnGo;
        }

        // ---- The memory-dive: the settlement as it was, ghost-tinted (all under the inactive diveRoot). ----

        /// <summary>
        /// The square, alive and then not, replayed off the katana's stored record. z 150-200. Structural
        /// geometry (ground, stalls, the well) stays solid/desaturated; only the human figures use
        /// MakeGhostMaterial — villagers still, Kira near the well, his younger conditioned self and the
        /// squad frozen mid-advance down the lane. No sentinels, no violence: everyone here is a still
        /// silhouette, matching the respectful staging Ch3's playback set.
        /// </summary>
        private static void Ch5BuildMemoryDive(Transform diveRoot, out GameObject diveEntryPointGo)
        {
            var greyGround = new Color(0.26f, 0.27f, 0.28f);
            var greyStall = new Color(0.32f, 0.32f, 0.34f);
            var ghostMat = MemoryFlashbackController.MakeGhostMaterial();

            // The square's ground: one open plane, no walls (an outdoor market, same as the ash-world
            // above it). x[-9,9], z[150,200].
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "MemorySquare_Ground";
            ground.transform.SetParent(diveRoot, false);
            ground.transform.localPosition = new Vector3(0f, -0.1f, 175f);
            ground.transform.localScale = new Vector3(18f, 0.2f, 50f);
            TintShared(ground.GetComponent<Renderer>(), greyGround);

            // Market stalls, the well the girl kept her count beside.
            BuildProp(diveRoot, "MemoryStall0", new Vector3(-4f, 0.4f, 158f), new Vector3(1.2f, 0.8f, 0.7f), greyStall);
            BuildProp(diveRoot, "MemoryStall1", new Vector3(4f, 0.4f, 162f), new Vector3(1.2f, 0.8f, 0.7f), greyStall);
            BuildProp(diveRoot, "MemoryStall2", new Vector3(-3f, 0.4f, 178f), new Vector3(1.2f, 0.8f, 0.7f), greyStall);
            var well = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            well.name = "MemoryWell";
            well.transform.SetParent(diveRoot, false);
            well.transform.localPosition = new Vector3(1f, 0.4f, 186f);
            well.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
            TintShared(well.GetComponent<Renderer>(), greyStall);
            Object.DestroyImmediate(well.GetComponent<Collider>());

            // Villagers: still silhouettes trading in the square.
            Ch5BuildGhostFigure(diveRoot, "Ghost_Villager0", new Vector3(-3.5f, 0f, 159f), 1.7f, ghostMat);
            Ch5BuildGhostFigure(diveRoot, "Ghost_Villager1", new Vector3(3.5f, 0f, 163f), 1.75f, ghostMat);
            Ch5BuildGhostFigure(diveRoot, "Ghost_Villager2", new Vector3(-2f, 0f, 176f), 1.6f, ghostMat);
            Ch5BuildGhostFigure(diveRoot, "Ghost_Villager3", new Vector3(4f, 0f, 180f), 1.7f, ghostMat);

            // Kira: by the well, the last thing in the lane's path.
            var kiraGhostGo = InstantiateNpc(Ch5KiraDuskPrefab, new Vector3(0.5f, 0f, 187f), "Ghost_Kira");
            if (kiraGhostGo != null)
            {
                kiraGhostGo.transform.SetParent(diveRoot, true);
                FitNamedCharacter(kiraGhostGo);
                foreach (var r in kiraGhostGo.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = ghostMat;
            }

            // His conditioned self: frozen mid-lane, one lane away, never reachable — a still silhouette,
            // not an actor the player can approach as a threat.
            Ch5BuildGhostFigure(diveRoot, "Ghost_YoungerRonin", new Vector3(0f, 0f, 182f), 1.8f, ghostMat);
            // The squad: frozen mid-advance behind him.
            Ch5BuildGhostFigure(diveRoot, "Ghost_Squad0", new Vector3(-1.2f, 0f, 178f), 1.75f, ghostMat);
            Ch5BuildGhostFigure(diveRoot, "Ghost_Squad1", new Vector3(1.4f, 0f, 179f), 1.8f, ghostMat);

            // Cold, drained accent lights, parented under the dive so they only exist while it does.
            Ch5BuildPlaybackLight(diveRoot, "MemoryLight_Entry", new Vector3(0f, 2.6f, 158f), new Color(0.55f, 0.58f, 0.65f), 1f);
            Ch5BuildPlaybackLight(diveRoot, "MemoryLight_Mid", new Vector3(0f, 2.6f, 174f), new Color(0.5f, 0.5f, 0.56f), 0.8f);
            Ch5BuildPlaybackLight(diveRoot, "MemoryLight_Well", new Vector3(0.5f, 2.6f, 187f), new Color(0.6f, 0.62f, 0.7f), 1.1f);

            // Dive entry/reset anchor, just inside the square's mouth, facing up the lane (+Z).
            diveEntryPointGo = new GameObject("DiveEntryPoint");
            diveEntryPointGo.transform.SetParent(diveRoot, false);
            diveEntryPointGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, 152f), Quaternion.identity);
        }

        /// <summary>A cheap shadowless point light parented under the dive root (BuildAccentPointLight
        /// creates standalone scene objects, which would light the memory square even while it's
        /// inactive) — mirrors Ch3BuildPlaybackLight.</summary>
        private static void Ch5BuildPlaybackLight(Transform parent, string name, Vector3 pos, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = 12f;
            l.shadows = LightShadows.None; // keep it cheap on Quest
        }

        /// <summary>A still ghost silhouette: a capsule (torso) + sphere (head) in the shared ghost
        /// material, colliders stripped — memory cast, never an obstacle, never a threat. Mirrors
        /// Ch3BuildGhostFigure.</summary>
        private static GameObject Ch5BuildGhostFigure(Transform parent, string name, Vector3 pos, float height, Material ghostMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            float torsoH = height * 0.42f; // capsule default height is 2 at scaleY=1
            body.transform.localPosition = new Vector3(0f, torsoH, 0f);
            body.transform.localScale = new Vector3(height * 0.22f, torsoH, height * 0.22f);
            body.GetComponent<Renderer>().sharedMaterial = ghostMat;
            Object.DestroyImmediate(body.GetComponent<Collider>());

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, height * 0.9f, 0f);
            head.transform.localScale = Vector3.one * height * 0.16f;
            head.GetComponent<Renderer>().sharedMaterial = ghostMat;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            return root;
        }

        /// <summary>A worldspace "CHAPTER 5 COMPLETE" canvas, created inactive (the outro reveals it).</summary>
        private static GameObject Ch5BuildCompleteCanvas(Vector3 position)
        {
            var canvasGo = new GameObject("CHAPTER 5 COMPLETE Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, 220f);
            rt.localScale = Vector3.one * 0.0015f;
            rt.position = position;
            rt.rotation = Quaternion.Euler(0f, 180f, 0f); // face -z, toward the player

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.9f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(canvasGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "CHAPTER 5 COMPLETE";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 54;
            label.color = new Color(0.9f, 0.92f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            canvasGo.SetActive(false);
            return canvasGo;
        }
    }
}
