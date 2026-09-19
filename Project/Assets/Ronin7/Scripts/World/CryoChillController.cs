using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// "Thermopause" cold-survival mechanic. Models the player's body temperature as a
    /// "chill" meter in [0,1] (0 = warm, 1 = fully frozen). Chill rises continuously while the
    /// player is in the supercooled ice-world / cryo-vault environment, and falls while they
    /// stand inside a <see cref="HeatVent"/> (reactor vent / storm-shelter / fire pocket).
    ///
    /// When chill reaches <c>frostbiteThreshold</c> the player enters the frostbite state and
    /// takes periodic minor damage via their <see cref="Health"/>; warming back below the
    /// threshold clears it. Mirrors the shape of <see cref="PollenHazeController"/> (a [0,1]
    /// meter with <c>Tick(dt)</c>, public state, one-shot UnityEvents, and an autoAdvance toggle
    /// for deterministic tests).
    /// </summary>
    public class CryoChillController : MonoBehaviour
    {
        [SerializeField] private float chillRiseRate = 0.06f;     // chill gained per second in the cold
        [SerializeField] private float warmRate = 0.35f;          // chill shed per second inside a HeatVent
        [SerializeField] private float frostbiteThreshold = 0.85f; // chill at which frostbite begins
        [SerializeField] private float damagePerTick = 4f;
        [SerializeField] private float damageInterval = 1.5f;
        [SerializeField] private Health playerHealth;
        [Tooltip("When true, Update() advances chill + frostbite damage. Disable for deterministic tests.")]
        [SerializeField] private bool autoAdvance = true;

        public UnityEvent onFrostbite = new UnityEvent();
        public UnityEvent onCleared = new UnityEvent();

        private float chill;
        private bool isFrostbitten;
        private float damageTimer;

        /// <summary>Current chill in [0,1]: 0 = warm, 1 = fully frozen.</summary>
        public float Chill => chill;

        /// <summary>True once chill crosses the frostbite threshold, until the player warms back below it.</summary>
        public bool IsFrostbitten => isFrostbitten;

        /// <summary>When true, Update() advances chill + frostbite damage automatically. Exposed for tests.</summary>
        public bool AutoAdvance { get => autoAdvance; set => autoAdvance = value; }

        private void Awake()
        {
            // Auto-wire the player's Health when placed on the rig and left unset in the inspector.
            if (playerHealth == null) playerHealth = GetComponent<Health>();
        }

        private void Update()
        {
            if (!autoAdvance) return;
            Tick(Time.deltaTime);
            TickDamage(Time.deltaTime);
        }

        /// <summary>Raise chill by chillRiseRate * dt (the ambient cold of the scene).</summary>
        public void Tick(float dt) => SetChill(chill + chillRiseRate * dt);

        /// <summary>Lower chill by warmRate * dt. Called by <see cref="HeatVent"/> while the player is inside it.</summary>
        public void Warm(float dt) => SetChill(chill - warmRate * dt);

        /// <summary>
        /// Set chill to a clamped value. Fires onFrostbite once when crossing up through the
        /// threshold, and onCleared once when warming back below it.
        /// </summary>
        public void SetChill(float v)
        {
            chill = Mathf.Clamp01(v);

            if (!isFrostbitten && chill >= frostbiteThreshold)
            {
                isFrostbitten = true;
                damageTimer = 0f;
                onFrostbite?.Invoke();
            }
            else if (isFrostbitten && chill < frostbiteThreshold)
            {
                isFrostbitten = false;
                onCleared?.Invoke();
            }
        }

        /// <summary>
        /// Advance the frostbite damage timer; applies damagePerTick to the player's Health each
        /// time damageInterval elapses while frostbitten. At most one tick is applied per call.
        /// </summary>
        public void TickDamage(float dt)
        {
            if (!isFrostbitten || playerHealth == null) return;
            damageTimer += dt;
            if (damageTimer >= damageInterval)
            {
                damageTimer -= damageInterval;
                playerHealth.ApplyDamage(new DamageInfo(
                    damagePerTick, playerHealth.transform.position, Vector3.zero, gameObject));
            }
        }

        /// <summary>Reset chill to 0 and clear the frostbite state (fires onCleared once if it was active).</summary>
        public void Clear()
        {
            bool was = isFrostbitten;
            chill = 0f;
            isFrostbitten = false;
            damageTimer = 0f;
            if (was) onCleared?.Invoke();
        }
    }
}
