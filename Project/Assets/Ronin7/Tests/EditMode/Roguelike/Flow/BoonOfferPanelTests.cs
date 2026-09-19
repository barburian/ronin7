using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Coverage for <see cref="BoonOfferPanel"/>'s pure/side-effect-only pieces: the title/description
    /// formatters, <see cref="BoonOfferPanel.SetChoices"/>/<see cref="BoonOfferPanel.SetRerollInteractable"/>
    /// applying to plain UI primitives, and <see cref="BoonOfferPanel.Reposition"/> touching position
    /// only (A7.2). Built inactive (mirrors GameFlowGameOverLatchTests) so Awake's button-listener
    /// wiring never runs — these tests only touch the state each method sets directly.
    /// </summary>
    public class BoonOfferPanelTests
    {
        private readonly List<Object> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in spawned)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            spawned.Clear();
        }

        private BoonDefinition MakeBoon(string id, string name, string description, BoonRarity rarity)
        {
            var def = ScriptableObject.CreateInstance<BoonDefinition>();
            def.id = id;
            def.displayName = name;
            def.description = description;
            def.rarity = rarity;
            spawned.Add(def);
            return def;
        }

        private BoonOfferPanel MakePanel(int buttonCount)
        {
            var go = new GameObject("panel");
            go.SetActive(false); // keep Awake from running while we assemble the arrays
            spawned.Add(go);
            go.AddComponent<RectTransform>(); // Reposition casts transform to RectTransform, mirroring the factory's Canvas-backed root
            var panel = go.AddComponent<BoonOfferPanel>();

            var buttons = new Button[buttonCount];
            var labels = new Text[buttonCount];
            var descriptions = new Text[buttonCount];
            for (int i = 0; i < buttonCount; i++)
            {
                var btnGo = new GameObject($"choice{i}");
                spawned.Add(btnGo);
                buttons[i] = btnGo.AddComponent<Button>();

                var labelGo = new GameObject($"label{i}");
                spawned.Add(labelGo);
                labels[i] = labelGo.AddComponent<Text>();

                var descGo = new GameObject($"desc{i}");
                spawned.Add(descGo);
                descriptions[i] = descGo.AddComponent<Text>();
            }
            panel.choiceButtons = buttons;
            panel.choiceLabels = labels;
            panel.descriptionLabels = descriptions;

            var rerollGo = new GameObject("reroll");
            spawned.Add(rerollGo);
            panel.rerollButton = rerollGo.AddComponent<Button>();

            return panel;
        }

        [Test]
        public void FormatChoiceTitle_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, BoonOfferPanel.FormatChoiceTitle(null));
        }

        [Test]
        public void FormatChoiceTitle_IncludesNameAndRarity()
        {
            var def = MakeBoon("boon_test", "Keen Edge", "Sharper strikes.", BoonRarity.Rare);

            Assert.AreEqual("Keen Edge\n<Rare>", BoonOfferPanel.FormatChoiceTitle(def));
        }

        [Test]
        public void FormatChoiceDescription_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, BoonOfferPanel.FormatChoiceDescription(null));
        }

        [Test]
        public void FormatChoiceDescription_ReturnsTheDescription()
        {
            var def = MakeBoon("boon_test", "Keen Edge", "Sharper strikes.", BoonRarity.Rare);

            Assert.AreEqual("Sharper strikes.", BoonOfferPanel.FormatChoiceDescription(def));
        }

        [Test]
        public void SetChoices_FewerThanButtonCount_HidesExtraButtonsAndLabelsTheRest()
        {
            var panel = MakePanel(3);
            var boon = MakeBoon("boon_a", "A", "desc a", BoonRarity.Common);

            panel.SetChoices(new List<BoonDefinition> { boon });

            Assert.IsTrue(panel.choiceButtons[0].gameObject.activeSelf);
            Assert.AreEqual(BoonOfferPanel.FormatChoiceTitle(boon), panel.choiceLabels[0].text);
            Assert.AreEqual(BoonOfferPanel.FormatChoiceDescription(boon), panel.descriptionLabels[0].text);
            Assert.IsFalse(panel.choiceButtons[1].gameObject.activeSelf);
            Assert.IsFalse(panel.choiceButtons[2].gameObject.activeSelf);
        }

        [Test]
        public void SetChoices_FullSet_ShowsAllButtons()
        {
            var panel = MakePanel(3);
            var choices = new List<BoonDefinition>
            {
                MakeBoon("boon_a", "A", "desc a", BoonRarity.Common),
                MakeBoon("boon_b", "B", "desc b", BoonRarity.Rare),
                MakeBoon("boon_c", "C", "desc c", BoonRarity.Epic),
            };

            panel.SetChoices(choices);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(panel.choiceButtons[i].gameObject.activeSelf);
                Assert.AreEqual(BoonOfferPanel.FormatChoiceTitle(choices[i]), panel.choiceLabels[i].text);
                Assert.AreEqual(BoonOfferPanel.FormatChoiceDescription(choices[i]), panel.descriptionLabels[i].text);
            }
        }

        [Test]
        public void SetRerollInteractable_TogglesTheButton()
        {
            var panel = MakePanel(3);

            panel.SetRerollInteractable(false);
            Assert.IsFalse(panel.rerollButton.interactable);

            panel.SetRerollInteractable(true);
            Assert.IsTrue(panel.rerollButton.interactable);
        }

        [Test]
        public void Reposition_MovesPositionOnly_NeverTouchesRotation()
        {
            var panel = MakePanel(1);
            var rt = (RectTransform)panel.transform;
            rt.rotation = Quaternion.Euler(0f, 45f, 0f);
            var expectedRotation = rt.rotation;
            var target = new Vector3(1f, 2f, 3f);

            panel.Reposition(target);

            Assert.AreEqual(target, rt.position);
            Assert.AreEqual(expectedRotation, rt.rotation);
        }
    }
}
