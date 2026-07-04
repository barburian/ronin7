using System.Collections.Generic;
using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Ship
{
    /// <summary>
    /// A hostile fighter that dogfights the player in a readable loop:
    /// <c>Spawn → Approach → Strafe → Fire → Evade → (repeat) → Death</c>. Has a
    /// <see cref="Health"/> component so player bolts can cut it down, and fires pooled bolts back.
    ///
    /// FRAME OF REFERENCE (the crux of Phase 7 — read carefully):
    /// The player rig/cockpit is pinned at the world origin and never moves; the
    /// <see cref="ShipController"/> instead moves the <c>universe</c> transform as the inverse of
    /// the virtual ship pose so the world appears to fly past. Planets — and these enemy ships —
    /// are parented UNDER <c>universe</c>. Consequences this AI is built around:
    /// <list type="bullet">
    /// <item>The player's effective position in the universe frame is
    /// <see cref="ShipController.ShipPosition"/>. We chase/strafe relative to THAT, working entirely
    /// in <b>universe-local</b> coordinates (this transform's localPosition), so motion is stable no
    /// matter where the universe has been swept to.</item>
    /// <item>To shoot the player we aim at the player's universe-local position
    /// (<see cref="ShipController.ShipPosition"/>) and fire bolts parented under <c>universe</c> so
    /// they share our frame and converge on the cockpit (which renders at the origin).</item>
    /// <item>Our world transform (what the camera sees) is whatever <c>universe</c> maps our local
    /// pose to — we never reason in world space here except implicitly through the parenting.</item>
    /// </list>
    /// Telegraph/state readability mirrors <see cref="Ronin7.Enemies.Enemy"/>: tint shifts and
    /// distinct beats (line-up, burst, jink) so the player can read intent.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnemyShip : MonoBehaviour
    {
        private enum State { Spawn, Approach, Strafe, Evade, Dead, Disabled }

        /// <summary>
        /// All live enemy ships, for cheap lookups (aim assist, encounter clear-checks) without a
        /// scene-wide FindObjectsByType every frame. Kept tiny by conservative encounter sizes.
        /// </summary>
        public static readonly List<EnemyShip> Active = new();

        [Header("Refs")]
        [SerializeField] private EnemyShipDefinition definition;
        [Tooltip("The moving-world root this ship lives under (drives the shared frame). Auto-found from parent if null.")]
        [SerializeField] private Transform universe;
        [Tooltip("Shared bolt pool. Enemy bolts are fired parented under 'universe'.")]
        [SerializeField] private ProjectilePool pool;
        [SerializeField] private ShipController player;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Renderer bodyRenderer;

        [Header("Spawn beat")]
        [Tooltip("Seconds of harmless drift after spawn before the ship engages (a readable 'arrival').")]
        [SerializeField] private float spawnSettle = 0.5f;

        [Header("Non-lethal disable")]
        [Tooltip("If true, the ship transitions to a drifting Disabled state at low health instead of dying.")]
        [SerializeField] private bool disableInsteadOfDestroy = false;
        [Tooltip("Health fraction at or below which the ship disables (only if disableInsteadOfDestroy).")]
        [Range(0f, 1f)] [SerializeField] private float disableHealthFraction = 0.18f;

        private static readonly Color IdleColor = new Color(0.7f, 0.72f, 0.78f);
        private static readonly Color AggroColor = new Color(1f, 0.5f, 0.3f);
        private static readonly Color DeadColor = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color DisabledColor = new Color(0.30f, 0.34f, 0.40f);

        private Health health;
        private State state = State.Spawn;
        private float timer;
        private float strafeDir = 1f;          // +1 / -1 peel direction, flipped between attack runs
        private float nextShotTime;
        private float burstEndTime;
        private bool firing;

        public bool IsAlive => health != null && health.IsAlive && state != State.Dead && state != State.Disabled;

        /// <summary>Pure check: should the ship transition to Disabled at its current health? Extracted so the
        /// threshold logic can be unit-tested without a live Health component.</summary>
        public static bool ShouldDisable(bool disableInsteadOfDestroy, float currentHealth, float maxHealth, float disableFraction) =>
            disableInsteadOfDestroy && maxHealth > 0f && (currentHealth / maxHealth) <= disableFraction;

        /// <summary>This ship's position in the universe frame (matches the frame ShipPosition lives in).</summary>
        public Vector3 UniversePosition =>
            universe != null ? universe.InverseTransformPoint(transform.position) : transform.position;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (definition == null)
            {
                Debug.LogWarning("[EnemyShip] No EnemyShipDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<EnemyShipDefinition>();
            }
            health.Configure(definition.maxHealth);
            health.Died += OnDied;

            if (universe == null && transform.parent != null) universe = transform.parent;
            if (player == null) player = ShipController.Instance;
            if (muzzle == null) muzzle = transform;

            Tint(IdleColor);
        }

        /// <summary>
        /// Wire the data + shared refs for a ship spawned at runtime (e.g. by
        /// <see cref="SpaceEncounterManager"/>). Safe to call before or after Awake: it (re)applies
        /// the definition's health and caches the frame/pool/player references. Call AFTER the ship
        /// has been parented under <c>universe</c>.
        /// </summary>
        public void Configure(EnemyShipDefinition def, Transform universeRoot, ProjectilePool boltPool, ShipController target)
        {
            if (def != null) definition = def;
            if (universeRoot != null) universe = universeRoot;
            if (boltPool != null) pool = boltPool;
            if (target != null) player = target;

            if (health == null) health = GetComponent<Health>();
            if (definition != null && health != null) health.Configure(definition.maxHealth);
        }

        /// <summary>Opt this ship into the non-lethal drifting-disable end-state (used by GuardEncounter for "disable, not destroy" beats).</summary>
        public void SetNonLethal(bool on, float fraction)
        {
            disableInsteadOfDestroy = on;
            if (fraction > 0f) disableHealthFraction = Mathf.Clamp01(fraction);
        }

        /// <summary>Assign the visuals for a grey-box fighter built from primitives by the spawner.</summary>
        public void WireGreyBox(Renderer body, Transform muzzleTransform)
        {
            bodyRenderer = body;
            muzzle = muzzleTransform;
            Tint(IdleColor);
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            Active.Remove(this);
        }

        /// <summary>Transition to a drifting, powered-down state without destroying the ship.
        /// Idempotent; no-op if already Dead or Disabled.</summary>
        public void Disable()
        {
            if (state == State.Dead || state == State.Disabled) return;
            state = State.Disabled;
            if (health != null) health.Died -= OnDied; // further hits won't destroy a disabled hulk
            Tint(DisabledColor);
            EventBus.Publish(new EnemyShipDisabled(gameObject, UniversePosition));
        }

        private void Update()
        {
            if (state == State.Dead || state == State.Disabled) return;
            timer += Time.deltaTime;

            // Check if ship should be disabled at low health
            if (health != null && ShouldDisable(disableInsteadOfDestroy, health.Current, health.Max, disableHealthFraction))
            {
                Disable();
                return;
            }

            // Everything below operates in universe-local space: our position and the player's
            // virtual position both expressed in that frame so distances/directions are meaningful.
            Vector3 myLocal = LocalPos();
            Vector3 playerLocal = player != null ? player.ShipPosition : Vector3.zero;
            Vector3 toPlayer = playerLocal - myLocal;
            float dist = toPlayer.magnitude;
            Vector3 toPlayerDir = dist > 0.001f ? toPlayer / dist : Vector3.forward;

            switch (state)
            {
                case State.Spawn:
                    // Drift forward harmlessly along our world heading (a simple, in-frame nudge:
                    // translating by world-forward keeps us moving relative to the swept universe),
                    // then engage. Uses transform.Translate so we don't double-apply the universe basis.
                    transform.Translate(Vector3.forward * (definition.moveSpeed * 0.4f) * Time.deltaTime, Space.Self);
                    if (timer >= spawnSettle) Enter(State.Approach);
                    break;

                case State.Approach:
                    // Nose-led flight: steer toward the player and fly along our forward only —
                    // ships never translate sideways, so their motion reads like a real fighter.
                    FaceLocalDir(toPlayerDir);
                    if (dist > definition.preferredRange + definition.rangeTolerance)
                        MoveForward(definition.moveSpeed);
                    else
                    {
                        Tint(AggroColor);
                        nextShotTime = Time.time + definition.aimTime; // line-up beat before firing
                        burstEndTime = 0f;
                        Enter(State.Strafe);
                    }
                    break;

                case State.Strafe:
                    Strafe(playerLocal, toPlayerDir, dist);
                    break;

                case State.Evade:
                    // Peel off: steer away from the player (with a lateral lean so the arc varies)
                    // and fly forward at boosted speed — a fly-by, not a sideways jink.
                    Vector3 evadeTangent = Vector3.Cross(Vector3.up, toPlayerDir).normalized * strafeDir;
                    FaceLocalDir((-toPlayerDir + evadeTangent * 0.6f).normalized);
                    MoveForward(definition.moveSpeed * definition.evadeSpeedMultiplier);
                    if (timer >= definition.evadeTime)
                    {
                        Tint(IdleColor);
                        Enter(State.Approach);
                    }
                    break;
            }
        }

        /// <summary>
        /// Attack run: keep the nose on the player, fly forward (slowing inside the range band so
        /// the pass lingers), and fire bursts when the nose is on target. Peels into Evade when the
        /// run overshoots too close or the burst ends.
        /// </summary>
        private void Strafe(Vector3 playerLocal, Vector3 toPlayerDir, float dist)
        {
            // Aim the nose at the player so bursts can connect. Cache the world-space aim direction
            // once: it's reused unchanged by the fire-cone check below (same tick, same input).
            Vector3 toPlayerWorld = Universe2World(toPlayerDir);
            FaceWorldDir(toPlayerWorld);

            // Forward-only motion: full speed outside the range band, a slow creep inside it so the
            // ship keeps drifting in rather than parking. Overshooting too close ends the run.
            float closeBreak = definition.preferredRange * 0.35f;
            if (dist <= closeBreak)
            {
                strafeDir = -strafeDir; // vary the peel direction between runs
                Enter(State.Evade);
                return;
            }
            float speed = dist > definition.preferredRange ? definition.moveSpeed : definition.moveSpeed * 0.3f;
            MoveForward(speed);

            // --- Firing: only once the burst is "armed" (aim beat elapsed) and the nose is on target. ---
            if (Time.time < nextShotTime && burstEndTime == 0f) return; // still lining up

            if (burstEndTime == 0f) burstEndTime = Time.time + definition.burstDuration;

            bool onTarget = Vector3.Angle(transform.forward, toPlayerWorld) <= definition.fireConeAngle;
            if (Time.time <= burstEndTime)
            {
                if (onTarget && Time.time >= nextShotTime)
                {
                    nextShotTime = Time.time + definition.SecondsPerShot;
                    FireAtPlayer(playerLocal);
                }
            }
            else
            {
                // Burst done → peel off and evade, then re-approach.
                Enter(State.Evade);
            }
        }

        /// <summary>
        /// Fire a bolt that lives under <c>universe</c> (the enemy frame) aimed at the player's
        /// virtual position. Because the cockpit renders at the origin and the bolt shares the
        /// universe frame, a bolt aimed at <see cref="ShipController.ShipPosition"/> converges on
        /// the player as the world sweeps past.
        /// </summary>
        private void FireAtPlayer(Vector3 playerLocal)
        {
            if (pool == null) return;
            Vector3 muzzleWorld = muzzle.position;
            // Aim point in WORLD space = where the player's virtual position maps to under universe.
            Vector3 targetWorld = universe != null ? universe.TransformPoint(playerLocal) : playerLocal;
            Vector3 dir = (targetWorld - muzzleWorld).normalized;

            pool.Fire(gameObject, muzzleWorld, Quaternion.LookRotation(dir),
                definition.projectileSpeed, definition.projectileDamage, definition.projectileLifetime,
                definition.projectileRadius, definition.tracerColor, parent: universe);

            OnFired(muzzleWorld, dir);
        }

        private void OnDied()
        {
            if (state == State.Dead) return;
            state = State.Dead;
            Tint(DeadColor);
            EventBus.Publish(new EnemyShipDestroyed(gameObject, UniversePosition));
            OnDestroyedFx(transform.position);
            // Leave the hulk briefly then remove; the encounter manager tracks the kill via the event.
            Destroy(gameObject, 0.1f);
        }

        // --- Universe-frame movement helpers -----------------------------------------------------

        private Vector3 LocalPos() =>
            universe != null ? universe.InverseTransformPoint(transform.position) : transform.position;

        /// <summary>Fly along our own nose at the given speed — the ONLY way these ships move once
        /// spawned, so motion always matches the visible heading (no sideways sliding).</summary>
        private void MoveForward(float speed)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
        }

        /// <summary>Smoothly turn the nose toward a universe-local direction (rendered via the universe transform).</summary>
        private void FaceLocalDir(Vector3 universeDir) => FaceWorldDir(Universe2World(universeDir));

        /// <summary>Smoothly turn the nose toward an already-resolved world-space direction.</summary>
        private void FaceWorldDir(Vector3 worldDir)
        {
            if (worldDir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, definition.turnSpeed * Time.deltaTime);
        }

        private Vector3 Universe2World(Vector3 universeDir) =>
            universe != null ? universe.TransformVector(universeDir) : universeDir;

        private void Enter(State next) { state = next; timer = 0f; }

        private void Tint(Color c) { RendererTint.Apply(bodyRenderer, c); }

        // --- FX hooks ----------------------------------------------------------------------------

        /// <summary>FX/SFX hook for the muzzle flash / fire SFX. Overridable.</summary>
        protected virtual void OnFired(Vector3 muzzlePos, Vector3 direction) { }

        /// <summary>FX/SFX hook for the destruction explosion. Overridable.</summary>
        protected virtual void OnDestroyedFx(Vector3 worldPoint) { }
    }
}
