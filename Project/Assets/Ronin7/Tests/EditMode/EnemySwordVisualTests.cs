using NUnit.Framework;
using Ronin7.Enemies;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    public class EnemySwordVisualTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void NullWeapon_NoOp_ReturnsFalse()
        {
            Assert.IsFalse(EnemySwordVisual.EnsureVisible(null));
        }

        [Test]
        public void EmptyWeapon_BuildsRenderers_WithNoColliders()
        {
            _go = new GameObject("Weapon");

            bool built = EnemySwordVisual.EnsureVisible(_go.transform);

            Assert.IsTrue(built);
            Assert.AreEqual(3, _go.GetComponentsInChildren<Renderer>(true).Length,
                "Expected Handle + Guard + Blade.");
            Assert.AreEqual(0, _go.GetComponentsInChildren<Collider>(true).Length,
                "Visual-only geometry must not carry colliders (would pollute parry/blade physics probes).");
        }

        [Test]
        public void SecondCall_IsIdempotent_DoesNotDuplicateRenderers()
        {
            _go = new GameObject("Weapon");

            EnemySwordVisual.EnsureVisible(_go.transform);
            bool builtAgain = EnemySwordVisual.EnsureVisible(_go.transform);

            Assert.IsFalse(builtAgain);
            Assert.AreEqual(3, _go.GetComponentsInChildren<Renderer>(true).Length);
        }

        [Test]
        public void WeaponAlreadyHasRenderer_NoOp_LeavesHierarchyUntouched()
        {
            _go = new GameObject("Weapon");
            var existing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            existing.transform.SetParent(_go.transform, false);
            int childCountBefore = _go.transform.childCount;

            bool built = EnemySwordVisual.EnsureVisible(_go.transform);

            Assert.IsFalse(built);
            Assert.AreEqual(childCountBefore, _go.transform.childCount);
        }

        [Test]
        public void Blade_TipReach_MatchesFixedBladeTipContract()
        {
            // Every scene builder's fallback rig hangs BladeTip at local (0,0,0.5) off the Blade
            // child — the visual Blade's far Z edge must land on that same point so the rendered
            // sword doesn't over/under-reach the actual parry/swing-speed sample point.
            _go = new GameObject("Weapon");
            EnemySwordVisual.EnsureVisible(_go.transform);

            var blade = _go.transform.Find("Blade");
            Assert.IsNotNull(blade);

            float tipZ = blade.localPosition.z + blade.localScale.z * 0.5f;
            Assert.AreEqual(0.5f, tipZ, 1e-4f);
        }
    }
}
