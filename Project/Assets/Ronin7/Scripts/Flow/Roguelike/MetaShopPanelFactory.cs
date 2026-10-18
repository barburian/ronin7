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

        // P4-08 LOADOUT rack. Costs are the P4-10 Shrine unlock prices (shown while locked).
        private static readonly (string id, string name, int cost)[] Loadouts =
        {
            (WeaponId.Katana, "KATANA", 0),
            (WeaponId.Nodachi, "NODACHI", 150),
            (WeaponId.Tanto, "TANTO", 150),
            (WeaponId.TwinWakizashi, "TWIN WAKIZASHI", 300),
        };

        // P4-09 heat selector, in HeatRank order.
        private static readonly string[] HeatNames =
        {
            "SHARPENED PROGRAM", "HARDENED SHELLS", "QUICK TELEGRAPH", "SWARM PROTOCOL",
            "RUSTED FORGE", "NO SECOND CHANCES", "UNFORGIVING GUARD", "ESCORTED BOSSES",
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
            // P4-09: the heat section (shown only once heat unlocks) adds 330 under the rack; yo lifts the rest to make room.
            bool heatOn = MetaProgression.HeatUnlocked;
            float yo = heatOn ? 165f : 0f;
            rt.sizeDelta = new Vector2(1000f, heatOn ? 1130f : 800f); // P4-08: +180 for the LOADOUT rack under the upgrades
            rt.localScale = Vector3.one * 0.001f;
            BoonOfferPanel.Place(rt, targetCamera.transform);
            canvasGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            var panel = canvasGo.AddComponent<MetaShopPanel>();
            MakeText(rt, font, "SHRINE OF ECHOES", 48, TextAnchor.MiddleCenter, new Vector2(-150f, 345f + yo), new Vector2(600f, 80f), Color.white);
            panel.echoesLabel = MakeText(rt, font, "", 34, TextAnchor.MiddleRight, new Vector2(330f, 345f + yo), new Vector2(320f, 80f), new Color(1f, 0.85f, 0.4f));

            panel.rows = new MetaShopPanel.Row[Upgrades.Length];
            for (int i = 0; i < Upgrades.Length; i++)
            {
                float y = 215f + yo - i * 100f;
                var r = new MetaShopPanel.Row { id = Upgrades[i].id };
                r.nameText = MakeText(rt, font, Upgrades[i].name, 30, TextAnchor.MiddleLeft, new Vector2(-300f, y), new Vector2(380f, 80f), Color.white);
                r.pipsText = MakeText(rt, font, "", 34, TextAnchor.MiddleCenter, new Vector2(10f, y), new Vector2(240f, 80f), new Color(0.5f, 0.9f, 1f));
                r.costText = MakeText(rt, font, "", 30, TextAnchor.MiddleCenter, new Vector2(210f, y), new Vector2(140f, 80f), new Color(1f, 0.85f, 0.4f));
                r.buy = MakeButton(rt, font, "BUY", new Vector2(390f, y), new Vector2(160f, 80f));
                panel.rows[i] = r;
            }
            MakeText(rt, font, "LOADOUT", 34, TextAnchor.MiddleCenter, new Vector2(0f, -195f + yo), new Vector2(400f, 50f), new Color(0.5f, 0.9f, 1f));
            panel.loadouts = new MetaShopPanel.Loadout[Loadouts.Length];
            for (int i = 0; i < Loadouts.Length; i++)
            {
                var b = MakeButton(rt, font, Loadouts[i].name, new Vector2(-345f + i * 230f, -270f + yo), new Vector2(210f, 90f));
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 24;
                panel.loadouts[i] = new MetaShopPanel.Loadout { id = Loadouts[i].id, name = Loadouts[i].name, cost = Loadouts[i].cost, label = label, button = b };
            }
            if (heatOn)
            {
                panel.heatTitle = MakeText(rt, font, "", 30, TextAnchor.MiddleCenter, new Vector2(0f, -175f), new Vector2(900f, 50f), new Color(1f, 0.5f, 0.35f));
                panel.heat = new MetaShopPanel.HeatSlot[Heat.Count];
                for (int i = 0; i < Heat.Count; i++)
                {
                    var b = MakeButton(rt, font, HeatNames[i], new Vector2(-345f + (i % 4) * 230f, -255f - (i / 4) * 90f), new Vector2(210f, 80f));
                    var label = b.GetComponentInChildren<Text>();
                    label.fontSize = 20;
                    panel.heat[i] = new MetaShopPanel.HeatSlot { rank = (HeatRank)i, name = HeatNames[i], label = label, button = b };
                }
                panel.dailyButton = MakeButton(rt, font, "DAILY FRACTURE", new Vector2(-180f, -470f), new Vector2(320f, 70f));
            }
            panel.closeButton = MakeButton(rt, font, "CLOSE", new Vector2(heatOn ? 180f : 0f, heatOn ? -470f : -360f), new Vector2(320f, 70f));
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
