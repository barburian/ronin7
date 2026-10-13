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
    /// applying to plain UI primitives, and <see cref="BoonOfferPanel.Follow"/>'s lazy-follow (P1-13). Built inactive (mirrors GameFlowGameOverLatchTests) so Awake's button-listener
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
            go.AddComponent<RectTransform>(); // mirrors the factory's Canvas-backed root
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
        public void FormatChoiceTitle_PrefixesTierWordBeforeName()
        {
            var def = MakeBoon("boon_test", "Keen Edge", "Sharper strikes.", BoonRarity.Rare);

            Assert.AreEqual("RARE · Keen Edge", BoonOfferPanel.FormatChoiceTitle(def));
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

        // P1-13 re-purposes Reposition_MovesPositionOnly_NeverTouchesRotation. Its intent was "following
        // must not rotate the panel every frame" (VR comfort). That still holds inside the dead-zone;
        // beyond it (BUG-16: panel left behind a >90 deg turn) the panel must now re-face the player.
        [Test]
        public void Follow_LookAroundWithinDeadzone_LeavesPoseUntouched()
        {
            var panel = MakePanel(1);
            var head = MakeHead(yaw: 20f);
            var rt = panel.transform;
            rt.SetPositionAndRotation(new Vector3(0f, 1.6f, 1.3f), Quaternion.Euler(0f, 5f, 0f));
            var pos = rt.position; var rot = rt.rotation;

            panel.Follow(head, 0.1f);
            panel.Follow(head, 0.1f);

            Assert.AreEqual(pos, rt.position);
            Assert.AreEqual(rot, rt.rotation);
        }

        [Test]
        public void Follow_TurnedAway_RecentersInFrontOfHeadAtHeadHeightMinus10cm()
        {
            var panel = MakePanel(1);
            var head = MakeHead(yaw: 180f); // looking at -Z; panel left at +Z
            var rt = panel.transform;
            rt.position = new Vector3(0f, 1.6f, 1.3f);

            panel.Follow(head, 0.1f);
            panel.Follow(head, 0.3f); // > 0.25 s -> settled

            Assert.AreEqual(-1.3f, rt.position.z, 0.01f);
            Assert.AreEqual(1.7f - 0.1f, rt.position.y, 0.001f);
            Assert.AreEqual(1f, Vector3.Dot(rt.forward.normalized, (rt.position - head.position).normalized), 0.001f);
        }

        [Test]
        public void ShouldRecenter_20_40_180()
        {
            var f = Vector3.forward;
            Assert.IsFalse(LazyFollow.ShouldRecenter(f, Quaternion.Euler(0f, 20f, 0f) * f));
            Assert.IsTrue(LazyFollow.ShouldRecenter(f, Quaternion.Euler(0f, 40f, 0f) * f));
            Assert.IsTrue(LazyFollow.ShouldRecenter(f, -f));
        }

        [Test]
        public void FollowPose_LookingDown_KeepsHeadHeight()
        {
            var headPos = new Vector3(0f, 1.7f, 0f);
            // Straight down: forward = -Y, head up = +Z (top of the head points ahead of the body).
            var flat = LazyFollow.FlatForward(Vector3.down, Vector3.forward, Vector3.right);
            LazyFollow.FollowPose(headPos, flat, 0f, 1.3f, out var pos, out var rot);

            Assert.AreEqual(1.6f, pos.y, 1e-4f);
            Assert.AreEqual(1.3f, new Vector2(pos.x, pos.z).magnitude, 1e-3f);
            Assert.AreEqual(1f, Vector3.Dot(flat, Vector3.forward), 1e-4f);
            Assert.IsFalse(float.IsNaN(rot.x));
        }

        [Test]
        public void FlatForward_Degenerate_FallsBackToLastGood()
        {
            Assert.AreEqual(Vector3.right, LazyFollow.FlatForward(Vector3.down, Vector3.down, Vector3.right));
        }

        private Transform MakeHead(float yaw)
        {
            var go = new GameObject("head");
            spawned.Add(go);
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, 0f), Quaternion.Euler(0f, yaw, 0f));
            return go.transform;
        }

        [Test]
        public void FormatOwnedAndTier()
        {
            Assert.AreEqual("OWNED 1/3", BoonOfferPanel.FormatOwned(1, 3));
            Assert.AreEqual("EPIC", BoonOfferPanel.TierWord(BoonRarity.Epic));
        }
    }
}
