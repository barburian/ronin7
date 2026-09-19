using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;

namespace Ronin7.World
{
    /// <summary>
    /// "tidal flood" mechanic: a rising water volume that deals pressure damage over time
    /// to a submerged player. The water surface rises from startY to endY over riseDuration,
    /// applying continuous chip damage at damageInterval while the player remains submerged.
    ///
    /// Requires a trigger collider on the same GameObject (BoxCollider isTrigger recommended).
    /// </summary>
    public class FloodingWaterHazard : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("The player Health to damage while submerged.")]
        [SerializeField] private Health playerHealth;

        [Header("Damage")]
        [Tooltip("Pressure damage per tick applied while submerged.")]
        [SerializeField] private float damagePerTick = 6f;
        [Tooltip("Seconds between damage ticks (cooldown per victim).")]
        [SerializeField] private float damageInterval = 1f;

        [Header("Rise Behaviour")]
        [Tooltip("Seconds for water to rise from startY to endY.")]
        [SerializeField] private float riseDuration = 12f;
        [Tooltip("Local Y position when water is empty (rising starts here).")]
        [SerializeField] private float startY = -2f;
        [Tooltip("Local Y position when water is fully flooded.")]
        [SerializeField] private float endY = 2.5f;
        [Tooltip("Auto-start the rise sequence on Awake.")]
        [SerializeField] private bool autoStart = true;

        private float nextDamageTime;
        private float riseElapsed;
        private bool rising;

        /// <summary>Public damage rate for test inspection.</summary>
        public float DamagePerTick => damagePerTick;

        /// <summary>Public damage interval for test inspection.</summary>
        public float DamageInterval => damageInterval;

        /// <summary>Editor/runtime wiring entry point (mirrors AsteroidHazard.Configure).</summary>
        public void Configure(Health player)
        {
            playerHealth = player;
        }

        /// <summary>Begin the rising animation.</summary>
        public void StartRising()
        {
            rising = true;
            riseElapsed = 0f;
        }

        /// <summary>
        /// Attempt to apply pressure damage to a target.
        /// Returns false if target is null, dead, or on cooldown; true if damage was applied.
        /// </summary>
        public bool TryApplyPressure(Health target, float now)
        {
            if (target == null || !target.IsAlive || now < nextDamageTime)
                return false;

            target.ApplyDamage(new DamageInfo(damagePerTick, transform.position, Vector3.up, gameObject));
            nextDamageTime = now + damageInterval;
            return true;
        }

        private void Start()
        {
            // Initialize water height to empty state
            Vector3 pos = transform.localPosition;
            pos.y = startY;
            transform.localPosition = pos;

            // Auto-start if configured
            if (autoStart)
                StartRising();
        }

        private void Update()
        {
            if (!rising) return;

            riseElapsed += Time.deltaTime;
            // Compute normalized rise progress [0, 1]
            float t = riseDuration <= 0f ? 1f : Mathf.Clamp01(riseElapsed / riseDuration);

            // Lerp water height from startY to endY
            Vector3 pos = transform.localPosition;
            pos.y = Mathf.Lerp(startY, endY, t);
            transform.localPosition = pos;

            // Stop rising once fully flooded
            if (t >= 1f)
                rising = false;
        }

        /// <summary>Apply pressure damage to any Health touching the water volume.</summary>
        private void OnTriggerStay(Collider other)
        {
            // Find Health on the object or its root
            Health health = other.GetComponentInParent<Health>();
            if (health == null) return;

            // Only damage the player (identified by CharacterController component)
            if (health.GetComponent<CharacterController>() == null) return;

            // Try to apply damage with cooldown
            TryApplyPressure(health, Time.time);
        }
    }
}
