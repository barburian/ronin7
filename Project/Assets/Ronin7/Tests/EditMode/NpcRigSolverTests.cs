using NUnit.Framework;
using Ronin7.World;
using UnityEngine;

namespace Ronin7.Tests.EditMode
{
    /// <summary>
    /// Covers the pure vertex→bone-weight heuristics behind the NPC auto-rig. Uses a synthetic
    /// 1.8m-tall, 0.9m-wide humanoid bounding box (feet at y=0, A-pose-ish arm extents).
    /// </summary>
    public class NpcRigSolverTests
    {
        private static NpcRigLayout Layout()
        {
            // Center x/z at 0, feet at y=0: height 1.8, width 0.9, depth 0.4.
            var bounds = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.8f, 0.4f));
            return NpcRigSolver.SolveLayout(bounds);
        }

        private static float WeightFor(BoneWeight w, int bone)
        {
            float total = 0f;
            if (w.boneIndex0 == bone) total += w.weight0;
            if (w.boneIndex1 == bone) total += w.weight1;
            if (w.boneIndex2 == bone) total += w.weight2;
            if (w.boneIndex3 == bone) total += w.weight3;
            return total;
        }

        private static float TotalWeight(BoneWeight w)
            => w.weight0 + w.weight1 + w.weight2 + w.weight3;

        [Test]
        public void Layout_PutsHipsAndShouldersInsideBounds()
        {
            var l = Layout();
            Assert.Greater(l.hipY, 0.3f * 1.8f);
            Assert.Less(l.hipY, 0.6f * 1.8f);
            Assert.Greater(l.shoulderY, l.hipY);
            Assert.Less(l.shoulderY, 1.8f);
            Assert.Greater(l.torsoHalfWidth, 0f);
            Assert.Less(l.torsoHalfWidth, 0.45f);
        }

        [Test]
        public void FootVertices_GoFullyToTheirSidesLeg()
        {
            var l = Layout();
            var left = NpcRigSolver.SolveVertex(new Vector3(-0.15f, 0.05f, 0f), l);
            var right = NpcRigSolver.SolveVertex(new Vector3(0.15f, 0.05f, 0f), l);

            Assert.Greater(WeightFor(left, NpcRigSolver.BoneLegL), 0.95f);
            Assert.Greater(WeightFor(right, NpcRigSolver.BoneLegR), 0.95f);
        }

        [Test]
        public void HeadVertex_IsFullyBody()
        {
            var l = Layout();
            var head = NpcRigSolver.SolveVertex(new Vector3(0f, 1.7f, 0.05f), l);
            Assert.Greater(WeightFor(head, NpcRigSolver.BoneBody), 0.99f);
        }

        [Test]
        public void OutstretchedHandVertices_GoToTheirSidesArm()
        {
            var l = Layout();
            var leftHand = NpcRigSolver.SolveVertex(new Vector3(-0.44f, 1.2f, 0f), l);
            var rightHand = NpcRigSolver.SolveVertex(new Vector3(0.44f, 1.2f, 0f), l);

            Assert.Greater(WeightFor(leftHand, NpcRigSolver.BoneArmL), 0.9f);
            Assert.Greater(WeightFor(rightHand, NpcRigSolver.BoneArmR), 0.9f);
        }

        [Test]
        public void CenterSeamLowVertex_BlendsBothLegs()
        {
            var l = Layout();
            var seam = NpcRigSolver.SolveVertex(new Vector3(0f, 0.2f, 0f), l);

            float legL = WeightFor(seam, NpcRigSolver.BoneLegL);
            float legR = WeightFor(seam, NpcRigSolver.BoneLegR);
            Assert.Greater(legL, 0.3f);
            Assert.Greater(legR, 0.3f);
            Assert.AreEqual(legL, legR, 0.05f, "center seam should split evenly between legs");
        }

        [Test]
        public void HipLineVertex_BlendsLegIntoBody()
        {
            var l = Layout();
            // Just below the hip line, inside the leg→body blend band.
            var v = NpcRigSolver.SolveVertex(new Vector3(-0.15f, l.hipY - 0.02f, 0f), l);

            Assert.Greater(WeightFor(v, NpcRigSolver.BoneBody), 0.3f);
            Assert.Greater(WeightFor(v, NpcRigSolver.BoneLegL), 0.05f);
        }

        [Test]
        public void AllWeights_AreNormalizedAndSortedDescending()
        {
            var l = Layout();
            var samples = new[]
            {
                new Vector3(-0.15f, 0.05f, 0f),
                new Vector3(0f, 0.2f, 0f),
                new Vector3(0f, 1.7f, 0f),
                new Vector3(0.44f, 1.2f, 0f),
                new Vector3(-0.21f, l.hipY - 0.03f, 0.1f),
                new Vector3(0.05f, l.hipY + 0.01f, -0.1f),
            };

            foreach (var v in samples)
            {
                var w = NpcRigSolver.SolveVertex(v, l);
                Assert.AreEqual(1f, TotalWeight(w), 1e-4f, $"weights must sum to 1 at {v}");
                Assert.GreaterOrEqual(w.weight0, w.weight1, $"weights must be sorted at {v}");
                Assert.GreaterOrEqual(w.weight1, w.weight2, $"weights must be sorted at {v}");
            }
        }

        [Test]
        public void LowOutboardSkirtVertex_StaysWithLegsNotArms()
        {
            var l = Layout();
            // Wide skirt hem: far out on x but near the ground — must not be grabbed by an arm.
            var hem = NpcRigSolver.SolveVertex(new Vector3(0.4f, 0.15f, 0f), l);
            Assert.AreEqual(0f, WeightFor(hem, NpcRigSolver.BoneArmR), 1e-4f);
            Assert.Greater(WeightFor(hem, NpcRigSolver.BoneLegR), 0.9f);
        }
    }
}
