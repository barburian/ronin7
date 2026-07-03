namespace Ronin7.Player
{
    /// <summary>
    /// Pure charge/burst state machine for the Overdrive ability (Ch9 "The Pit and the Deep" unlock —
    /// Vane/Wraith-6's freed blade-shadow). No UnityEngine dependency, mirrors
    /// <c>Ronin7.World.HeatLogic</c> / <c>WeakpointSightState</c> so it is deterministic and
    /// unit-testable apart from the MonoBehaviour that drives it (<see cref="OverdriveController"/>).
    ///
    /// Charge is normalized 0..1: <see cref="AddCharge"/> is the sword-hit deposit, <see cref="Tick"/>
    /// drains it while active. Activation is gated on <c>Charge >= activationThreshold</c>; the burst
    /// auto-ends the instant charge hits empty (no lingering "spent" state to clear separately).
    /// </summary>
    public class OverdriveLogic
    {
        private readonly float chargePerHit;
        private readonly float drainPerSecond;
        private readonly float activationThreshold;

        /// <summary>Current charge, clamped to [0, 1].</summary>
        public float Charge { get; private set; }

        /// <summary>True while the time-dilation burst is running.</summary>
        public bool IsActive { get; private set; }

        public OverdriveLogic(float chargePerHit, float drainPerSecond, float activationThreshold)
        {
            this.chargePerHit = chargePerHit;
            this.drainPerSecond = drainPerSecond;
            this.activationThreshold = activationThreshold;
        }

        /// <summary>
        /// Deposits charge from a landed sword hit (<paramref name="hits"/> lets a caller batch multiple
        /// impacts in one call; defaults to a single hit). No-ops while already active — charge does not
        /// build mid-burst, only between bursts — and ignores non-positive amounts.
        /// </summary>
        public void AddCharge(float hits = 1f)
        {
            if (IsActive || hits <= 0f) return;
            Charge = Clamp01(Charge + chargePerHit * hits);
        }

        /// <summary>
        /// Attempts to start the burst. Fails (returns false, no state change) if already active or
        /// charge hasn't reached <c>activationThreshold</c>.
        /// </summary>
        public bool TryActivate()
        {
            if (IsActive || Charge < activationThreshold) return false;
            IsActive = true;
            return true;
        }

        /// <summary>
        /// Drains charge over elapsed time while active. Returns true the frame charge empties and the
        /// burst auto-ends, so the caller can run its own one-shot end-of-burst logic (e.g. restoring
        /// Time.timeScale). No-ops (returns false) when inactive or <paramref name="dt"/> is non-positive.
        /// </summary>
        public bool Tick(float dt)
        {
            if (!IsActive || dt <= 0f) return false;
            Charge = Clamp01(Charge - drainPerSecond * dt);
            if (Charge <= 0f)
            {
                IsActive = false;
                return true;
            }
            return false;
        }

        /// <summary>Forces the inactive state without draining charge (e.g. an OnDisable failsafe). Idempotent.</summary>
        public void Deactivate() => IsActive = false;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
