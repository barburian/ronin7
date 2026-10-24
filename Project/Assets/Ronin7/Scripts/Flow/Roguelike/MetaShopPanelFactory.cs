using Ronin7.Core;
using TMPro;
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
            rt.gameObject.AddComponent<UiScaleFollower>(); // P6-03
            BoonOfferPanel.Place(rt, targetCamera.transform);
            UiKit.Frame(canvasGo.AddComponent<Image>(), UiKit.Cyan);
            var accent = new Color(0.5f, 0.9f, 1f);

            var panel = canvasGo.AddComponent<MetaShopPanel>();
            MakeText(rt, "SHRINE OF ECHOES", 48, TextAlignmentOptions.Center, new Vector2(-150f, 345f), new Vector2(600f, 80f), Color.white);
            panel.echoesLabel = MakeText(rt, "", 34, TextAlignmentOptions.Right, new Vector2(330f, 345f), new Vector2(320f, 80f), new Color(1f, 0.85f, 0.4f));

            panel.tabs = new Button[TabNames.Length];
            panel.pages = new GameObject[TabNames.Length];
            for (int i = 0; i < TabNames.Length; i++)
            {
                panel.tabs[i] = MakeButton(rt, TabNames[i], new Vector2(-369f + i * 246f, 260f), new Vector2(242f, 70f)); // P5-07d: 242 px so "PACKS & KEEPSAKES" (238 px at 28 px bold) stays on one line
                panel.tabs[i].GetComponentInChildren<TMP_Text>().fontSize = UiKit.MinBody; // P5-07b: was 22/28 — clamped to the 28 px body minimum
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
                r.nameText = MakeText(upgrades, Upgrades[i].name, 30, TextAlignmentOptions.Left, new Vector2(-300f, y), new Vector2(380f, 80f), Color.white);
                r.pipsText = MakeText(upgrades, "", 34, TextAlignmentOptions.Center, new Vector2(10f, y), new Vector2(240f, 80f), accent);
                r.costText = MakeText(upgrades, "", 30, TextAlignmentOptions.Center, new Vector2(210f, y), new Vector2(140f, 80f), new Color(1f, 0.85f, 0.4f));
                r.buy = MakeButton(upgrades, "BUY", new Vector2(390f, y), new Vector2(160f, 80f));
                panel.rows[i] = r;
            }

            // ARMORY: P4-08 LOADOUT rack (locked = buy on click) + P4-10 Art selector
            MakeText(armory, "LOADOUT", 34, TextAlignmentOptions.Center, new Vector2(0f, 175f), new Vector2(400f, 50f), accent);
            panel.loadouts = new MetaShopPanel.Loadout[Loadouts.Length];
            for (int i = 0; i < Loadouts.Length; i++)
            {
                var b = MakeButton(armory, Loadouts[i].name, new Vector2(-345f + i * 230f, 95f), new Vector2(210f, 100f));
                var label = b.GetComponentInChildren<TMP_Text>();
                label.fontSize = UiKit.MinBody; // P5-07b: was 24
                panel.loadouts[i] = new MetaShopPanel.Loadout { id = Loadouts[i].id, name = Loadouts[i].name, cost = MetaProgression.UpgradeCost(Loadouts[i].id, 1), label = label, button = b };
            }
            MakeText(armory, "ARTS", 34, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(400f, 50f), accent);
            panel.weaponArtButton = MakeButton(armory, "", new Vector2(-235f, -110f), new Vector2(440f, 120f));
            panel.weaponArtLabel = panel.weaponArtButton.GetComponentInChildren<TMP_Text>();
            panel.offhandArtButton = MakeButton(armory, "", new Vector2(235f, -110f), new Vector2(440f, 120f));
            panel.offhandArtLabel = panel.offhandArtButton.GetComponentInChildren<TMP_Text>();
            MakeText(armory, "TAP A SLOT TO CHANGE ITS ART", 24, TextAlignmentOptions.Center, new Vector2(0f, -210f), new Vector2(900f, 40f), new Color(1f, 1f, 1f, 0.6f));

            // PACKS & KEEPSAKES
            MakeText(packsPage, "ARCHETYPE PACKS", 34, TextAlignmentOptions.Center, new Vector2(0f, 175f), new Vector2(600f, 50f), accent);
            panel.packs = new MetaShopPanel.Loadout[Packs.Length];
            for (int i = 0; i < Packs.Length; i++)
            {
                var b = MakeButton(packsPage, Packs[i].name, new Vector2(-310f + i * 310f, 95f), new Vector2(290f, 100f));
                var label = b.GetComponentInChildren<TMP_Text>();
                label.fontSize = UiKit.MinBody; // P5-07b: was 26
                panel.packs[i] = new MetaShopPanel.Loadout { id = Packs[i].id, name = Packs[i].name, cost = MetaProgression.UpgradeCost(Packs[i].id, 1), label = label, button = b };
            }
            MakeText(packsPage, "KEEPSAKES: START A RUN WITH A PAST EPIC", 30, TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(900f, 50f), accent);
            var past = MetaProgression.PastEpics;
            int n = Mathf.Min(past.Count, MaxKeepsakes);
            if (n == 0)
                MakeText(packsPage, "PICK AN EPIC BOON IN A RUN TO UNLOCK ONE", 26, TextAlignmentOptions.Center, new Vector2(0f, -110f), new Vector2(900f, 60f), new Color(1f, 1f, 1f, 0.6f));
            var catalog = RunDirector.Instance != null ? RunDirector.Instance.Catalog : null;
            panel.keepsakes = new MetaShopPanel.Loadout[n];
            for (int i = 0; i < n; i++)
            {
                var def = catalog != null ? catalog.Find(past[i]) : null;
                string name = def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName.ToUpperInvariant() : past[i];
                var b = MakeButton(packsPage, name, new Vector2(-345f + (i % 4) * 230f, -90f - (i / 4) * 100f), new Vector2(210f, 90f));
                var label = b.GetComponentInChildren<TMP_Text>();
                label.fontSize = UiKit.MinBody; // P5-07b: was 22
                panel.keepsakes[i] = new MetaShopPanel.Loadout { id = past[i], name = name, cost = MetaProgression.KeepsakeCost, label = label, button = b };
            }

            // HEAT (P4-09; unlocks after the first win)
            if (MetaProgression.HeatUnlocked)
            {
                panel.heatTitle = MakeText(heatPage, "", 30, TextAlignmentOptions.Center, new Vector2(0f, 175f), new Vector2(900f, 50f), new Color(1f, 0.5f, 0.35f));
                panel.heat = new MetaShopPanel.HeatSlot[Heat.Count];
                for (int i = 0; i < Heat.Count; i++)
                {
                    var b = MakeButton(heatPage, HeatNames[i], new Vector2(-345f + (i % 4) * 230f, 95f - (i / 4) * 100f), new Vector2(210f, 90f));
                    var label = b.GetComponentInChildren<TMP_Text>();
                    label.fontSize = UiKit.MinBody; // P5-07b: was 20
                    panel.heat[i] = new MetaShopPanel.HeatSlot { rank = (HeatRank)i, name = HeatNames[i], label = label, button = b };
                }
                panel.dailyButton = MakeButton(heatPage, "DAILY FRACTURE", new Vector2(0f, -140f), new Vector2(360f, 80f));
            }
            else
                MakeText(heatPage, "FRACTURE PRESSURE UNLOCKS\nAFTER YOUR FIRST WIN", 34, TextAlignmentOptions.Center, new Vector2(0f, 40f), new Vector2(900f, 120f), new Color(1f, 0.5f, 0.35f));

            panel.closeButton = MakeButton(rt, "CLOSE", new Vector2(0f, -345f), new Vector2(320f, 70f));
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

        private static TMP_Text MakeText(RectTransform parent, string s, int size, TextAlignmentOptions anchor, Vector2 pos, Vector2 dim, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            return UiKit.Text(go, s, size, color, anchor, bold: true, title: size >= UiKit.MinTitle);
        }

        private static Button MakeButton(RectTransform parent, string label, Vector2 pos, Vector2 dim)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dim;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            UiKit.Frame(img, UiKit.Cyan, 0.95f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.25f); // unaffordable / maxed reads as dimmed
            b.colors = colors;
            var lt = MakeText(rt, label, 30, TextAlignmentOptions.Center, Vector2.zero, dim, Color.white);
            lt.rectTransform.anchorMin = Vector2.zero;
            lt.rectTransform.anchorMax = Vector2.one;
            lt.rectTransform.offsetMin = lt.rectTransform.offsetMax = Vector2.zero;
            return b;
        }
    }
}
