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
            BoonOfferPanel.Place(rt, targetCamera.transform);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            BuildTitle(rt, font);

            var choiceButtons = new Button[ChoiceCount];
            var choiceLabels = new Text[ChoiceCount];
            var descriptionLabels = new Text[ChoiceCount];
            var ownedLabels = new Text[ChoiceCount];
            for (int i = 0; i < ChoiceCount; i++)
                choiceButtons[i] = BuildChoiceButton(rt, font, (i - 1) * 320f, out choiceLabels[i], out descriptionLabels[i], out ownedLabels[i]);

            var rerollButton = BuildRerollButton(rt, font);

            var panel = canvasGo.AddComponent<BoonOfferPanel>();
            panel.choiceButtons = choiceButtons;
            panel.choiceLabels = choiceLabels;
            panel.descriptionLabels = descriptionLabels;
            panel.rerollButton = rerollButton;
            panel.ownedLabels = ownedLabels;
            panel.countdownLabel = BuildCountdownLabel(rt, font);
            panel.titleLabel = rt.Find("Title").GetComponent<Text>();
            // Must follow the field assignments above: AddComponent already ran Awake, so the button
            // listeners can only be attached now (see BoonOfferPanel.WireButtons).
            panel.WireButtons();
            panel.SetChoices(choices);
            panel.SetRerollInteractable(rerollAvailable);
            return panel;
        }

        /// <summary>
        /// P2-04: the same panel shell for the CHOOSE YOUR PATH / SHOP / EVENT screens — a title, a body
        /// line, up to three option columns (title, description, sub-label such as a price) and
        /// <paramref name="footerLabels"/> bottom buttons. Content is set through
        /// <see cref="BoonOfferPanel.SetOption"/>/<see cref="BoonOfferPanel.SetFooter"/>; it lazy-follows
        /// the head like the boon offer (P1-13). Null under the same conditions as <see cref="Build"/>.
        /// </summary>
        public static BoonOfferPanel BuildCustom(Camera targetCamera, string title, string subtitle, int optionCount, params string[] footerLabels)
        {
            if (targetCamera == null || EventSystem.current == null) return null;

            var canvasGo = new GameObject("Choice Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1000f, 600f);
            rt.localScale = Vector3.one * 0.001f;
            BoonOfferPanel.Place(rt, targetCamera.transform);

            var bg = canvasGo.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.03f, 1f); // opaque: the glowing exit gates sit right behind it

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            BuildTitle(rt, font);
            var titleRt = (RectTransform)rt.Find("Title");
            titleRt.anchoredPosition = new Vector2(0f, 250f);

            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(rt, false);
            var subRt = subGo.AddComponent<RectTransform>();
            subRt.sizeDelta = new Vector2(940f, 70f);
            subRt.anchoredPosition = new Vector2(0f, 190f);
            var sub = subGo.AddComponent<Text>();
            sub.font = font;
            sub.fontSize = 24;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.verticalOverflow = VerticalWrapMode.Truncate;
            sub.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            var choiceButtons = new Button[ChoiceCount];
            var choiceLabels = new Text[ChoiceCount];
            var descriptionLabels = new Text[ChoiceCount];
            var ownedLabels = new Text[ChoiceCount];
            for (int i = 0; i < ChoiceCount; i++)
                choiceButtons[i] = BuildChoiceButton(rt, font, (i - 1) * 320f, out choiceLabels[i], out descriptionLabels[i], out ownedLabels[i]);

            var footer = new Button[footerLabels.Length];
            for (int i = 0; i < footer.Length; i++)
            {
                footer[i] = BuildRerollButton(rt, font);
                footer[i].gameObject.name = "Footer_" + i;
                ((RectTransform)footer[i].transform).anchoredPosition = new Vector2(BoonOfferPanel.ColumnX(i, footer.Length) * 1.15f, -240f);
            }

            var panel = canvasGo.AddComponent<BoonOfferPanel>();
            panel.choiceButtons = choiceButtons;
            panel.choiceLabels = choiceLabels;
            panel.descriptionLabels = descriptionLabels;
            panel.ownedLabels = ownedLabels;
            panel.footerButtons = footer;
            panel.titleLabel = titleRt.GetComponent<Text>();
            panel.subtitleLabel = sub;
            panel.WireButtons(); // after the field assignments (see Build)
            panel.SetTitle(title, subtitle);
            panel.ShowOptions(optionCount);
            for (int i = 0; i < footer.Length; i++) panel.SetFooter(i, footerLabels[i], true);
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

        private static Button BuildChoiceButton(RectTransform parent, Font font, float x, out Text titleLabel, out Text descriptionLabel, out Text ownedLabel)
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
            descRt.offsetMin = new Vector2(16f, 52f);
            descRt.offsetMax = new Vector2(-16f, -110f);
            descriptionLabel = descGo.AddComponent<Text>();
            descriptionLabel.font = font;
            descriptionLabel.fontSize = 22; // P1-13: >= 22 px readable at arm reach (1 canvas px = 1 mm)
            descriptionLabel.alignment = TextAnchor.UpperCenter;
            descriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionLabel.verticalOverflow = VerticalWrapMode.Truncate;
            descriptionLabel.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // OWNED n/max: pinned to the bottom of the button, its own rect so it never fights the description.
            var ownedGo = new GameObject("Owned");
            ownedGo.transform.SetParent(btnRt, false);
            var ownedRt = ownedGo.AddComponent<RectTransform>();
            ownedRt.anchorMin = Vector2.zero;
            ownedRt.anchorMax = new Vector2(1f, 0f);
            ownedRt.pivot = new Vector2(0.5f, 0f);
            ownedRt.sizeDelta = new Vector2(-32f, 34f);
            ownedRt.anchoredPosition = new Vector2(0f, 12f);
            ownedLabel = ownedGo.AddComponent<Text>();
            ownedLabel.font = font;
            ownedLabel.fontSize = 22;
            ownedLabel.fontStyle = FontStyle.Bold;
            ownedLabel.alignment = TextAnchor.MiddleCenter;
            ownedLabel.color = new Color(1f, 0.85f, 0.4f, 1f);

            return button;
        }

        private static Text BuildCountdownLabel(RectTransform parent, Font font)
        {
            var go = new GameObject("Countdown");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300f, 50f);
            rt.anchoredPosition = new Vector2(350f, -220f); // right of the reroll button, inside the 1000 px panel
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 26;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.6f, 0.3f, 1f);
            return t;
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
