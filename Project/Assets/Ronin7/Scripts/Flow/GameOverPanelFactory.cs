using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Builds the worldspace "Game Over" panel from primitives so the flow manager doesn't
    /// have to. The flow manager keeps the timing / dismiss-race concerns; widget construction
    /// lives here. No prefab needed — matches the grey-box runtime-built UI style used
    /// elsewhere in the project.
    /// </summary>
    public static class GameOverPanelFactory
    {
        /// <summary>
        /// Construct the panel anchored ~1 m in front of <paramref name="targetCamera"/>, at
        /// eye height, facing the camera. Returns null if no camera was supplied.
        /// </summary>
        public static GameOverPanel Build(Camera targetCamera)
        {
            if (targetCamera == null) return null;

            var canvasGo = new GameObject("Game Over Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 400f);
            rt.localScale = Vector3.one * 0.001f;
            // Pin 1m in front of the eyes at eye height, facing the camera.
            var camT = targetCamera.transform;
            rt.position = camT.position + camT.forward * 1.0f;
            rt.rotation = Quaternion.LookRotation(rt.position - camT.position, Vector3.up);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            BuildTitle(rt, font);
            var button = BuildReturnButton(rt, font);

            var panel = canvasGo.AddComponent<GameOverPanel>();
            panel.returnButton = button;
            return panel;
        }

        private static void BuildTitle(RectTransform parent, Font font)
        {
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(parent, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.sizeDelta = new Vector2(560f, 120f);
            titleRt.anchoredPosition = new Vector2(0f, 80f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "GAME OVER";
            titleText.font = font;
            titleText.fontSize = 60;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.3f, 0.3f);
        }

        private static Button BuildReturnButton(RectTransform parent, Font font)
        {
            var btnGo = new GameObject("ReturnButton");
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(400f, 80f);
            btnRt.anchoredPosition = new Vector2(0f, -100f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnRt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "RETURN TO MENU";
            labelText.font = font;
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;

            return button;
        }
    }
}
