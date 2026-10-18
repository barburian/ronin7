using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Sekiro-style posture/stagger meter, opt-in and placed beside a <see cref="Health"/> on any
    /// damageable entity. Builds from incoming damage; once it crosses <see cref="postureMax"/> it
    /// resets, deals a bonus "death blow" hit via <see cref="Health.ApplyDamage"/>, force-staggers the
    /// entity (if it runs the <see cref="MeleeAttacker"/> FSM), and publishes <see cref="PostureBroken"/>.
    ///
    /// NAMESPACE DEVIATION from the original design (which specified Ronin7.Combat): this component
    /// calls <c>GetComponent&lt;MeleeAttacker&gt;()</c>, and MeleeAttacker lives in Ronin7.Enemies.
    /// Ronin7.Combat's asmdef only references Ronin7.Core (checked); Ronin7.Enemies already references
    /// Ronin7.Combat, so a Combat -> Enemies reference the other way would be circular, which Unity's
    /// asmdef system rejects outright. Living in Ronin7.Enemies (which already sees both Health and
    /// MeleeAttacker) is the minimal fix — the component still sits beside Health on the GameObject, it
    /// just compiles in a different assembly than Health itself, the same way Ronin7.Player's
    /// WeakpointSight/UnbrokenWard already sit beside a Ronin7.Combat Health without living in that
    /// assembly.
    ///
    /// RE-ENTRANCY: the bonus "break" damage is applied through the same <see cref="Health.ApplyDamage"/>
    /// that raised <see cref="Health.Damaged"/> in the first place, which would otherwise re-enter
    /// <see cref="OnDamaged"/> and re-accumulate/re-break. Guarded by <see cref="applyingBonus"/> for the
    /// duration of that one call.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PostureMeter : MonoBehaviour
    {
        [SerializeField] private float postureMax = 100f;
        [SerializeField] private float gainPerDamage = 0.9f;
        [SerializeField] private float decayDelay = 1f;
        [SerializeField] private float decayPerSecond = 40f;
        [SerializeField] private float breakBonusDamage = 12f;
        [SerializeField] private float nearBreakFraction = 0.8f;

        private float gainMultiplier_ = 1f;

        public float BreakBonusDamage { get => breakBonusDamage; set => breakBonusDamage = value; }

        private Health health;
        private float current;
        private float lastDamageTime = float.NegativeInfinity;
        private bool applyingBonus;
        private bool nearBreakArmed = true;

        /// <summary>P3-02: posture-gain scale for the next damaging hit only (0 = resisted, 5 = PostureOnly),
        /// set by Enemy.FilterDamage and reset to 1 once consumed.</summary>
        internal float NextHitGainScale = 1f;

        /// <summary>True while the break bonus hit is being applied (Enemy.FilterDamage passes it through).</summary>
        internal bool ApplyingBonus => applyingBonus;

        /// <summary>Current posture (0..postureMax), for UI/inspection.</summary>
        public float Current => current;

        /// <summary>BUG-39: current / max (0..1), drives the Construct's crack marker.</summary>
        public float Fraction => postureMax > 0f ? current / postureMax : 0f;

        /// <summary>Set the posture-break threshold (used by data-driven enemies so tougher tiers take
        /// longer to stagger — mirrors <see cref="Health.Configure"/>). Resets current posture to 0.
        /// Meters that never call this keep their serialized postureMax exactly as authored.</summary>
        public void Configure(float max) => Configure(max, 1f);

        /// <summary>Also sets a posture-gain multiplier applied on top of gainPerDamage (poise).</summary>
        public void Configure(float max, float gainMultiplier)
        {
            gainMultiplier_ = gainMultiplier;
            postureMax = max;
            current = 0f;
            nearBreakArmed = true;
        }

        private void Awake() => health = GetComponent<Health>();

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void Update()
        {
            if (current <= 0f) return;
            if (Time.time - lastDamageTime < decayDelay) return;
            current = Decay(current, Time.deltaTime, decayPerSecond);
            if (!nearBreakArmed && current < nearBreakFraction * postureMax) nearBreakArmed = true;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (applyingBonus) return; // ignore the bonus hit's own re-entrant Damaged callback

            lastDamageTime = Time.time;
            float previous = current;
            float postureScale = PostureDamageScale(SourceBoon(info.Source, BoonEffectKind.PostureDamage));
            current = Accumulate(current, info.Amount * NextHitGainScale * postureScale, gainPerDamage * gainMultiplier_, postureMax);
            NextHitGainScale = 1f;
            bool broken = IsBroken(current, postureMax);

            if (CrossedNearBreak(previous, current, nearBreakFraction, postureMax, nearBreakArmed, broken))
            {
                nearBreakArmed = false;
                EventBus.Publish(new PostureNearBreak(gameObject));
            }

            if (!broken) return;

            current = 0f;
            nearBreakArmed = true; // meter reset on break -> re-arm for the next approach
            applyingBonus = true;
            health.ApplyDamage(new DamageInfo(breakBonusDamage, transform.position, info.Direction, info.Source));
            applyingBonus = false;

            // The bonus damage can be lethal, and Health fires Died synchronously inside ApplyDamage —
            // by this line the FSM may already be in State.Dead. Forcing Stagger then would overwrite
            // Dead and resurrect the enemy into its post-stagger state (Chase for Enemy), leaking the
            // CombatActivity aggro count and blocking saves. Only stagger the living.
            if (health.IsAlive) GetComponent<MeleeAttacker>()?.ForceStagger();
            EventBus.Publish(new PostureBroken(gameObject));
        }

        // P4-02: the attacker's boon stats, resolved from DamageInfo.Source (cached per source).
        private GameObject modsSource;
        private PlayerCombatModifiers sourceMods;

        private float SourceBoon(GameObject source, BoonEffectKind kind)
        {
            if (source == null) return 0f;
            if (source != modsSource || sourceMods == null)
            {
                modsSource = source;
                sourceMods = source.GetComponentInParent<PlayerCombatModifiers>();
            }
            return sourceMods != null ? sourceMods.Boon(kind) : 0f;
        }

        /// <summary>P4-02 PostureDamage bonus -> posture-gain scale, capped at x2.</summary>
        internal static float PostureDamageScale(float postureBonus) => 1f + Mathf.Clamp(postureBonus, 0f, 1f);

        /// <summary>Adds damage-scaled posture, clamped to [0, max].</summary>
        internal static float Accumulate(float current, float damage, float gainPerDamage, float max)
            => Mathf.Clamp(current + damage * gainPerDamage, 0f, max);

        /// <summary>Drains posture at a flat rate, clamped at 0.</summary>
        internal static float Decay(float current, float dt, float decayPerSecond)
            => Mathf.Max(0f, current - decayPerSecond * dt);

        /// <summary>True once posture has reached (or passed) the break threshold.</summary>
        internal static bool IsBroken(float current, float max) => current >= max;

        /// <summary>
        /// True the instant posture crosses <paramref name="threshold"/> (a fraction of
        /// <paramref name="max"/>) from below — the "near-break" haptic cue. Fires only while
        /// <paramref name="armed"/> (re-armed by the caller once posture decays back below the
        /// threshold), and never on a hit that also crosses through to <paramref name="broken"/> —
        /// the break's own stronger haptic already covers that hit, no double stacking.
        /// </summary>
        internal static bool CrossedNearBreak(float previous, float current, float threshold, float max, bool armed, bool broken)
        {
            if (broken || !armed) return false;
            float thresholdValue = threshold * max;
            return previous < thresholdValue && current >= thresholdValue;
        }
    }
}
