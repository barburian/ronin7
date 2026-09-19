using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Enemies
{
    /// <summary>
    /// "Shardborn" mechanic: an enemy whose pooled combat instincts leave an integration gap.
    /// It keeps its guard up most of the time, refunding most incoming damage via healing, and only
    /// briefly "commits" — a telegraphed window during which damage lands at full value. A hit landed
    /// inside the committed window fires a one-time event (the player punishing the half-beat).
    ///
    /// Mirrors <see cref="PatternedDuelist"/>: subscribes to Health.Damaged and refunds with
    /// <see cref="Health.Heal"/> (which no-ops once dead, so a refund can never resurrect a corpse).
    /// A refundFraction &lt; 1 keeps death reachable even through the guard; window hits are always lethal.
    /// </summary>
    public class HesitantAttacker : MonoBehaviour
    {
        [Tooltip("How long the committed (vulnerable) window stays open, in seconds.")]
        [SerializeField] private float windowDuration = 0.6f;
        [Tooltip("Full cycle length: the committed window repeats once every interval.")]
        [SerializeField] private float windowInterval = 3f;
        [Tooltip("Fraction of damage refunded (healed back) while the guard is up. <1 keeps death reachable.")]
        [SerializeField] private float guardRefundFraction = 0.85f;
        [Tooltip("Renderer flashed to telegraph the committed window. Optional.")]
        [SerializeField] private Renderer flashRenderer;
        [SerializeField] private Color flashColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("When true the window cycles on a timer. Disable for deterministic tests that drive IsCommitted directly.")]
        [SerializeField] private bool autoCycle = true;

        [SerializeField] private UnityEvent onWindowExploited = new UnityEvent();

        /// <summary>True while the committed (vulnerable) window is open. Settable for testing when autoCycle is off.</summary>
        public bool IsCommitted { get; set; }

        /// <summary>Public so builders can AddListener after a runtime AddComponent.</summary>
        public UnityEvent OnWindowExploited => onWindowExploited;

        private Health health;
        private bool exploitedFired;
        private MaterialPropertyBlock mpb;
        private Color baseEmission;
        private bool hasBaseEmission;

        private void OnEnable()
        {
            health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }

            if (flashRenderer != null)
            {
                mpb = new MaterialPropertyBlock();
                flashRenderer.GetPropertyBlock(mpb);
                baseEmission = mpb.GetVector("_EmissionColor");
                hasBaseEmission = true;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        private void Update()
        {
            if (autoCycle && windowInterval > 0f)
            {
                float phase = Time.time % windowInterval;
                IsCommitted = phase < windowDuration;
            }

            UpdateTelegraph();
        }

        private void UpdateTelegraph()
        {
            if (flashRenderer == null || !hasBaseEmission) return;
            flashRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", IsCommitted ? flashColor : baseEmission);
            flashRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Damage already subtracted by Health. If the guard is up (not committed), refund most of it.
        /// If committed, the hit stands and we fire the one-time exploited event.
        /// </summary>
        private void OnDamaged(DamageInfo info)
        {
            if (IsCommitted)
            {
                if (!exploitedFired)
                {
                    exploitedFired = true;
                    onWindowExploited?.Invoke();
                }
                return;
            }

            float refund = info.Amount * guardRefundFraction;
            if (refund > 0f)
            {
                health.Heal(refund);
            }
        }
    }
}
