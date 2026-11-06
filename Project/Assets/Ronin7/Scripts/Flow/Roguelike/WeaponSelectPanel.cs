using Ronin7.Core;
using Ronin7.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Ronin7.Flow
{
    /// <summary>P9-01 (A15.13): START RUN -> WEAPON SELECT -> START. Four cards (A15.1 roster order) that only select;
    /// START begins the run, BACK returns to the menu. Locked cards stay visible, dimmed, and cannot be picked.
    /// Buying lives in Shrine > ARMORY. Built by <see cref="WeaponSelectPanelFactory"/>.</summary>
    public class WeaponSelectPanel : MonoBehaviour
    {
        internal struct Card
        {
            public string id;
            public Button button;
            public TMP_Text stateText;
            public CanvasGroup group;
        }

        /// <summary>Roster order, display name and the one-line "how it plays" copy (A15.4-A15.9).</summary>
        internal static readonly (string id, string name, string blurb)[] Cards =
        {
            (WeaponId.Katana, "KATANA", "Fast, clean cuts. Parry to stagger."),
            (WeaponId.TwinWakizashi, "DOUBLE KATANA", "A blade in each hand. Quicker, lighter hits."),
            (WeaponId.PolearmShield, "POLEARM + SHIELD", "Thrust from range. Raise the shield to block."),
            (WeaponId.Whip, "WHIP", "Crack at range, lasso a foe, no parry."),
        };

        internal Card[] cards = System.Array.Empty<Card>();
        internal Button startButton, backButton;
        internal Button[] modeButtons = System.Array.Empty<Button>(); // R13: Easy / Normal / Hard, index = (int)DifficultyMode
        internal TMP_Text modeText;
        internal System.Action startRun; // set by the menu: begins the run
        internal System.Action onClosed;
        private readonly LazyFollow follow = new LazyFollow(1.3f);

        internal void Wire()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                string id = cards[i].id;
                cards[i].button.onClick.AddListener(() => Select(id));
            }
            for (int i = 0; i < modeButtons.Length; i++)
            {
                var mode = (DifficultyMode)i;
                modeButtons[i].onClick.AddListener(() => SelectMode(mode));
            }
            startButton.onClick.AddListener(Begin);
            backButton.onClick.AddListener(Close);
            Refresh();
        }

        /// <summary>A card only selects, and only when unlocked. Persists the pick.</summary>
        internal void Select(string id)
        {
            if (MetaProgression.TrySelectLoadout(id))
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.Save();
            }
            Refresh();
        }

        /// <summary>R13: persists the difficulty pick. START locks it into the run (<see cref="RunState.Difficulty"/>).</summary>
        internal void SelectMode(DifficultyMode mode)
        {
            if (MetaProgression.SelectedDifficulty != mode)
            {
                Haptics.Pulse(XRNode.RightHand, 0.4f, 0.06f);
                MetaProgression.SelectDifficulty(mode);
                MetaProgression.Save();
            }
            Refresh();
        }

        /// <summary>Selector button label: the chosen mode is bracketed.</summary>
        internal static string ModeLabel(DifficultyMode mode, bool selected) =>
            selected ? "[ " + Difficulty.Label(mode) + " ]" : Difficulty.Label(mode);

        private void Close()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject); // edit-mode tests
        }

        /// <summary>START: closes the panel and begins the run with the saved pick (<see cref="RunState.LoadoutId"/>).</summary>
        internal void Begin()
        {
            var run = startRun;
            Close();
            run?.Invoke();
        }

        /// <summary>Card state line: SELECTED, OWNED, or what unlocks it (price and wins, or the Whip challenge).</summary>
        internal static string StateText(string id, bool unlocked, bool selected, int best) =>
            !unlocked ? MetaProgression.RequirementText(id, best) : selected ? "SELECTED" : "OWNED";

        internal void Refresh()
        {
            var chosen = MetaProgression.SelectedDifficulty;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool on = i == (int)chosen;
                var label = modeButtons[i].GetComponentInChildren<TMP_Text>();
                label.text = ModeLabel((DifficultyMode)i, on);
                label.color = on ? new Color(0.5f, 1f, 0.7f) : Color.white;
            }
            if (modeText != null) modeText.text = Difficulty.Blurb(chosen);
            string pick = MetaProgression.SelectedLoadout;
            foreach (var c in cards)
            {
                bool unlocked = MetaProgression.IsWeaponUnlocked(c.id);
                c.stateText.text = StateText(c.id, unlocked, c.id == pick, MetaProgression.BestHits);
                c.button.interactable = unlocked;
                if (c.group != null) c.group.alpha = unlocked ? 1f : 0.4f;
                c.stateText.color = c.id == pick && unlocked ? new Color(0.5f, 1f, 0.7f) : Color.white;
            }
        }

        private void OnDestroy() => onClosed?.Invoke();

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null) follow.Tick(transform, cam.transform, Time.unscaledDeltaTime);
        }
    }
}
