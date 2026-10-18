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

        // P4-08 LOADOUT rack. P4-10 prices come from MetaProgression.UpgradeCost.
        private static readonly (string id, string name)[] Loadouts =
        {
            (WeaponId.Katana, "KATANA"),
            (WeaponId.Nodachi, "NODACHI"),
            (WeaponId.Tanto, "TANTO"),
            (WeaponId.TwinWakizashi, "TWIN WAKIZASHI"),
        };

        // P4-09 heat selector, in HeatRank order.
        private static readonly string[] HeatNames =
        {
            "SHARPENED PROGRAM", "HARDENED SHELLS", "QUICK TELEGRAPH", "SWARM PROTOCOL",
            "RUSTED FORGE", "NO SECOND CHANCES", "UNFORGIVING GUARD", "ESCORTED BOSSES",
        };

        // P4-10 archetype packs.
        private static readonly (string id, string name)[] Packs =
        {
            (BoonPackId.Bleed, "BLEED PACK"),
            (BoonPackId.Storm, "STORM PACK"),
            (BoonPackId.Executioner, "EXECUTIONER PACK"),
        };

        private static readonly string[] TabNames = { "UPGRADES", "ARMORY", "PACKS & KEEPSAKES", "HEAT" };
        private const int MaxKeepsakes = 8; // ponytail: 2 rows of 4; page the grid if more than 8 Epics exist

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
            // P4-10: back to the P2-01 1000x800 footprint (arm reach); the sections now live on tabbed pages.
            rt.sizeDelta = new Vector2(1000f, 800f);
            rt.localScale = Vector3.one * 0.001f;
            BoonOfferPanel.Place(rt, targetCamera.transform);
            canvasGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            var accent = new Color(0.5f, 0.9f, 1f);

            var panel = canvasGo.AddComponent<MetaShopPanel>();
            MakeText(rt, font, "SHRINE OF ECHOES", 48, TextAnchor.MiddleCenter, new Vector2(-150f, 345f), new Vector2(600f, 80f), Color.white);
            panel.echoesLabel = MakeText(rt, font, "", 34, TextAnchor.MiddleRight, new Vector2(330f, 345f), new Vector2(320f, 80f), new Color(1f, 0.85f, 0.4f));

            panel.tabs = new Button[TabNames.Length];
            panel.pages = new GameObject[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                panel.tabs[i] = MakeButton(rt, font, TabNames[i], new Vector2(-360f + i * 240f, 260f), new Vector2(230f, 70f));
                panel.tabs[i].GetComponentInChildren<Text>().fontSize = i == 2 ? 22 : 28;
                panel.pages[i] = MakePage(rt, TabNames[i]);
            }
            var upgrades = (RectTransform)panel.pages[0].transform;
            var armory = (RectTransform)panel.pages[1].transform;
            var packsPage = (RectTransform)panel.pages[2].transform;
            var heatPage = (RectTransform)panel.pages[3].transform;

            // UPGRADES (P2-01 rows)
            panel.rows = new MetaShopPanel.Row[Upgrades.Length];
            for (int i = 0; i < Upgrades.Length; i++)
            {
                float y = 160f - i * 100f;
                var r = new MetaShopPanel.Row { id = Upgrades[i].id };
                r.nameText = MakeText(upgrades, font, Upgrades[i].name, 30, TextAnchor.MiddleLeft, new Vector2(-300f, y), new Vector2(380f, 80f), Color.white);
                r.pipsText = MakeText(upgrades, font, "", 34, TextAnchor.MiddleCenter, new Vector2(10f, y), new Vector2(240f, 80f), accent);
                r.costText = MakeText(upgrades, font, "", 30, TextAnchor.MiddleCenter, new Vector2(210f, y), new Vector2(140f, 80f), new Color(1f, 0.85f, 0.4f));
                r.buy = MakeButton(upgrades, font, "BUY", new Vector2(390f, y), new Vector2(160f, 80f));
                panel.rows[i] = r;
            }

            // ARMORY: P4-08 LOADOUT rack (locked = buy on click) + P4-10 Art selector
            MakeText(armory, font, "LOADOUT", 34, TextAnchor.MiddleCenter, new Vector2(0f, 175f), new Vector2(400f, 50f), accent);
            panel.loadouts = new MetaShopPanel.Loadout[Loadouts.Length];
            for (int i = 0; i < Loadouts.Length; i++)
            {
                var b = MakeButton(armory, font, Loadouts[i].name, new Vector2(-345f + i * 230f, 95f), new Vector2(210f, 100f));
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 24;
                panel.loadouts[i] = new MetaShopPanel.Loadout { id = Loadouts[i].id, name = Loadouts[i].name, cost = MetaProgression.UpgradeCost(Loadouts[i].id, 1), label = label, button = b };
            }
            MakeText(armory, font, "ARTS", 34, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(400f, 50f), accent);
            panel.weaponArtButton = MakeButton(armory, font, "", new Vector2(-235f, -110f), new Vector2(440f, 120f));
            panel.weaponArtLabel = panel.weaponArtButton.GetComponentInChildren<Text>();
            panel.offhandArtButton = MakeButton(armory, font, "", new Vector2(235f, -110f), new Vector2(440f, 120f));
            panel.offhandArtLabel = panel.offhandArtButton.GetComponentInChildren<Text>();
            MakeText(armory, font, "TAP A SLOT TO CHANGE ITS ART", 24, TextAnchor.MiddleCenter, new Vector2(0f, -210f), new Vector2(900f, 40f), new Color(1f, 1f, 1f, 0.6f));

            // PACKS & KEEPSAKES
            MakeText(packsPage, font, "ARCHETYPE PACKS", 34, TextAnchor.MiddleCenter, new Vector2(0f, 175f), new Vector2(600f, 50f), accent);
            panel.packs = new MetaShopPanel.Loadout[Packs.Length];
            for (int i = 0; i < Packs.Length; i++)
            {
                var b = MakeButton(packsPage, font, Packs[i].name, new Vector2(-310f + i * 310f, 95f), new Vector2(290f, 100f));
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 26;
                panel.packs[i] = new MetaShopPanel.Loadout { id = Packs[i].id, name = Packs[i].name, cost = MetaProgression.UpgradeCost(Packs[i].id, 1), label = label, button = b };
            }
            MakeText(packsPage, font, "KEEPSAKES: START A RUN WITH A PAST EPIC", 30, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(900f, 50f), accent);
            var past = MetaProgression.PastEpics;
            int n = Mathf.Min(past.Count, MaxKeepsakes);
            if (n == 0)
                MakeText(packsPage, font, "PICK AN EPIC BOON IN A RUN TO UNLOCK ONE", 26, TextAnchor.MiddleCenter, new Vector2(0f, -110f), new Vector2(900f, 60f), new Color(1f, 1f, 1f, 0.6f));
            var catalog = RunDirector.Instance != null ? RunDirector.Instance.Catalog : null;
            panel.keepsakes = new MetaShopPanel.Loadout[n];
            for (int i = 0; i < n; i++)
            {
                var def = catalog != null ? catalog.Find(past[i]) : null;
                string name = def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName.ToUpperInvariant() : past[i];
                var b = MakeButton(packsPage, font, name, new Vector2(-345f + (i % 4) * 230f, -90f - (i / 4) * 100f), new Vector2(210f, 90f));
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 22;
                panel.keepsakes[i] = new MetaShopPanel.Loadout { id = past[i], name = name, cost = MetaProgression.KeepsakeCost, label = label, button = b };
            }

            // HEAT (P4-09; unlocks after the first win)
            if (MetaProgression.HeatUnlocked)
            {
                panel.heatTitle = MakeText(heatPage, font, "", 30, TextAnchor.MiddleCenter, new Vector2(0f, 175f), new Vector2(900f, 50f), new Color(1f, 0.5f, 0.35f));
                panel.heat = new MetaShopPanel.HeatSlot[Heat.Count];
                for (int i = 0; i < Heat.Count; i++)
                {
                    var b = MakeButton(heatPage, font, HeatNames[i], new Vector2(-345f + (i % 4) * 230f, 95f - (i / 4) * 100f), new Vector2(210f, 90f));
                    var label = b.GetComponentInChildren<Text>();
                    label.fontSize = 20;
                    panel.heat[i] = new MetaShopPanel.HeatSlot { rank = (HeatRank)i, name = HeatNames[i], label = label, button = b };
                }
                panel.dailyButton = MakeButton(heatPage, font, "DAILY FRACTURE", new Vector2(0f, -140f), new Vector2(360f, 80f));
            }
            else
                MakeText(heatPage, font, "FRACTURE PRESSURE UNLOCKS\nAFTER YOUR FIRST WIN", 34, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(900f, 120f), new Color(1f, 0.5f, 0.35f));

            panel.closeButton = MakeButton(rt, font, "CLOSE", new Vector2(0f, -345f), new Vector2(320f, 70f));
            panel.Wire();
            return panel;
        }

        private static GameObject MakePage(RectTransform parent, string name)
        {
            var go = new GameObject(name + " Page");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = parent.sizeDelta;
            return go;
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
