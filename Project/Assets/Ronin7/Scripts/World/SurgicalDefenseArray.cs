using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Vault mechanic: an automated surgical defense with precision-blade arrays that SWEEP on a
    /// timed cycle. While a sweep is ACTIVE, the blades are deadly and damage the player on contact;
    /// between sweeps they are safe. Each active sweep can damage the player at most once — the player
    /// must time movement through the gaps.
    ///
    /// Requires a trigger collider on the same GameObject (like ShockMine). Call <see cref="Disable()"/>
    /// to permanently shut an array down (no more sweeps, collider off).
    ///
    /// Set <see cref="autoSweep"/> false to freeze the cycle and drive state manually (deterministic tests).
    /// </summary>
    public class SurgicalDefenseArray : MonoBehaviour
    {
        [Header("Damage")]
        [SerializeField] private float damage = 12f;

        [Header("Sweep Cycle")]
        [SerializeField] private float sweepInterval = 2.5f;
        [SerializeField] private float activeDuration = 0.8f;

        [Header("Visuals")]
        [SerializeField] private GameObject bladeVisual;

        [Tooltip("When false, the sweep cycle pauses so tests can drive Tick manually.")]
        [SerializeField] private bool autoSweep = true;

        private float clock;
        private bool active;
        private bool disabled;
        private bool damagedThisSweep;

        public bool IsActive => active && !disabled;
        public bool IsDisabled => disabled;

        private void Update()
        {
            if (!autoSweep || disabled) return;

            Tick(Time.deltaTime);
        }

        /// <summary>Advance the sweep cycle by dt and update blade state.</summary>
        public void Tick(float dt)
        {
            if (disabled) return;

            clock += dt;

            // Guard divide by zero.
            if (sweepInterval > 0f)
                clock %= sweepInterval;

            bool nowActive = clock < activeDuration;

            // Transition from active → inactive: reset one-shot damage flag.
            if (active && !nowActive)
                damagedThisSweep = false;

            active = nowActive;

            // Sync blade visual with active state.
            if (bladeVisual != null)
                bladeVisual.SetActive(IsActive);
        }

        /// <summary>Permanently disable this array. No more sweeps, collider off, blade visual hidden.</summary>
        public void Disable()
        {
            disabled = true;
            active = false;

            if (bladeVisual != null)
                bladeVisual.SetActive(false);

            var c = GetComponent<Collider>();
            if (c != null)
                c.enabled = false;
        }

        /// <summary>Attempt to damage the player via collider contact. Returns true if damage was applied.</summary>
        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other);
        }

        /// <summary>Attempt to damage the player via collider overlap. Returns true if damage was applied.</summary>
        private void OnTriggerStay(Collider other)
        {
            TryDamage(other);
        }

        /// <summary>Attempt to damage via collider contact. Gated by active state and one-shot per sweep.</summary>
        public bool TryDamage(Collider other)
        {
            Health health = other.GetComponentInParent<Health>();
            if (health == null) return false;

            return TryDamageHealth(health);
        }

        /// <summary>Attempt to damage a Health directly. Gated by active state, disabled state, and one-shot per sweep.</summary>
        public bool TryDamageHealth(Health health)
        {
            if (disabled || !active || damagedThisSweep)
                return false;

            if (health == null)
                return false;

            // Only damage the player (identified by CharacterController component).
            if (health.GetComponent<CharacterController>() == null)
                return false;

            // Apply damage.
            Vector3 hitPoint = transform.position;
            Vector3 direction = Vector3.up;
            health.ApplyDamage(new DamageInfo(damage, hitPoint, direction, gameObject));

            damagedThisSweep = true;
            return true;
        }
    }
}
