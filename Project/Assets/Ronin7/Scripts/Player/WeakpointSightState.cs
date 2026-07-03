namespace Ronin7.Player
{
    /// <summary>
    /// Pure on/off state machine for the weakpoint-sight ability (Ch7 "Forgotten Names" unlock): the
    /// active/inactive toggle and the damage multiplier each state implies. No UnityEngine dependency,
    /// mirrors <c>Ronin7.World.HeatLogic</c> / <c>Ronin7.World.Story.MultiObjectiveLogic</c> so it is
    /// deterministic and unit-testable apart from the MonoBehaviour that drives it
    /// (<see cref="WeakpointSight"/>).
    /// </summary>
    public class WeakpointSightState
    {
        private readonly float activeMultiplier;

        /// <summary>True while weakpoint-sight is toggled on.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The damage multiplier for the current state: the configured multiplier while
        /// active, 1 (no effect) while inactive.</summary>
        public float DamageMultiplier => IsActive ? activeMultiplier : 1f;

        public WeakpointSightState(float activeMultiplier)
        {
            this.activeMultiplier = activeMultiplier;
        }

        /// <summary>Flips the active state. Returns the new value.</summary>
        public bool Toggle()
        {
            IsActive = !IsActive;
            return IsActive;
        }

        /// <summary>Forces the inactive state (e.g. an OnDisable failsafe). Idempotent.</summary>
        public void Deactivate() => IsActive = false;
    }
}
