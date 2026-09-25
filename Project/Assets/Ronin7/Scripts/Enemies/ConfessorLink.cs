using UnityEngine;

namespace Ronin7.Enemies
{
    /// <summary>
    /// "The Requiem Protocol" mechanic: the confessor-link duel of wills. During a duel, Cipher's
    /// neural link to the dying tries to "open and feed" — a hunger that rises while the link is active,
    /// which the player must hold back (restrain) by fighting clean rather than for the kill. If hunger
    /// fills completely (reaches consumeThreshold), Cipher is "consumed" (fail state). Winning the duel
    /// with hunger below the threshold means Cipher REFUSED the hunger.
    ///
    /// This is a transform/state-only mechanic, non-invasive: it does not touch MeleeAttacker or Health
    /// internals. It tracks hunger rise/decay based on linkActive and restrained flags, and can be driven
    /// by external systems (e.g., DuelYield).
    ///
    /// Set <see cref="autoTick"/> false to freeze hunger updates and drive state manually (deterministic tests).
    /// No per-frame allocation.
    /// </summary>
    public class ConfessorLink : MonoBehaviour
    {
        [SerializeField] private float consumeThreshold = 1f;

        [Header("Link Pressure")]
        [SerializeField] private float riseRate = 0.18f;
        [SerializeField] private float decayRate = 0.35f;

        [SerializeField] private bool linkActive = false;

        [Tooltip("When false, hunger holds so tests can drive Tick manually.")]
        [SerializeField] private bool autoTick = true;

        private float hunger;
        private bool restrained;
        private bool consumedFired;
        private bool resolved;
        private bool refused;

        public float Hunger => hunger;
        public bool LinkActive => linkActive;
        public bool Restrained => restrained;
        public bool IsConsumed => hunger >= consumeThreshold;
        public bool Resolved => resolved;
        public bool Refused => refused;

        private void Update()
        {
            if (!autoTick) return;

            Tick(Time.deltaTime);
        }

        /// <summary>Advance the hunger state by dt based on link activity and restrain status.</summary>
        public void Tick(float dt)
        {
            if (!linkActive) return;

            if (restrained)
                hunger -= decayRate * dt;
            else
                hunger += riseRate * dt;

            // Clamp hunger to [0, consumeThreshold].
            hunger = Mathf.Clamp(hunger, 0f, consumeThreshold);

            // One-shot: fire consumed callback when hunger reaches threshold.
            if (hunger >= consumeThreshold && !consumedFired)
            {
                consumedFired = true;
                // TODO: Trigger consumed feedback (VFX, SFX, etc.) here.
            }
        }

        /// <summary>Set whether the duel-of-wills link is active.</summary>
        public void SetLinkActive(bool v)
        {
            linkActive = v;
        }

        /// <summary>Set whether the player is restrained (holding back, not fighting to kill).</summary>
        public void Restrain(bool v)
        {
            restrained = v;
        }

        /// <summary>Spike hunger by the given amount (e.g., from a reckless/lethal strike). Clamped to [0, consumeThreshold].</summary>
        public void Feed(float amount)
        {
            hunger += amount;
            hunger = Mathf.Clamp(hunger, 0f, consumeThreshold);

            // One-shot: fire consumed callback if Feed causes the threshold to be crossed.
            if (hunger >= consumeThreshold && !consumedFired)
            {
                consumedFired = true;
                // TODO: Trigger consumed feedback (VFX, SFX, etc.) here.
            }
        }

        /// <summary>Resolve the duel-of-wills: mark the link as resolved and compute refused state.</summary>
        public void Resolve()
        {
            resolved = true;
            refused = !IsConsumed;
        }
    }
}
