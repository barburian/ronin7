using System;
using System.IO;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-01 (A15.13): the WEAPON SELECT panel, the buy-only Armory picker, and the deleted boot rack.</summary>
    public class WeaponSelectTests
    {
        private string dir;
        private GameObject camera;

        [SetUp]
        public void SetUp()
        {
            MetaProgression.ResetForTests();
            RunState.Reset();
            dir = Path.Combine(Path.GetTempPath(), "ss_wselect_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            MetaProgression.Load(); // Save() refuses before Load (BUG-25)
            camera = new GameObject("Cam", typeof(Camera));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(camera);
            foreach (var p in UnityEngine.Object.FindObjectsByType<WeaponSelectPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            foreach (var p in UnityEngine.Object.FindObjectsByType<MetaShopPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(p.gameObject);
            MetaProgression.ResetForTests();
            RunState.Reset();
            SaveSystem.DirectoryOverride = null;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        private WeaponSelectPanel Build()
        {
            var panel = WeaponSelectPanelFactory.Build(camera.GetComponent<Camera>(), requireEventSystem: false);
            Assert.IsNotNull(panel);
            return panel;
        }

        private static int CardIndex(WeaponSelectPanel p, string id) => Array.FindIndex(p.cards, c => c.id == id);

        // --- roster (A15.1) ---

        [Test]
        public void Cards_AreTheFourRosterWeapons_InOrder_NoNodachiOrTanto()
        {
            CollectionAssert.AreEqual(
                new[] { WeaponId.Katana, WeaponId.TwinWakizashi, WeaponId.PolearmShield, WeaponId.Whip },
                Array.ConvertAll(WeaponSelectPanel.Cards, c => c.id));
            foreach (var c in WeaponSelectPanel.Cards)
            {
                Assert.IsTrue(WeaponId.OnRoster(c.id), c.id);
                Assert.IsNotEmpty(c.blurb, c.id);
            }
            var p = Build();
            foreach (var c in p.cards) Assert.AreNotEqual(WeaponId.Nodachi, c.id);
            foreach (var c in p.cards) Assert.AreNotEqual(WeaponId.Tanto, c.id);
        }

        // --- card state table ---

        [Test]
        public void StateText_Table()
        {
            Assert.AreEqual("SELECTED", WeaponSelectPanel.StateText(WeaponId.Katana, true, true, -1));
            Assert.AreEqual("OWNED", WeaponSelectPanel.StateText(WeaponId.Katana, true, false, -1));
            Assert.AreEqual("OWNED", WeaponSelectPanel.StateText(WeaponId.TwinWakizashi, true, false, -1));
            Assert.AreEqual("SELECTED", WeaponSelectPanel.StateText(WeaponId.Whip, true, true, 2));
            Assert.AreEqual("300 ECHOES + 1 WIN", WeaponSelectPanel.StateText(WeaponId.TwinWakizashi, false, false, -1));
            Assert.AreEqual("450 ECHOES + 2 WINS", WeaponSelectPanel.StateText(WeaponId.PolearmShield, false, false, 5));
            Assert.AreEqual("WIN WITH 3 HITS MAX / BEST: --", WeaponSelectPanel.StateText(WeaponId.Whip, false, false, -1));
            Assert.AreEqual("WIN WITH 3 HITS MAX / BEST: 7", WeaponSelectPanel.StateText(WeaponId.Whip, false, false, 7));
            // A locked weapon is never "selected", even if the flag says so.
            Assert.AreEqual("450 ECHOES + 2 WINS", WeaponSelectPanel.StateText(WeaponId.PolearmShield, false, true, -1));
        }

        [Test]
        public void Panel_ShowsStatesFromSave_PolearmOneWinShort_WhipWithBest()
        {
            MetaProgression.NoteRunEnded(15, won: true, hitsTaken: 7); // 1 win, best 7, Whip stays locked
            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            var p = Build();
            Assert.AreEqual("OWNED", p.cards[CardIndex(p, WeaponId.TwinWakizashi)].stateText.text);
            Assert.AreEqual("SELECTED", p.cards[CardIndex(p, WeaponId.Katana)].stateText.text);
            Assert.AreEqual("450 ECHOES + 2 WINS", p.cards[CardIndex(p, WeaponId.PolearmShield)].stateText.text);
            Assert.AreEqual("WIN WITH 3 HITS MAX / BEST: 7", p.cards[CardIndex(p, WeaponId.Whip)].stateText.text);
            Assert.IsFalse(p.cards[CardIndex(p, WeaponId.PolearmShield)].button.interactable, "locked cards are dimmed and inert");
            Assert.IsFalse(p.cards[CardIndex(p, WeaponId.Whip)].button.interactable);
            Assert.AreEqual(0.4f, p.cards[CardIndex(p, WeaponId.Whip)].group.alpha, 1e-4f);
            Assert.IsTrue(p.cards[CardIndex(p, WeaponId.TwinWakizashi)].button.interactable);
            Assert.AreEqual(1f, p.cards[CardIndex(p, WeaponId.TwinWakizashi)].group.alpha, 1e-4f);
        }

        [Test]
        public void LastPick_IsPreselected()
        {
            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            Assert.IsTrue(MetaProgression.TrySelectLoadout(WeaponId.TwinWakizashi));
            var p = Build();
            Assert.AreEqual("SELECTED", p.cards[CardIndex(p, WeaponId.TwinWakizashi)].stateText.text);
            Assert.AreEqual("OWNED", p.cards[CardIndex(p, WeaponId.Katana)].stateText.text);
        }

        [Test]
        public void OffRosterPick_FallsBackToKatana_OnTheScreen()
        {
            MetaProgression.UnlockWeapon(WeaponId.Nodachi);
            MetaProgression.TrySelectLoadout(WeaponId.Nodachi);
            var p = Build();
            Assert.AreEqual("SELECTED", p.cards[CardIndex(p, WeaponId.Katana)].stateText.text);
            Assert.AreEqual(4, p.cards.Length, "no fifth card for the retired blade");
        }

        // --- selecting and starting ---

        [Test]
        public void Select_Persists_AndLockedCardDoesNotSelect()
        {
            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            var p = Build();
            p.cards[CardIndex(p, WeaponId.TwinWakizashi)].button.onClick.Invoke();
            Assert.AreEqual(WeaponId.TwinWakizashi, MetaProgression.SelectedLoadout);
            Assert.AreEqual("SELECTED", p.cards[CardIndex(p, WeaponId.TwinWakizashi)].stateText.text);
            Assert.AreEqual("OWNED", p.cards[CardIndex(p, WeaponId.Katana)].stateText.text);

            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.AreEqual(WeaponId.TwinWakizashi, MetaProgression.SelectedLoadout, "the pick survives a reload");

            p.Select(WeaponId.PolearmShield); // locked
            p.cards[CardIndex(p, WeaponId.Whip)].button.onClick.Invoke(); // locked
            Assert.AreEqual(WeaponId.TwinWakizashi, MetaProgression.SelectedLoadout);
        }

        [Test]
        public void Start_BeginsTheRun_WithThePick_AndOnlyStartDoes()
        {
            MetaProgression.UnlockWeapon(WeaponId.TwinWakizashi);
            var p = Build();
            int started = 0;
            p.startRun = () => { started++; RunState.Begin(7u); };

            p.cards[CardIndex(p, WeaponId.TwinWakizashi)].button.onClick.Invoke();
            Assert.AreEqual(0, started, "a card only selects");

            p.startButton.onClick.Invoke();
            Assert.AreEqual(1, started);
            Assert.AreEqual(WeaponId.TwinWakizashi, RunState.LoadoutId);
        }

        [Test]
        public void ResumeAndDaily_SkipTheScreen_AndDailyUsesTheLastPick()
        {
            Assert.IsFalse(RunEntryOptions.NeedsWeaponSelect(true), "RESUME RUN skips the screen");
            Assert.IsTrue(RunEntryOptions.NeedsWeaponSelect(false), "a new run, even on a brand-new save, goes through it");
            MetaProgression.UnlockWeapon(WeaponId.PolearmShield);
            MetaProgression.TrySelectLoadout(WeaponId.PolearmShield);
            RunState.Begin(Heat.DailySeed(DateTime.UtcNow)); // MetaShopPanel.StartDaily goes straight to RunDirector.StartRun(seed)
            Assert.AreEqual(WeaponId.PolearmShield, RunState.LoadoutId);
        }

        // --- Armory is buy-only ---

        [Test]
        public void Armory_IsBuyOnly()
        {
            MetaProgression.AddEchoes(1000);
            MetaProgression.NoteRunEnded(15, won: true, hitsTaken: 9);
            var shop = MetaShopPanelFactory.Build(camera.GetComponent<Camera>(), requireEventSystem: false);
            Assert.IsNotNull(shop);
            int twin = Array.FindIndex(shop.loadouts, l => l.id == WeaponId.TwinWakizashi);
            Assert.IsTrue(shop.loadouts[twin].button.interactable);

            shop.loadouts[twin].button.onClick.Invoke(); // buys...
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi));
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout, "...and does not select");
            Assert.AreEqual("DOUBLE KATANA\nOWNED", shop.loadouts[twin].label.text);
            Assert.IsFalse(shop.loadouts[twin].button.interactable, "owned = nothing left to do");

            shop.loadouts[twin].button.onClick.Invoke(); // a stray click on an owned weapon
            Assert.AreEqual(WeaponId.Katana, MetaProgression.SelectedLoadout);
            Assert.AreEqual(1000 - 300, MetaProgression.Echoes, "no double charge");
        }

        // --- display copies (A15.13: cannot grab, hit or parry) ---

        private static void AssertInert(GameObject go)
        {
            Assert.AreEqual(0, go.GetComponentsInChildren<Collider>(true).Length, "collider");
            Assert.AreEqual(0, go.GetComponentsInChildren<BladeDamager>(true).Length, "BladeDamager");
            Assert.AreEqual(0, go.GetComponentsInChildren<Grabbable>(true).Length, "Grabbable");
            Assert.AreEqual(0, go.GetComponentsInChildren<MonoBehaviour>(true).Length, "scripts");
            Assert.AreEqual(0, go.GetComponentsInChildren<Light>(true).Length, "light");
            Assert.AreEqual(0, go.GetComponentsInChildren<TrailRenderer>(true).Length, "trail");
            Assert.AreEqual(0, go.GetComponentsInChildren<Rigidbody>(true).Length, "rigidbody");
        }

        [Test]
        public void DisplayCopy_StripsColliderBladeDamagerGrabbableTrailLight()
        {
            var src = new GameObject("Src");
            var sword = src.AddComponent<Sword>(); // RequireComponent adds the Grabbable
            var blade = new GameObject("Blade", typeof(BoxCollider), typeof(MeshFilter), typeof(MeshRenderer));
            blade.transform.SetParent(sword.transform, false);
            blade.AddComponent<BladeDamager>();
            blade.AddComponent<TrailRenderer>();
            blade.AddComponent<Light>();
            var card = new GameObject("Card");
            try
            {
                Assert.GreaterOrEqual(src.GetComponentsInChildren<Grabbable>(true).Length, 1, "the fixture really has a Grabbable");
                var copy = WeaponSelectPanelFactory.AttachModel(src, card.transform, Vector3.zero, true);
                AssertInert(copy);
                Assert.AreEqual(1, copy.GetComponentsInChildren<MeshRenderer>(true).Length, "the visual survives");
            }
            finally { UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(card); }
        }

        [Test]
        public void LockedModel_GlowIsZeroedOnThePropertyBlock_UnlockedIsNot()
        {
            var src = new GameObject("Src", typeof(MeshFilter), typeof(MeshRenderer));
            var card = new GameObject("Card");
            try
            {
                var mpb = new MaterialPropertyBlock();
                var locked = WeaponSelectPanelFactory.AttachModel(src, card.transform, Vector3.zero, false);
                locked.GetComponent<Renderer>().GetPropertyBlock(mpb);
                Assert.AreEqual(Color.black, mpb.GetColor("_EmissionColor"), "CyberNeon/CyberSword emission");
                Assert.AreEqual(Color.black, mpb.GetColor("_FresnelColor"), "CyberNeon fresnel");
                var open = WeaponSelectPanelFactory.AttachModel(src, card.transform, Vector3.zero, true);
                open.GetComponent<Renderer>().GetPropertyBlock(mpb);
                Assert.IsTrue(mpb.isEmpty, "unlocked keeps the authored glow");
            }
            finally { UnityEngine.Object.DestroyImmediate(src); UnityEngine.Object.DestroyImmediate(card); }
        }

        [Test]
        public void ShippedCardModels_BuildInertCopies()
        {
            var models = Resources.Load<WeaponCardModels>(WeaponCardModels.ResourcePath);
            Assert.IsNotNull(models, "Resources/WeaponCardModels.asset ships the card models");
            var card = new GameObject("Card");
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var prefab = models.Get(i);
                    Assert.IsNotNull(prefab, "card " + i);
                    var copy = WeaponSelectPanelFactory.AttachModel(prefab, card.transform, Vector3.zero, true);
                    AssertInert(copy);
                    Assert.Greater(copy.GetComponentsInChildren<Renderer>(true).Length, 0, "card " + i + " has a visible model");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(card); }
        }

        // --- the boot rack is gone (A15.13) ---

        [Test]
        public void BootScene_HasNoLoadoutRackDisplay_AndNoRackBlades()
        {
            string yaml = File.ReadAllText("Assets/Ronin7/Scenes/Phase6_Boot.unity");
            StringAssert.DoesNotContain("LoadoutRackDisplay", yaml);
            StringAssert.DoesNotContain("82f2a9d11ba50ba46a3ad00dcadcba9b", yaml, "the deleted script's GUID");
            StringAssert.DoesNotContain("Rack_Sword", yaml);
            StringAssert.Contains("Loadout Rack", yaml, "the wall stays");
        }
    }
}
