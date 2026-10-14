using System;
using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Worldspace "choose a boon" panel runtime hook — the post-clear reward screen. Mirrors
    /// <see cref="GameOverPanel"/>'s idiom exactly: the visual hierarchy is built by
    /// <see cref="BoonOfferPanelFactory"/>, and this component only surfaces button clicks as events
    /// <see cref="RunDirector"/> reacts to. Unlike GameOverPanel it also supports a reroll, which
    /// refreshes the three displayed choices in place instead of rebuilding the panel.
    /// </summary>
    public class BoonOfferPanel : MonoBehaviour
    {
        // Public so BoonOfferPanelFactory can assign them after AddComponent. Assignment necessarily
        // lands AFTER Awake has already run (see WireButtons), which is why the listener wiring is an
        // explicit call the factory makes and not an Awake.
        public Button[] choiceButtons;
        public Text[] choiceLabels;
        /// <summary>A7.9: a separate, smaller sub-label for each choice's description. Previously the
        /// name/rarity/description were one Text with VerticalWrapMode.Overflow, so a long [TextArea]
        /// description spilled onto the neighbouring choice's button — off that button's own raycast
        /// target, so it read as a label for the wrong boon. Optional: null entries are skipped.</summary>
        public Text[] descriptionLabels;
        public Button rerollButton;
        /// <summary>P1-13: per-choice "OWNED n/max" line. Optional.</summary>
        public Text[] ownedLabels;
        /// <summary>P1-13: "AUTO-PICK IN n" for the last 10 s of the fallback timer. Optional.</summary>
        public Text countdownLabel;
        /// <summary>P2-04 generic panels (path / shop / event): optional bottom buttons and a body line.</summary>
        public Button[] footerButtons;
        public Text titleLabel;
        public Text subtitleLabel;

        /// <summary>Fired with the chosen choice's index (0..choiceButtons.Length-1).</summary>
        public event Action<int> ChoiceSelected;
        public event Action RerollRequested;
        /// <summary>P2-04: fired with the footer button's index.</summary>
        public event Action<int> FooterSelected;

        /// <summary>
        /// Wire every button to its event. The builder MUST call this after assigning the button
        /// fields — this cannot live in <c>Awake</c>.
        ///
        /// A7.2 (second route into the same soft-lock): <c>AddComponent</c> on an *active*
        /// GameObject runs <c>Awake</c> synchronously, inside the <c>AddComponent</c> call itself,
        /// so an <c>Awake</c>-based wiring observed <c>choiceButtons == null</c> and silently added
        /// no listeners at all. The panel still rendered, still highlighted under the ray and still
        /// consumed the click — <c>onClick</c> just had nothing attached — so the run could never
        /// advance past the first boon offer. Measured in-editor: listener count 0, event fired 0
        /// times. Idempotent, so a second call cannot double-fire a choice.
        /// </summary>
        public void WireButtons()
        {
            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    if (choiceButtons[i] == null) continue;
                    int index = i; // capture by value, not the loop variable
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                }
            }
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveAllListeners();
                rerollButton.onClick.AddListener(() => RerollRequested?.Invoke());
            }
            if (footerButtons != null)
            {
                for (int i = 0; i < footerButtons.Length; i++)
                {
                    if (footerButtons[i] == null) continue;
                    int index = i;
                    footerButtons[i].onClick.RemoveAllListeners();
                    footerButtons[i].onClick.AddListener(() => FooterSelected?.Invoke(index));
                }
            }
        }

        // ---- P2-04 generic content (path / shop / event panels) ----

        public void SetTitle(string title, string subtitle = null)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle ?? string.Empty;
        }

        /// <summary>Shows the first <paramref name="count"/> choice buttons, centred (1, 2 or 3 columns).</summary>
        public void ShowOptions(int count)
        {
            if (choiceButtons == null) return;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                bool on = i < count;
                choiceButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                var rt = (RectTransform)choiceButtons[i].transform;
                rt.anchoredPosition = new Vector2(ColumnX(i, count), rt.anchoredPosition.y);
            }
        }

        /// <summary>Column centre for option <paramref name="i"/> of <paramref name="count"/> (320 px pitch).</summary>
        internal static float ColumnX(int i, int count) => (i - (count - 1) * 0.5f) * 320f;

        public void SetOption(int i, string title, Color titleColor, string description, string subLabel, bool interactable)
        {
            if (choiceButtons == null || i < 0 || i >= choiceButtons.Length) return;
            if (choiceLabels != null && i < choiceLabels.Length && choiceLabels[i] != null)
            {
                choiceLabels[i].text = title;
                choiceLabels[i].color = titleColor;
            }
            if (descriptionLabels != null && i < descriptionLabels.Length && descriptionLabels[i] != null)
                descriptionLabels[i].text = description;
            if (ownedLabels != null && i < ownedLabels.Length && ownedLabels[i] != null)
                ownedLabels[i].text = subLabel;
            choiceButtons[i].interactable = interactable;
        }

        public void SetFooter(int i, string label, bool visible, bool interactable = true)
        {
            if (footerButtons == null || i < 0 || i >= footerButtons.Length || footerButtons[i] == null) return;
            footerButtons[i].gameObject.SetActive(visible);
            footerButtons[i].interactable = interactable;
            var t = footerButtons[i].GetComponentInChildren<Text>(true);
            if (t != null) t.text = label;
        }

        /// <summary>
        /// Refresh the displayed choices in place (used by reroll) without rebuilding the panel.
        /// Hides any button beyond <paramref name="choices"/>'s count — the offer pool can run dry.
        /// </summary>
        public void SetChoices(IReadOnlyList<BoonDefinition> choices)
        {
            if (choiceButtons == null) return;
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool has = choices != null && i < choices.Count;
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                if (choiceLabels != null && i < choiceLabels.Length && choiceLabels[i] != null)
                {
                    choiceLabels[i].text = FormatChoiceTitle(choices[i]);
                    choiceLabels[i].color = RarityColor(choices[i].rarity);
                }
                if (ownedLabels != null && i < ownedLabels.Length && ownedLabels[i] != null)
                    ownedLabels[i].text = FormatOwned(RunState.BoonCount(choices[i].id), choices[i].maxStacks);
                if (descriptionLabels != null && i < descriptionLabels.Length && descriptionLabels[i] != null)
                    descriptionLabels[i].text = FormatChoiceDescription(choices[i]);
            }
        }

        public void SetRerollInteractable(bool interactable)
        {
            if (rerollButton != null) rerollButton.interactable = interactable;
        }

        /// <summary>REROLL (n) with n = remaining reroll tokens; disabled at 0.</summary>
        public void SetRerollTokens(int tokens)
        {
            SetRerollInteractable(tokens > 0);
            var label = rerollButton != null ? rerollButton.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.text = "REROLL (" + tokens + ")";
        }

        private int shownCountdown = -1;

        /// <summary>Shows "AUTO-PICK IN n" only in the last <see cref="CountdownSeconds"/> s; the string
        /// is rebuilt only when the whole-second value changes (no per-frame allocation).</summary>
        public void SetCountdown(float secondsLeft)
        {
            if (countdownLabel == null) return;
            int whole = secondsLeft > 0f && secondsLeft <= CountdownSeconds ? Mathf.CeilToInt(secondsLeft) : 0;
            if (whole == shownCountdown) return;
            shownCountdown = whole;
            countdownLabel.text = whole > 0 ? "AUTO-PICK IN " + whole : string.Empty;
        }

        public const float CountdownSeconds = 10f;

        private readonly LazyFollow follow = new LazyFollow(FollowDistance);
        public const float FollowDistance = 1.3f;

        /// <summary>Rest pose in front of <paramref name="head"/> (factory build).</summary>
        internal static void Place(Transform panel, Transform head) => new LazyFollow(FollowDistance).Snap(panel, head);

        /// <summary>P1-13 (BUG-16): lazy follow — see <see cref="LazyFollow"/>.</summary>
        internal void Follow(Transform head, float dt) => follow.Tick(transform, head, dt);

        private void Update()
        {
            var cam = Camera.main;
            if (cam != null) Follow(cam.transform, Time.unscaledDeltaTime);
        }

        /// <summary>Pure title formatter — name + rarity, the choiceLabels line. Internal so it is
        /// unit-testable without building the panel.</summary>
        internal static string FormatChoiceTitle(BoonDefinition def) =>
            def == null ? string.Empty : TierWord(def.rarity) + " · " + def.displayName;

        /// <summary>Tier word shown with the colour so rarity never rides on colour alone.</summary>
        internal static string TierWord(BoonRarity r) => r == BoonRarity.Epic ? "EPIC" : r == BoonRarity.Rare ? "RARE" : "COMMON";

        /// <summary>Common grey #B8C0CC, Rare blue #3FA9FF, Epic violet #B66BFF.</summary>
        internal static Color RarityColor(BoonRarity r) =>
            r == BoonRarity.Epic ? new Color32(0xB6, 0x6B, 0xFF, 255)
            : r == BoonRarity.Rare ? new Color32(0x3F, 0xA9, 0xFF, 255)
            : new Color32(0xB8, 0xC0, 0xCC, 255);

        internal static string FormatOwned(int owned, int max) => "OWNED " + owned + "/" + max;

        /// <summary>Pure description formatter, the descriptionLabels line. Internal so it is
        /// unit-testable without building the panel; the overflow fix itself (A7.9) is the description
        /// label's own smaller font + VerticalWrapMode.Truncate, not this method's job.</summary>
        internal static string FormatChoiceDescription(BoonDefinition def) =>
            def == null ? string.Empty : def.description;
    }
}
