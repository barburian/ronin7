using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Shared FSM for melee enemies. Subclasses provide tuning, the engagement decision,
    /// and the parry geometry; this base owns the Windup → Active → Recover → Stagger
    /// pose-lerp, tinting, deflect/land-hit/died publishing on the <see cref="EventBus"/>,
    /// and FindPlayer / Tint / Enter helpers.
    ///
    /// FSM:  Idle ──┐
    ///              ├── (subclass enters) ── Chase ──┐
    ///              │                                │
    ///              └─ Windup → Active → Recover ────┴─→ StateAfterRecover (default Idle)
    ///                         │
    ///                         └─ deflect → Stagger ───→ StateAfterStagger (default Idle)
    ///
    /// Dead is terminal. Chase exists in the enum because Enemy uses it; TrainingDummy
    /// goes Idle → Windup directly and never enters Chase.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public abstract class MeleeAttacker : MonoBehaviour
    {
        protected enum State { Idle, Chase, Windup, Active, Recover, Stagger, Dead }

        [Header("Refs (MeleeAttacker)")]
        [SerializeField] protected Transform weapon;
        [SerializeField] protected Renderer bodyRenderer;
        [SerializeField] protected Health target;
        [SerializeField] protected bool nonLethalDisable = false;

        [Header("Weapon poses (local euler) — overhead downward chop")]
        [SerializeField] protected Vector3 restEuler = new Vector3(15f, 0f, 0f);
        [SerializeField] protected Vector3 windupEuler = new Vector3(-140f, 0f, 0f);
        [SerializeField] protected Vector3 strikeEuler = new Vector3(55f, 0f, 0f);
        [SerializeField] protected Vector3 recoilEuler = new Vector3(-120f, 0f, 0f);

        // Sunder Beat: a deflect landing within this many seconds of entering State.Active counts as
        // "perfect" (see Deflect / ParryTiming.ParryQuality).
        private const float PerfectParryWindow = 0.12f;

        // G4 Blade Clash: both blades must be at/above this speed (m/s) for a deflect to also publish
        // BladeClash. WeaponDefinition.referenceSwingSpeed defaults to 6 (max-damage swing); 4.5 sits
        // below that so a committed swing — not necessarily a maxed one — still counts on both sides.
        private const float ClashThreshold = 4.5f;

        [Tooltip("Exponential smoothing on this enemy's tracked swing speed — same idiom as " +
                 "BladeDamager's speedSmoothing.")]
        [SerializeField, Range(0f, 1f)] private float swingSmoothing = 0.5f;

        protected static readonly Color IdleColor = Color.white;
        protected static readonly Color TelegraphColor = new Color(1f, 0.4f, 0.25f);
        protected static readonly Color StaggerColor = new Color(0.4f, 0.6f, 1f);

        // Blade renderer under weapon, resolved once in Awake (see EnemySwordVisual.FindBladeRenderer).
        // Null for TrainingDummy/art-prefab rigs with no "Blade"-named child — RendererTint.Apply is
        // null-safe so Tint() stays a no-op for the blade in that case.
        private Renderer bladeRenderer;

        protected Health health;
        protected State state = State.Idle;
        protected float timer;
        protected bool deflectedThisSwing;
        protected Vector3 currentEuler;
        protected Vector3 staggerFromEuler;
        private bool aggroCounted;

        // G4 Blade Clash: EMA-smoothed speed of SwingTrackPoint, using BladeDamager's exact math.
        private Vector3 lastSwingPos;
        private float swingSpeed;

        // ---- Per-subclass tuning (sourced from SO or inline fields) ----
        protected abstract float TelegraphTime { get; }
        protected abstract float ActiveTime { get; }
        protected abstract float RecoverTime { get; }
        protected abstract float StaggerTime { get; }
        protected abstract float AttackDamage { get; }

        // ---- Hooks the subclass MUST implement ----
        /// <summary>FixedUpdate callback during the parry window. Run a physics query
        /// (capsule for a moving blade, sphere for a fixed guard zone) and invoke
        /// <see cref="Deflect"/> if a blade is in zone.</summary>
        protected abstract void TryDetectParry();

        /// <summary>True if the player is still in landing-hit range when Active ends.</summary>
        protected abstract bool IsInLandHitRange();

        // ---- Hooks the subclass MAY override ----
        /// <summary>Per-state per-frame hook called BEFORE the case switch. Used by
        /// subclasses that want to face the player every frame regardless of state.</summary>
        protected virtual void BeforeStateTick() { }

        /// <summary>Called every Update tick while in <see cref="State.Idle"/>.
        /// Subclass decides when to <see cref="BeginAttack"/> or <c>Enter(State.Chase)</c>.</summary>
        protected virtual void OnIdleTick() { }

        /// <summary>Called every Update tick while in <see cref="State.Chase"/>.
        /// Subclass moves the body and decides when to <see cref="BeginAttack"/>. Default
        /// is no-op — TrainingDummy never enters Chase.</summary>
        protected virtual void OnChaseTick() { }

        /// <summary>Pre-attack hook (e.g. snap-face the player). Runs inside
        /// <see cref="BeginAttack"/> before the Windup transition.</summary>
        protected virtual void OnBeginAttack() { }

        /// <summary>Runs at the end of Active, after the LandHit check, before the Recover transition.</summary>
        protected virtual void OnActiveEnd() { }

        /// <summary>Extra death pose (e.g. lock the weapon at strike before the body topples).</summary>
        protected virtual void OnDeathPose() { }

        /// <summary>True when this frame is inside the parry window. Default: <see cref="State.Active"/>.
        /// Subclasses may widen — e.g. Enemy also lets late Windup parry.</summary>
        protected virtual bool IsInParryWindow() => state == State.Active;

        /// <summary>
        /// Point whose world-space velocity approximates this enemy's blade-tip speed (G4 Blade
        /// Clash). Default is <see cref="weapon"/> itself, but that transform only rotates in these
        /// rigs (the chop is animated via <see cref="PoseWeapon"/> changing localRotation, not
        /// position) so its own position barely moves. Subclasses with a real swinging tip at a fixed
        /// offset from the pivot — e.g. Enemy's bladeTip, the same point used for its parry capsule —
        /// should override this to return that point instead.
        /// </summary>
        protected virtual Transform SwingTrackPoint => weapon;

        /// <summary>Smoothed enemy blade-tip speed (m/s); 0 outside Windup/Active. Feeds
        /// <see cref="ParryTiming.IsClash"/> via the <see cref="Deflect(Vector3, BladeDamager)"/> overload.</summary>
        protected float SwingSpeed => swingSpeed;

        /// <summary>State to land in once Recover completes.</summary>
        protected virtual State StateAfterRecover => State.Idle;

        /// <summary>State to land in once Stagger completes.</summary>
        protected virtual State StateAfterStagger => State.Idle;

        /// <summary>Slerp-toward-player rate for <see cref="FacePlayer"/>. Default 8 (Enemy);
        /// TrainingDummy overrides to 5 for a slower, less twitchy track.</summary>
        protected virtual float FaceTurnRate => 8f;

        /// <summary>Optional per-subclass debug log. Default is silent.</summary>
        protected virtual void Log(string msg) { }

        // ---- Combat activity tracking ----
        /// <summary>Keeps the global on-foot aggro count in sync with this enemy's FSM (save gating).</summary>
        private void SetAggroCounted(bool aggro)
        {
            if (aggro == aggroCounted) return;
            aggroCounted = aggro;
            if (aggro) CombatActivity.Add();
            else CombatActivity.Remove();
        }

        // ---- Unity lifecycle ----
        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            health.Died += OnDied;
            if (target == null) target = FindPlayer();
            currentEuler = restEuler;
            if (weapon != null) weapon.localRotation = Quaternion.Euler(restEuler);
            EnemySwordVisual.EnsureVisible(weapon, transform); // placeholder katana so the chop has something to swing
            bladeRenderer = EnemySwordVisual.FindBladeRenderer(weapon);
            Tint(IdleColor);
        }

        protected virtual void OnDisable()
        {
            SetAggroCounted(false);
        }

        protected virtual void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
            SetAggroCounted(false);
        }

        protected virtual void Update()
        {
            if (state == State.Dead) return;
            if (target == null) { target = FindPlayer(); if (target == null) return; }

            timer += Time.deltaTime;
            BeforeStateTick();

            switch (state)
            {
                case State.Idle:
                    OnIdleTick();
                    break;

                case State.Chase:
                    OnChaseTick();
                    break;

                case State.Windup:
                    PoseWeapon(restEuler, windupEuler, timer / TelegraphTime);
                    Tint(Color.Lerp(IdleColor, TelegraphColor, timer / TelegraphTime));
                    if (timer >= TelegraphTime)
                    {
                        Log("STRIKE");
                        Enter(State.Active);
                    }
                    break;

                case State.Active:
                    PoseWeapon(windupEuler, strikeEuler, timer / ActiveTime);
                    if (timer >= ActiveTime)
                    {
                        if (!deflectedThisSwing && IsInLandHitRange()) LandHit();
                        OnActiveEnd();
                        Enter(State.Recover);
                    }
                    break;

                case State.Recover:
                    PoseWeapon(strikeEuler, restEuler, timer / RecoverTime);
                    Tint(Color.Lerp(TelegraphColor, IdleColor, timer / RecoverTime));
                    if (timer >= RecoverTime) Enter(StateAfterRecover);
                    break;

                case State.Stagger:
                    if (timer < StaggerTime * 0.35f)
                        PoseWeapon(staggerFromEuler, recoilEuler, timer / (StaggerTime * 0.35f));
                    else
                        PoseWeapon(recoilEuler, restEuler,
                            (timer - StaggerTime * 0.35f) / (StaggerTime * 0.65f));
                    Tint(Color.Lerp(StaggerColor, IdleColor, timer / StaggerTime));
                    if (timer >= StaggerTime) Enter(StateAfterStagger);
                    break;
            }
        }

        protected virtual void FixedUpdate()
        {
            if (state == State.Dead) return;
            UpdateSwingSpeed();
            if (deflectedThisSwing) return;
            if (!IsInParryWindow()) return;
            TryDetectParry();
        }

        /// <summary>
        /// G4 Blade Clash: one EMA step (BladeDamager.SmoothSpeed) over SwingTrackPoint while
        /// Windup/Active, so <see cref="SwingSpeed"/> reflects how hard this enemy is actually
        /// swinging. Zeroed outside those states — cheap, and a clash can't be measured against a
        /// weapon that isn't mid-swing.
        /// </summary>
        private void UpdateSwingSpeed()
        {
            Transform tip = SwingTrackPoint;
            if (tip == null) { swingSpeed = 0f; return; }

            bool tracking = state == State.Windup || state == State.Active;
            swingSpeed = tracking
                ? BladeDamager.SmoothSpeed(lastSwingPos, tip.position, Time.fixedDeltaTime, swingSpeed, swingSmoothing)
                : 0f;
            lastSwingPos = tip.position;
        }

        // ---- Shared helpers ----
        /// <summary>Begin a Windup. Resets the deflect-latch and runs the pre-attack hook.</summary>
        protected void BeginAttack()
        {
            OnBeginAttack();
            Log("telegraph");
            deflectedThisSwing = false;
            Enter(State.Windup);
            EventBus.Publish(new AttackWindupStarted(weapon != null ? weapon.position : transform.position, gameObject));
        }

        protected void Deflect(Vector3 point) => Deflect(point, null);

        /// <summary>
        /// Overload used when the incoming attack's <see cref="BladeDamager"/> is known — the normal
        /// parry path (Enemy/TrainingDummy resolve it while probing for a deflect). Same
        /// deflect/PerfectParry flow as <see cref="Deflect(Vector3)"/>, plus: if the attacker's blade
        /// was known and both blades were genuinely moving hard (see <see cref="ParryTiming.IsClash"/>),
        /// also publishes <see cref="BladeClash"/> — a true mutual clash, not just a well-timed parry
        /// against a slow swing. <paramref name="attackerBlade"/> null (e.g. <see cref="ForceStagger"/>'s
        /// posture-break path) skips the clash check entirely.
        /// </summary>
        protected void Deflect(Vector3 point, BladeDamager attackerBlade)
        {
            // Sunder Beat: only a deflect that lands while this enemy is actually Active (mid-swing) can
            // be "perfect" — Enemy also widens the parry window into late Windup, but pre-empting a
            // telegraph isn't a timing feat and shouldn't feed the streak.
            if (state == State.Active)
            {
                float quality = ParryTiming.ParryQuality(timer, PerfectParryWindow);
                if (quality > 0f) EventBus.Publish(new PerfectParry(point, gameObject, quality));
            }

            if (attackerBlade != null && ParryTiming.IsClash(attackerBlade.Speed, swingSpeed, ClashThreshold))
                EventBus.Publish(new BladeClash(point, gameObject));

            deflectedThisSwing = true;
            staggerFromEuler = currentEuler;
            Tint(StaggerColor);
            Log("DEFLECTED");
            EventBus.Publish(new SwordDeflected(point, gameObject));
            Enter(State.Stagger);
        }

        /// <summary>Test/system hook: force this enemy into Stagger as if its swing had just been
        /// deflected (e.g. a posture-meter break). Mirrors DuelYield's Force* hooks.</summary>
        public void ForceStagger() => Deflect(transform.position);

        protected void LandHit()
        {
            if (target == null || !target.IsAlive) return;
            Vector3 dir = (target.transform.position - transform.position).normalized;
            target.ApplyDamage(new DamageInfo(AttackDamage, target.transform.position, dir, gameObject));
            Log("hit player");
            EventBus.Publish(new PlayerHit(AttackDamage, target.transform.position));
        }

        protected virtual void OnDied()
        {
            // Route through Enter so the global aggro count is released the moment the enemy
            // dies (a dead-but-not-destroyed enemy must not keep the save gate locked).
            Enter(State.Dead);
            OnDeathPose();

            if (nonLethalDisable)
            {
                // Non-lethal: slumped/pinned pose with slate-blue tint.
                Tint(new Color(0.45f, 0.5f, 0.62f));
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 70f);
            }
            else
            {
                // Lethal: gray tint with topple pose.
                Tint(Color.gray);
                transform.rotation = Quaternion.Euler(85f, transform.eulerAngles.y, 0f);
            }
        }

        protected void Enter(State next) { state = next; timer = 0f; SetAggroCounted(next != State.Idle && next != State.Dead); }

        protected void PoseWeapon(Vector3 fromEuler, Vector3 toEuler, float t)
        {
            // Interpolate the angle directly so the blade always travels the intended arc
            // (overhead → forward → down) instead of Slerp's shortest path over the back.
            currentEuler = Vector3.Lerp(fromEuler, toEuler, Mathf.Clamp01(t));
            if (weapon != null) weapon.localRotation = Quaternion.Euler(currentEuler);
        }

        protected void FacePlayer()
        {
            if (target == null) return;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(to), FaceTurnRate * Time.deltaTime);
        }

        protected void SnapFacePlayer()
        {
            if (target == null) return;
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(to);
        }

        protected void Tint(Color c)
        {
            RendererTint.Apply(bodyRenderer, c);
            RendererTint.Apply(bladeRenderer, EnemySwordVisual.BladeTint(c));
        }

        protected static Health FindPlayer()
        {
            foreach (var h in Object.FindObjectsByType<Health>())
                if (h.GetComponent<CharacterController>() != null) return h;
            return null;
        }
    }
}
