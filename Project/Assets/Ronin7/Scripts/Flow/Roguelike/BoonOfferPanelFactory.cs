using System.Collections.Generic;
using Ronin7.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Ronin7.Flow
{
    /// <summary>
    /// Builds the worldspace "choose a boon" panel from primitives — mirrors
    /// <see cref="GameOverPanelFactory"/>'s grey-box runtime-built UI approach exactly, extended to
    /// three side-by-side choice columns plus a reroll button. No prefab needed, matches the rest of
    /// this project's runtime-built UI.
    /// </summary>
    public static class BoonOfferPanelFactory
    {
        private const int ChoiceCount = 3;

        /// <summary>
        /// Construct the panel anchored ~1.3 m in front of <paramref name="targetCamera"/> (comfortable
        /// VR arm reach for a menu the player reads and points at), at eye height, facing the camera.
        /// Returns null if no camera, no choices, or no <see cref="EventSystem"/> were available — a
        /// world-space canvas is unclickable without one (A7.2: this is a headset-level soft-lock, not
        /// a cosmetic gap), so callers must not stall the run waiting on a panel that will never appear
        /// or never be dismissable.
        /// </summary>
        public static BoonOfferPanel Build(Camera targetCamera, IReadOnlyList<BoonDefinition> choices, bool rerollAvailable)
        {
            if (targetCamera == null || choices == null || choices.Count == 0) return null;
            if (EventSystem.current == null) return null;

            var canvasGo = new GameObject("Boon Offer Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 520f);
            rt.localScale = Vector3.one * 0.001f;
            var camT = targetCamera.transform;
            rt.position = camT.position + camT.forward * 1.3f;
            rt.rotation = Quaternion.LookRotation(rt.position - camT.position, Vector3.up);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            BuildTitle(rt, font);

            var choiceButtons = new Button[ChoiceCount];
            var choiceLabels = new Text[ChoiceCount];
            var descriptionLabels = new Text[ChoiceCount];
            for (int i = 0; i < ChoiceCount; i++)
                choiceButtons[i] = BuildChoiceButton(rt, font, (i - 1) * 320f, out choiceLabels[i], out descriptionLabels[i]);

            var rerollButton = BuildRerollButton(rt, font);

            var panel = canvasGo.AddComponent<BoonOfferPanel>();
            panel.choiceButtons = choiceButtons;
            panel.choiceLabels = choiceLabels;
            panel.descriptionLabels = descriptionLabels;
            panel.rerollButton = rerollButton;
            // Must follow the field assignments above: AddComponent already ran Awake, so the button
            // listeners can only be attached now (see BoonOfferPanel.WireButtons).
            panel.WireButtons();
            panel.SetChoices(choices);
            panel.SetRerollInteractable(rerollAvailable);
            return panel;
        }

        private static void BuildTitle(RectTransform parent, Font font)
        {
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(parent, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.sizeDelta = new Vector2(960f, 80f);
            titleRt.anchoredPosition = new Vector2(0f, 210f);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "CHOOSE A BOON";
            titleText.font = font;
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
        }

        private static Button BuildChoiceButton(RectTransform parent, Font font, float x, out Text titleLabel, out Text descriptionLabel)
        {
            var btnGo = new GameObject("Choice");
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(280f, 320f);
            btnRt.anchoredPosition = new Vector2(x, 20f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;

            // Title (name + rarity): pinned to the top, one/two short lines — safe to let this overflow
            // since it's never more than the boon's name plus a "<Rarity>" tag.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(btnRt, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(-32f, 90f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleLabel = titleGo.AddComponent<Text>();
            titleLabel.font = font;
            titleLabel.fontSize = 24;
            titleLabel.fontStyle = FontStyle.Bold;
            titleLabel.alignment = TextAnchor.UpperCenter;
            titleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleLabel.verticalOverflow = VerticalWrapMode.Overflow;
            titleLabel.color = Color.white;

            // Description: its own smaller sub-label below the title, clipped to its own rect
            // (A7.9 — previously one Text with VerticalWrapMode.Overflow let a long [TextArea]
            // description spill onto the adjacent choice's button, off that button's raycast target).
            var descGo = new GameObject("Description");
            descGo.transform.SetParent(btnRt, false);
            var descRt = descGo.AddComponent<RectTransform>();
            descRt.anchorMin = Vector2.zero;
            descRt.anchorMax = Vector2.one;
            descRt.offsetMin = new Vector2(16f, 16f);
            descRt.offsetMax = new Vector2(-16f, -110f);
            descriptionLabel = descGo.AddComponent<Text>();
            descriptionLabel.font = font;
            descriptionLabel.fontSize = 16;
            descriptionLabel.alignment = TextAnchor.UpperCenter;
            descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionLabel.verticalOverflow = VerticalWrapMode.Truncate;
            descriptionLabel.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            return button;
        }

        private static Button BuildRerollButton(RectTransform parent, Font font)
        {
            var btnGo = new GameObject("RerollButton");
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(320f, 70f);
            btnRt.anchoredPosition = new Vector2(0f, -220f);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.28f, 0.9f);
            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnRt, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = "REROLL";
            labelText.font = font;
            labelText.fontSize = 26;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;

            return button;
        }
    }
}
