using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.World
{
    /// <summary>
    /// EP28 "failsafe erosion pulse" mechanic. Models Cipher's neural-purge clock spiking under
    /// stress. When triggered, the pulse runs for a fixed duration during which the player's vision
    /// degrades (tracked as a [0,1] meter that an overlay reads) and the player is more vulnerable
    /// to incoming damage. The controller also self-applies periodic small "erosion" damage to the
    /// player's Health while active, representing the felt vulnerability of the pulse state.
    /// Mirrors the shape of <see cref="CryoChillController"/> (a timed state with <c>Tick(dt)</c>,
    /// public state, one-shot UnityEvents, and an autoAdvance toggle for deterministic tests).
    /// </summary>
    public class FailsafeErosionPulse : MonoBehaviour
    {
        [SerializeField] private float pulseDuration = 30f;
        [SerializeField] private float damageTakenMultiplierWhileActive = 1.5f;
        [SerializeField] private float erosionDamagePerTick = 3f;
        [SerializeField] private float erosionInterval = 2f;
        [SerializeField] private Health playerHealth;
        [SerializeField] private Renderer vignetteRenderer;
        [Tooltip("Auto-trigger the pulse shortly after the scene starts (for scripted mid-combat pulses).")]
        [SerializeField] private bool triggerOnStart = false;
        [SerializeField] private float triggerDelay = 3f;
        [Tooltip("When true, Update() advances the pulse timer and applies erosion damage. Disable for deterministic tests.")]
        [SerializeField] private bool autoAdvance = true;

        public UnityEvent onPulseStart = new UnityEvent();
        public UnityEvent onPulseEnd = new UnityEvent();

        private float remaining;
        private bool isActive;
        private float damageTimer;
        private float startCountdown = -1f;
        /// <summary>The duration actually in effect for the current pulse (may differ from the
        /// configured pulseDuration when TriggerPulse was called with an explicit override).</summary>
        private float activeDuration = 1f;
        /// <summary>Vignette RGB captured once from the shared material so per-frame updates only vary alpha.</summary>
        private Color vignetteBaseColor = Color.black;
        /// <summary>Last alpha written to the vignette MPB; UpdateVignette no-ops when unchanged.</summary>
        private float lastVignetteAlpha = -1f;

        /// <summary>True while the erosion pulse is active.</summary>
        public bool IsActive => isActive;

        /// <summary>
        /// Vision degradation in [0,1]: 0 when inactive, 1 at pulse start, easing linearly to 0
        /// as the pulse expires. Used by a camera-space overlay to degrade the player's vision.
        /// </summary>
        public float VisionDegradation => isActive ? Mathf.Clamp01(remaining / activeDuration) : 0f;

        /// <summary>Damage multiplier while the pulse is active; 1.0 when inactive.</summary>
        public float DamageTakenMultiplier => isActive ? damageTakenMultiplierWhileActive : 1f;

        /// <summary>When true, Update() advances the pulse timer and applies erosion damage automatically. Exposed for tests.</summary>
        public bool AutoAdvance { get => autoAdvance; set => autoAdvance = value; }

        private void Awake()
        {
            // Auto-wire the player's Health when placed on the rig and left unset in the inspector.
            if (playerHealth == null) playerHealth = GetComponent<Health>();
            // Read via sharedMaterial (not .material) so this doesn't clone a material instance.
            if (vignetteRenderer != null && vignetteRenderer.sharedMaterial != null)
                vignetteBaseColor = vignetteRenderer.sharedMaterial.color;
        }

        private void Start()
        {
            if (triggerOnStart) startCountdown = triggerDelay;
        }

        private void Update()
        {
            if (!autoAdvance) return;
            if (startCountdown >= 0f)
            {
                startCountdown -= Time.deltaTime;
                if (startCountdown <= 0f)
                {
                    startCountdown = -1f;
                    TriggerPulse();
                }
            }
            Tick(Time.deltaTime);
            TickDamage(Time.deltaTime);
            UpdateVignette();
        }

        /// <summary>
        /// Trigger the erosion pulse. If duration is positive, use it; otherwise use pulseDuration.
        /// Only fires onPulseStart when transitioning from inactive to active.
        /// </summary>
        public void TriggerPulse(float duration = -1f)
        {
            bool wasActive = isActive;
            activeDuration = Mathf.Max(0.0001f, duration > 0f ? duration : pulseDuration);
            remaining = activeDuration;
            isActive = true;
            damageTimer = 0f;
            if (!wasActive)
            {
                onPulseStart?.Invoke();
            }
        }

        /// <summary>Advance the pulse timer; deactivates and fires onPulseEnd once when time expires.</summary>
        public void Tick(float dt)
        {
            if (!isActive) return;
            remaining -= dt;
            if (remaining <= 0f)
            {
                remaining = 0f;
                isActive = false;
                onPulseEnd?.Invoke();
            }
        }

        /// <summary>
        /// Advance the erosion damage timer; applies erosionDamagePerTick to the player's Health
        /// each time erosionInterval elapses while the pulse is active. At most one tick is
        /// applied per call.
        /// </summary>
        public void TickDamage(float dt)
        {
            if (!isActive || playerHealth == null) return;
            damageTimer += dt;
            if (damageTimer >= erosionInterval)
            {
                damageTimer -= erosionInterval;
                playerHealth.ApplyDamage(new DamageInfo(
                    erosionDamagePerTick, playerHealth.transform.position, Vector3.zero, gameObject));
            }
        }

        /// <summary>Update the vignette renderer's alpha (via MPB, no material clone) to match the vision degradation value.</summary>
        public void UpdateVignette()
        {
            if (vignetteRenderer == null) return;
            float alpha = VisionDegradation;
            if (Mathf.Approximately(alpha, lastVignetteAlpha)) return;
            lastVignetteAlpha = alpha;
            Color color = vignetteBaseColor;
            color.a = alpha;
            RendererTint.Apply(vignetteRenderer, color);
        }

        /// <summary>Reset the pulse to inactive and clear all timers (fires onPulseEnd once if it was active).</summary>
        public void Clear()
        {
            bool was = isActive;
            isActive = false;
            remaining = 0f;
            damageTimer = 0f;
            if (was) onPulseEnd?.Invoke();
        }
    }
}
