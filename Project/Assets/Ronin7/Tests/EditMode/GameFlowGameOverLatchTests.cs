using System.Reflection;
using NUnit.Framework;
using Ronin7.Flow;
using Ronin7.Ship;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the A5 fix in <see cref="GameFlowManager"/>: a player death (EntityDied /
    /// PlayerShipDestroyed) that lands while `transitioning` is true (mid fade+load, e.g. a
    /// hazard/DoT tick during a hub&lt;-&gt;mission hop) used to be silently dropped for good by the
    /// `if (transitioning) return;` guard, so the player arrived in the next scene "alive" despite
    /// dying. ResolveGameOver is the pure Ignore/Latch/FireNow decision; ConsumePendingGameOver is
    /// the read-and-clear step Transition() calls once the in-flight transition finishes.
    /// </summary>
    public class GameFlowGameOverLatchTests
    {
        private GameObject go;
        private GameFlowManager manager;

        [SetUp]
        public void SetUp()
        {
            // Built inactive so Awake/OnEnable/Start (Instance/DontDestroyOnLoad, EventBus subscribe,
            // the auto-boot transition) never run in EditMode.
            go = new GameObject();
            go.SetActive(false);
            manager = go.AddComponent<GameFlowManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        private void SetField(string name, bool value)
        {
            typeof(GameFlowManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, value);
        }

        private bool GetField(string name)
        {
            return (bool)typeof(GameFlowManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(manager);
        }

        [Test]
        public void ResolveGameOver_Idle_FiresNow()
        {
            Assert.AreEqual(GameFlowManager.GameOverAction.FireNow,
                GameFlowManager.ResolveGameOver(gameOverInProgress: false, transitioning: false));
        }

        [Test]
        public void ResolveGameOver_Transitioning_Latches()
        {
            Assert.AreEqual(GameFlowManager.GameOverAction.Latch,
                GameFlowManager.ResolveGameOver(gameOverInProgress: false, transitioning: true));
        }

        [Test]
        public void ResolveGameOver_GameOverInProgress_Ignores()
        {
            Assert.AreEqual(GameFlowManager.GameOverAction.Ignore,
                GameFlowManager.ResolveGameOver(gameOverInProgress: true, transitioning: false));
        }

        [Test]
        public void ResolveGameOver_GameOverInProgressAndTransitioning_Ignores()
        {
            // gameOverInProgress wins regardless of transitioning: a game over is already in flight
            // (its own final FadeLoadFade back to the main menu also sets transitioning=true).
            Assert.AreEqual(GameFlowManager.GameOverAction.Ignore,
                GameFlowManager.ResolveGameOver(gameOverInProgress: true, transitioning: true));
        }

        [Test]
        public void OnPlayerShipDestroyed_WhileTransitioning_LatchesInsteadOfDropping()
        {
            SetField("transitioning", true);

            typeof(GameFlowManager).GetMethod("OnPlayerShipDestroyed", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { new PlayerShipDestroyed(null, Vector3.zero) });

            Assert.IsTrue(GetField("pendingGameOver"),
                "A death mid-transition must set the latch instead of being dropped by the transitioning guard.");
        }

        [Test]
        public void ConsumePendingGameOver_WhenPending_ReturnsTrueOnceThenFalse()
        {
            SetField("pendingGameOver", true);

            Assert.IsTrue(manager.ConsumePendingGameOver(), "First consume should report the latched death.");
            Assert.IsFalse(manager.ConsumePendingGameOver(), "Second consume must not double-fire.");
        }

        [Test]
        public void ConsumePendingGameOver_WhenNotPending_ReturnsFalse()
        {
            Assert.IsFalse(manager.ConsumePendingGameOver());
        }
    }
}
