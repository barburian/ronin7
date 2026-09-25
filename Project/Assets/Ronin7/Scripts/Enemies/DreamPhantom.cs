using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Dream enemy: a phantom that cycles between Solid (vulnerable, real) and Phased
    /// (invulnerable, attacks pass through). When phased, incoming damage is fully refunded
    /// via Health.Heal(). The phantom can also be marked as purely illusory (a copy/hallucination).
    /// On death, the phantom dissolves (becomes inactive).
    /// </summary>
    public class DreamPhantom : MonoBehaviour
    {
        public enum Phase { Solid, Phased }

        [SerializeField] private float phaseInterval = 2f;
        [SerializeField] private bool illusory = false;
        [Tooltip("When true, Tick(dt) toggles phase every phaseInterval seconds. Disable for deterministic tests.")]
        [SerializeField] private bool autoCycle = true;

        public UnityEvent onDissolved = new UnityEvent();

        private Health health;
        private Phase currentPhase = Phase.Solid;
        private float phaseTimer;
        private bool dissolved;

        /// <summary>Current phase: Solid (vulnerable) or Phased (invulnerable).</summary>
        public Phase CurrentPhase => currentPhase;

        /// <summary>Convenience: true if currently in Phased phase.</summary>
        public bool IsPhased => currentPhase == Phase.Phased;

        /// <summary>True if this phantom represents a pure illusion/copy.</summary>
        public bool Illusory => illusory;

        /// <summary>Public so builders can AddListener after a runtime AddComponent.</summary>
        public UnityEvent OnDissolved => onDissolved;

        private void OnEnable()
        {
            health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
                health.Died += OnDied;
            }

            currentPhase = Phase.Solid;
            phaseTimer = 0f;
            dissolved = false;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }
        }

        private void Update()
        {
            if (autoCycle)
            {
                Tick(Time.deltaTime);
            }
        }

        /// <summary>
        /// Advance the phase cycle: accumulate dt toward the next phase toggle.
        /// Every phaseInterval seconds, toggle between Solid and Phased.
        /// </summary>
        public void Tick(float dt)
        {
            if (phaseInterval <= 0f) return;

            phaseTimer += dt;
            while (phaseTimer >= phaseInterval)
            {
                phaseTimer -= phaseInterval;
                TogglePhase();
            }
        }

        /// <summary>Toggle CurrentPhase between Solid and Phased.</summary>
        private void TogglePhase()
        {
            currentPhase = currentPhase == Phase.Solid ? Phase.Phased : Phase.Solid;
        }

        /// <summary>Set the current phase (for testing when autoCycle is off).</summary>
        public void SetPhase(Phase p)
        {
            currentPhase = p;
        }

        /// <summary>Set whether this phantom is illusory (for testing).</summary>
        public void SetIllusory(bool v)
        {
            illusory = v;
        }

        /// <summary>
        /// Handle incoming damage. If Phased, fully refund the damage so the attack
        /// "passes through" (phantom cannot die while phased). If Solid, damage stands.
        /// </summary>
        private void OnDamaged(DamageInfo info)
        {
            if (IsPhased)
            {
                // Refund the full damage: attack passes through while phased
                health.Heal(info.Amount);
            }
            // If Solid, damage is not refunded (it stands)
        }

        /// <summary>
        /// Handle the phantom's death: dissolve it into inactivity.
        /// </summary>
        private void OnDied()
        {
            Dissolve();
        }

        /// <summary>
        /// Dissolve the phantom: idempotent, marks as dissolved, invokes onDissolved,
        /// and deactivates the GameObject. Safe to call multiple times.
        /// </summary>
        public void Dissolve()
        {
            if (dissolved) return;

            dissolved = true;
            onDissolved?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
