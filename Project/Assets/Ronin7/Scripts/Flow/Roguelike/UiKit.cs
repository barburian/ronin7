using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// P5-07b: shared look for the runtime-built roguelike panels — TextMeshPro labels (the TMP default
    /// font is Rajdhani-SemiBold, whose Bold weight slot is Rajdhani-Bold; see UiFontBuilder) with the
    /// roadmap's minimum sizes at 0.001 canvas scale, and a baked 9-slice dark-glass frame with a thin
    /// neon edge (one sprite, see <see cref="FrameSprite"/>).
    /// </summary>
    public static class UiKit
    {
        public const float MinTitle = 48f; // px at 0.001 scale = 48 mm
        public const float MinBody = 28f;
        public static readonly Color Cyan = new Color(0.3f, 0.9f, 1f, 1f);

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

        /// <summary>P5-07d (A13.7): resource name of the baked 9-slice frame (dark glass fill + neon edge in ONE
        /// sprite, built by BoonIconBuilder into the BoonIcons atlas folder so frames and boon icons share a texture
        /// in builds).</summary>
        public const string FrameSprite = "UiKitFrame";
        private static Sprite frame;

        /// <summary>Turns <paramref name="img"/> into the 9-slice glass frame. The whole frame is tinted by
        /// <paramref name="accent"/>: the edge texels are white (full accent), the fill texels near-black (dark
        /// accent glass). Returns <paramref name="img"/>; recolour it to change the edge.</summary>
        public static Image Frame(Image img, Color accent, float alpha = 0.85f)
        {
            if (frame == null) frame = Resources.Load<Sprite>(FrameSprite);
            img.sprite = frame;
            img.type = Image.Type.Sliced;
            img.color = new Color(accent.r, accent.g, accent.b, alpha);
            return img;
        }

        /// <summary>Signed distance from (x, y) to a rounded box of half-size <paramref name="half"/> (negative inside).</summary>
        public static float RoundBoxDistance(float x, float y, float half, float r)
        {
            float qx = Mathf.Abs(x) - half + r, qy = Mathf.Abs(y) - half + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
