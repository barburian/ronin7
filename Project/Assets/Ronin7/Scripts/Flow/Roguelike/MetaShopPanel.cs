using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>P2-01: the Shrine of Echoes. Four upgrade rows (name, level pips, cost, BUY) over
    /// <see cref="MetaProgression"/>. Built by <see cref="MetaShopPanelFactory"/>.</summary>
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
            public int cost; // P4-10 Shrine unlock price, shown while locked
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

        /// <summary>P4-08: locked weapons can't be picked (their button is also non-interactable).</summary>
        internal void SelectLoadout(string id)
        {
            if (MetaProgression.TrySelectLoadout(id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

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
                l.button.interactable = unlocked;
            }
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
