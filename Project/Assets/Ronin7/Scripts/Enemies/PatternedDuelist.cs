using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Enemies
{
    /// <summary>
    /// Elite enemy that "predicts" repetitive attacks. Tracks damage hits and their spatial side
    /// (left vs right relative to this enemy). Repeated hits from the SAME side within a time window
    /// are mostly refunded via healing. Alternating sides trigger a one-time pattern-broken event.
    /// </summary>
    public class PatternedDuelist : MonoBehaviour
    {
        [SerializeField] private float patternWindow = 4f;
        [SerializeField] private float refundFraction = 0.85f;
        [SerializeField] private UnityEvent onPredicted = new UnityEvent();
        [SerializeField] private UnityEvent onPatternBroken = new UnityEvent();

        private Health health;
        private float lastHitSide;
        private float lastHitTime;
        private bool patternBrokenFired;

        private void OnEnable()
        {
            health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        /// <summary>Handle incoming damage: compute hit side, check for patterns, refund or trigger events.</summary>
        private void OnDamaged(DamageInfo info)
        {
            // Compute which side the hit came from
            Vector3 hitPoint = info.Point;
            float newSide = Mathf.Sign(Vector3.Dot(transform.right, hitPoint - transform.position));

            // Check if this is a repeated hit from the same side
            if (IsRepeatedSide(lastHitSide, newSide) && Time.time - lastHitTime < patternWindow)
            {
                // Refund damage by healing the entity
                float refundAmount = info.Amount * refundFraction;
                health.Heal(refundAmount);
                onPredicted?.Invoke();
            }
            else if (lastHitSide != 0 && !Mathf.Approximately(Mathf.Sign(lastHitSide), Mathf.Sign(newSide)))
            {
                // Pattern broken: attacker switched sides
                if (!patternBrokenFired)
                {
                    patternBrokenFired = true;
                    onPatternBroken?.Invoke();
                }
            }

            // Record this hit for the next frame
            lastHitSide = newSide;
            lastHitTime = Time.time;
        }

        /// <summary>
        /// Helper: returns true if lastSide and newSide are on the same side (same sign).
        /// Used by instance logic and for EditMode-only testing.
        /// </summary>
        public static bool IsRepeatedSide(float lastSide, float newSide)
        {
            return lastSide != 0 && Mathf.Sign(lastSide) == Mathf.Sign(newSide);
        }
    }
}
