using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ronin7.World.Story;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards TalkInteractor.NearestTalkable's nearest-eligible-NPC selection now that it iterates
    /// <see cref="StoryNpc.Active"/> (perf fix P-C) instead of calling FindObjectsByType every frame.
    /// Exercises the real private method via reflection, same idiom as UnbrokenWardTests/
    /// ProtectNpcObjectiveTests (EditMode does not auto-invoke Awake/OnEnable on AddComponent).
    /// </summary>
    public class TalkInteractorNearestTalkableTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private GameObject _camGo;
        private Camera _reusedMainCam;
        private Vector3 _reusedMainCamOriginalPos;

        private static void Life(StoryNpc npc, string method) =>
            typeof(StoryNpc).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(npc, null);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private StoryNpc MakeNpc(Vector3 pos, bool talked = false, bool remote = false, bool withDialogue = true)
        {
            var go = new GameObject("Npc");
            go.transform.position = pos;
            _spawned.Add(go);
            var npc = go.AddComponent<StoryNpc>();
            Life(npc, "OnEnable");

            if (withDialogue) SetField(npc, "dialogue", go.AddComponent<DialoguePlayer>());
            if (remote) SetField(npc, "remote", true);
            if (talked) npc.MarkTalked();

            return npc;
        }

        [SetUp]
        public void SetUp()
        {
            // NearestTalkable early-outs when Camera.main is null, and measures distance from its
            // transform. The EditMode test runner's ambient scene already has a MainCamera-tagged
            // camera (Camera.main caches to whichever camera claimed the tag first, so a second
            // freshly tagged GameObject here would silently lose and NearestTalkable would measure
            // from the wrong position). Reuse the existing one at the origin; only create one if the
            // ambient scene genuinely has none.
            var existing = Camera.main;
            if (existing != null)
            {
                _reusedMainCam = existing;
                _reusedMainCamOriginalPos = existing.transform.position;
                existing.transform.position = Vector3.zero;
            }
            else
            {
                _camGo = new GameObject("MainCamera");
                _camGo.tag = "MainCamera";
                _camGo.AddComponent<Camera>();
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            StoryNpc.Active.Clear();
            if (_reusedMainCam != null) _reusedMainCam.transform.position = _reusedMainCamOriginalPos;
            if (_camGo != null) Object.DestroyImmediate(_camGo);
        }

        [Test]
        public void NearestTalkable_PicksNearestEligibleNpc_WithinRadius()
        {
            var interactorGo = new GameObject("Interactor");
            _spawned.Add(interactorGo);
            var interactor = interactorGo.AddComponent<TalkInteractor>();
            SetField(interactor, "talkRadius", 5f);

            MakeNpc(new Vector3(0.5f, 0f, 0f), talked: true);      // ignored: already talked
            MakeNpc(new Vector3(0.6f, 0f, 0f), remote: true);      // ignored: remote
            MakeNpc(new Vector3(0.7f, 0f, 0f), withDialogue: false); // ignored: no dialogue
            MakeNpc(new Vector3(20f, 0f, 0f));                      // ignored: outside talkRadius
            MakeNpc(new Vector3(4f, 0f, 0f));                       // farther than nearNpc, but still in range
            var nearNpc = MakeNpc(new Vector3(2f, 0f, 0f));

            var result = (StoryNpc)typeof(TalkInteractor)
                .GetMethod("NearestTalkable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(interactor, null);

            Assert.AreSame(nearNpc, result);
        }
    }
}
