using NUnit.Framework;
using UnityEngine;
using Ronin7.World;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Lifecycle-free logic for the EP23 CryoChillController: chill rise/warm/clamp, the
    /// frostbite threshold one-shot events, and Clear(). The Health damage integration needs
    /// Awake/OnEnable and so lives in PlayMode (Ep23MechanicsPlayTests).
    /// </summary>
    public class CryoChillControllerTests
    {
        private GameObject go;
        private CryoChillController controller;

        [SetUp]
        public void Setup()
        {
            go = new GameObject();
            controller = go.AddComponent<CryoChillController>();
            controller.AutoAdvance = false; // deterministic: no Update()-driven ticks
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Chill_StartsAtZero()
        {
            Assert.AreEqual(0f, controller.Chill, 0.001f);
            Assert.IsFalse(controller.IsFrostbitten);
        }

        [Test]
        public void Tick_RaisesChill()
        {
            controller.Tick(1f);
            Assert.Greater(controller.Chill, 0f);
        }

        [Test]
        public void Warm_LowersChill_AndClampsAtZero()
        {
            controller.SetChill(0.5f);
            controller.Warm(1f);
            Assert.Less(controller.Chill, 0.5f);

            controller.Warm(100f);
            Assert.AreEqual(0f, controller.Chill, 0.001f);
        }

        [Test]
        public void SetChill_ClampsToRange()
        {
            controller.SetChill(-5f);
            Assert.AreEqual(0f, controller.Chill, 0.001f);

            controller.SetChill(5f);
            Assert.AreEqual(1f, controller.Chill, 0.001f);
        }

        [Test]
        public void OnFrostbite_FiresOnceWhenCrossingThreshold()
        {
            int counter = 0;
            controller.onFrostbite.AddListener(() => counter++);

            controller.SetChill(0.9f);
            controller.SetChill(0.95f); // still above threshold — must not refire

            Assert.AreEqual(1, counter);
            Assert.IsTrue(controller.IsFrostbitten);
        }

        [Test]
        public void WarmingBelowThreshold_FiresOnClearedOnce()
        {
            controller.SetChill(0.95f); // frostbite engages
            Assert.IsTrue(controller.IsFrostbitten);

            int counter = 0;
            controller.onCleared.AddListener(() => counter++);

            controller.SetChill(0.5f); // back below threshold

            Assert.AreEqual(1, counter);
            Assert.IsFalse(controller.IsFrostbitten);
        }

        [Test]
        public void Clear_ResetsAndFiresOnCleared()
        {
            controller.SetChill(0.95f);

            int counter = 0;
            controller.onCleared.AddListener(() => counter++);

            controller.Clear();

            Assert.AreEqual(0f, controller.Chill, 0.001f);
            Assert.IsFalse(controller.IsFrostbitten);
            Assert.AreEqual(1, counter);
        }
    }
}
