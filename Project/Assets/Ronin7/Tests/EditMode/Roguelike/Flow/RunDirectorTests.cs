using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Coverage for <see cref="RunDirector"/>'s pure decision seams (A7.10): the post-clear-node
    /// decision, the boon-offer soft-lock resolution (A7.2), the scene-load latch (A7.1's
    /// pendingScene/transitioning idiom), starting-stack clamping (A7.6), the heal-on-kill gate
    /// (A7.4), and the Health.DeathInterceptor chaining that fixes the Second Wind / Unbroken
    /// conflict (A6.1/A6.2). No scene/rig needed for any of these.
    /// </summary>
    public class RunDirectorTests
    {
        [TestCase(RoomKind.Combat, false, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Combat, true, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Elite, false, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Elite, true, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Treasure, false, nameof(RunDirector.PostClearAction.OpenTreasure))]
        [TestCase(RoomKind.Treasure, true, nameof(RunDirector.PostClearAction.OpenTreasure))]
        [TestCase(RoomKind.Forge, false, nameof(RunDirector.PostClearAction.OpenForge))]
        [TestCase(RoomKind.Forge, true, nameof(RunDirector.PostClearAction.OpenForge))]
        [TestCase(RoomKind.Boss, false, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Boss, true, nameof(RunDirector.PostClearAction.WinRun))]
        [TestCase(RoomKind.Shop, false, nameof(RunDirector.PostClearAction.OpenShop))]
        [TestCase(RoomKind.Event, false, nameof(RunDirector.PostClearAction.OpenEvent))]
        public void ResolvePostClear_MatchesRewardTable(RoomKind kind, bool isFinalNode, string expected)
        {
            Assert.AreEqual(expected, RunDirector.ResolvePostClear(kind, isFinalNode).ToString());
        }

        // ---- A7.2/A7.10: ResolveOffer — the boon-panel soft-lock decision ----

        [TestCase(0, true, true, nameof(RunDirector.OfferResolution.AutoAdvanceNoChoices))]
        [TestCase(0, false, false, nameof(RunDirector.OfferResolution.AutoAdvanceNoChoices))] // no-choices wins even when camera/EventSystem are also missing
        [TestCase(3, true, false, nameof(RunDirector.OfferResolution.AutoAdvanceNoEventSystem))]
        [TestCase(3, false, false, nameof(RunDirector.OfferResolution.AutoAdvanceNoEventSystem))] // EventSystem checked before camera
        [TestCase(3, false, true, nameof(RunDirector.OfferResolution.AutoAdvanceNoCamera))]
        [TestCase(3, true, true, nameof(RunDirector.OfferResolution.ShowPanel))]
        public void ResolveOffer_MatchesPriorityOrder(int choiceCount, bool hasCamera, bool hasEventSystem, string expected)
        {
            Assert.AreEqual(expected, RunDirector.ResolveOffer(choiceCount, hasCamera, hasEventSystem).ToString());
        }

        // ---- A7.2/A7.10: CanHonourChoice — the guard that must never silently stall the offer ----
        //
        // OnBoonChoiceSelected used to `return` outright when this was false, which left the offer's
        // fallback deadline armed so Update() re-entered it every frame forever: the panel was never
        // dismissed and the run never advanced. The guard now only decides whether a boon is GRANTED;
        // the caller dismisses and advances either way.

        [TestCase(3, 0, true)]
        [TestCase(3, 2, true)]
        [TestCase(1, 0, true)]
        [TestCase(3, 3, false)]   // off the end
        [TestCase(3, -1, false)]  // negative
        [TestCase(0, 0, false)]   // A1.6: reroll can legitimately leave zero offerable boons
        public void CanHonourChoice_MatchesBounds(int choiceCount, int index, bool expected)
        {
            var choices = new List<BoonDefinition>();
            for (int i = 0; i < choiceCount; i++)
                choices.Add(ScriptableObject.CreateInstance<BoonDefinition>());

            try
            {
                Assert.AreEqual(expected, RunDirector.CanHonourChoice(choices, index));
            }
            finally
            {
                foreach (var c in choices) UnityEngine.Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void CanHonourChoice_NullChoices_IsFalse()
        {
            Assert.IsFalse(RunDirector.CanHonourChoice(null, 0));
        }

        // ---- A7.1/A7.10: the scene-load latch ----

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ShouldLatchSceneLoad_MatchesTransitioningFlag(bool transitioning, bool expected)
        {
            Assert.AreEqual(expected, RunDirector.ShouldLatchSceneLoad(transitioning));
        }

        [Test]
        public void ConsumePendingSceneLoad_ReturnsQueuedSceneOnceThenNull()
        {
            var go = new GameObject("rd");
            go.SetActive(false); // keep Awake (Instance/DontDestroyOnLoad wiring) from running
            var director = go.AddComponent<RunDirector>();
            SetPrivateField(director, "pendingScene", "RunArena");

            Assert.AreEqual("RunArena", director.ConsumePendingSceneLoad());
            Assert.IsNull(director.ConsumePendingSceneLoad());

            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"expected a private field named '{name}'");
            field.SetValue(target, value);
        }

        // ---- A7.6/A7.10: StartingStacks clamp ----

        [TestCase(0, 3, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(5, 3, 3)] // A7.6: a level-5 meta upgrade must not exceed a maxStacks-3 catalog authoring
        [TestCase(5, 8, 5)]
        [TestCase(-1, 3, 0)]
        public void StartingStacks_ClampsToAuthoredMaxStacks(int upgradeLevel, int maxStacks, int expected)
        {
            Assert.AreEqual(expected, RunDirector.StartingStacks(upgradeLevel, maxStacks));
        }

        // ---- A7.4/A7.10: heal-on-kill gate ----

        [Test]
        public void ShouldHealOnKill_TrueForNonPlayerDeathWithPositiveHeal()
        {
            var dead = new GameObject("enemy");
            var player = new GameObject("player");

            Assert.IsTrue(RunDirector.ShouldHealOnKill(dead, player, 3f, true));

            UnityEngine.Object.DestroyImmediate(dead);
            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseForThePlayersOwnDeath()
        {
            var player = new GameObject("player");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(player, player, 3f, true));

            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseWhenNoHealOnKillBoonIsHeld()
        {
            var dead = new GameObject("enemy");
            var player = new GameObject("player");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(dead, player, 0f, true));

            UnityEngine.Object.DestroyImmediate(dead);
            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseWhenEitherReferenceIsMissing()
        {
            var dead = new GameObject("enemy");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(dead, null, 3f, true));
            Assert.IsFalse(RunDirector.ShouldHealOnKill(null, dead, 3f, true));

            UnityEngine.Object.DestroyImmediate(dead);
        }

        // ---- A6.1/A6.2: Health.DeathInterceptor chaining (Second Wind + Unbroken both fire) ----

        [Test]
        public void DeathInterceptorChain_SecondWindThenUnbroken_SurvivesTwiceThenDies()
        {
            var go = new GameObject("rig");
            var health = go.AddComponent<Health>();
            health.Configure(100f); // EditMode skips Awake; seed via Configure per Health's own test contract

            // Mirrors UnbrokenWard: registers first (rig OnEnable runs before RunDirector's hook),
            // fires at most once per life.
            bool unbrokenUsed = false;
            Func<bool> unbroken = () =>
            {
                if (unbrokenUsed) return false;
                unbrokenUsed = true;
                return true;
            };
            health.DeathInterceptor = unbroken;

            // Mirrors RunDirector.ApplyBoonInventoryToRig's A6.1 fix: capture the existing interceptor
            // and chain, never clobber/null it.
            int revivesRemaining = 1;
            Func<bool> consumeRevive = () =>
            {
                if (revivesRemaining <= 0) return false;
                revivesRemaining--;
                return true;
            };
            var previous = health.DeathInterceptor;
            health.DeathInterceptor = () => consumeRevive() || (previous != null && previous());
            health.ReviveFraction = 0.3f; // A6.2: RunDirector sets this while a Second Wind is held

            var lethal = new DamageInfo(200f, Vector3.zero, Vector3.forward, null);

            health.ApplyDamage(lethal); // Second Wind consumes its one revive
            Assert.IsTrue(health.IsAlive);
            Assert.That(health.Current, Is.EqualTo(30f).Within(0.001f)); // Max(1, 100 * 0.3f) — float, not exact

            health.ApplyDamage(lethal); // Second Wind is spent; Unbroken fires instead
            Assert.IsTrue(health.IsAlive);
            Assert.That(health.Current, Is.EqualTo(30f).Within(0.001f));

            health.ApplyDamage(lethal); // both spent — this one actually kills
            Assert.IsFalse(health.IsAlive);

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ---- BUG-02: zero-enemy clears wait for the rig's HP restore ----

        [TestCase(40f, 100f, 76f)]
        [TestCase(100f, 100f, 100f)]
        [TestCase(0f, 100f, 60f)]
        [TestCase(150f, 100f, 100f)] // stored above max (max fell) clamps
        public void ForgeResultHealth_CarriesHpPlusSixtyPercentOfMissing(float stored, float max, float expected)
        {
            Assert.That(RunDirector.ForgeResultHealth(stored, max), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void ApplyMaxHealth_RaisesMaxAndCurrentByDeltaPlusHeal_Bug43()
        {
            var go = new GameObject("HealthBug43");
            try
            {
                var h = go.AddComponent<Health>();
                h.Configure(100f);
                h.SetCurrent(60f);
                Assert.IsTrue(RunDirector.ApplyMaxHealth(h, 125f, 0f)); // tempered Ironskin: +25 max
                Assert.AreEqual(125f, h.Max);
                Assert.AreEqual(85f, h.Current, 0.001f);
                Assert.IsTrue(RunDirector.ApplyMaxHealth(h, 135f, 10f)); // fresh pick: +10 max, heal 10
                Assert.AreEqual(105f, h.Current, 0.001f);
                Assert.IsFalse(RunDirector.ApplyMaxHealth(h, 135f, 0f)); // nothing changed
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void ShouldDeferClear_OnlyWhileRigRestorePendingForSameNode(bool awaiting, bool sameNode, bool expected)
        {
            Assert.AreEqual(expected, RunDirector.ShouldDeferClear(awaiting, sameNode));
        }

        [Test]
        public void ReviveFraction_DefaultZero_MatchesLegacySurviveAtOneHp()
        {
            var go = new GameObject("rig");
            var health = go.AddComponent<Health>();
            health.Configure(100f);
            health.DeathInterceptor = () => true; // always survives, e.g. a lone Unbroken with no boon held

            health.ApplyDamage(new DamageInfo(200f, Vector3.zero, Vector3.forward, null));

            Assert.AreEqual(0f, health.ReviveFraction);
            Assert.AreEqual(1f, health.Current); // byte-identical to pre-A6.2 behaviour

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ---- P0-03: the boon-offer fallback freezes through a pause ----

        [Test]
        public void RemainingOfferSeconds_FreezeThaw()
        {
            // Offer armed at t=100 for 60 s; paused at t=130 -> 30 s left.
            float remaining = RunDirector.RemainingOfferSeconds(160f, 130f);
            Assert.AreEqual(30f, remaining, 1e-4f);
            // Resumed after a 90 s pause (t=220): the new deadline still leaves those 30 s.
            float thawedDeadline = 220f + remaining;
            Assert.AreEqual(30f, RunDirector.RemainingOfferSeconds(thawedDeadline, 220f), 1e-4f);
            // Already past the deadline: never negative.
            Assert.AreEqual(0f, RunDirector.RemainingOfferSeconds(160f, 200f));
        }

        // ---- BUG-25: the run summary is finalized only on the run-end scene's load ----

        [TestCase(true, "Phase6_Boot", true)]
        [TestCase(true, "RunArena", false)]   // death during an arena fade-in: wait for the menu load
        [TestCase(false, "Phase6_Boot", false)] // SAVE & QUIT / plain menu load
        [TestCase(false, "RunArena", false)]
        public void ShouldFinalizeRunEnd_Table(bool endingRun, string loaded, bool expected)
        {
            Assert.AreEqual(expected, RunDirector.ShouldFinalizeRunEnd(endingRun, loaded, "Phase6_Boot"));
        }

        // ---- P2-02: sector card, Forge MEND, death/victory beat ----

        [TestCase(RoomKind.Boss, 0, true)]
        [TestCase(RoomKind.Boss, 1, true)]
        [TestCase(RoomKind.Boss, 2, false)]   // the last boss wins the run instead
        [TestCase(RoomKind.Combat, 0, false)]
        [TestCase(RoomKind.Elite, 1, false)]
        public void ShouldShowSectorCard_OnlyAfterSector0And1Boss(RoomKind kind, int sector, bool expected)
        {
            Assert.AreEqual(expected, RunDirector.ShouldShowSectorCard(kind, sector));
        }

        [TestCase(0, "SECTOR 1 — THE RUSTFANG")]
        [TestCase(1, "SECTOR 2 — THE PROGRAM")]
        [TestCase(2, "SECTOR 3 — THE SILENT GARDEN")]
        public void SectorTitle_UsesBiomeNames(int sector, string expected)
        {
            Assert.AreEqual(expected, SectorText.Title(sector));
        }

        [Test]
        public void ForgeMend_Heals60PercentOfMissing()
        {
            Assert.That(RunDirector.ForgeResultHealth(40f, 100f), Is.EqualTo(76f).Within(0.001f));
        }

        // ---- BUG-30: Forge effects are visible on the cards ----

        [TestCase(40f, 100f, "+36 HP")]
        [TestCase(90f, 100f, "+6 HP")]
        [TestCase(99.5f, 100f, "HP FULL")] // HUD already shows 100/100: nothing visible to gain
        [TestCase(100f, 100f, "HP FULL")]
        public void ForgeMendLabel_ShowsHudVisibleGainOrHpFull(float current, float max, string expected)
        {
            Assert.AreEqual(expected, RunDirector.ForgeMendLabel(current, max));
        }

        [Test]
        public void ForgeMend_AppliedToRig_ChangesHealthAndStoredHp()
        {
            var rig = new GameObject("rig");
            var dirGo = new GameObject("director");
            var instance = typeof(VRRig).GetProperty("Instance");
            try
            {
                instance.SetValue(null, rig.AddComponent<VRRig>());
                var health = rig.AddComponent<Health>();
                health.Configure(100f);
                health.SetCurrent(40f);
                var director = dirGo.AddComponent<RunDirector>();
                typeof(RunDirector).GetMethod("ApplyForgeMend", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(director, null);
                Assert.AreEqual(76f, health.Current, 0.001f);
                Assert.AreEqual(76f, RunState.PlayerHealth, 0.001f);
            }
            finally
            {
                instance.SetValue(null, null);
                RunState.Reset();
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(dirGo);
            }
        }

        [Test]
        public void SectorCheckpoint_SaveAndQuit_ResumesAtNextSectorEntry()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronin7_ckpt_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            var go = new GameObject("RunDirector");
            var director = go.AddComponent<RunDirector>();
            try
            {
                RunState.Begin(7u);
                while (RunState.CurrentNode.Kind != RoomKind.Boss) Assert.IsTrue(RunState.Advance(RunState.NextChoices[0]));
                Assert.AreEqual(0, RunState.CurrentNode.Sector);
                RunState.NotePlayerHealth(77f);

                Assert.IsTrue(director.CheckpointAdvance());
                int entry = RunState.NodeIndex;
                Assert.AreEqual(1, RunState.CurrentNode.Sector, "advanced past the boss into sector 2");

                RunState.Reset();
                var snapshot = RunSnapshot.Read();
                Assert.IsNotNull(snapshot);
                Assert.IsTrue(RunState.Restore(snapshot));
                Assert.AreEqual(entry, RunState.NodeIndex, "resume lands at the next sector's first room");
                Assert.AreEqual(1, RunState.CurrentNode.Sector);
                Assert.AreEqual(77f, RunState.PlayerHealth);
            }
            finally
            {
                RunState.Reset();
                UnityEngine.Object.DestroyImmediate(go);
                SaveSystem.DirectoryOverride = null;
                MetaProgression.Reset();
                try { System.IO.Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void ClearEndingLatch_UnblocksLaterRuns()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronin7_latch_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            RunLog.DirectoryOverride = dir;
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            var go = new GameObject("RunDirector");
            var director = go.AddComponent<RunDirector>();
            try
            {
                RunState.Begin(1u);
                director.EndRun(won: false, beat: false); // latches; the scene load is never completed (stuck)
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No GameFlowManager"));
                RunState.Reset();
                Assert.IsTrue(director.EndingRun);
                director.ClearEndingLatch();
                Assert.IsFalse(director.EndingRun);
            }
            finally
            {
                RunState.Reset();
                UnityEngine.Object.DestroyImmediate(go);
                SaveSystem.DirectoryOverride = null;
                RunLog.DirectoryOverride = null;
                MetaProgression.Reset();
                try { System.IO.Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void EndRun_LatchesBeforeDelay()
        {
            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ronin7_endrun_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            RunLog.DirectoryOverride = dir;
            MetaProgression.ResetForTests();
            MetaProgression.Load();
            var go = new GameObject("RunDirector");
            var director = go.AddComponent<RunDirector>();
            int ended = 0;
            Action<RunEnded> count = _ => ended++;
            EventBus.Subscribe(count);
            RunState.Begin(1u);
            try
            {
                director.EndRun(won: false);
                Assert.IsTrue(director.EndingRun, "latched immediately, before the 1.2 s delay");
                director.EndRun(won: false); // a second death inside the beat
                Assert.AreEqual(1, ended, "RunEnded must publish once");
                // No scene load is attempted synchronously (that would log "No GameFlowManager"): the load waits out the beat.
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                EventBus.Unsubscribe(count);
                RunState.Reset();
                UnityEngine.Object.DestroyImmediate(go);
                SaveSystem.DirectoryOverride = null;
                RunLog.DirectoryOverride = null;
                MetaProgression.Reset();
                try { System.IO.Directory.Delete(dir, true); } catch { }
            }
        }

        [Test]
        public void UnknownBoonOnResume_WarnsOncePerId()
        {
            var go = new GameObject("RunDirector");
            var director = go.AddComponent<RunDirector>();
            var catalog = ScriptableObject.CreateInstance<BoonCatalog>();
            catalog.boons = new BoonDefinition[0];
            typeof(RunDirector).GetField("boonCatalog", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(director, catalog);
            RunState.Begin(1u);
            RunState.AddBoon("retired_boon");
            RunState.AddBoon("retired_boon");
            try
            {
                var unknown = new System.Text.RegularExpressions.Regex("Unknown boon 'retired_boon'");
                var noRig = new System.Text.RegularExpressions.Regex("No VRRig.Instance");
                LogAssert.Expect(LogType.Warning, unknown);
                LogAssert.Expect(LogType.Warning, noRig);
                director.ApplyBoonInventoryToRig();
                LogAssert.Expect(LogType.Warning, noRig);
                director.ApplyBoonInventoryToRig(); // second node load: no repeat of the unknown-boon warning
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                RunState.Reset();
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ---- P4-01: every kind is pushed onto the rig's generic stat slots ----

        [Test]
        public void PushBoonStats_WritesEveryKindAndNamedSlots()
        {
            var rig = new GameObject("rig");
            var mods = rig.AddComponent<PlayerCombatModifiers>();
            var heal = ScriptableObject.CreateInstance<BoonDefinition>();
            heal.id = "heal"; heal.effect = BoonEffectKind.HealOnKill; heal.magnitude = 3f; heal.maxStacks = 3;
            var blade = ScriptableObject.CreateInstance<BoonDefinition>();
            blade.id = "blade"; blade.effect = BoonEffectKind.BladeDamageMultiplier; blade.magnitude = 0.2f; blade.maxStacks = 3;
            var inv = new BoonInventory();
            inv.Add(heal); inv.Add(heal); inv.Add(blade);

            RunDirector.PushBoonStats(mods, inv);

            Assert.AreEqual(6f, mods.Boon(BoonEffectKind.HealOnKill), 1e-5f);
            Assert.AreEqual(0.2f, mods.Boon(BoonEffectKind.BladeDamageMultiplier), 1e-5f);
            Assert.AreEqual(1.2f, mods.BoonMultiplier, 1e-5f);
            Assert.AreEqual(0f, mods.Boon(BoonEffectKind.ComboBonus));

            UnityEngine.Object.DestroyImmediate(heal);
            UnityEngine.Object.DestroyImmediate(blade);
            UnityEngine.Object.DestroyImmediate(rig);
        }
    }
}
