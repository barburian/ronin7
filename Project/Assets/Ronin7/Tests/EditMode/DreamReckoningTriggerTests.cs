using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Ronin7.Combat;
using Ronin7.Enemies;

namespace Ronin7.Tests.EditMode
{
    public class DreamReckoningTriggerTests
    {
        private GameObject triggerGo;
        private DreamReckoningTrigger trigger;

        [SetUp]
        public void Setup()
        {
            triggerGo = new GameObject();
            trigger = triggerGo.AddComponent<DreamReckoningTrigger>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(triggerGo);
        }

        [Test]
        public void Acknowledge_FiresOnceIdempotent()
        {
            int counter = 0;
            trigger.onAcknowledged.AddListener(() => counter++);

            trigger.Acknowledge();
            trigger.Acknowledge();

            Assert.AreEqual(1, counter);
            Assert.IsTrue(trigger.HasAcknowledged);
        }

        [Test]
        public void Acknowledge_DissolvesLinkedPhantoms()
        {
            // Create a DreamPhantom with Health
            var phantomGo = new GameObject();
            phantomGo.SetActive(false);
            var health = phantomGo.AddComponent<Health>();
            var phantom = phantomGo.AddComponent<DreamPhantom>();
            phantomGo.SetActive(true);

            // Use SerializedObject to set the trigger's phantoms array
            var so = new SerializedObject(trigger);
            var phantomsProperty = so.FindProperty("phantoms");
            phantomsProperty.arraySize = 1;
            phantomsProperty.GetArrayElementAtIndex(0).objectReferenceValue = phantom;
            so.ApplyModifiedProperties();

            trigger.Acknowledge();

            Assert.IsFalse(phantomGo.activeSelf);

            Object.DestroyImmediate(phantomGo);
        }
    }
}
