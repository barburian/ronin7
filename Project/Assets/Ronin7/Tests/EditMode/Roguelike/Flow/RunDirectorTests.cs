using System;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

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
        [TestCase(RoomKind.Treasure, false, nameof(RunDirector.PostClearAction.AdvanceImmediately))]
        [TestCase(RoomKind.Treasure, true, nameof(RunDirector.PostClearAction.AdvanceImmediately))]
        [TestCase(RoomKind.Forge, false, nameof(RunDirector.PostClearAction.AdvanceImmediately))]
        [TestCase(RoomKind.Forge, true, nameof(RunDirector.PostClearAction.AdvanceImmediately))]
        [TestCase(RoomKind.Boss, false, nameof(RunDirector.PostClearAction.OfferBoon))]
        [TestCase(RoomKind.Boss, true, nameof(RunDirector.PostClearAction.WinRun))]
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

            Assert.IsTrue(RunDirector.ShouldHealOnKill(dead, player, 3f));

            UnityEngine.Object.DestroyImmediate(dead);
            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseForThePlayersOwnDeath()
        {
            var player = new GameObject("player");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(player, player, 3f));

            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseWhenNoHealOnKillBoonIsHeld()
        {
            var dead = new GameObject("enemy");
            var player = new GameObject("player");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(dead, player, 0f));

            UnityEngine.Object.DestroyImmediate(dead);
            UnityEngine.Object.DestroyImmediate(player);
        }

        [Test]
        public void ShouldHealOnKill_FalseWhenEitherReferenceIsMissing()
        {
            var dead = new GameObject("enemy");

            Assert.IsFalse(RunDirector.ShouldHealOnKill(dead, null, 3f));
            Assert.IsFalse(RunDirector.ShouldHealOnKill(null, dead, 3f));

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
    }
}
