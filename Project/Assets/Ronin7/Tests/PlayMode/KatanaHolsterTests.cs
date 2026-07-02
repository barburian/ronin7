using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>
    /// The holster contract: the sword sits at the hip anchor whenever it is not held — snapped
    /// there on Awake, and snapped back on release no matter where the hand let go of it. This is
    /// what guarantees the player can never lose the sword in the world.
    /// </summary>
    public class KatanaHolsterTests
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
        }

        private Grabbable CreateSword(Vector3 position)
        {
            var go = new GameObject("Sword");
            go.transform.position = position;
            go.AddComponent<Rigidbody>();
            var grab = go.AddComponent<Grabbable>();
            _spawned.Add(go);
            return grab;
        }

        private KatanaHolster CreateHolster(Grabbable sword, out Transform hipAnchor)
        {
            var rig = new GameObject("Rig");
            _spawned.Add(rig);
            var anchorGo = new GameObject("HipAnchor");
            anchorGo.transform.SetParent(rig.transform, false);
            anchorGo.transform.localPosition = new Vector3(0.15f, 0.9f, 0.05f);
            hipAnchor = anchorGo.transform;

            // Inject the private serialized refs before Awake runs (rig starts inactive).
            rig.SetActive(false);
            var holster = rig.AddComponent<KatanaHolster>();
            var type = typeof(KatanaHolster);
            type.GetField("sword", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(holster, sword);
            type.GetField("hipAnchor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(holster, hipAnchor);
            rig.SetActive(true);
            return holster;
        }

        private static void AssertSheathed(Grabbable sword, Transform hipAnchor, string context)
        {
            Assert.AreEqual(hipAnchor, sword.transform.parent, $"{context}: sword should be parented to the hip anchor.");
            Assert.Less(sword.transform.localPosition.magnitude, 0.001f, $"{context}: sword should sit at the anchor.");
            Assert.IsTrue(sword.GetComponent<Rigidbody>().isKinematic, $"{context}: sheathed sword must be kinematic.");
        }

        [UnityTest]
        public IEnumerator Holster_OnAwake_SnapsSwordToHip()
        {
            var sword = CreateSword(new Vector3(5f, 0f, 5f)); // spawned loose in the world
            CreateHolster(sword, out var hipAnchor);
            yield return null;

            AssertSheathed(sword, hipAnchor, "Awake");
        }

        [UnityTest]
        public IEnumerator Holster_ReleasedFarFromHip_ReturnsSwordToHip()
        {
            var sword = CreateSword(Vector3.zero);
            CreateHolster(sword, out var hipAnchor);
            yield return null;

            var hand = new GameObject("Hand").transform;
            _spawned.Add(hand.gameObject);
            sword.Grab(hand);
            hand.position = new Vector3(20f, 1f, 20f); // wander far from the hip
            yield return null;
            Assert.AreNotEqual(hipAnchor, sword.transform.parent, "Held sword should follow the hand, not the hip.");

            sword.Release(new Vector3(0f, 5f, 0f), Vector3.zero); // thrown away
            yield return null;

            AssertSheathed(sword, hipAnchor, "Release far from hip");
        }
    }
}
