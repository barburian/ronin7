using System.Reflection;
using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class TimeLoopControllerTests
    {
        private GameObject _controller;
        private TimeLoopController _timeLoop;

        [SetUp]
        public void SetUp()
        {
            _controller = new GameObject("TimeLoopControllerTest");
            _timeLoop = _controller.AddComponent<TimeLoopController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
            {
                Object.DestroyImmediate(_controller);
            }
        }

        [Test]
        public void AdvancePhase_WrapsModuloGroupCount()
        {
            // Create 3 phase groups
            var groups = new TimeLoopController.PhaseGroup[3];
            groups[0] = new TimeLoopController.PhaseGroup { members = new GameObject[0] };
            groups[1] = new TimeLoopController.PhaseGroup { members = new GameObject[0] };
            groups[2] = new TimeLoopController.PhaseGroup { members = new GameObject[0] };

            SetPhaseGroups(groups);

            // CurrentPhase starts at 0
            Assert.AreEqual(0, _timeLoop.CurrentPhase);

            // Advance 3 times — should wrap back to 0
            _timeLoop.AdvancePhase();
            Assert.AreEqual(1, _timeLoop.CurrentPhase);

            _timeLoop.AdvancePhase();
            Assert.AreEqual(2, _timeLoop.CurrentPhase);

            _timeLoop.AdvancePhase();
            Assert.AreEqual(0, _timeLoop.CurrentPhase);
        }

        [Test]
        public void AdvancePhase_ActivatesOnlyCurrentPhaseGroup()
        {
            // Create 3 groups, each with one child object
            var obj0 = new GameObject("Phase0Object");
            var obj1 = new GameObject("Phase1Object");
            var obj2 = new GameObject("Phase2Object");

            var groups = new TimeLoopController.PhaseGroup[3];
            groups[0] = new TimeLoopController.PhaseGroup { members = new GameObject[] { obj0 } };
            groups[1] = new TimeLoopController.PhaseGroup { members = new GameObject[] { obj1 } };
            groups[2] = new TimeLoopController.PhaseGroup { members = new GameObject[] { obj2 } };

            SetPhaseGroups(groups);

            // Manually call ApplyPhase to set up initial state (EditMode doesn't auto-call Start)
            InvokeApplyPhase();

            // Phase 0: obj0 active, obj1 and obj2 inactive
            Assert.IsTrue(obj0.activeSelf);
            Assert.IsFalse(obj1.activeSelf);
            Assert.IsFalse(obj2.activeSelf);

            // Advance to phase 1
            _timeLoop.AdvancePhase();
            Assert.IsFalse(obj0.activeSelf);
            Assert.IsTrue(obj1.activeSelf);
            Assert.IsFalse(obj2.activeSelf);

            // Advance to phase 2
            _timeLoop.AdvancePhase();
            Assert.IsFalse(obj0.activeSelf);
            Assert.IsFalse(obj1.activeSelf);
            Assert.IsTrue(obj2.activeSelf);

            // Cleanup
            Object.DestroyImmediate(obj0);
            Object.DestroyImmediate(obj1);
            Object.DestroyImmediate(obj2);
        }

        [Test]
        public void AdvancePhase_WithZeroGroups_DoesNotThrow()
        {
            var groups = new TimeLoopController.PhaseGroup[0];
            SetPhaseGroups(groups);

            // Should not throw
            Assert.DoesNotThrow(() => _timeLoop.AdvancePhase());
            Assert.AreEqual(0, _timeLoop.CurrentPhase);
        }

        private void SetPhaseGroups(TimeLoopController.PhaseGroup[] groups)
        {
            var field = typeof(TimeLoopController).GetField("phaseGroups", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "phaseGroups field not found");
            field.SetValue(_timeLoop, groups);
        }

        private void InvokeApplyPhase()
        {
            var method = typeof(TimeLoopController).GetMethod("ApplyPhase", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "ApplyPhase method not found");
            method.Invoke(_timeLoop, null);
        }
    }
}
