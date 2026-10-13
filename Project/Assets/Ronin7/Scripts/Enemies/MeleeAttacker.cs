using System.Collections.Generic;
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
        [Tooltip("P1-14: 2-point LineRenderer from the weapon hand to the blade tip; shown only in Windup/Active.")]
        [SerializeField] private LineRenderer bladeEdge;

        [Header("Weapon poses (local euler) — overhead downward chop")]
        [SerializeField] protected Vector3 restEuler = new Vector3(15f, 0f, 0f);
        [SerializeField] protected Vector3 windupEuler = new Vector3(-140f, 0f, 0f);
        [SerializeField] protected Vector3 strikeEuler = new Vector3(55f, 0f, 0f);
        [SerializeField] protected Vector3 recoilEuler = new Vector3(-120f, 0f, 0f);

        [Header("Flinch (light stagger on any hit)")]
        [Tooltip("Seconds of recoil interrupt after taking damage (0.15-0.3 feels right).")]
        [SerializeField] private float flinchTime = 0.2f;
        [Tooltip("Seconds, measured from flinch start, during which further hits do not re-flinch " +
                 "(prevents perma-stunlock). Should be >= flinchTime.")]
        [SerializeField] private float reflinchImmunity = 0.4f;
        [Tooltip("Metres the enemy is shoved away from the hit over the flinch.")]
        [SerializeField] private float flinchPush = 0.12f;
        [Tooltip("Peak lean (degrees) of the whole body away from the hit. Visual only; no camera motion.")]
        [SerializeField] private float flinchTiltDegrees = 12f;
        [Tooltip("If true a hit during Windup cancels the attack (flinch then StateAfterStagger). If false " +
                 "the enemy ignores hits mid-windup (bosses / heavies). Hits during Active never flinch.")]
        [SerializeField] private bool flinchCancelsWindup = true;

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
        // HDR fresnel rim (GrittyCyber _RimColor): >1 pushes through Bloom so the windup reads on
        // textured art bodies, where a _BaseColor tint alone is barely visible.
        // Rim priority (P1-11): telegraph/stagger > Weakpoint Sight gold > faction idle. There is no
        // faction colour on MeleeAttacker yet, so idle rim is off.
        protected static readonly Color TelegraphRim = TelegraphColor * 2.5f;
        protected static readonly Color StaggerRim = StaggerColor * 2f;
        protected static readonly Color IdleRim = Color.black;
        protected static readonly Color SightRim = new Color(1f, 0.75f, 0.2f) * 1.5f;
        private const float FlashTime = 0.08f;

        // P1-14 telegraph grace: an attacker outside the player's +-ConeHalfAngle yaw cone gets +Grace s of warning.
        private const float ConeHalfAngle = 50f;
        private const float OffscreenGrace = 0.25f;

        /// <summary>Pure: telegraph length for an attacker <paramref name="angleDeg"/> off the player's forward.</summary>
        public static float TelegraphFor(float baseTime, float angleDeg)
            => Mathf.Abs(Mathf.DeltaAngle(0f, angleDeg)) > ConeHalfAngle ? baseTime + OffscreenGrace : baseTime;

        // Blade renderer under weapon, resolved once in Awake (see EnemySwordVisual.FindBladeRenderer).
        // Null for TrainingDummy/art-prefab rigs with no "Blade"-named child — RendererTint.Apply is
        // null-safe so Tint() stays a no-op for the blade in that case.
        private Renderer bladeRenderer;
        // Body material reads _RimColor/_FlashAmount (GrittyCyber). False for e.g. SamuraiToon bodies, whose
        // own material _RimColor must not be overridden by an MPB black.
        private bool bodyHasRim;
        private PlayerCombatModifiers playerMods; // resolved with target; read for the Sight gold rim
        private bool sightRimShown;
        private float flashTimer;
        private float telegraph; // this attack's telegraph (base + off-cone grace), set in BeginAttack
        private Color edgeColor = Color.white;

        protected Health health;
        protected State state = State.Idle;
        protected float timer;
        private float realTimer; // unscaled time in state: the perfect-parry window stays real-time under Overdrive
        protected bool deflectedThisSwing;
        protected Vector3 currentEuler;
        protected Vector3 staggerFromEuler;
        private bool aggroCounted;

        // Stagger duration in use: StaggerTime for a deflect/posture break, flinchTime for a flinch.
        private float staggerLen;
        private bool flinching;
        private float flinchImmuneUntil;
        private Vector3 flinchDir, flinchStartPos;
        private Quaternion flinchBaseRot;

        // G4 Blade Clash: EMA-smoothed speed of SwingTrackPoint, using BladeDamager's exact math.
        private Vector3 lastSwingPos;
        private float swingSpeed;

        // ---- Per-subclass tuning (sourced from SO or inline fields) ----
        protected abstract float TelegraphTime { get; }
        /// <summary>Telegraph of the attack in progress (parry window = 0.75 x this).</summary>
        protected float CurrentTelegraph => telegraph;
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

        /// <summary>True while Update is driving <see cref="weapon"/> (Windup..Stagger). NpcWalkAnimator
        /// reads this so it doesn't overwrite the shared Rig_ArmR bone in LateUpdate.</summary>
        public bool IsPosingWeapon =>
            state == State.Windup || state == State.Active || state == State.Recover || state == State.Stagger;

        /// <summary>State to land in once Recover completes.</summary>
        protected virtual State StateAfterRecover => State.Idle;

        /// <summary>State to land in once Stagger completes.</summary>
        protected virtual State StateAfterStagger => State.Idle;

        /// <summary>Slerp-toward-player rate for <see cref="FacePlayer"/>. Default 8 (Enemy);
        /// TrainingDummy overrides to 5 for a slower, less twitchy track.</summary>
        protected virtual float FaceTurnRate => 8f;

        /// <summary>Whether this enemy flinches on damage. Off by default (TrainingDummy stays put);
        /// <see cref="Enemy"/> turns it on.</summary>
        protected virtual bool FlinchEnabled => false;
        /// <summary>Whether a Windup hit cancels the attack. Super-armor enemies override to false.</summary>
        protected virtual bool FlinchCancelsWindup => flinchCancelsWindup;

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
            health.Damaged += OnDamagedFlinch;
            if (target == null) target = FindPlayer();
            if (target != null) playerMods = target.GetComponentInParent<PlayerCombatModifiers>();
            var bodyMat = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            bodyHasRim = bodyMat != null && bodyMat.HasProperty("_RimColor") && bodyMat.HasProperty("_FlashAmount");
            currentEuler = restEuler;
            if (weapon != null) weapon.localRotation = Quaternion.Euler(restEuler);
            EnemySwordVisual.EnsureVisible(weapon, transform); // placeholder katana so the chop has something to swing
            bladeRenderer = EnemySwordVisual.FindBladeRenderer(weapon);
            telegraph = TelegraphTime;
            if (bladeEdge != null) bladeEdge.enabled = false;
            Tint(IdleColor);
        }

        protected virtual void OnDisable()
        {
            SetAggroCounted(false);
        }

        protected virtual void OnDestroy()
        {
            if (health != null) { health.Died -= OnDied; health.Damaged -= OnDamagedFlinch; }
            SetAggroCounted(false);
        }

        protected virtual void Update()
        {
            // Hit flash 1 -> 0 over FlashTime; ticks before the Dead return so a killing blow still fades.
            if (flashTimer > 0f)
            {
                flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime);
                if (bodyHasRim) RendererTint.Flash(bodyRenderer, flashTimer / FlashTime);
            }

            if (state == State.Dead) return;
            if (target == null)
            {
                target = FindPlayer();
                if (target == null) return;
                playerMods = target.GetComponentInParent<PlayerCombatModifiers>();
            }

            timer += Time.deltaTime;
            realTimer += Time.unscaledDeltaTime;
            BeforeStateTick();

            switch (state)
            {
                case State.Idle:
                    RefreshSightRim();
                    OnIdleTick();
                    break;

                case State.Chase:
                    RefreshSightRim();
                    OnChaseTick();
                    break;

                case State.Windup:
                    PoseWeapon(restEuler, windupEuler, timer / telegraph);
                    Tint(Color.Lerp(IdleColor, TelegraphColor, timer / telegraph),
                         Color.Lerp(RestRim, TelegraphRim, timer / telegraph));
                    if (timer >= telegraph)
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
                    Tint(Color.Lerp(TelegraphColor, IdleColor, timer / RecoverTime),
                         Color.Lerp(TelegraphRim, RestRim, timer / RecoverTime));
                    if (timer >= RecoverTime) Enter(StateAfterRecover);
                    break;

                case State.Stagger:
                    if (timer < staggerLen * 0.35f)
                        PoseWeapon(staggerFromEuler, recoilEuler, timer / (staggerLen * 0.35f));
                    else
                        PoseWeapon(recoilEuler, restEuler,
                            (timer - staggerLen * 0.35f) / (staggerLen * 0.65f));
                    Tint(Color.Lerp(StaggerColor, IdleColor, timer / staggerLen),
                         Color.Lerp(StaggerRim, RestRim, timer / staggerLen));
                    if (flinching) PoseFlinch(timer / staggerLen);
                    if (timer >= staggerLen) Enter(StateAfterStagger);
                    break;
            }
        }

        // After all pose/animator writes, so the edge tracks the real blade tip. Only runs the work while visible.
        private void LateUpdate()
        {
            if (bladeEdge == null || !bladeEdge.enabled) return;
            Transform tip = SwingTrackPoint;
            if (weapon == null || tip == null) return;
            bladeEdge.SetPosition(0, weapon.position);
            bladeEdge.SetPosition(1, tip.position);
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
                ? BladeDamager.SmoothSpeed(lastSwingPos, tip.position, BladeDamager.RealFixedDt(Time.fixedDeltaTime, Time.timeScale), swingSpeed, swingSmoothing)
                : 0f;
            lastSwingPos = tip.position;
        }

        // ---- Shared helpers ----
        /// <summary>Begin a Windup. Resets the deflect-latch and runs the pre-attack hook.</summary>
        protected void BeginAttack()
        {
            OnBeginAttack();
            telegraph = TelegraphFor(TelegraphTime, AngleFromPlayerForward());
            Log("telegraph");
            deflectedThisSwing = false;
            Enter(State.Windup);
            EventBus.Publish(new AttackWindupStarted(weapon != null ? weapon.position : transform.position, gameObject));
        }

        /// <summary>Yaw angle between the player's view direction and this enemy (0 when unknown).</summary>
        private float AngleFromPlayerForward()
        {
            if (target == null) return 0f;
            var cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : target.transform.forward;
            Vector3 to = transform.position - target.transform.position;
            fwd.y = 0f; to.y = 0f;
            return fwd.sqrMagnitude < 1e-4f || to.sqrMagnitude < 1e-4f ? 0f : Vector3.SignedAngle(fwd, to, Vector3.up);
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
                float quality = ParryTiming.ParryQuality(realTimer, PerfectParryWindow);
                if (quality > 0f) EventBus.Publish(new PerfectParry(point, gameObject, quality));
            }

            if (attackerBlade != null && ParryTiming.IsClash(attackerBlade.Speed, swingSpeed, ClashThreshold))
                EventBus.Publish(new BladeClash(point, gameObject));

            deflectedThisSwing = true;
            EndFlinchPose();
            staggerLen = StaggerTime;
            staggerFromEuler = currentEuler;
            Tint(StaggerColor, StaggerRim);
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
            EventBus.Publish(new PlayerHit(AttackDamage, target.transform.position, transform.position));
        }

        /// <summary>
        /// Light stagger on any damage: reuses State.Stagger with flinchTime, shoves the body away
        /// from the hit and leans it back. A Windup hit cancels the attack (-> StateAfterStagger)
        /// unless flinchCancelsWindup is off; Active is never interrupted. See <see cref="FlinchTiming"/>.
        /// </summary>
        private void OnDamagedFlinch(DamageInfo info)
        {
            flashTimer = FlashTime; // P1-11 hit flash, every damaging hit (also on non-flinching enemies)
            if (!FlinchEnabled) return;
            if (!FlinchTiming.ShouldFlinch(health.IsAlive, state == State.Stagger, state == State.Active,
                    state == State.Windup, FlinchCancelsWindup, Time.time, flinchImmuneUntil)) return;

            flinchImmuneUntil = Time.time + reflinchImmunity;

            Vector3 dir = info.Direction; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f && target != null)
            {
                dir = transform.position - target.transform.position; dir.y = 0f;
            }
            flinchDir = dir.sqrMagnitude < 1e-4f ? -transform.forward : dir.normalized;
            flinchStartPos = transform.position;
            flinchBaseRot = transform.rotation;
            flinching = true;

            staggerLen = Mathf.Max(0.01f, flinchTime);
            staggerFromEuler = currentEuler;
            Tint(StaggerColor, StaggerRim);
            Enter(State.Stagger);
        }

        private void PoseFlinch(float t)
        {
            t = Mathf.Clamp01(t);
            transform.position = flinchStartPos + flinchDir * (flinchPush * (1f - (1f - t) * (1f - t)));
            // Lean the body toward flinchDir; sine envelope returns to the base rotation at t = 1.
            transform.rotation = Quaternion.AngleAxis(flinchTiltDegrees * FlinchTiming.TiltEnvelope(t),
                Vector3.Cross(Vector3.up, flinchDir)) * flinchBaseRot;
        }

        private void EndFlinchPose()
        {
            if (!flinching) return;
            flinching = false;
            transform.rotation = flinchBaseRot;
        }

        protected virtual void OnDied()
        {
            flinching = false;
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

        protected void Enter(State next)
        {
            if (bladeEdge != null) bladeEdge.enabled = next == State.Windup || next == State.Active;
            EnterCore(next);
        }

        private void EnterCore(State next) { state = next; timer = 0f; realTimer = 0f; SetAggroCounted(next != State.Idle && next != State.Dead); }

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

        protected void Tint(Color c) => Tint(c, state == State.Dead ? IdleRim : RestRim);

        /// <summary>Body base tint plus HDR rim; the blade takes the base tint only.</summary>
        protected void Tint(Color c, Color rim)
        {
            if (bodyHasRim) RendererTint.Apply(bodyRenderer, c, rim);
            else RendererTint.Apply(bodyRenderer, c);
            RendererTint.Apply(bladeRenderer, EnemySwordVisual.BladeTint(c));
            if (bladeEdge != null && bladeEdge.enabled)
            {
                edgeColor = c;
                bladeEdge.startColor = bladeEdge.endColor = edgeColor;
            }
        }

        private bool SightOn => playerMods != null && playerMods.WeakpointSightActive;

        /// <summary>Rim when no telegraph/stagger owns it: Sight gold, else the idle rim.</summary>
        private Color RestRim => SightOn ? SightRim : IdleRim;

        /// <summary>Idle/Chase: re-tint only when Weakpoint Sight flips, not every frame.</summary>
        private void RefreshSightRim()
        {
            if (SightOn == sightRimShown) return;
            sightRimShown = SightOn;
            Tint(IdleColor, RestRim);
        }

        protected static Health FindPlayer() => FindPlayer(Health.Active);

        // ponytail: player = the Health with a CharacterController; if a second player-like
        // Health ever appears, tag it instead of widening this check.
        internal static Health FindPlayer(IReadOnlyList<Health> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].TryGetComponent<CharacterController>(out _)) return candidates[i];
            return null;
        }
    }
}
