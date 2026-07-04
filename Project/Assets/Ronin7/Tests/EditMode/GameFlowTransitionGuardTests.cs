using System.Reflection;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the re-entrancy fix in <see cref="GameFlowManager"/>: OnLandingRequested and
    /// OnZoneCompleted used to mutate CampaignState (NoteLanding / NoteZoneCompleted) BEFORE
    /// Transition()'s own `if (transitioning) return;` guard could drop a duplicate call, so a
    /// double-fire (e.g. two LandingRequested publishes in the same frame) desynced the campaign
    /// state even though only one transition actually ran. Each handler now bails before touching
    /// CampaignState when a transition is already underway.
    /// </summary>
    public class GameFlowTransitionGuardTests
    {
        private GameObject go;
        private GameFlowManager manager;

        [SetUp]
        public void SetUp()
        {
            CampaignState.Reset();
            // Built inactive so Awake/OnEnable/Start (Instance/DontDestroyOnLoad, EventBus subscribe,
            // the auto-boot transition) never run in EditMode — the guard fix only needs the private
            // handler methods invoked directly below.
            go = new GameObject();
            go.SetActive(false);
            manager = go.AddComponent<GameFlowManager>();
            SetTransitioning(true);
        }

        [TearDown]
        public void TearDown()
        {
            CampaignState.Reset();
            if (go != null) Object.DestroyImmediate(go);
        }

        private void SetTransitioning(bool value)
        {
            typeof(GameFlowManager).GetField("transitioning", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, value);
        }

        private void InvokeLandingRequested(LandingRequested evt)
        {
            typeof(GameFlowManager).GetMethod("OnLandingRequested", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { evt });
        }

        private void InvokeZoneCompleted(ZoneCompleted evt)
        {
            typeof(GameFlowManager).GetMethod("OnZoneCompleted", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(manager, new object[] { evt });
        }

        [Test]
        public void OnLandingRequested_WhileTransitioning_LeavesCampaignStateUntouched()
        {
            InvokeLandingRequested(new LandingRequested("SomeZoneScene"));

            Assert.AreEqual("", CampaignState.LastPlanetScene);
        }

        [Test]
        public void OnZoneCompleted_WhileTransitioning_LeavesCampaignStateUntouched()
        {
            // Baseline as if a landing had already registered a current planet, so an unguarded
            // NoteZoneCompleted would have something to mark complete.
            CampaignState.NoteLanding("PlanetA", fromSpace: true);

            InvokeZoneCompleted(new ZoneCompleted());

            Assert.AreEqual(0, CampaignState.Completed.Count);
        }
    }
}
