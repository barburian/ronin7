using Ronin7.Core;
using Ronin7.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>P2-01: the Shrine of Echoes over <see cref="MetaProgression"/>. P4-10: four tabbed pages
    /// (UPGRADES / ARMORY / HEAT) so it stays in the 1000x800 arm-reach footprint.
    /// Locked items buy on click. Built by <see cref="MetaShopPanelFactory"/>.</summary>
    public class MetaShopPanel : MonoBehaviour
    {
        internal struct Row
        {
            public string id;
            public TMP_Text nameText, pipsText, costText;
            public Button buy;
        }

        /// <summary>P4-08 LOADOUT rack slot: one button per weapon.</summary>
        internal struct Loadout
        {
            public string id, name;
            public int cost; // P4-10 Shrine unlock price, shown while locked
            public TMP_Text label;
            public Button button;
        }

        /// <summary>P4-09 heat selector slot: one button per <see cref="HeatRank"/>; a click cycles 0..max.</summary>
        internal struct HeatSlot
        {
            public HeatRank rank;
            public string name;
            public TMP_Text label;
            public Button button;
        }

        internal Row[] rows;
        internal HeatSlot[] heat = System.Array.Empty<HeatSlot>(); // empty until heat unlocks (first win)
        internal TMP_Text heatTitle;
        internal Button dailyButton;
        internal Loadout[] loadouts = System.Array.Empty<Loadout>();
        internal TMP_Text weaponArtLabel, offhandArtLabel, weaponArtHelp, offhandArtHelp;                // P4-10 Art selector
        internal Button weaponArtButton, offhandArtButton;
        internal GameObject[] pages = System.Array.Empty<GameObject>(); // P4-10 tabs
        internal Button[] tabs = System.Array.Empty<Button>();
        internal int tab;
        private static readonly Color TabIdle = new Color(0.2f, 0.2f, 0.28f, 0.95f), TabActive = new Color(0.25f, 0.45f, 0.6f, 0.95f);

        // P4-10 Art selector cycles, in order. R9: no NONE entry, a slot always holds an Art.
        internal static readonly string[] WeaponArts = { ArtId.ChargedSlash, ArtId.BladeThrow };
        internal static readonly string[] OffhandArts = { ArtId.KiaiPalm, ArtId.ChainHook, ArtId.ElementImbue };
        internal TMP_Text echoesLabel;
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
                l.button.onClick.AddListener(() => Buy(id)); // A15.13: buy-only, WeaponSelectPanel picks
            }
            foreach (var h in heat)
            {
                HeatRank r = h.rank;
                h.button.onClick.AddListener(() => CycleHeat(r));
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

        internal void Buy(string id) => AfterBuy(MetaProgression.TryPurchase(id), id);

        private void AfterBuy(bool bought, string id)
        {
            if (bought)
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

        /// <summary>R9: what the Art does and which trigger fires it, one short line for the Armory.</summary>
        internal static string ArtHelp(string id) => id switch
        {
            ArtId.ChargedSlash => "HOLD TRIGGER, RELEASE\nMID-SWING TO SLASH",
            ArtId.BladeThrow => "HOLD TRIGGER, LET GO OF\nGRIP WHILE SWINGING TO THROW",
            ArtId.KiaiPalm => "HOLD TRIGGER, THRUST\nEMPTY HAND TO SHOVE",
            ArtId.ChainHook => "AIM AT AN ENEMY, HOLD\nTRIGGER, YANK HAND BACK",
            ArtId.ElementImbue => "HOLD TRIGGER, SWEEP HAND\nALONG THE BLADE",
            _ => "",
        };

        /// <summary>A click cycles the slot's Art.</summary>
        internal void ClickArt(bool weapon)
        {
            if (MetaProgression.TryEquipArt(weapon, NextArt(weapon ? WeaponArts : OffhandArts, MetaProgression.EquippedArt(weapon))))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        internal static string ArtText(bool weapon, string artId) =>
            (weapon ? "WEAPON ART" : "OFF-HAND ART") + "\n" + ArtName(artId);

        internal static string OwnedText(string name, bool owned, bool selected, int cost) =>
            name + "\n" + (!owned ? "♦ " + cost : selected ? "● EQUIPPED" : "OWNED");

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

        /// <summary>P4-09 DAILY FRACTURE: today's UTC seed with the fixed daily heat set and the last weapon pick
        /// (A15.13: skips the select screen). Not offered while a run is saved.</summary>
        internal void StartDaily()
        {
            if (RunState.InRun || RunEntryOptions.HasSavedRun() || RunDirector.Instance == null) return;
            Destroy(gameObject);
            RunDirector.Instance.StartRun(Heat.DailySeed(System.DateTime.UtcNow));
        }

        internal static string HeatTitleText(int totalRanks) =>
            "FRACTURE PRESSURE  ECHOES x" + RunDirector.HeatEchoMultiplier(totalRanks).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Armory-sized requirement (the 210 px button fits two short lines); the select card spells it out.</summary>
        internal static string LockedShort(string id)
        {
            if (id == WeaponId.Whip) return "WIN, " + MetaProgression.WhipMaxHits + " HITS MAX";
            int cost = MetaProgression.UpgradeCost(id, 1), wins = MetaProgression.WinsRequired(id);
            return (cost > 0 ? "♦ " + cost : "") + (wins > 0 ? " + " + wins + "W" : "");
        }

        /// <summary>A15.13: a loadout button only buys. Cost 0 is not for sale (A15.12, the Whip); an owned weapon is done.</summary>
        internal static bool LoadoutInteractable(bool unlocked, int echoes, int cost) => !unlocked && cost > 0 && echoes >= cost;

        internal void Refresh()
        {
            echoesLabel.text = EchoesText(MetaProgression.Echoes);
            foreach (var r in rows)
            {
                int level = MetaProgression.UpgradeLevel(r.id), max = MetaProgression.MaxLevel(r.id);
                int cost = MetaProgression.UpgradeCost(r.id, level + 1);
                r.pipsText.text = Pips(level, max);
                r.costText.text = level >= max ? "MAX" : "♦ " + cost;
                r.buy.interactable = level < max && MetaProgression.Echoes >= cost;
            }
            foreach (var l in loadouts)
            {
                bool unlocked = MetaProgression.IsWeaponUnlocked(l.id);
                l.label.text = l.name + "\n" + (unlocked ? "OWNED" : LockedShort(l.id));
                l.button.interactable = LoadoutInteractable(unlocked, MetaProgression.Echoes, l.cost) && MetaProgression.RunsWon >= MetaProgression.WinsRequired(l.id);
            }
            RefreshArt(weaponArtLabel, weaponArtHelp, true);
            RefreshArt(offhandArtLabel, offhandArtHelp, false);
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

        private static void RefreshArt(TMP_Text label, TMP_Text help, bool weapon)
        {
            if (label == null) return;
            string art = MetaProgression.EquippedArt(weapon);
            label.text = ArtText(weapon, art);
            if (weapon && !WeaponId.AllowsWeaponArt(MetaProgression.SelectedLoadout)) label.text += "\nUNAVAILABLE ON THIS WEAPON"; // A15.18
            if (help != null) help.text = ArtHelp(art);
        }

        internal static string EchoesText(int n) => "ECHOES " + n; // P4-11: ♦ is the Shard glyph now

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
