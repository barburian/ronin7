using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>P2-01: the Shrine of Echoes over <see cref="MetaProgression"/>. P4-10: four tabbed pages
    /// (UPGRADES / ARMORY / PACKS &amp; KEEPSAKES / HEAT) so it stays in the 1000x800 arm-reach footprint.
    /// Locked items buy on click. Built by <see cref="MetaShopPanelFactory"/>.</summary>
    public class MetaShopPanel : MonoBehaviour
    {
        internal struct Row
        {
            public string id;
            public Text nameText, pipsText, costText;
            public Button buy;
        }

        /// <summary>P4-08 LOADOUT rack slot: one button per weapon.</summary>
        internal struct Loadout
        {
            public string id, name;
            public int cost; // P4-10 Shrine unlock price, shown while locked (also used for packs and Keepsakes)
            public Text label;
            public Button button;
        }

        /// <summary>P4-09 heat selector slot: one button per <see cref="HeatRank"/>; a click cycles 0..max.</summary>
        internal struct HeatSlot
        {
            public HeatRank rank;
            public string name;
            public Text label;
            public Button button;
        }

        internal Row[] rows;
        internal HeatSlot[] heat = System.Array.Empty<HeatSlot>(); // empty until heat unlocks (first win)
        internal Text heatTitle;
        internal Button dailyButton;
        internal Loadout[] loadouts = System.Array.Empty<Loadout>();
        internal Loadout[] packs = System.Array.Empty<Loadout>();     // P4-10
        internal Loadout[] keepsakes = System.Array.Empty<Loadout>(); // P4-10: one per past Epic
        internal Text weaponArtLabel, offhandArtLabel;                 // P4-10 Art selector
        internal Button weaponArtButton, offhandArtButton;
        internal GameObject[] pages = System.Array.Empty<GameObject>(); // P4-10 tabs
        internal Button[] tabs = System.Array.Empty<Button>();
        internal int tab;
        private static readonly Color TabIdle = new Color(0.2f, 0.2f, 0.28f, 0.95f), TabActive = new Color(0.25f, 0.45f, 0.6f, 0.95f);

        // P4-10 Art selector cycles, in order ("" = none).
        internal static readonly string[] WeaponArts = { "", ArtId.ChargedSlash, ArtId.BladeThrow };
        internal static readonly string[] OffhandArts = { "", ArtId.KiaiPalm, ArtId.ChainHook, ArtId.ElementImbue };
        internal Text echoesLabel;
        internal Button closeButton;
        internal System.Action onClosed;
        private readonly LazyFollow follow = new LazyFollow(1.3f);

        internal void Wire()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                string id = rows[i].id;
                rows[i].buy.onClick.AddListener(() => Buy(id));
            }
            foreach (var l in loadouts)
            {
                string id = l.id;
                l.button.onClick.AddListener(() => SelectLoadout(id));
            }
            foreach (var h in heat)
            {
                HeatRank r = h.rank;
                h.button.onClick.AddListener(() => CycleHeat(r));
            }
            foreach (var p in packs)
            {
                string id = p.id;
                p.button.onClick.AddListener(() => Buy(id));
            }
            foreach (var k in keepsakes)
            {
                string id = k.id;
                k.button.onClick.AddListener(() => ClickKeepsake(id));
            }
            if (weaponArtButton != null) weaponArtButton.onClick.AddListener(() => ClickArt(true));
            if (offhandArtButton != null) offhandArtButton.onClick.AddListener(() => ClickArt(false));
            for (int i = 0; i < tabs.Length; i++)
            {
                int t = i;
                tabs[i].onClick.AddListener(() => SelectTab(t));
            }
            SelectTab(tab);
            if (dailyButton != null) dailyButton.onClick.AddListener(StartDaily);
            closeButton.onClick.AddListener(() => Destroy(gameObject));
            Refresh();
        }

        internal void Buy(string id)
        {
            if (MetaProgression.TryPurchase(id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.6f, 0.1f);
                // AudioDirector (which references Flow, so we can't call it) plays its boonChosen cue on this event.
                EventBus.Publish(new BoonChosen(id));
            }
            Refresh();
        }

        internal void SelectTab(int t)
        {
            tab = t;
            for (int i = 0; i < pages.Length; i++) pages[i].SetActive(i == t);
            for (int i = 0; i < tabs.Length; i++) tabs[i].image.color = i == t ? TabActive : TabIdle;
        }

        /// <summary>P4-08: picks an unlocked weapon. P4-10: a locked one is bought instead (button only live when affordable).</summary>
        internal void SelectLoadout(string id)
        {
            if (!MetaProgression.IsWeaponUnlocked(id)) { Buy(id); return; }
            if (MetaProgression.TrySelectLoadout(id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        /// <summary>P4-10: next entry of an Art cycle, wrapping; an unknown id restarts at the first entry.</summary>
        internal static string NextArt(string[] cycle, string current) =>
            cycle[(System.Array.IndexOf(cycle, current ?? "") + 1) % cycle.Length];

        internal static string ArtName(string id) => id switch
        {
            ArtId.ChargedSlash => "CHARGED SLASH",
            ArtId.BladeThrow => "BLADE THROW",
            ArtId.KiaiPalm => "KIAI PALM",
            ArtId.ChainHook => "CHAIN HOOK",
            ArtId.ElementImbue => "ELEMENT IMBUE",
            _ => "NONE",
        };

        /// <summary>P4-10: an unbought slot buys; a bought one cycles its Art.</summary>
        internal void ClickArt(bool weapon)
        {
            if (!MetaProgression.HasArtSlot(weapon)) { Buy(weapon ? MetaUpgradeId.WeaponArtSlot : MetaUpgradeId.OffhandArtSlot); return; }
            if (MetaProgression.TryEquipArt(weapon, NextArt(weapon ? WeaponArts : OffhandArts, MetaProgression.EquippedArt(weapon))))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        /// <summary>P4-10: an unowned Keepsake buys; an owned one toggles as the next run's Keepsake.</summary>
        internal void ClickKeepsake(string id)
        {
            if (!MetaProgression.HasKeepsake(id))
            {
                if (MetaProgression.TryBuyKeepsake(id))
                {
                    Haptics.Pulse(XRNode.RightHand, 0.6f, 0.1f);
                    EventBus.Publish(new BoonChosen(id));
                }
            }
            else if (MetaProgression.TrySelectKeepsake(MetaProgression.SelectedKeepsake == id ? "" : id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        internal static string ArtText(bool weapon, bool owned, string artId, int cost) =>
            (weapon ? "WEAPON ART" : "OFF-HAND ART") + "\n" + (owned ? ArtName(artId) : "LOCKED ◈ " + cost);

        internal static string OwnedText(string name, bool owned, bool selected, int cost) =>
            name + "\n" + (!owned ? "◈ " + cost : selected ? "● EQUIPPED" : "OWNED");

        /// <summary>P4-09: next rank on a click, wrapping max -> 0.</summary>
        internal static int NextHeatRank(int current, int max) => current >= max ? 0 : current + 1;

        internal void CycleHeat(HeatRank r)
        {
            // Locked while a saved run exists: RunState re-reads the pick on resume until P4-11 snapshots it.
            if (RunEntryOptions.HasSavedRun()) { Refresh(); return; }
            if (MetaProgression.TrySetHeatRank(r, NextHeatRank(MetaProgression.HeatRankOf(r), Heat.Max(r))))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        /// <summary>P4-09 DAILY FRACTURE: today's UTC seed with the fixed daily heat set. Not offered while a run is saved.</summary>
        internal void StartDaily()
        {
            if (RunState.InRun || RunEntryOptions.HasSavedRun() || RunDirector.Instance == null) return;
            Destroy(gameObject);
            RunDirector.Instance.StartRun(Heat.DailySeed(System.DateTime.UtcNow));
        }

        internal static string HeatTitleText(int totalRanks) =>
            "FRACTURE PRESSURE  ECHOES x" + RunDirector.HeatEchoMultiplier(totalRanks).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        internal static string LoadoutText(string name, bool unlocked, bool selected, int cost) =>
            !unlocked ? name + "\nLOCKED ◈ " + cost : selected ? "● " + name : name; // glyphs the LegacyRuntime font already renders

        internal void Refresh()
        {
            echoesLabel.text = EchoesText(MetaProgression.Echoes);
            foreach (var r in rows)
            {
                int level = MetaProgression.UpgradeLevel(r.id), max = MetaProgression.MaxLevel(r.id);
                int cost = MetaProgression.UpgradeCost(r.id, level + 1);
                r.pipsText.text = Pips(level, max);
                r.costText.text = level >= max ? "MAX" : "◈ " + cost;
                r.buy.interactable = level < max && MetaProgression.Echoes >= cost;
            }
            string selected = MetaProgression.SelectedLoadout;
            foreach (var l in loadouts)
            {
                bool unlocked = MetaProgression.IsWeaponUnlocked(l.id);
                l.label.text = LoadoutText(l.name, unlocked, l.id == selected, l.cost);
                l.button.interactable = unlocked || MetaProgression.Echoes >= l.cost;
            }
            foreach (var p in packs)
            {
                bool owned = MetaProgression.IsPackUnlocked(p.id);
                p.label.text = OwnedText(p.name, owned, false, p.cost);
                p.button.interactable = !owned && MetaProgression.Echoes >= p.cost;
            }
            string keep = MetaProgression.SelectedKeepsake;
            foreach (var k in keepsakes)
            {
                bool owned = MetaProgression.HasKeepsake(k.id);
                k.label.text = OwnedText(k.name, owned, k.id == keep, k.cost);
                k.button.interactable = owned || MetaProgression.Echoes >= k.cost;
            }
            RefreshArt(weaponArtLabel, weaponArtButton, true);
            RefreshArt(offhandArtLabel, offhandArtButton, false);
            if (heat.Length == 0) return;
            bool saved = RunEntryOptions.HasSavedRun();
            int total = 0;
            foreach (var h in heat)
            {
                int n = MetaProgression.HeatRankOf(h.rank);
                total += n;
                h.label.text = h.name + "\n" + Pips(n, Heat.Max(h.rank));
                h.button.interactable = !saved;
            }
            heatTitle.text = saved ? "FRACTURE PRESSURE  LOCKED: RUN SAVED" : HeatTitleText(total);
            if (dailyButton != null) dailyButton.interactable = !saved;
        }

        private static void RefreshArt(Text label, Button button, bool weapon)
        {
            if (label == null) return;
            bool owned = MetaProgression.HasArtSlot(weapon);
            int cost = MetaProgression.UpgradeCost(weapon ? MetaUpgradeId.WeaponArtSlot : MetaUpgradeId.OffhandArtSlot, 1);
            label.text = ArtText(weapon, owned, MetaProgression.EquippedArt(weapon), cost);
            button.interactable = owned || MetaProgression.Echoes >= cost;
        }

        internal static string EchoesText(int n) => "◈ ECHOES " + n;

        internal static string Pips(int level, int max) =>
            new string('●', Mathf.Clamp(level, 0, max)) + new string('○', Mathf.Max(0, max - level));

        private void OnDestroy() => onClosed?.Invoke();

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null) follow.Tick(transform, cam.transform, Time.unscaledDeltaTime);
        }
    }
}
