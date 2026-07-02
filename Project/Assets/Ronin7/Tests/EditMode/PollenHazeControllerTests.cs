using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    public class PollenHazeControllerTests
    {
        private GameObject go;
        private PollenHazeController controller;

        [SetUp]
        public void Setup()
        {
            go = new GameObject();
            controller = go.AddComponent<PollenHazeController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Coherence_StartsAtOne()
        {
            Assert.AreEqual(1f, controller.Coherence, 0.001f);
        }

        [Test]
        public void Tick_ReducesCoherence()
        {
            controller.Tick(1f);
            Assert.AreEqual(0.9f, controller.Coherence, 0.001f);
        }

        [Test]
        public void SetCoherence_ClampsToRange()
        {
            controller.SetCoherence(-5f);
            Assert.AreEqual(0f, controller.Coherence, 0.001f);

            controller.SetCoherence(5f);
            Assert.AreEqual(1f, controller.Coherence, 0.001f);
        }

        [Test]
        public void OnHazeEngaged_FiresOnceWhenCrossingThreshold()
        {
            int counter = 0;
            controller.onHazeEngaged.AddListener(() => counter++);

            controller.SetCoherence(0.4f);
            controller.SetCoherence(0.3f);

            Assert.AreEqual(1, counter);
            Assert.IsTrue(controller.IsEngaged);
        }

        [Test]
        public void Clear_RestoresAndFiresOnCleared()
        {
            controller.SetCoherence(0.2f);

            int counter = 0;
            controller.onCleared.AddListener(() => counter++);

            controller.Clear();

            Assert.AreEqual(1f, controller.Coherence, 0.001f);
            Assert.IsFalse(controller.IsEngaged);
            Assert.AreEqual(1, counter);
        }
    }
}
