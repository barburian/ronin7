using Ronin7.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>Builds the worldspace Shrine of Echoes panel from primitives (same idiom as
    /// <see cref="BoonOfferPanelFactory"/>). Returns null without a camera or EventSystem.</summary>
    public static class MetaShopPanelFactory
    {
        private static readonly (string id, string name)[] Upgrades =
        {
            (MetaUpgradeId.StartingHealth, "STARTING HEALTH"),
            (MetaUpgradeId.StartingDamage, "STARTING DAMAGE"),
            (MetaUpgradeId.StartingBoon, "STARTING BOON"),
            (MetaUpgradeId.RerollTokens, "REROLL TOKENS"),
        };

        public static MetaShopPanel Build(Camera targetCamera)
        {
            if (targetCamera == null || EventSystem.current == null) return null;

            var canvasGo = new GameObject("Meta Shop Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 620f);
            rt.localScale = Vector3.one * 0.001f;
            BoonOfferPanel.Place(rt, targetCamera.transform);
            canvasGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var panel = canvasGo.AddComponent<MetaShopPanel>();
            MakeText(rt, font, "SHRINE OF ECHOES", 48, TextAnchor.MiddleCenter, new Vector2(-150f, 255f), new Vector2(600f, 80f), Color.white);
            panel.echoesLabel = MakeText(rt, font, "", 34, TextAnchor.MiddleRight, new Vector2(330f, 255f), new Vector2(320f, 80f), new Color(1f, 0.85f, 0.4f));

            panel.rows = new MetaShopPanel.Row[Upgrades.Length];
            for (int i = 0; i < Upgrades.Length; i++)
            {
                float y = 125f - i * 100f;
                var r = new MetaShopPanel.Row { id = Upgrades[i].id };
                r.nameText = MakeText(rt, font, Upgrades[i].name, 30, TextAnchor.MiddleLeft, new Vector2(-300f, y), new Vector2(380f, 80f), Color.white);
                r.pipsText = MakeText(rt, font, "", 34, TextAnchor.MiddleCenter, new Vector2(10f, y), new Vector2(240f, 80f), new Color(0.5f, 0.9f, 1f));
                r.costText = MakeText(rt, font, "", 30, TextAnchor.MiddleCenter, new Vector2(210f, y), new Vector2(140f, 80f), new Color(1f, 0.85f, 0.4f));
                r.buy = MakeButton(rt, font, "BUY", new Vector2(390f, y), new Vector2(160f, 80f));
                panel.rows[i] = r;
            }
            panel.closeButton = MakeButton(rt, font, "CLOSE", new Vector2(0f, -270f), new Vector2(320f, 70f));
            panel.Wire();
            return panel;
        }

        private static Text MakeText(RectTransform parent, Font font, string s, int size, TextAnchor anchor, Vector2 pos, Vector2 dim, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            var t = go.AddComponent<Text>();
            t.text = s;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = color;
            return t;
        }

        private static Button MakeButton(RectTransform parent, Font font, string label, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.28f, 0.95f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f); // unaffordable / maxed reads as dimmed
            b.colors = colors;
            var lt = MakeText(rt, font, label, 30, TextAnchor.MiddleCenter, Vector2.zero, dim, Color.white);
            lt.rectTransform.anchorMin = Vector2.zero;
            lt.rectTransform.anchorMax = Vector2.one;
            lt.rectTransform.offsetMin = lt.rectTransform.offsetMax = Vector2.zero;
            return b;
        }
    }
}
