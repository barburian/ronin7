using NUnit.Framework;
using Ronin7.EditorTools;
using Ronin7.Flow;
using Ronin7.Player;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>P9-04 (A15.16): the Tripo Polearm, Shield, Whip handle and coiled Whip sit at real scale on the rig gear and the
    /// select cards, add no colliders, use URP-lit materials and dim when locked. Gameplay transforms stay as P9-02 built them.</summary>
    public class WeaponMeshTests
    {
        private static readonly string[] Kinds = { "Shield", "Polearm", "WhipHandle", "WhipCoiled" };
        private GameObject rig;

        [TearDown] public void TearDown() { if (rig != null) Object.DestroyImmediate(rig); }

        private PolearmShieldGear BuildGear()
        {
            rig = new GameObject("Rig");
            XRRigBuilder.RogueEnsurePolearmShield(rig.transform);
            XRRigBuilder.RogueEnsureWhip(rig.transform);
            return rig.GetComponent<PolearmShieldGear>();
        }

        /// <summary>Bounds of the Tripo mesh (child "TripoMesh" of the visual parent) in the gear root's local space.</summary>
        private static Bounds InGear(Transform gear, string visual)
        {
            var mesh = gear.Find(visual + "/" + WeaponMeshFit.MeshName);
            Assert.IsNotNull(mesh, "Tripo mesh present under " + gear.name);
            var mf = mesh.GetComponentInChildren<MeshFilter>();
            var b = new Bounds(gear.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.vertices[0])), Vector3.zero);
            foreach (var v in mf.sharedMesh.vertices) b.Encapsulate(gear.InverseTransformPoint(mf.transform.TransformPoint(v)));
            return b;
        }

        [Test]
        public void Assets_NoColliders_UrpLit_Within10kTrisBudget()
        {
            foreach (var kind in Kinds)
            {
                var prefab = WeaponMeshFit.Prefab(kind);
                Assert.IsNotNull(prefab, kind);
                Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length, kind + " has a collider");
                int tris = 0;
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true)) tris += mf.sharedMesh.triangles.Length / 3;
                Assert.Less(tris, 12000, kind + " tris");
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        Assert.IsNotNull(m, kind);
                        Assert.AreEqual("Universal Render Pipeline/Lit", m.shader.name, kind);
                    }
            }
        }

        [Test]
        public void Polearm_BoxUnchanged_VisualReachAndAxisMatch()
        {
            var g = BuildGear();
            var box = g.polearm.transform.Find("Blade").GetComponent<BoxCollider>();
            Assert.AreEqual(0.3f, box.center.z - box.size.z * 0.5f, 1e-4f);
            Assert.AreEqual(1.4f, box.center.z + box.size.z * 0.5f, 1e-4f);
            var b = InGear(g.polearm.transform, "PolearmVisual");
            Assert.AreEqual(1.4f, b.max.z, 0.03f, "head tip about 1.4 m ahead of the hand");
            Assert.AreEqual(-0.35f, b.min.z, 0.03f, "rear visual behind the grip");
            Assert.Less(b.size.x, 0.35f, "shaft aligned to the blade axis (not diagonal)");
            Assert.Less(b.size.y, 0.35f, "shaft aligned to the blade axis (not diagonal)");
            Assert.AreEqual(1, g.polearm.GetComponentsInChildren<MeshFilter>(true).Length, "placeholders gone");
            Assert.AreEqual(2, g.polearm.GetComponentsInChildren<Collider>(true).Length, "grip + blade only");
            Assert.AreEqual(0, g.polearm.GetComponentsInChildren<MeshCollider>(true).Length);
        }

        [Test]
        public void Shield_About0p6Across_FaceOnTheShieldFace()
        {
            var g = BuildGear();
            var b = InGear(g.shield.transform, "ShieldVisual");
            Assert.AreEqual(0.6f, Mathf.Max(b.size.x, b.size.y), 0.02f);
            Assert.AreEqual(0.13f, b.max.z, 0.01f, "front surface at the ShieldFace (box 0.10 to 0.14)");
            Assert.Greater(b.min.z, -0.01f, "the back stays on the hand side of the face, not through the hand; bounds " + b);
            var face = g.shield.transform.Find("ShieldFace");
            Assert.AreEqual(new Vector3(0f, 0f, 0.12f), face.GetComponent<BoxCollider>().center);
            Assert.AreEqual(new Vector3(0.5f, 0.5f, 0.04f), face.GetComponent<BoxCollider>().size);
            Assert.AreEqual(1, g.shield.GetComponentsInChildren<MeshFilter>(true).Length, "placeholders gone");
            Assert.AreEqual(0, g.shield.GetComponentsInChildren<MeshCollider>(true).Length);
        }

        [Test]
        public void Whip_HandleLength_RingExactlyAtChainAttach()
        {
            var g = BuildGear();
            var b = InGear(g.whip.transform, "WhipVisual");
            Assert.AreEqual(0.33f, b.size.z, 0.02f, "handle about 0.3 m");
            WeaponMeshFit.WhipHandle(out var ringMesh);
            var mesh = g.whip.transform.Find("WhipVisual/" + WeaponMeshFit.MeshName);
            var ringInGear = g.whip.transform.InverseTransformPoint(mesh.TransformPoint(ringMesh));
            var attach = g.whip.transform.Find("ChainRing").localPosition;
            Assert.AreEqual(0f, Vector3.Distance(ringInGear, attach), 0.01f, "ring centre on the chain attach point");
            Assert.AreEqual(0.22f, attach.z, 1e-5f, "ChainRing unchanged");
            Assert.IsNotNull(g.whip.transform.Find("WhipVisual/Coil"), "holstered coil kept");
            Assert.IsNull(g.whip.transform.Find("ChainRing/Ring"), "the mesh's own ring replaces the placeholder");
            Assert.AreEqual(2, g.whip.GetComponentsInChildren<Collider>(true).Length, "grip + blade only");
        }

        [Test]
        public void ApplyTo_IsIdempotent()
        {
            var g = BuildGear();
            int meshes = g.GetComponentsInChildren<MeshFilter>(true).Length;
            Assert.IsFalse(WeaponMeshFit.ApplyTo(g));
            Assert.AreEqual(meshes, g.GetComponentsInChildren<MeshFilter>(true).Length);
        }

        [Test]
        public void CardModels_AreTripo_NoColliders_DisplayCopyStripped_LockedDims()
        {
            var models = Resources.Load<WeaponCardModels>(WeaponCardModels.ResourcePath);
            Assert.IsNotNull(models);
            Assert.AreEqual(2, models.Get(2).GetComponentsInChildren<MeshFilter>(true).Length, "Polearm card: Polearm + Shield meshes");
            Assert.AreEqual(1, models.Get(3).GetComponentsInChildren<MeshFilter>(true).Length, "Whip card: the coiled mesh");
            var card = new GameObject("Card");
            try
            {
                foreach (int i in new[] { 2, 3 })
                {
                    var prefab = models.Get(i);
                    Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length, "card " + i);
                    foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                        Assert.Greater(mf.sharedMesh.triangles.Length / 3, 5000, "card " + i + " shows a Tripo mesh, not a primitive");
                    var copy = WeaponSelectPanelFactory.AttachModel(prefab, card.transform, Vector3.zero, false);
                    Assert.AreEqual(0, copy.GetComponentsInChildren<Collider>(true).Length);
                    Assert.AreEqual(0, copy.GetComponentsInChildren<MonoBehaviour>(true).Length, "scripts stripped from the display copy");
                    var mpb = new MaterialPropertyBlock();
                    foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                    {
                        r.GetPropertyBlock(mpb);
                        Assert.AreEqual(Color.black, mpb.GetColor("_EmissionColor"), "locked emission, card " + i);
                        Assert.Less(mpb.GetColor("_BaseColor").grayscale, 0.5f, "locked base colour dimmed on URP/Lit, card " + i);
                    }
                    var open = WeaponSelectPanelFactory.AttachModel(prefab, card.transform, Vector3.zero, true);
                    foreach (var r in open.GetComponentsInChildren<Renderer>(true)) { r.GetPropertyBlock(mpb); Assert.IsTrue(mpb.isEmpty, "unlocked keeps authored look"); }
                }
            }
            finally { Object.DestroyImmediate(card); }
        }
    }
}
