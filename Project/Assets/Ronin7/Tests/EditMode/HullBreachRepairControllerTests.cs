using NUnit.Framework;
using Ronin7.World;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// EP32 hull breach repair controller: covers the Unity-serialization contract for authored
    /// breaches (AddBreach via the Ep32BuilderFinale must survive scene save/load) and the pulse-clock
    /// reset that gives a late-activated controller a fresh breachInterval grace window instead of an
    /// instant damage pulse. Health-integrated Tick behavior lives in Ep32MechanicsPlayTests.
    /// </summary>
    public class HullBreachRepairControllerTests
    {
        private GameObject go;
        private HullBreachRepairController controller;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("HullBreachController");
            controller = go.AddComponent<HullBreachRepairController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Breaches_SurviveUnitySerializationRoundTrip()
        {
            controller.AddBreach("breach_1");
            controller.AddBreach("breach_2");

            string json = EditorJsonUtility.ToJson(controller);

            var freshGo = new GameObject("HullBreachControllerFresh");
            try
            {
                var fresh = freshGo.AddComponent<HullBreachRepairController>();
                EditorJsonUtility.FromJsonOverwrite(json, fresh);

                Assert.AreEqual(2, fresh.BreachCount);
            }
            finally
            {
                Object.DestroyImmediate(freshGo);
            }
        }

        [Test]
        public void ResetPulseClock_PreventsInstantPulseOnLateActivation()
        {
            var playerGo = new GameObject("Player");
            try
            {
                var playerHealth = playerGo.AddComponent<Ronin7.Combat.Health>();
                playerHealth.Configure(100f);
                controller.Configure(playerHealth);

                // Breach authored at scene-load time; lastPulseTime defaults to 0.
                controller.AddBreach("breach_test");

                // Controller activates late into the scene (e.g. at t=500s).
                controller.ResetPulseClock(500f);

                // Ticking at the activation time must not fire an instant pulse.
                int pulsesAtActivation = controller.Tick(500f);
                Assert.AreEqual(0, pulsesAtActivation, "Late activation must not cause an instant pulse");
                Assert.AreEqual(100f, playerHealth.Current, 0.001f);

                // A full breachInterval (default 5s) later, the pulse fires as normal.
                int pulsesAfterInterval = controller.Tick(505f);
                Assert.AreEqual(1, pulsesAfterInterval, "Pulse should fire once a full breachInterval has elapsed since reset");
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
            }
        }
    }
}
