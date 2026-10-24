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
    /// <see cref="MainMenuController.OnStartRunClicked"/> existed but no button called it, so a player
    /// could never start a run.
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
        private const string RogueRunButtonName = "Btn_EnterTheFracture";
        private const string RogueAbandonButtonName = "Btn_AbandonRun";
        private const string RogueShrineButtonName = "Btn_Shrine";
        private const string RogueEchoesLabelName = "Txt_Echoes";
        private const string RogueSettingsButtonName = "Btn_Settings";

        [MenuItem("Tools/Space Samurai/Roguelike/Patch Entry Points (additive)", priority = 510)]
        public static void PatchRoguelikeEntryPoints()
        {
            RoguePatchBootMenu();
            RoguePatchRunArenaWristHud();
            RoguePatchRunArenaComfortRows();
            RoguePatchComfortOptionRows();
            RoguePatchAccessibility();
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

        /// <summary>P6-02: add the Haptics / Reduce Flashes / Slow-mo / Vignette rows to the boot and RunArena
        /// settings panels. Additive and idempotent (see <see cref="SettingsPanelBuilder.AddComfortOptionRows"/>).</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Comfort Option Rows (P6-02)", priority = 511)]
        public static void RoguePatchComfortOptionRows()
        {
            foreach (var path in new[] { RogueBootScenePath, RogueRunArenaScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var panel in Object.FindObjectsByType<Ronin7.Audio.SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    changed |= SettingsPanelBuilder.AddComfortOptionRows(panel);
                if (!changed) { Debug.Log("[RogueEntry] Comfort option rows already present in " + path); continue; }
                Debug.Log("[RogueEntry] Added P6-02 comfort option rows to " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>P6-03: second settings column (UI scale, assists, subtitles), UiScaleFollower on the wrist HUD and
        /// main menu, and a SubtitleDisplay, in Phase6_Boot and RunArena. Additive and idempotent.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Accessibility (P6-03)", priority = 512)]
        public static void RoguePatchAccessibility()
        {
            foreach (var path in new[] { RogueBootScenePath, RogueRunArenaScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var panel in Object.FindObjectsByType<Ronin7.Audio.SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    changed |= SettingsPanelBuilder.AddAccessibilityColumn(panel);
                foreach (var hud in Object.FindObjectsByType<Ronin7.Flow.RunHudPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (hud.GetComponent<Ronin7.Core.UiScaleFollower>() == null) { hud.gameObject.AddComponent<Ronin7.Core.UiScaleFollower>(); changed = true; }
                foreach (var menu in Object.FindObjectsByType<Ronin7.Flow.MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (menu.GetComponent<Ronin7.Core.UiScaleFollower>() == null) { menu.gameObject.AddComponent<Ronin7.Core.UiScaleFollower>(); changed = true; }
                if (Object.FindAnyObjectByType<Ronin7.Flow.SubtitleDisplay>(FindObjectsInactive.Include) == null)
                {
                    new GameObject("Subtitles").AddComponent<Ronin7.Flow.SubtitleDisplay>();
                    changed = true;
                }
                if (!changed) { Debug.Log("[RogueEntry] Accessibility already present in " + path); continue; }
                Debug.Log("[RogueEntry] Added P6-03 accessibility to " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>P6-06: "Replay Tutorial" toggle row in the settings panel of Phase6_Boot and RunArena. Additive and idempotent.</summary>
        [MenuItem("Tools/Space Samurai/Roguelike/Patch Replay Tutorial (P6-06)", priority = 513)]
        public static void RoguePatchReplayTutorial()
        {
            foreach (var path in new[] { RogueBootScenePath, RogueRunArenaScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (var panel in Object.FindObjectsByType<Ronin7.Audio.SettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    changed |= SettingsPanelBuilder.AddReplayTutorialRow(panel);
                if (!changed) { Debug.Log("[RogueEntry] Replay Tutorial row already present in " + path); continue; }
                Debug.Log("[RogueEntry] Added P6-06 Replay Tutorial row to " + path);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
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
            var existing = leftHand.transform.Find(RogueWristHudName);
            if (existing != null)
            {
                // P5-07b: migrate a legacy TextMesh HUD to TextMeshPro (Rajdhani) in place; pose/scale untouched.
                var legacy = existing.GetComponent<TextMesh>();
                if (legacy == null)
                {
                    Debug.Log("[RogueEntry] RunArena wrist HUD already present — left as-is.");
                    return;
                }
                Object.DestroyImmediate(legacy);
                var mr = existing.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                var hud = existing.GetComponent<RunHudPanel>();
                if (hud == null) hud = existing.gameObject.AddComponent<RunHudPanel>();
                BindHud(hud, AddHudText(existing.gameObject));
                Debug.Log("[RogueEntry] Migrated the RunArena wrist HUD from TextMesh to TextMeshPro.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            var go = new GameObject(RogueWristHudName);
            go.transform.SetParent(leftHand.transform, false);
            // ponytail: placement/size are first-guess calibration knobs — tune in-headset.
            go.transform.localPosition = new Vector3(0f, 0.05f, -0.12f); // 5 cm up, 12 cm back toward the forearm
            go.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            go.transform.localScale = Vector3.one * 0.004f; // ~1.5 cm glyphs at TMP fontSize 40

            BindHud(go.AddComponent<RunHudPanel>(), AddHudText(go));

            Debug.Log("[RogueEntry] Mounted RunHudPanel wrist HUD on the left controller in RunArena.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>P5-07b: world-space TextMeshPro (TMP default font = Rajdhani-SemiBold) for the wrist HUD.</summary>
        private static TMPro.TextMeshPro AddHudText(GameObject go)
        {
            var tm = go.AddComponent<TMPro.TextMeshPro>();
            tm.rectTransform.sizeDelta = new Vector2(60f, 30f); // 24 x 12 cm at 0.004 scale; text may overflow vertically
            tm.fontSize = 40f;
            tm.alignment = TMPro.TextAlignmentOptions.Center;
            tm.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tm.color = new Color(0.95f, 0.85f, 0.55f);
            return tm;
        }

        private static void BindHud(RunHudPanel hud, TMPro.TMP_Text text)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("target").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
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

            // P2-01: SHRINE (opens the Echo shop) + the "ECHOES n" balance label.
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
                var t = MenuNewText(canvasRt, "ECHOES 0", new Vector2(0f, 232f), new Vector2(480f, 40f), 28, TextAnchor.MiddleCenter);
                t.gameObject.name = RogueEchoesLabelName;
                t.color = new Color(1f, 0.85f, 0.4f);
                echoes = t.transform;
            }

            // P2-09: a seated / left-handed player must set comfort options BEFORE the first run.
            var settingsToggle = RogueEnsureBootSettingsPanel();
            if (canvasRt.Find(RogueSettingsButtonName) == null)
            {
                var settingsBtn = MenuMakeButton(canvasRt, "SETTINGS", new Vector2(0f, 16f));
                settingsBtn.gameObject.name = RogueSettingsButtonName;
                UnityEventTools.AddPersistentListener(settingsBtn.onClick,
                    new UnityEngine.Events.UnityAction(controller.OnSettingsClicked));
                Debug.Log("[RogueEntry] Added SETTINGS button to the boot menu.");
            }

            var so = new SerializedObject(controller);
            so.FindProperty("settings").objectReferenceValue = settingsToggle;
            so.FindProperty("runButton").objectReferenceValue = canvasRt.Find(RogueRunButtonName).GetComponent<Button>();
            so.FindProperty("abandonButton").objectReferenceValue = abandonBtn;
            so.FindProperty("echoesLabel").objectReferenceValue = echoes.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Restack: eight buttons at 82px spacing (80px tall) stay clear of the title (y=270); the
            // canvas grows to 900 (+/-450) so EXIT (bottom edge -434) stays inside it. Matching by the
            // label each button carries.
            // BUG-27: ABANDON RUN sits 20+ cm (1 px = 1 mm) below ENTER THE FRACTURE / RESUME RUN —
            // it used to be 9 cm under it, one slip from discarding the run (now 41 cm).
            canvasRt.sizeDelta = new Vector2(canvasRt.sizeDelta.x, Mathf.Max(canvasRt.sizeDelta.y, 900f));
            RogueStack(canvasRt, "ENTER THE FRACTURE", 180f);
            RogueStack(canvasRt, "SHRINE", 98f);
            RogueStack(canvasRt, "SETTINGS", 16f);
            RogueStack(canvasRt, "RECALIBRATE HEIGHT", -66f);
            RogueStack(canvasRt, "START NEW GAME", -148f);
            RogueStack(canvasRt, "ABANDON RUN", -230f);
            RogueStack(canvasRt, "CONTINUE", -312f);
            RogueStack(canvasRt, "EXIT GAME", -394f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>P2-09: the boot scene had no settings panel. Adds the standard one (comfort rows
        /// included), hidden, with its <see cref="Ronin7.Player.SettingsMenuToggle"/>. "Return to Main
        /// Menu" becomes BACK (we're already in it), wired to the toggle's Hide. Save slots stay but
        /// are non-interactable here (SaveGate refuses GameMode.Boot). Idempotent.</summary>
        private static Ronin7.Player.SettingsMenuToggle RogueEnsureBootSettingsPanel()
        {
            var toggle = Object.FindAnyObjectByType<Ronin7.Player.SettingsMenuToggle>(FindObjectsInactive.Include);
            if (toggle != null && toggle.HasPanel) return toggle;

            var panel = SettingsPanelBuilder.BuildCanvas();
            SettingsPanelBuilder.EnsureMenuToggle(panel.gameObject);
            toggle = Object.FindAnyObjectByType<Ronin7.Player.SettingsMenuToggle>(FindObjectsInactive.Include);

            var so = new SerializedObject(panel);
            var ret = so.FindProperty("returnToMenuButton");
            var back = ret.objectReferenceValue as Button;
            ret.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (back != null)
            {
                var label = back.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "BACK";
                UnityEventTools.AddPersistentListener(back.onClick, new UnityEngine.Events.UnityAction(toggle.Hide));
            }
            panel.gameObject.SetActive(false);
            Debug.Log("[RogueEntry] Added a hidden settings panel (with comfort options) to the boot scene.");
            return toggle;
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
    }
}
