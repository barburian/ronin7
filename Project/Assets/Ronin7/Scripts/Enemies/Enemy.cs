using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Ground-melee enemy. Approaches the player on flat ground (Chase state), then runs the
    /// shared Windup → Active → Recover chop from <see cref="MeleeAttacker"/>. Parry probe is
    /// a capsule between <c>weapon</c> and <c>bladeTip</c>; the parry window also covers the
    /// late part of Windup (≥ 0.75 of telegraphTime) so a brave swing can interrupt the chop.
    /// </summary>
    public class Enemy : MeleeAttacker
    {
        [Header("Refs (Enemy)")]
        [SerializeField] private EnemyDefinition definition;
        [SerializeField] private Transform bladeTip;
        private Transform bladeBase; // P8-03: composed weapon's edgeBase; the parry capsule runs bladeBase..bladeTip

        [Header("Parry probe")]
        [SerializeField] private float parryRadius = 0.12f;

        [SerializeField] private bool debugLogs = false;

        [Header("BUG-39 weak-point markers (baked by EnemyPrefabBaker)")]
        [Tooltip("Alternating: [0] left, [1] right shoulder. Backstab: [0] back plate. PostureOnly: [0] crack bar.")]
        [SerializeField] private Renderer[] weakMarks;

        private static readonly Collider[] _bladeHits = new Collider[4];

        private float groundY;
        protected float nextAttackTime;
        private float chaseSpeed; // P2-11: ramped walk speed, m/s (0 outside Chase)

        // P3-02 weakness wiring
        private PostureMeter posture;
        private int lastHitSide; // 0 = no previous hit
        private float lastHitTime = float.NegativeInfinity;
        private float lastBlinkTime = float.NegativeInfinity;
        // ponytail: per-hit latch; move to a DamageInfo flag if a 2nd consumer appears
        private bool resistedThisHit;

        // P3-04 displacement + attack chain
        private float arenaHalfExtent; // 0 = unbounded (set via Configure(def, halfExtent))
        private Vector3 displaceVel;
        private float displaceLeft;
        private int chainDone;
        private float chainDeadline = float.NegativeInfinity;
        private const float ChainGrace = 0.5f; // s after Recover begins in which the next chain hit may start
        internal const float ArenaWallMargin = 0.5f;

        protected float ArenaHalfExtent => arenaHalfExtent;

        protected EnemyDefinition Definition => definition;

        /// <summary>P5-03: drives the hit-spark look (EnemyDefinition.materialClass).</summary>
        public MaterialClass VfxClass => definition != null ? definition.materialClass : MaterialClass.Organic;

        // P3-07 feints: read once per windup (BeginAttack), so each draw advances the seeded stream.
        protected override float TelegraphTime => definition.telegraphMaxTime > definition.telegraphTime
            ? Mathf.Lerp(definition.telegraphTime, definition.telegraphMaxTime, telegraphRng.NextFloat())
            : definition.telegraphTime;
        private RunRng telegraphRng = new RunRng(1);

        /// <summary>P3-07: seeds the randomized-telegraph stream from the run (deterministic per node).</summary>
        public void SeedTelegraph(uint seed) => telegraphRng = new RunRng(seed);
        protected override float ActiveTime => definition.activeTime;
        protected override float RecoverTime => definition.recoverTime;
        protected override float StaggerTime => definition.staggerTime;
        protected override Color EliteRim => definition.eliteRim;
        protected override float LifestealFraction => definition.vampiric ? 0.5f : 0f;
        protected override float AttackDamage => definition.ScaledDamage(CampaignState.GalaxiesCompleted);

        // After Recover/Stagger, drop back into Chase rather than Idle — the Idle state's
        // 0.2s settle gate is meant for the very first spawn only.
        // P3-02 / D-07: super-armor types never flinch; a resisted hit never flinches either.
        protected override bool FlinchEnabled => !definition.superArmor && !ConsumeResisted();
        protected override bool FlinchCancelsWindup => !definition.superArmor && base.FlinchCancelsWindup;

        protected override float PoseArmScale => definition.poseArmScale;
        protected override float PoseTwistDegrees => definition.poseTwistDegrees;
        protected override float PoseLeanDegrees => definition.poseLeanDegrees;
        protected override bool PoseWholeBody => definition.poseWholeBody;
        protected override float MaxTurnDegreesPerSecond => definition.turnRateDegrees;

        // BUG-39 tells: gold rim while the state-based weakness is open, blue-grey rest rim for guarded kinds.
        protected override bool WeakOpen => definition != null && WeaknessRules.IsOpen(definition.weakness,
            state == State.Stagger && !IsFlinching, state == State.Windup || state == State.Active,
            state == State.Recover, Time.time - lastBlinkTime < WeaknessRules.BlinkWindow);
        protected override bool WeakGuarded => definition != null && WeaknessRules.RestsGuarded(definition.weakness);

        /// <summary>BUG-39: this enemy's current weakness rule (first-encounter teach key).</summary>
        public WeaknessKind Weakness => definition != null ? definition.weakness : WeaknessKind.None;

        protected override State StateAfterRecover => State.Chase;
        protected override State StateAfterStagger => State.Chase;

        // Parry window includes late Windup so a player can pre-empt the chop.
        protected override bool IsInParryWindow()
            => state == State.Active
            || (state == State.Windup && timer >= CurrentTelegraph * 0.75f);

        // bladeTip is the point that actually sweeps through the chop (weapon is the static pivot) —
        // the same point already used for the parry capsule below, so it doubles as the G4 Blade
        // Clash swing-speed tracker's sample point.
        protected override Transform SwingTrackPoint => bladeTip != null ? bladeTip : weapon;
        // P8-04: ranged kits author edgeBase == tip, so their edge runs from the weapon root (barrel / bow) to the tip.
        protected override Transform EdgeBase
        {
            get
            {
                if (bladeTip == null || bladeBase == null) return weapon;
                bool flat = (bladeBase.position - bladeTip.position).sqrMagnitude < 1e-4f;
                return flat && bladeTip.parent != null ? bladeTip.parent : bladeBase;
            }
        }

        protected override void Awake()
        {
            if (definition == null)
            {
                Debug.LogWarning("[Enemy] No EnemyDefinition assigned — using default stats.", this);
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            }
            base.Awake();
            posture = GetComponent<PostureMeter>();
            GrantShieldGuard();
            ApplyDefinition();
            if (bladeTip == null)
                Debug.LogError("[Enemy] bladeTip not wired — parry detection disabled.", this);
            groundY = transform.position.y;
            InitWeakMarks();
        }

        // ---- BUG-39 weak-point markers ----
        private const float SlabFlickerSeconds = 0.12f, FlareSeconds = 0.2f;
        private static readonly Color OpenMarkColor = OpenColor * 2f;   // HDR: reads at 3-8 m in the dark arena
        private static readonly Color GuardMarkColor = GuardColor * 1.5f;
        private Vector3[] markBaseScale; // [i] = weakMarks[i]; [Length] = ArchetypeMark
        private float slabFlickerUntil = -1f;
        private float fxStart, fxEnd = -1f;
        private bool fxPulse; // true: first-encounter pulse, false: exploit flare
        private bool crackGold;

        private void InitWeakMarks()
        {
            int n = weakMarks != null ? weakMarks.Length : 0;
            markBaseScale = new Vector3[n + 1];
            for (int i = 0; i < n; i++)
            {
                if (weakMarks[i] == null) continue;
                weakMarks[i].sharedMaterial = VrUiMaterials.SharedUnlit;
                markBaseScale[i] = weakMarks[i].transform.localScale;
                RendererTint.Apply(weakMarks[i], definition.weakness == WeaknessKind.PostureOnly ? GuardMarkColor : OpenMarkColor);
                // Alternating shoulders only show while open; Backstab plate / Construct crack are always on.
                weakMarks[i].enabled = definition.weakness != WeaknessKind.Alternating;
            }
            if (ArchetypeMark != null) markBaseScale[n] = ArchetypeMark.transform.localScale;
        }

        /// <summary>BUG-39 first-encounter cue: pulse the weak-point marker (the archetype mark when there
        /// is none) for <paramref name="seconds"/> real time.</summary>
        public void PulseWeakPoint(float seconds) { fxPulse = true; fxStart = Time.unscaledTime; fxEnd = fxStart + seconds; }

        private void FlareWeakPoint() { fxPulse = false; fxStart = Time.unscaledTime; fxEnd = fxStart + FlareSeconds; }

        /// <summary>Per frame, allocation-free: slab guard state, shoulder side, crack growth, pulse/flare scale.</summary>
        private void TickWeakMarks()
        {
            if (state == State.Dead || markBaseScale == null) return;
            var kind = definition.weakness;
            bool broken = state == State.Stagger && !IsFlinching;
            Renderer slab = ArchetypeMark;

            // HeavyBreak: the archetype slab IS the guard arc. Up (archetype cyan) = guarded; a resisted
            // slow hit flickers it blue-grey; a heavy-break stagger hides it ("shattered") until recovery.
            if (kind == WeaknessKind.HeavyBreak && slab != null)
            {
                if (slab.enabled == broken) slab.enabled = !broken;
                if (weakSpot != null && weakSpot.enabled == broken) weakSpot.enabled = !broken;
                if (slabFlickerUntil > 0f && Time.unscaledTime >= slabFlickerUntil)
                {
                    slabFlickerUntil = -1f;
                    if (weakSpot != null) RendererTint.Apply(weakSpot, OpenMarkColor);
                    else RendererTint.Apply(slab, ArchetypeColor);
                }
            }

            int n = weakMarks != null ? weakMarks.Length : 0;
            float fx = MarkFxScale();
            if (kind == WeaknessKind.Alternating && n >= 2)
            {
                // Gold on the shoulder opposite the last hit while the alternating window is live.
                int open = lastHitSide != 0 && Time.time - lastHitTime < WeaknessRules.AlternatingWindow ? -lastHitSide : 0;
                bool pulsing = fxEnd > 0f && fxPulse;
                SetMark(0, open < 0 || pulsing);
                SetMark(1, open > 0 || pulsing);
            }
            else if (kind == WeaknessKind.PostureOnly && n >= 1 && weakMarks[0] != null)
            {
                // Crack grows with posture; flares gold at the break (stagger).
                if (broken != crackGold) { crackGold = broken; RendererTint.Apply(weakMarks[0], broken ? OpenMarkColor : GuardMarkColor); }
                float f = posture != null ? posture.Fraction : 0f;
                Vector3 b = markBaseScale[0];
                weakMarks[0].transform.localScale = new Vector3(b.x * Mathf.Lerp(0.15f, 1f, broken ? 1f : f) * fx, b.y * fx, b.z * fx);
                if (fxEnd > 0f && Time.unscaledTime >= fxEnd) fxEnd = -1f;
                return;
            }

            if (fxEnd < 0f) return;
            if (n > 0) for (int i = 0; i < n; i++) ScaleMark(weakMarks[i], markBaseScale[i], fx);
            else ScaleMark(slab, markBaseScale[n], fx);
            if (Time.unscaledTime >= fxEnd) fxEnd = -1f; // fx was 1 this frame: scales are back at rest
        }

        private float MarkFxScale()
        {
            if (fxEnd < 0f) return 1f;
            float now = Time.unscaledTime;
            if (now >= fxEnd) return 1f;
            float t = now - fxStart;
            return fxPulse ? 1f + 0.4f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * AccessibilityOptions.WeakPointBeatHz(GameSettings.Active.ReduceFlashes))) // 3 beats a second (1.5 with Reduce Flashes)
                           : 1f + 0.5f * (1f - t / (fxEnd - fxStart));         // pop, then settle
        }

        private void SetMark(int i, bool on)
        {
            var r = weakMarks[i];
            if (r != null && r.enabled != on) r.enabled = on;
        }

        private static void ScaleMark(Renderer r, Vector3 baseScale, float k)
        {
            if (r != null) r.transform.localScale = baseScale * k;
        }

        /// <summary>Health, posture and weakness filter from <see cref="definition"/>. Awake, and again on
        /// every boss phase swap (P3-07), so the per-hit weakness latches start fresh for the new rule.</summary>
        private void ApplyDefinition()
        {
            health.Configure(definition.ScaledMaxHealth(CampaignState.GalaxiesCompleted));
            if (definition.postureMaxFraction > 0f && posture != null)
                posture.Configure(definition.PostureMax(CampaignState.GalaxiesCompleted), definition.postureGainMultiplier);
            if (definition.postureBreakBonusFraction > 0f && posture != null)
                posture.BreakBonusDamage = health.Max * definition.postureBreakBonusFraction; // FilterDamage passes ApplyingBonus through unchanged
            health.DamageFilter = definition.weakness != WeaknessKind.None ? FilterDamage : null; // one delegate per spawn/phase
            resistedThisHit = false;
            lastHitSide = 0;
            lastHitTime = float.NegativeInfinity;
            lastBlinkTime = float.NegativeInfinity;
            if (posture != null) posture.NextHitGainScale = 1f;
        }

        // ---- P3-07 boss phases ----
        /// <summary>Stagger held at every phase swap.</summary>
        public const float PhaseStaggerSeconds = 2f;
        private EnemyDefinition[] nextPhases;
        private int phase; // 0 = first phase; also the count of phases consumed from nextPhases

        /// <summary>P3-07: queue the definitions that replace this one, in order, each time health hits 0.
        /// The boss only dies (Died/EntityDied) after the last one is spent.</summary>
        public void ConfigurePhases(EnemyDefinition[] next)
        {
            nextPhases = next;
            phase = 0;
            Health h = GetComponent<Health>(); // may run before Awake (A3.1 inactive spawn)
            h.DeathInterceptor = AdvancePhase;
            h.ReviveFraction = 1f;
        }

        /// <summary>P3-07 death interceptor: false once no phases remain (the blow kills); otherwise swaps to
        /// the next definition, refills health/posture, re-arms the weakness filter, holds a 2 s stagger and
        /// publishes <see cref="BossPhaseChanged"/> (adds are the arena's job, never the boss's — A5.1).</summary>
        internal bool AdvancePhase()
        {
            if (nextPhases == null || phase >= nextPhases.Length) return false;
            definition = nextPhases[phase++];
            ApplyDefinition();
            ForceStagger(PhaseStaggerSeconds);
            EventBus.Publish(new BossPhaseChanged(gameObject, phase));
            return true;
        }

        // FacePlayer every frame while engaging or telegraphing — NOT during Active/Recover/
        // Stagger, so the body locks orientation during the chop animation. Matches the
        // original per-case FacePlayer placement.
        protected override void BeforeStateTick()
        {
            if (state == State.Idle || state == State.Chase || state == State.Windup)
                FacePlayer();
            if (state != State.Chase)
            {
                hoverWant = Vector3.zero;
                CoastOrStop(); // every Chase entry ramps up from a standstill
            }
        }

        // ---- P3-09 (BUG-38) ----
        private Vector3 hoverVel, hoverWant;
        private float hoverBank, bobPhase;
        private bool hovering;
        private bool IsBoss => nextPhases != null;

        /// <summary>Drones: ask to move along <paramref name="dir"/> (flat, unit or zero) at moveSpeed. The velocity
        /// eases toward it (no instant reversals) and the rig banks/bobs; the root stays on the ground latch.</summary>
        protected void Hover(Vector3 dir)
        {
            if (!hovering) { hovering = true; bobPhase = (transform.position.x * 7.3f + transform.position.z * 3.1f); }
            hoverWant = dir * definition.moveSpeed;
        }

        private void HoverTick(float dt)
        {
            Vector3 prev = hoverVel;
            hoverVel = MovementSmoothing.StepVelocity(hoverVel, hoverWant, definition.acceleration, definition.deceleration, dt);
            if (hoverVel.sqrMagnitude > 1e-6f) Displace(hoverVel * dt, 0f); // arena-clamped, ground-latched
            float lateral = dt > 0f ? Vector3.Dot((hoverVel - prev) / dt, transform.right) : 0f;
            hoverBank = MovementSmoothing.StepEase(hoverBank, MovementSmoothing.BankTarget(lateral), MovementSmoothing.BankEase, dt);
            PoseHover(hoverBank, MovementSmoothing.HoverBob(Time.time, bobPhase));
        }

        // Bosses carry momentum into the windup (braking at a heavy decel) instead of stopping dead;
        // stagger and death still stop at once. Everyone else keeps the old "ramp up from a standstill".
        private void CoastOrStop()
        {
            if (!IsBoss || chaseSpeed <= 0f || state == State.Stagger || state == State.Dead) { chaseSpeed = 0f; return; }
            float dt = Time.deltaTime;
            chaseSpeed = MovementSmoothing.StepSpeed(chaseSpeed, 0f, definition.acceleration,
                definition.deceleration * MovementSmoothing.BossBrakeScale, dt);
            Displace(transform.forward * (chaseSpeed * dt), 0f);
        }

        protected override void OnIdleTick()
        {
            // Brief settle on spawn before chasing (original used a dedicated Idle→Chase
            // transition gated on timer > 0.2s; same effect here).
            if (timer > 0.2f) Enter(State.Chase);
        }

        protected override void Update()
        {
            base.Update();
            TickWeakMarks();
            if (hovering && state != State.Dead) HoverTick(Time.deltaTime);
            if (displaceLeft <= 0f) return;
            if (state == State.Dead || state == State.Stagger) { displaceLeft = 0f; return; } // a deflected lunge stops
            float dt = Mathf.Min(Time.deltaTime, displaceLeft);
            displaceLeft -= dt;
            Vector3 pos = transform.position + displaceVel * dt;
            pos.y = groundY;
            transform.position = pos;
        }

        protected override void OnBeginAttack()
        {
            if (Time.time > chainDeadline) chainDone = 0; // a broken chain (parry/flinch) starts fresh
        }

        protected override void OnChaseTick()
        {
            if (Time.time <= chainDeadline) { BeginAttack(); chainDeadline = float.NegativeInfinity; return; } // next hit of a chain (clear AFTER: OnBeginAttack keeps chainDone while the deadline is live)
            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            bool ranged = NextStrike().Ranged;
            float range = ranged ? KiteMax : definition.attackRange; // P8-02c: shooters hold off at KiteMax
            if (ranged && dist < KiteMin && Time.time >= nextBackstep && state == State.Chase)
            {
                nextBackstep = Time.time + 1.5f;
                Displace(-to / Mathf.Max(dist, 1e-3f) * 1.5f, 0.35f);
            }

            if (dist > range)
            {
                // P2-11: walk where the body faces (turn first, no sideways/backwards slide), ramp the
                // speed up and brake into attackRange instead of jumping between full speed and zero.
                Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
                float align = IsBoss // P3-09: bosses turn in place before travelling
                    ? MovementSmoothing.TurnGate(Mathf.DeltaAngle(transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg))
                    : Mathf.Clamp01(Vector3.Dot(fwd, to / dist));
                float decel = IsBoss ? definition.deceleration * MovementSmoothing.BossBrakeScale : definition.deceleration;
                // Brake for the gap left after this frame's step, or the discrete ramp lags and stops hard.
                float want = align * MovementSmoothing.ArrivalSpeed(definition.moveSpeed,
                    dist - range - chaseSpeed * Time.deltaTime, decel);
                chaseSpeed = MovementSmoothing.StepSpeed(chaseSpeed, want,
                    definition.acceleration, decel, Time.deltaTime);
                Vector3 pos = transform.position + fwd * (chaseSpeed * Time.deltaTime);
                pos.y = groundY;
                transform.position = pos;
            }
            else
            {
                chaseSpeed = 0f; // arrival braking already brought it to ~0 here
                if (Time.time >= nextAttackTime) BeginAttack();
            }
        }

        protected override void TryDetectParry()
        {
            if (!CurrentStrike.Parryable) return; // P8-03: ranged and DODGE strikes ignore a blade parry
            if (bladeTip == null || weapon == null) return;

            int n = Physics.OverlapCapsuleNonAlloc(ParryCapsuleStart, bladeTip.position,
                parryRadius, _bladeHits, Layers.BladeMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var blade = _bladeHits[i].GetComponentInParent<BladeDamager>();
                if (blade == null) continue;
                Deflect(bladeTip.position, blade);
                return;
            }
        }

        /// <summary>P8-03: blade only - the composed weapon's edgeBase (the arm pivot for prefab-baked kits).</summary>
        internal Vector3 ParryCapsuleStart => bladeBase != null ? bladeBase.position : weapon.position;

        // +0.4 slop so a player who's just slipped out of range during Active still eats the hit.
        protected override bool IsInLandHitRange()
            => target != null && !CurrentStrike.Ranged
            && (target.transform.position - transform.position).sqrMagnitude
               <= (definition.attackRange + 0.4f) * (definition.attackRange + 0.4f);

        /// <summary>P8-02c: wired by ProjectilePool (Enemies cannot see Ship): owner, muzzle, rotation, speed, damage, life, radius, colour, gravity.</summary>
        public static System.Action<GameObject, Vector3, Quaternion, float, float, float, float, Color, float> FireProjectile;
        public const float KiteMin = 5f, KiteMax = 7f;
        private float nextBackstep;

        /// <summary>Pure: unit launch direction from <paramref name="from"/> that lands on <paramref name="to"/> at
        /// <paramref name="speed"/> under <paramref name="gravity"/> (low-angle arc; 45 degrees when out of range).</summary>
        public static Vector3 BallisticDirection(Vector3 from, Vector3 to, float speed, float gravity)
        {
            Vector3 d = to - from;
            if (gravity <= 0f) return d.normalized;
            Vector3 flat = new Vector3(d.x, 0f, d.z);
            float x = flat.magnitude;
            if (x < 1e-3f) return d.y >= 0f ? Vector3.up : Vector3.down;
            float v2 = speed * speed, disc = v2 * v2 - gravity * (gravity * x * x + 2f * d.y * v2);
            float theta = disc < 0f ? Mathf.PI / 4f : Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (gravity * x));
            return flat / x * Mathf.Cos(theta) + Vector3.up * Mathf.Sin(theta);
        }

        /// <summary>Fires <paramref name="s"/> as a bolt at the player's head from the weapon tip; no-op for melee or without a pool.</summary>
        internal void FireStrike(Strike s)
        {
            if (!s.Ranged || FireProjectile == null) return;
            Vector3 muzzle = bladeTip != null ? bladeTip.position : weapon != null ? weapon.position : transform.position + Vector3.up;
            var cam = Camera.main;
            Vector3 head = cam != null ? cam.transform.position
                : target != null ? target.transform.position + Vector3.up * 1.6f : muzzle + transform.forward * 5f;
            Vector3 dir = BallisticDirection(muzzle, head, s.boltSpeed, s.boltGravity);
            FireProjectile(gameObject, muzzle, Quaternion.LookRotation(dir), s.boltSpeed, AttackDamage, 4f, s.boltRadius, OpenColor, s.boltGravity); // BUG-39: gold = "send it back" (Reflect)
        }

        protected override void OnActiveEnd()
        {
            if (CurrentStrike.Ranged) FireStrike(CurrentStrike);
            if (ComboPending) { chainDeadline = Time.time + StrikeRecover + ChainGrace; return; } // P8-03: next strike of the pattern
            if (CurrentStrike.Ranged) { nextAttackTime = Time.time + AccessibilityOptions.Cooldown(definition.attackCooldown, GameSettings.Active.EnemyCooldownScale); return; }
            if (++chainDone < definition.chainHits) { chainDeadline = Time.time + RecoverTime + ChainGrace; return; }
            chainDone = 0;
            nextAttackTime = Time.time + AccessibilityOptions.Cooldown(definition.attackCooldown, GameSettings.Active.EnemyCooldownScale); // P6-03 assist
        }

        /// <summary>P3-04: slide the body by <paramref name="delta"/> (flattened) over <paramref name="seconds"/>
        /// (0 = instant), on the spawn-height latch and clamped to the arena (halfExtent - 0.5 m).
        /// A stagger or death cancels a timed slide. Arena is assumed centred on the world origin (A5.3).</summary>
        public void Displace(Vector3 delta, float seconds)
        {
            delta.y = 0f;
            Vector3 start = transform.position;
            Vector3 end = ClampToArena(start + delta, arenaHalfExtent);
            end.y = groundY;
            if (seconds <= 0f) { transform.position = end; displaceLeft = 0f; return; }
            displaceVel = (end - start) / seconds;
            displaceVel.y = 0f;
            displaceLeft = seconds;
        }

        /// <summary>Pure: clamp x/z into +-(halfExtent - 0.5). halfExtent &lt;= 0 means unbounded.</summary>
        internal static Vector3 ClampToArena(Vector3 p, float halfExtent)
        {
            if (halfExtent <= 0f) return p;
            float lim = Mathf.Max(0f, halfExtent - ArenaWallMargin);
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            return p;
        }

        private bool ConsumeResisted()
        {
            bool r = resistedThisHit;
            resistedThisHit = false;
            return r;
        }

        /// <summary>Hook for the Blink archetype (P3-04): call right after teleporting.</summary>
        protected void MarkBlinked() => lastBlinkTime = Time.time;

        /// <summary>P3-02: Health.DamageFilter. Applies the definition's weakness rule to one hit.</summary>
        private float FilterDamage(DamageInfo info)
        {
            if (posture != null && posture.ApplyingBonus) return info.Amount; // posture-break bonus: not a new hit
            if (info.Type == DamageType.Status) return info.Amount; // P4-05 procs ignore facing (no resist/exploit events)

            Transform t = transform;
            GameObject src = info.Source;
            PlayerCombatModifiers mods = null;
            bool isPlayer = src != null && src.TryGetComponent(out mods);
            bool sight = isPlayer && mods.WeakpointSightActive;
            bool isAlly = src != null && src.TryGetComponent<AllyCombatant>(out _);
            int side = WeaknessRules.Side(t.position, t.forward, info.Point);
            float now = Time.time;
            bool staggered = state == State.Stagger && !IsFlinching;
            // Melee: hit point, not the source (player rig root can be metres from the body under roomscale); others: source position.
            Vector3 srcPos = info.Type == DamageType.Melee || src == null ? info.Point : src.transform.position;
            var ctx = new WeaknessContext(t.position, t.forward, srcPos,
                info.Point, info.Speed, info.Type, isPlayer, isAlly, sight,
                staggered: staggered,
                attacking: state == State.Windup || state == State.Active,
                recovering: state == State.Recover,
                recentlyBlinked: now - lastBlinkTime < WeaknessRules.BlinkWindow,
                side, lastHitSide, now - lastHitTime,
                onWeakSpot: weakSpot != null && weakSpot.enabled && WeaknessRules.OnWeakSpot(weakSpot.bounds, info.Point));
            lastHitSide = side;
            lastHitTime = now;

            var kind = definition.weakness;
            var result = WeaknessRules.Evaluate(kind, ctx, definition.heavySwingSpeed);
            resistedThisHit = result == WeaknessResult.Resisted;
            if (posture != null)
                posture.NextHitGainScale = kind == WeaknessKind.PostureOnly ? WeaknessRules.PostureOnlyPostureMultiplier
                    : resistedThisHit ? 0f : 1f;

            if (result == WeaknessResult.Resisted)
            {
                EventBus.Publish(new HitResisted(info.Point, gameObject));
                if (kind == WeaknessKind.HeavyBreak && ArchetypeMark != null
                    && !(weakSpot != null && GameSettings.Active.ReduceFlashes)) // P8-05: weak spot stays steady
                {
                    // slab flicker: "too slow". A shield keeps its armour material, so its weak spot flickers instead.
                    RendererTint.Apply(weakSpot != null ? weakSpot : ArchetypeMark, GuardMarkColor);
                    slabFlickerUntil = Time.unscaledTime + SlabFlickerSeconds;
                }
                float resist = kind == WeaknessKind.PostureOnly ? WeaknessRules.PostureOnlyHpMultiplier : definition.resistMultiplier;
                return info.Amount * resist;
            }
            if (result == WeaknessResult.Neutral) return info.Amount;

            EventBus.Publish(new WeaknessExploited(info.Point, gameObject, (int)kind));
            FlareWeakPoint();
            if (!staggered && (kind == WeaknessKind.HeavyBreak || kind == WeaknessKind.ParryGate)) ForceStagger(); // not while staggered: prevents stunlock
            return kind == WeaknessKind.Reflect ? health.Max * definition.exploitMultiplier
                : info.Amount * definition.exploitMultiplier;
        }

        protected override void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[Enemy] {msg}", this);
        }

        /// <summary>A3.1: assigns this enemy's definition. The roguelike spawner instantiates
        /// prefabs baked inactive (Unity skips Awake on an inactive instantiate), calls this, then
        /// activates — so Awake's one-time Health/PostureMeter configuration reads the scaled
        /// runtime copy instead of the shared asset, with no reflection into a private field.</summary>
        public void Configure(EnemyDefinition def) => definition = def;

        /// <summary>P3-04: as <see cref="Configure(EnemyDefinition)"/>, plus the arena half-extent that
        /// bounds <see cref="Displace"/> (Enemies cannot see World).</summary>
        public void Configure(EnemyDefinition def, float halfExtent) { definition = def; arenaHalfExtent = halfExtent; }

        /// <summary>P8-02 (D-17): compose the spawn's weapons onto the body's sockets, before activation (like
        /// <see cref="Configure(EnemyDefinition)"/>). The main hand's tip becomes <c>bladeTip</c>; an off-hand
        /// renderer named "Shield" becomes the HeavyBreak guard mark; a <c>forearm</c> off-hand straps to Socket_Shield
        /// (P8-02c). <c>weapon</c> stays the arm bone the chop
        /// swings. Null main = keep the prefab's own kit (bosses). P8-05: a shield grants HeavyBreak (on the per-spawn
        /// definition), wears the body's own armour material, and lights its "WeakSpot" strip.</summary>
        public void Equip(EnemyWeaponDefinition main, EnemyWeaponDefinition off = null)
        {
            if (main == null) return;
            Transform root = main.Attach(FindChild(EnemyWeaponDefinition.MainSocket) ?? weapon);
            bladeTip = root.Find(EnemyWeaponDefinition.TipName);
            bladeBase = root.Find(EnemyWeaponDefinition.EdgeBaseName);
            Renderer shield = null;
            if (off != null)
                foreach (var r in off.Attach((off.forearm ? FindChild(EnemyWeaponDefinition.ShieldSocket) : null)
                                             ?? FindChild(EnemyWeaponDefinition.OffSocket) ?? transform).GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Shield") shield = r;
                    else if (r.name == WeakSpotName) weakSpot = r;
            SetWeaponKit(main, off, shield);
            if (shield == null) { weakSpot = null; return; }
            if (bodyRenderer != null) shield.sharedMaterial = bodyRenderer.sharedMaterial; // shared: no new material/SetPass
            if (weakSpot != null)
            {
                weakSpot.sharedMaterial = VrUiMaterials.SharedUnlit;
                RendererTint.Apply(weakSpot, OpenMarkColor); // steady bright strip: reads in greyscale, never pulses
            }
            shieldGuard = true;
            GrantShieldGuard();
            if (health != null && definition != null) health.DamageFilter = FilterDamage; // equipped after Awake
        }

        /// <summary>P8-05: the shield's weak-spot renderer name (EW_Shield prefab).</summary>
        public const string WeakSpotName = "WeakSpot";
        private Renderer weakSpot;
        private bool shieldGuard;

        /// <summary>P8-05: HeavyBreak follows the shield. <c>definition</c> is the spawner's per-spawn copy (A3.1
        /// Configure), so this never touches the shared asset.</summary>
        private void GrantShieldGuard()
        {
            if (!shieldGuard || definition == null) return;
            definition.weakness = WeaknessKind.HeavyBreak;
            definition.resistMultiplier = WeaknessRules.ShieldResist;
            definition.exploitMultiplier = WeaknessRules.ShieldExploit;
        }

        private Transform FindChild(string childName)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == childName) return t;
            return null;
        }

        /// <summary>Revive and reposition for a fresh duel.</summary>
        public virtual void Respawn(Vector3 position)
        {
            if (definition != null) health.Configure(definition.ScaledMaxHealth(CampaignState.GalaxiesCompleted));
            transform.SetPositionAndRotation(position, Quaternion.identity);
            groundY = position.y;
            currentEuler = restEuler;
            ResetWeaponRest();
            PoseBody(0f, 0f, 0f);
            Tint(IdleColor);
            deflectedThisSwing = false;
            nextAttackTime = 0f;
            Enter(State.Idle);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (weapon == null || bladeTip == null) return;
            bool lateWindup = state == State.Windup && definition != null
                && timer >= definition.telegraphTime * 0.75f;
            if (state != State.Active && !lateWindup) return;
            Gizmos.color = deflectedThisSwing ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(ParryCapsuleStart, parryRadius);
            Gizmos.DrawWireSphere(bladeTip.position, parryRadius);
            Gizmos.DrawLine(ParryCapsuleStart, bladeTip.position);
        }
    }
}
