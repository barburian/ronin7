using System.Runtime.CompilerServices;
using Ronin7.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the blade collider (a trigger). Samples its own speed and, on contact with a
    /// <see cref="Health"/>, deals damage scaled by swing speed. Ignores the wielder so you
    /// can't cut yourself, and rate-limits hits so one swing lands once.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BladeDamager : MonoBehaviour
    {
        [Tooltip("Optional explicit definition; otherwise taken from a Sword on a parent.")]
        [SerializeField] private WeaponDefinition definitionOverride;

        [Tooltip("Exponential smoothing on the measured blade speed (0 = raw/spike-prone, 1 = frozen). " +
                 "Rejects single-frame tracking spikes so a stutter can't inflate damage.")]
        [SerializeField, Range(0f, 1f)] private float speedSmoothing = 0.5f;

        private WeaponDefinition definition;
        private Vector3 lastPos;
        private float speed;
        private float lastHitTime = -999f;
        private PlayerCombatModifiers wielderMods;

        /// <summary>Current blade speed in m/s (world), exponentially smoothed.</summary>
        public float Speed => speed;

        private void Awake()
        {
            definition = definitionOverride != null
                ? definitionOverride
                : GetComponentInParent<Sword>()?.Definition;
            lastPos = transform.position;
            // NOTE: the wielder's PlayerCombatModifiers is resolved lazily in OnTriggerEnter, not here.
            // At Awake the sword is an unparented world Grabbable (transform.root == the sword itself),
            // so there is no rig above it to find yet; it is only reparented under the hand on grab.
        }

        private void FixedUpdate()
        {
            speed = SmoothSpeed(lastPos, transform.position, Time.fixedDeltaTime, speed, speedSmoothing);
            lastPos = transform.position;
        }

        /// <summary>
        /// One exponential-moving-average step over the raw frame-to-frame blade speed. Pure and
        /// stateless so it can be unit-tested without the physics loop. A single spurious tracking
        /// spike only contributes (1 - <paramref name="smoothing"/>) to the result, so a stutter
        /// frame can't inflate the speed (and therefore the damage) it feeds.
        /// </summary>
        internal static float SmoothSpeed(Vector3 from, Vector3 to, float dt, float previousSpeed, float smoothing)
        {
            if (dt <= 0f) return previousSpeed;
            float instantaneous = (to - from).magnitude / dt;
            return Mathf.Lerp(instantaneous, previousSpeed, smoothing);
        }

        /// <summary>
        /// Applies the wielder's <see cref="PlayerCombatModifiers.DamageMultiplier"/> to a base damage
        /// amount. Pure/stateless so the multiplier math (e.g. Ch7's weakpoint-sight 2x) is
        /// unit-testable without a physics trigger, mirroring <see cref="SmoothSpeed"/>.
        /// </summary>
        internal static float ApplyWielderMultiplier(float baseDamage, float multiplier) => baseDamage * multiplier;

        private void OnTriggerEnter(Collider other)
        {
            if (definition == null) return;

            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return;

            // Don't damage whoever is holding the sword (sword is parented under them).
            if (((Component)health).transform.root == transform.root) return;

            if (Time.time - lastHitTime < definition.hitCooldown) return;

            float dmg = definition.DamageForSpeed(speed);
            // Resolve the wielder's modifiers at hit time: while held, transform.root is the rig, so its
            // PlayerCombatModifiers (e.g. Ch7 weakpoint-sight) is reachable. Null (unheld / no rig) => 1x.
            if (wielderMods == null) wielderMods = transform.root.GetComponentInParent<PlayerCombatModifiers>();
            dmg = ApplyWielderMultiplier(dmg, wielderMods != null ? wielderMods.DamageMultiplier : 1f);
            if (dmg <= 0f) return;

            lastHitTime = Time.time;
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 dir = (other.transform.position - transform.position).normalized;
            health.ApplyDamage(new DamageInfo(dmg, point, dir, transform.root.gameObject));
            EventBus.Publish(new SwordImpact(point, speed, health.gameObject));
        }
    }
}
