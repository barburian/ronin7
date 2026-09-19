using System;
using System.Collections.Generic;
using Ronin7.Combat;
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
        // Public so BoonOfferPanelFactory can assign them after AddComponent (mirrors GameOverPanel's
        // returnButton — Awake lands after that assignment for the same reason).
        public Button[] choiceButtons;
        public Text[] choiceLabels;
        /// <summary>A7.9: a separate, smaller sub-label for each choice's description. Previously the
        /// name/rarity/description were one Text with VerticalWrapMode.Overflow, so a long [TextArea]
        /// description spilled onto the neighbouring choice's button — off that button's own raycast
        /// target, so it read as a label for the wrong boon. Optional: null entries are skipped.</summary>
        public Text[] descriptionLabels;
        public Button rerollButton;

        /// <summary>Fired with the chosen choice's index (0..choiceButtons.Length-1).</summary>
        public event Action<int> ChoiceSelected;
        public event Action RerollRequested;

        private void Awake()
        {
            if (choiceButtons != null)
            {
                for (int i = 0; i < choiceButtons.Length; i++)
                {
                    if (choiceButtons[i] == null) continue;
                    int index = i; // capture by value, not the loop variable
                    choiceButtons[i].onClick.AddListener(() => ChoiceSelected?.Invoke(index));
                }
            }
            if (rerollButton != null)
                rerollButton.onClick.AddListener(() => RerollRequested?.Invoke());
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
                    choiceLabels[i].text = FormatChoiceTitle(choices[i]);
                if (descriptionLabels != null && i < descriptionLabels.Length && descriptionLabels[i] != null)
                    descriptionLabels[i].text = FormatChoiceDescription(choices[i]);
            }
        }

        public void SetRerollInteractable(bool interactable)
        {
            if (rerollButton != null) rerollButton.interactable = interactable;
        }

        /// <summary>Re-anchors the panel to <paramref name="worldPosition"/> only — never touches
        /// rotation. A7.2: called every frame while the panel is up so it follows the player without
        /// ever rotating the world around them (VR comfort — CLAUDE.md forbids camera shake, and
        /// continuously re-facing the panel is the same class of discomfort) and without any shake.
        /// </summary>
        public void Reposition(Vector3 worldPosition)
        {
            ((RectTransform)transform).position = worldPosition;
        }

        /// <summary>Pure title formatter — name + rarity, the choiceLabels line. Internal so it is
        /// unit-testable without building the panel.</summary>
        internal static string FormatChoiceTitle(BoonDefinition def) =>
            def == null ? string.Empty : $"{def.displayName}\n<{def.rarity}>";

        /// <summary>Pure description formatter, the descriptionLabels line. Internal so it is
        /// unit-testable without building the panel; the overflow fix itself (A7.9) is the description
        /// label's own smaller font + VerticalWrapMode.Truncate, not this method's job.</summary>
        internal static string FormatChoiceDescription(BoonDefinition def) =>
            def == null ? string.Empty : def.description;
    }
}
