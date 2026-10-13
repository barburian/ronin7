using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P1-07 (BUG-13): Unbroken is once per run and fires before Second Wind, each source
    /// setting its own revive fraction.</summary>
    public class UnbrokenChainTests
    {
        private static readonly DamageInfo Lethal = new DamageInfo(500f, Vector3.zero, Vector3.forward, null);

        [SetUp] public void SetUp() => RunState.Reset();
        [TearDown] public void TearDown() => RunState.Reset();

        private static Health MakeHealth(out GameObject go)
        {
            go = new GameObject("rig");
            var h = go.AddComponent<Health>();
            h.Configure(100f);
            return h;
        }

        [Test]
        public void DeathInterceptorChain_UnbrokenFiresBeforeSecondWind()
        {
            var h = MakeHealth(out var go);
            int unbrokenCalls = 0, windCalls = 0;
            h.DeathInterceptor = RunDirector.ComposeDeathInterceptor(h,
                () => { unbrokenCalls++; return unbrokenCalls == 1; },
                () => { windCalls++; return true; });

            h.ApplyDamage(Lethal);
            Assert.AreEqual(1, unbrokenCalls);
            Assert.AreEqual(0, windCalls, "Second Wind must not be spent while Unbroken is available");

            h.ApplyDamage(Lethal);
            Assert.AreEqual(1, windCalls);
            Assert.IsTrue(h.IsAlive);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ReviveFraction_PerSource()
        {
            var h = MakeHealth(out var go);
            bool unbrokenReady = true;
            h.DeathInterceptor = RunDirector.ComposeDeathInterceptor(h,
                () => { bool r = unbrokenReady; unbrokenReady = false; return r; },
                () => true);

            h.ApplyDamage(Lethal);
            Assert.AreEqual(1f, h.Current, "Unbroken revives at 1 HP");

            h.ApplyDamage(Lethal);
            Assert.That(h.Current, Is.EqualTo(30f).Within(0.001f), "Second Wind revives at 30%");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Unbroken_OncePerRun_SurvivesNodeReload()
        {
            RunState.Begin(123u);
            Assert.IsFalse(RunState.UnbrokenUsed);
            RunState.NoteUnbrokenUsed();

            RunSnapshot snap = JsonUtility.FromJson<RunSnapshot>(JsonUtility.ToJson(RunState.ToSnapshot()));
            RunState.Reset();
            Assert.IsTrue(RunState.Restore(snap));
            Assert.IsTrue(RunState.UnbrokenUsed, "node reload must not refresh Unbroken");
            Assert.AreEqual(1, RunSnapshot.CurrentVersion);

            RunState.Begin(123u);
            Assert.IsFalse(RunState.UnbrokenUsed, "new run resets it");
        }

        [Test]
        public void LegacySnapshotJson_WithoutUnbrokenField_LoadsAsFalse()
        {
            RunSnapshot snap = JsonUtility.FromJson<RunSnapshot>("{\"version\":1,\"seed\":123,\"nodeIndex\":0}");
            Assert.IsTrue(RunState.Restore(snap));
            Assert.IsFalse(RunState.UnbrokenUsed);
        }
    }
}
