using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ronin7.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the blade collider (a trigger). Samples its own speed and, on contact with a
    /// <see cref="Health"/>, deals damage scaled by swing speed. Ignores the wielder so you
    /// can't cut yourself, and rate-limits hits per target so one swing can't multi-hit the
    /// same enemy, while still landing on every other target it touches in that swing.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BladeDamager : MonoBehaviour
    {
        [Tooltip("Optional explicit definition; otherwise taken from a Sword on a parent.")]
        [SerializeField] private WeaponDefinition definitionOverride;

        [Tooltip("Exponential smoothing on the measured blade speed (0 = raw/spike-prone, 1 = frozen). " +
                 "Rejects single-frame tracking spikes so a stutter can't inflate damage.")]
        [SerializeField, Range(0f, 1f)] private float speedSmoothing = 0.5f;

        // Once the per-target debounce map grows past this many entries, opportunistically prune it
        // (see PruneStale) so destroyed/long-cold targets don't retain Health refs for the scene's
        // lifetime. Small enough that a normal swing (a handful of targets) never triggers it.
        private const int PruneCheckThreshold = 16;
        // Entries older than this many cooldowns are considered cold and safe to drop.
        private const float StaleAfterCooldowns = 4f;

        private WeaponDefinition definition;
        private Vector3 lastPos;
        private float speed;
        private readonly Dictionary<Health, float> lastHitTimes = new();
        // Reusable scratch buffer for PruneStale so pruning never allocates.
        private static readonly List<Health> pruneScratch = new();
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
        ///
        /// DEVIATION: public rather than internal (unlike this class's other pure helpers) so
        /// <c>Ronin7.Enemies.MeleeAttacker</c>'s enemy-side swing-speed tracker (G4 Blade Clash) can
        /// reuse the exact same math instead of duplicating it — Enemies cannot see Combat internals.
        /// </summary>
        public static float SmoothSpeed(Vector3 from, Vector3 to, float dt, float previousSpeed, float smoothing)
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

        /// <summary>
        /// Per-target hit debounce: registers a hit for <paramref name="target"/> at <paramref name="now"/>
        /// if its last hit was at least <paramref name="cooldown"/> ago (or never), returning whether the
        /// hit should land. Keyed per target so one swing can hit multiple enemies while still rate-limiting
        /// repeat hits on the same one. Pure/stateless over the passed-in map, mirroring <see cref="SmoothSpeed"/>.
        /// </summary>
        internal static bool TryRegisterHit(Dictionary<Health, float> lastHitTimes, Health target, float now, float cooldown)
        {
            if (lastHitTimes.TryGetValue(target, out float last) && now - last < cooldown) return false;
            lastHitTimes[target] = now;
            return true;
        }

        /// <summary>
        /// Removes entries from <paramref name="lastHitTimes"/> that are either stale (last hit at
        /// least <paramref name="staleAfter"/> seconds ago) or destroyed (Unity's overloaded null
        /// check on the <see cref="Health"/> key). Without this, a debounce map keyed by every enemy
        /// a blade has ever touched would retain destroyed Health refs for the scene's duration.
        /// Uses a shared scratch list so it never allocates. Pure/stateless over the passed-in map,
        /// mirroring <see cref="SmoothSpeed"/>.
        /// </summary>
        internal static void PruneStale(Dictionary<Health, float> lastHitTimes, float now, float staleAfter)
        {
            pruneScratch.Clear();
            foreach (var kv in lastHitTimes)
            {
                if (kv.Key == null || now - kv.Value >= staleAfter)
                    pruneScratch.Add(kv.Key);
            }
            for (int i = 0; i < pruneScratch.Count; i++)
                lastHitTimes.Remove(pruneScratch[i]);
            pruneScratch.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (definition == null) return;

            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return;

            // Don't damage whoever is holding the sword (sword is parented under them).
            if (((Component)health).transform.root == transform.root) return;

            // Opportunistic prune: only bother once the map has grown enough for it to matter, so a
            // normal swing never pays for it.
            if (lastHitTimes.Count > PruneCheckThreshold)
                PruneStale(lastHitTimes, Time.time, definition.hitCooldown * StaleAfterCooldowns);

            if (!TryRegisterHit(lastHitTimes, health, Time.time, definition.hitCooldown)) return;

            float dmg = definition.DamageForSpeed(speed);
            // Resolve the wielder's modifiers at hit time: while held, transform.root is the rig, so its
            // PlayerCombatModifiers (e.g. Ch7 weakpoint-sight) is reachable. Null (unheld / no rig) => 1x.
            if (wielderMods == null) wielderMods = transform.root.GetComponentInParent<PlayerCombatModifiers>();
            dmg = ApplyWielderMultiplier(dmg, wielderMods != null ? wielderMods.DamageMultiplier : 1f);
            if (dmg <= 0f) return;

            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 dir = (other.transform.position - transform.position).normalized;
            health.ApplyDamage(new DamageInfo(dmg, point, dir, transform.root.gameObject));
            EventBus.Publish(new SwordImpact(point, speed, health.gameObject));
        }
    }
}
