using NUnit.Framework;
using Ronin7.World.Story;
using UnityEditor;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Enter/Reset/Exit state transitions on a constructed hierarchy. EditMode-safe: no coroutines,
    /// no physics; the flashback field is left unwired so RenderSettings are never touched.
    /// </summary>
    public class MemoryDiveControllerTests
    {
        private GameObject controllerGo;
        private GameObject diveRootGo;
        private GameObject entryGo;
        private GameObject exitGo;
        private GameObject rigGo;
        private MemoryDiveController dive;

        [SetUp]
        public void SetUp()
        {
            diveRootGo = new GameObject("DiveRoot");
            entryGo = new GameObject("Entry");
            entryGo.transform.SetParent(diveRootGo.transform, false);
            entryGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, 42f), Quaternion.Euler(0f, 90f, 0f));
            diveRootGo.SetActive(false);

            exitGo = new GameObject("Exit");
            exitGo.transform.position = new Vector3(1f, 0f, 8f);

            rigGo = new GameObject("Rig");
            rigGo.transform.position = new Vector3(0f, 0f, 2f);

            controllerGo = new GameObject("MemoryDive");
            dive = controllerGo.AddComponent<MemoryDiveController>();
            var so = new SerializedObject(dive);
            so.FindProperty("diveRoot").objectReferenceValue = diveRootGo;
            so.FindProperty("diveEntryPoint").objectReferenceValue = entryGo.transform;
            so.FindProperty("diveExitPoint").objectReferenceValue = exitGo.transform;
            so.FindProperty("rigRoot").objectReferenceValue = rigGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(diveRootGo);
            Object.DestroyImmediate(exitGo);
            Object.DestroyImmediate(rigGo);
        }

        private static void AssertAt(Transform t, Transform anchor)
        {
            Assert.Less(Vector3.Distance(t.position, anchor.position), 1e-4f,
                $"expected {t.name} at {anchor.position}, was {t.position}");
            Assert.Less(Quaternion.Angle(t.rotation, anchor.rotation), 0.01f);
        }

        [Test]
        public void EnterDive_ActivatesRoot_AndTeleportsRigToEntry()
        {
            dive.EnterDive();

            Assert.IsTrue(diveRootGo.activeSelf);
            Assert.IsTrue(dive.IsActive);
            AssertAt(rigGo.transform, entryGo.transform);
        }

        [Test]
        public void ResetToEntry_TeleportsRigBack_WithoutTouchingDiveState()
        {
            dive.EnterDive();
            rigGo.transform.SetPositionAndRotation(new Vector3(3f, 0f, 66f), Quaternion.identity);

            dive.ResetToEntry();

            Assert.IsTrue(dive.IsActive);
            AssertAt(rigGo.transform, entryGo.transform);
        }

        [Test]
        public void ExitDive_DeactivatesRoot_AndTeleportsRigToExitPoint()
        {
            dive.EnterDive();
            dive.ExitDive();

            Assert.IsFalse(diveRootGo.activeSelf);
            Assert.IsFalse(dive.IsActive);
            AssertAt(rigGo.transform, exitGo.transform);
        }

        [Test]
        public void EnterDive_WithCharacterControllerOnRig_StillTeleports()
        {
            // The teleport toggles the CharacterController around the transform write (same idiom as
            // ZoneBounds' fall reset) and must leave it enabled afterwards.
            var controller = rigGo.AddComponent<CharacterController>();

            dive.EnterDive();

            AssertAt(rigGo.transform, entryGo.transform);
            Assert.IsTrue(controller.enabled);
        }

        [Test]
        public void UnwiredFields_DoNotThrow()
        {
            var bareGo = new GameObject("BareDive");
            try
            {
                var bare = bareGo.AddComponent<MemoryDiveController>();
                Assert.DoesNotThrow(() => bare.EnterDive());
                Assert.DoesNotThrow(() => bare.ResetToEntry());
                Assert.DoesNotThrow(() => bare.ExitDive());
                Assert.IsFalse(bare.IsActive);
            }
            finally
            {
                Object.DestroyImmediate(bareGo);
            }
        }
    }
}
