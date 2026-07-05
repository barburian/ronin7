using System.Reflection;
using NUnit.Framework;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the Finished guarantee added to <see cref="DialoguePlayer"/>: once <c>Play()</c> has
    /// started a conversation, <see cref="DialoguePlayer.Finished"/> must fire exactly once, even if
    /// the coroutine never reaches its natural end because the GameObject is disabled/destroyed
    /// mid-line (the bug this guards: TalkInteractor/MissionDirector/HackTerminal soft-locking).
    ///
    /// EditMode doesn't pump coroutines, so these tests don't call Play() (which would start one);
    /// instead they drive the private `started` flag and lifecycle methods directly via reflection,
    /// mirroring the Life() idiom used by PostureMeterTests/UnbrokenWardTests.
    /// </summary>
    public class DialoguePlayerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        private static void Life(MonoBehaviour c, string method) =>
            c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);

        private static void SetStarted(DialoguePlayer player)
        {
            var field = typeof(DialoguePlayer).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "DialoguePlayer.started field not found — update this test.");
            field.SetValue(player, true);
        }

        [Test]
        public void OnDisable_MidConversation_FiresFinishedExactlyOnce()
        {
            _go = new GameObject("DialoguePlayerTestTarget");
            var player = _go.AddComponent<DialoguePlayer>();
            SetStarted(player); // simulates Play() having been called (its coroutine never advances here)

            int finishedCount = 0;
            player.Finished += () => finishedCount++;

            Life(player, "OnDisable");

            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void OnDisable_CalledTwice_StillFiresFinishedOnlyOnce()
        {
            _go = new GameObject("DialoguePlayerTestTarget2");
            var player = _go.AddComponent<DialoguePlayer>();
            SetStarted(player);

            int finishedCount = 0;
            player.Finished += () => finishedCount++;

            Life(player, "OnDisable");
            Life(player, "OnDisable"); // e.g. SetActive(false) then Destroy also disabling

            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void OnDestroy_AfterOnDisableAlreadyFinished_DoesNotDoubleFire()
        {
            _go = new GameObject("DialoguePlayerTestTarget3");
            var player = _go.AddComponent<DialoguePlayer>();
            SetStarted(player);

            int finishedCount = 0;
            player.Finished += () => finishedCount++;

            Life(player, "OnDisable"); // fires Finished (started && !finished)
            Life(player, "OnDestroy"); // must be a no-op now (already finished)

            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void OnDisable_PlayNeverCalled_DoesNotFireFinished()
        {
            _go = new GameObject("DialoguePlayerTestTarget4");
            var player = _go.AddComponent<DialoguePlayer>();
            // started left false: Play() was never called on this instance.

            int finishedCount = 0;
            player.Finished += () => finishedCount++;

            Life(player, "OnDisable");

            Assert.AreEqual(0, finishedCount);
        }
    }
}
