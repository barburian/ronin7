using UnityEngine;

namespace Ronin7.Audio
{
    /// <summary>
    /// Pure footstep-cadence math for <see cref="NpcFootstepCadence"/>: advances a phase accumulator
    /// at a rate derived from the NPC's own measured speed and stride length, and reports each time
    /// the phase crosses a foot-plant boundary (every half-cycle, i.e. every multiple of PI).
    /// </summary>
    public static class NpcFootstepSolver
    {
        public static float AdvancePhase(float phase, float smoothedSpeed, float strideLength, float dt) =>
            (smoothedSpeed <= 0f || strideLength <= 0f) ? phase : phase + Mathf.PI * (smoothedSpeed / strideLength) * dt;

        public static bool CrossedFootPlant(float oldPhase, float newPhase) =>
            Mathf.Floor(newPhase / Mathf.PI) > Mathf.Floor(oldPhase / Mathf.PI);
    }

    /// <summary>
    /// Self-measured ambient footstep audio for NPCs: derives speed from the NPC's own transform
    /// (same EMA-smoothing technique as <see cref="Ronin7.World.NpcWalkAnimator"/>, so it works no
    /// matter which script moves the NPC), advances a stride-length-driven phase, and fires a pooled
    /// one-shot each time a foot plants. Owns a private <see cref="OneShotPool"/> child so it never
    /// competes with other one-shot sources for pool slots. Zero per-frame allocations.
    /// </summary>
    public class NpcFootstepCadence : MonoBehaviour
    {
        private const float MinAudibleSpeed = 0.05f;

        [Tooltip("Length of one step in meters; matches NpcWalkAnimator's default so footfalls land on the visual stride.")]
        [SerializeField] private float strideLength = 0.65f;

        [SerializeField] private float volume = 0.5f;
        [SerializeField] private AudioClip[] footstepClips;

        private OneShotPool pool;
        private Vector3 lastPos;
        private float smoothedSpeed;
        private float phase;
        private int nextClipIndex;

        private void Awake()
        {
            var poolGo = new GameObject("FootstepPool");
            poolGo.transform.SetParent(transform, false);
            pool = poolGo.AddComponent<OneShotPool>();
            lastPos = transform.position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos;
            lastPos = pos;
            delta.y = 0f;

            // EMA smoothing so a single stalled/teleported frame doesn't spike the cadence.
            float speed = delta.magnitude / dt;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-8f * dt));

            float oldPhase = phase;
            phase = NpcFootstepSolver.AdvancePhase(phase, smoothedSpeed, strideLength, dt);

            if (smoothedSpeed > MinAudibleSpeed && NpcFootstepSolver.CrossedFootPlant(oldPhase, phase))
                PlayFootstep();
        }

        private void PlayFootstep()
        {
            if (footstepClips == null || footstepClips.Length == 0) return; // OneShotPool also no-ops null clips

            AudioClip clip = footstepClips[nextClipIndex];
            nextClipIndex = (nextClipIndex + 1) % footstepClips.Length;
            pool.Play(clip, transform.position, volume);
        }
    }
}
