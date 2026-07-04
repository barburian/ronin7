using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Joint layout for the procedural 5-bone NPC walk rig, derived purely from a mesh's
    /// root-space bounds. All positions are in the character root's space (+Y up, feet at minY).
    /// </summary>
    public struct NpcRigLayout
    {
        public float minY;
        public float height;
        public float width;
        /// <summary>Y of the hip line: below it vertices belong to the legs.</summary>
        public float hipY;
        /// <summary>Y of the shoulder line where the arm joints sit.</summary>
        public float shoulderY;
        /// <summary>Half-width of the torso; vertices further out on X belong to the arms.</summary>
        public float torsoHalfWidth;
        /// <summary>Z used for every joint (the body's depth center).</summary>
        public float centerZ;

        public Vector3 BodyJoint => new Vector3(0f, hipY, centerZ);
        public Vector3 HipL => new Vector3(-torsoHalfWidth * 0.5f, hipY, centerZ);
        public Vector3 HipR => new Vector3(torsoHalfWidth * 0.5f, hipY, centerZ);
        public Vector3 ShoulderL => new Vector3(-torsoHalfWidth, shoulderY, centerZ);
        public Vector3 ShoulderR => new Vector3(torsoHalfWidth, shoulderY, centerZ);
    }

    /// <summary>
    /// Pure logic behind the NPC auto-rig: classifies every vertex of a humanoid-ish mesh into
    /// bone weights for [Body, LegL, LegR, ArmL, ArmR] using height/width heuristics with smooth
    /// blend bands, so a single static mesh (the Tripo image→3D characters) can be skinned and
    /// walk-animated without a hand-authored rig. No UnityEditor dependencies so the editor baker
    /// and the EditMode tests can both use it.
    /// </summary>
    public static class NpcRigSolver
    {
        public const int BoneBody = 0;
        public const int BoneLegL = 1;
        public const int BoneLegR = 2;
        public const int BoneArmL = 3;
        public const int BoneArmR = 4;
        public const int BoneCount = 5;

        // Proportion heuristics (fractions of bounds height/width). Tuned for the stylized
        // humanoid cast; characters that deviate (robes, thrones) just end up mostly Body-weighted,
        // which degrades to the old rigid look instead of breaking.
        private const float HipHeightFrac = 0.48f;        // hip line at ~48% of height
        private const float ShoulderHeightFrac = 0.74f;   // shoulder line at ~74% of height
        private const float TorsoHalfWidthFrac = 0.22f;   // torso spans ~±22% of full width
        private const float LegSideBlendFrac = 0.08f;     // L/R blend band around x=0 (of width)
        private const float LegHipBlendFrac = 0.10f;      // leg→body blend band below hip (of height)
        private const float ArmBlendFrac = 0.06f;         // arm→body blend band past torso edge (of width)
        private const float ArmMinHeightFrac = 0.30f;     // below this, outboard verts are still legs/skirt

        public static NpcRigLayout SolveLayout(Bounds rootSpaceBounds)
        {
            var b = rootSpaceBounds;
            return new NpcRigLayout
            {
                minY = b.min.y,
                height = Mathf.Max(b.size.y, 0.0001f),
                width = Mathf.Max(b.size.x, 0.0001f),
                hipY = b.min.y + b.size.y * HipHeightFrac,
                shoulderY = b.min.y + b.size.y * ShoulderHeightFrac,
                torsoHalfWidth = b.size.x * TorsoHalfWidthFrac,
                centerZ = b.center.z,
            };
        }

        /// <summary>Bone weights for a single root-space vertex. Weights sum to 1, sorted descending.</summary>
        public static BoneWeight SolveVertex(Vector3 v, in NpcRigLayout l)
        {
            float armEdge = l.torsoHalfWidth;
            float armBlend = l.width * ArmBlendFrac;
            bool armRegion = Mathf.Abs(v.x) > armEdge && v.y > l.minY + l.height * ArmMinHeightFrac;

            if (armRegion)
            {
                // Fade from body to arm as |x| leaves the torso.
                float w = Mathf.InverseLerp(armEdge, armEdge + armBlend, Mathf.Abs(v.x));
                int bone = v.x < 0f ? BoneArmL : BoneArmR;
                return MakeWeight(bone, w, BoneBody, 1f - w);
            }

            if (v.y < l.hipY)
            {
                // Fade from body (at the hip line) to full leg below the blend band.
                float legW = Mathf.InverseLerp(l.hipY, l.hipY - l.height * LegHipBlendFrac, v.y);
                // Split the leg share between L and R across the center seam so robes/closed
                // stances stretch instead of tearing.
                float band = l.width * LegSideBlendFrac;
                float tR = Mathf.InverseLerp(-band, band, v.x);
                return MakeWeight3(BoneLegL, legW * (1f - tR), BoneLegR, legW * tR, BoneBody, 1f - legW);
            }

            return MakeWeight(BoneBody, 1f, BoneBody, 0f);
        }

        public static BoneWeight[] Solve(IList<Vector3> rootSpaceVerts, NpcRigLayout layout)
        {
            var weights = new BoneWeight[rootSpaceVerts.Count];
            for (int i = 0; i < rootSpaceVerts.Count; i++)
                weights[i] = SolveVertex(rootSpaceVerts[i], layout);
            return weights;
        }

        private static BoneWeight MakeWeight(int boneA, float wA, int boneB, float wB)
            => MakeWeight3(boneA, wA, boneB, wB, BoneBody, 0f);

        private static BoneWeight MakeWeight3(int boneA, float wA, int boneB, float wB, int boneC, float wC)
        {
            // Sort descending (Unity expects boneIndex0 to carry the largest weight) and normalize.
            int b0 = boneA, b1 = boneB, b2 = boneC;
            float w0 = wA, w1 = wB, w2 = wC;
            if (w0 < w1) { (b0, b1) = (b1, b0); (w0, w1) = (w1, w0); }
            if (w1 < w2) { (b1, b2) = (b2, b1); (w1, w2) = (w2, w1); }
            if (w0 < w1) { (b0, b1) = (b1, b0); (w0, w1) = (w1, w0); }

            float sum = w0 + w1 + w2;
            if (sum <= 0f) { b0 = BoneBody; w0 = 1f; w1 = 0f; w2 = 0f; sum = 1f; }

            return new BoneWeight
            {
                boneIndex0 = b0, weight0 = w0 / sum,
                boneIndex1 = b1, weight1 = w1 / sum,
                boneIndex2 = b2, weight2 = w2 / sum,
                boneIndex3 = 0, weight3 = 0f,
            };
        }
    }
}
