using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Combat;
using Ronin7.Core;
using Ronin7.Ship;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    public class Ep11MechanicsPlayTests
    {
        private readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// EnemyShip_Disable_PublishesEventStaysAliveAndClearsEncounter: verify that calling Disable()
        /// on an EnemyShip publishes EnemyShipDisabled, leaves the GameObject intact (not destroyed),
        /// and correctly updates IsAlive to false.
        /// </summary>
        [UnityTest]
        public IEnumerator EnemyShip_Disable_PublishesEventAndSetIsAliveFalse()
        {
            // Create the enemy ship
            var shipGo = new GameObject("EnemyShip");
            _spawned.Add(shipGo);
            shipGo.transform.position = Vector3.zero;

            var health = shipGo.AddComponent<Health>();
            health.Configure(100f);

            var ship = shipGo.AddComponent<EnemyShip>();

            // Create a definition for proper initialization
            var definition = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            ship.Configure(definition, null, null, null);

            // Track the disable event
            bool disabledEventFired = false;
            EnemyShipDisabled receivedEvent = default;
            void OnDisabled(EnemyShipDisabled evt)
            {
                disabledEventFired = true;
                receivedEvent = evt;
            }

            EventBus.Subscribe<EnemyShipDisabled>(OnDisabled);

            try
            {
                yield return null; // Let Awake run

                // Verify initial state
                Assert.IsTrue(ship.IsAlive, "Ship should be alive initially");

                // Call Disable
                ship.Disable();

                // Verify GameObject is not destroyed
                Assert.IsNotNull(shipGo, "GameObject should not be destroyed after disable");

                // Verify IsAlive is now false
                Assert.IsFalse(ship.IsAlive, "Ship should not be alive after Disable()");

                // Verify the event was published
                Assert.IsTrue(disabledEventFired, "EnemyShipDisabled event should be published");
                Assert.AreEqual(shipGo, receivedEvent.Ship, "Event should carry the correct ship GameObject");
            }
            finally
            {
                EventBus.Unsubscribe<EnemyShipDisabled>(OnDisabled);
            }
        }

        /// <summary>
        /// EnemyShip_Disable_IsIdempotent: verify that calling Disable() multiple times is safe
        /// and only publishes the event once.
        /// </summary>
        [UnityTest]
        public IEnumerator EnemyShip_Disable_IsIdempotent()
        {
            // Create the enemy ship
            var shipGo = new GameObject("EnemyShip");
            _spawned.Add(shipGo);
            shipGo.transform.position = Vector3.zero;

            var health = shipGo.AddComponent<Health>();
            health.Configure(100f);

            var ship = shipGo.AddComponent<EnemyShip>();

            var definition = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            ship.Configure(definition, null, null, null);

            int disableEventCount = 0;
            void OnDisabled(EnemyShipDisabled evt) => disableEventCount++;

            EventBus.Subscribe<EnemyShipDisabled>(OnDisabled);

            try
            {
                yield return null;

                // Call Disable twice
                ship.Disable();
                ship.Disable();

                // Event should only fire once
                Assert.AreEqual(1, disableEventCount, "EnemyShipDisabled should only be published once");
                Assert.IsFalse(ship.IsAlive, "Ship should still not be alive");
            }
            finally
            {
                EventBus.Unsubscribe<EnemyShipDisabled>(OnDisabled);
            }
        }
    }
}
