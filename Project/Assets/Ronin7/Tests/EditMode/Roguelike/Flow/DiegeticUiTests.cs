using System.Collections.Generic;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P5-07b: TextMeshPro + Rajdhani panels, minimum sizes, glass frames, curved boon cards.</summary>
    public class DiegeticUiTests
    {
        private readonly List<Object> spawned = new();
        private EventSystem es;

        // EventSystem.OnEnable (which registers it as current) does not run in edit mode, and the public
        // setter only accepts registered systems — so register through the private static list.
        private static List<EventSystem> Registry() => (List<EventSystem>)typeof(EventSystem)
            .GetField("m_EventSystems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .GetValue(null);

        [SetUp]
        public void SetUp()
        {
            es = new GameObject("es").AddComponent<EventSystem>();
            spawned.Add(es.gameObject);
            Registry().Insert(0, es);
        }

        [TearDown]
        public void TearDown()
        {
            Registry().Remove(es);
            foreach (var o in spawned) if (o != null) Object.DestroyImmediate(o);
            spawned.Clear();
        }

        [Test]
        public void CardPose_CentreFlat_OuterCardsYawTwelveOnArc()
        {
            BoonOfferPanel.CardPose(0f, out float z0, out float yaw0);
            Assert.AreEqual(0f, z0, 1e-5f);
            Assert.AreEqual(0f, yaw0, 1e-5f);

            BoonOfferPanel.CardPose(320f, out float zr, out float yawR);
            BoonOfferPanel.CardPose(-320f, out float zl, out float yawL);
            Assert.AreEqual(12f, yawR, 1e-4f);
            Assert.AreEqual(-12f, yawL, 1e-4f);
            Assert.AreEqual(zr, zl, 1e-4f);
            Assert.AreEqual(-320f * Mathf.Tan(6f * Mathf.Deg2Rad), zr, 1e-3f); // toward the viewer (-z)

            BoonOfferPanel.CardPose(160f, out float zh, out float yawH); // 2-option row: half the yaw
            Assert.AreEqual(6f, yawH, 1e-4f);
            Assert.Less(zr, zh);
        }

        [Test]
        public void ClampSize_EnforcesBodyAndTitleMinimums()
        {
            Assert.AreEqual(28f, UiKit.ClampSize(20f, false));
            Assert.AreEqual(34f, UiKit.ClampSize(34f, false));
            Assert.AreEqual(48f, UiKit.ClampSize(40f, true));
        }

        [Test]
        public void RoundBoxDistance_InsideNegativeOutsidePositive()
        {
            Assert.Less(UiKit.RoundBoxDistance(0f, 0f, 16f, 10f), 0f);
            Assert.Greater(UiKit.RoundBoxDistance(16f, 16f, 16f, 10f), 0f); // rounded-off corner
            Assert.AreEqual(0f, UiKit.RoundBoxDistance(16f, 0f, 16f, 10f), 1e-4f);
        }

        [Test]
        public void TmpDefaultFont_IsRajdhaniWithBoldWeight()
        {
            var def = TMP_Settings.defaultFontAsset;
            Assert.IsNotNull(def);
            Assert.AreEqual("Rajdhani-SemiBold SDF", def.name);
            var bold = def.fontWeightTable[7].regularTypeface;
            Assert.IsNotNull(bold, "SemiBold's Bold (700) slot must map to Rajdhani-Bold");
            Assert.AreEqual("Rajdhani-Bold SDF", bold.name);
        }

        [Test]
        public void BoonOffer_NoLegacyText_MinSizes_IconsTinted_CardsCurved()
        {
            var icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            spawned.Add(icon);
            var choices = new List<BoonDefinition>();
            var rarities = new[] { BoonRarity.Common, BoonRarity.Rare, BoonRarity.Epic };
            for (int i = 0; i < 3; i++)
            {
                var b = ScriptableObject.CreateInstance<BoonDefinition>();
                b.id = "t" + i;
                b.displayName = "Test " + i;
                b.rarity = rarities[i];
                b.maxStacks = 1;
                b.icon = icon;
                spawned.Add(b);
                choices.Add(b);
            }

            var panel = BoonOfferPanelFactory.Build(Cam(), choices, true);
            Assert.IsNotNull(panel);
            spawned.Add(panel.gameObject);
            AssertPanel(panel.gameObject);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(panel.iconImages[i].gameObject.activeSelf);
                Assert.AreSame(icon, panel.iconImages[i].sprite);
                Assert.AreEqual(BoonOfferPanel.RarityColor(rarities[i]), panel.iconImages[i].color);
                Assert.AreEqual(BoonOfferPanel.RarityColor(rarities[i]), panel.edgeImages[i].color);
            }
            Assert.AreEqual(-12f, Mathf.DeltaAngle(0f, panel.choiceButtons[0].transform.localEulerAngles.y), 1e-3f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, panel.choiceButtons[1].transform.localEulerAngles.y), 1e-3f);
            Assert.AreEqual(12f, Mathf.DeltaAngle(0f, panel.choiceButtons[2].transform.localEulerAngles.y), 1e-3f);
            Assert.GreaterOrEqual(panel.titleLabel.fontSize, UiKit.MinTitle);
        }

        [Test]
        public void ChoiceAndPausePanels_NoLegacyText_MinSizes()
        {
            var choice = BoonOfferPanelFactory.BuildCustom(Cam(), "SHOP", "Shards are this run's currency.", 2, "MEND", "LEAVE");
            Assert.IsNotNull(choice);
            spawned.Add(choice.gameObject);
            AssertPanel(choice.gameObject);

            var pause = RunPausePanelFactory.Build(Cam());
            Assert.IsNotNull(pause);
            spawned.Add(pause.gameObject);
            AssertPanel(pause.gameObject);

        }

        private Camera Cam()
        {
            var cam = new GameObject("cam").AddComponent<Camera>();
            spawned.Add(cam.gameObject);
            return cam;
        }

        private static void AssertPanel(GameObject root)
        {
            Assert.IsEmpty(root.GetComponentsInChildren<Text>(true), "legacy UnityEngine.UI.Text left in " + root.name);
            var tmps = root.GetComponentsInChildren<TMP_Text>(true);
            Assert.IsNotEmpty(tmps);
            foreach (var t in tmps)
                Assert.GreaterOrEqual(t.enableAutoSizing ? t.fontSizeMin : t.fontSize, UiKit.MinBody, t.name);
            Assert.AreEqual(Image.Type.Sliced, root.GetComponent<Image>().type, "glass frame");
            Assert.AreEqual(UiKit.FrameSprite, root.GetComponent<Image>().sprite?.name, "baked glass+edge frame sprite (A13.7)");
        }
    }
}
