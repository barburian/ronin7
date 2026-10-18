using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ronin7.Core;
using UnityEngine;

[assembly: InternalsVisibleTo("Ronin7.Tests.EditMode")]
// P4-01: RunDirector (Ronin7.Flow) is the sole writer of PlayerCombatModifiers.SetBoon.
[assembly: InternalsVisibleTo("Ronin7.Flow")]

namespace Ronin7.Combat
{
    /// <summary>
    /// Sits on the blade collider (a trigger). Samples its own speed and, on contact with a
    /// <see cref="Health"/>, deals damage scaled by swing speed. Ignores the wielder so you
    /// can't cut yourself, and hits each target at most once per swing (until the blade slows
    /// below minSwingSpeed, or hitCooldown elapses), while still landing on every other target
    /// it touches in that swing.
    ///
    /// The blade is a trigger on a kinematic hand-parented body, so it passes through enemies.
    /// Because a fast swing can jump past a thin target between steps, LateUpdate also sweeps a
    /// few points along the blade from last frame's pose to this frame's (covers rotation arcs).
    /// The trigger and the sweep share the same per-swing dedup, so nothing is double-damaged.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BladeDamager : MonoBehaviour
    {
        [Tooltip("Optional explicit definition; otherwise taken from a Sword on a parent.")]
        [SerializeField] private WeaponDefinition definitionOverride;

        [Tooltip("Exponential smoothing on the measured blade speed (0 = raw/spike-prone, 1 = frozen). " +
                 "Rejects single-frame tracking spikes so a stutter can't inflate damage.")]
        [SerializeField, Range(0f, 1f)] private float speedSmoothing = 0.5f;

        [Header("Swept hits")]
        [Tooltip("Points sampled base→tip along the blade and swept each frame. Each costs one SphereCast per frame while swinging.")]
        [SerializeField, Range(2, 5)] private int sweepSamples = 4;
        [Tooltip("World radius (m) of each swept sample — roughly half the blade width.")]
        [SerializeField] private float sweepRadius = 0.02f;
        [Tooltip("A sample moving further than this (m) in one frame is a teleport (grab, respawn), not a swing: skip the sweep.")]
        [SerializeField] private float maxSweepPerFrame = 1f;

        private const int MaxSamples = 5;
        private const int HitBufferSize = 8;

        private WeaponDefinition definition;
        private Vector3 lastPos;
        private float speed;
        private Vector3 bladeVel; // raw world velocity (real time), for the thrust test
        // Per-swing dedup shared by OnTriggerEnter and the sweep. Cleared when the swing ends
        // (speed < minSwingSpeed), so it never accumulates stale/destroyed Health refs.
        private readonly Dictionary<Health, float> lastHitTimes = new();
        private PlayerCombatModifiers wielderMods;

        private BoxCollider box;
        private int sweepMask;
        private bool havePrevSamples;
        private readonly Vector3[] prevSamples = new Vector3[MaxSamples];
        private readonly Vector3[] curSamples = new Vector3[MaxSamples];
        private static readonly RaycastHit[] sweepHits = new RaycastHit[HitBufferSize];

        /// <summary>P4-08: the weapon stats this blade reads at hit time.</summary>
        public WeaponDefinition Definition => definition;

        /// <summary>P4-08: swaps the loadout in after scene load. Awake resolves <see cref="Sword.Definition"/>
        /// before RunDirector knows the run's loadout, so RunDirector.ApplyBoonInventoryToRig calls this. Null is ignored.</summary>
        public void SetDefinition(WeaponDefinition def)
        {
            if (def != null) definition = def;
        }

        /// <summary>Current blade speed in m/s (world), exponentially smoothed.</summary>
        public float Speed => speed;

        /// <summary>P4-04: raw world blade velocity (real time).</summary>
        public Vector3 Velocity => bladeVel;

        /// <summary>P4-04 S5: the rig that last grabbed this sword. Kept while thrown, so a thrown blade still
        /// skips its owner and credits him as <see cref="DamageInfo.Source"/>.</summary>
        public Transform WielderRoot { get; private set; }

        /// <summary>P4-04: world base and tip of the blade (box ends along its longest axis).</summary>
        public void WorldEndpoints(out Vector3 baseWorld, out Vector3 tipWorld)
        {
            if (box == null) { baseWorld = tipWorld = transform.position; return; }
            BladeEndpoints(box.center, box.size, out Vector3 a, out Vector3 b);
            baseWorld = transform.TransformPoint(a);
            tipWorld = transform.TransformPoint(b);
        }

        /// <summary>P4-04 S4: what a plain swing at the current speed would deal (DamageForSpeed x the wielder's
        /// <see cref="PlayerCombatModifiers.DamageMultiplier"/>), without consuming any skill bonus.</summary>
        public float SwingDamageNow()
        {
            if (definition == null) return 0f;
            var mods = Mods();
            return definition.DamageForSpeed(speed, ThresholdScale()) * (mods != null ? mods.DamageMultiplier : 1f);
        }

        private void Awake()
        {
            definition = definitionOverride != null
                ? definitionOverride
                : GetComponentInParent<Sword>()?.Definition;
            lastPos = transform.position;
            box = GetComponent<BoxCollider>();
            // Same layers the blade's trigger can touch, so the sweep and OnTriggerEnter agree.
            for (int l = 0; l < 32; l++)
                if (!Physics.GetIgnoreLayerCollision(gameObject.layer, l)) sweepMask |= 1 << l;
            var grabbable = GetComponentInParent<Grabbable>();
            if (grabbable != null) grabbable.Grabbed += OnGrabbed;
            // NOTE: the wielder's PlayerCombatModifiers is resolved lazily in OnTriggerEnter, not here.
            // At Awake the sword is an unparented world Grabbable (transform.root == the sword itself),
            // so there is no rig above it to find yet; it is only reparented under the hand on grab.
        }

        private void OnGrabbed(Transform hand)
        {
            if (hand.root == WielderRoot) return;
            WielderRoot = hand.root;
            wielderMods = null; // re-resolve against the new wielder
        }

        // While held the rig is transform.root; while thrown (S5) the stored wielder stands in for it.
        private Transform OwnerRoot => WielderRoot != null ? WielderRoot : transform.root;

        private void FixedUpdate()
        {
            speed = SmoothSpeed(lastPos, transform.position, RealFixedDt(Time.fixedDeltaTime, Time.timeScale), speed, speedSmoothing);
            float realDt = RealFixedDt(Time.fixedDeltaTime, Time.timeScale);
            if (realDt > 0f) bladeVel = (transform.position - lastPos) / realDt;
            lastPos = transform.position;
            if (definition != null) ResetIfSwingEnded(lastHitTimes, speed, definition.minSwingSpeed);
        }

        // Re-enabling (tracking regained, pause ended) must start a fresh swing: the pose may have
        // jumped while disabled, and a stale lastPos would read that jump as a max-speed flick.
        private void OnEnable()
        {
            havePrevSamples = false;
            lastPos = transform.position;
            speed = 0f;
            lastHitTimes.Clear();
        }

        // LateUpdate, not FixedUpdate: the tracked hand pose is written at render rate (Update /
        // before-render), and 50 Hz physics only sees every ~1.5th frame of it. Sweeping the final
        // per-frame pose means hits line up with the blade the player actually sees.
        private void LateUpdate()
        {
            if (box == null || definition == null) return;

            int n = Mathf.Clamp(sweepSamples, 2, MaxSamples);
            BladeEndpoints(box.center, box.size, out Vector3 a, out Vector3 b);
            Vector3 wa = transform.TransformPoint(a), wb = transform.TransformPoint(b);
            for (int i = 0; i < n; i++) curSamples[i] = SamplePoint(wa, wb, i, n);

            // Below swing speed nothing can deal damage, so don't pay for casts. P4-02: same effective
            // (EdgeSensitivity-scaled) minimum as DamageForSpeed.
            if (havePrevSamples && speed >= definition.minSwingSpeed * ThresholdScale())
            {
                for (int i = 0; i < n; i++) Sweep(prevSamples[i], curSamples[i]);
            }

            for (int i = 0; i < n; i++) prevSamples[i] = curSamples[i];
            havePrevSamples = true;
        }

        private void Sweep(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 1e-4f || dist > maxSweepPerFrame) return;

            int hits = Physics.SphereCastNonAlloc(from, sweepRadius, delta / dist, sweepHits, dist,
                sweepMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits; i++)
            {
                // distance 0 = already overlapping at the sweep start: OnTriggerEnter owns entry
                // contacts, and skipping these stops a blade resting inside a body from re-hitting.
                if (sweepHits[i].distance <= 0f) continue;
                TryHit(sweepHits[i].collider, sweepHits[i].point);
            }
        }

