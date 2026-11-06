using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike.Core
{
    /// <summary>P9-03 / A15.12-13: weapon win gates, the Whip hit challenge, best score, hitsTaken and the resume fallback.</summary>
    public class ShrineWinGateTests
    {
        private string dir;

        [SetUp]
        public void SetUp()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            dir = Path.Combine(Path.GetTempPath(), "ronin7_p903_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            MetaProgression.Load();
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Reset();
            MetaProgression.ResetForTests();
            SaveSystem.DirectoryOverride = null;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        private static void Wins(int n) { for (int i = 0; i < n; i++) MetaProgression.NoteRunEnded(15, true); }

        [TestCase(WeaponId.TwinWakizashi, 300, 0, false, 300)]  // no wins: refused, Echoes unspent
        [TestCase(WeaponId.TwinWakizashi, 300, 1, true, 0)]
        [TestCase(WeaponId.TwinWakizashi, 299, 1, false, 299)]  // short Echoes
        [TestCase(WeaponId.PolearmShield, 450, 1, false, 450)]  // short wins
        [TestCase(WeaponId.PolearmShield, 450, 2, true, 0)]
        [TestCase(WeaponId.PolearmShield, 449, 2, false, 449)]
        public void Purchase_Table(string id, int echoes, int wins, bool bought, int echoesLeft)
        {
            Wins(wins);
            MetaProgression.AddEchoes(echoes);
            Assert.AreEqual(bought, MetaProgression.TryPurchase(id));
            Assert.AreEqual(echoesLeft, MetaProgression.Echoes);
            Assert.AreEqual(bought, MetaProgression.IsWeaponUnlocked(id));
        }

        [Test]
        public void Purchase_AlreadyOwned_SpendsNothing()
        {
            Wins(1);
            MetaProgression.AddEchoes(600);
            Assert.IsTrue(MetaProgression.TryPurchase(WeaponId.TwinWakizashi));
            Assert.IsFalse(MetaProgression.TryPurchase(WeaponId.TwinWakizashi));
            Assert.AreEqual(300, MetaProgression.Echoes);
        }

        [Test]
        public void OwnedTwin_StaysUnlockedWithoutWins()
        {
            var data = new MetaSaveData { metaRunsWon = 0 };
            data.unlockedWeapons.Add(WeaponId.TwinWakizashi);
            MetaProgression.ApplyFrom(data);
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.TwinWakizashi), "no refunds, no re-lock");
        }

        [TestCase(0, true, true)]
        [TestCase(3, true, true)]
        [TestCase(4, true, false)]
        [TestCase(0, false, false)]
        public void Whip_UnlocksOnlyOnWinWithinHitLimit(int hits, bool won, bool unlocked)
        {
            MetaProgression.NoteRunEnded(15, won, hits);
            Assert.AreEqual(unlocked, MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
        }

        [Test]
        public void Whip_CannotBeBought()
        {
            MetaProgression.AddEchoes(99999);
            Wins(5);
            Assert.IsFalse(MetaProgression.TryPurchase(WeaponId.Whip));
            Assert.AreEqual(99999, MetaProgression.Echoes);
        }

        [Test]
        public void Whip_UnlockPersists()
        {
            MetaProgression.NoteRunEnded(15, true, 2);
            MetaProgression.Save();
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            Assert.IsTrue(MetaProgression.IsWeaponUnlocked(WeaponId.Whip));
            Assert.AreEqual(2, MetaProgression.BestHits);
        }

        [Test]
        public void BestHits_LowerReplaces_HigherAndLossesDoNot()
        {
            Assert.AreEqual(-1, MetaProgression.BestHits);
            MetaProgression.NoteRunEnded(15, true, 7);
            Assert.AreEqual(7, MetaProgression.BestHits);
            MetaProgression.NoteRunEnded(15, true, 9);
            Assert.AreEqual(7, MetaProgression.BestHits);
            MetaProgression.NoteRunEnded(15, false, 0);
            Assert.AreEqual(7, MetaProgression.BestHits);
            MetaProgression.NoteRunEnded(15, true, 5);
            Assert.AreEqual(5, MetaProgression.BestHits);
        }

        [Test]
        public void DisplayStrings()
        {
            Assert.AreEqual("BEST: 7", MetaProgression.BestHitsText(7));
            Assert.AreEqual("BEST: 0", MetaProgression.BestHitsText(0));
            Assert.AreEqual("BEST: --", MetaProgression.BestHitsText(-1));
            Assert.AreEqual("300 ECHOES + 1 WIN", MetaProgression.RequirementText(WeaponId.TwinWakizashi, -1));
            Assert.AreEqual("450 ECHOES + 2 WINS", MetaProgression.RequirementText(WeaponId.PolearmShield, -1));
            Assert.AreEqual("WIN ON NORMAL+ WITH 3 HITS MAX / BEST: 7", MetaProgression.RequirementText(WeaponId.Whip, 7));
        }

        [Test]
        public void HitsTaken_ResetsAtBegin_CountsInRunOnly_SurvivesSnapshot()
        {
            RunState.NoteHitTaken();
            Assert.AreEqual(0, RunState.HitsTaken, "not in a run");
            RunState.Begin(11);
            RunState.NoteHitTaken();
            RunState.NoteHitTaken();
            RunSnapshot s = RunState.ToSnapshot();
            Assert.AreEqual(2, s.hitsTaken);
            RunState.Reset();
            Assert.AreEqual(0, RunState.HitsTaken);
            Assert.IsTrue(RunState.Restore(s));
            Assert.AreEqual(2, RunState.HitsTaken);
            RunState.Begin(12);
            Assert.AreEqual(0, RunState.HitsTaken);
        }

        [Test]
        public void OnlyPlayerHitCounts()
        {
            var go = new GameObject("rd");
            go.SetActive(false);
            var director = go.AddComponent<RunDirector>();
            const BindingFlags Bf = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(RunDirector).GetMethod("OnEnable", Bf).Invoke(director, null);
            try
            {
                RunState.Begin(21);
                EventBus.Publish(new GuardChipped(5f));
                EventBus.Publish(new HitResisted(Vector3.zero, null));
                Assert.AreEqual(0, RunState.HitsTaken);
                EventBus.Publish(new PlayerHit(10f, Vector3.zero));
                Assert.AreEqual(1, RunState.HitsTaken);
            }
            finally
            {
                typeof(RunDirector).GetMethod("OnDisable", Bf).Invoke(director, null);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resume_UnownedWeapon_PlaysAsKatana_OwnedStays_OffRosterVerbatim()
        {
            RunState.Begin(31);
            RunSnapshot s = RunState.ToSnapshot();
            s.loadoutId = WeaponId.Whip;
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(s));
            Assert.AreEqual(WeaponId.Katana, RunState.LoadoutId, "Whip not unlocked");
            Assert.IsTrue(WeaponId.ParryAvailable(RunState.LoadoutId), "parry-dependent systems agree with the Katana");
            Assert.IsTrue(WeaponId.AllowsWeaponArt(RunState.LoadoutId));

            MetaProgression.UnlockWeapon(WeaponId.Whip);
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(s));
            Assert.AreEqual(WeaponId.Whip, RunState.LoadoutId);

            s.loadoutId = WeaponId.Tanto;
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(s));
            Assert.AreEqual(WeaponId.Tanto, RunState.LoadoutId, "off-roster ids stay verbatim for the fixture; Effective falls back");
        }
    }
}
