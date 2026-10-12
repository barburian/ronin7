using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Builds the in-run pause panel from primitives, mirroring <see cref="RunSummaryPanelFactory"/>'s
    /// runtime-built idiom. Placed once in front of the head (level, at eye height) when the pause
    /// begins; it does not follow the head. Returns null without a camera or an
    /// <see cref="EventSystem"/>, since the panel would be unclickable — the caller retries later.
    /// </summary>
    public static class RunPausePanelFactory
    {
        private const float Distance = 1.2f; // metres; arm's-length reading distance

        public static RunPausePanel Build(Camera targetCamera)
        {
            if (targetCamera == null) return null;
            if (EventSystem.current == null) return null;

            var canvasGo = new GameObject("Run Pause Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(620f, 640f);
            rt.localScale = Vector3.one * 0.001f; // 1 px = 1 mm

            var camT = targetCamera.transform;
            Vector3 fwd = camT.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            rt.SetPositionAndRotation(camT.position + fwd * Distance, Quaternion.LookRotation(fwd, Vector3.up));

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            AddLabel(rt, font, "PAUSED", 60, new Vector2(0f, 240f), new Vector2(560f, 90f));

            var panel = canvasGo.AddComponent<RunPausePanel>();
            panel.resumeButton = AddButton(rt, font, "RESUME", 120f);
            panel.settingsButton = AddButton(rt, font, "SETTINGS", 10f);
            panel.recalibrateButton = AddButton(rt, font, "RECALIBRATE HEIGHT", -100f);
            // Must follow the assignments above: AddComponent already ran Awake.
            panel.WireButtons();
            return panel;
        }

        private static void AddLabel(RectTransform parent, Font font, string text, int size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
        }

        /// <summary>A large button (480 x 90 mm) — pressed at arm's length with a controller ray.</summary>
        internal static Button AddButton(RectTransform parent, Font font, string label, float y)
        {
            var btnGo = new GameObject("Btn_" + label.Replace(' ', '_'));
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(480f, 90f);
            btnRt.anchoredPosition = new Vector2(0f, y);
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
            t.text = label;
            t.font = font;
            t.fontSize = 32;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            return button;
        }
    }
}
