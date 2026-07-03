namespace Ronin7.Player
{
    /// <summary>
    /// Pure summon/cooldown state machine for the Mirror ability (Ch12 "The Fracture" unlock, the
    /// FINAL permanent ability — the Ronin-7 Edition's freed blade-shadow). No UnityEngine dependency,
    /// mirrors <see cref="OverdriveLogic"/>/<see cref="PhaseStepSolver"/> so it is deterministic and
    /// unit-testable apart from the MonoBehaviour that drives it (<see cref="MirrorSummonController"/>).
    ///
    /// Unlike Overdrive's charge meter, Mirror has no build-up: <see cref="TrySummon"/> starts the
    /// active window and the cooldown together (both counted from the same summon moment), and
    /// <see cref="Tick"/> counts down the active window first, then lets the remaining cooldown finish
    /// on its own. <see cref="CanSummon"/> is false both while the phantom is out AND while the
    /// post-despawn cooldown remainder is still running.
    /// </summary>
    public class MirrorSummonLogic
    {
        private readonly float activeDuration;
        private readonly float cooldownSeconds;

        private float activeRemaining;
        private float cooldownRemaining;

        /// <summary>True while the summoned phantom should still be alive in the world.</summary>
        public bool IsActive => activeRemaining > 0f;

        /// <summary>True iff neither the active window nor the cooldown are still running.</summary>
        public bool CanSummon => !IsActive && cooldownRemaining <= 0f;

        public MirrorSummonLogic(float activeDuration, float cooldownSeconds)
        {
            this.activeDuration = activeDuration;
            this.cooldownSeconds = cooldownSeconds;
        }

        /// <summary>
        /// Attempts to summon. Fails (returns false, no state change) if the phantom is already active
        /// or the cooldown from the last summon hasn't elapsed yet.
        /// </summary>
        public bool TrySummon()
        {
            if (!CanSummon) return false;
            activeRemaining = activeDuration;
            cooldownRemaining = cooldownSeconds;
            return true;
        }

        /// <summary>
        /// Advances the active window and the cooldown by <paramref name="dt"/>. Returns true on the
        /// frame the active window ends (the caller should despawn the phantom that frame). No-ops
        /// (returns false) for a non-positive <paramref name="dt"/>.
        /// </summary>
        public bool Tick(float dt)
        {
            if (dt <= 0f) return false;

            bool justEnded = false;
            if (activeRemaining > 0f)
            {
                activeRemaining -= dt;
                if (activeRemaining <= 0f)
                {
                    activeRemaining = 0f;
                    justEnded = true;
                }
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining -= dt;
                if (cooldownRemaining < 0f) cooldownRemaining = 0f;
            }

            return justEnded;
        }

        /// <summary>
        /// Forces the active window to end without touching the cooldown (e.g. an OnDisable/scene
        /// -transition failsafe that despawns the phantom early). Idempotent.
        /// </summary>
        public void ForceEnd() => activeRemaining = 0f;
    }
}
