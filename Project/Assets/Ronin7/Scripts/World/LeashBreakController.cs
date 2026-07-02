using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// EP29 "leash break" mechanic. Models Samurai-4's neural conditioning breaking as the player
    /// forces her to read evidence on a neural-scan console. Her conviction meter (a [0,1] value)
    /// drains both passively (via convictionDrainPerSecond) and actively (via ReadEvidence() calls).
    /// When conviction falls to or below breakThreshold, her leash breaks and she turns ally —
    /// fires a one-shot onLeashBreak event.
    /// Mirrors the shape of <see cref="FailsafeErosionPulse"/> (a meter state with Tick(dt),
    /// public state, one-shot UnityEvents, and an autoAdvance toggle for deterministic tests).
    /// </summary>
    public class LeashBreakController : MonoBehaviour
    {
        [SerializeField] private float startingConviction = 1f;
        [SerializeField] [Range(0f, 1f)] private float breakThreshold = 0.25f;
        [SerializeField] private float convictionDrainPerSecond = 0f;
        [SerializeField] private float evidenceDrainAmount = 0.34f;
        [SerializeField] private bool autoAdvance = true;

        public UnityEvent onLeashBreak = new UnityEvent();
        public UnityEvent onConvictionChanged = new UnityEvent();

        private float conviction = 1f;
        private bool isBroken = false;

        /// <summary>Current conviction meter, clamped to [0,1].</summary>
        public float Conviction => conviction;

        /// <summary>True once the leash has broken and the one-shot onLeashBreak event has fired.</summary>
        public bool IsBroken => isBroken;

        /// <summary>When true, Update() advances the conviction drain automatically. Exposed for tests.</summary>
        public bool AutoAdvance { get => autoAdvance; set => autoAdvance = value; }

        private void Awake()
        {
            conviction = startingConviction;
        }

        private void Update()
        {
            if (!autoAdvance) return;
            Tick(Time.deltaTime);
        }

        /// <summary>Apply evidence drain (triggered by console reads) and check for break threshold.</summary>
        public void ReadEvidence()
        {
            conviction -= evidenceDrainAmount;
            ClampAndCheckBreak();
            onConvictionChanged?.Invoke();
        }

        /// <summary>Advance the conviction drain via passive time; subtracts convictionDrainPerSecond * dt and checks for break threshold.</summary>
        public void Tick(float dt)
        {
            if (convictionDrainPerSecond == 0f) return;
            conviction -= convictionDrainPerSecond * dt;
            ClampAndCheckBreak();
            onConvictionChanged?.Invoke();
        }

        /// <summary>Clamp conviction to [0,1] and fire onLeashBreak once if conviction crosses or equals the break threshold.</summary>
        private void ClampAndCheckBreak()
        {
            conviction = Mathf.Clamp01(conviction);
            if (!isBroken && conviction <= breakThreshold)
            {
                isBroken = true;
                onLeashBreak?.Invoke();
            }
        }

        /// <summary>Reset the leash to unbroken and restore conviction to starting value.</summary>
        public void Clear()
        {
            conviction = startingConviction;
            isBroken = false;
        }
    }
}
