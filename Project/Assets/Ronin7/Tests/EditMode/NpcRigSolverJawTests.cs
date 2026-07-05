using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Guards the Rig_Jaw extension of the procedural NPC rig (the lip-sync fix): lower-front head
    /// vertices must weight to the jaw bone so VO amplitude can open the mouth, while the skull,
    /// back of the head, neck and torso stay rigidly on Body. Uses a 1.8m-tall unit-ish character
    /// bounds, mirroring how NpcAutoRigger derives the layout from mesh bounds.
    /// </summary>
    public class NpcRigSolverJawTests
    {
        // Character bounds: 1.8m tall, 0.6m wide, 0.3m deep, feet at y=0, centered on x=0/z=0.
        private static NpcRigLayout Layout() =>
            NpcRigSolver.SolveLayout(new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.8f, 0.3f)));

        private static float JawWeightOf(Vector3 v)
        {
            BoneWeight w = NpcRigSolver.SolveVertex(v, Layout());
            if (w.boneIndex0 == NpcRigSolver.BoneJaw) return w.weight0;
            if (w.boneIndex1 == NpcRigSolver.BoneJaw) return w.weight1;
            if (w.boneIndex2 == NpcRigSolver.BoneJaw) return w.weight2;
            return 0f;
        }

        [Test]
        public void ChinFrontVertex_IsJawWeighted()
        {
            // Mouth level (~88.5% of 1.8m = 1.593) on the front of the face (z forward).
            float w = JawWeightOf(new Vector3(0f, 1.59f, 0.09f));
            Assert.Greater(w, 0.5f, "A mouth-level front vertex must be predominantly jaw-weighted.");
        }

        [Test]
        public void BackOfHeadVertex_SameHeight_HasNoJawWeight()
        {
            float w = JawWeightOf(new Vector3(0f, 1.59f, -0.09f));
            Assert.AreEqual(0f, w, "The skull/back of the head must stay rigid on Body.");
        }

        [Test]
        public void NeckVertex_BelowChin_HasNoJawWeight()
        {
            float w = JawWeightOf(new Vector3(0f, 1.5f, 0.09f)); // ~83% height: below the neck fade
            Assert.AreEqual(0f, w, "Neck/chest vertices must not swing with the jaw.");
        }

        [Test]
        public void EarLineVertex_AtHingeHeight_HasNoJawWeight()
        {
            // Just under the hinge (92% = 1.656): weight ramps in from zero at the hinge line.
            float w = JawWeightOf(new Vector3(0f, 1.652f, 0.09f));
            Assert.Less(w, 0.1f, "Vertices at the hinge line must barely move so the seam stays smooth.");
        }

        [Test]
        public void JawVertexWeights_SumToOne_AndRemainderIsBody()
        {
            BoneWeight w = NpcRigSolver.SolveVertex(new Vector3(0f, 1.59f, 0.09f), Layout());
            float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
            Assert.AreEqual(1f, sum, 1e-4f);
            bool bodyCarriesRest =
                (w.boneIndex0 == NpcRigSolver.BoneBody && w.weight0 > 0f) ||
                (w.boneIndex1 == NpcRigSolver.BoneBody && w.weight1 > 0f) ||
                (w.boneIndex2 == NpcRigSolver.BoneBody && w.weight2 > 0f);
            Assert.IsTrue(bodyCarriesRest, "The non-jaw share of a chin vertex must blend back to Body.");
        }

        [Test]
        public void JawJoint_SitsAtHingeHeight_OnDepthCenter()
        {
            NpcRigLayout l = Layout();
            Assert.AreEqual(1.8f * 0.92f, l.JawJoint.y, 1e-4f);
            Assert.AreEqual(0f, l.JawJoint.z, 1e-4f);
        }

        [Test]
        public void TorsoAndLegVertices_AreUnaffectedByJawExtension()
        {
            // Chest vertex: full Body. Leg vertex: leg-dominant. (Regression guard: the jaw branch
            // must not disturb the existing classification.)
            BoneWeight chest = NpcRigSolver.SolveVertex(new Vector3(0f, 1.2f, 0.09f), Layout());
            Assert.AreEqual(NpcRigSolver.BoneBody, chest.boneIndex0);
            Assert.AreEqual(1f, chest.weight0, 1e-4f);

            BoneWeight shin = NpcRigSolver.SolveVertex(new Vector3(0.1f, 0.3f, 0f), Layout());
            Assert.IsTrue(shin.boneIndex0 == NpcRigSolver.BoneLegR || shin.boneIndex0 == NpcRigSolver.BoneLegL);
        }
    }
}
