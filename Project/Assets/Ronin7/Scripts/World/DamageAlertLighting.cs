using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// Warning light that pulses from a base color/intensity to an alert color/intensity when its
    /// watched entity takes damage, then decays smoothly back. Listens to <see cref="EntityDamaged"/>
    /// only — Ronin7.World cannot reference Ronin7.Ship (would create a circular asmdef reference),
    /// but Health publishes EntityDamaged for every damageable entity including the player's ship, so
    /// no damage source is missed. Comfort: the envelope always ramps smoothly, never snaps or
    /// strobes, so it reads as a warning light rather than an epilepsy risk.
    ///
    /// Deliberately not gated by <see cref="Ronin7.Core.LightBudget"/> — damage alerts are gameplay
    /// feedback and must animate on every tier.
    /// </summary>
    public class DamageAlertLighting : MonoBehaviour
    {
        /// <summary>
        /// 0..1 pulse envelope at <paramref name="elapsed"/> seconds into an attack/hold/decay cycle:
        /// ramps 0→1 over <paramref name="attackTime"/>, holds at 1 for <paramref name="holdTime"/>,
        /// then ramps 1→0 over <paramref name="decayTime"/>. Negative phase lengths are treated as
        /// zero (never divides by zero or throws); negative elapsed or elapsed past the total duration
        /// both return 0.
        /// </summary>
        public static float DecayEnvelope(float elapsed, float attackTime, float holdTime, float decayTime)
        {
            attackTime = Mathf.Max(0f, attackTime);
            holdTime = Mathf.Max(0f, holdTime);
            decayTime = Mathf.Max(0f, decayTime);

            if (elapsed < 0f) return 0f;

            if (elapsed < attackTime)
                return attackTime > 0f ? elapsed / attackTime : 1f;

            float holdEnd = attackTime + holdTime;
            if (elapsed < holdEnd) return 1f;

            float decayEnd = holdEnd + decayTime;
            if (elapsed < decayEnd)
                return decayTime > 0f ? 1f - (elapsed - holdEnd) / decayTime : 0f;

            return 0f;
        }

        [SerializeField] private Light targetLight;
        [Tooltip("Only react to damage on this entity. Leave unassigned to react to any entity's EntityDamaged.")]
        [SerializeField] private GameObject watchTarget;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Color alertColor = Color.red;
        [SerializeField] private float baseIntensity = 1f;
        [SerializeField] private float alertIntensity = 4f;
        [SerializeField] private float attackTime = 0.1f;
        [SerializeField] private float holdTime = 0.25f;
        [SerializeField] private float decayTime = 1.5f;

        private bool isPulsing;
        private float pulseStartTime;
        // Snapshot of "was a watchTarget assigned at all", taken once in OnEnable. Lets the handler
        // tell "never wired up (react to any entity)" apart from "was wired up, but the subject has
        // since been destroyed" (go silent) instead of the destroyed reference silently reading as
        // unassigned and reacting to everything again.
        private bool hasWatchTarget;

        private void OnEnable()
        {
            hasWatchTarget = watchTarget != null;
            EventBus.Subscribe<EntityDamaged>(OnEntityDamaged);
        }

        private void OnDisable() => EventBus.Unsubscribe<EntityDamaged>(OnEntityDamaged);

        private void OnEntityDamaged(EntityDamaged evt)
        {
            // Destroyed subject => stay silent. Never assigned => original room-wide behavior.
            if (hasWatchTarget && (watchTarget == null || evt.Entity != watchTarget)) return;
            pulseStartTime = Time.unscaledTime;
            isPulsing = true;
        }

        private void Update()
        {
            if (!isPulsing || targetLight == null) return;

            float elapsed = Time.unscaledTime - pulseStartTime;
            float total = Mathf.Max(0f, attackTime) + Mathf.Max(0f, holdTime) + Mathf.Max(0f, decayTime);
            if (elapsed >= total)
            {
                targetLight.color = baseColor;
                targetLight.intensity = baseIntensity;
                isPulsing = false;
                return;
            }

            float t = DecayEnvelope(elapsed, attackTime, holdTime, decayTime);
            targetLight.color = Color.Lerp(baseColor, alertColor, t);
            targetLight.intensity = Mathf.Lerp(baseIntensity, alertIntensity, t);
        }
    }
}
