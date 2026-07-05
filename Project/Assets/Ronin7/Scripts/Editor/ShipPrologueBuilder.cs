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
    /// Chapter-restructure (cycle 6): every campaign chapter now BEGINS ABOARD KESSLER'S SHIP.
    /// One generated "ChXX_Prologue" scene per chapter — the Cairn's bridge, Kessler delivering the
    /// chapter briefing (his jaw/nod animate via the VO-amplitude path), and a DESCEND console that
    /// chains into the chapter's planet scene via StoryTransition.LoadOnFootScene. The hub launches
    /// missions INTO the prologue (the CampaignDirector's entryScene is the prologue), the
    /// prologue→planet hop is an interior LandingRequested (never repoints LastPlanetScene), and the
    /// planet scene's existing ZoneCompleted marks the prologue entry complete — so every chapter is
    /// a multi-scene sequence (ship → planet) with mission tracking intact. Legacy saves that
    /// completed planet-scene names are renamed by the SaveSystem v2 migration.
    /// </summary>
    public static partial class XRRigBuilder
    {
        internal struct PrologueSpec
        {
            public string id;          // "CH02"
            public string mainScene;   // "Ch02_Auction"
            public string title;       // console label
            public string[] briefing;  // Kessler's lines
        }

        internal static readonly PrologueSpec[] Prologues =
        {
            new PrologueSpec { id = "CH02", mainScene = "Ch02_Auction", title = "THE AUCTION MOON", briefing = new[]{
                "The auction moon. Half the sector's scum bidding on salvage that isn't theirs.",
                "Your sword is down there, Cipher. And somebody paid a lot to make sure it never reaches the block.",
                "Get in, find Mira, get out. I'll keep the Cairn warm." } },
            new PrologueSpec { id = "CH03", mainScene = "Ch03_SwordRemembers", title = "THE SWORD REMEMBERS", briefing = new[]{
                "That blade of yours has been talking in its sleep. The steel remembers what your head can't.",
                "There's a place on this rock that can read old metal. Take the sword down. Listen to what it says." } },
            new PrologueSpec { id = "CH04", mainScene = "Ch04_OverseersHunt", title = "THE OVERSEER'S HUNT", briefing = new[]{
                "The Overseer's hunters found our wake. They're sweeping the port below.",
                "Somebody sabotaged your killswitch — the answer is down there with the hunt. Don't let them box you in." } },
            new PrologueSpec { id = "CH05", mainScene = "Ch05_DebtOfAshes", title = "A DEBT OF ASHES", briefing = new[]{
                "Kael Vor. A name the Program hung its bodies on. The ledger says he's here.",
                "Ashes pay their debts, Cipher. Go collect." } },
            new PrologueSpec { id = "CH06", mainScene = "Ch06_IronDojo", title = "THE IRON DOJO", briefing = new[]{
                "The Iron Dojo. Three masters, one citadel, and a bell that hasn't rung since the purge.",
                "Morrigan's waiting at the window. Climb, fight, and don't let the beauty fool you — it's a cage." } },
            new PrologueSpec { id = "CH07", mainScene = "Ch07_ForgottenNames", title = "FORGOTTEN NAMES", briefing = new[]{
                "A reliquary of dead lanes and kept blades. The names they erased are shelved down there.",
                "Coral Vex knows the stacks. Find her, and bring the names home." } },
            new PrologueSpec { id = "CH08", mainScene = "Ch08_SilentGarden", title = "THE SILENT GARDEN", briefing = new[]{
                "A garden where nobody speaks above the wind. Khall's grief grows here.",
                "Walk it quiet, Cipher. Some answers only come to those who stop swinging." } },
            new PrologueSpec { id = "CH09", mainScene = "Ch09_PitAndTheDeep", title = "THE PIT AND THE DEEP", briefing = new[]{
                "The pit goes down further than the charts admit. Sable says the water draws itself.",
                "There's a voice in the deep that reaches every world at once. Go hear it before it hears you." } },
            new PrologueSpec { id = "CH10", mainScene = "Ch10_LedgerOfRust", title = "THE LEDGER OF RUST", briefing = new[]{
                "Rust keeps better records than men. The ledger below has a page with your number on it.",
                "Cassie and Vess are already counting. Don't keep them waiting." } },
            new PrologueSpec { id = "CH11", mainScene = "Ch11_GhostsAndOrigins", title = "GHOSTS AND ORIGINS", briefing = new[]{
                "Ghosts first, origins after. That's the order the dead prefer.",
                "The Unbroken ward is real — I've watched a man take a killing blow and stand. Watch for it." } },
            new PrologueSpec { id = "CH12", mainScene = "Ch12_TheFracture", title = "THE FRACTURE", briefing = new[]{
                "Cold deck. Frost makes every rail a promise it won't keep — Gryph's words, not mine.",
                "The record's whole down there. It says who drew the leash. Go read the name." } },
            new PrologueSpec { id = "CH13", mainScene = "Ch13_SterileReckoning", title = "A STERILE RECKONING", briefing = new[]{
                "Dr. Heris built you, built the switch, and built the flaw that saved you. All in one pair of hands.",
                "She's in the sterile levels. This one's not a fight, Cipher. It's a reckoning." } },
            new PrologueSpec { id = "CH16", mainScene = "Ch16_ThroneOfAshes", title = "THE THRONE OF ASHES", briefing = new[]{
                "End of the ladder. Khall's throne, and every ash we've raised on the way up.",
                "Whatever you are when you walk back up that ramp — that's what you chose to be. Go finish it." } },
        };

        /// <summary>Prologue scene name for a chapter's main scene ("Ch02_Auction" → "Ch02_Prologue").</summary>
        internal static string PrologueSceneNameFor(string mainScene)
            => mainScene.Substring(0, mainScene.IndexOf('_')) + "_Prologue";

        [MenuItem("Tools/Space Samurai/Chapters/Build All Ship Prologues", priority = 110)]
        public static void BuildAllShipPrologues()
        {
            int built = 0;
            foreach (var spec in Prologues)
            {
                BuildShipPrologue(spec);
                built++;
            }
            Debug.Log($"[ShipPrologue] {built} prologue scenes built + registered.");
        }

        private static void BuildShipPrologue(PrologueSpec spec)
        {
            if (!TryLoadInputRefs(out var refs)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string sceneName = PrologueSceneNameFor(spec.mainScene);
            string scenePath = $"{SceneFolder}/{sceneName}.unity";

            // ---- Cairn bridge lighting: cold cabin steel warmed by console glow. ----
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.75f, 0.9f);
            light.intensity = 0.7f;
            lightGo.transform.rotation = Quaternion.Euler(60f, -20f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.22f);
            RenderSettings.fog = false;
            BuildAccentPointLight("ConsoleGlow", new Vector3(0f, 1.8f, 7f), new Color(1f, 0.75f, 0.45f), 1.5f, 8f);
            BuildAccentPointLight("BridgeFill", new Vector3(-2f, 2.4f, 4f), new Color(0.55f, 0.65f, 0.95f), 1.1f, 9f);

            // ---- The bridge: one 12x10 cabin, star-window wall at the far end. ----
            var worldGo = new GameObject("CairnBridge");
            var world = worldGo.transform;
            var hull = new Color(0.2f, 0.21f, 0.25f);
            BuildFloorCeiling(world, "Bridge", new Vector3(0f, 0f, 4f), new Vector3(12f, 0f, 10f),
                new Color(0.16f, 0.17f, 0.2f), new Color(0.12f, 0.13f, 0.16f));
            BuildWall(world, "Bridge_WallW", new Vector3(-6f, RoomH / 2f, 4f), new Vector3(0.2f, RoomH, 10f));
            BuildWall(world, "Bridge_WallE", new Vector3(6f, RoomH / 2f, 4f), new Vector3(0.2f, RoomH, 10f));
            BuildWall(world, "Bridge_WallS", new Vector3(0f, RoomH / 2f, -1f), new Vector3(12f, RoomH, 0.2f));
            BuildWall(world, "Bridge_WallN", new Vector3(0f, RoomH / 2f, 9f), new Vector3(12f, RoomH, 0.2f));
            // Star window: a deep-space panel across the north wall's upper half.
            BuildProp(world, "StarWindow", new Vector3(0f, 2.1f, 8.85f), new Vector3(9f, 1.7f, 0.05f),
                new Color(0.02f, 0.03f, 0.08f));
            // Dressing: pilot seat block, side lockers.
            BuildProp(world, "PilotSeat", new Vector3(2.5f, 0.5f, 7f), new Vector3(0.8f, 1f, 0.8f), hull * 1.2f);
            BuildProp(world, "LockerRow", new Vector3(-5.4f, 1f, 4f), new Vector3(0.6f, 2f, 4f), hull);

            // ---- Kessler + the briefing (the Tripo Named prefab — feet-pivot, jaw-rigged). ----
            var kessler = InstantiateNpc("Assets/Ronin7/Art/Generated/Characters3D/Named/Kessler.prefab",
                new Vector3(-1.8f, 0f, 6.5f), "Kessler");
            DialoguePlayer briefing = BuildDialoguePlayer("BriefingDialogue", new Vector3(-1.8f, 0f, 6.9f),
                BuildBriefingLines(spec.briefing), FindRef(refs, "Left Hand", "Talk"));
            if (kessler != null)
            {
                FitNamedCharacter(kessler);
                kessler.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the spawn
                var storyNpc = kessler.AddComponent<StoryNpc>();
                var snSo = new SerializedObject(storyNpc);
                snSo.FindProperty("displayName").stringValue = "Kessler";
                var dlgProp = snSo.FindProperty("dialogue");
                if (dlgProp != null) dlgProp.objectReferenceValue = briefing;
                snSo.ApplyModifiedPropertiesWithoutUndo();
                Ronin7.World.NpcTalkAnimator.EnsureOn(kessler); // nod + Rig_Jaw lip-sync during the briefing
            }

            // ---- Descend console (starts inactive; the briefing's Trigger step reveals it). ----
            var descendGo = BuildTransitionBox("DescendConsole", new Vector3(0f, 1.2f, 7.6f),
                $"DESCEND: {spec.title}", out var descendBtn, out var descendTransition);
            descendGo.transform.SetParent(world, true);
            var dtSo = new SerializedObject(descendTransition);
            dtSo.FindProperty("onFootScene").stringValue = spec.mainScene;
            dtSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(descendBtn.onClick,
                new UnityEngine.Events.UnityAction(descendTransition.LoadOnFootScene));
            descendGo.SetActive(false);

            // ---- Game root + mission flow: briefing plays, then the console unlocks. ----
            var gameGo = new GameObject("Game");
            gameGo.AddComponent<Ronin7.Core.GameState>();
            gameGo.AddComponent<CombatFeedbackController>();
            // MissionDirector on its OWN GameObject: arriving from boot/hub, the persistent
            // GameState singleton destroys this scene's duplicate "Game" object wholesale
            // (GameState.Awake → Destroy(gameObject)) — a director riding on it would die with it
            // and the descend console would never unlock. Mirrors the chapter builders' layout.
            var missionGo = new GameObject("Mission");
            var director = missionGo.AddComponent<MissionDirector>();
            var dirSo = new SerializedObject(director);
            var steps = dirSo.FindProperty("steps");
            steps.arraySize = 2;
            AuthorDialogueStep(steps, 0, "Kessler's briefing", briefing);
            AuthorTriggerStep(steps, 1, "Unlock the descent", descendGo);
            dirSo.ApplyModifiedPropertiesWithoutUndo();

            BuildRig(refs, addLocomotion: true);

            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            EnsureXRUIEventSystemMenu();
            WireRightHandRayInteractorMenu();

            BuildAmbienceLayer("CairnBridgeAmbience", new Vector3(0f, 1.5f, 4f), 4f, 14f, 0.35f);
            ProceduralAudioClipBuilder.AssignGeneratedClips();

            EnsureFolder(SceneFolder);
            SettingsPanelBuilder.BuildSettingsPanel();
            RewireOpenScene();
            EditorSceneManager.SaveScene(scene, scenePath);
            EnsureScenesInBuild(scenePath);
        }

        private static DialogueLine[] BuildBriefingLines(string[] texts)
        {
            var lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                lines[i] = new DialogueLine { speaker = "Kessler", text = texts[i], seconds = Mathf.Clamp(texts[i].Length * 0.055f, 3f, 9f) };
            return lines;
        }

        // ==================== Prologue VO (manifest export + clip wiring) ====================

        private const string PrologueVoiceFolder = "Assets/Ronin7/Art/Generated/Audio/Voice";

        /// <summary>Clip name for a prologue briefing line: prologue_ch02_00_kessler.</summary>
        internal static string PrologueClipName(string chapterId, int index)
            => $"prologue_{chapterId.ToLowerInvariant()}_{index:00}_kessler";

        /// <summary>
        /// Exports the 13 Kessler briefings as a voice manifest for generate_voice.py (same JSON
        /// shape as ChapterVoiceManifest). Pipeline: export → run the python generator (edge-tts,
        /// Kessler = en-US-GuyNeural) → "Wire Prologue Voice Clips".
        /// </summary>
        [MenuItem("Tools/Space Samurai/Audio/Export Prologue Voice Manifest", priority = 111)]
        public static void ExportPrologueVoiceManifest()
        {
            var sb = new System.Text.StringBuilder("{\n  \"lines\": [\n");
            bool first = true;
            foreach (var spec in Prologues)
            {
                for (int i = 0; i < spec.briefing.Length; i++)
                {
                    if (!first) sb.Append(",\n");
                    first = false;
                    string text = spec.briefing[i].Replace("\"", "\\\"");
                    sb.Append($"    {{ \"file\": \"{PrologueClipName(spec.id, i)}\", \"speaker\": \"Kessler\", \"text\": \"{text}\" }}");
                }
            }
            sb.Append("\n  ]\n}\n");
            string path = $"{PrologueVoiceFolder}/prologue_voice_manifest.json";
            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[ShipPrologue] Voice manifest exported to {path}.");
        }

        /// <summary>
        /// Wires generated prologue VO clips into the 13 prologue scenes' briefing DialoguePlayers
        /// (additive, idempotent — mirrors ChapterVoiceWirer, but each prologue has exactly one
        /// dialogue so clip names resolve directly from the Prologues table).
        /// </summary>
        [MenuItem("Tools/Space Samurai/Audio/Wire Prologue Voice Clips", priority = 112)]
        public static void WirePrologueVoiceClips()
        {
            int resolved = 0, total = 0;
            foreach (var spec in Prologues)
            {
                string scenePath = $"{SceneFolder}/{PrologueSceneNameFor(spec.mainScene)}.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var dp = Object.FindAnyObjectByType<DialoguePlayer>(FindObjectsInactive.Include);
                if (dp == null) { Debug.LogWarning($"[ShipPrologue] No DialoguePlayer in {scenePath}"); continue; }

                var so = new SerializedObject(dp);
                var linesProp = so.FindProperty("lines");
                bool dirty = false;
                for (int i = 0; i < linesProp.arraySize; i++)
                {
                    total++;
                    var clipProp = linesProp.GetArrayElementAtIndex(i).FindPropertyRelative("clip");
                    if (clipProp.objectReferenceValue != null) { resolved++; continue; }
                    string clipName = PrologueClipName(spec.id, i);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{PrologueVoiceFolder}/{clipName}.wav")
                               ?? AssetDatabase.LoadAssetAtPath<AudioClip>($"{PrologueVoiceFolder}/{clipName}.mp3");
                    if (clip == null) { Debug.LogWarning($"[ShipPrologue] Missing clip {clipName}"); continue; }
                    clipProp.objectReferenceValue = clip;
                    resolved++;
                    dirty = true;
                }
                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            Debug.Log($"[ShipPrologue] Voice wiring: {resolved}/{total} briefing lines have clips.");
        }
    }
}
