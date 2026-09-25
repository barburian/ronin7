using Ronin7.Core;
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
            var camT = targetCamera.transform;
            rt.position = camT.position + camT.forward * 1.5f - camT.right * 0.85f;
            rt.rotation = Quaternion.LookRotation(rt.position - camT.position, Vector3.up);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            bool won = RunSummary.Won;
            // Win/loss must be unambiguous at a glance: colour AND wording, never colour alone.
            var accent = won ? new Color(0.45f, 0.95f, 0.55f) : new Color(1f, 0.45f, 0.4f);

            float y = 380f;
            AddText(rt, font, RunSummary.FormatOutcome(won), 56, FontStyle.Bold, accent, ref y, 90f);
            AddText(rt, font, RunSummary.FormatOutcomeDetail(won, RunSummary.Depth), 24, FontStyle.Normal,
                new Color(0.85f, 0.85f, 0.85f), ref y, 70f);

            y -= 20f;
            AddRow(rt, font, "ROOMS CLEARED", RunSummary.FormatDepth(RunSummary.Depth, won), ref y);
            AddRow(rt, font, "ENEMIES DEFEATED", RunSummary.EnemiesKilled.ToString(), ref y);
            AddRow(rt, font, "BOONS HELD", RunSummary.BoonsHeld.ToString(), ref y);
            AddRow(rt, font, "ECHOES EARNED", RunSummary.EchoesEarned.ToString(), ref y);

            y -= 24f;
            AddText(rt, font, "LIFETIME", 26, FontStyle.Bold, new Color(0.7f, 0.8f, 1f), ref y, 48f);
            AddRow(rt, font, "BEST DEPTH", MetaProgression.BestDepth.ToString(), ref y);
            AddRow(rt, font, "RUNS COMPLETED", MetaProgression.RunsCompleted.ToString(), ref y);
            AddRow(rt, font, "RUNS WON", MetaProgression.RunsWon.ToString(), ref y);
            AddRow(rt, font, "ECHOES BANKED", MetaProgression.Echoes.ToString(), ref y);

            var dismiss = BuildDismissButton(rt, font);

            var panel = canvasGo.AddComponent<RunSummaryPanel>();
            panel.dismissButton = dismiss;
            // Must follow the assignment above: AddComponent already ran Awake.
            panel.WireButtons();
            return panel;
        }

        private static void AddText(RectTransform parent, Font font, string text, int size,
            FontStyle style, Color color, ref float y, float height)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700f, height);
            rt.anchoredPosition = new Vector2(0f, y);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color;
            y -= height;
        }

        /// <summary>One label/value row. Large type — this is read at real arm's length in a headset.</summary>
        private static void AddRow(RectTransform parent, Font font, string label, string value, ref float y)
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
            var lt = labelGo.AddComponent<Text>();
            lt.text = label;
            lt.font = font;
            lt.fontSize = 26;
            lt.alignment = TextAnchor.MiddleLeft;
            lt.color = new Color(0.78f, 0.78f, 0.82f);

            var valueGo = new GameObject("Value");
            valueGo.transform.SetParent(rowRt, false);
            var vrt = valueGo.AddComponent<RectTransform>();
            vrt.anchorMin = new Vector2(0.65f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            var vt = valueGo.AddComponent<Text>();
            vt.text = value;
            vt.font = font;
            vt.fontSize = 30;
            vt.fontStyle = FontStyle.Bold;
            vt.alignment = TextAnchor.MiddleRight;
            vt.color = Color.white;

            y -= 56f;
        }

        private static Button BuildDismissButton(RectTransform parent, Font font)
        {
            var btnGo = new GameObject("DismissButton");
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(360f, 84f);
            btnRt.anchoredPosition = new Vector2(0f, -380f);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.25f, 1f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = img;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnRt, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var t = labelGo.AddComponent<Text>();
            t.text = "DISMISS";
            t.font = font;
            t.fontSize = 30;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;

            return button;
        }
    }
}
