using Ronin7.Flow;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.EditorTools
{
    /// <summary>
    /// Adds the roguelike's ENTRY POINTS to the shipped scenes. Without this the mode is unreachable:
    /// <see cref="MainMenuController.OnStartRunClicked"/> existed but no button called it, and the hub
    /// had no <see cref="RunLauncher"/> at all, so a player could never start a run.
    ///
    /// ADDITIVE, like <c>ChapterVoiceWirer</c>/<c>EnemyArtWirer</c>: opens each shipped scene and
    /// patches it in place. It never rebuilds them — the chapter/boot scenes carry hand-wired
    /// post-build work that a rebuild would wipe.
    ///
    /// Idempotent: re-running finds the existing button/launcher by name and leaves them alone.
    ///
    /// Lives in the <see cref="XRRigBuilder"/> partial so it can reuse that class's private
    /// <c>MenuMakeButton</c> helper and keep the new button visually identical to the existing four.
    /// </summary>
    public static partial class XRRigBuilder
    {
        private const string RogueBootScenePath = SceneFolder + "/Phase6_Boot.unity";
        private const string RogueHubScenePath = SceneFolder + "/Galaxy1_Ch1_Hub.unity";
        private const string RogueRunButtonName = "Btn_EnterTheFracture";
        private const string RogueAbandonButtonName = "Btn_AbandonRun";
        private const string RogueShrineButtonName = "Btn_Shrine";
        private const string RogueEchoesLabelName = "Txt_Echoes";

        [MenuItem("Tools/Space Samurai/Roguelike/Patch Entry Points (additive)", priority = 510)]
        public static void PatchRoguelikeEntryPoints()
        {
            RoguePatchBootMenu();
            RoguePatchHubLauncher();
            RoguePatchRunArenaWristHud();
            RoguePatchRunArenaComfortRows();
            AssetDatabase.SaveAssets();
        }

        /// <summary>P2-09: add the Seated / Left-Handed / Teleport rows to RunArena's shipped settings
        /// panel(s). Additive and idempotent (see <see cref="SettingsPanelBuilder.AddComfortRows"/>).</summary>
        private static void RoguePatchRunArenaComfortRows()
        {
            var scene = EditorSceneManager.OpenScene(RogueRunArenaScenePath, OpenSceneMode.Single);
            bool changed = false;
            foreach (var panel in Object.FindObjectsByType<Ronin7.Audio.SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                changed |= SettingsPanelBuilder.AddComfortRows(panel);
            if (!changed)
            {
                Debug.Log("[RogueEntry] RunArena comfort rows already present (or no panel) — left as-is.");
                return;
            }
            Debug.Log("[RogueEntry] Added Seated / Left-Handed / Teleport rows to RunArena's settings panel.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private const string RogueRunArenaScenePath = SceneFolder + "/RunArena.unity";
        private const string RogueWristHudName = Ronin7.Player.HandednessRig.WristHudName;

        /// <summary>RunArena: mount a world-space <see cref="RunHudPanel"/> on the left controller so
        /// depth/boons/echoes are visible mid-run. Sits above and behind the controller (over the
        /// wrist, clear of the hand mesh), tilted toward the face when the wrist is raised.</summary>
        private static void RoguePatchRunArenaWristHud()
        {
            var scene = EditorSceneManager.OpenScene(RogueRunArenaScenePath, OpenSceneMode.Single);

            var leftHand = GameObject.Find("Left Hand Controller");
            if (leftHand == null)
            {
                Debug.LogError("[RogueEntry] No 'Left Hand Controller' in RunArena — cannot mount the wrist HUD.");
                return;
            }
            if (leftHand.transform.Find(RogueWristHudName) != null)
            {
                Debug.Log("[RogueEntry] RunArena wrist HUD already present — left as-is.");
                return;
            }

            var go = new GameObject(RogueWristHudName);
            go.transform.SetParent(leftHand.transform, false);
            // ponytail: placement/size are first-guess calibration knobs — tune in-headset.
            go.transform.localPosition = new Vector3(0f, 0.05f, -0.12f); // 5 cm up, 12 cm back toward the forearm
            go.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            go.transform.localScale = Vector3.one * 0.004f; // ~1.5 cm glyphs at fontSize 64

            var tm = go.AddComponent<TextMesh>();
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.85f, 0.55f);

            var hud = go.AddComponent<RunHudPanel>();
            var so = new SerializedObject(hud);
            so.FindProperty("target").objectReferenceValue = tm;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[RogueEntry] Mounted RunHudPanel wrist HUD on the left controller in RunArena.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Boot menu: add an ENTER THE FRACTURE button wired to OnStartRunClicked, and
        /// restack all five buttons so they still fit the 600x700 canvas under the title.</summary>
        private static void RoguePatchBootMenu()
        {
            var scene = EditorSceneManager.OpenScene(RogueBootScenePath, OpenSceneMode.Single);

            var controller = Object.FindFirstObjectByType<MainMenuController>();
            if (controller == null)
            {
                Debug.LogError("[RogueEntry] No MainMenuController in Phase6_Boot — cannot add the run button.");
                return;
            }

            var canvasRt = controller.GetComponent<RectTransform>();
            if (canvasRt == null)
            {
                Debug.LogError("[RogueEntry] MainMenuController is not on the menu canvas — cannot place the button.");
                return;
            }

            var existing = canvasRt.Find(RogueRunButtonName);
            if (existing == null)
            {
                var runBtn = MenuMakeButton(canvasRt, "ENTER THE FRACTURE", new Vector2(0f, 170f));
                runBtn.gameObject.name = RogueRunButtonName;
                UnityEventTools.AddPersistentListener(runBtn.onClick,
                    new UnityEngine.Events.UnityAction(controller.OnStartRunClicked));
                Debug.Log("[RogueEntry] Added ENTER THE FRACTURE button to the boot menu.");
            }
            else
            {
                Debug.Log("[RogueEntry] Run button already present — left as-is.");
            }

            // ABANDON RUN: runtime-shown only when a saved run exists (MainMenuController.OnEnable).
            var abandon = canvasRt.Find(RogueAbandonButtonName);
            Button abandonBtn;
            if (abandon == null)
            {
                abandonBtn = MenuMakeButton(canvasRt, "ABANDON RUN", new Vector2(0f, 100f));
                abandonBtn.gameObject.name = RogueAbandonButtonName;
                UnityEventTools.AddPersistentListener(abandonBtn.onClick,
                    new UnityEngine.Events.UnityAction(controller.OnAbandonRunClicked));
                Debug.Log("[RogueEntry] Added ABANDON RUN button to the boot menu.");
            }
            else
            {
                abandonBtn = abandon.GetComponent<Button>();
            }

            // P2-01: SHRINE (opens the Echo shop) + the "◈ ECHOES n" balance label.
            var shrine = canvasRt.Find(RogueShrineButtonName);
            if (shrine == null)
            {
                var shrineBtn = MenuMakeButton(canvasRt, "SHRINE", new Vector2(0f, 120f));
                shrineBtn.gameObject.name = RogueShrineButtonName;
                UnityEventTools.AddPersistentListener(shrineBtn.onClick,
                    new UnityEngine.Events.UnityAction(controller.OnShrineClicked));
                Debug.Log("[RogueEntry] Added SHRINE button to the boot menu.");
            }
            var echoes = canvasRt.Find(RogueEchoesLabelName);
            if (echoes == null)
            {
                var t = MenuNewText(canvasRt, "◈ ECHOES 0", new Vector2(0f, 232f), new Vector2(480f, 40f), 28, TextAnchor.MiddleCenter);
                t.gameObject.name = RogueEchoesLabelName;
                t.color = new Color(1f, 0.85f, 0.4f);
                echoes = t.transform;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("runButton").objectReferenceValue = canvasRt.Find(RogueRunButtonName).GetComponent<Button>();
            so.FindProperty("abandonButton").objectReferenceValue = abandonBtn;
            so.FindProperty("echoesLabel").objectReferenceValue = echoes.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Restack: seven buttons at 82px spacing (80px tall) stay clear of the title (y=270) and
            // inside the canvas (height 700 => +/-350). Matching by the label each button carries.
            // BUG-27: ABANDON RUN sits 20+ cm (1 px = 1 mm) below ENTER THE FRACTURE / RESUME RUN —
            // it used to be 9 cm under it, one slip from discarding the run.
            RogueStack(canvasRt, "ENTER THE FRACTURE", 180f);
            RogueStack(canvasRt, "SHRINE", 98f);
            RogueStack(canvasRt, "RECALIBRATE HEIGHT", 16f);
            RogueStack(canvasRt, "START NEW GAME", -66f);
            RogueStack(canvasRt, "ABANDON RUN", -148f);
            RogueStack(canvasRt, "CONTINUE", -230f);
            RogueStack(canvasRt, "EXIT GAME", -312f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Move whichever button carries <paramref name="label"/> to <paramref name="y"/>.</summary>
        private static void RogueStack(RectTransform canvasRt, string label, float y)
        {
            var buttons = canvasRt.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var text = b.GetComponentInChildren<Text>(true);
                if (text == null || text.text != label) continue;
                var rt = b.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
                return;
            }
        }

        /// <summary>Hub: give Kessler's ship a RunLauncher so a new run can be started from the hub —
        /// the roguelike returns the player here after every run, win or permadeath.</summary>
        private static void RoguePatchHubLauncher()
        {
            var scene = EditorSceneManager.OpenScene(RogueHubScenePath, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<RunLauncher>() != null)
            {
                Debug.Log("[RogueEntry] Hub already has a RunLauncher — left as-is.");
                return;
            }

            var host = GameObject.Find("RunLauncher");
            if (host == null) host = new GameObject("RunLauncher");
            host.AddComponent<RunLauncher>();

            Debug.Log("[RogueEntry] Added RunLauncher to the hub. NOTE: it exposes LaunchRun() but is "
                    + "not bound to an interactable yet — the boot menu is the working entry point.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
