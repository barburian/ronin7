using UnityEngine;
using UnityEngine.Events;
using Ronin7.Core;

namespace Ronin7.Combat
{
    /// <summary>
    /// Boss-duel resolver: finite state machine that tracks opponent health, yields when
    /// threshold is crossed, and accepts the yield once the player sheathes the sword.
    /// </summary>
    public class DuelYield : MonoBehaviour
    {
        public enum DuelState { Fighting, Yielded, Accepted }

        [SerializeField] private Health opponent;
        [SerializeField] [Range(0f, 1f)] private float yieldThreshold = 0.25f;
        [SerializeField] private Behaviour[] disableOnYield;
        // Public so scene builders can wire persistent listeners (DialoguePlayer.Play,
        // MissionDirector.AdvanceFromPrompt) via UnityEventTools at build time.
        public UnityEvent onYielded = new UnityEvent();
        public UnityEvent onAccepted = new UnityEvent();
        [SerializeField] private float autoAcceptSeconds = 0f;
        [SerializeField] private Grabbable sword;

        public DuelState State { get; private set; }

        private float yieldTime;

        private void OnEnable()
        {
            if (opponent != null)
            {
                opponent.Damaged += OnOpponentDamaged;
                opponent.Died += OnOpponentDied;
            }
        }

        private void OnDisable()
        {
            if (opponent != null)
            {
                opponent.Damaged -= OnOpponentDamaged;
                opponent.Died -= OnOpponentDied;
            }
        }

        private void Update()
        {
            if (State != DuelState.Yielded) return;

            bool swordSheathed = sword != null && !sword.IsHeld;
            bool autoAcceptElapsed = autoAcceptSeconds > 0f && Time.time - yieldTime >= autoAcceptSeconds;

            if (swordSheathed || autoAcceptElapsed)
            {
                Accept();
            }
        }

        private void OnOpponentDamaged(DamageInfo info)
        {
            if (State != DuelState.Fighting) return;
            if (opponent == null) return;

            if (ShouldYield(opponent.Current, opponent.Max, yieldThreshold))
            {
                EnterYield();
            }
        }

        private void OnOpponentDied()
        {
            if (State == DuelState.Fighting)
            {
                // Overkill in one hit: yield instead of death. Builder should ensure opponent
                // health is high enough to avoid crossing from above-threshold to 0 in one damage.
                EnterYield();
            }
        }

        /// <summary>Transition to Yielded: disable opponent behaviors, invoke onYielded, record time.</summary>
        public void EnterYield()
        {
            if (State != DuelState.Fighting) return; // never double-fire the yield

            State = DuelState.Yielded;
            yieldTime = Time.time;

            if (disableOnYield != null)
            {
                foreach (var behaviour in disableOnYield)
                {
                    if (behaviour != null) behaviour.enabled = false;
                }
            }

            onYielded?.Invoke();
        }

        /// <summary>Transition to Accepted: invoke onAccepted and disable this component.</summary>
        public void Accept()
        {
            if (State == DuelState.Accepted) return; // never double-fire the acceptance

            State = DuelState.Accepted;
            onAccepted?.Invoke();
            enabled = false;
        }

        /// <summary>Pure-logic helper: true if opponent health fraction is at or below threshold, or health is zero or negative.</summary>
        public static bool ShouldYield(float current, float max, float threshold)
        {
            if (max <= 0f) return false;
            return current <= 0f || current / max <= threshold;
        }

        /// <summary>Test hook: force entry to Yielded state.</summary>
        public void ForceYield() => EnterYield();

        /// <summary>Test hook: force entry to Accepted state.</summary>
        public void ForceAccept() => Accept();
    }
}
