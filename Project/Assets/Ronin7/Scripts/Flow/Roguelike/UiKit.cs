using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// P5-07b: shared look for the runtime-built roguelike panels — TextMeshPro labels (the TMP default
    /// font is Rajdhani-SemiBold, whose Bold weight slot is Rajdhani-Bold; see UiFontBuilder) with the
    /// roadmap's minimum sizes at 0.001 canvas scale, and a procedural 9-slice dark-glass frame with a
    /// thin neon edge. Both frame sprites share one tiny texture so the frames of a panel batch together.
    /// </summary>
    public static class UiKit
    {
        public const float MinTitle = 48f; // px at 0.001 scale = 48 mm
        public const float MinBody = 28f;
        public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f, 1f);
        public static readonly Color GlassColor = new Color(0.02f, 0.03f, 0.06f, 0.85f);

        internal static float ClampSize(float size, bool title) => Mathf.Max(size, title ? MinTitle : MinBody);

        /// <summary>Adds a TextMeshProUGUI to <paramref name="go"/>; size clamped to the body/title minimum.</summary>
        public static TextMeshProUGUI Text(GameObject go, string text, float size, Color color,
            TextAlignmentOptions align, bool bold = false, bool title = false)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = ClampSize(size, title);
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align;
            t.color = color;
            return t;
        }

        /// <summary>Turns <paramref name="img"/> into the 9-slice dark glass and adds a thin neon edge child
        /// tinted <paramref name="accent"/> (call before adding content so the edge draws under it).</summary>
        public static Image Frame(Image img, Color accent, float alpha = 0.85f)
        {
            Ensure();
            img.sprite = glass;
            img.type = Image.Type.Sliced;
            img.color = new Color(GlassColor.r, GlassColor.g, GlassColor.b, alpha);

            var go = new GameObject("Edge");
            go.transform.SetParent(img.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var edge = go.AddComponent<Image>();
            edge.sprite = edgeSprite;
            edge.type = Image.Type.Sliced;
            edge.color = accent;
            edge.raycastTarget = false;
            return edge;
        }

        private const int Cell = 32, Border = 12;
        private const float Radius = 10f, EdgeWidth = 3f;
        private static Sprite glass, edgeSprite;

        private static void Ensure()
        {
            if (glass != null) return;
            var tex = new Texture2D(Cell * 2, Cell, TextureFormat.RGBA32, false)
            {
                name = "UiKit Glass", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[Cell * 2 * Cell];
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float d = RoundBoxDistance(x + 0.5f - Cell * 0.5f, y + 0.5f - Cell * 0.5f, Cell * 0.5f - 0.5f, Radius);
                float fill = Mathf.Clamp01(0.5f - d);
                float edge = fill * Mathf.Clamp01(d + EdgeWidth + 0.5f);
                px[y * Cell * 2 + x] = new Color32(255, 255, 255, (byte)(fill * 255f));
                px[y * Cell * 2 + Cell + x] = new Color32(255, 255, 255, (byte)(edge * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var border = new Vector4(Border, Border, Border, Border);
            glass = Sprite.Create(tex, new Rect(0, 0, Cell, Cell), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            edgeSprite = Sprite.Create(tex, new Rect(Cell, 0, Cell, Cell), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            glass.hideFlags = edgeSprite.hideFlags = HideFlags.DontSave;
        }

        /// <summary>Signed distance from (x, y) to a rounded box of half-size <paramref name="half"/> (negative inside).</summary>
        internal static float RoundBoxDistance(float x, float y, float half, float r)
        {
            float qx = Mathf.Abs(x) - half + r, qy = Mathf.Abs(y) - half + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
