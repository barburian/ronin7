using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// P2-02: a non-interactive world-space title ("SECTOR 2 — THE PROGRAM", "FRACTURE CLOSED") placed
    /// 1.3 m in front of the head and destroyed after its duration. No EventSystem needed; static in
    /// the world (no follow, no motion) so it is comfort-safe.
    /// </summary>
    public static class RunTitleCard
    {
        public static GameObject Show(Camera cam, string text, Color color, float seconds)
        {
            if (cam == null) return null;
            var go = new GameObject("Run Title Card");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1600f, 300f);
            rt.localScale = Vector3.one * 0.001f;
            BoonOfferPanel.Place(rt, cam.transform);

            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(20f, 10f);
            labelRt.offsetMax = new Vector2(-20f, -10f);
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = 110;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 48;
            label.resizeTextMaxSize = 110;
            label.color = color;

            Object.Destroy(go, seconds);
            return go;
        }
    }
}
