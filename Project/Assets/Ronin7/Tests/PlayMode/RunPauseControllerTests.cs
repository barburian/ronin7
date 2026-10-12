using System.Collections;
using NUnit.Framework;
using Ronin7.Core;
using Ronin7.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Ronin7.Tests.PlayMode
{
    /// <summary>P0-03: a pause freezes time and shows the panel; only Resume (or the run going away)
    /// unfreezes it.</summary>
    public class RunPauseControllerTests
    {
        private GameObject cameraGo;
        private GameObject eventSystemGo;

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("Camera") { tag = "MainCamera" };
            cameraGo.AddComponent<Camera>();
            eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            if (RunPauseController.Instance == null)
                new GameObject("RunPauseController").AddComponent<RunPauseController>();
        }

        [TearDown]
        public void TearDown()
        {
            RunPauseController.Instance.Resume();
            RunState.Reset();
            Object.Destroy(cameraGo);
            Object.Destroy(eventSystemGo);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTimeAndShowsPanel_ResumeRestores()
        {
            var pause = RunPauseController.Instance;
            pause.Pause();
            Assert.IsFalse(pause.IsPaused, "Outside a run the pause is a no-op.");

            RunState.Begin(1);
            pause.Pause();
            Assert.IsTrue(pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(AudioListener.pause);
            Assert.IsNotNull(Object.FindAnyObjectByType<RunPausePanel>(), "The pause panel must be shown.");

            yield return null;
            Assert.IsTrue(pause.IsPaused, "A pause never ends by itself.");

            pause.Resume();
            yield return null;
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsNull(Object.FindAnyObjectByType<RunPausePanel>());
        }

        [UnityTest]
        public IEnumerator RunEndingWhilePaused_Unfreezes()
        {
            var pause = RunPauseController.Instance;
            RunState.Begin(1);
            pause.Pause();
            RunState.Reset();
            yield return null;
            Assert.IsFalse(pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
