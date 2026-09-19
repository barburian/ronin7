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
            Assert.IsFalse(EnemySwordVisual.EnsureVisible(null, null));
        }

        [Test]
        public void EmptyWeapon_BuildsRenderers_WithNoColliders()
        {
            _go = new GameObject("Weapon");

            bool built = EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);

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

            EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);
            bool builtAgain = EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);

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

            bool built = EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);

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
            EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);

            var blade = _go.transform.Find("Blade");
            Assert.IsNotNull(blade);

            float tipZ = blade.localPosition.z + blade.localScale.z * 0.5f;
            Assert.AreEqual(0.5f, tipZ, 1e-4f);
        }

        [Test]
        public void OwnerWithCharacterArt_SkipsPlaceholder()
        {
            // Rigged Tripo art sculpts the character's own weapon into the body mesh, and binds the
            // combat rig to a bare skinning bone — so the Renderer guard never trips and the enemy
            // used to end up holding a second, mismatched greybox katana.
            _go = new GameObject("Enemy");
            var art = new GameObject("Visual_Skinned");
            art.transform.SetParent(_go.transform, false);
            art.AddComponent<SkinnedMeshRenderer>();
            var weapon = new GameObject("Rig_ArmR");
            weapon.transform.SetParent(_go.transform, false);

            bool built = EnemySwordVisual.EnsureVisible(weapon.transform, _go.transform);

            Assert.IsFalse(built);
            Assert.AreEqual(0, weapon.transform.childCount);
        }

        [Test]
        public void OwnerWithInactiveCharacterArt_SkipsPlaceholder()
        {
            // Baked roguelike enemy prefabs are saved with the root INACTIVE, so the art is only
            // found by an include-inactive search.
            _go = new GameObject("Enemy");
            var art = new GameObject("Visual_Skinned");
            art.transform.SetParent(_go.transform, false);
            art.AddComponent<SkinnedMeshRenderer>();
            art.SetActive(false);
            var weapon = new GameObject("Rig_ArmR");
            weapon.transform.SetParent(_go.transform, false);

            Assert.IsFalse(EnemySwordVisual.EnsureVisible(weapon.transform, _go.transform));
        }

        [Test]
        public void GreyboxOwner_StillBuildsPlaceholder()
        {
            // Greybox enemies (MeshRenderer primitives, no character art) keep the fallback katana —
            // without it a chopping capsule has nothing visible to telegraph its swing.
            _go = new GameObject("Enemy");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(_go.transform, false);
            var weapon = new GameObject("ArmR");
            weapon.transform.SetParent(body.transform, false);

            bool built = EnemySwordVisual.EnsureVisible(weapon.transform, _go.transform);

            Assert.IsTrue(built);
            Assert.AreEqual(3, weapon.GetComponentsInChildren<Renderer>(true).Length);
        }

        [Test]
        public void FindBladeRenderer_NullWeapon_ReturnsNull()
        {
            Assert.IsNull(EnemySwordVisual.FindBladeRenderer(null));
        }

        [Test]
        public void FindBladeRenderer_AfterEnsureVisible_ResolvesDirectBladeChild()
        {
            _go = new GameObject("Weapon");
            EnemySwordVisual.EnsureVisible(_go.transform, _go.transform);

            var renderer = EnemySwordVisual.FindBladeRenderer(_go.transform);

            Assert.IsNotNull(renderer);
            Assert.AreEqual("Blade", renderer.gameObject.name);
        }

        [Test]
        public void FindBladeRenderer_NestedBladeChild_ResolvesForArtPrefabRig()
        {
            // Art-prefab katanas (ArtPrefabBuilder.AttachHeldKatanaRig) nest Blade one level
            // deeper than the runtime placeholder — under a "Sword" child of weapon, not weapon
            // itself. FindBladeRenderer must resolve that layout too.
            _go = new GameObject("Weapon");
            var sword = new GameObject("Sword");
            sword.transform.SetParent(_go.transform, false);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Blade";
            blade.transform.SetParent(sword.transform, false);

            var renderer = EnemySwordVisual.FindBladeRenderer(_go.transform);

            Assert.IsNotNull(renderer);
            Assert.AreSame(blade.GetComponent<Renderer>(), renderer);
        }

        [Test]
        public void FindBladeRenderer_NoBladeChild_ReturnsNull()
        {
            _go = new GameObject("Weapon");

            Assert.IsNull(EnemySwordVisual.FindBladeRenderer(_go.transform));
        }

        [Test]
        public void BladeTint_RestColor_ReturnsBladeBaseColor()
        {
            Assert.AreEqual(EnemySwordVisual.BladeColor, EnemySwordVisual.BladeTint(Color.white));
        }

        [Test]
        public void BladeTint_NonRestColor_PassesThroughUnchanged()
        {
            var telegraphColor = new Color(1f, 0.4f, 0.25f);
            var staggerColor = new Color(0.4f, 0.6f, 1f);

            Assert.AreEqual(telegraphColor, EnemySwordVisual.BladeTint(telegraphColor));
            Assert.AreEqual(staggerColor, EnemySwordVisual.BladeTint(staggerColor));
            Assert.AreEqual(Color.gray, EnemySwordVisual.BladeTint(Color.gray));
        }
    }
}
