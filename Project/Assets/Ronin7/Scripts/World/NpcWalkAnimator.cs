using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Procedural walk cycle for auto-rigged NPCs: swings the Rig_LegL/R and Rig_ArmL/R bones
    /// (baked into the character prefabs by the NpcAutoRigger editor tool) in opposite phase while
    /// the NPC is moving, plus a subtle torso bob, and eases everything back to the rest pose when
    /// it stops. Movement is self-measured from the transform's own position delta, so it works no
    /// matter who moves the NPC (NpcWander, StoryNpcWander, NpcWalker, or any scripted beat) — the
    /// movement scripts call <see cref="EnsureOn"/> so existing scenes pick this up without a
    /// rebuild. Disables itself when the character has no rig (e.g. placeholder greybox blobs),
    /// which safely degrades to the old rigid slide. Pure per-frame math on five transforms —
    /// no allocations, Quest-friendly.
    /// </summary>
    public class NpcWalkAnimator : MonoBehaviour
    {
        public const string RootBoneName = "Rig_Root";
        public const string BodyBoneName = "Rig_Body";
        public const string LegLBoneName = "Rig_LegL";
        public const string LegRBoneName = "Rig_LegR";
        public const string ArmLBoneName = "Rig_ArmL";
        public const string ArmRBoneName = "Rig_ArmR";

        [Tooltip("Speed (m/s) at which the swing reaches full amplitude.")]
        [SerializeField] private float fullSwingSpeed = 1.2f;

        [Tooltip("Length of one step in meters; sets stride frequency from measured speed.")]
        [SerializeField] private float strideLength = 0.65f;

        [SerializeField] private float legSwingDegrees = 32f;
        [SerializeField] private float armSwingDegrees = 18f;

        [Tooltip("Torso bob height in meters at full walk speed.")]
        [SerializeField] private float bobHeight = 0.02f;

        private Transform body, legL, legR, armL, armR;
        private Quaternion legLRest, legRRest, armLRest, armRRest;
        private Vector3 bodyRestPos;
        private Vector3 lastPos;
        private float phase;
        private float smoothedSpeed;
        private float amplitude;

        /// <summary>Adds (or finds) a walk animator on the given NPC root.</summary>
        public static NpcWalkAnimator EnsureOn(GameObject npc)
        {
            var anim = npc.GetComponent<NpcWalkAnimator>();
            return anim != null ? anim : npc.AddComponent<NpcWalkAnimator>();
        }

        private void Awake()
        {
            body = FindDeep(transform, BodyBoneName);
            legL = FindDeep(transform, LegLBoneName);
            legR = FindDeep(transform, LegRBoneName);
            armL = FindDeep(transform, ArmLBoneName);
            armR = FindDeep(transform, ArmRBoneName);

            if (legL == null || legR == null)
            {
                enabled = false; // unrigged character (placeholder/non-humanoid): keep the old behavior
                return;
            }

            legLRest = legL.localRotation;
            legRRest = legR.localRotation;
            if (armL != null) armLRest = armL.localRotation;
            if (armR != null) armRRest = armR.localRotation;
            if (body != null) bodyRestPos = body.localPosition;
            lastPos = transform.position;
        }

        private void OnEnable()
        {
            lastPos = transform.position;
            smoothedSpeed = 0f;
        }

        // LateUpdate so the coroutine-driven movement scripts have already moved us this frame.
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos;
            lastPos = pos;
            delta.y = 0f;

            // EMA smoothing so a single stalled/teleported frame doesn't pop the pose.
            float speed = delta.magnitude / dt;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-8f * dt));

            float targetAmp = Mathf.Clamp01(smoothedSpeed / fullSwingSpeed);
            amplitude = Mathf.MoveTowards(amplitude, targetAmp, 5f * dt);

            if (amplitude > 0.001f)
            {
                // One full cycle (two steps) per 2*strideLength travelled.
                phase += Mathf.PI * (smoothedSpeed / strideLength) * dt;
            }
            else
            {
                phase = 0f; // settled: snap the cycle so the rest pose is exact
            }

            float swing = Mathf.Sin(phase) * amplitude;

            legL.localRotation = legLRest * Quaternion.AngleAxis(swing * legSwingDegrees, Vector3.right);
            legR.localRotation = legRRest * Quaternion.AngleAxis(-swing * legSwingDegrees, Vector3.right);
            if (armL != null)
                armL.localRotation = armLRest * Quaternion.AngleAxis(-swing * armSwingDegrees, Vector3.right);
            if (armR != null)
                armR.localRotation = armRRest * Quaternion.AngleAxis(swing * armSwingDegrees, Vector3.right);
            if (body != null)
            {
                // Two bobs per cycle (one per step), rising as the legs pass each other.
                float bob = bobHeight * amplitude * 0.5f * (1f - Mathf.Cos(2f * phase));
                body.localPosition = bodyRestPos + Vector3.up * bob;
            }
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