        /// <summary>
        /// Local-space ends of a box blade: the two face centres along its longest axis. Pure so the
        /// sample geometry is unit-testable without a collider.
        /// </summary>
        internal static void BladeEndpoints(Vector3 center, Vector3 size, out Vector3 a, out Vector3 b)
        {
            Vector3 axis = size.z >= size.x && size.z >= size.y ? new Vector3(0f, 0f, size.z)
                         : size.y >= size.x ? new Vector3(0f, size.y, 0f)
                         : new Vector3(size.x, 0f, 0f);
            a = center - axis * 0.5f;
            b = center + axis * 0.5f;
        }

        /// <summary>Sample <paramref name="i"/> of <paramref name="count"/>, evenly spaced from a to b inclusive.</summary>
        internal static Vector3 SamplePoint(Vector3 a, Vector3 b, int i, int count)
            => count <= 1 ? b : Vector3.Lerp(a, b, (float)i / (count - 1));

        /// <summary>
        /// Ends the swing (clearing the per-swing hit set so every target can be hit again) once the
        /// blade has slowed below <paramref name="minSwingSpeed"/>. Returns whether it reset.
        /// </summary>
        internal static bool ResetIfSwingEnded(Dictionary<Health, float> lastHitTimes, float speed, float minSwingSpeed)
        {
            if (speed >= minSwingSpeed) return false;
            if (lastHitTimes.Count > 0) lastHitTimes.Clear();
            return true;
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
        public static float RealFixedDt(float fixedDt, float timeScale) => timeScale > 0f ? fixedDt / timeScale : 0f;

        /// <summary>
        /// Real (unscaled) seconds between physics steps: TimeScaleArbiter shrinks fixedDeltaTime by
        /// timeScale, so dividing it back out keeps swing speed independent of Overdrive (BUG-21).
        /// 0 when paused, which <see cref="SmoothSpeed"/> treats as "keep previous speed".
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
        internal const float PrecisionBaseMultiplier = 1.25f;
        internal const float PrecisionSightMultiplier = 2f;

        /// <summary>Head = hit point in the top 20% of the victim's bounds; thrust = blade velocity within ~46 deg of its axis.</summary>
        internal static bool IsPrecisionHit(float hitY, Bounds victimBounds, Vector3 bladeVel, Vector3 bladeAxis)
        {
            if (hitY >= victimBounds.max.y - 0.2f * victimBounds.size.y) return true;
            return Vector3.Dot(bladeVel.normalized, bladeAxis) > 0.7f; // zero velocity normalizes to zero -> false
        }

        /// <summary>Precision hits get 1.25x (2x with Weakpoint Sight); body hits are untouched.</summary>
        internal static float PrecisionMultiplier(bool precision, bool sightActive)
            => PrecisionMultiplier(precision, sightActive, 0f);

        /// <summary>P4-02 Precision: the base 1.25x rises by the boon bonus, capped at Sight's 2x.</summary>
        internal static float PrecisionMultiplier(bool precision, bool sightActive, float precisionBonus)
            => !precision ? 1f : sightActive ? PrecisionSightMultiplier
                : Mathf.Min(PrecisionBaseMultiplier + Mathf.Max(0f, precisionBonus), PrecisionSightMultiplier);

        // Resolved lazily: at Awake the sword is unparented, the rig only sits above it once grabbed.
        private PlayerCombatModifiers Mods()
        {
            if (wielderMods == null) wielderMods = OwnerRoot.GetComponentInParent<PlayerCombatModifiers>();
            return wielderMods;
        }

        private float ThresholdScale()
        {
            var mods = Mods();
            return mods != null ? WeaponDefinition.EdgeThresholdScale(mods.Boon(BoonEffectKind.EdgeSensitivity)) : 1f;
        }

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

        private void OnTriggerEnter(Collider other) => TryHit(other, other.ClosestPoint(transform.position));

        // Shared by the trigger and the sweep so both go through the same per-swing dedup.
        private void TryHit(Collider other, Vector3 point)
        {
            if (definition == null) return;

            var health = other.GetComponentInParent<Health>();
            if (health == null || !health.IsAlive) return;

            // Don't damage whoever is holding the sword (sword is parented under them).
            if (((Component)health).transform.root == OwnerRoot) return;

            if (!TryRegisterHit(lastHitTimes, health, Time.time, definition.hitCooldown)) return;

            // Resolve the wielder's modifiers at hit time: while held, transform.root is the rig, so its
            // PlayerCombatModifiers (e.g. Ch7 weakpoint-sight) is reachable. Null (unheld / no rig) => 1x.
            Mods();
            float dmg = definition.DamageForSpeed(speed, ThresholdScale());
            float postureScale = 1f;
            dmg = ApplyWielderMultiplier(dmg, wielderMods != null ? wielderMods.DamageMultiplierFor(health.gameObject, out postureScale) : 1f);
            if (box != null)
            {
                BladeEndpoints(box.center, box.size, out Vector3 la, out Vector3 lb);
                Vector3 axis = (transform.TransformPoint(lb) - transform.TransformPoint(la)).normalized;
                bool precision = IsPrecisionHit(point.y, other.bounds, bladeVel, axis);
                dmg *= PrecisionMultiplier(precision, wielderMods != null && wielderMods.WeakpointSightActive,
                    wielderMods != null ? wielderMods.Boon(BoonEffectKind.Precision) : 0f);
            }
            if (dmg <= 0f) return;

            Vector3 dir = (other.transform.position - transform.position).normalized;
            if (wielderMods != null) wielderMods.HitPostureScale = postureScale;
            health.ApplyDamage(new DamageInfo(dmg, point, dir, OwnerRoot.gameObject, DamageType.Melee, speed));
            if (wielderMods != null) wielderMods.HitPostureScale = 1f;
            EventBus.Publish(new SwordImpact(point, speed, health.gameObject, bladeVel));
        }
    }
}
