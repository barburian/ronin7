using Ronin7.Combat;
using Ronin7.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Ronin7.Enemies
{
    /// <summary>
    /// "EchoHunter" mechanic: a hunter drone trained on Cipher's operational file.
    /// It predicts the player's repeated attack pattern. While the player keeps striking from the SAME side
    /// within a time window, the drone guards and refunds most damage. A novel/varied strike (different side)
    /// lands full damage and fires a one-time prediction-broken event. This inverts PatternedDuelist's reward
    /// semantics: here, REPETITION is penalized and VARIETY is rewarded (canon: the drone was "programmed
    /// with his movements", so repeating a known move is countered).
    ///
    /// Mirrors <see cref="PatternedDuelist"/>: subscribes to Health.Damaged and refunds with
    /// <see cref="Health.Heal"/> (which no-ops once dead, so a refund can never resurrect a corpse).
    /// A refundFraction &lt; 1 keeps death reachable even through the guard.
    /// </summary>
    public class EchoHunter : MonoBehaviour
    {
        [SerializeField] private float predictionWindow = 4f;
        [SerializeField] private float refundFraction = 0.85f;
        [SerializeField] private Renderer flashRenderer;
        [SerializeField] private Color flashColor = new Color(0.3f, 0.7f, 1f);
        [Tooltip("When true prediction cycles on recorded hits. Disable for deterministic tests that drive IsPredicting directly.")]
        [SerializeField] private bool autoTrack = true;

        [SerializeField] private UnityEvent onPredictionBroken = new UnityEvent();

        /// <summary>True while the drone is predicting (last hit was from the same side within the window). Settable for testing when autoTrack is off.</summary>
        public bool IsPredicting { get; set; }

        /// <summary>Public so builders can AddListener after a runtime AddComponent.</summary>
        public UnityEvent OnPredictionBroken => onPredictionBroken;

        private Health health;
        private float lastHitSide;
        private float lastHitTime;
        private bool predictionBrokenFired;

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
            if (autoTrack)
            {
                // Update prediction state: true if the last hit is still within the window
                if (lastHitSide != 0 && Time.time - lastHitTime < predictionWindow)
                {
                    IsPredicting = true;
                }
                else
                {
                    IsPredicting = false;
                }
            }

            UpdateTelegraph();
        }

        private void UpdateTelegraph()
        {
            if (flashRenderer == null || !hasBaseEmission) return;
            flashRenderer.GetPropertyBlock(mpb);
            mpb.SetColor("_EmissionColor", IsPredicting ? flashColor : baseEmission);
            flashRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>Handle incoming damage: compute hit side, check for prediction, refund or trigger events.</summary>
        private void OnDamaged(DamageInfo info)
        {
            if (!autoTrack)
            {
                // Test-driven mode: IsPredicting decides directly. A predicted hit is refunded;
                // a non-predicted hit is the player breaking the prediction (full damage stands).
                if (IsPredicting)
                {
                    Refund(info.Amount);
                }
                else
                {
                    FirePredictionBroken();
                }
                return;
            }

            // Compute which side the hit came from
            Vector3 hitPoint = info.Point;
            float newSide = Mathf.Sign(Vector3.Dot(transform.right, hitPoint - transform.position));

            if (IsRepeatedSide(lastHitSide, newSide) && Time.time - lastHitTime < predictionWindow)
            {
                // Predicted repeat (same side within the window): refund most of the damage.
                Refund(info.Amount);
            }
            else if (lastHitSide != 0 && Mathf.Sign(lastHitSide) != Mathf.Sign(newSide))
            {
                // Novel/varied strike (different side): prediction broken, full damage stands.
                FirePredictionBroken();
            }

            // Record this hit for the next frame
            lastHitSide = newSide;
            lastHitTime = Time.time;
        }

        /// <summary>Heal back the refunded fraction. Health.Heal no-ops once dead, so it never resurrects.</summary>
        private void Refund(float amount)
        {
            float refundAmount = amount * refundFraction;
            if (refundAmount > 0f)
            {
                health.Heal(refundAmount);
            }
        }

        /// <summary>Fire the prediction-broken event exactly once.</summary>
        private void FirePredictionBroken()
        {
            if (!predictionBrokenFired)
            {
                predictionBrokenFired = true;
                onPredictionBroken?.Invoke();
            }
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
