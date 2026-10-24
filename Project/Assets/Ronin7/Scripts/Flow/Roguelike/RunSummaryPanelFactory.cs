using Ronin7.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Builds the post-run summary panel from primitives, mirroring
    /// <see cref="BoonOfferPanelFactory"/>'s runtime-built grey-box idiom.
    ///
    /// Anchored beside the main-menu canvas rather than on top of it, so the player can read the
    /// result and still see ENTER THE FRACTURE. Returns null when there is no camera or no
    /// <see cref="EventSystem"/> — a world-space canvas is unclickable without one, and this panel
    /// sits between the player and the menu, so a dead one would be worse than none.
    /// </summary>
    public static class RunSummaryPanelFactory
    {
        public static RunSummaryPanel Build(Camera targetCamera)
        {
            if (targetCamera == null) return null;
            if (EventSystem.current == null) return null;

            var canvasGo = new GameObject("Run Summary Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(760f, 900f);
            rt.localScale = Vector3.one * 0.001f;

            // Left of the menu at comfortable reading distance, at eye height. 1 unit = 1 m.
            RunSummaryPanel.Place(rt, targetCamera.transform);

            UiKit.Frame(canvasGo.AddComponent<Image>(), UiKit.Cyan);

            bool won = RunSummary.Won;
            // Win/loss must be unambiguous at a glance: colour AND wording, never colour alone (P6-03: blue/amber, no red/green).
            var accent = RunSummary.OutcomeAccent(won);

            float y = 380f;
            AddText(rt, RunSummary.FormatOutcome(won), 56, true, accent, ref y, 90f, title: true);
            AddText(rt, RunSummary.FormatOutcomeDetail(won, RunSummary.Depth), 28, false,
                new Color(0.85f, 0.85f, 0.85f), ref y, 70f);

            if (RunSummary.Assisted)
                AddText(rt, RunSummary.AssistedLabel, 30, true, new Color(1f, 0.85f, 0.4f), ref y, 44f); // P6-03
            y -= 20f;
            AddRow(rt, "ROOMS CLEARED", RunSummary.FormatDepth(RunSummary.Depth, won), ref y);
            AddRow(rt, "ENEMIES DEFEATED", RunSummary.EnemiesKilled.ToString(), ref y);
            AddRow(rt, "BOONS HELD", RunSummary.BoonsHeld.ToString(), ref y);
            AddRow(rt, "ECHOES EARNED", RunSummary.EchoesEarned.ToString(), ref y);

            y -= 24f;
            AddText(rt, "LIFETIME", 30, true, new Color(0.7f, 0.8f, 1f), ref y, 48f);
            AddRow(rt, "BEST DEPTH", $"{MetaProgression.BestDepth} / {RunMapGenerator.TotalNodes}", ref y);
            AddRow(rt, "RUNS COMPLETED", MetaProgression.RunsCompleted.ToString(), ref y);
            AddRow(rt, "RUNS WON", MetaProgression.RunsWon.ToString(), ref y);
            AddRow(rt, "ECHOES BANKED", MetaProgression.Echoes.ToString(), ref y);

            var dismiss = BuildDismissButton(rt);

            canvasGo.AddComponent<UiScaleFollower>(); // P6-03
            var panel = canvasGo.AddComponent<RunSummaryPanel>();
            panel.dismissButton = dismiss;
            // Must follow the assignment above: AddComponent already ran Awake.
            panel.WireButtons();
            return panel;
        }

        private static void AddText(RectTransform parent, string text, int size,
            bool bold, Color color, ref float y, float height, bool title = false)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            UiKit.Text(go, text, size, color, TextAlignmentOptions.Center, bold, title);
            y -= height;
        }

        /// <summary>One label/value row. Large type — this is read at real arm's length in a headset.</summary>
        private static void AddRow(RectTransform parent, string label, string value, ref float y)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(parent, false);
            var rowRt = go.AddComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(660f, 54f);
            rowRt.anchoredPosition = new Vector2(0f, y);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rowRt, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.65f, 1f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            UiKit.Text(labelGo, label, 28, new Color(0.78f, 0.78f, 0.82f), TextAlignmentOptions.Left);

            var valueGo = new GameObject("Value");
            valueGo.transform.SetParent(rowRt, false);
            var vrt = valueGo.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.65f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            UiKit.Text(valueGo, value, 32, Color.white, TextAlignmentOptions.Right, bold: true);

            y -= 56f;
        }

        private static Button BuildDismissButton(RectTransform parent)
        {
            var btnGo = new GameObject("DismissButton");
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(360f, 84f);
            btnRt.anchoredPosition = new Vector2(0f, -380f);
            var img = btnGo.AddComponent<Image>();
            UiKit.Frame(img, UiKit.Cyan, 0.92f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = img;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnRt, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            UiKit.Text(labelGo, "DISMISS", 32, Color.white, TextAlignmentOptions.Center, bold: true);

            return button;
        }
    }
}
