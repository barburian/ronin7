using UnityEngine;
using Ronin7.World.Story;

namespace Ronin7.World
{
    /// <summary>
    /// Pure amplitude-to-nod math for <see cref="NpcTalkAnimator"/>: how strongly a talking NPC's
    /// head should nod given a sampled RMS amplitude, plus the smoothing that turns per-frame
    /// amplitude jitter into a readable micro-motion instead of a jittery snap.
    /// </summary>
    public static class NpcTalkSolver
    {
        /// <summary>Maps a raw RMS sample to a 0..1 talk weight: silence at/below <paramref name="noiseFloor"/>
        /// reads as zero, ramping linearly to 1 at <paramref name="saturateRms"/> (clamped beyond).</summary>
        public static float ComputeTalkWeight(float rms, float noiseFloor, float saturateRms)
        {
            if (saturateRms <= noiseFloor)
            {
                return rms > noiseFloor ? 1f : 0f;
            }
            float t = (rms - noiseFloor) / (saturateRms - noiseFloor);
            return Mathf.Clamp01(t);
        }

        /// <summary>Eases the currently-displayed weight toward <paramref name="targetWeight"/> at a
        /// fixed max rate per second.</summary>
        public static float SmoothTalkWeight(float current, float targetWeight, float deltaTime, float speedPerSecond)
        {
            return Mathf.MoveTowards(current, targetWeight, speedPerSecond * deltaTime);
        }
    }

    /// <summary>
    /// Drives a subtle amplitude-driven "talking" nod on the Rig_Body bone (baked in by the
    /// NpcAutoRigger, see <see cref="NpcWalkAnimator"/>) while this NPC's <see cref="StoryNpc.Dialogue"/>
    /// line audio is playing. Tripo characters have no jaw bone or blend shapes — the whole head is
    /// rigidly weighted to Rig_Body (see NpcRigSolver) — so a small pitch nod reads as talking without
    /// a literal jaw. Rotation-only: NpcWalkAnimator exclusively owns Rig_Body's localPosition (torso
    /// bob) every LateUpdate, so this component never touches position and the two can't fight over a
    /// channel. Disables itself when the character has no rig. Zero per-frame allocations: the RMS
    /// sample buffer is allocated once in Awake and reused.
    /// </summary>
    public class NpcTalkAnimator : MonoBehaviour
    {
        private const string BodyBoneName = "Rig_Body";
        private const int SampleCount = 256;

        [Tooltip("Max pitch nod, in degrees, at full talk amplitude.")]
        [SerializeField] private float maxPitchDegrees = 3.5f;
        [Tooltip("RMS amplitude below which audio reads as silence (no nod).")]
        [SerializeField] private float noiseFloor = 0.01f;
        [Tooltip("RMS amplitude at which the nod reaches full amplitude.")]
        [SerializeField] private float saturateRms = 0.15f;
        [Tooltip("Max weight change per second; smooths amplitude jitter into a readable motion.")]
        [SerializeField] private float smoothSpeed = 6f;

        private Transform body;
        private Quaternion bodyRestRotation;
        private StoryNpc storyNpc;
        private float[] sampleBuffer;
        private float weight;
        private bool driving;

        /// <summary>Adds (or finds) a talk animator on the given NPC root.</summary>
        public static NpcTalkAnimator EnsureOn(GameObject npc)
        {
            var anim = npc.GetComponent<NpcTalkAnimator>();
            return anim != null ? anim : npc.AddComponent<NpcTalkAnimator>();
        }

        private void Awake()
        {
            body = FindDeep(transform, BodyBoneName);
            if (body == null)
            {
                enabled = false; // unrigged character (placeholder/non-humanoid): nothing to nod
                return;
            }

            storyNpc = GetComponent<StoryNpc>();
            sampleBuffer = new float[SampleCount];
        }

        // LateUpdate so it runs after any movement/animation this frame, matching NpcWalkAnimator.
        private void LateUpdate()
        {
            var dialogue = storyNpc != null ? storyNpc.Dialogue : null;
            var audioSource = dialogue != null ? dialogue.AudioSource : null;
            bool speaking = dialogue != null && dialogue.IsPlaying && audioSource != null && audioSource.isPlaying;

            float targetWeight = 0f;
            if (speaking)
            {
                audioSource.GetOutputData(sampleBuffer, 0);
                float sumSq = 0f;
                for (int i = 0; i < sampleBuffer.Length; i++)
                {
                    float s = sampleBuffer[i];
                    sumSq += s * s;
                }
                float rms = Mathf.Sqrt(sumSq / sampleBuffer.Length);
                targetWeight = NpcTalkSolver.ComputeTalkWeight(rms, noiseFloor, saturateRms);
            }

            weight = NpcTalkSolver.SmoothTalkWeight(weight, targetWeight, Time.deltaTime, smoothSpeed);

            // Only own the rotation channel while actually nodding: capture the pose at talk start
            // (it may include a NpcGazeGlance yaw), restore it once when the nod decays, and stay
            // hands-off while idle so glances aren't masked between conversations.
            if (weight > 0.0005f)
            {
                if (!driving)
                {
                    bodyRestRotation = body.localRotation;
                    driving = true;
                }
                body.localRotation = bodyRestRotation * Quaternion.AngleAxis(weight * maxPitchDegrees, Vector3.right);
            }
            else if (driving)
            {
                body.localRotation = bodyRestRotation;
                driving = false;
            }
        }

        // Same recursive-search idiom as NpcWalkAnimator.FindDeep, duplicated privately so this
        // component has no dependency on it.
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
