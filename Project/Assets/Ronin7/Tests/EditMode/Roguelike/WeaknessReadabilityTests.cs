using System.Reflection;
using NUnit.Framework;
using Ronin7.Audio.Vfx;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Enemies;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode.Roguelike
{
    /// <summary>BUG-39 weakness readability: open-state rule, seen-flag persistence, same-frame hit-confirm
    /// suppression and the once-per-kind teach (slow-mo restored on pause).</summary>
    public class WeaknessReadabilityTests
    {
        private const int S = 1, A = 2, R = 4, B = 8; // staggered, attacking, recovering, recently blinked

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear();
            TimeScaleArbiter.ResetForTests();
            MetaProgression.ResetForTests();
        }

        // ---- IsOpen: full truth table (every kind x all 16 flag combinations). Every rule is an OR of
        // its opening flags, so "open" == any flag in the kind's mask is set. ----
        [TestCase(WeaknessKind.None, 0)]
        [TestCase(WeaknessKind.Backstab, S)]
        [TestCase(WeaknessKind.HeavyBreak, 0)]
        [TestCase(WeaknessKind.Recovery, S | R)]
        [TestCase(WeaknessKind.Alternating, 0)]
        [TestCase(WeaknessKind.Reflect, 0)]
        [TestCase(WeaknessKind.Linchpin, 0)]
        [TestCase(WeaknessKind.ParryGate, S)]
        [TestCase(WeaknessKind.CounterHit, S | A)]
        [TestCase(WeaknessKind.PostureOnly, 0)]
        [TestCase(WeaknessKind.Blink, B)]
        public void IsOpen_TruthTable(WeaknessKind kind, int openMask)
        {
            for (int f = 0; f < 16; f++)
                Assert.AreEqual((f & openMask) != 0,
                    WeaknessRules.IsOpen(kind, (f & S) != 0, (f & A) != 0, (f & R) != 0, (f & B) != 0),
                    $"{kind} flags={f}");
        }

        [Test]
        public void IsOpen_CoversEveryKind()
        {
            Assert.AreEqual(11, System.Enum.GetValues(typeof(WeaknessKind)).Length, "new WeaknessKind: add it to IsOpen_TruthTable");
        }

        [Test]
        public void RestsGuarded_OnlyParryGateCounterHitPostureOnly()
        {
            foreach (WeaknessKind k in System.Enum.GetValues(typeof(WeaknessKind)))
                Assert.AreEqual(k == WeaknessKind.ParryGate || k == WeaknessKind.CounterHit || k == WeaknessKind.PostureOnly,
                    WeaknessRules.RestsGuarded(k), k.ToString());
        }

        // ---- seenWeaknesses persistence ----
        [Test]
        public void SeenWeaknesses_RoundTripThroughJson()
        {
            MetaProgression.MarkSeen((int)WeaknessKind.Blink);
            MetaProgression.MarkSeen((int)WeaknessKind.Backstab);
            MetaProgression.MarkSeen((int)WeaknessKind.Backstab);
            var data = new MetaSaveData();
            MetaProgression.WriteTo(data);
            string json = JsonUtility.ToJson(data);

            MetaProgression.ResetForTests();
            Assert.IsFalse(MetaProgression.HasSeen((int)WeaknessKind.Blink));
            var loaded = JsonUtility.FromJson<MetaSaveData>(json);
            CollectionAssert.AreEqual(new[] { (int)WeaknessKind.Backstab, (int)WeaknessKind.Blink }, loaded.seenWeaknesses);
            MetaProgression.ApplyFrom(loaded);
            Assert.IsTrue(MetaProgression.HasSeen((int)WeaknessKind.Blink));
            Assert.IsTrue(MetaProgression.HasSeen((int)WeaknessKind.Backstab));
            Assert.IsFalse(MetaProgression.HasSeen((int)WeaknessKind.Recovery));
            Assert.AreEqual(1, MetaSaveData.CurrentVersion);
        }

        [Test]
        public void SeenWeaknesses_V1FileWithoutField_LoadsEmpty()
        {
            MetaProgression.MarkSeen((int)WeaknessKind.Blink);
            var data = JsonUtility.FromJson<MetaSaveData>("{\"version\":1,\"metaEchoes\":5,\"tutorialDone\":true}");
            Assert.IsNotNull(data.seenWeaknesses);
            Assert.AreEqual(0, data.seenWeaknesses.Count);
            MetaProgression.ApplyFrom(data);
            Assert.IsFalse(MetaProgression.HasSeen((int)WeaknessKind.Blink));
            Assert.AreEqual(5, MetaProgression.Echoes);
        }

        [Test]
        public void OnboardingReset_ClearsSeenWeaknesses()
        {
            MetaProgression.MarkSeen((int)WeaknessKind.Blink);
            MetaProgression.ResetTutorialInMemory();
            Assert.IsFalse(MetaProgression.HasSeen((int)WeaknessKind.Blink));
        }

        // ---- same-frame suppression ----
        [Test]
        public void HitConfirmLatch_SameFrameSameVictimOnly()
        {
            var v = new GameObject("v");
            var other = new GameObject("other");
            try
            {
                var latch = new HitConfirmLatch();
                Assert.IsFalse(latch.Suppresses(v, 0), "fresh latch suppresses nothing");
                latch.Mark(v, 10);
                Assert.IsTrue(latch.Suppresses(v, 10));
                Assert.IsFalse(latch.Suppresses(v, 11), "next frame");
                Assert.IsFalse(latch.Suppresses(other, 10), "another victim hit the same frame");
            }
            finally { Object.DestroyImmediate(v); Object.DestroyImmediate(other); }
        }

        [Test]
        public void Vfx_ResistedAndExploitedHits_SuppressGenericImpactAndDamageSameFrame()
        {
            var host = new GameObject("vfx");
            var victim = new GameObject("victim");
            var other = new GameObject("other");
            try
            {
                var vfx = host.AddComponent<CombatVfxController>();
                vfx.Subscribe();
                var info = new DamageInfo(1f, Vector3.zero, Vector3.forward, null);

                // Publish order of a real sword hit: weakness event (inside ApplyDamage), EntityDamaged, SwordImpact.
                EventBus.Publish(new HitResisted(Vector3.zero, victim));
                EventBus.Publish(new EntityDamaged(victim, info, 1f, 1f));
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, victim));
                Assert.AreEqual(1, vfx.HandledCount, "resisted: only the blue-grey deflect");

                EventBus.Publish(new WeaknessExploited(Vector3.zero, other, (int)WeaknessKind.Backstab));
                EventBus.Publish(new EntityDamaged(other, info, 1f, 1f));
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, other));
                Assert.AreEqual(2, vfx.HandledCount, "exploited: only the gold sparks");

                var neutral = new GameObject("neutral");
                EventBus.Publish(new EntityDamaged(neutral, info, 1f, 1f));
                EventBus.Publish(new SwordImpact(Vector3.zero, 5f, neutral));
                Object.DestroyImmediate(neutral);
                Assert.AreEqual(4, vfx.HandledCount, "neutral hit keeps the generic feedback");
                vfx.Unsubscribe();
            }
            finally { Object.DestroyImmediate(host); Object.DestroyImmediate(victim); Object.DestroyImmediate(other); }
        }

        // ---- teach ----
        private static void Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        [Test]
        public void Teach_FiresOncePerKind_AndPersistsSeen()
        {
            var go = new GameObject("teach");
            try
            {
                var t = go.AddComponent<WeaknessTeach>();
                Assert.IsFalse(t.TryTeach(WeaknessKind.None));
                Assert.IsTrue(t.TryTeach(WeaknessKind.Backstab));
                Assert.IsFalse(t.TryTeach(WeaknessKind.Backstab), "second aggro of the same kind");
                Assert.IsTrue(t.TryTeach(WeaknessKind.Blink), "a different kind still teaches");
                Assert.IsTrue(MetaProgression.HasSeen((int)WeaknessKind.Backstab));

                t.OnExploited(new WeaknessExploited(Vector3.zero, null, (int)WeaknessKind.Backstab));
                Assert.AreEqual(WeaknessTeach.SlowScale, TimeScaleArbiter.EffectiveScale, 1e-5f);
                Invoke(t, "RestoreTime");
                t.OnExploited(new WeaknessExploited(Vector3.zero, null, (int)WeaknessKind.Backstab));
                Assert.AreEqual(1f, TimeScaleArbiter.EffectiveScale, 1e-5f, "slow-mo only on the first exploit");
                t.OnExploited(new WeaknessExploited(Vector3.zero, null, (int)WeaknessKind.Recovery));
                Assert.AreEqual(1f, TimeScaleArbiter.EffectiveScale, 1e-5f, "untaught kind: no slow-mo");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Teach_SlowMo_RestoredOnPauseAndModeChange()
        {
            var go = new GameObject("teach");
            try
            {
                var t = go.AddComponent<WeaknessTeach>();
                t.TryTeach(WeaknessKind.Recovery);
                t.TryTeach(WeaknessKind.Blink);

                t.OnExploited(new WeaknessExploited(Vector3.zero, null, (int)WeaknessKind.Recovery));
                Assert.AreEqual(0.5f, TimeScaleArbiter.EffectiveScale, 1e-5f);
                Invoke(t, "OnApplicationPause", false);
                Assert.AreEqual(0.5f, TimeScaleArbiter.EffectiveScale, 1e-5f, "resume does not restore");
                Invoke(t, "OnApplicationPause", true);
                Assert.AreEqual(1f, TimeScaleArbiter.EffectiveScale, 1e-5f, "pause restores time");

                t.OnExploited(new WeaknessExploited(Vector3.zero, null, (int)WeaknessKind.Blink));
                Assert.AreEqual(0.5f, TimeScaleArbiter.EffectiveScale, 1e-5f);
                Invoke(t, "OnModeChanged", new GameModeChanged(GameMode.OnFoot, GameMode.OnFoot));
                Assert.AreEqual(1f, TimeScaleArbiter.EffectiveScale, 1e-5f, "mode change restores time");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hint_FiveWordsOrFewer_FitsHudLine()
        {
            foreach (WeaknessKind k in System.Enum.GetValues(typeof(WeaknessKind)))
            {
                string h = WeaknessTeach.Hint(k);
                Assert.AreEqual(k == WeaknessKind.None, h.Length == 0, k.ToString());
                Assert.LessOrEqual(h.Split(' ').Length, 5, h);
                Assert.LessOrEqual(h.Length, 18, h);
            }
        }
    }
}
