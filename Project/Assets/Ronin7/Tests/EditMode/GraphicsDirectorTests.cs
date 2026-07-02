using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Verifies the one place the look dials up/down: <see cref="GraphicsDirector.SetQuality"/> must
    /// push the matching <see cref="GraphicsRuntime"/> snapshot that the combat VFX storm and the
    /// plasma-blade lights read. (Bloom itself lives on a runtime Volume built in Awake, which the
    /// editor test harness doesn't run — that part is the owed in-headset/play-mode check.)
    /// </summary>
    public class GraphicsDirectorTests
    {
        private GraphicsDirector director;

        [SetUp]
        public void SetUp()
        {
            director = new GameObject("GraphicsDirector (test)").AddComponent<GraphicsDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (director != null) Object.DestroyImmediate(director.gameObject);
        }

        [Test]
        public void SetQuality_Low_DampensParticlesAndDisablesBladeLights()
        {
            director.SetQuality(GraphicsQuality.Low);

            Assert.AreEqual(GraphicsQuality.Low, GraphicsRuntime.Quality);
            Assert.Less(GraphicsRuntime.ParticleScale, 1f, "Low tier should thin the particle storm.");
            Assert.IsFalse(GraphicsRuntime.BladeLightsEnabled, "Low tier should drop blade point-lights.");
        }

        [Test]
        public void SetQuality_High_FullParticlesAndBladeLights()
        {
            director.SetQuality(GraphicsQuality.High);

            Assert.AreEqual(GraphicsQuality.High, GraphicsRuntime.Quality);
            Assert.AreEqual(1f, GraphicsRuntime.ParticleScale, 1e-4f);
            Assert.IsTrue(GraphicsRuntime.BladeLightsEnabled);
        }
    }
}
