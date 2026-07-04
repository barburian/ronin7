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

        private Health health;
        private float current;
        private float lastDamageTime = float.NegativeInfinity;
        private bool applyingBonus;

        /// <summary>Current posture (0..postureMax), for UI/inspection.</summary>
        public float Current => current;

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
        }

        private void OnDamaged(DamageInfo info)
        {
            if (applyingBonus) return; // ignore the bonus hit's own re-entrant Damaged callback

            lastDamageTime = Time.time;
            current = Accumulate(current, info.Amount, gainPerDamage, postureMax);
            if (!IsBroken(current, postureMax)) return;

            current = 0f;
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

        /// <summary>Adds damage-scaled posture, clamped to [0, max].</summary>
        internal static float Accumulate(float current, float damage, float gainPerDamage, float max)
            => Mathf.Clamp(current + damage * gainPerDamage, 0f, max);

        /// <summary>Drains posture at a flat rate, clamped at 0.</summary>
        internal static float Decay(float current, float dt, float decayPerSecond)
            => Mathf.Max(0f, current - decayPerSecond * dt);

        /// <summary>True once posture has reached (or passed) the break threshold.</summary>
        internal static bool IsBroken(float current, float max) => current >= max;
    }
}
