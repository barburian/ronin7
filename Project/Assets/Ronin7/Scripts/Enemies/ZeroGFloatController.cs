using System.Collections.Generic;
using UnityEngine;
using Ronin7.Combat;

namespace Ronin7.Enemies
{
    /// <summary>
    /// EP25 "The Sterile Reckoning" mechanic: drives a GROUP of MeleeAttacker members with a gentle
    /// per-member vertical bob and horizontal sway, as if drifting weightlessly in zero-partial gravity.
    /// Each member floats around its spawn position with an independent phase offset, causing the group
    /// to desynchronize and visually feel like they are not in unison.
    ///
    /// This augment is non-invasive: it only offsets transforms, never touches MeleeAttacker's targeting
    /// or FSM. Dead members (Health.IsAlive == false) are skipped each frame.
    ///
    /// Set <see cref="autoFloat"/> false to freeze motion and drive state manually (deterministic tests).
    /// No per-frame allocation.
    /// </summary>
    public class ZeroGFloatController : MonoBehaviour
    {
        [SerializeField] private List<MeleeAttacker> members = new();

        [Header("Float Motion (vertical bob)")]
        [SerializeField] private float bobAmplitude = 0.5f;
        [SerializeField] private float bobFrequency = 0.6f;

        [Header("Float Motion (horizontal sway)")]
        [SerializeField] private float swayAmplitude = 0.3f;
        [SerializeField] private float swayFrequency = 0.35f;

        [Tooltip("When false, members hold their positions (no float) so tests can drive Tick manually.")]
        [SerializeField] private bool autoFloat = true;

        private Vector3[] baseLocalPos;
        private float[] phase;
        private Health[] healths;
        private float clock;
        private bool initialized;

        public int MemberCount => members != null ? members.Count : 0;
        public float Clock => clock;

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;

            int n = members != null ? members.Count : 0;
            baseLocalPos = new Vector3[n];
            phase = new float[n];
            healths = new Health[n];
            clock = 0f;

            // Deterministic phase offsets: each member gets a unique phase so the group desyncs.
            // Using a prime-based offset ensures the pattern is deterministic across test runs.
            for (int i = 0; i < n; i++)
            {
                var m = members[i];
                if (m != null)
                {
                    baseLocalPos[i] = m.transform.localPosition;
                    phase[i] = i * 1.234567f;  // Deterministic, not random.
                    healths[i] = m.GetComponent<Health>();
                }
                else
                {
                    baseLocalPos[i] = Vector3.zero;
                    phase[i] = 0f;
                }
            }
            initialized = true;
        }

        /// <summary>Add a member at runtime and (re)initialize so the new member is driven. For spawners/tests
        /// (the scene builder wires the serialized <c>members</c> list directly instead).</summary>
        public void RegisterMember(MeleeAttacker m)
        {
            if (m == null) return;
            members ??= new List<MeleeAttacker>();
            members.Add(m);
            initialized = false;
            Initialize();
        }

        private void Update()
        {
            if (!autoFloat || !initialized) return;

            Tick(Time.deltaTime);
        }

        /// <summary>Advance the float animation by dt and apply offsets to all members.</summary>
        public void Tick(float dt)
        {
            if (!initialized) Initialize();

            clock += dt;

            int n = members != null ? members.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var m = members[i];
                if (m == null) continue;

                var h = healths != null ? healths[i] : null;
                if (h != null && !h.IsAlive) continue;

                m.transform.localPosition = baseLocalPos[i] + Offset(i);
            }
        }

        /// <summary>Compute the bob+sway offset for member i at the current clock phase.</summary>
        public Vector3 Offset(int i)
        {
            if (i < 0 || i >= (phase != null ? phase.Length : 0))
                return Vector3.zero;

            float bobPhase = (clock * bobFrequency + phase[i]) * 2f * Mathf.PI;
            float swayPhase = (clock * swayFrequency + phase[i]) * 2f * Mathf.PI;

            return new Vector3(
                Mathf.Sin(swayPhase) * swayAmplitude,
                Mathf.Sin(bobPhase) * bobAmplitude,
                0f
            );
        }
    }
}
