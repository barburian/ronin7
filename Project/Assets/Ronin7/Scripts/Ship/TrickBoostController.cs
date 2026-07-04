using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// H4 "clean near-miss speed trim": rewards flying close past asteroids without touching them by
    /// building a streak that trims <see cref="ShipController.SpeedTrimMultiplier"/> upward. INERT unless
    /// a scene adds this component — it does nothing on its own, it only ever writes the
    /// <see cref="ShipController"/> integration point that already exists (see
    /// <see cref="ShipController.EffectiveSpeed"/>), so it composes with the sun-boost hook for free.
    ///
    /// FRAME OF REFERENCE: mirrors <see cref="AsteroidHazard"/>'s player scan exactly — contacts are
    /// resolved in WORLD space against <see cref="Asteroid.Active"/>, using the player hull's world
    /// position (rig root ≈ world origin) and a combined contact radius of
    /// <see cref="shipHullRadius"/> + <see cref="Asteroid.WorldRadius"/> per asteroid.
    ///
    /// NEAR-MISS STATE MACHINE (locked-in semantic): each live asteroid is tracked as untracked
    /// ("outside", the implicit default — no dictionary entry), <see cref="ApproachState.InBand"/>
    /// (inside the near-miss band, never having touched the hull during this approach), or
    /// <see cref="ApproachState.Contacted"/> (has touched the hull during this approach, so any near-miss
    /// credit for this approach is forfeit). A clean pass is only counted on the InBand -> outside
    /// transition, i.e. when an asteroid the ship never touched fully clears the band moving away.
    /// Entries are added when an asteroid first enters the band or contact zone and removed the moment
    /// it clears the band, so the dictionary only ever holds asteroids currently "close" — it is also
    /// opportunistically pruned of destroyed/despawned keys past <see cref="PruneCheckThreshold"/>
    /// entries, mirroring <see cref="AsteroidHazard"/>'s enemy-cooldown pruning and
    /// <see cref="BladeDamager"/>'s per-target debounce pruning. Zero per-frame allocation (indexed List iteration, Dictionary reads/writes on existing capacity,
    /// reused static scratch buffers for pruning).
    ///
    /// STREAK: <see cref="NextStreak"/> extends the streak on every clean pass;
    /// <see cref="SpeedMultiplier"/> converts it into <see cref="ShipController.SpeedTrimMultiplier"/>.
    /// The streak resets to zero (multiplier back to 1) on either taking asteroid collision damage
    /// (an <see cref="EntityDamaged"/> for the ship with <see cref="DamageType.Collision"/> — the exact
    /// same damage <see cref="AsteroidHazard"/> applies on an actual hit) or on
    /// <see cref="idleResetSeconds"/> of unscaled time passing without a new clean pass.
    ///
    /// DESIGN FLAG: missBand/perStack/maxStreak/idleResetSeconds/shipHullRadius are STARTING VALUES —
    /// final feel needs an in-headset tuning pass (flagged by design), same caveat as ShipController's
    /// steering rates.
    /// </summary>
    public class TrickBoostController : MonoBehaviour
    {
        // Once the per-asteroid state map grows past this many entries, opportunistically prune
        // destroyed/no-longer-active keys (see PruneStale). A normal belt pass never has this many
        // asteroids simultaneously close, so this never triggers in the common case.
        private const int PruneCheckThreshold = 16;

        [Header("Refs")]
        [Tooltip("The ship this trims. Left empty, resolved via GetComponent on this object.")]
        [SerializeField] private ShipController shipController;
        [Tooltip("The player ship's Health (rig) — same reference AsteroidHazard.Configure receives. " +
                  "Sources the hull-contact world position (mirrors AsteroidHazard.ScanPlayer) and " +
                  "identifies 'the ship' for the collision-break reset. Left empty, resolved via " +
                  "GetComponent on this object; if this component doesn't live on the rig, a scene " +
                  "wiring pass must assign it explicitly (same object AsteroidHazard.Configure is given).")]
        [SerializeField] private Health shipHealth;

        [Header("Tuning — STARTING VALUES, needs an in-headset pass (flagged by design)")]
        [Tooltip("Distance (metres) beyond the contact radius where a pass still reads as a clean near-miss.")]
        [SerializeField] private float missBand = 4f;
        [Tooltip("Speed-trim bonus per streak stack, e.g. 0.04 = +4% forward speed per stack.")]
        [SerializeField] private float perStack = 0.04f;
        [Tooltip("Streak cap.")]
        [SerializeField] private int maxStreak = 5;
        [Tooltip("Unscaled seconds without a new clean pass before the streak decays to zero.")]
        [SerializeField] private float idleResetSeconds = 3f;
        [Tooltip("World-space radius of the player's hull contact volume — mirrors AsteroidHazard.playerHullRadius.")]
        [SerializeField] private float shipHullRadius = 1.2f;

        /// <summary>Per-asteroid near-miss progress. Absence of a key means "outside" (the implicit
        /// default state); see the class summary for the full state machine.</summary>
        private enum ApproachState { InBand, Contacted }

        private readonly Dictionary<Asteroid, ApproachState> approachState = new();
        // Reused scratch buffers for PruneStale so pruning never allocates (mirrors AsteroidHazard.PruneEnemies).
        private static readonly List<Asteroid> _pruneScratch = new();
        private static readonly HashSet<Asteroid> _aliveSet = new();

        private int streak;
        private float lastNearMissUnscaledTime = float.NegativeInfinity;

        private void Awake()
        {
            if (shipController == null) shipController = GetComponent<ShipController>();
            if (shipHealth == null) shipHealth = GetComponent<Health>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EntityDamaged>(OnEntityDamaged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityDamaged>(OnEntityDamaged);
            approachState.Clear();
            streak = 0;
            ApplyMultiplier(); // never leave a stale speed trim active past teardown (mirrors ParryFlowController.OnDisable)
        }

        private void Update()
        {
            ScanNearMisses();

            // Idle decay, unscaled so an unrelated slow-mo elsewhere can't stretch/shrink it (mirrors
            // ParryFlowController's idle clock). Ordered AFTER the scan so a clean pass registered this
            // very frame refreshes the timestamp before this check runs.
            if (streak > 0 && Time.unscaledTime - lastNearMissUnscaledTime >= idleResetSeconds)
            {
                streak = 0;
                ApplyMultiplier();
            }
        }

        /// <summary>True when centerDistance is at or beyond the combined contact radius but still
        /// within missBand of it — i.e. a clean pass, not a touch. LOCKED SEMANTIC: exactly AT the
        /// contact radius reads as a near-miss (inclusive lower bound), never a contact — genuine
        /// overlap requires centerDistance strictly less than combinedContactRadius. This is
        /// independent of AsteroidHazard's own (separately authoritative) collision-damage check.</summary>
        public static bool IsNearMiss(float centerDistance, float combinedContactRadius, float missBand)
            => centerDistance >= combinedContactRadius && centerDistance < combinedContactRadius + missBand;

        /// <summary>Advances the streak by one clean pass, capped at max.</summary>
        public static int NextStreak(int current, int max) => Mathf.Min(current + 1, max);

        /// <summary>Speed-trim multiplier from the current streak: +perStack per stack. STARTING
        /// FORMULA — feel needs an in-headset pass (flagged by design).</summary>
        public static float SpeedMultiplier(int streak, float perStack) => 1f + streak * perStack;

        /// <summary>Scans every live asteroid against the ship hull, exactly mirroring
        /// AsteroidHazard.ScanPlayer's world-space distance check, and advances each asteroid's
        /// per-approach state (see the class summary's state machine).</summary>
        private void ScanNearMisses()
        {
            var list = Asteroid.Active;
            if (approachState.Count > PruneCheckThreshold) PruneStale(list);

            // Rig root ≈ world origin — same convention as AsteroidHazard.ScanPlayer's hullCenter.
            Vector3 hullCenter = shipHealth != null ? shipHealth.transform.position : transform.position;

            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (a == null || !a.IsAlive) continue;

                float dist = Vector3.Distance(hullCenter, a.WorldCenter);
                float contact = shipHullRadius + a.WorldRadius;
                bool wasTracked = approachState.TryGetValue(a, out ApproachState state);

                if (dist < contact)
                {
                    // Touched — forfeits near-miss credit for this approach until it fully clears the band.
                    approachState[a] = ApproachState.Contacted;
                }
                else if (IsNearMiss(dist, contact, missBand))
                {
                    if (!wasTracked) approachState[a] = ApproachState.InBand;
                    // Already tracked (InBand or Contacted): no state change until it clears the band.
                }
                else if (wasTracked)
                {
                    // Fully clear of the band: this is the InBand -> outside transition. Only a clean
                    // approach (never Contacted) counts as a near-miss.
                    approachState.Remove(a);
                    if (state == ApproachState.InBand) RegisterNearMiss();
                }
            }
        }

        private void RegisterNearMiss()
        {
            streak = NextStreak(streak, maxStreak);
            lastNearMissUnscaledTime = Time.unscaledTime;
            ApplyMultiplier();
        }

        private void ApplyMultiplier()
        {
            if (shipController != null) shipController.SpeedTrimMultiplier = SpeedMultiplier(streak, perStack);
        }

        /// <summary>Breaks the streak on taking asteroid collision damage — the exact same DamageType
        /// AsteroidHazard applies on an actual hit. Identifies "the ship" via shipHealth's GameObject,
        /// the same reference AsteroidHazard/PlayerShipDamageRelay use.</summary>
        private void OnEntityDamaged(EntityDamaged e)
        {
            if (e.Info.Type != DamageType.Collision) return;
            if (shipHealth == null || e.Entity != shipHealth.gameObject) return;
            if (streak == 0) return;

            streak = 0;
            ApplyMultiplier();
        }

        /// <summary>Drops entries for asteroids no longer alive/active so the map can't grow unbounded.
        /// Mirrors AsteroidHazard.PruneEnemies exactly (same scratch-buffer idiom, no allocation).</summary>
        private void PruneStale(List<Asteroid> active)
        {
            _aliveSet.Clear();
            for (int i = 0; i < active.Count; i++) _aliveSet.Add(active[i]);
            _pruneScratch.Clear();
            foreach (var kv in approachState)
                if (kv.Key == null || !_aliveSet.Contains(kv.Key)) _pruneScratch.Add(kv.Key);
            for (int i = 0; i < _pruneScratch.Count; i++) approachState.Remove(_pruneScratch[i]);
        }
    }
}
