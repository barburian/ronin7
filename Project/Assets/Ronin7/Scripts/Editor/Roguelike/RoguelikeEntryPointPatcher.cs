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

        [MenuItem("Tools/Space Samurai/Roguelike/Patch Entry Points (additive)", priority = 510)]
        public static void PatchRoguelikeEntryPoints()
        {
            RoguePatchBootMenu();
            RoguePatchHubLauncher();
            AssetDatabase.SaveAssets();
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

            // Restack: five buttons at 95px spacing keeps them clear of the title (y=270) and inside
            // the canvas (height 700 => +/-350). Matching by the label each button carries.
            RogueStack(canvasRt, "ENTER THE FRACTURE", 170f);
            RogueStack(canvasRt, "START NEW GAME", 75f);
            RogueStack(canvasRt, "CONTINUE", -20f);
            RogueStack(canvasRt, "RECALIBRATE HEIGHT", -115f);
            RogueStack(canvasRt, "EXIT GAME", -210f);

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
