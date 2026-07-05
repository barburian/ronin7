using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the stat-vs-buff decoupling in <see cref="CampaignStatsTracker"/>: BEST COMBO /
    /// BEST KILL STREAK / BEST PARRY STREAK on the hub stats board must climb from the primitive
    /// combat events every scene publishes (SwordImpact / EntityDied / PerfectParry), NOT from the
    /// per-scene opt-in buff mechanics' events — before this, all three sat frozen at 0 for the whole
    /// Ch1–Ch6 arc (and BEST KILL STREAK game-wide) because no early scene carries the buff
    /// components. Drives the private handlers via reflection (no OnEnable in EditMode), matching
    /// the SpaceEncounterManagerOnEnemyDestroyedTests idiom.
    /// </summary>
    public class CampaignStatsTrackerTests
    {
        private GameObject _trackerGo;
        private CampaignStatsTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            CampaignStats.Reset();
            _trackerGo = new GameObject("CampaignStatsTrackerTest");
            _tracker = _trackerGo.AddComponent<CampaignStatsTracker>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_trackerGo != null) Object.DestroyImmediate(_trackerGo);
            CampaignStats.Reset();
            EventBus.Clear();
        }

        private void Invoke(string method, object evt)
        {
            var m = typeof(CampaignStatsTracker).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"CampaignStatsTracker.{method} not found — update this test.");
            m.Invoke(_tracker, new[] { evt });
        }

        // ---- pure combo transitions (mirror ComboMomentumController.RegisterHit's locked-in scheme) ----

        [Test]
        public void NextCombo_FreshHit_StartsChainAtOne()
        {
            var (count, lastTarget) = CampaignStatsTracker.NextCombo(0, 0, 0f, targetId: 7, now: 10f, window: 1.2f, maxCombo: 4);
            Assert.AreEqual(1, count);
            Assert.AreEqual(7, lastTarget);
        }

        [Test]
        public void NextCombo_DistinctTargetWithinWindow_Extends_AndCaps()
        {
            var (count, _) = CampaignStatsTracker.NextCombo(1, 7, 10f, targetId: 8, now: 10.5f, window: 1.2f, maxCombo: 4);
            Assert.AreEqual(2, count);

            (count, _) = CampaignStatsTracker.NextCombo(4, 7, 10f, targetId: 8, now: 10.5f, window: 1.2f, maxCombo: 4);
            Assert.AreEqual(4, count, "Chain must cap at maxCombo.");
        }

        [Test]
        public void NextCombo_SameTargetWithinWindow_PunishesToZero()
        {
            var (count, _) = CampaignStatsTracker.NextCombo(3, 7, 10f, targetId: 7, now: 10.5f, window: 1.2f, maxCombo: 4);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void NextCombo_ExpiredWindow_StartsFreshChain()
        {
            var (count, _) = CampaignStatsTracker.NextCombo(3, 7, 10f, targetId: 8, now: 11.5f, window: 1.2f, maxCombo: 4);
            Assert.AreEqual(1, count);
        }

        // ---- pure kill-streak transitions (mirror AdrenalineFlow.RegisterKill's semantics) ----

        [Test]
        public void NextKillStreak_WithinWindow_Extends_AndCaps()
        {
            Assert.AreEqual(2, CampaignStatsTracker.NextKillStreak(1, 10f, 12f, window: 4f, maxStreak: 6));
            Assert.AreEqual(6, CampaignStatsTracker.NextKillStreak(6, 10f, 12f, window: 4f, maxStreak: 6));
        }

        [Test]
        public void NextKillStreak_ExpiredOrFirstKill_RestartsAtOne()
        {
            Assert.AreEqual(1, CampaignStatsTracker.NextKillStreak(0, 0f, 10f, window: 4f, maxStreak: 6));
            Assert.AreEqual(1, CampaignStatsTracker.NextKillStreak(3, 10f, 15f, window: 4f, maxStreak: 6));
        }

        // ---- handler-level: the stats climb without any buff mechanic present ----

        [Test]
        public void SwordImpacts_AcrossDistinctTargets_RecordBestCombo_WithoutComboController()
        {
            var a = new GameObject("VictimA");
            var b = new GameObject("VictimB");
            try
            {
                Invoke("OnSwordImpact", new SwordImpact(Vector3.zero, 5f, a));
                Invoke("OnSwordImpact", new SwordImpact(Vector3.zero, 5f, b));
                Assert.AreEqual(2, CampaignStats.BestCombo,
                    "Chaining two distinct targets must record BEST COMBO 2 with no ComboMomentumController in the scene.");
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void SwordImpacts_SameTargetTwice_DoesNotInflateBestCombo()
        {
            var a = new GameObject("VictimA");
            try
            {
                Invoke("OnSwordImpact", new SwordImpact(Vector3.zero, 5f, a));
                Invoke("OnSwordImpact", new SwordImpact(Vector3.zero, 5f, a));
                Assert.AreEqual(1, CampaignStats.BestCombo,
                    "Double-hitting one target punishes the chain — best stays at the first hit's 1.");
            }
            finally
            {
                Object.DestroyImmediate(a);
            }
        }

        [Test]
        public void EnemyDeaths_RecordDefeats_AndKillStreak_WithoutAdrenalineFlow()
        {
            var e1 = new GameObject("Enemy1");
            var e2 = new GameObject("Enemy2");
            try
            {
                Invoke("OnEntityDied", new EntityDied(e1));
                Invoke("OnEntityDied", new EntityDied(e2));
                Assert.AreEqual(2, CampaignStats.EnemiesDefeated);
                Assert.AreEqual(2, CampaignStats.BestKillStreak,
                    "Two same-window kills must record BEST KILL STREAK 2 with no AdrenalineFlow in any scene.");
            }
            finally
            {
                Object.DestroyImmediate(e1);
                Object.DestroyImmediate(e2);
            }
        }

        [Test]
        public void PerfectParries_RecordParryStreak_AndPlayerHitBreaksIt()
        {
            Invoke("OnPerfectParry", new PerfectParry(Vector3.zero, null, 0.9f));
            Invoke("OnPerfectParry", new PerfectParry(Vector3.zero, null, 0.8f));
            Invoke("OnPerfectParry", new PerfectParry(Vector3.zero, null, 0.7f));
            Assert.AreEqual(3, CampaignStats.BestParryStreak,
                "Three perfect parries must record BEST PARRY STREAK 3 with no ParryFlowController in the scene.");
            Assert.AreEqual(3, CampaignStats.PerfectParries);

            Invoke("OnPlayerHit", new PlayerHit(10f, Vector3.zero));
            Invoke("OnPerfectParry", new PerfectParry(Vector3.zero, null, 0.9f));
            Assert.AreEqual(3, CampaignStats.BestParryStreak,
                "After a hit breaks the streak, the next parry restarts at 1 — best must not climb.");
        }
    }
}
