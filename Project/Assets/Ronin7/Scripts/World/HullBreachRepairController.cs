using Ronin7.Combat;
using Ronin7.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// "hull breach repair under fire" mechanic: manages a set of hull breach points
    /// that the player must seal by holding interaction (grip) on each one. Each unsealed
    /// breach pulses damage at regular intervals until sealed. Once sealed, a breach is safe.
    /// </summary>
    public class HullBreachRepairController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The player ship Health to damage on breach timeout.")]
        [SerializeField] private Health playerHealth;

        [Header("Breach Config")]
        [Tooltip("Seconds of continuous grip required to seal one breach.")]
        [SerializeField] private float secondsToSeal = 3f;
        [Tooltip("Damage per breach timeout pulse.")]
        [SerializeField] private float damagePerBreach = 8f;
        [Tooltip("Seconds between each damage pulse from an unsealed breach.")]
        [SerializeField] private float breachInterval = 5f;

        [SerializeField] private List<Breach> breaches = new List<Breach>();

        /// <summary>Number of registered breaches.</summary>
        public int BreachCount => breaches.Count;

        /// <summary>Number of sealed breaches.</summary>
        public int SealedCount
        {
            get
            {
                int count = 0;
                foreach (var b in breaches)
                    if (b.isSealed) count++;
                return count;
            }
        }

        /// <summary>True when all breaches are sealed.</summary>
        public bool AllSealed
        {
            get
            {
                if (breaches.Count == 0) return true;
                foreach (var b in breaches)
                    if (!b.isSealed) return false;
                return true;
            }
        }

        /// <summary>Public accessor for config (seconds to seal).</summary>
        public float SecondsToSeal => secondsToSeal;

        /// <summary>Public accessor for config (damage per breach).</summary>
        public float DamagePerBreach => damagePerBreach;

        /// <summary>Editor/runtime wiring entry point.</summary>
        public void Configure(Health player)
        {
            playerHealth = player;
        }

        /// <summary>
        /// Register a breach by stable string id. Returns the breach index (0-based).
        /// Idempotent: safe to call multiple times, but each call adds a new breach.
        /// </summary>
        public int AddBreach(string id)
        {
            breaches.Add(new Breach { id = id, holdProgress = 0f, isSealed = false, lastPulseTime = 0f });
            return breaches.Count - 1;
        }

        /// <summary>
        /// Add seal-progress (in seconds) to a breach by index. Returns true if the breach
        /// is now sealed. If already sealed, returns true and does nothing.
        /// </summary>
        public bool AddSealProgress(int breachIndex, float dt)
        {
            if (breachIndex < 0 || breachIndex >= breaches.Count)
                return false;

            Breach b = breaches[breachIndex];
            if (b.isSealed)
                return true;

            b.holdProgress += dt;
            if (b.holdProgress >= secondsToSeal)
            {
                b.isSealed = true;
            }

            breaches[breachIndex] = b;
            return b.isSealed;
        }

        /// <summary>
        /// Pure tick: for each unsealed breach, apply damage if due (now >= lastPulseTime + breachInterval).
        /// Returns the number of damage pulses applied. Safe if playerHealth is null or dead (applies nothing).
        /// </summary>
        public int Tick(float now)
        {
            if (playerHealth == null || !playerHealth.IsAlive)
                return 0;

            int pulsesApplied = 0;

            for (int i = 0; i < breaches.Count; i++)
            {
                Breach b = breaches[i];
                if (b.isSealed)
                    continue;

                if (now >= b.lastPulseTime + breachInterval)
                {
                    playerHealth.ApplyDamage(new DamageInfo(damagePerBreach, transform.position, Vector3.up, gameObject));
                    b.lastPulseTime = now;
                    breaches[i] = b;
                    pulsesApplied++;
                }
            }

            return pulsesApplied;
        }

        private void Start()
        {
            ResetPulseClock(Time.time);
        }

        /// <summary>
        /// Rebase every breach's pulse clock to <paramref name="now"/>. Without this, a breach added
        /// with lastPulseTime left at its 0f default fires an instant damage pulse the moment
        /// Tick() is next called with a large "now" — e.g. a controller enabled minutes into a scene.
        /// Called from Start() so authored breaches always get a fresh breachInterval grace window
        /// from activation instead of inheriting a stale absolute timestamp.
        /// </summary>
        public void ResetPulseClock(float now)
        {
            for (int i = 0; i < breaches.Count; i++)
            {
                Breach b = breaches[i];
                b.lastPulseTime = now;
                breaches[i] = b;
            }
        }

        private void Update()
        {
            if (!AllSealed)
                Tick(Time.time);
        }

        /// <summary>Private inner class representing one hull breach.</summary>
        [System.Serializable]
        private struct Breach
        {
            public string id;
            public float holdProgress;
            public bool isSealed;
            public float lastPulseTime;
        }
    }
}
